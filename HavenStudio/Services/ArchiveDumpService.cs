using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Qar;

namespace HavenStudio.Services;

public static class ArchiveDumpService
{
    public static ExtractSummary ExtractFiles(
        string archivePath,
        string outputFolder,
        Endianness? archiveEndianness = null)
    {
        if (string.IsNullOrWhiteSpace(archivePath)) throw new ArgumentException("Archive path is required.", nameof(archivePath));
        if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Output folder is required.", nameof(outputFolder));

        Directory.CreateDirectory(outputFolder);

        var extension = Path.GetExtension(archivePath).ToLowerInvariant();
        var archiveName = Path.GetFileName(archivePath);

        var endianness = archiveEndianness ?? EndianBinaryReader.DefaultEndianness;
        int extracted = extension switch
        {
            ".qar" => ExtractFromQar(archivePath, outputFolder, endianness),
            ".dar" => ExtractFromDar(archivePath, outputFolder, endianness),
            _ => throw new NotSupportedException($"Archive type '{extension}' is not supported.")
        };

        return new ExtractSummary { ArchiveName = archiveName, ExtractedCount = extracted, OutputFolder = outputFolder };
    }

    public static string DumpFileTable(
        string archivePath,
        string outputFolder,
        Endianness? archiveEndianness = null)
    {
        if (string.IsNullOrWhiteSpace(archivePath)) throw new ArgumentException("Archive path is required.", nameof(archivePath));
        if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Output folder is required.", nameof(outputFolder));

        Directory.CreateDirectory(outputFolder);

        var extension = Path.GetExtension(archivePath).ToLowerInvariant();
        var archiveName = Path.GetFileName(archivePath);

        var endianness = archiveEndianness ?? EndianBinaryReader.DefaultEndianness;
        ArchiveFileTable table = extension switch
        {
            ".qar" => BuildQarTable(archivePath, archiveName, endianness),
            ".dar" => BuildDarTable(archivePath, archiveName, endianness),
            _ => throw new NotSupportedException($"Archive type '{extension}' is not supported.")
        };

        var outputPath = Path.Combine(outputFolder, $"{archiveName}.filetable.json");
        var json = JsonSerializer.Serialize(table, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, json);
        return outputPath;
    }

    private static ArchiveFileTable BuildQarTable(string archivePath, string archiveName, Endianness endianness)
    {
        using var stream = File.OpenRead(archivePath);
        var qar = QarFile.Read(stream, endianness);

        var entries = new List<ArchiveEntry>(qar.Entries.Count);
        foreach (var entry in qar.Entries)
        {
            entries.Add(new ArchiveEntry
            {
                Name = entry.Filename ?? string.Empty,
                Size = entry.Data?.Length ?? 0,
                Info = entry.Info
            });
        }

        return new ArchiveFileTable
        {
            ArchiveName = archiveName,
            ArchiveType = "qar",
            EntryCount = entries.Count,
            Entries = entries
        };
    }

    private static ArchiveFileTable BuildDarTable(string archivePath, string archiveName, Endianness endianness)
    {
        using var stream = File.OpenRead(archivePath);
        var dar = DarFile.Read(stream, endianness);

        var entries = new List<ArchiveEntry>(dar.Entries.Count);
        foreach (var entry in dar.Entries)
        {
            entries.Add(new ArchiveEntry
            {
                Name = entry.Filename,
                Size = entry.Bytes?.Length ?? 0,
                Info = null
            });
        }

        return new ArchiveFileTable
        {
            ArchiveName = archiveName,
            ArchiveType = "dar",
            EntryCount = entries.Count,
            Entries = entries
        };
    }

    private static int ExtractFromQar(string archivePath, string outputFolder, Endianness endianness)
    {
        using var stream = File.OpenRead(archivePath);
        var qar = QarFile.Read(stream, endianness);

        var entries = new List<ExtractableEntry>();
        foreach (var entry in qar.Entries)
        {
            if (string.IsNullOrEmpty(entry.Filename) || entry.Data == null) continue;
            entries.Add(new ExtractableEntry(entry.Filename, entry.Data));
        }

        return ExtractEntries(entries, outputFolder);
    }

    private static int ExtractFromDar(string archivePath, string outputFolder, Endianness endianness)
    {
        using var stream = File.OpenRead(archivePath);
        var dar = DarFile.Read(stream, endianness);

        var entries = new List<ExtractableEntry>();
        foreach (var entry in dar.Entries)
        {
            if (string.IsNullOrEmpty(entry.Filename) || entry.Bytes == null) continue;
            entries.Add(new ExtractableEntry(entry.Filename, entry.Bytes));
        }

        return ExtractEntries(entries, outputFolder);
    }

    private static int ExtractEntries(IReadOnlyList<ExtractableEntry> entries, string outputFolder)
    {
        var outputRoot = Path.GetFullPath(outputFolder);
        var resolvedEntries = new List<ResolvedEntry>(entries.Count);
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var destination = ResolveEntryPath(outputRoot, entry.Name);
            if (!destinations.Add(destination))
            {
                throw new InvalidDataException($"Archive contains duplicate destination '{entry.Name}'.");
            }

            foreach (var existing in destinations)
            {
                if (existing == destination)
                {
                    continue;
                }

                if (IsParentPath(existing, destination) || IsParentPath(destination, existing))
                {
                    throw new InvalidDataException(
                        $"Archive entries have a file/directory collision involving '{entry.Name}'.");
                }
            }

            resolvedEntries.Add(new ResolvedEntry(destination, entry.Data));
        }

        foreach (var entry in resolvedEntries)
        {
            EnsureNoSymbolicLinkTraversal(outputRoot, entry.Path);
            var directory = Path.GetDirectoryName(entry.Path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(entry.Path, entry.Data);
        }

        return resolvedEntries.Count;
    }

    private static string ResolveEntryPath(string outputRoot, string entryName)
    {
        var normalizedName = entryName.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedName) ||
            normalizedName.StartsWith('/') ||
            Path.IsPathRooted(normalizedName) ||
            LooksLikeWindowsDrivePath(normalizedName))
        {
            throw new InvalidDataException($"Archive entry has an invalid rooted path: '{entryName}'.");
        }

        var segments = normalizedName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException($"Archive entry has an invalid relative path: '{entryName}'.");
        }

        string destination;
        try
        {
            destination = Path.GetFullPath(Path.Combine(outputRoot, Path.Combine(segments)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException($"Archive entry has an invalid path: '{entryName}'.", ex);
        }

        var relative = Path.GetRelativePath(outputRoot, destination);
        if (Path.IsPathRooted(relative) ||
            relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Archive entry escapes the output directory: '{entryName}'.");
        }

        return destination;
    }

    private static bool LooksLikeWindowsDrivePath(string path)
    {
        return path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }

    private static bool IsParentPath(string possibleParent, string path)
    {
        return path.StartsWith(
            possibleParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNoSymbolicLinkTraversal(string outputRoot, string destination)
    {
        var relative = Path.GetRelativePath(outputRoot, destination);
        var current = outputRoot;
        var segments = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < segments.Length - 1; i++)
        {
            current = Path.Combine(current, segments[i]);
            if (File.Exists(current) || Directory.Exists(current))
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        $"Archive entry would traverse a symbolic link: '{relative}'.");
                }
            }
        }
    }

    public sealed class ExtractSummary
    {
        public string ArchiveName { get; set; } = string.Empty;
        public int ExtractedCount { get; set; }
        public string OutputFolder { get; set; } = string.Empty;
    }

    private sealed class ArchiveFileTable
    {
        public string ArchiveName { get; set; } = string.Empty;
        public string ArchiveType { get; set; } = string.Empty;
        public int EntryCount { get; set; }
        public List<ArchiveEntry> Entries { get; set; } = new();
    }

    private sealed class ArchiveEntry
    {
        public string Name { get; set; } = string.Empty;
        public int Size { get; set; }
        public int? Info { get; set; }
    }

    private sealed record ExtractableEntry(string Name, byte[] Data);
    private sealed record ResolvedEntry(string Path, byte[] Data);
}
