using System.Text;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Qar;
using HavenStudio.Services;
using HavenStudio.Tests.TestSupport;

namespace HavenStudio.Tests.Services;

public sealed class ArchiveRestoreServiceTests
{
    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public void BuildFromFolder_packs_nested_files_into_a_qar(Endianness endianness)
    {
        using var temp = new TempDirectory();
        var folder = temp.GetPath("dump");
        File.WriteAllBytes(temp.GetPath("dump/root.bin"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(temp.GetPath("dump/nested/model.mdn"), new byte[] { 4, 5 });

        var result = ArchiveRestoreService.BuildFromFolder("stage.qar", folder, endianness);

        Assert.Equal("stage.qar", result.ArchiveName);
        Assert.Equal(2, result.EntryCount);

        using var stream = new MemoryStream(result.Bytes);
        var qar = QarFile.Read(stream, endianness);
        Assert.Equal(new byte[] { 1, 2, 3 }, EntryData(qar, "root.bin"));
        Assert.Equal(new byte[] { 4, 5 }, EntryData(qar, "nested/model.mdn"));
    }

    [Fact]
    public void BuildFromFolder_round_trips_through_the_dump_service()
    {
        using var temp = new TempDirectory();
        var archivePath = temp.GetPath("original.dar");
        var original = new Dar();
        original.Entries.Add(new DarEntry("a/first.bin", Encoding.UTF8.GetBytes("first")));
        original.Entries.Add(new DarEntry("second.bin", Encoding.UTF8.GetBytes("second")));
        using (var stream = File.Create(archivePath))
        {
            DarFile.Write(stream, original, Endianness.Big);
        }

        var dumpFolder = temp.GetPath("dumped");
        ArchiveDumpService.ExtractFiles(archivePath, dumpFolder, Endianness.Big);

        var rebuilt = ArchiveRestoreService.BuildFromFolder("original.dar", dumpFolder, Endianness.Big);
        using var rebuiltStream = new MemoryStream(rebuilt.Bytes);
        var dar = DarFile.Read(rebuiltStream, Endianness.Big);

        Assert.Equal(2, dar.Entries.Count);
        Assert.Equal("first", Encoding.UTF8.GetString(EntryData(dar, "a/first.bin")));
        Assert.Equal("second", Encoding.UTF8.GetString(EntryData(dar, "second.bin")));
    }

    [Fact]
    public void BuildFromFolder_rejects_an_empty_folder()
    {
        using var temp = new TempDirectory();
        var folder = temp.GetPath("empty");
        Directory.CreateDirectory(folder);

        Assert.Throws<InvalidDataException>(
            () => ArchiveRestoreService.BuildFromFolder("stage.qar", folder, Endianness.Big));
    }

    [Fact]
    public void BuildFromFolder_rejects_unsupported_archive_types()
    {
        using var temp = new TempDirectory();
        File.WriteAllBytes(temp.GetPath("data/file.bin"), new byte[] { 1 });

        Assert.Throws<NotSupportedException>(
            () => ArchiveRestoreService.BuildFromFolder("stage.pak", temp.GetPath("data"), Endianness.Big));
    }

    private static byte[] EntryData(Qar qar, string name) =>
        qar.Entries.Single(entry => entry.Filename == name).Data!;

    private static byte[] EntryData(Dar dar, string name) =>
        dar.Entries.Single(entry => entry.Filename == name).Bytes!;
}
