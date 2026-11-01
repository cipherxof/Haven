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
    private byte[]? _originalBytes;
    private long _editGeneration;

    public GeomFile? Document { get; private set; }
    public WorkspacePath? CurrentPath { get; private set; }
    public bool IsDirty { get; private set; }
    public bool HasDocument => Document != null && CurrentPath != null;
    public bool RequiresFullRebuild { get; private set; }
    public string? LastSaveBackupPath { get; private set; }

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

        var loadedDocument = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var source = OpenRead(path);
            using var copy = new MemoryStream();
            source.CopyTo(copy);
            var originalBytes = copy.ToArray();
            var stream = new MemoryStream(originalBytes, writable: false);
            try
            {
                var loaded = new GeomFile(stream, ResolveEndianness(path));
                cancellationToken.ThrowIfCancellationRequested();
                return (Document: loaded, OriginalBytes: originalBytes);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }, cancellationToken);

        if (generation != Volatile.Read(ref _loadGeneration) || cancellationToken.IsCancellationRequested)
        {
            loadedDocument.Document.CloseStream();
            return false;
        }

        Document = loadedDocument.Document;
        _originalBytes = loadedDocument.OriginalBytes;
        CurrentPath = path;
        IsDirty = false;
        RequiresFullRebuild = false;
        return true;
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        var document = Document;
        var path = CurrentPath;
        var originalBytes = _originalBytes;
        var editGeneration = Volatile.Read(ref _editGeneration);
        if (document == null || path == null || originalBytes == null)
        {
            return false;
        }

        var savedBytes = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new MemoryStream();
            if (RequiresFullRebuild)
            {
                document.Save(stream, document.Reader.Endianness);
            }
            else
            {
                document.SaveSurgicalEdits(originalBytes, stream, document.Reader.Endianness);
            }
            cancellationToken.ThrowIfCancellationRequested();
            var data = stream.GetBuffer().AsSpan(0, checked((int)stream.Length));

            // Compare the actual source before writing and verify exact read-back.
            // Existing encrypted siblings are deliberately not silently replaced.
            var inWorkspace = IsInWorkspace(path);
            if (path.IsArchiveEntry && _workspace is WorkspaceCatalog concrete)
                concrete.InvalidateArchive(path.PhysicalPath);
            byte[] ReadCurrent() => inWorkspace ? _workspace!.ReadAllBytes(path) : File.ReadAllBytes(path.PhysicalPath);
            if (!ReadCurrent().AsSpan().SequenceEqual(originalBytes))
                throw new IOException("The GEOM changed on disk since loading. Reload before saving; no changes were overwritten.");
            LastSaveBackupPath = null;
            if (!data.SequenceEqual(originalBytes))
            {
                var backup = $"{path.PhysicalPath}.haven-geom-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bak";
                using (var backupFile = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    backupFile.Write(File.ReadAllBytes(path.PhysicalPath));
                LastSaveBackupPath = backup;
            }
            if (inWorkspace)
            {
                _workspace!.Replace(path, data);
            }
            else if (path.IsArchiveEntry)
            {
                throw new InvalidOperationException("Archived GEOM files require an open workspace.");
            }
            else File.WriteAllBytes(path.PhysicalPath, data);
            if (path.IsArchiveEntry && _workspace is WorkspaceCatalog savedWorkspace)
                savedWorkspace.InvalidateArchive(path.PhysicalPath);
            if (!ReadCurrent().AsSpan().SequenceEqual(data))
                throw new IOException($"GEOM save read-back failed. Original retained at {LastSaveBackupPath}.");
            return data.ToArray();
        }, cancellationToken);

        if (ReferenceEquals(Document, document) && Equals(CurrentPath, path))
        {
            IsDirty = editGeneration != Volatile.Read(ref _editGeneration);
            if (!IsDirty) RequiresFullRebuild = false;
            _originalBytes = savedBytes;
        }

        return true;
    }

    public void MarkDirty(bool requiresFullRebuild = false)
    {
        if (Document != null)
        {
            Interlocked.Increment(ref _editGeneration);
            IsDirty = true;
            RequiresFullRebuild |= requiresFullRebuild;
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
        _originalBytes = null;
        CurrentPath = null;
        IsDirty = false;
        RequiresFullRebuild = false;
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
