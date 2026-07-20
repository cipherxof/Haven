using System;
using System.Collections.Generic;
using System.Linq;
using HavenStudio.Formats.Geo;
using HavenStudio.Utils;

namespace HavenStudio.Services;

/// <summary>A single spawn placement projected onto the [0,1] minimap plane.</summary>
public sealed record SpawnMarker(string Label, double U, double V, float WorldX, float WorldZ);

/// <summary>The scale, additive world offset, and UI canvas extent used by the game.</summary>
public readonly record struct MinimapProjection(
    double ScaleX,
    double ScaleZ,
    double OffsetX,
    double OffsetZ,
    double CanvasWidth,
    double CanvasHeight);

/// <summary>A node in the spawn hierarchy (e.g. RULE, ITEM, CBOX) with its descendant spawns.</summary>
public sealed class SpawnGroup
{
    public required string Label { get; init; }
    public IReadOnlyList<SpawnGroup> Children { get; init; } = [];
    public IReadOnlyList<SpawnMarker> Spawns { get; init; } = [];
    public int SpawnCount => Spawns.Count;
    public string Display => $"{Label}  ({SpawnCount})";
}

/// <summary>
/// Turns a GEOM's effect tree ("props") into spawn markers grouped by the effect
/// hierarchy (RULE, ITEM, CBOX, START, …), with each marker's world position projected
/// onto the [0,1] minimap plane using the scale and world offsets from the game's
/// online-map setup. Stages without known setup values fall back to their GEOM bounds.
/// </summary>
public static class MinimapSpawns
{
    private static readonly uint SizeMarker01 = Utils.String.HashString("PRP_STAGE_SIZE_01");
    private static readonly uint SizeMarker02 = Utils.String.HashString("PRP_STAGE_SIZE_02");
    private static readonly uint CenterMarker = Utils.String.HashString("PRP_STAGE_CENTER");

    public static SpawnGroup Build(
        IReadOnlyList<GeoEffect> rootEffects,
        int mapWidth,
        int mapHeight,
        MinimapProjection? mapProjection = null)
    {
        ArgumentNullException.ThrowIfNull(rootEffects);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mapHeight);

        var all = Flatten(rootEffects).ToList();
        var fallbackBounds = mapProjection is null ? ResolveBounds(all) : default;

        double U(float x) => mapProjection is { } projection
            ? 0.5 + (x + projection.OffsetX) * projection.ScaleX / projection.CanvasWidth
            : (x - fallbackBounds.MinX) / fallbackBounds.SpanX;
        double V(float z) => mapProjection is { } projection
            ? 0.5 + (z + projection.OffsetZ) * projection.ScaleZ / projection.CanvasHeight
            : (z - fallbackBounds.MinZ) / fallbackBounds.SpanZ;

        SpawnGroup BuildNode(GeoEffect effect)
        {
            var spawns = new List<SpawnMarker>();
            var children = new List<SpawnGroup>();

            if (IsSpawn(effect))
            {
                spawns.Add(new SpawnMarker(Resolve(effect.Name), U(effect.X), V(effect.Z), effect.X, effect.Z));
            }

            foreach (var child in effect.Children)
            {
                var node = BuildNode(child);
                spawns.AddRange(node.Spawns);
                if (child.Children.Count > 0 && node.Spawns.Count > 0)
                {
                    children.Add(node);
                }
            }

            return new SpawnGroup { Label = Resolve(effect.Name), Children = children, Spawns = spawns };
        }

        var rootGroups = rootEffects
            .Select(BuildNode)
            .Where(group => group.Spawns.Count > 0)
            .ToList();

        return new SpawnGroup
        {
            Label = "All spawns",
            Children = rootGroups,
            Spawns = rootGroups.SelectMany(group => group.Spawns).ToList()
        };
    }

    private static (float MinX, float MinZ, float SpanX, float SpanZ) ResolveBounds(
        IReadOnlyList<GeoEffect> all)
    {
        var size1 = FindMarker(all, SizeMarker01);
        var size2 = FindMarker(all, SizeMarker02);

        float minX, maxX, minZ, maxZ;
        if (size1 is { } s1 && size2 is { } s2)
        {
            minX = MathF.Min(s1.X, s2.X);
            maxX = MathF.Max(s1.X, s2.X);
            minZ = MathF.Min(s1.Z, s2.Z);
            maxZ = MathF.Max(s1.Z, s2.Z);
        }
        else
        {
            var spawns = all.Where(IsSpawn).ToList();
            if (spawns.Count == 0)
            {
                return (0f, 0f, 1f, 1f);
            }

            minX = spawns.Min(effect => effect.X);
            maxX = spawns.Max(effect => effect.X);
            minZ = spawns.Min(effect => effect.Z);
            maxZ = spawns.Max(effect => effect.Z);
            var marginX = (maxX - minX) * 0.05f;
            var marginZ = (maxZ - minZ) * 0.05f;
            minX -= marginX;
            maxX += marginX;
            minZ -= marginZ;
            maxZ += marginZ;
        }

        var spanX = maxX - minX;
        var spanZ = maxZ - minZ;
        return (minX, minZ, spanX > 0 ? spanX : 1f, spanZ > 0 ? spanZ : 1f);
    }

    private static GeoEffect? FindMarker(IEnumerable<GeoEffect> all, uint nameHash) =>
        all.FirstOrDefault(effect => (uint)effect.Name == nameHash);

    private static bool IsSpawn(GeoEffect effect)
    {
        if (GeoEffectLayout.GetPositionSlot(effect.Index) == 0)
        {
            return false;
        }

        var name = (uint)effect.Name;
        return name != SizeMarker01 && name != SizeMarker02 && name != CenterMarker;
    }

    private static IEnumerable<GeoEffect> Flatten(IEnumerable<GeoEffect> effects)
    {
        foreach (var effect in effects)
        {
            yield return effect;
            foreach (var child in Flatten(effect.Children))
            {
                yield return child;
            }
        }
    }

    private static string Resolve(int nameHash)
    {
        var hash = (uint)nameHash;
        return DictionaryFile.TryGetLookupName(hash, out var name) ? name : hash.ToString("X8");
    }

}
