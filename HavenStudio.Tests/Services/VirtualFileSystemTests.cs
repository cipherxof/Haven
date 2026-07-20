using System.Text;
using HavenStudio.Extensions;
using HavenStudio.Formats.Qar;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;

namespace HavenStudio.Tests.Services;

public sealed class VirtualFileSystemTests
{
    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public async Task Catalog_enumerates_physical_and_archive_entries(Endianness endianness)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.GetPath("physical.txt"), "physical");
        var archivePath = temp.GetPath("fixture.qar");
        WriteArchive(archivePath, "nested/archive.txt", "archived", endianness);
        var catalog = new WorkspaceCatalog(temp.Path, endianness);
        var snapshot = await catalog.ScanAsync();
        var files = snapshot.WithExtension(".txt").ToList();

        Assert.Contains(files, file => !file.IsArchiveEntry && file.Name == "physical.txt");
        Assert.Contains(files, file => file.Path.ArchiveEntryName == "nested/archive.txt");
    }

    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public async Task Catalog_reads_and_replaces_archive_entry(Endianness endianness)
    {
        using var temp = new TempDirectory();
        var archivePath = temp.GetPath("fixture.qar");
        WriteArchive(archivePath, "nested/archive.txt", "before", endianness);
        var catalog = new WorkspaceCatalog(temp.Path, endianness);
        var snapshot = await catalog.ScanAsync();
        var archiveEntry = snapshot.Files.Single(file => file.Path.IsArchiveEntry).Path;

        using (var reader = new StreamReader(catalog.OpenRead(archiveEntry), Encoding.UTF8))
        {
            Assert.Equal("before", reader.ReadToEnd());
        }

        catalog.Replace(archiveEntry, Encoding.UTF8.GetBytes("after"));

        using var updatedReader = new StreamReader(catalog.OpenRead(archiveEntry), Encoding.UTF8);
        Assert.Equal("after", updatedReader.ReadToEnd());
        Assert.False(File.Exists(archivePath + ".bak"));
    }

    private static void WriteArchive(
        string path,
        string entryName,
        string content,
        Endianness endianness = Endianness.Big)
    {
        var archive = new Qar();
        archive.Entries.Add(new QarEntry
        {
            Filename = entryName,
            Data = Encoding.UTF8.GetBytes(content)
        });

        using var stream = File.Create(path);
        QarFile.Write(stream, archive, endianness);
    }
}
