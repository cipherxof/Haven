using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Formats.Gcx;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Editors.GcxEditing;

public sealed class GcxDocumentSession
{
    private IWorkspaceCatalog? _workspace;
    private long _loadGeneration;

    public Gcx? Document { get; private set; }
    public WorkspacePath? CurrentPath { get; private set; }
    public bool IsDirty { get; private set; }
    public bool HasDocument => Document != null && CurrentPath != null;

    public void SetWorkspace(IWorkspaceCatalog workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public async Task<bool> LoadAsync(WorkspacePath? path, CancellationToken cancellationToken = default)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        Document = null;
        CurrentPath = null;
        IsDirty = false;

        if (path == null || (!path.IsArchiveEntry && !File.Exists(path.PhysicalPath)))
        {
            return false;
        }

        var document = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = OpenRead(path);
            var loaded = GcxFile.Read(stream);
            cancellationToken.ThrowIfCancellationRequested();
            return loaded;
        }, cancellationToken);

        if (generation != Volatile.Read(ref _loadGeneration) || cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        Document = document;
        CurrentPath = path;
        IsDirty = false;
        return true;
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        var document = Document;
        var path = CurrentPath;
        if (document == null || path == null)
        {
            return false;
        }

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsInWorkspace(path))
            {
                using var stream = new MemoryStream();
                GcxFile.Write(stream, document);
                cancellationToken.ThrowIfCancellationRequested();
                _workspace!.Replace(path, stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
                return;
            }

            if (path.IsArchiveEntry)
            {
                throw new InvalidOperationException("Archived GCX files require an open workspace.");
            }

            using var destination = new FileStream(path.PhysicalPath, FileMode.Create, FileAccess.Write, FileShare.None);
            GcxFile.Write(destination, document);
        }, cancellationToken);

        if (ReferenceEquals(Document, document) && Equals(CurrentPath, path))
        {
            IsDirty = false;
        }

        return true;
    }

    public void MarkDirty()
    {
        if (Document != null)
        {
            IsDirty = true;
        }
    }

    public void Unload()
    {
        Interlocked.Increment(ref _loadGeneration);
        Document = null;
        CurrentPath = null;
        IsDirty = false;
    }

    private Stream OpenRead(WorkspacePath path)
    {
        if (IsInWorkspace(path))
        {
            return _workspace!.OpenRead(path);
        }

        if (path.IsArchiveEntry)
        {
            throw new InvalidOperationException("Archived GCX files require an open workspace.");
        }

        return File.OpenRead(path.PhysicalPath);
    }

    private bool IsInWorkspace(WorkspacePath path)
    {
        return _workspace?.Snapshot?.TryGetFile(path, out _) == true;
    }
}
