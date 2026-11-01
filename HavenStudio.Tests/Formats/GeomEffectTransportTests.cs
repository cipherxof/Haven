using System.Buffers.Binary;
using HavenStudio.Extensions;
using HavenStudio.Formats.Geo;

namespace HavenStudio.Tests.Formats;

public sealed class GeomEffectTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Surgical_save_keeps_an_edited_alias_and_synchronizes_repeated_saves(bool editLastAlias)
    {
        var original = BuildGeomFixture();
        var geometry = new GeomFile(new MemoryStream(original, writable: false), Endianness.Big);
        var aliasGeometry = new GeomFile(new MemoryStream(original, writable: false), Endianness.Big);
        try
        {
            var first = Assert.Single(geometry.GeomBlocks);
            var alias = Assert.Single(aliasGeometry.GeomBlocks);
            geometry.GeomBlocks.Add(alias);
            geometry.BlockFaceData.Add(alias, aliasGeometry.BlockFaceData[alias]);
            var firstPrimitive = Assert.Single(geometry.BlockFaceData[first]);
            var aliasPrimitive = Assert.Single(geometry.BlockFaceData[alias]);
            var selected = editLastAlias ? aliasPrimitive : firstPrimitive;
            selected.Poly![0].Attribute = 0x12C0;
            using var output = new MemoryStream();
            geometry.SaveSurgicalEdits(original, output, Endianness.Big);
            var saved = output.ToArray();
            Assert.Equal((ushort)0x12C0,
                BinaryPrimitives.ReadUInt16BigEndian(saved.AsSpan(selected.Offset + 0x26, 2)));
            Assert.Equal((ushort)0x12C0, firstPrimitive.Poly![0].Attribute);
            Assert.Equal((ushort)0x12C0, aliasPrimitive.Poly![0].Attribute);
            Assert.All(original.Select((value, index) => (value, index))
                .Where(item => item.value != saved[item.index]),
                item => Assert.InRange(item.index, selected.Offset + 0x26, selected.Offset + 0x27));
            using var secondOutput = new MemoryStream();
            geometry.SaveSurgicalEdits(saved, secondOutput, Endianness.Big);
            Assert.Equal(saved, secondOutput.ToArray());
            var crypto = new HavenStudio.CryptoService();
            Assert.Equal(saved, crypto.Decrypt(crypto.Encrypt(saved, "stage/n023a"), "stage/n023a"));
        }
        finally { geometry.CloseStream(); aliasGeometry.CloseStream(); }
    }

    [Fact]
    public void Surgical_save_rejects_conflicting_edited_aliases()
    {
        var original = BuildGeomFixture();
        var geometry = new GeomFile(new MemoryStream(original, writable: false), Endianness.Big);
        var aliasGeometry = new GeomFile(new MemoryStream(original, writable: false), Endianness.Big);
        try
        {
            var first = Assert.Single(geometry.GeomBlocks);
            var alias = Assert.Single(aliasGeometry.GeomBlocks);
            geometry.GeomBlocks.Add(alias);
            geometry.BlockFaceData.Add(alias, aliasGeometry.BlockFaceData[alias]);
            Assert.Single(geometry.BlockFaceData[first]).Poly![0].Attribute = 0x12C0;
            Assert.Single(geometry.BlockFaceData[alias]).Poly![0].Attribute = 0x6300;
            using var output = new MemoryStream();
            Assert.Throws<InvalidDataException>(() => geometry.SaveSurgicalEdits(original, output, Endianness.Big));
            Assert.Equal(0, output.Length);
        }
        finally { geometry.CloseStream(); aliasGeometry.CloseStream(); }
    }

    [Fact]
    public void Surgical_edit_save_patches_collision_attributes_without_rebuilding_geom()
    {
        var original = BuildGeomFixture();
        var geometry = new GeomFile(new MemoryStream(original, writable: false), Endianness.Big);
        var block = Assert.Single(geometry.GeomBlocks);
        var primitive = Assert.Single(geometry.BlockFaceData[block]);
        var polygon = Assert.Single(primitive.Poly!);
        block.Attribute = 0x1122334455667788;
        primitive.Attribute = 0x8877665544332211;
        polygon.Attribute = 0x03C0;

        using var output = new MemoryStream();
        geometry.SaveSurgicalEdits(original, output, Endianness.Big);
        geometry.CloseStream();
        var patched = output.ToArray();

        Assert.Equal(original.Length, patched.Length);
        var permitted = new HashSet<int>(
            Enumerable.Range(block.Offset + 0x18, sizeof(ulong))
                .Concat(Enumerable.Range(primitive.Offset + 0x18, sizeof(ulong)))
                .Concat(Enumerable.Range(primitive.Offset + 0x20 + 6, sizeof(ushort))));
        Assert.All(
            original.Select((value, index) => (value, index))
                .Where(item => item.value != patched[item.index]),
            item => Assert.Contains(item.index, permitted));

        var reloaded = new GeomFile(new MemoryStream(patched, writable: false), Endianness.Big);
        var reloadedBlock = Assert.Single(reloaded.GeomBlocks);
        var reloadedPrimitive = Assert.Single(reloaded.BlockFaceData[reloadedBlock]);
        Assert.Equal(0x1122334455667788UL, reloadedBlock.Attribute);
        Assert.Equal(0x8877665544332211UL, reloadedPrimitive.Attribute);
        Assert.Equal((ushort)0x03C0, Assert.Single(reloadedPrimitive.Poly!).Attribute);
        reloaded.CloseStream();
    }

    [Fact]
    public void Surgical_effect_save_preserves_every_byte_outside_chunk_6()
    {
        var source = LoadGeom();
        source.GeomChunk6 = BuildPositionedEffectChunk(name: 0x11223344, x: 1, y: 2, z: 3);
        source.GeoEffects.Clear();
        source.GeoEffects.Add(new GeoEffect
        {
            Name = 0x11223344,
            Index = 2,
            ChunkOffset = 0,
            X = 1,
            Y = 2,
            Z = 3,
            W = 1
        });
        var target = LoadGeom();
        target.TransportEffectsFrom(source);
        using var canonicalOutput = new MemoryStream();
        target.Save(canonicalOutput, Endianness.Big);
        target.CloseStream();
        var canonical = canonicalOutput.ToArray();

        using var canonicalSource = new MemoryStream(canonical, writable: false);
        var geometry = new GeomFile(canonicalSource, Endianness.Big);
        var effect = Assert.Single(geometry.GeoEffects);
        effect.X = 100;
        effect.Y = 200;
        effect.Z = 300;
        geometry.CloseStream();
        using var patchedOutput = new MemoryStream();
        geometry.SaveEffectTransforms(canonical, patchedOutput, Endianness.Big);
        var patched = patchedOutput.ToArray();

        Assert.Equal(canonical.Length, patched.Length);
        using var patchedSource = new MemoryStream(patched, writable: false);
        var reloaded = new GeomFile(patchedSource, Endianness.Big);
        var props = reloaded.GetChunkFromType(GeoChunkType.PROPS)!;
        Assert.Equal(canonical.AsSpan(0, props.DataOffset).ToArray(), patched.AsSpan(0, props.DataOffset).ToArray());
        var propsEnd = props.DataOffset + props.Size;
        Assert.Equal(canonical.AsSpan(propsEnd).ToArray(), patched.AsSpan(propsEnd).ToArray());
        var moved = Assert.Single(reloaded.GeoEffects);
        Assert.Equal(100f, moved.X);
        Assert.Equal(200f, moved.Y);
        Assert.Equal(300f, moved.Z);
        reloaded.CloseStream();
    }

    [Fact]
    public void Surgical_effect_save_can_add_rotation_storage_without_rewriting_earlier_chunks()
    {
        var geometry = LoadGeom();
        geometry.GeomChunk6 = BuildPositionedEffectChunk(name: 0x11223344, x: 1, y: 2, z: 3);
        geometry.GeoEffects.Clear();
        geometry.GeoEffects.Add(new GeoEffect
        {
            Name = 0x11223344,
            Index = 2,
            ChunkOffset = 0,
            X = 1,
            Y = 2,
            Z = 3,
            W = 1
        });
        using var canonicalOutput = new MemoryStream();
        geometry.Save(canonicalOutput, Endianness.Big);
        geometry.CloseStream();
        var canonical = canonicalOutput.ToArray();

        var loaded = new GeomFile(new MemoryStream(canonical, writable: false), Endianness.Big);
        var groups = loaded.GetChunkFromType(GeoChunkType.GROUPS)!;
        var references = loaded.GetChunkFromType(GeoChunkType.REFS)!;
        var layout = GeoEffectChunkBuilder.Capture(loaded.GeomChunk6, loaded.GeoEffects, Endianness.Big);
        var effect = Assert.Single(loaded.GeoEffects);
        effect.Index |= 4 << 10;
        effect.RotationY = MathF.PI / 2f;
        loaded.GeomChunk6 = layout.Rebuild(loaded.GeoEffects);
        loaded.CloseStream();

        using var patchedOutput = new MemoryStream();
        loaded.SaveEffectTransforms(canonical, patchedOutput, Endianness.Big);
        var patched = patchedOutput.ToArray();
        Assert.Equal(canonical.Length + 16, patched.Length);
        Assert.Equal(
            canonical.AsSpan(groups.DataOffset, groups.Size).ToArray(),
            patched.AsSpan(groups.DataOffset, groups.Size).ToArray());
        Assert.Equal(
            canonical.AsSpan(references.DataOffset, references.Size).ToArray(),
            patched.AsSpan(references.DataOffset, references.Size).ToArray());

        var reloaded = new GeomFile(new MemoryStream(patched, writable: false), Endianness.Big);
        Assert.All(reloaded.Header.Chunks, chunk => Assert.Equal(0, chunk.DataOffset & 0x0F));
        var rotated = Assert.Single(reloaded.GeoEffects);
        Assert.Equal(4, GeoEffectLayout.GetRotationSlot(rotated.Index));
        Assert.Equal(MathF.PI / 2f, rotated.RotationY, 4);
        reloaded.CloseStream();
    }

    [Fact]
    public void TransportEffectsFrom_deep_copies_the_source_effects_and_chunk()
    {
        var source = LoadGeom();
        source.GeomChunk6 = BuildSingleEffectChunk(name: 0x11223344);
        source.GeoEffects.Clear();
        source.GeoEffects.Add(new GeoEffect { Name = 0x11223344, Index = 0, ChunkOffset = 0 });
        var target = LoadGeom();

        var transported = target.TransportEffectsFrom(source);

        Assert.Equal(1, transported);
        var effect = Assert.Single(target.GeoEffects);
        Assert.Equal(0x11223344, effect.Name);
        Assert.NotSame(source.GeoEffects[0], effect);
        Assert.Equal(source.GeomChunk6, target.GeomChunk6);
        Assert.NotSame(source.GeomChunk6, target.GeomChunk6);

        var reloaded = SaveAndReload(target);
        Assert.Equal(0x11223344, Assert.Single(reloaded.GeoEffects).Name);
    }

    [Fact]
    public void TransportEffectsFrom_adds_a_props_chunk_when_the_target_has_none()
    {
        var source = LoadGeom();
        source.GeomChunk6 = BuildSingleEffectChunk(name: 0x55);
        source.GeoEffects.Clear();
        source.GeoEffects.Add(new GeoEffect { Name = 0x55, Index = 0, ChunkOffset = 0 });

        var target = LoadGeom();
        target.Header.Chunks.RemoveAll(chunk => chunk.Type == (ushort)GeoChunkType.PROPS);
        Assert.Null(target.GetChunkFromType(GeoChunkType.PROPS));

        target.TransportEffectsFrom(source);

        var propsChunk = target.GetChunkFromType(GeoChunkType.PROPS);
        Assert.NotNull(propsChunk);
        var routesIndex = target.Header.Chunks.FindIndex(chunk => chunk.Type == (ushort)GeoChunkType.ROUTES);
        var propsIndex = target.Header.Chunks.IndexOf(propsChunk!);
        Assert.True(propsIndex < routesIndex);

        var reloaded = SaveAndReload(target);
        Assert.Equal(0x55, Assert.Single(reloaded.GeoEffects).Name);
    }

    [Fact]
    public void TransportEffectsFrom_replaces_existing_target_effects()
    {
        var source = LoadGeom();
        source.GeomChunk6 = BuildSingleEffectChunk(name: 0xAA);
        source.GeoEffects.Clear();
        source.GeoEffects.Add(new GeoEffect { Name = 0xAA, Index = 0, ChunkOffset = 0 });

        var target = LoadGeom();
        target.GeomChunk6 = BuildSingleEffectChunk(name: 0xBB);
        target.GeoEffects.Clear();
        target.GeoEffects.Add(new GeoEffect { Name = 0xBB, Index = 0, ChunkOffset = 0 });

        target.TransportEffectsFrom(source);

        Assert.Equal(0xAA, Assert.Single(target.GeoEffects).Name);
    }

    private static GeomFile LoadGeom() =>
        new(new MemoryStream(BuildGeomFixture(), writable: false), Endianness.Big);

    private static GeomFile SaveAndReload(GeomFile geometry)
    {
        using var output = new MemoryStream();
        geometry.Save(output, Endianness.Big);
        geometry.CloseStream();
        return new GeomFile(new MemoryStream(output.ToArray(), writable: false), Endianness.Big);
    }

    private static byte[] BuildSingleEffectChunk(int name)
    {
        var chunk = new byte[0x10];
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(0), 0); // next
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(4), 0); // child
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(8), name);
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(12), 0); // index (no position/rotation slots)
        return chunk;
    }

    private static byte[] BuildPositionedEffectChunk(int name, float x, float y, float z)
    {
        var chunk = new byte[0x20];
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(8), name);
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(12), 2);
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(16), BitConverter.SingleToInt32Bits(x));
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(20), BitConverter.SingleToInt32Bits(y));
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(24), BitConverter.SingleToInt32Bits(z));
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(28), BitConverter.SingleToInt32Bits(1));
        return chunk;
    }

    private static byte[] BuildGeomFixture()
    {
        const int groupOffset = 0x80;
        const int radixOffset = 0xC0;
        const int blockOffset = 0xD0;
        const int primitiveOffset = 0xF0;
        const int vertexOffset = 0x120;
        const int refsOffset = 0x190;
        const int fileSize = 0x200;
        using var output = new MemoryStream(new byte[fileSize], writable: true);
        using var writer = new EndianBinaryWriter(output, Endianness.Big, leaveOpen: true);

        writer.Write(1u);
        writer.Write((uint)fileSize);
        writer.Write(5);
        writer.Write(0);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);
        WriteChunk(writer, GeoChunkType.GROUPS, refsOffset - groupOffset, groupOffset);
        WriteChunk(writer, GeoChunkType.REFS, 0x70, refsOffset);
        WriteChunk(writer, GeoChunkType.UNKOWN, 0, fileSize);
        WriteChunk(writer, GeoChunkType.PROPS, 0, fileSize);
        WriteChunk(writer, GeoChunkType.ROUTES, 0, fileSize);
        writer.Write(new byte[8]);
        writer.Write(0x01020304u);
        writer.Write(new byte[0x18]);

        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0);
        writer.Write(100f);
        writer.Write(100f);
        writer.Write(100f);
        writer.Write(1f);
        writer.Write(1);
        writer.Write(1);
        writer.Write(1);
        writer.Write(0x30);
        writer.Write(1);
        writer.Write((short)1);
        writer.Write((short)0x10);
        writer.Write(radixOffset);
        writer.Write(blockOffset);

        output.Position = radixOffset;
        writer.Write((short)0);
        writer.Write((byte)0);
        writer.Write(new byte[0x0D]);

        output.Position = blockOffset;
        writer.Write((byte)0x01);
        writer.Write((byte)1);
        writer.Write((ushort)0xA0);
        writer.Write((ushort)0);
        writer.Write(ushort.MaxValue);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(vertexOffset);
        writer.Write(primitiveOffset);
        writer.Write(0);
        writer.Write(GeoCollisionAttributes.Floor);

        output.Position = primitiveOffset;
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)2);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0x10203040u);
        writer.Write(0);
        writer.Write(GeoCollisionAttributes.Bullet);
        writer.Write(new byte[] { 0, 1, 2, 3, 0, 0 });
        writer.Write((ushort)GeoCollisionAttributes.Bullet);
        writer.Write(new byte[8]);

        output.Position = vertexOffset;
        writer.Write(6);
        writer.Write(2);
        writer.Write(1);
        writer.Write(0);
        WriteVector(writer, 10, 20, 30, 1);
        WriteVector(writer, 0, 1, 0, 0);
        WriteVector(writer, 0, 0, 0, 0);
        WriteVector(writer, 1, 0, 0, 0);
        WriteVector(writer, 1, 0, 1, 0);
        WriteVector(writer, 0, 0, 1, 0);

        output.Position = refsOffset;
        writer.Write(new byte[0x70]);
        writer.Flush();
        return output.ToArray();
    }

    private static void WriteChunk(EndianBinaryWriter writer, GeoChunkType type, int size, int offset)
    {
        writer.Write((ushort)type);
        writer.Write((ushort)0);
        writer.Write(size);
        writer.Write(offset);
    }

    private static void WriteVector(EndianBinaryWriter writer, float x, float y, float z, float w)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
        writer.Write(w);
    }
}
