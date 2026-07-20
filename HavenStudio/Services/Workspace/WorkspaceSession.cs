using System;
using System.Threading;
using System.Threading.Tasks;

namespace HavenStudio.Services.Workspace;

/// <summary>
/// Serializes workspace publication. A superseded scan may finish, but it can
/// never replace the newer catalog and snapshot.
/// </summary>
public sealed class WorkspaceSession : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _scanCancellation;
    private IWorkspaceCatalog? _catalog;
    private WorkspaceSnapshot? _snapshot;
    private long _generation;
    private bool _disposed;

    public IWorkspaceCatalog? Catalog
    {
        get
        {
            lock (_gate)
            {
                return _catalog;
            }
        }
    }

    public WorkspaceSnapshot? Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public async Task<bool> OpenAsync(
        IWorkspaceCatalog catalog,
        CancellationToken cancellationToken = default,
        IProgress<WorkspaceScanProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        CancellationTokenSource scanCancellation;
        long generation;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _scanCancellation?.Cancel();
            _scanCancellation?.Dispose();
            _scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            scanCancellation = _scanCancellation;
            generation = ++_generation;
        }

        WorkspaceSnapshot snapshot;
        try
        {
            snapshot = await catalog.ScanAsync(scanCancellation.Token, progress).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        lock (_gate)
        {
            if (_disposed || generation != _generation || scanCancellation.IsCancellationRequested)
            {
                return false;
            }

            _catalog = catalog;
            _snapshot = snapshot;
            return true;
        }
    }

    public void CancelPendingScan()
    {
        lock (_gate)
        {
            _scanCancellation?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _scanCancellation?.Cancel();
            _scanCancellation?.Dispose();
            _scanCancellation = null;
        }
    }
}
