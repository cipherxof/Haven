using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Services.Workspace;
using Serilog;

namespace HavenStudio.Services.FileOpening;

public sealed class FileOpenCoordinator
{
    private static readonly ILogger Log = Serilog.Log.ForContext<FileOpenCoordinator>();

    private static readonly IReadOnlyDictionary<string, FileOpenKind> FileKinds =
        new Dictionary<string, FileOpenKind>(StringComparer.OrdinalIgnoreCase)
        {
            [".gcx"] = FileOpenKind.Gcx,
            [".geom"] = FileOpenKind.Geom,
            [".txn"] = FileOpenKind.Txn,
            [".dds"] = FileOpenKind.Dds,
            [".mdn"] = FileOpenKind.Mdn,
            [".lt2"] = FileOpenKind.Lit,
            [".lt3"] = FileOpenKind.Lit,
            [".txt"] = FileOpenKind.Text,
            [".cnf"] = FileOpenKind.Text,
            [".nni"] = FileOpenKind.Text
        };

    private readonly IFileOpenActions _actions;

    public FileOpenCoordinator(IFileOpenActions actions)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public async Task<FileOpenResult> OpenAsync(
        WorkspacePath path,
        IWorkspaceCatalog? workspace = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!FileKinds.TryGetValue(path.Extension, out var kind))
        {
            return FileOpenResult.Unsupported(
                $"HavenStudio does not have an editor or viewer for '{path.Extension}'.");
        }

        if (!path.IsArchiveEntry && !File.Exists(path.PhysicalPath))
        {
            var exception = new FileNotFoundException("The selected file was not found.", path.PhysicalPath);
            return FileOpenResult.Failed(exception.Message, exception);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _actions.OpenAsync(kind, path, workspace, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return FileOpenResult.Opened();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return FileOpenResult.Cancelled();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open {FileKind} file {FilePath}", kind, path);
            return FileOpenResult.Failed($"Failed to open '{path.FileName}': {ex.Message}", ex);
        }
    }
}
