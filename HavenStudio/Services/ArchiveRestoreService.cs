using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Qar;

namespace HavenStudio.Services;

/// <summary>
/// Rebuilds a QAR or DAR archive from the files in a folder, the inverse of
/// <see cref="ArchiveDumpService.ExtractFiles"/>. Each file becomes an entry whose
/// name is its path relative to the folder, using forward slashes.
/// </summary>
public static class ArchiveRestoreService
{
    public sealed record RestoreResult(string ArchiveName, int EntryCount, byte[] Bytes);

    public static RestoreResult BuildFromFolder(
        string archiveName,
        string inputFolder,
        Endianness endianness)
    {
        if (string.IsNullOrWhiteSpace(archiveName)) throw new ArgumentException("Archive name is required.", nameof(archiveName));
        if (string.IsNullOrWhiteSpace(inputFolder)) throw new ArgumentException("Input folder is required.", nameof(inputFolder));
        if (!Directory.Exists(inputFolder)) throw new DirectoryNotFoundException($"Folder '{inputFolder}' was not found.");

        var extension = Path.GetExtension(archiveName).ToLowerInvariant();
        var entries = CollectEntries(inputFolder);
        if (entries.Count == 0)
        {
            throw new InvalidDataException("The selected folder contains no files to pack.");
        }

        var bytes = extension switch
        {
            ".qar" => BuildQar(entries, endianness),
            ".dar" => BuildDar(entries, endianness),
            _ => throw new NotSupportedException($"Archive type '{extension}' is not supported.")
        };

        return new RestoreResult(Path.GetFileName(archiveName), entries.Count, bytes);
    }

    private static List<(string Name, byte[] Data)> CollectEntries(string inputFolder)
    {
        var root = Path.GetFullPath(inputFolder);
        var entries = new List<(string Name, byte[] Data)>();
        foreach (var path in Directory
                     .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            entries.Add((relative, File.ReadAllBytes(path)));
        }

        return entries;
    }

    private static byte[] BuildQar(IReadOnlyList<(string Name, byte[] Data)> entries, Endianness endianness)
    {
        var qar = new Qar();
        foreach (var (name, data) in entries)
        {
            qar.Entries.Add(new QarEntry { Info = 0, Filename = name, Data = data });
        }

        using var stream = new MemoryStream();
        QarFile.Write(stream, qar, endianness);
        return stream.ToArray();
    }

    private static byte[] BuildDar(IReadOnlyList<(string Name, byte[] Data)> entries, Endianness endianness)
    {
        var dar = new Dar();
        foreach (var (name, data) in entries)
        {
            dar.Entries.Add(new DarEntry(name, data));
        }

        using var stream = new MemoryStream();
        DarFile.Write(stream, dar, endianness);
        return stream.ToArray();
    }
}
