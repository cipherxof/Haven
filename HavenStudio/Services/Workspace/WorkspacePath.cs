using System;
using System.IO;

namespace HavenStudio.Services.Workspace;

/// <summary>
/// Identifies either a physical workspace file or an entry stored in an archive.
/// String conversion exists only as a compatibility boundary for older UI code.
/// </summary>
public sealed record WorkspacePath
{
    private const string LegacySeparator = "::";

    private WorkspacePath(string physicalPath, string? archiveEntryName)
    {
        PhysicalPath = NormalizePhysicalPath(physicalPath);
        ArchiveEntryName = string.IsNullOrWhiteSpace(archiveEntryName)
            ? null
            : archiveEntryName;
    }

    public string PhysicalPath { get; }
    public string? ArchiveEntryName { get; }
    public bool IsArchiveEntry => ArchiveEntryName is not null;
    public string FileName => ArchiveEntryName is null
        ? Path.GetFileName(PhysicalPath)
        : Path.GetFileName(ArchiveEntryName.Replace('\\', '/'));
    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

    public static WorkspacePath Physical(string physicalPath)
    {
        if (string.IsNullOrWhiteSpace(physicalPath))
        {
            throw new ArgumentException("A physical path is required.", nameof(physicalPath));
        }

        return new WorkspacePath(physicalPath, null);
    }

    public static WorkspacePath ArchiveEntry(string archivePath, string entryName)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            throw new ArgumentException("An archive path is required.", nameof(archivePath));
        }

        if (string.IsNullOrWhiteSpace(entryName))
        {
            throw new ArgumentException("An archive entry name is required.", nameof(entryName));
        }

        return new WorkspacePath(archivePath, entryName);
    }

    /// <summary>
    /// Converts the former archive::entry representation at an application boundary.
    /// Core workspace code should pass <see cref="WorkspacePath"/> directly.
    /// </summary>
    public static WorkspacePath ParseLegacy(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A path is required.", nameof(path));
        }

        var separatorIndex = path.IndexOf(LegacySeparator, StringComparison.Ordinal);
        return separatorIndex < 0
            ? Physical(path)
            : ArchiveEntry(
                path[..separatorIndex],
                path[(separatorIndex + LegacySeparator.Length)..]);
    }

    public string ToLegacyString()
    {
        return ArchiveEntryName is null
            ? PhysicalPath
            : $"{PhysicalPath}{LegacySeparator}{ArchiveEntryName}";
    }

    /// <summary>
    /// Whether this path and <paramref name="other"/> live in the same logical directory,
    /// meaning the same archive and entry folder for archive entries, or the same physical
    /// directory for loose files.
    /// </summary>
    public bool IsSameDirectoryAs(WorkspacePath other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsArchiveEntry != other.IsArchiveEntry)
        {
            return false;
        }

        if (IsArchiveEntry)
        {
            return PhysicalPath.Equals(other.PhysicalPath, StringComparison.OrdinalIgnoreCase)
                && ArchiveDirectory(ArchiveEntryName)
                    .Equals(ArchiveDirectory(other.ArchiveEntryName), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
            Path.GetDirectoryName(PhysicalPath),
            Path.GetDirectoryName(other.PhysicalPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ArchiveDirectory(string? entryName)
    {
        var normalized = (entryName ?? string.Empty).Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');
        return separator < 0 ? string.Empty : normalized[..separator];
    }

    public override string ToString() => ToLegacyString();

    private static string NormalizePhysicalPath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
