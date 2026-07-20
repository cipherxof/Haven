using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Formats.Dds;
using HavenStudio.Formats.Dld;
using HavenStudio.Formats.Dlz;
using HavenStudio.Formats.Txn;
using HavenStudio.Services.Workspace;
using HavenStudio.Utils;
using Serilog;

namespace HavenStudio.Services;

/// <summary>
/// Decodes the level textures stored in a stage's <c>online_map.txn</c> for the minimap
/// panel. The grid texture is skipped; the remaining textures become one decoded RGBA
/// image per level and carry the game's stage-specific world-to-map projection.
/// </summary>
public static class MinimapService
{
    private const string OnlineMapFileName = "online_map.txn";
    private const double PatchedScaleUnit = 0.05;

    private static readonly ILogger Log = Serilog.Log.ForContext(typeof(MinimapService));

    public sealed record MinimapTexture(
        string Label,
        int Width,
        int Height,
        byte[] Rgba,
        MinimapProjection? Projection);

    public static IReadOnlyList<MinimapTexture> Load(IWorkspaceCatalog workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var snapshot = workspace.Snapshot;
        if (snapshot is null)
        {
            return [];
        }

        var txnFile = snapshot.Files.FirstOrDefault(file =>
            string.Equals(file.Name, OnlineMapFileName, StringComparison.OrdinalIgnoreCase));
        if (txnFile is null)
        {
            return [];
        }

        TxnFile txn;
        try
        {
            using var stream = workspace.OpenRead(txnFile.Path);
            txn = new TxnFile(stream, workspace.Endianness);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Failed to read minimap TXN '{TxnPath}'.", txnFile.Path);
            return [];
        }

        var containers = new LazyContainerSet(workspace, txnFile.Path);
        var textures = new List<MinimapTexture>();
        var workspaceStage = Path.GetFileName(Path.TrimEndingDirectorySeparator(workspace.RootPath));

        for (int index = 0; index < txn.ImageInfo.Count; index++)
        {
            var info = txn.ImageInfo[index];
            var label = ResolveName(info.TexId);
            if (IsGrid(label))
            {
                continue;
            }

            var imageIndex = txn.GetIndex(info);
            if (imageIndex < 0 || imageIndex >= txn.Images.Count)
            {
                continue;
            }

            var image = txn.Images[imageIndex];
            var format = GetDxtFormat(image.FourCC);
            if (format is null)
            {
                continue;
            }

            int width = info.Width > 0 ? info.Width : image.Width;
            int height = info.Height > 0 ? info.Height : image.Height;
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            if (!containers.TryFindMainTexture(info.TriId, index, out var data))
            {
                continue;
            }

            byte[] rgba;
            try
            {
                rgba = DxtDecoder.DecodeToRgba(width, height, format, data);
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Failed to decode minimap texture '{Label}'.", label);
                continue;
            }

            textures.Add(new MinimapTexture(
                label,
                width,
                height,
                rgba,
                ResolveProjection(label, workspaceStage)));
        }

        return textures;
    }

    private static MinimapProjection? ResolveProjection(string label, string workspaceStage)
    {
        if (ResolveStageProjection(workspaceStage) is { } workspaceProjection)
        {
            return workspaceProjection;
        }

        var mapPrefix = label.IndexOf("map_", StringComparison.OrdinalIgnoreCase);
        if (mapPrefix < 0 || label.Length < mapPrefix + 9)
        {
            return null;
        }

        return ResolveStageProjection(label.Substring(mapPrefix + 4, 5));
    }

    private static MinimapProjection? ResolveStageProjection(string stage)
    {
        if (stage.Length < 5)
        {
            return null;
        }

        return stage[..5].ToLowerInvariant() switch
        {
            "n001a" => new MinimapProjection(0.080, 0.080, 0, -5000, 8192, 8192),
            "n002a" => new MinimapProjection(0.085, 0.088, 1500, 98500, 9638, 9638),
            "n003a" => new MinimapProjection(0.090, 0.090, -1420, -12700, 8192, 8192),
            "n004a" => new MinimapProjection(0.060, 0.060, -17500, 63000, 8192, 8192),
            "n005a" => new MinimapProjection(0.078, 0.080, 5500, 5500, 8192, 8192),
            "n007a" => new MinimapProjection(0.118, 0.118, 200, -1600, 8192, 8192),
            "n008a" => new MinimapProjection(0.078, 0.078, 1800, -1000, 8192, 8192),
            "n012a" => new MinimapProjection(0.068, 0.068, 7000, 88500, 16384, 8192),
            "n014a" => new MinimapProjection(0.048, 0.048, 2000, -39000, 8192, 8192),
            // These stages patch scale values consumed in 0.05-unit increments before
            // calling the game's minimap actor. The other patched globals control HUD
            // presentation and do not change texture-local marker coordinates.
            "sm_ll" => PatchedProjection(2.2, 1.0, -8000, 0),
            "n020a" => PatchedProjection(1.4, 2.0, -4000, 104000),
            "sm_dd" => PatchedProjection(1.4, 1.4, -23000, -11500),
            "n022a" => PatchedProjection(3.5, 1.35, -17000, -20000),
            "n024a" => PatchedProjection(1.15, 1.45, -9000, -18000),
            _ => null
        };
    }

    private static MinimapProjection PatchedProjection(
        double scaleX,
        double scaleZ,
        double offsetX,
        double offsetZ) =>
        new(
            scaleX * PatchedScaleUnit,
            scaleZ * PatchedScaleUnit,
            offsetX,
            offsetZ,
            8192,
            8192);

    private static bool IsGrid(string label) =>
        label.Contains("grid", StringComparison.OrdinalIgnoreCase);

    private static string ResolveName(uint hash) =>
        DictionaryFile.TryGetLookupName(hash, out var name) ? name : hash.ToString("X8");

    private static string? GetDxtFormat(ushort fourCc) => fourCc switch
    {
        9 => "DXT1",
        10 => "DXT3",
        11 => "DXT5",
        _ => null
    };

    /// <summary>
    /// Loads DLD/DLZ containers on demand while searching for texture data, trying the
    /// containers beside the TXN first so a co-located minimap never has to unpack the
    /// stage's large caches.
    /// </summary>
    private sealed class LazyContainerSet
    {
        private readonly IWorkspaceCatalog _workspace;
        private readonly List<WorkspaceFile> _orderedContainers;
        private readonly Dictionary<WorkspacePath, DldFile?> _loaded = new();

        public LazyContainerSet(IWorkspaceCatalog workspace, WorkspacePath txnPath)
        {
            _workspace = workspace;
            var snapshot = workspace.Snapshot;
            var containers = snapshot is null
                ? Enumerable.Empty<WorkspaceFile>()
                : snapshot.WithExtension(".dld").Concat(snapshot.WithExtension(".dlz"));
            _orderedContainers = containers
                .OrderByDescending(container => container.Path.IsSameDirectoryAs(txnPath))
                .ToList();
        }

        public bool TryFindMainTexture(uint triId, int entryNumber, out byte[] data)
        {
            foreach (var container in _orderedContainers)
            {
                var dld = EnsureLoaded(container);
                var texture = dld?.FindTexture(triId, entryNumber, DldPriority.Main);
                if (texture != null)
                {
                    data = texture.Data;
                    return true;
                }
            }

            data = [];
            return false;
        }

        private DldFile? EnsureLoaded(WorkspaceFile container)
        {
            if (_loaded.TryGetValue(container.Path, out var cached))
            {
                return cached;
            }

            DldFile? dld = null;
            try
            {
                using var stream = _workspace.OpenRead(container.Path);
                if (string.Equals(container.Extension, ".dlz", StringComparison.OrdinalIgnoreCase))
                {
                    var dlz = new DlzFile(stream, _workspace.Endianness);
                    using var unpacked = new MemoryStream();
                    dlz.Unpack(unpacked);
                    unpacked.Position = 0;
                    dld = new DldFile(unpacked, _workspace.Endianness);
                }
                else
                {
                    dld = new DldFile(stream, _workspace.Endianness);
                }
            }
            catch (Exception exception)
            {
                Log.Warning(exception, "Failed to load minimap container '{ContainerPath}'.", container.Path);
            }

            _loaded[container.Path] = dld;
            return dld;
        }
    }
}
