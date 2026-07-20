using System.Buffers.Binary;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dds;
using HavenStudio.Formats.Dld;
using HavenStudio.Formats.Dlz;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Mdn;
using HavenStudio.Formats.Txn;
using HavenStudio.Tests.TestSupport;
using HavenStudio.Utils;

namespace HavenStudio.Tests.Formats;

public sealed class DocumentFormatTests
{
    [Fact]
    public void Gcx_write_read_preserves_scripts_and_metadata()
    {
        var document = new Gcx
        {
            Timestamp = 0x12345678,
            CryptoSeed = 0,
            MainScript = new GcxScript([0x8E, 0x00, 0x00, 0x04])
        };
        document.ScriptDefinitions.Add(new GcxScriptDefinition(type: 0, offset: 0)
        {
            Script = new GcxScript([0x10, 0x20, 0x30, 0x40])
        });

        using var stream = new MemoryStream();
        GcxFile.Write(stream, document);
        stream.Position = 0;

        var restored = GcxFile.Read(stream);

        Assert.Equal(0x12345678, restored.Timestamp);
        Assert.Equal(0, restored.CryptoSeed);
        Assert.Equal([0x8E, 0x00, 0x00, 0x04], restored.MainScript.Bytes);
        var definition = Assert.Single(restored.ScriptDefinitions);
        Assert.Equal(0, definition.Type);
        Assert.Equal([0x10, 0x20, 0x30, 0x40], definition.Script!.Bytes);
    }

    [Fact]
    public void Gcx_read_write_preserves_encrypted_string_section_and_padding()
    {
        var document = new Gcx
        {
            Timestamp = 123,
            CryptoSeed = unchecked((int)0x4D26CEAF),
            StringSectionPadding = [0x46],
            MainScript = new GcxScript([0x8E, 0x00])
        };
        document.StringDefinitions.Add(new GcxStringDefinition(type: 0x80, offset: 0)
        {
            Value = "encrypted fixture"
        });

        using var source = new MemoryStream();
        GcxFile.Write(source, document);
        var sourceBytes = source.ToArray();
        source.Position = 0;
        var restored = GcxFile.Read(source);
        using var rewritten = new MemoryStream();
        GcxFile.Write(rewritten, restored);

        Assert.Equal("encrypted fixture", Assert.Single(restored.StringDefinitions).Value);
        Assert.Equal([0x46], restored.StringSectionPadding);
        Assert.Equal(sourceBytes, rewritten.ToArray());
    }

    [Fact]
    public void Gcx_read_write_preserves_table_order_when_script_offsets_are_out_of_order()
    {
        var document = new Gcx
        {
            StringSectionPadding = [],
            MainScript = new GcxScript([0x8E])
        };
        document.ScriptDefinitions.Add(new GcxScriptDefinition(0, 0)
        {
            Script = new GcxScript([0x10])
        });
        document.ScriptDefinitions.Add(new GcxScriptDefinition(1, 0)
        {
            Script = new GcxScript([0x20, 0x21])
        });
        document.ScriptDefinitions.Add(new GcxScriptDefinition(2, 0)
        {
            Script = new GcxScript([0x30, 0x31, 0x32])
        });

        using var initial = new MemoryStream();
        GcxFile.Write(initial, document);
        var sourceBytes = initial.ToArray();
        var secondDefinition = sourceBytes.AsSpan(8, sizeof(int)).ToArray();
        sourceBytes.AsSpan(12, sizeof(int)).CopyTo(sourceBytes.AsSpan(8, sizeof(int)));
        secondDefinition.CopyTo(sourceBytes.AsSpan(12, sizeof(int)));

        using var source = new MemoryStream(sourceBytes);
        var restored = GcxFile.Read(source);
        using var rewritten = new MemoryStream();
        GcxFile.Write(rewritten, restored);

        Assert.Collection(
            restored.ScriptDefinitions,
            definition =>
            {
                Assert.Equal(0, definition.Type);
                Assert.Equal([0x10], definition.Script!.Bytes);
            },
            definition =>
            {
                Assert.Equal(2, definition.Type);
                Assert.Equal([0x30, 0x31, 0x32], definition.Script!.Bytes);
            },
            definition =>
            {
                Assert.Equal(1, definition.Type);
                Assert.Equal([0x20, 0x21], definition.Script!.Bytes);
            });
        Assert.Equal(sourceBytes, rewritten.ToArray());
    }

    [Fact]
    public void Gcx_json_round_trip_preserves_document_content()
    {
        var document = new Gcx
        {
            Timestamp = 42,
            CryptoSeed = 7,
            StringSectionPadding = [0x46, 0x4F, 0x4E],
            MainScript = new GcxScript([0x01, 0x02])
        };
        document.StringDefinitions.Add(new GcxStringDefinition(type: 0x80, offset: 0)
        {
            Value = "fixture"
        });

        var json = GcxJsonIO.Serialize(GcxJsonConverter.ToJsonModel(document));
        var restored = GcxJsonConverter.FromJsonModel(GcxJsonIO.Deserialize(json));

        Assert.Equal(42, restored.Timestamp);
        Assert.Equal(7, restored.CryptoSeed);
        Assert.Equal([0x46, 0x4F, 0x4E], restored.StringSectionPadding);
        Assert.Equal([0x01, 0x02], restored.MainScript.Bytes);
        Assert.Equal("fixture", Assert.Single(restored.StringDefinitions).Value);
    }

    [Fact]
    public void Gcx_read_rejects_section_offset_outside_file()
    {
        var document = new Gcx { MainScript = new GcxScript(Array.Empty<byte>()) };
        using var stream = new MemoryStream();
        GcxFile.Write(stream, document);
        var bytes = stream.ToArray();

        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 8);
        using var malformed = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => GcxFile.Read(malformed));
    }

    [Fact]
    public void Minimal_mdn_reader_preserves_header_and_bounds()
    {
        using var stream = BuildMinimalMdnFixture();

        var restored = MdnFile.Read(stream);

        Assert.True(restored.BigEndian);
        Assert.Equal(unchecked((int)0x89ABCDEF), restored.TxnFilenameHash);
        Assert.NotNull(restored.Bounds);
        Assert.Equal(10, restored.Bounds.MaxX);
        Assert.Equal(-30, restored.Bounds.MinZ);
        Assert.Empty(restored.VertexIndices);
        Assert.Empty(restored.Materials);
    }

    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public void Minimal_mdn_write_read_round_trip(Endianness endianness)
    {
        var document = CreateMinimalMdn();

        using var stream = new MemoryStream();
        MdnFile.Write(stream, document, endianness);
        stream.Position = 0;

        var restored = MdnFile.Read(stream);

        Assert.Equal(document.TxnFilenameHash, restored.TxnFilenameHash);
        Assert.Equal(endianness == Endianness.Big, restored.BigEndian);
        Assert.NotNull(restored.Bounds);
        Assert.Equal(document.Bounds!.MaxX, restored.Bounds.MaxX);
    }

    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public void Txn_stream_round_trip_preserves_image_and_lookup_metadata(Endianness endianness)
    {
        var document = new TxnFile();
        document.Images.Add(new TxnImage(
            width: 8,
            height: 4,
            fourCC: 11,
            flag: 2,
            offset: 0x100,
            mipmapOffset: 0x200));
        var info = new TxnInfo(
            materialId: 0x11223344,
            objectId: 0x55667788,
            width: 8,
            height: 4,
            positionX: 1,
            positionY: 2,
            offset: 0,
            weightX: 1,
            weightY: 0.5f,
            weightX2: 0.25f,
            weightY2: 0.75f);
        document.ImageInfo.Add(info);
        document.IndexLookup[info] = 0;

        using var stream = new MemoryStream();
        document.Save(stream, endianness);
        stream.Position = 0;
        var restored = new TxnFile(stream, endianness);

        var image = Assert.Single(restored.Images);
        Assert.Equal((ushort)8, image.Width);
        Assert.Equal((ushort)4, image.Height);
        Assert.Equal((ushort)11, image.FourCC);
        var restoredInfo = Assert.Single(restored.ImageInfo);
        Assert.Equal(0x11223344u, restoredInfo.TexId);
        Assert.Equal(0x55667788u, restoredInfo.TriId);
        Assert.Equal(0, restored.GetIndex(restoredInfo));
    }

    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public void Dld_stream_round_trip_preserves_texture_payload(Endianness endianness)
    {
        var payload = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
        var document = new DldFile();
        document.Textures.Add(new DldTexture(
            type: 1,
            prio: DldPriority.Main,
            hashId: 0xAABBCCDD,
            parentDataSize: (uint)payload.Length,
            dataSize: (uint)payload.Length,
            mipmapCount: 1,
            entryNumber: 3,
            data: payload));

        using var stream = new MemoryStream();
        document.Save(stream, endianness);
        stream.Position = 0;
        var restored = new DldFile(stream, endianness);

        var texture = Assert.Single(restored.Textures);
        Assert.Equal(0xAABBCCDDu, texture.HashId);
        Assert.Equal(3u, texture.EntryNumber);
        Assert.Equal(payload, texture.Data);
    }

    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public void Dlz_stream_round_trip_unpack_restores_uncompressed_payload(Endianness endianness)
    {
        var payload = Enumerable.Range(0, 257).Select(i => (byte)(i % 251)).ToArray();
        var compressed = Compression.DeflateBuffer(payload);
        var document = new DlzFile(
        [
            new DlzDataContainer(compressed.Length, payload.Length, compressed)
        ]);

        using var stream = new MemoryStream();
        document.Save(stream, endianness);
        stream.Position = 0;
        var restored = new DlzFile(stream, endianness);
        using var unpacked = new MemoryStream();
        restored.Unpack(unpacked);

        Assert.Single(restored.Segs);
        Assert.Equal(payload, unpacked.ToArray());
    }

    [Fact]
    public void Dlz_read_write_preserves_padding_reserved_slots_and_spillover_chunks()
    {
        const int segmentSize = 0x20000;
        var firstPayload = Enumerable.Range(0, 257).Select(i => (byte)(i % 251)).ToArray();
        var finalPayload = Enumerable.Range(0, 129).Select(i => (byte)(255 - i)).ToArray();
        var firstSegmentFile = BuildDlzFixture(firstPayload);
        var finalSegmentFile = BuildDlzFixture(finalPayload);

        var firstSlot = new byte[segmentSize];
        firstSegmentFile.CopyTo(firstSlot, 0);
        for (var i = 0x1000; i < 0x1100; i++)
        {
            firstSlot[i] = (byte)((i * 17) | 1);
        }

        var opaqueSlot = new byte[segmentSize];
        for (var i = 0; i < opaqueSlot.Length; i++)
        {
            opaqueSlot[i] = (byte)((i * 31 + 7) % 251 + 1);
        }

        for (var i = finalSegmentFile.Length - 128; i < finalSegmentFile.Length; i++)
        {
            finalSegmentFile[i] = (byte)((i * 13) | 1);
        }

        var sourceBytes = new byte[(segmentSize * 2) + finalSegmentFile.Length];
        firstSlot.CopyTo(sourceBytes, 0);
        opaqueSlot.CopyTo(sourceBytes, segmentSize);
        finalSegmentFile.CopyTo(sourceBytes, segmentSize * 2);

        var compressedFirstPayload = Compression.DeflateBuffer(firstPayload);
        var spilloverStart = segmentSize - 4;
        var spilloverSegmentSize = (spilloverStart + compressedFirstPayload.Length + 15) & ~15;
        BinaryPrimitives.WriteUInt32LittleEndian(
            sourceBytes.AsSpan(0x0C, 4),
            (uint)spilloverSegmentSize);
        BinaryPrimitives.WriteUInt32BigEndian(sourceBytes.AsSpan(0x14, 4), (uint)spilloverStart + 1);
        compressedFirstPayload.CopyTo(sourceBytes.AsSpan(spilloverStart));

        using var source = new MemoryStream(sourceBytes);
        var document = new DlzFile(source, Endianness.Big);
        using var rewritten = new MemoryStream();
        document.Save(rewritten, Endianness.Big);
        using var unpacked = new MemoryStream();
        document.Unpack(unpacked);

        Assert.Equal(2, document.Segs.Count);
        Assert.Equal(sourceBytes, rewritten.ToArray());
        Assert.Equal(firstPayload.Concat(finalPayload).ToArray(), unpacked.ToArray());
    }

    [Fact]
    public void Dlz_structural_edits_require_explicit_repack()
    {
        var originalPayload = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var replacementPayload = Enumerable.Range(0, 1024).Select(i => (byte)(i % 7)).ToArray();
        var originalCompressed = Compression.DeflateBuffer(originalPayload);
        var document = new DlzFile(
        [
            new DlzDataContainer(
                originalCompressed.Length,
                originalPayload.Length,
                originalCompressed)
        ]);
        var segment = Assert.Single(document.Segs);
        segment.SegData[0] = Compression.DeflateBuffer(replacementPayload);
        segment.SegIndex[0].SizeDecompressed = (ushort)replacementPayload.Length;

        using var invalidOutput = new MemoryStream();
        var exception = Assert.Throws<InvalidOperationException>(
            () => document.Save(invalidOutput, Endianness.Big));
        Assert.Contains("Repack()", exception.Message);

        document.Repack();
        using var output = new MemoryStream();
        document.Save(output, Endianness.Big);
        output.Position = 0;
        var restored = new DlzFile(output, Endianness.Big);
        using var unpacked = new MemoryStream();
        restored.Unpack(unpacked);

        Assert.Equal(replacementPayload, unpacked.ToArray());
    }

    [Fact]
    public void Dds_create_read_preserves_header_and_main_level()
    {
        var block = new byte[] { 0x00, 0xF8, 0xE0, 0x07, 0, 0, 0, 0 };

        using var stream = new MemoryStream();
        DdsFile.Create(stream, height: 4, width: 4, fourCc: "DXT1", mipMapCount: 1, data: block);
        stream.Position = 0;
        var restored = DdsFile.Read(stream);

        Assert.Equal(4, restored.Width);
        Assert.Equal(4, restored.Height);
        Assert.Equal("DXT1", restored.FourCc);
        Assert.Equal(1, restored.MipMapCount);
        Assert.Equal(block, restored.MainData);
        Assert.Empty(restored.MipData);
    }

    [Fact]
    public void Dds_read_rejects_truncated_header()
    {
        using var stream = new MemoryStream(new byte[32]);

        Assert.Throws<InvalidDataException>(() => DdsFile.Read(stream));
    }

    private static Mdn CreateMinimalMdn()
    {
        return new Mdn
        {
            TxnFilenameHash = unchecked((int)0x89ABCDEF),
            Bounds = new MdnBounds
            {
                MaxX = 10,
                MaxY = 20,
                MaxZ = 30,
                MaxW = 1,
                MinX = -10,
                MinY = -20,
                MinZ = -30,
                MinW = 1
            }
        };
    }

    private static byte[] BuildDlzFixture(byte[] payload)
    {
        var compressed = Compression.DeflateBuffer(payload);
        var document = new DlzFile(
        [
            new DlzDataContainer(compressed.Length, payload.Length, compressed)
        ]);
        using var stream = new MemoryStream();
        document.Save(stream, Endianness.Big);
        return stream.ToArray();
    }

    private static MemoryStream BuildMinimalMdnFixture()
    {
        const int fileSize = 0x80;
        var stream = new MemoryStream();
        using (var writer = new EndianBinaryWriter(stream, Endianness.Big, leaveOpen: true))
        {
            writer.Write(MdnFile.Magic);
            writer.Write(unchecked((int)0x89ABCDEF));

            for (var i = 0; i < 8; i++)
            {
                writer.Write(0);
            }

            for (var i = 0; i < 8; i++)
            {
                writer.Write(fileSize);
            }

            writer.Write(fileSize);
            writer.Write(0);
            writer.Write(fileSize);
            writer.Write(0);
            writer.Write(0);
            writer.Write(fileSize);

            CreateMinimalMdn().Bounds!.WriteTo(writer);
        }

        Assert.Equal(fileSize, stream.Length);
        stream.Position = 0;
        return stream;
    }
}
