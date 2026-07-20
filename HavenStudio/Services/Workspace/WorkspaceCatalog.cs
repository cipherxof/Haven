using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Qar;
using HavenStudio.Services;
using Serilog;

namespace HavenStudio.Services.Workspace;

/// <summary>
/// An instance-scoped index and I/O boundary for one opened workspace.
/// </summary>
public sealed class WorkspaceCatalog : IWorkspaceCatalog
{
    private static readonly ILogger Log = Serilog.Log.ForContext<WorkspaceCatalog>();

    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CachedArchive> _archiveCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _snapshotLock = new();
    private WorkspaceSnapshot? _snapshot;

    public WorkspaceCatalog(string rootPath, Endianness endianness)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("A workspace root is required.", nameof(rootPath));
        }

        RootPath = Path.GetFullPath(rootPath);
        Endianness = endianness;
    }

    public string RootPath { get; }
    public Endianness Endianness { get; }

    public WorkspaceSnapshot? Snapshot
    {
        get
        {
            lock (_snapshotLock)
            {
                return _snapshot;
            }
        }
    }

    public Task<WorkspaceSnapshot> ScanAsync(
        CancellationToken cancellationToken = default,
        IProgress<WorkspaceScanProgress>? progress = null)
    {
        return Task.Run(() => Scan(cancellationToken, progress), cancellationToken);
    }

    public Stream OpenRead(WorkspacePath path)
    {
        EnsureBelongsToWorkspace(path);
        if (!path.IsArchiveEntry)
        {
            return new FileStream(path.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        var archive = GetArchive(path.PhysicalPath);
        var entry = archive.Entries.FirstOrDefault(candidate =>
            candidate.Name.Equals(path.ArchiveEntryName, StringComparison.Ordinal));
        if (entry is null)
        {
            throw new FileNotFoundException(
                $"Entry '{path.ArchiveEntryName}' was not found in '{path.PhysicalPath}'.",
                path.ToString());
        }

        return new MemoryStream(entry.Data, writable: false);
    }

    public byte[] ReadAllBytes(WorkspacePath path)
    {
        using var stream = OpenRead(path);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public void Replace(WorkspacePath path, ReadOnlySpan<byte> data)
    {
        EnsureBelongsToWorkspace(path);
        var replacement = data.ToArray();
        if (!path.IsArchiveEntry)
        {
            File.WriteAllBytes(path.PhysicalPath, replacement);
            InvalidateArchive(path.PhysicalPath);
            if (path.Extension is ".qar" or ".dar")
            {
                RefreshArchiveInSnapshot(path.PhysicalPath);
            }
            else
            {
                RefreshPhysicalFileInSnapshot(path, replacement.LongLength);
            }
            return;
        }

        var extension = Path.GetExtension(path.PhysicalPath).ToLowerInvariant();
        switch (extension)
        {
            case ".qar":
                ReplaceQarEntry(path, replacement);
                break;
            case ".dar":
                ReplaceDarEntry(path, replacement);
                break;
            default:
                throw new NotSupportedException($"Archive type '{extension}' is not supported.");
        }

        InvalidateArchive(path.PhysicalPath);
        RefreshArchiveInSnapshot(path.PhysicalPath);
    }

    public ArchiveDumpService.ExtractSummary ExtractArchive(WorkspacePath archivePath, string outputFolder)
    {
        EnsureBelongsToWorkspace(archivePath);
        return ArchiveDumpService.ExtractFiles(archivePath.PhysicalPath, outputFolder, Endianness);
    }

    public void InvalidateArchive(string archivePath)
    {
        var normalized = NormalizePath(archivePath);
        lock (_cacheLock)
        {
            _archiveCache.Remove(normalized);
        }
    }

    private WorkspaceSnapshot Scan(
        CancellationToken cancellationToken,
        IProgress<WorkspaceScanProgress>? progress)
    {
        if (!Directory.Exists(RootPath))
        {
            throw new DirectoryNotFoundException($"Workspace root '{RootPath}' was not found.");
        }

        var directories = new List<string>();
        var files = new List<WorkspaceFile>();
        var discoveredArchives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(RootPath);
        var physicalFilesScanned = 0;
        var archiveEntriesIndexed = 0;

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pendingDirectories.Pop();
            foreach (var entryPath in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(entryPath))
                {
                    var relativeDirectory = Path.GetRelativePath(RootPath, entryPath);
                    directories.Add(relativeDirectory);
                    pendingDirectories.Push(entryPath);
                    continue;
                }

                if (!File.Exists(entryPath))
                {
                    continue;
                }

                var info = new FileInfo(entryPath);
                var physicalPath = WorkspacePath.Physical(info.FullName);
                var relativePath = Path.GetRelativePath(RootPath, info.FullName);
                files.Add(new WorkspaceFile(physicalPath, relativePath, info.Length));
                physicalFilesScanned++;

                if (physicalPath.Extension is ".qar" or ".dar")
                {
                    discoveredArchives.Add(physicalPath.PhysicalPath);
                    try
                    {
                        var archive = GetArchive(physicalPath.PhysicalPath);
                        foreach (var archiveEntry in archive.Entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            files.Add(new WorkspaceFile(
                                WorkspacePath.ArchiveEntry(physicalPath.PhysicalPath, archiveEntry.Name),
                                relativePath,
                                archiveEntry.Data.LongLength));
                            archiveEntriesIndexed++;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Log.Error(ex, "Failed to index archive {ArchivePath}", physicalPath.PhysicalPath);
                    }
                }

                progress?.Report(new WorkspaceScanProgress(
                    physicalFilesScanned,
                    archiveEntriesIndexed,
                    relativePath));
            }
        }

        RemoveUndiscoveredArchives(discoveredArchives);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = new WorkspaceSnapshot(RootPath, directories, files);
        lock (_snapshotLock)
        {
            _snapshot = snapshot;
        }

        return snapshot;
    }

    private CachedArchive GetArchive(string archivePath)
    {
        var normalized = NormalizePath(archivePath);
        var info = new FileInfo(normalized);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Archive was not found.", normalized);
        }

        lock (_cacheLock)
        {
            if (_archiveCache.TryGetValue(normalized, out var cached) &&
                cached.LastWriteTimeUtc == info.LastWriteTimeUtc &&
                cached.Length == info.Length)
            {
                return cached;
            }

            var loaded = LoadArchive(normalized, info.LastWriteTimeUtc, info.Length);
            _archiveCache[normalized] = loaded;
            return loaded;
        }
    }

    private CachedArchive LoadArchive(string archivePath, DateTime lastWriteTimeUtc, long length)
    {
        using var stream = File.OpenRead(archivePath);
        var extension = Path.GetExtension(archivePath).ToLowerInvariant();
        IReadOnlyList<CachedArchiveEntry> entries = extension switch
        {
            ".qar" => QarFile.Read(stream, Endianness).Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Filename))
                .Select(entry => new CachedArchiveEntry(entry.Filename!, entry.Data ?? []))
                .ToList(),
            ".dar" => DarFile.Read(stream, Endianness).Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Filename))
                .Select(entry => new CachedArchiveEntry(entry.Filename, entry.Bytes ?? []))
                .ToList(),
            _ => throw new NotSupportedException($"Archive type '{extension}' is not supported.")
        };

        return new CachedArchive(lastWriteTimeUtc, length, entries);
    }

    private void ReplaceQarEntry(WorkspacePath path, byte[] replacement)
    {
        Qar archive;
        using (var input = File.OpenRead(path.PhysicalPath))
        {
            archive = QarFile.Read(input, Endianness);
        }

        var entry = archive.Entries.FirstOrDefault(candidate =>
            candidate.Filename == path.ArchiveEntryName)
            ?? throw new FileNotFoundException(
                $"Entry '{path.ArchiveEntryName}' was not found in '{path.PhysicalPath}'.");
        entry.Data = replacement;

        using var output = new FileStream(path.PhysicalPath, FileMode.Create, FileAccess.Write, FileShare.None);
        QarFile.Write(output, archive, Endianness);
    }

    private void ReplaceDarEntry(WorkspacePath path, byte[] replacement)
    {
        Dar archive;
        using (var input = File.OpenRead(path.PhysicalPath))
        {
            archive = DarFile.Read(input, Endianness);
        }

        var entry = archive.Entries.FirstOrDefault(candidate =>
            candidate.Filename == path.ArchiveEntryName)
            ?? throw new FileNotFoundException(
                $"Entry '{path.ArchiveEntryName}' was not found in '{path.PhysicalPath}'.");
        entry.Bytes = replacement;

        using var output = new FileStream(path.PhysicalPath, FileMode.Create, FileAccess.Write, FileShare.None);
        DarFile.Write(output, archive, Endianness);
    }

    private void RefreshPhysicalFileInSnapshot(WorkspacePath path, long length)
    {
        lock (_snapshotLock)
        {
            if (_snapshot is null)
            {
                return;
            }

            var found = false;
            var files = _snapshot.Files.Select(file =>
            {
                if (file.Path != path)
                {
                    return file;
                }
                found = true;
                return file with { Length = length };
            }).ToList();
            if (!found)
            {
                files.Add(new WorkspaceFile(
                    path,
                    Path.GetRelativePath(RootPath, path.PhysicalPath),
                    length));
            }
            _snapshot = new WorkspaceSnapshot(RootPath, _snapshot.Directories, files);
        }
    }

    private void RefreshArchiveInSnapshot(string archivePath)
    {
        var normalized = NormalizePath(archivePath);
        lock (_snapshotLock)
        {
            if (_snapshot is null)
            {
                return;
            }

            var archive = GetArchive(normalized);
            var info = new FileInfo(normalized);
            var relativePath = Path.GetRelativePath(RootPath, normalized);
            var files = _snapshot.Files
                .Where(file => !file.Path.IsArchiveEntry ||
                    !file.Path.PhysicalPath.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                .Select(file =>
                    !file.Path.IsArchiveEntry && file.Path.PhysicalPath.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                        ? file with { Length = info.Length }
                        : file)
                .ToList();

            files.AddRange(archive.Entries.Select(entry => new WorkspaceFile(
                WorkspacePath.ArchiveEntry(normalized, entry.Name),
                relativePath,
                entry.Data.LongLength)));
            _snapshot = new WorkspaceSnapshot(RootPath, _snapshot.Directories, files);
        }
    }

    private void RemoveUndiscoveredArchives(HashSet<string> discoveredArchives)
    {
        lock (_cacheLock)
        {
            foreach (var cachedPath in _archiveCache.Keys
                         .Where(path => !discoveredArchives.Contains(path))
                         .ToList())
            {
                _archiveCache.Remove(cachedPath);
            }
        }
    }

    private void EnsureBelongsToWorkspace(WorkspacePath path)
    {
        var relative = Path.GetRelativePath(RootPath, path.PhysicalPath);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Path '{path.PhysicalPath}' is outside workspace '{RootPath}'.",
                nameof(path));
        }
    }

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private sealed record CachedArchive(
        DateTime LastWriteTimeUtc,
        long Length,
        IReadOnlyList<CachedArchiveEntry> Entries);

    private sealed record CachedArchiveEntry(string Name, byte[] Data);
}
