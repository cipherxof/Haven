using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HavenStudio.Services.FileExplorer;

namespace HavenStudio.Views;

public partial class FileExplorerView : UserControl
{
    private FileExplorerOperations? _operations;
    private ContextMenu? _openContextMenu;

    public FileExplorerView()
    {
        InitializeComponent();
    }

    public void InitializeOperations(FileExplorerOperations operations)
    {
        _operations = operations;
    }

    public void ClearOperations()
    {
        _operations = null;
    }

    public void CloseContextMenu()
    {
        _openContextMenu?.Close();
    }

    private async void OnFileTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_operations != null && sender is TreeView { SelectedItem: FileNode { IsFile: true } fileNode })
        {
            await _operations.OpenAsync(fileNode.WorkspacePath);
        }
    }

    private async void OnDumpArchiveFiles(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.ExtractArchiveAsync(fileNode);
        }
    }

    private async void OnReplaceArchiveFile(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.ReplaceArchiveEntryAsync(fileNode);
        }
    }

    private async void OnDumpTxnTextures(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.DumpTxnTexturesAsync(fileNode);
        }
    }

    private async void OnOpenTxnEditor(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.OpenAsync(fileNode.WorkspacePath);
        }
    }

    private async void OnDumpDlzToDld(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.DumpDlzAsync(fileNode);
        }
    }

    private async void OnDumpGcxToJson(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.DumpGcxJsonAsync(fileNode);
        }
    }

    private async void OnRestoreGcxFromJson(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.RestoreGcxJsonAsync(fileNode);
        }
    }

    private async void OnDumpMapToJson(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.DumpMapJsonAsync(fileNode);
        }
    }

    private async void OnRestoreMapFromJson(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.RestoreMapJsonAsync(fileNode);
        }
    }

    private async void OnExportGeomToGltf(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.ExportGeomGltfAsync(fileNode);
        }
    }

    private async void OnImportGeomGltfPositions(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.ImportGeomGltfPositionsAsync(fileNode);
        }
    }

    private async void OnImportGeomGltfTopology(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.ImportGeomGltfTopologyAsync(fileNode);
        }
    }

    private async void OnCreateGeomFromGltf(object? sender, RoutedEventArgs e)
    {
        if (_operations != null)
        {
            await _operations.CreateGeomFromGltfAsync();
        }
    }

    private async void OnTransportGeomEffects(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.TransportGeomEffectsAsync(fileNode);
        }
    }

    private async void OnRestoreArchiveFromFolder(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.RestoreArchiveFromFolderAsync(fileNode);
        }
    }

    private async void OnRestoreTxnFromFolder(object? sender, RoutedEventArgs e)
    {
        if (_operations != null && TryGetFileNode(sender) is { } fileNode)
        {
            await _operations.RestoreTxnFromFolderAsync(fileNode);
        }
    }

    private static FileNode? TryGetFileNode(object? sender)
    {
        return sender is MenuItem { DataContext: FileNode fileNode } ? fileNode : null;
    }

    private void OnContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        _openContextMenu = sender as ContextMenu;
    }

    private void OnContextMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_openContextMenu, sender))
        {
            _openContextMenu = null;
        }
    }
}
