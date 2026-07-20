using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Extensions;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Geo;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Services.Persistence;

public static class MapDocumentApplier
{
    private const float DegreesToRadians = MathF.PI / 180f;

    public static MapDocumentApplyResult Apply(
        MapDocument document,
        Endianness endianness,
        bool isMgs3 = false)
    {
        ValidateDocument(document);
        var gcx = GcxJsonConverter.FromJsonModel(document.Opaque.Gcx);
        byte[] originalGeomBytes;
        try
        {
            originalGeomBytes = Convert.FromBase64String(document.Opaque.GeomBytesBase64);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The opaque GEOM carrier is not valid base64.", exception);
        }
        if (originalGeomBytes.Length == 0)
        {
            throw new InvalidDataException("The opaque GEOM carrier is empty.");
        }

        using var geomStream = new MemoryStream(originalGeomBytes, writable: false);
        var geometry = new GeomFile(geomStream, endianness);
        try
        {
            var projections = MapDocumentBuilder.BuildPlacementProjections(gcx, geometry, isMgs3);
            var placementEffectEdits = ApplyPlacements(projections, document.Placements);
            var geomBytes = ApplyGeometry(
                geometry,
                originalGeomBytes,
                document.Geom,
                placementEffectEdits,
                endianness);

            using var gcxOutput = new MemoryStream();
            GcxFile.Write(gcxOutput, gcx);
            return new MapDocumentApplyResult(gcxOutput.ToArray(), geomBytes);
        }
        finally
        {
            geometry.CloseStream();
        }
    }

    private static IReadOnlyDictionary<GeoEffect, PlacementEffectEdit> ApplyPlacements(
        IReadOnlyList<MapPlacementProjection> baseline,
        IReadOnlyList<MapPlacementDocument> requested)
    {
        var baselineById = ToUniqueDictionary(
            baseline,
            projection => projection.Document.Id,
            "opaque placement");
        var requestedById = ToUniqueDictionary(requested, placement => placement.Id, "placement");
        EnsureSameKeys(baselineById.Keys, requestedById.Keys, "placement");

        var rows = new List<PlacementRow>(baseline.Count);
        foreach (var projection in baseline)
        {
            var target = requestedById[projection.Document.Id];
            ValidatePlacementProjection(projection.Document, target);
            rows.Add(new PlacementRow(projection, target));
        }

        var effectEdits = BuildPlacementEffectEdits(rows);
        var commandGroups = rows
            .Where(row => row.Projection.Placement.Binding != null)
            .GroupBy(row => new CommandKey(
                row.Projection.Placement.Binding!.Script,
                row.Projection.Placement.Binding.Site.CommandOffset))
            .GroupBy(group => group.Key.Script);

        foreach (var scriptGroups in commandGroups)
        {
            var script = scriptGroups.Key;
            var bytes = script.Bytes ?? Array.Empty<byte>();
            foreach (var commandRows in scriptGroups.OrderByDescending(group => group.Key.CommandOffset))
            {
                var commandRowsArray = commandRows.ToArray();
                var binding = commandRowsArray[0].Projection.Placement.Binding!;
                var site = binding.Site;

                var changedModelRows = commandRowsArray
                    .Where(row => row.Target.ModelHash != row.Projection.Document.ModelHash)
                    .ToArray();
                if (changedModelRows.Length > 0)
                {
                    if (site.IsNested || binding.ModelSite == null)
                    {
                        throw ReadOnlyPlacement(binding, "model hash");
                    }
                    var targetModel = RequireSingleValue(
                        changedModelRows.Select(row => row.Target.ModelHash),
                        binding,
                        "model hash");
                    EnsureCommandProjectionCompatibility(
                        commandRowsArray,
                        row => row.Target.ModelHash,
                        targetModel,
                        binding,
                        "model hash");
                    bytes = GcxPlacementWriter.WriteModelHash(bytes, binding.ModelSite, targetModel).Bytes;
                }

                var changedEffectRows = commandRowsArray
                    .Where(row => row.Target.EffectHash != row.Projection.Document.EffectHash)
                    .ToArray();
                if (changedEffectRows.Length > 0)
                {
                    var targetEffect = RequireSingleValue(
                        changedEffectRows.Select(row => row.Target.EffectHash),
                        binding,
                        "effect hash");
                    if (targetEffect is not { } hash || site.IsNested || site.Effect == null)
                    {
                        throw ReadOnlyPlacement(binding, "effect hash");
                    }
                    EnsureCommandProjectionCompatibility(
                        commandRowsArray,
                        row => row.Target.EffectHash,
                        targetEffect,
                        binding,
                        "effect hash");
                    bytes = GcxPlacementWriter.WriteEffectHash(bytes, site, hash).Bytes;
                }

                Vector3? position = null;
                var directPositionRows = commandRowsArray
                    .Where(row => row.Projection.Document.Source == MapPlacementSources.Position)
                    .ToArray();
                if (directPositionRows.Length > 0)
                {
                    position = GetChangedVector(
                        directPositionRows,
                        row => row.Projection.Document.Position,
                        row => row.Target.Position,
                        binding,
                        "position");
                }

                Vector3? direction = null;
                var directDirectionRows = commandRowsArray
                    .Where(row => site.Direction?.HasThreeLiteralComponents == true && !site.IsNested)
                    .ToArray();
                if (directDirectionRows.Length > 0)
                {
                    var degrees = GetChangedVector(
                        directDirectionRows,
                        row => row.Projection.Document.DirectionDegrees,
                        row => row.Target.DirectionDegrees,
                        binding,
                        "direction");
                    if (degrees is { } value)
                    {
                        direction = value * DegreesToRadians;
                    }
                }

                if (position != null || direction != null)
                {
                    bytes = GcxPlacementWriter.WriteTransform(bytes, site, position, direction).Bytes;
                }
            }
            script.Bytes = bytes;
        }

        foreach (var row in rows.Where(row => row.Projection.Placement.Binding == null))
        {
            if (!PlacementContentEquals(row.Projection.Document, row.Target))
            {
                throw new InvalidDataException(
                    $"Placement '{row.Target.Id}' has no writable GCX source command.");
            }
        }
        return effectEdits;
    }

    private static Dictionary<GeoEffect, PlacementEffectEdit> BuildPlacementEffectEdits(
        IEnumerable<PlacementRow> rows)
    {
        var edits = new Dictionary<GeoEffect, PlacementEffectEdit>(ReferenceEqualityComparer.Instance);
        foreach (var row in rows)
        {
            var placement = row.Projection.Placement;
            var effect = placement.SourceEffect;
            if (effect == null || row.Projection.Document.Source != MapPlacementSources.Effect)
            {
                continue;
            }

            var positionChanged = !VectorsEqual(
                row.Projection.Document.Position,
                row.Target.Position);
            var directionIsDirect = placement.Binding?.Site.Direction?.HasThreeLiteralComponents == true &&
                placement.Binding.Site.IsNested == false;
            var rotationChanged = !directionIsDirect && !VectorsEqual(
                row.Projection.Document.DirectionDegrees,
                row.Target.DirectionDegrees);
            if (!positionChanged && !rotationChanged)
            {
                continue;
            }
            if (!row.Projection.Document.Editable)
            {
                throw new InvalidDataException($"Placement '{row.Target.Id}' is read-only.");
            }

            var edit = edits.GetValueOrDefault(effect) ?? new PlacementEffectEdit();
            if (positionChanged)
            {
                var value = ReadVector(row.Target.Position, 3, $"placement '{row.Target.Id}' position");
                edit.Position = MergeEffectValue(edit.Position, value, row.Target.Id, "position");
            }
            if (rotationChanged)
            {
                var value = ReadVector(
                    row.Target.DirectionDegrees,
                    3,
                    $"placement '{row.Target.Id}' direction") * DegreesToRadians;
                edit.Rotation = MergeEffectValue(edit.Rotation, value, row.Target.Id, "rotation");
            }
            edits[effect] = edit;
        }
        return edits;
    }

    private static byte[] ApplyGeometry(
        GeomFile geometry,
        byte[] originalBytes,
        MapGeomDocument requested,
        IReadOnlyDictionary<GeoEffect, PlacementEffectEdit> placementEffectEdits,
        Endianness endianness)
    {
        ArgumentNullException.ThrowIfNull(requested);
        var output = originalBytes.ToArray();
        ApplyEffects(geometry, output, requested.Effects, placementEffectEdits, endianness);
        ApplyCollisionAttributes(geometry, output, requested.CollisionAttributes, endianness);
        return output;
    }

    private static void ApplyEffects(
        GeomFile geometry,
        byte[] output,
        IReadOnlyList<MapEffectDocument> requested,
        IReadOnlyDictionary<GeoEffect, PlacementEffectEdit> placementEdits,
        Endianness endianness)
    {
        var effects = TreeTraversal.Flatten(geometry.GeoEffects, effect => effect.Children).ToArray();
        var baselineByOffset = ToUniqueDictionary(effects, effect => effect.ChunkOffset, "opaque GEOM effect");
        var requestedByOffset = ToUniqueDictionary(requested, effect => effect.ChunkOffset, "GEOM effect");
        EnsureSameKeys(baselineByOffset.Keys, requestedByOffset.Keys, "GEOM effect");

        foreach (var effect in effects)
        {
            var target = requestedByOffset[effect.ChunkOffset];
            if (target.Index != effect.Index)
            {
                throw new InvalidDataException(
                    $"GEOM effect at chunk offset 0x{effect.ChunkOffset:X} cannot change its structural index in mapdoc v1.");
            }
            var position = ReadVector4(
                target.Position,
                $"GEOM effect at chunk offset 0x{effect.ChunkOffset:X} position");
            var rotation = ReadVector(
                target.RotationDegrees,
                3,
                $"GEOM effect at chunk offset 0x{effect.ChunkOffset:X} rotation") * DegreesToRadians;

            if (placementEdits.TryGetValue(effect, out var placementEdit))
            {
                if (placementEdit.Position is { } placementPosition)
                {
                    var targetChanged = position.X != effect.X || position.Y != effect.Y || position.Z != effect.Z;
                    if (targetChanged && new Vector3(position.X, position.Y, position.Z) != placementPosition)
                    {
                        throw new InvalidDataException(
                            $"GEOM effect at chunk offset 0x{effect.ChunkOffset:X} has conflicting position edits.");
                    }
                    position.X = placementPosition.X;
                    position.Y = placementPosition.Y;
                    position.Z = placementPosition.Z;
                }
                if (placementEdit.Rotation is { } placementRotation)
                {
                    var targetChanged = rotation != new Vector3(
                        effect.RotationX,
                        effect.RotationY,
                        effect.RotationZ);
                    if (targetChanged && rotation != placementRotation)
                    {
                        throw new InvalidDataException(
                            $"GEOM effect at chunk offset 0x{effect.ChunkOffset:X} has conflicting rotation edits.");
                    }
                    rotation = placementRotation;
                }
            }

            if ((effect.Index & 2) == 0 &&
                (position.X != effect.X || position.Y != effect.Y ||
                 position.Z != effect.Z || position.W != effect.W))
            {
                throw new InvalidDataException(
                    $"GEOM effect at chunk offset 0x{effect.ChunkOffset:X} has no writable position payload.");
            }
            if (((effect.Index >> 10) & 0x3FF) == 0 &&
                rotation != new Vector3(effect.RotationX, effect.RotationY, effect.RotationZ))
            {
                throw new InvalidDataException(
                    $"GEOM effect at chunk offset 0x{effect.ChunkOffset:X} has no writable rotation slot.");
            }

            effect.Name = target.Name;
            effect.X = position.X;
            effect.Y = position.Y;
            effect.Z = position.Z;
            effect.W = position.W;
            effect.RotationX = rotation.X;
            effect.RotationY = rotation.Y;
            effect.RotationZ = rotation.Z;
        }

        var props = geometry.GetChunkFromType(GeoChunkType.PROPS);
        if (effects.Length == 0)
        {
            if (requested.Count != 0)
            {
                throw new InvalidDataException("The map document adds GEOM effects, which mapdoc v1 does not support.");
            }
            return;
        }
        if (props == null || props.Size != geometry.GeomChunk6.Length)
        {
            throw new InvalidDataException("The opaque GEOM props chunk is unavailable or inconsistent.");
        }

        var chunk = geometry.GeomChunk6.ToArray();
        GeoEffectChunkPatcher.Patch(chunk, geometry.GeoEffects, endianness);
        foreach (var effect in effects)
        {
            WriteInt32(chunk, effect.ChunkOffset + 8, effect.Name, endianness, "GEOM effect name");
        }
        EnsureRange(output, props.DataOffset, chunk.Length, "GEOM props chunk");
        chunk.CopyTo(output.AsSpan(props.DataOffset, chunk.Length));
    }

    private static void ApplyCollisionAttributes(
        GeomFile geometry,
        byte[] output,
        IReadOnlyList<MapCollisionAttributeDocument> requested,
        Endianness endianness)
    {
        var targets = BuildCollisionTargets(geometry).ToArray();
        var baselineByKey = ToUniqueDictionary(targets, target => target.Key, "opaque collision attribute");
        var requestedByKey = ToUniqueDictionary(
            requested,
            CollisionKey,
            "collision attribute");
        EnsureSameKeys(baselineByKey.Keys, requestedByKey.Keys, "collision attribute");

        foreach (var target in targets)
        {
            var value = requestedByKey[target.Key].Attribute;
            if (target.Width == sizeof(ulong))
            {
                WriteUInt64(output, target.Offset, value, endianness, "collision attribute");
            }
            else
            {
                if (value > ushort.MaxValue)
                {
                    throw new InvalidDataException(
                        $"Polygon collision attribute '{target.Key}' exceeds 16 bits.");
                }
                WriteUInt16(output, target.Offset, (ushort)value, endianness, "polygon collision attribute");
            }
        }
    }

    private static IEnumerable<CollisionTarget> BuildCollisionTargets(GeomFile geometry)
    {
        for (var blockIndex = 0; blockIndex < geometry.GeomBlocks.Count; blockIndex++)
        {
            var block = geometry.GeomBlocks[blockIndex];
            yield return new CollisionTarget(
                $"{MapCollisionAttributeTargets.Block}:{blockIndex}",
                block.Offset + 0x18,
                sizeof(ulong));
            if (!geometry.BlockFaceData.TryGetValue(block, out var primitives))
            {
                continue;
            }
            for (var primitiveIndex = 0; primitiveIndex < primitives.Count; primitiveIndex++)
            {
                var primitive = primitives[primitiveIndex];
                yield return new CollisionTarget(
                    $"{MapCollisionAttributeTargets.Primitive}:{blockIndex}:{primitiveIndex}",
                    primitive.Offset + 0x18,
                    sizeof(ulong));
                if (primitive.Poly == null)
                {
                    continue;
                }
                for (var polygonIndex = 0; polygonIndex < primitive.Poly.Length; polygonIndex++)
                {
                    yield return new CollisionTarget(
                        $"{MapCollisionAttributeTargets.Polygon}:{blockIndex}:{primitiveIndex}:{polygonIndex}",
                        primitive.Offset + 0x20 + polygonIndex * 8 + 6,
                        sizeof(ushort));
                }
            }
        }
    }

    private static string CollisionKey(MapCollisionAttributeDocument value)
    {
        return value.Target switch
        {
            MapCollisionAttributeTargets.Block when value.Primitive == null && value.Polygon == null =>
                $"{value.Target}:{value.Block}",
            MapCollisionAttributeTargets.Primitive when value.Primitive != null && value.Polygon == null =>
                $"{value.Target}:{value.Block}:{value.Primitive.Value}",
            MapCollisionAttributeTargets.Polygon when value.Primitive != null && value.Polygon != null =>
                $"{value.Target}:{value.Block}:{value.Primitive.Value}:{value.Polygon.Value}",
            _ => throw new InvalidDataException("A collision attribute has an inconsistent target hierarchy.")
        };
    }

    private static void ValidatePlacementProjection(
        MapPlacementDocument baseline,
        MapPlacementDocument target)
    {
        if (!string.Equals(baseline.Command, target.Command, StringComparison.Ordinal) ||
            !string.Equals(baseline.Source, target.Source, StringComparison.Ordinal) ||
            baseline.Editable != target.Editable)
        {
            throw new InvalidDataException(
                $"Placement '{baseline.Id}' command, source, and editable fields are derived and cannot be changed.");
        }
        ValidateOptionalVector(target.Position, 3, $"placement '{target.Id}' position");
        ValidateOptionalVector(target.DirectionDegrees, 3, $"placement '{target.Id}' direction");
        if (!baseline.Editable &&
            (!VectorsEqual(baseline.Position, target.Position) ||
             !VectorsEqual(baseline.DirectionDegrees, target.DirectionDegrees)))
        {
            throw new InvalidDataException($"Placement '{target.Id}' is read-only.");
        }
    }

    private static Vector3? GetChangedVector(
        IReadOnlyList<PlacementRow> rows,
        Func<PlacementRow, float[]?> baselineSelector,
        Func<PlacementRow, float[]?> targetSelector,
        GcxPlacementBinding binding,
        string field)
    {
        if (rows.All(row => VectorsEqual(baselineSelector(row), targetSelector(row))))
        {
            return null;
        }
        var baseline = RequireSingleVector(rows.Select(baselineSelector), binding, $"opaque {field}");
        var target = RequireSingleVector(rows.Select(targetSelector), binding, field);
        if (VectorsEqual(baseline, target))
        {
            return null;
        }
        return ReadVector(target, 3, $"placement '{rows[0].Target.Id}' {field}");
    }

    private static void EnsureCommandProjectionCompatibility<T>(
        IEnumerable<PlacementRow> rows,
        Func<PlacementRow, T> selector,
        T requested,
        GcxPlacementBinding binding,
        string field)
    {
        if (rows.Any(row => !EqualityComparer<T>.Default.Equals(selector(row), requested)))
        {
            throw new InvalidDataException(
                $"Placement command '{binding.ScriptName}/{binding.Site.CommandName}@0x{binding.Site.CommandOffset:X}' has conflicting {field} projections.");
        }
    }

    private static float[]? RequireSingleVector(
        IEnumerable<float[]?> values,
        GcxPlacementBinding binding,
        string field)
    {
        float[]? result = null;
        var hasResult = false;
        foreach (var value in values)
        {
            if (!hasResult)
            {
                result = value;
                hasResult = true;
            }
            else if (!VectorsEqual(result, value))
            {
                throw new InvalidDataException(
                    $"Placement command '{binding.ScriptName}/{binding.Site.CommandName}@0x{binding.Site.CommandOffset:X}' has conflicting {field} projections.");
            }
        }
        return result;
    }

    private static T RequireSingleValue<T>(
        IEnumerable<T> values,
        GcxPlacementBinding binding,
        string field)
    {
        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            throw new InvalidDataException("A placement command projection is empty.");
        }
        var result = enumerator.Current;
        while (enumerator.MoveNext())
        {
            if (!EqualityComparer<T>.Default.Equals(result, enumerator.Current))
            {
                throw new InvalidDataException(
                    $"Placement command '{binding.ScriptName}/{binding.Site.CommandName}@0x{binding.Site.CommandOffset:X}' has conflicting {field} projections.");
            }
        }
        return result;
    }

    private static InvalidDataException ReadOnlyPlacement(GcxPlacementBinding binding, string field)
    {
        return new InvalidDataException(
            $"Placement command '{binding.ScriptName}/{binding.Site.CommandName}@0x{binding.Site.CommandOffset:X}' cannot write its {field}.");
    }

    private static Vector3 MergeEffectValue(
        Vector3? current,
        Vector3 requested,
        string placementId,
        string field)
    {
        if (current is { } existing && existing != requested)
        {
            throw new InvalidDataException(
                $"Placements sharing a GEOM effect have conflicting {field} edits near '{placementId}'.");
        }
        return requested;
    }

    private static bool PlacementContentEquals(MapPlacementDocument left, MapPlacementDocument right)
    {
        return left.ModelHash == right.ModelHash &&
            left.EffectHash == right.EffectHash &&
            VectorsEqual(left.Position, right.Position) &&
            VectorsEqual(left.DirectionDegrees, right.DirectionDegrees);
    }

    private static bool VectorsEqual(float[]? left, float[]? right)
    {
        return ReferenceEquals(left, right) ||
            left != null && right != null && left.AsSpan().SequenceEqual(right);
    }

    private static Vector3 ReadVector(float[]? value, int length, string label)
    {
        ValidateOptionalVector(value, length, label);
        if (value == null)
        {
            throw new InvalidDataException($"The {label} cannot be removed in mapdoc v1.");
        }
        return new Vector3(value[0], value[1], value[2]);
    }

    private static Vector4 ReadVector4(float[]? value, string label)
    {
        ValidateOptionalVector(value, 4, label);
        if (value == null)
        {
            throw new InvalidDataException($"The {label} is required.");
        }
        return new Vector4(value[0], value[1], value[2], value[3]);
    }

    private static void ValidateOptionalVector(float[]? value, int length, string label)
    {
        if (value == null)
        {
            return;
        }
        if (value.Length != length || value.Any(component => !float.IsFinite(component)))
        {
            throw new InvalidDataException($"The {label} must contain {length} finite numbers.");
        }
    }

    private static Dictionary<TKey, TValue> ToUniqueDictionary<TValue, TKey>(
        IEnumerable<TValue> values,
        Func<TValue, TKey> keySelector,
        string label)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();
        foreach (var value in values)
        {
            var key = keySelector(value);
            if (!result.TryAdd(key, value))
            {
                throw new InvalidDataException($"The map document contains a duplicate {label} key '{key}'.");
            }
        }
        return result;
    }

    private static void EnsureSameKeys<TKey>(
        IEnumerable<TKey> baseline,
        IEnumerable<TKey> requested,
        string label)
        where TKey : notnull
    {
        var baselineSet = baseline.ToHashSet();
        var requestedSet = requested.ToHashSet();
        if (!baselineSet.SetEquals(requestedSet))
        {
            var missing = baselineSet.Except(requestedSet).FirstOrDefault();
            var added = requestedSet.Except(baselineSet).FirstOrDefault();
            throw new InvalidDataException(
                $"Mapdoc v1 cannot add or remove {label} entries (missing '{missing}', added '{added}').");
        }
    }

    private static void ValidateDocument(MapDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != MapDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported map document schema {document.SchemaVersion}; expected {MapDocument.CurrentSchemaVersion}.");
        }
        if (document.Sources == null || document.Opaque?.Gcx == null || document.Geom == null ||
            document.Placements == null || document.Geom.Effects == null ||
            document.Geom.CollisionAttributes == null ||
            string.IsNullOrWhiteSpace(document.Sources.Gcx) ||
            string.IsNullOrWhiteSpace(document.Sources.Geom))
        {
            throw new InvalidDataException("The map document is missing required sources, projections, or opaque carriers.");
        }
    }

    private static void WriteInt32(
        byte[] bytes,
        int offset,
        int value,
        Endianness endianness,
        string label)
    {
        EnsureRange(bytes, offset, sizeof(int), label);
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset, sizeof(int)), value);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), value);
        }
    }

    private static void WriteUInt64(
        byte[] bytes,
        int offset,
        ulong value,
        Endianness endianness,
        string label)
    {
        EnsureRange(bytes, offset, sizeof(ulong), label);
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(offset, sizeof(ulong)), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, sizeof(ulong)), value);
        }
    }

    private static void WriteUInt16(
        byte[] bytes,
        int offset,
        ushort value,
        Endianness endianness,
        string label)
    {
        EnsureRange(bytes, offset, sizeof(ushort), label);
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset, sizeof(ushort)), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), value);
        }
    }

    private static void EnsureRange(byte[] bytes, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException($"The {label} points outside the opaque GEOM carrier.");
        }
    }

    private sealed record PlacementRow(
        MapPlacementProjection Projection,
        MapPlacementDocument Target);

    private sealed record CommandKey(GcxScript Script, int CommandOffset);

    private sealed class PlacementEffectEdit
    {
        public Vector3? Position { get; set; }
        public Vector3? Rotation { get; set; }
    }

    private sealed record CollisionTarget(string Key, int Offset, int Width);
}
