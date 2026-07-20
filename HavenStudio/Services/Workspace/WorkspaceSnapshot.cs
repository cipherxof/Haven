using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HavenStudio.Services.Workspace;

public sealed class WorkspaceSnapshot
{
    private readonly IReadOnlyDictionary<WorkspacePath, WorkspaceFile> _filesByPath;

    public WorkspaceSnapshot(
        string rootPath,
        IEnumerable<string> directories,
        IEnumerable<WorkspaceFile> files)
    {
        RootPath = System.IO.Path.GetFullPath(rootPath);
        Directories = new ReadOnlyCollection<string>(directories.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        Files = new ReadOnlyCollection<WorkspaceFile>(files.ToList());
        var filesByPath = new Dictionary<WorkspacePath, WorkspaceFile>(WorkspacePathComparer.Instance);
        foreach (var file in Files)
        {
            filesByPath.TryAdd(file.Path, file);
        }

        _filesByPath = filesByPath;
    }

    public string RootPath { get; }
    public IReadOnlyList<string> Directories { get; }
    public IReadOnlyList<WorkspaceFile> Files { get; }

    public IEnumerable<WorkspaceFile> WithExtension(string extension)
    {
        var normalized = extension.StartsWith('.') ? extension : $".{extension}";
        return Files.Where(file => file.Extension.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<WorkspaceFile> InArchive(string archivePath)
    {
        var normalized = System.IO.Path.GetFullPath(archivePath);
        return Files.Where(file =>
            file.Path.IsArchiveEntry &&
            file.Path.PhysicalPath.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public bool TryGetFile(WorkspacePath path, out WorkspaceFile file)
    {
        return _filesByPath.TryGetValue(path, out file!);
    }

    private sealed class WorkspacePathComparer : IEqualityComparer<WorkspacePath>
    {
        public static WorkspacePathComparer Instance { get; } = new();

        public bool Equals(WorkspacePath? left, WorkspacePath? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.PhysicalPath.Equals(right.PhysicalPath, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.ArchiveEntryName, right.ArchiveEntryName, StringComparison.Ordinal);
        }

        public int GetHashCode(WorkspacePath path)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(path.PhysicalPath),
                path.ArchiveEntryName is null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(path.ArchiveEntryName));
        }
    }
}
