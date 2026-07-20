using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Qar;
using HavenStudio.Services;
using HavenStudio.Tests.TestSupport;

namespace HavenStudio.Tests.Services;

public sealed class ArchiveDumpServiceTests
{
    [Fact]
    public void ExtractFiles_preserves_safe_nested_paths()
    {
        using var temp = new TempDirectory();
        var archivePath = temp.GetPath("safe.qar");
        var outputPath = temp.GetPath("output");
        WriteQar(archivePath, ("nested/data.bin", new byte[] { 1, 2, 3 }));

        var summary = ArchiveDumpService.ExtractFiles(archivePath, outputPath);

        Assert.Equal(1, summary.ExtractedCount);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(outputPath, "nested", "data.bin")));
    }

    [Theory]
    [InlineData("../escape.bin")]
    [InlineData("nested/../../escape.bin")]
    [InlineData("/absolute.bin")]
    [InlineData("C:\\escape.bin")]
    public void ExtractFiles_rejects_unsafe_qar_paths(string entryName)
    {
        using var temp = new TempDirectory();
        var archivePath = temp.GetPath("unsafe.qar");
        var outputPath = temp.GetPath("output");
        WriteQar(archivePath, (entryName, new byte[] { 1 }));

        Assert.Throws<InvalidDataException>(() => ArchiveDumpService.ExtractFiles(archivePath, outputPath));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "escape.bin", SearchOption.AllDirectories));
    }

    [Fact]
    public void ExtractFiles_rejects_unsafe_dar_paths()
    {
        using var temp = new TempDirectory();
        var archivePath = temp.GetPath("unsafe.dar");
        var outputPath = temp.GetPath("output");
        var archive = new Dar();
        archive.Entries.Add(new DarEntry("../escape.bin", new byte[] { 1 }));
        using (var stream = File.Create(archivePath))
        {
            DarFile.Write(stream, archive);
        }

        Assert.Throws<InvalidDataException>(() => ArchiveDumpService.ExtractFiles(archivePath, outputPath));
        Assert.False(File.Exists(temp.GetPath("escape.bin")));
    }

    [Fact]
    public void ExtractFiles_rejects_case_colliding_destinations_before_writing()
    {
        using var temp = new TempDirectory();
        var archivePath = temp.GetPath("duplicate.qar");
        var outputPath = temp.GetPath("output");
        WriteQar(
            archivePath,
            ("same.bin", new byte[] { 1 }),
            ("SAME.BIN", new byte[] { 2 }));

        Assert.Throws<InvalidDataException>(() => ArchiveDumpService.ExtractFiles(archivePath, outputPath));
        Assert.Empty(Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories));
    }

    private static void WriteQar(string path, params (string Name, byte[] Data)[] entries)
    {
        var archive = new Qar();
        foreach (var entry in entries)
        {
            archive.Entries.Add(new QarEntry { Filename = entry.Name, Data = entry.Data });
        }

        using var stream = File.Create(path);
        QarFile.Write(stream, archive);
    }
}
