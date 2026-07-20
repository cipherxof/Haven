using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Qar;

namespace HavenStudio.Tests.Formats;

public sealed class ArchiveFormatTests
{
    [Fact]
    public void Qar_write_matches_golden_bytes_and_round_trips()
    {
        var archive = new Qar();
        archive.Entries.Add(new QarEntry
        {
            Info = 0x11223344,
            Filename = "a.bin",
            Data = [0x01, 0x02, 0x03]
        });
        archive.Entries.Add(new QarEntry
        {
            Info = 0,
            Filename = "b",
            Data = [0xAA]
        });

        using var stream = new MemoryStream();
        QarFile.Write(stream, archive);

        var expected = Convert.FromHexString(
            "010203AA0002000011223344000000030000000000000001612E62696E00620000000004");
        Assert.Equal(expected, stream.ToArray());

        stream.Position = 0;
        var restored = QarFile.Read(stream);

        Assert.Collection(
            restored.Entries,
            entry =>
            {
                Assert.Equal(0x11223344, entry.Info);
                Assert.Equal("a.bin", entry.Filename);
                Assert.Equal([0x01, 0x02, 0x03], entry.Data);
            },
            entry =>
            {
                Assert.Equal(0, entry.Info);
                Assert.Equal("b", entry.Filename);
                Assert.Equal([0xAA], entry.Data);
            });
    }

    [Fact]
    public void Qar_write_aligns_header_offset_footer_to_four_bytes()
    {
        var archive = new Qar();
        archive.Entries.Add(new QarEntry { Filename = "x", Data = [] });

        using var stream = new MemoryStream();
        QarFile.Write(stream, archive);

        var expected = Convert.FromHexString(
            "0001000000000000000000007800000000000000");
        Assert.Equal(expected, stream.ToArray());
    }

    [Fact]
    public void Dar_write_matches_golden_bytes_and_round_trips()
    {
        var archive = new Dar();
        archive.Entries.Add(new DarEntry("a.bin", [0x01, 0x02, 0x03]));

        using var stream = new MemoryStream();
        DarFile.Write(stream, archive);

        var expected = Convert.FromHexString(
            "00000001612E62696E0000000000000301020300");
        Assert.Equal(expected, stream.ToArray());

        stream.Position = 0;
        var restored = DarFile.Read(stream);

        var entry = Assert.Single(restored.Entries);
        Assert.Equal("a.bin", entry.Filename);
        Assert.Equal([0x01, 0x02, 0x03], entry.Bytes);
    }

    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public void Qar_and_dar_round_trip_with_explicit_endianness(Endianness endianness)
    {
        var qar = new Qar();
        qar.Entries.Add(new QarEntry
        {
            Info = 0x11223344,
            Filename = "entry.bin",
            Data = [0xAA, 0xBB]
        });
        using var qarStream = new MemoryStream();
        QarFile.Write(qarStream, qar, endianness);
        qarStream.Position = 0;

        var restoredQar = QarFile.Read(qarStream, endianness);

        var qarEntry = Assert.Single(restoredQar.Entries);
        Assert.Equal(0x11223344, qarEntry.Info);
        Assert.Equal([0xAA, 0xBB], qarEntry.Data);

        var dar = new Dar();
        dar.Entries.Add(new DarEntry("entry.bin", [0x10, 0x20]));
        using var darStream = new MemoryStream();
        DarFile.Write(darStream, dar, endianness);
        darStream.Position = 0;

        var restoredDar = DarFile.Read(darStream, endianness);

        Assert.Equal([0x10, 0x20], Assert.Single(restoredDar.Entries).Bytes);
    }

    [Fact]
    public void Qar_read_rejects_negative_entry_count()
    {
        using var stream = new MemoryStream(Convert.FromHexString("FFFF000000000000"));

        Assert.Throws<InvalidDataException>(() => QarFile.Read(stream));
    }

    [Fact]
    public void Qar_read_rejects_header_offset_outside_file()
    {
        using var stream = new MemoryStream(Convert.FromHexString("00000010"));

        Assert.Throws<InvalidDataException>(() => QarFile.Read(stream));
    }

    [Fact]
    public void Dar_read_rejects_truncated_payload()
    {
        using var stream = new MemoryStream(Convert.FromHexString("000000016100000000000004"));

        Assert.Throws<InvalidDataException>(() => DarFile.Read(stream));
    }
}
