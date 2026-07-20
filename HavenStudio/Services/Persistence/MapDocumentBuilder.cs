using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Extensions;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Geo;
using HavenStudio.Utils;

namespace HavenStudio.Services.Persistence;

public static class MapDocumentBuilder
{
    private const float RadiansToDegrees = 180f / MathF.PI;

    public static MapDocument Build(
        ReadOnlySpan<byte> gcxBytes,
        ReadOnlySpan<byte> geomBytes,
        MapDocumentSources sources,
        Endianness endianness,
        bool isMgs3 = false)
    {
        using var gcxStream = new MemoryStream(gcxBytes.ToArray(), writable: false);
        var gcx = GcxFile.Read(gcxStream);
        var geometryBytes = geomBytes.ToArray();
        using var geomStream = new MemoryStream(geometryBytes, writable: false);
        var geometry = new GeomFile(geomStream, endianness);
        try
        {
            return Build(gcx, geometry, geometryBytes, sources, isMgs3);
        }
        finally
        {
            geometry.CloseStream();
        }
    }

    public static MapDocument Build(
        Gcx gcx,
        GeomFile geometry,
        ReadOnlySpan<byte> geomBytes,
        MapDocumentSources sources,
        bool isMgs3 = false)
    {
        ArgumentNullException.ThrowIfNull(gcx);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(sources);

        var document = new MapDocument
        {
            Sources = new MapDocumentSources
            {
                Gcx = sources.Gcx,
                Geom = sources.Geom
            },
            Opaque = new MapOpaqueDocument
            {
                Gcx = GcxJsonConverter.ToJsonModel(gcx),
                GeomBytesBase64 = Convert.ToBase64String(geomBytes)
            }
        };

        document.Placements.AddRange(BuildPlacementProjections(gcx, geometry, isMgs3)
            .Select(projection => projection.Document));
        document.Geom.Effects.AddRange(
            TreeTraversal.Flatten(geometry.GeoEffects, effect => effect.Children).Select(ToEffectDocument));
        document.Geom.CollisionAttributes.AddRange(BuildCollisionAttributes(geometry));
        return document;
    }

    internal static IReadOnlyList<MapPlacementProjection> BuildPlacementProjections(
        Gcx gcx,
        GeomFile geometry,
        bool isMgs3)
    {
        var references = new GcxModelReferenceScanner().Scan(gcx, geometry, isMgs3);
        var identifiers = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<MapPlacementProjection>(references.PlacedModels.Count);
        for (var index = 0; index < references.PlacedModels.Count; index++)
        {
            var placement = references.PlacedModels[index];
            var binding = placement.Binding;
            var source = GetSource(placement);
            var baseIdentifier = binding == null
                ? $"unbound/placement@{index}"
                : $"{binding.ScriptName}/{binding.Site.CommandName}@0x{binding.Site.CommandOffset:X}";
            identifiers.TryGetValue(baseIdentifier, out var occurrence);
            identifiers[baseIdentifier] = occurrence + 1;
            var identifier = occurrence == 0 ? baseIdentifier : $"{baseIdentifier}#{occurrence + 1}";

            var dto = new MapPlacementDocument
            {
                Id = identifier,
                Command = binding?.Site.CommandName ?? "unknown",
                ModelHash = placement.ModelHash,
                Position = placement.Position is { } position
                    ? [position.X, position.Y, position.Z]
                    : null,
                DirectionDegrees = placement.Rotation is { } rotation
                    ?
                    [
                        rotation.X * RadiansToDegrees,
                        rotation.Y * RadiansToDegrees,
                        rotation.Z * RadiansToDegrees
                    ]
                    : null,
                EffectHash = placement.EffectHash,
                Source = source,
                Editable = IsEditable(placement, source)
            };
            result.Add(new MapPlacementProjection(dto, placement));
        }
        return result;
    }

    private static string GetSource(PlacedModelReference placement)
    {
        if (placement.Binding is { } binding &&
            (binding.Site.IsNested || binding.ForeachRowIndex != null))
        {
            return MapPlacementSources.Foreach;
        }
        if (placement.SourceEffect != null)
        {
            return MapPlacementSources.Effect;
        }
        return MapPlacementSources.Position;
    }

    private static bool IsEditable(PlacedModelReference placement, string source)
    {
        return source switch
        {
            MapPlacementSources.Foreach => false,
            MapPlacementSources.Effect => placement.SourceEffect is { Index: var index } &&
                ((index & 2) != 0 || ((index >> 10) & 0x3FF) != 0),
            _ => placement.Binding?.Site.Editable == true
        };
    }

    private static MapEffectDocument ToEffectDocument(GeoEffect effect)
    {
        return new MapEffectDocument
        {
            ChunkOffset = effect.ChunkOffset,
            Name = effect.Name,
            Index = effect.Index,
            Position = [effect.X, effect.Y, effect.Z, effect.W],
            RotationDegrees =
            [
                effect.RotationX * RadiansToDegrees,
                effect.RotationY * RadiansToDegrees,
                effect.RotationZ * RadiansToDegrees
            ]
        };
    }

    private static IEnumerable<MapCollisionAttributeDocument> BuildCollisionAttributes(GeomFile geometry)
    {
        for (var blockIndex = 0; blockIndex < geometry.GeomBlocks.Count; blockIndex++)
        {
            var block = geometry.GeomBlocks[blockIndex];
            yield return new MapCollisionAttributeDocument
            {
                Target = MapCollisionAttributeTargets.Block,
                Block = blockIndex,
                Attribute = block.Attribute
            };

            if (!geometry.BlockFaceData.TryGetValue(block, out var primitives))
            {
                continue;
            }
            for (var primitiveIndex = 0; primitiveIndex < primitives.Count; primitiveIndex++)
            {
                var primitive = primitives[primitiveIndex];
                yield return new MapCollisionAttributeDocument
                {
                    Target = MapCollisionAttributeTargets.Primitive,
                    Block = blockIndex,
                    Primitive = primitiveIndex,
                    Attribute = primitive.Attribute
                };

                if (primitive.Poly == null)
                {
                    continue;
                }
                for (var polygonIndex = 0; polygonIndex < primitive.Poly.Length; polygonIndex++)
                {
                    yield return new MapCollisionAttributeDocument
                    {
                        Target = MapCollisionAttributeTargets.Polygon,
                        Block = blockIndex,
                        Primitive = primitiveIndex,
                        Polygon = polygonIndex,
                        Attribute = primitive.Poly[polygonIndex].Attribute
                    };
                }
            }
        }
    }

}

internal sealed record MapPlacementProjection(
    MapPlacementDocument Document,
    PlacedModelReference Placement);
