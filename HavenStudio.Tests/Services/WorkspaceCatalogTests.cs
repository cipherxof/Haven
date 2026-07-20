using System.Text;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Qar;
using HavenStudio.Services;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;

namespace HavenStudio.Tests.Services;

public sealed class WorkspaceCatalogTests
{
    [Fact]
    public void Workspace_path_centralizes_legacy_path_parsing()
    {
        using var temp = new TempDirectory();
        var archivePath = temp.GetPath("data.qar");

        var physical = WorkspacePath.Physical(archivePath);
        var archived = WorkspacePath.ArchiveEntry(archivePath, "nested/model.mdn");

        Assert.False(physical.IsArchiveEntry);
        Assert.Equal(".qar", physical.Extension);
        Assert.True(archived.IsArchiveEntry);
        Assert.Equal("model.mdn", archived.FileName);
        Assert.Equal(".mdn", archived.Extension);
        Assert.Equal(archived, WorkspacePath.ParseLegacy(archived.ToLegacyString()));
    }

    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public async Task Catalog_scans_once_and_opens_physical_qar_and_dar_files(Endianness endianness)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.GetPath("physical.mdn"), "physical");
        WriteQar(temp.GetPath("models.qar"), "archived.mdn", "qar", endianness);
        WriteDar(temp.GetPath("models.dar"), "another.mdn", "dar", endianness);
        var catalog = new WorkspaceCatalog(temp.Path, endianness);
        var progress = new ProgressCollector();

        var snapshot = await catalog.ScanAsync(progress: progress);

        var models = snapshot.WithExtension(".mdn").ToList();
        Assert.Equal(3, models.Count);
        Assert.NotEmpty(progress.Values);
        Assert.Equal("physical", ReadText(catalog, models.Single(file => !file.IsArchiveEntry).Path));
        Assert.Equal("qar", ReadText(catalog, models.Single(file => file.Name == "archived.mdn").Path));
        Assert.Equal("dar", ReadText(catalog, models.Single(file => file.Name == "another.mdn").Path));
    }

    [Fact]
    public async Task Catalog_instances_do_not_share_workspace_results()
    {
        using var firstRoot = new TempDirectory();
        using var secondRoot = new TempDirectory();
        WriteQar(firstRoot.GetPath("data.qar"), "first.mdn", "first", Endianness.Big);
        WriteQar(secondRoot.GetPath("data.qar"), "second.mdn", "second", Endianness.Big);
        var first = new WorkspaceCatalog(firstRoot.Path, Endianness.Big);
        var second = new WorkspaceCatalog(secondRoot.Path, Endianness.Big);

        var firstSnapshot = await first.ScanAsync();
        var secondSnapshot = await second.ScanAsync();

        Assert.Contains(firstSnapshot.Files, file => file.Name == "first.mdn");
        Assert.DoesNotContain(firstSnapshot.Files, file => file.Name == "second.mdn");
        Assert.Contains(secondSnapshot.Files, file => file.Name == "second.mdn");
        Assert.DoesNotContain(secondSnapshot.Files, file => file.Name == "first.mdn");
    }

    [Fact]
    public async Task Replace_refreshes_only_the_changed_archive_in_the_snapshot()
    {
        using var temp = new TempDirectory();
        WriteQar(temp.GetPath("first.qar"), "first.txt", "before", Endianness.Big);
        WriteQar(temp.GetPath("second.qar"), "second.txt", "untouched", Endianness.Big);
        var catalog = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await catalog.ScanAsync();
        var first = snapshot.Files.Single(file => file.Name == "first.txt");
        var second = snapshot.Files.Single(file => file.Name == "second.txt");
        using var untouchedArchiveLock = new FileStream(
            temp.GetPath("second.qar"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        catalog.Replace(first.Path, Encoding.UTF8.GetBytes("after replacement"));

        Assert.Equal("after replacement", ReadText(catalog, first.Path));
        Assert.Equal("untouched", ReadText(catalog, second.Path));
        Assert.Equal(
            "after replacement".Length,
            catalog.Snapshot!.Files.Single(file => file.Name == "first.txt").Length);
        Assert.Equal(
            "untouched".Length,
            catalog.Snapshot.Files.Single(file => file.Name == "second.txt").Length);
    }

    [Fact]
    public async Task Replace_adds_a_new_physical_file_to_the_workspace_snapshot()
    {
        using var temp = new TempDirectory();
        var catalog = new WorkspaceCatalog(temp.Path, Endianness.Big);
        await catalog.ScanAsync();
        var path = WorkspacePath.Physical(temp.GetPath("authored.geom"));

        catalog.Replace(path, [1, 2, 3, 4]);

        var file = Assert.Single(catalog.Snapshot!.Files);
        Assert.Equal(path, file.Path);
        Assert.Equal("authored.geom", file.Name);
        Assert.Equal(4, file.Length);
    }

    [Fact]
    public async Task Archive_cache_reloads_for_timestamp_or_length_changes()
    {
        using var temp = new TempDirectory();
        var archivePath = temp.GetPath("data.qar");
        WriteQar(archivePath, "entry.txt", "one", Endianness.Big);
        var catalog = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await catalog.ScanAsync();
        var entryPath = snapshot.Files.Single(file => file.Name == "entry.txt").Path;
        Assert.Equal("one", ReadText(catalog, entryPath));

        WriteQar(archivePath, "entry.txt", "two", Endianness.Big);
        var changedTimestamp = DateTime.UtcNow.AddMinutes(1);
        File.SetLastWriteTimeUtc(archivePath, changedTimestamp);
        Assert.Equal("two", ReadText(catalog, entryPath));

        var cachedTimestamp = File.GetLastWriteTimeUtc(archivePath);
        WriteQar(archivePath, "entry.txt", "different length", Endianness.Big);
        File.SetLastWriteTimeUtc(archivePath, cachedTimestamp);
        Assert.Equal("different length", ReadText(catalog, entryPath));
    }

    [Fact]
    public async Task Scan_honors_cancellation_before_publishing_a_snapshot()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.GetPath("file.txt"), "content");
        var catalog = new WorkspaceCatalog(temp.Path, Endianness.Big);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => catalog.ScanAsync(cancellation.Token));

        Assert.Null(catalog.Snapshot);
    }

    [Fact]
    public async Task Workspace_session_never_publishes_a_superseded_scan()
    {
        using var firstRoot = new TempDirectory();
        using var secondRoot = new TempDirectory();
        using var session = new WorkspaceSession();
        var first = new ControlledCatalog(firstRoot.Path);
        var second = new ControlledCatalog(secondRoot.Path);

        var firstOpen = session.OpenAsync(first);
        await first.Started.Task;
        var secondOpen = session.OpenAsync(second);
        await second.Started.Task;

        second.Complete("second.txt");
        Assert.True(await secondOpen);
        first.Complete("first.txt");
        Assert.False(await firstOpen);

        Assert.Same(second, session.Catalog);
        Assert.Equal(secondRoot.Path, session.Snapshot!.RootPath);
        Assert.Contains(session.Snapshot.Files, file => file.Name == "second.txt");
        Assert.DoesNotContain(session.Snapshot.Files, file => file.Name == "first.txt");
    }

    private static string ReadText(IWorkspaceCatalog catalog, WorkspacePath path)
    {
        using var reader = new StreamReader(catalog.OpenRead(path), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void WriteQar(
        string path,
        string entryName,
        string content,
        Endianness endianness)
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

    private static void WriteDar(
        string path,
        string entryName,
        string content,
        Endianness endianness)
    {
        var archive = new Dar();
        archive.Entries.Add(new DarEntry(entryName, Encoding.UTF8.GetBytes(content)));
        using var stream = File.Create(path);
        DarFile.Write(stream, archive, endianness);
    }

    private sealed class ProgressCollector : IProgress<WorkspaceScanProgress>
    {
        public List<WorkspaceScanProgress> Values { get; } = [];

        public void Report(WorkspaceScanProgress value)
        {
            lock (Values)
            {
                Values.Add(value);
            }
        }
    }

    private sealed class ControlledCatalog(string rootPath) : IWorkspaceCatalog
    {
        private readonly TaskCompletionSource<WorkspaceSnapshot> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string RootPath { get; } = rootPath;
        public Endianness Endianness => Endianness.Big;
        public WorkspaceSnapshot? Snapshot { get; private set; }

        public async Task<WorkspaceSnapshot> ScanAsync(
            CancellationToken cancellationToken = default,
            IProgress<WorkspaceScanProgress>? progress = null)
        {
            Started.TrySetResult();
            Snapshot = await _completion.Task;
            return Snapshot;
        }

        public void Complete(string fileName)
        {
            var path = WorkspacePath.Physical(Path.Combine(RootPath, fileName));
            _completion.TrySetResult(new WorkspaceSnapshot(
                RootPath,
                [],
                [new WorkspaceFile(path, fileName, 0)]));
        }

        public Stream OpenRead(WorkspacePath path) => throw new NotSupportedException();
        public byte[] ReadAllBytes(WorkspacePath path) => throw new NotSupportedException();
        public void Replace(WorkspacePath path, ReadOnlySpan<byte> data) => throw new NotSupportedException();
        public ArchiveDumpService.ExtractSummary ExtractArchive(WorkspacePath archivePath, string outputFolder) =>
            throw new NotSupportedException();
    }
}
