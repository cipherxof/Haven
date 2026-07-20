using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Extensions;
using HavenStudio.Services.Workspace;
using HavenStudio.Windows;

namespace HavenStudio.Services.FileOpening;

public sealed class MainWindowFileOpenActions : IFileOpenActions
{
    private readonly MainWindow _owner;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowFileOpenActions(MainWindow owner, MainWindowViewModel viewModel)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public async Task OpenAsync(
        FileOpenKind kind,
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case FileOpenKind.Gcx:
                await OpenGcxAsync(path, workspace, cancellationToken);
                break;
            case FileOpenKind.Geom:
                await OpenGeomAsync(path, workspace, cancellationToken);
                break;
            case FileOpenKind.Txn:
                await OpenTxnAsync(path, workspace, cancellationToken);
                break;
            case FileOpenKind.Dds:
                await OpenDdsAsync(path, workspace, cancellationToken);
                break;
            case FileOpenKind.Mdn:
                await OpenMdnAsync(path, workspace, cancellationToken);
                break;
            case FileOpenKind.Lit:
                await OpenLitAsync(path, workspace, cancellationToken);
                break;
            case FileOpenKind.Text:
                await OpenTextAsync(path, workspace, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported file-open action.");
        }
    }

    private async Task OpenGcxAsync(
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsIndexedBy(workspace, path))
        {
            await _viewModel.LoadGcxFromWorkspacePathAsync(path);
        }
        else
        {
            await _viewModel.LoadGcxFromFilePathAsync(path.PhysicalPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _viewModel.SelectedTabIndex = 1;
    }

    private async Task OpenGeomAsync(
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsIndexedBy(workspace, path))
        {
            await _viewModel.LoadGeomFromWorkspacePathAsync(path);
        }
        else
        {
            await _viewModel.LoadGeomFromFilePathAsync(path.PhysicalPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _viewModel.SelectedTabIndex = 0;
    }

    private async Task OpenTxnAsync(
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken)
    {
        var catalog = await ResolveWorkspaceAsync(path, workspace, cancellationToken);
        var window = new TxnEditorWindow(path, catalog);
        await window.ShowDialog(_owner);
    }

    private async Task OpenDdsAsync(
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsIndexedBy(workspace, path))
        {
            await DdsPreviewService.ShowPreviewFromFileAsync(_owner, path, workspace!);
            return;
        }

        await DdsPreviewService.ShowPreviewFromFileAsync(_owner, path.PhysicalPath);
    }

    private async Task OpenMdnAsync(
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken)
    {
        var catalog = await ResolveWorkspaceAsync(path, workspace, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ModelViewerWindow.Open(path, catalog);
    }

    private async Task OpenLitAsync(
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken)
    {
        var catalog = await ResolveWorkspaceAsync(path, workspace, cancellationToken);
        await _viewModel.LoadLightsFromWorkspacePathAsync(path, catalog, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _viewModel.SelectedTabIndex = 0;
    }

    private async Task OpenTextAsync(
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsIndexedBy(workspace, path))
        {
            TextEditorWindow.Open(_owner, path, workspace!);
        }
        else
        {
            TextEditorWindow.Open(_owner, path.PhysicalPath);
        }

        await Task.CompletedTask;
    }

    private static bool IsIndexedBy(IWorkspaceCatalog? workspace, WorkspacePath path)
    {
        return workspace?.Snapshot?.TryGetFile(path, out _) == true;
    }

    private static async Task<IWorkspaceCatalog> ResolveWorkspaceAsync(
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken)
    {
        if (IsIndexedBy(workspace, path))
        {
            return workspace!;
        }

        var root = Path.GetDirectoryName(path.PhysicalPath) ?? Directory.GetCurrentDirectory();
        var externalWorkspace = new WorkspaceCatalog(root, EndianBinaryReader.DefaultEndianness);
        await externalWorkspace.ScanAsync(cancellationToken);
        return externalWorkspace;
    }
}
