using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HavenStudio.Services.Workspace;

public static class StageManifestResolver
{
    /// <summary>
    /// Returns existing physical stage assets explicitly named by a plaintext
    /// data.cnf. Files in nested backup/dump folders are deliberately excluded
    /// unless that folder has its own manifest naming them.
    /// </summary>
    public static IReadOnlySet<string> FindListedPhysicalPaths(
        IWorkspaceCatalog catalog,
        WorkspaceSnapshot snapshot,
        params string[] extensions)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(extensions);

        var normalizedExtensions = extensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.StartsWith('.') ? extension : $".{extension}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (normalizedExtensions.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var physicalFiles = snapshot.Files
            .Where(file => !file.Path.IsArchiveEntry)
            .Select(file => file.Path.PhysicalPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var listedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in snapshot.Files
                     .Where(file => !file.Path.IsArchiveEntry)
                     .Where(file => file.Path.FileName.Equals("data.cnf", StringComparison.OrdinalIgnoreCase)))
        {
            var manifestDirectory = Path.GetDirectoryName(manifest.Path.PhysicalPath);
            if (manifestDirectory == null)
            {
                continue;
            }

            string manifestText;
            try
            {
                manifestText = Encoding.UTF8.GetString(catalog.ReadAllBytes(manifest.Path));
            }
            catch
            {
                continue;
            }

            foreach (var rawLine in manifestText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var fileName = rawLine.Trim().Trim('"');
                if (fileName.StartsWith('@'))
                {
                    fileName = fileName[1..].TrimStart();
                }

                if (fileName.Length == 0 || fileName.StartsWith('.'))
                {
                    continue;
                }

                if (!normalizedExtensions.Contains(Path.GetExtension(fileName)))
                {
                    continue;
                }

                var candidate = Path.GetFullPath(Path.Combine(
                    manifestDirectory,
                    fileName.Replace('/', Path.DirectorySeparatorChar)));
                if (physicalFiles.Contains(candidate))
                {
                    listedPaths.Add(candidate);
                }
            }
        }

        return listedPaths;
    }

    public static WorkspacePath? FindGeomPath(IWorkspaceCatalog catalog, WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);

        var geomFiles = snapshot.WithExtension(".geom").ToArray();
        foreach (var manifest in snapshot.Files
                     .Where(file => file.Path.FileName.Equals("data.cnf", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(file => file.Path.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            var configuredName = FindGeomFileName(
                Encoding.UTF8.GetString(catalog.ReadAllBytes(manifest.Path)));
            if (configuredName == null)
            {
                continue;
            }

            var configured = geomFiles.FirstOrDefault(file =>
                IsSameLogicalDirectory(manifest.Path, file.Path) &&
                file.Path.FileName.Equals(configuredName, StringComparison.OrdinalIgnoreCase));
            if (configured != null)
            {
                return configured.Path;
            }
        }

        return geomFiles
            .OrderBy(file => file.Path.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(file => file.Path)
            .FirstOrDefault();
    }

    public static WorkspacePath? FindGcxPath(
        IWorkspaceCatalog catalog,
        WorkspaceSnapshot snapshot,
        WorkspacePath? geomPath)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);

        var gcxFiles = snapshot.WithExtension(".gcx").ToArray();
        foreach (var manifest in snapshot.Files
                     .Where(file => file.Path.FileName.Equals("data.cnf", StringComparison.OrdinalIgnoreCase))
                     .Where(file => geomPath == null || IsSameLogicalDirectory(file.Path, geomPath))
                     .OrderBy(file => file.Path.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            var configuredName = FindFileName(
                Encoding.UTF8.GetString(catalog.ReadAllBytes(manifest.Path)),
                ".gcx");
            if (configuredName == null)
            {
                continue;
            }

            var configured = gcxFiles.FirstOrDefault(file =>
                IsSameLogicalDirectory(manifest.Path, file.Path) &&
                file.Path.FileName.Equals(configuredName, StringComparison.OrdinalIgnoreCase));
            if (configured != null)
            {
                return configured.Path;
            }
        }

        if (geomPath != null)
        {
            var besideGeom = gcxFiles
                .Where(file => IsSameLogicalDirectory(geomPath, file.Path))
                .OrderBy(file => file.Path.FileName.Equals("scenerio.gcx", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(file => file.Path.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(file => file.Path)
                .FirstOrDefault();
            if (besideGeom != null)
            {
                return besideGeom;
            }
        }

        return gcxFiles
            .OrderBy(file => file.Path.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(file => file.Path)
            .FirstOrDefault();
    }

    public static string? FindGeomFileName(string manifestText)
        => FindFileName(manifestText, ".geom");

    private static string? FindFileName(string manifestText, string extension)
    {
        if (string.IsNullOrWhiteSpace(manifestText))
        {
            return null;
        }

        return manifestText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSameLogicalDirectory(WorkspacePath left, WorkspacePath right)
    {
        if (left.IsArchiveEntry != right.IsArchiveEntry)
        {
            return false;
        }

        if (!left.IsArchiveEntry)
        {
            return string.Equals(
                Path.GetDirectoryName(left.PhysicalPath),
                Path.GetDirectoryName(right.PhysicalPath),
                StringComparison.OrdinalIgnoreCase);
        }

        if (!left.PhysicalPath.Equals(right.PhysicalPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(
            Path.GetDirectoryName(left.ArchiveEntryName),
            Path.GetDirectoryName(right.ArchiveEntryName),
            StringComparison.OrdinalIgnoreCase);
    }
}
