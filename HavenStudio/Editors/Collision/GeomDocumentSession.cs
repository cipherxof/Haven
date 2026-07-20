using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Formats.Geo;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Editors;

public sealed class GeomDocumentSession
{
    private IWorkspaceCatalog? _workspace;
    private long _loadGeneration;

    public GeomFile? Document { get; private set; }
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
        UnloadDocument();

        if (path == null || (!path.IsArchiveEntry && !File.Exists(path.PhysicalPath)))
        {
            return false;
        }

        var document = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stream = OpenRead(path);
            try
            {
                var loaded = new GeomFile(stream, ResolveEndianness(path));
                cancellationToken.ThrowIfCancellationRequested();
                return loaded;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }, cancellationToken);

        if (generation != Volatile.Read(ref _loadGeneration) || cancellationToken.IsCancellationRequested)
        {
            document.CloseStream();
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
            using var stream = new MemoryStream();
            document.Save(stream, document.Reader.Endianness);
            cancellationToken.ThrowIfCancellationRequested();
            var data = stream.GetBuffer().AsSpan(0, checked((int)stream.Length));

            if (IsInWorkspace(path))
            {
                _workspace!.Replace(path, data);
                return;
            }

            if (path.IsArchiveEntry)
            {
                throw new InvalidOperationException("Archived GEOM files require an open workspace.");
            }

            File.WriteAllBytes(path.PhysicalPath, data);
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

    public void CloseDocumentStream()
    {
        Document?.CloseStream();
    }

    public void Unload()
    {
        Interlocked.Increment(ref _loadGeneration);
        UnloadDocument();
    }

    private void UnloadDocument()
    {
        Document?.CloseStream();
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
            throw new InvalidOperationException("Archived GEOM files require an open workspace.");
        }

        return File.OpenRead(path.PhysicalPath);
    }

    private Extensions.Endianness ResolveEndianness(WorkspacePath path)
    {
        return IsInWorkspace(path)
            ? _workspace!.Endianness
            : Extensions.EndianBinaryReader.DefaultEndianness;
    }

    private bool IsInWorkspace(WorkspacePath path)
    {
        return _workspace?.Snapshot?.TryGetFile(path, out _) == true;
    }
}
