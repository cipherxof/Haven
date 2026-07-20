using HavenStudio.Services.FileOpening;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;

namespace HavenStudio.Tests.Services;

public sealed class FileOpenCoordinatorTests
{
    [Theory]
    [InlineData("file.gcx", FileOpenKind.Gcx)]
    [InlineData("file.geom", FileOpenKind.Geom)]
    [InlineData("file.txn", FileOpenKind.Txn)]
    [InlineData("file.dds", FileOpenKind.Dds)]
    [InlineData("file.mdn", FileOpenKind.Mdn)]
    [InlineData("file.lt2", FileOpenKind.Lit)]
    [InlineData("file.lt3", FileOpenKind.Lit)]
    [InlineData("file.txt", FileOpenKind.Text)]
    [InlineData("file.cnf", FileOpenKind.Text)]
    [InlineData("file.nni", FileOpenKind.Text)]
    public async Task Supported_extensions_use_the_same_open_action(string fileName, FileOpenKind expectedKind)
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath(fileName);
        await File.WriteAllBytesAsync(path, [0x01]);
        var actions = new RecordingFileOpenActions();
        var coordinator = new FileOpenCoordinator(actions);

        var result = await coordinator.OpenAsync(WorkspacePath.Physical(path));

        Assert.Equal(FileOpenStatus.Opened, result.Status);
        Assert.Equal(expectedKind, actions.Kind);
        Assert.Equal(path, actions.Path?.PhysicalPath);
        Assert.Equal(1, actions.CallCount);
    }

    [Fact]
    public async Task Archive_entries_use_the_same_extension_routing()
    {
        var actions = new RecordingFileOpenActions();
        var coordinator = new FileOpenCoordinator(actions);
        var path = WorkspacePath.ArchiveEntry("unused.qar", "nested/script.gcx");

        var result = await coordinator.OpenAsync(path);

        Assert.Equal(FileOpenStatus.Opened, result.Status);
        Assert.Equal(FileOpenKind.Gcx, actions.Kind);
        Assert.Equal(path, actions.Path);
    }

    [Fact]
    public async Task Unsupported_extensions_return_an_explicit_result_without_calling_actions()
    {
        var actions = new RecordingFileOpenActions();
        var coordinator = new FileOpenCoordinator(actions);

        var result = await coordinator.OpenAsync(WorkspacePath.Physical("file.unknown"));

        Assert.Equal(FileOpenStatus.Unsupported, result.Status);
        Assert.Equal(0, actions.CallCount);
    }

    [Fact]
    public async Task Missing_physical_files_return_a_failed_result_without_calling_actions()
    {
        using var temp = new TempDirectory();
        var actions = new RecordingFileOpenActions();
        var coordinator = new FileOpenCoordinator(actions);

        var result = await coordinator.OpenAsync(WorkspacePath.Physical(temp.GetPath("missing.gcx")));

        Assert.Equal(FileOpenStatus.Failed, result.Status);
        Assert.IsType<FileNotFoundException>(result.Exception);
        Assert.Equal(0, actions.CallCount);
    }

    [Fact]
    public async Task Cancellation_returns_an_explicit_cancelled_result()
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath("file.gcx");
        await File.WriteAllBytesAsync(path, [0x01]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var actions = new RecordingFileOpenActions();
        var coordinator = new FileOpenCoordinator(actions);

        var result = await coordinator.OpenAsync(WorkspacePath.Physical(path), cancellationToken: cancellation.Token);

        Assert.Equal(FileOpenStatus.Cancelled, result.Status);
        Assert.Equal(0, actions.CallCount);
    }

    [Fact]
    public async Task Action_exceptions_return_an_explicit_failed_result()
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath("file.gcx");
        await File.WriteAllBytesAsync(path, [0x01]);
        var expected = new InvalidDataException("invalid payload");
        var actions = new RecordingFileOpenActions { Exception = expected };
        var coordinator = new FileOpenCoordinator(actions);

        var result = await coordinator.OpenAsync(WorkspacePath.Physical(path));

        Assert.Equal(FileOpenStatus.Failed, result.Status);
        Assert.Same(expected, result.Exception);
        Assert.Contains("invalid payload", result.Message);
        Assert.Equal(1, actions.CallCount);
    }

    private sealed class RecordingFileOpenActions : IFileOpenActions
    {
        public int CallCount { get; private set; }
        public FileOpenKind? Kind { get; private set; }
        public WorkspacePath? Path { get; private set; }
        public Exception? Exception { get; init; }

        public Task OpenAsync(
            FileOpenKind kind,
            WorkspacePath path,
            IWorkspaceCatalog? workspace,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Kind = kind;
            Path = path;
            return Exception == null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }
}
