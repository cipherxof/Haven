using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Geo;

public sealed record GeomMeshImportSummary(
    int Primitives,
    int UpdatedVertices,
    IReadOnlyList<string> Warnings);

public sealed record GeomMeshTopologyImportSummary(
    int Primitives,
    int UpdatedBlocks,
    int Vertices,
    int Triangles,
    IReadOnlyList<string> Warnings);

public static partial class GeomMeshExchange
{
    public static GeomMeshTopologyImportSummary ImportTopology(GeomFile geometry, string gltfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gltfPath);
        var fullPath = Path.GetFullPath(gltfPath);
        using var input = File.OpenRead(fullPath);
        return ImportTopology(geometry, input, Path.GetDirectoryName(fullPath));
    }

    public static GeomMeshTopologyImportSummary ImportTopology(
        GeomFile geometry,
        Stream gltf,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(gltf);
        if (!gltf.CanRead)
        {
            throw new ArgumentException("glTF import requires a readable stream.", nameof(gltf));
        }

        var expected = DecodePrimitives(geometry);
        using var reader = new GltfReader(gltf, baseDirectory);
        var importedNodes = ReadCollisionNodes(reader.Root, expected);
        var imported = new List<TopologyPrimitive>(expected.Count);
        foreach (var source in expected)
        {
            var primitive = importedNodes[source.StableName];
            var attributes = primitive.GetProperty("attributes");
            if (!attributes.TryGetProperty("POSITION", out var positionProperty))
            {
                throw new InvalidDataException($"{source.StableName} has no POSITION accessor.");
            }

            var positions = reader.ReadVector3Accessor(positionProperty.GetInt32());
            if (positions.Length == 0 || positions.Any(position =>
                    !float.IsFinite(position.X) ||
                    !float.IsFinite(position.Y) ||
                    !float.IsFinite(position.Z)))
            {
                throw new InvalidDataException($"{source.StableName} has no finite mesh vertices.");
            }
            if (!primitive.TryGetProperty("indices", out var indexProperty))
            {
                throw new InvalidDataException($"{source.StableName} is no longer indexed.");
            }

            var indices = reader.ReadUnsignedAccessor(indexProperty.GetInt32());
            if (indices.Length == 0 || indices.Length % 3 != 0)
            {
                throw new InvalidDataException(
                    $"{source.StableName} must contain a non-empty triangle index list.");
            }
            if (indices.Any(index => index >= positions.Length))
            {
                throw new InvalidDataException($"{source.StableName} contains an out-of-range triangle index.");
            }
            if (primitive.TryGetProperty("mode", out var mode) && mode.GetInt32() != TriangleMode)
            {
                throw new InvalidDataException($"{source.StableName} is no longer a triangle mesh.");
            }

            imported.Add(new TopologyPrimitive(source, positions, indices));
        }

        var changed = imported.Where(item => !TopologyMatches(item)).ToArray();
        if (changed.Length == 0)
        {
            return new GeomMeshTopologyImportSummary(
                expected.Count,
                0,
                expected.Sum(item => item.Positions.Length / 3),
                expected.Sum(item => item.Indices.Length / 3),
                []);
        }

        var warnings = new List<string>();
        var changedGroups = new HashSet<GeoGroup>();
        var updatedBlocks = 0;
        foreach (var blockGroup in changed.GroupBy(item => item.Source.BlockIndex))
        {
            var blockIndex = blockGroup.Key;
            if ((uint)blockIndex >= (uint)geometry.GeomBlocks.Count)
            {
                throw new InvalidDataException($"Imported block index {blockIndex} is out of range.");
            }
            var block = geometry.GeomBlocks[blockIndex];
            if (!geometry.BlockFaceData.TryGetValue(block, out var faces) ||
                !geometry.BlockVertexData.TryGetValue(block, out var vertexHeader))
            {
                throw new InvalidDataException($"block_{blockIndex} has no writable polygon/vertex arena.");
            }

            var allBlockPrimitives = imported
                .Where(item => item.Source.BlockIndex == blockIndex)
                .OrderBy(item => item.Source.Attribute)
                .ToArray();
            var exportedPrimitiveIndices = allBlockPrimitives
                .SelectMany(item => item.Source.SourcePrimitiveIndices)
                .ToHashSet();
            var missingPolygon = faces
                .Select((face, index) => (face, index))
                .FirstOrDefault(pair =>
                    pair.face.GetPrimType() == Geom.Primitive.GEO_POLY &&
                    pair.face.Poly is { Length: > 0 } &&
                    !exportedPrimitiveIndices.Contains(pair.index));
            if (missingPolygon.face != null)
            {
                throw new InvalidDataException(
                    $"block_{blockIndex}/prim_{missingPolygon.index} could not be decoded for safe topology replacement.");
            }

            RebuildBlockMesh(
                geometry,
                blockIndex,
                block,
                faces,
                vertexHeader,
                allBlockPrimitives,
                warnings,
                changedGroups);
            updatedBlocks++;
        }

        foreach (var group in changedGroups)
        {
            var boundsRemap = GeoRadixBuilder.EnsureBoundsContainBlockBases(geometry, group);
            GeoRadixBuilder.Rebuild(geometry, group, boundsRemap);
        }

        return new GeomMeshTopologyImportSummary(
            expected.Count,
            updatedBlocks,
            imported.Sum(item => item.Positions.Length),
            imported.Sum(item => item.Indices.Length / 3),
            warnings.ToArray());
    }

    public static GeomMeshImportSummary ImportPositions(GeomFile geometry, string gltfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gltfPath);
        var fullPath = Path.GetFullPath(gltfPath);
        using var input = File.OpenRead(fullPath);
        return ImportPositions(geometry, input, Path.GetDirectoryName(fullPath));
    }

    public static GeomMeshImportSummary ImportPositions(
        GeomFile geometry,
        Stream gltf,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(gltf);
        if (!gltf.CanRead)
        {
            throw new ArgumentException("glTF import requires a readable stream.", nameof(gltf));
        }

        var expected = DecodePrimitives(geometry);
        using var reader = new GltfReader(gltf, baseDirectory);
        var importedNodes = ReadCollisionNodes(reader.Root, expected);
        var pending = new Dictionary<VertexKey, Vector3>();
        var warnings = new List<string>();

        foreach (var source in expected)
        {
            var gltfPrimitive = importedNodes[source.StableName];
            var attributes = gltfPrimitive.GetProperty("attributes");
            if (!attributes.TryGetProperty("POSITION", out var positionProperty))
            {
                throw new InvalidDataException($"{source.StableName} has no POSITION accessor.");
            }

            var positions = reader.ReadVector3Accessor(positionProperty.GetInt32());
            if (positions.Length != source.Positions.Length / 3)
            {
                throw new InvalidDataException(
                    $"{source.StableName} changed vertex count from {source.Positions.Length / 3} to {positions.Length}.");
            }

            int[] sourceVertices;
            if (attributes.TryGetProperty("_GEOM_VERTEX", out var sourceVertexProperty))
            {
                sourceVertices = reader.ReadUnsignedAccessor(sourceVertexProperty.GetInt32())
                    .Select(value => checked((int)value))
                    .ToArray();
                if (!sourceVertices.Order().SequenceEqual(source.SourceVertexIndices.Order()))
                {
                    throw new InvalidDataException(
                        $"{source.StableName} changed its source-vertex mapping; topology edits require Phase E3.");
                }
            }
            else
            {
                sourceVertices = source.SourceVertexIndices;
                warnings.Add(
                    $"{source.StableName} has no _GEOM_VERTEX attribute; vertex order was assumed unchanged.");
            }

            if (sourceVertices.Length != positions.Length)
            {
                throw new InvalidDataException(
                    $"{source.StableName} source-vertex mapping count does not match POSITION count.");
            }

            if (!gltfPrimitive.TryGetProperty("indices", out var indexProperty))
            {
                throw new InvalidDataException($"{source.StableName} is no longer indexed.");
            }
            var indices = reader.ReadUnsignedAccessor(indexProperty.GetInt32());
            if (indices.Length != source.Indices.Length)
            {
                throw new InvalidDataException(
                    $"{source.StableName} changed index count from {source.Indices.Length} to {indices.Length}.");
            }
            if (indices.Any(index => index >= positions.Length))
            {
                throw new InvalidDataException($"{source.StableName} contains an out-of-range triangle index.");
            }
            if (gltfPrimitive.TryGetProperty("mode", out var mode) && mode.GetInt32() != TriangleMode)
            {
                throw new InvalidDataException($"{source.StableName} is no longer a triangle mesh.");
            }

            for (var localVertex = 0; localVertex < positions.Length; localVertex++)
            {
                var sourceVertex = sourceVertices[localVertex];
                var key = new VertexKey(source.BlockIndex, sourceVertex);
                var position = positions[localVertex];
                if (pending.TryGetValue(key, out var prior) && !NearlyEqual(prior, position))
                {
                    throw new InvalidDataException(
                        $"block_{source.BlockIndex} source vertex {sourceVertex} was split into conflicting positions; " +
                        "topology edits require Phase E3.");
                }
                pending[key] = position;
            }
        }

        var changed = new List<(VertexKey Key, Vector3 Old, Vector3 New)>();
        foreach (var pair in pending)
        {
            if ((uint)pair.Key.Block >= (uint)geometry.GeomBlocks.Count)
            {
                throw new InvalidDataException($"Imported block index {pair.Key.Block} is out of range.");
            }

            var block = geometry.GeomBlocks[pair.Key.Block];
            if (!geometry.BlockVertexData.TryGetValue(block, out var header) ||
                header.PositionStart < 0 || header.PositionStart >= header.Data.Length ||
                pair.Key.SourceVertex < 0 ||
                header.VertexStart + pair.Key.SourceVertex >= header.Data.Length)
            {
                throw new InvalidDataException(
                    $"block_{pair.Key.Block} source vertex {pair.Key.SourceVertex} is outside its GEOM vertex table.");
            }

            var basePosition = header.Data[header.PositionStart];
            var stored = header.Data[header.VertexStart + pair.Key.SourceVertex];
            var oldPosition = new Vector3(
                stored.X + basePosition.X,
                stored.Y + basePosition.Y,
                stored.Z + basePosition.Z);
            if (oldPosition != pair.Value)
            {
                changed.Add((pair.Key, oldPosition, pair.Value));
            }
        }

        foreach (var edit in changed)
        {
            AddRadixWarnings(geometry, edit.Key, edit.Old, edit.New, warnings);
        }

        // Apply only after every node/accessor/topology check has succeeded.
        foreach (var edit in changed)
        {
            var block = geometry.GeomBlocks[edit.Key.Block];
            var header = geometry.BlockVertexData[block];
            var basePosition = header.Data[header.PositionStart];
            var dataIndex = header.VertexStart + edit.Key.SourceVertex;
            var stored = header.Data[dataIndex];
            stored.X = edit.New.X - basePosition.X;
            stored.Y = edit.New.Y - basePosition.Y;
            stored.Z = edit.New.Z - basePosition.Z;
            header.Data[dataIndex] = stored;
        }

        return new GeomMeshImportSummary(expected.Count, changed.Count, warnings.ToArray());
    }

    private static bool TopologyMatches(TopologyPrimitive imported)
    {
        var source = imported.Source;
        if (!source.Indices.AsSpan().SequenceEqual(imported.Indices))
        {
            return false;
        }
        if (source.Positions.Length != imported.Positions.Length * 3)
        {
            return false;
        }
        for (var index = 0; index < imported.Positions.Length; index++)
        {
            var offset = index * 3;
            if (source.Positions[offset] != imported.Positions[index].X ||
                source.Positions[offset + 1] != imported.Positions[index].Y ||
                source.Positions[offset + 2] != imported.Positions[index].Z)
            {
                return false;
            }
        }
        return true;
    }

    private static void RebuildBlockMesh(
        GeomFile geometry,
        int blockIndex,
        GeoBlock block,
        List<Geom> faces,
        GeoVertexHeader vertexHeader,
        IReadOnlyList<TopologyPrimitive> primitives,
        ICollection<string> warnings,
        ISet<GeoGroup> changedGroups)
    {
        if (vertexHeader.VertexStart < 0 ||
            vertexHeader.VertexStart > vertexHeader.Data.Length ||
            vertexHeader.PositionStart < 0 ||
            vertexHeader.PositionStart >= vertexHeader.VertexStart)
        {
            throw new InvalidDataException($"block_{blockIndex} has an invalid vertex-table layout.");
        }

        var worldVertices = new List<Vector3>();
        var vertexLookup = new Dictionary<Vector3, int>();
        int AddVertex(Vector3 position)
        {
            if (vertexLookup.TryGetValue(position, out var existing))
            {
                return existing;
            }
            if (worldVertices.Count >= 1024)
            {
                throw new InvalidDataException(
                    $"block_{blockIndex} needs more than the GEOM limit of 1024 vertices.");
            }
            var index = worldVertices.Count;
            worldVertices.Add(position);
            vertexLookup.Add(position, index);
            return index;
        }

        if (faces.Any(face => face.GetPrimType() != Geom.Primitive.GEO_POLY))
        {
            throw new InvalidDataException(
                $"block_{blockIndex} mixes polygon and non-polygon arena records and cannot be rebuilt safely.");
        }

        var rebuiltFaces = new List<Geom>();
        foreach (var imported in primitives)
        {
            var templates = imported.Source.SourcePrimitiveIndices.Select(index => faces[index]).ToArray();
            var sourceFace = templates[0];
            var localToBlock = imported.Positions.Select(AddVertex).ToArray();
            var blockIndices = imported.Indices
                .Select(index => localToBlock[checked((int)index)])
                .ToArray();
            var originalPolygons = templates.SelectMany(face => face.Poly ?? []).ToArray();
            var rebuilt = BuildPolygons(
                imported.Source.StableName,
                blockIndices,
                worldVertices,
                originalPolygons,
                warnings);
            foreach (var polygon in rebuilt)
            {
                polygon.Attribute = unchecked((ushort)imported.Source.Attribute);
            }
            for (var polygonOffset = 0; polygonOffset < rebuilt.Count; polygonOffset += byte.MaxValue)
            {
                var polygons = rebuilt.Skip(polygonOffset).Take(byte.MaxValue).ToArray();
                var replacement = polygonOffset == 0 ? sourceFace : CloneFace(sourceFace);
                replacement.Attribute = imported.Source.Attribute;
                replacement.Poly = polygons;
                replacement.Length = checked((byte)polygons.Length);
                replacement.Data = (polygons.Length & 1) != 0
                    ? replacement.Data.Length == 8 ? replacement.Data : new byte[8]
                    : [];
                replacement.Flag = (uint)(replacement.Length << 24 |
                    replacement.Type << 16 |
                    replacement.Field002 << 8 |
                    replacement.Field003);
                rebuiltFaces.Add(replacement);
            }
        }

        faces.Clear();
        faces.AddRange(rebuiltFaces);

        var oldBase = vertexHeader.Data[vertexHeader.PositionStart];
        var oldW = new Dictionary<Vector3, float>();
        for (var sourceIndex = 0; vertexHeader.VertexStart + sourceIndex < vertexHeader.Data.Length; sourceIndex++)
        {
            var stored = vertexHeader.Data[vertexHeader.VertexStart + sourceIndex];
            oldW.TryAdd(
                new Vector3(stored.X + oldBase.X, stored.Y + oldBase.Y, stored.Z + oldBase.Z),
                stored.W);
        }

        var prefix = vertexHeader.Data[..vertexHeader.VertexStart];
        var rebuiltData = new Vector4[checked(prefix.Length + worldVertices.Count)];
        prefix.CopyTo(rebuiltData, 0);
        for (var index = 0; index < worldVertices.Count; index++)
        {
            var position = worldVertices[index];
            rebuiltData[vertexHeader.VertexStart + index] = new Vector4(
                position.X - oldBase.X,
                position.Y - oldBase.Y,
                position.Z - oldBase.Z,
                oldW.GetValueOrDefault(position));
        }
        vertexHeader.Data = rebuiltData;
        vertexHeader.Length = rebuiltData.Length;
        GeoBlockArenaBuilder.RebuildSequential(block, faces, vertexHeader);

        var owner = geometry.GeomGroups.FirstOrDefault(group => geometry.GeomGroupBlocks[group].Contains(block));
        if (owner == null)
        {
            return;
        }
        changedGroups.Add(owner);
        TryMoveBlockBaseToEditedCell(owner, vertexHeader, worldVertices, blockIndex, warnings);

        static Geom CloneFace(Geom source) => new()
        {
            Type = source.Type,
            Field002 = source.Field002,
            Field003 = source.Field003,
            Name = source.Name,
            Field014 = source.Field014,
            Attribute = source.Attribute,
            Data = []
        };
    }

    private static List<GeoPrimPoly> BuildPolygons(
        string name,
        IReadOnlyList<int> indices,
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<GeoPrimPoly> originals,
        ICollection<string> warnings)
    {
        var polygons = new List<GeoPrimPoly>();
        var triangle = 0;
        var degenerateCount = 0;
        while (triangle * 3 < indices.Count)
        {
            var offset = triangle * 3;
            var first = new[] { indices[offset], indices[offset + 1], indices[offset + 2] };
            int[] quad;
            if ((triangle + 1) * 3 < indices.Count &&
                TryPairTriangles(
                    first,
                    [indices[offset + 3], indices[offset + 4], indices[offset + 5]],
                    positions,
                    out quad))
            {
                triangle += 2;
            }
            else
            {
                quad = [first[0], first[1], first[2], first[2]];
                triangle++;
                degenerateCount++;
            }

            var data = new byte[6];
            GeomUtils.EncodeFaceIndices(quad[0], quad[1], quad[2], quad[3], data);
            var original = originals.Count == 0
                ? null
                : originals[Math.Min(polygons.Count, originals.Count - 1)];
            if (original is { Data.Length: >= 6 })
            {
                data[5] = original.Data[5];
            }
            polygons.Add(new GeoPrimPoly(data, original?.Attribute ?? 0));
        }

        if (degenerateCount != 0)
        {
            warnings.Add(
                $"{name}: encoded {degenerateCount} unpaired triangles as degenerate GEOM quads.");
        }
        return polygons;
    }

    private static bool TryPairTriangles(
        IReadOnlyList<int> first,
        IReadOnlyList<int> second,
        IReadOnlyList<Vector3> positions,
        out int[] quad)
    {
        quad = [];
        if (first.Concat(second).Distinct().Count() != 4)
        {
            return false;
        }

        var firstNormal = Vector3.Cross(
            positions[first[1]] - positions[first[0]],
            positions[first[2]] - positions[first[0]]);
        var secondNormal = Vector3.Cross(
            positions[second[1]] - positions[second[0]],
            positions[second[2]] - positions[second[0]]);
        if (firstNormal.LengthSquared <= 1e-10f || secondNormal.LengthSquared <= 1e-10f)
        {
            return false;
        }
        firstNormal = Vector3.Normalize(firstNormal);
        secondNormal = Vector3.Normalize(secondNormal);
        if (Vector3.Dot(firstNormal, secondNormal) < 0.999f)
        {
            return false;
        }

        for (var firstRotation = 0; firstRotation < 3; firstRotation++)
        {
            var a = first[firstRotation];
            var b = first[(firstRotation + 1) % 3];
            var c = first[(firstRotation + 2) % 3];
            for (var secondRotation = 0; secondRotation < 3; secondRotation++)
            {
                if (second[secondRotation] != a || second[(secondRotation + 1) % 3] != c)
                {
                    continue;
                }
                var d = second[(secondRotation + 2) % 3];
                var planeDistance = MathF.Abs(Vector3.Dot(firstNormal, positions[d] - positions[a]));
                var scale = MathF.Max(1f, (positions[c] - positions[a]).Length);
                if (planeDistance > scale * 0.001f)
                {
                    return false;
                }
                quad = [a, b, c, d];
                return true;
            }
        }
        return false;
    }

    private static void TryMoveBlockBaseToEditedCell(
        GeoGroup group,
        GeoVertexHeader header,
        IReadOnlyList<Vector3> positions,
        int blockIndex,
        ICollection<string> warnings)
    {
        var cells = positions.Select(position => (
            X: (int)MathF.Floor((position.X - group.BaseX) / group.DivX),
            Y: (int)MathF.Floor((position.Y - group.BaseY) / group.DivY),
            Z: (int)MathF.Floor((position.Z - group.BaseZ) / group.DivZ)))
            .Distinct()
            .ToArray();
        var oldBase = header.Data[header.PositionStart];
        var oldCell = (
            X: (int)MathF.Round((oldBase.X - group.BaseX) / group.DivX),
            Y: (int)MathF.Round((oldBase.Y - group.BaseY) / group.DivY),
            Z: (int)MathF.Round((oldBase.Z - group.BaseZ) / group.DivZ));
        if (cells.Length != 1)
        {
            if (cells.Any(cell => cell != oldCell))
            {
                warnings.Add(
                    $"block_{blockIndex} spans {cells.Length} radix cells; its existing cell was retained.");
            }
            return;
        }

        var target = cells[0];
        if (target == oldCell)
        {
            return;
        }
        var newBase = new Vector3(
            group.BaseX + target.X * group.DivX,
            group.BaseY + target.Y * group.DivY,
            group.BaseZ + target.Z * group.DivZ);
        var delta = new Vector3(oldBase.X, oldBase.Y, oldBase.Z) - newBase;
        for (var index = header.VertexStart; index < header.Data.Length; index++)
        {
            var stored = header.Data[index];
            stored.X += delta.X;
            stored.Y += delta.Y;
            stored.Z += delta.Z;
            header.Data[index] = stored;
        }
        oldBase.X = newBase.X;
        oldBase.Y = newBase.Y;
        oldBase.Z = newBase.Z;
        header.Data[header.PositionStart] = oldBase;
    }

    private static Dictionary<string, JsonElement> ReadCollisionNodes(
        JsonElement root,
        IReadOnlyList<ExchangePrimitive> expected)
    {
        if (!root.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("meshes", out var meshes) || meshes.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("glTF has no nodes/meshes arrays.");
        }

        var expectedNames = expected.Select(item => item.StableName).ToHashSet(StringComparer.Ordinal);
        var expectedByBlock = expected
            .GroupBy(item => item.BlockIndex)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Attribute).ToArray());
        var materials = root.TryGetProperty("materials", out var materialArray) &&
            materialArray.ValueKind == JsonValueKind.Array
                ? materialArray
                : default;
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var node in nodes.EnumerateArray())
        {
            if (!node.TryGetProperty("name", out var nameProperty) ||
                nameProperty.GetString() is not { } name)
            {
                continue;
            }
            if (!expectedNames.Contains(name))
            {
                if (TryParseBlockName(name, out var blockIndex) && expectedByBlock.TryGetValue(blockIndex, out var block))
                {
                    ValidateIdentityTransform(node, name);
                    var primitives = GetPrimitives(node, meshes, name);
                    var matched = 0;
                    foreach (var primitive in primitives.EnumerateArray())
                    {
                        if (TryReadCollisionAttribute(
                            primitive,
                            materials,
                            out var attribute,
                            out var materialName))
                        {
                            var stableName = $"block_{blockIndex}/attr_0x{attribute:X16}";
                            if (!expectedNames.Contains(stableName) || !result.TryAdd(stableName, primitive))
                            {
                                throw new InvalidDataException(
                                    $"Collision node '{name}' has unexpected or duplicate material '{materialName}'.");
                            }
                            matched++;
                        }
                    }

                    if (matched == 0 && primitives.GetArrayLength() == block.Length)
                    {
                        var imported = primitives.EnumerateArray().ToArray();
                        for (var index = 0; index < block.Length; index++)
                        {
                            result.Add(block[index].StableName, imported[index]);
                        }
                        matched = block.Length;
                    }
                    if (matched != block.Length)
                    {
                        throw new InvalidDataException(
                            $"Collision node '{name}' contains {primitives.GetArrayLength()} material groups, " +
                            $"but GEOM block {blockIndex} requires {block.Length} primitive groups.");
                    }
                    continue;
                }
                if (name.StartsWith("block_", StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"glTF contains unexpected collision node '{name}'.");
                }
                continue;
            }
            if (!result.TryAdd(name, GetOnlyPrimitive(node, meshes, name)))
            {
                throw new InvalidDataException($"glTF contains duplicate collision node '{name}'.");
            }
        }

        var missing = expectedNames.Where(name => !result.ContainsKey(name)).Order().ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidDataException(
                $"glTF is missing {missing.Length} collision nodes, beginning with '{missing[0]}'.");
        }
        return result;

        static bool TryParseBlockName(string name, out int blockIndex)
        {
            blockIndex = -1;
            const string prefix = "block_";
            return name.StartsWith(prefix, StringComparison.Ordinal) &&
                name.IndexOf('/', prefix.Length) < 0 &&
                int.TryParse(name.AsSpan(prefix.Length), out blockIndex) &&
                blockIndex >= 0;
        }

        static JsonElement GetPrimitives(JsonElement node, JsonElement meshes, string name)
        {
            if (!node.TryGetProperty("mesh", out var meshProperty))
            {
                throw new InvalidDataException($"Collision node '{name}' has no mesh.");
            }
            var meshIndex = meshProperty.GetInt32();
            if ((uint)meshIndex >= (uint)meshes.GetArrayLength())
            {
                throw new InvalidDataException($"Collision node '{name}' has an invalid mesh index.");
            }
            var mesh = meshes[meshIndex];
            if (!mesh.TryGetProperty("primitives", out var primitives) ||
                primitives.ValueKind != JsonValueKind.Array ||
                primitives.GetArrayLength() == 0)
            {
                throw new InvalidDataException($"Collision node '{name}' has no mesh primitives.");
            }
            return primitives;
        }

        static JsonElement GetOnlyPrimitive(JsonElement node, JsonElement meshes, string name)
        {
            ValidateIdentityTransform(node, name);
            var primitives = GetPrimitives(node, meshes, name);
            if (primitives.GetArrayLength() != 1)
            {
                throw new InvalidDataException($"Collision node '{name}' must contain exactly one mesh primitive.");
            }
            return primitives[0];
        }

        static void ValidateIdentityTransform(JsonElement node, string name)
        {
            if (node.TryGetProperty("matrix", out var matrix))
            {
                float[] identity =
                [
                    1, 0, 0, 0,
                    0, 1, 0, 0,
                    0, 0, 1, 0,
                    0, 0, 0, 1
                ];
                if (matrix.ValueKind != JsonValueKind.Array ||
                    !matrix.EnumerateArray().Select(value => value.GetSingle()).SequenceEqual(identity))
                {
                    throw new InvalidDataException(
                        $"Collision node '{name}' has an object transform. Apply transforms before position-only import.");
                }
            }
            ValidateVector("translation", [0f, 0f, 0f]);
            ValidateVector("rotation", [0f, 0f, 0f, 1f]);
            ValidateVector("scale", [1f, 1f, 1f]);

            void ValidateVector(string property, float[] identity)
            {
                if (node.TryGetProperty(property, out var value) &&
                    (value.ValueKind != JsonValueKind.Array ||
                     !value.EnumerateArray().Select(component => component.GetSingle()).SequenceEqual(identity)))
                {
                    throw new InvalidDataException(
                        $"Collision node '{name}' has an object transform. Apply transforms before position-only import.");
                }
            }
        }
    }

    private static void AddRadixWarnings(
        GeomFile geometry,
        VertexKey key,
        Vector3 oldPosition,
        Vector3 newPosition,
        ICollection<string> warnings)
    {
        var block = geometry.GeomBlocks[key.Block];
        foreach (var pair in geometry.GeomGroupBlocks)
        {
            if (!pair.Value.Contains(block))
            {
                continue;
            }

            var oldCell = GetCell(pair.Key, oldPosition);
            var newCell = GetCell(pair.Key, newPosition);
            if (oldCell != newCell)
            {
                warnings.Add(
                    $"block_{key.Block} source vertex {key.SourceVertex} crossed radix cell " +
                    $"{FormatCell(oldCell)} -> {FormatCell(newCell)}; chunk 0 was not rebuilt.");
            }
        }
    }

    private static GridCell GetCell(GeoGroup group, Vector3 position)
    {
        if (group.DivX <= 0 || group.DivY <= 0 || group.DivZ <= 0)
        {
            return new GridCell(0, 0, 0, false);
        }
        var x = (int)MathF.Floor((position.X - group.BaseX) / group.DivX);
        var y = (int)MathF.Floor((position.Y - group.BaseY) / group.DivY);
        var z = (int)MathF.Floor((position.Z - group.BaseZ) / group.DivZ);
        return new GridCell(
            x,
            y,
            z,
            x >= 0 && x < group.MaxX &&
            y >= 0 && y < group.MaxY &&
            z >= 0 && z < group.MaxZ);
    }

    private static string FormatCell(GridCell cell) =>
        cell.Inside ? $"({cell.X},{cell.Y},{cell.Z})" : $"outside({cell.X},{cell.Y},{cell.Z})";

    private static bool NearlyEqual(Vector3 left, Vector3 right) =>
        (left - right).LengthSquared <= 0.0001f;

    private readonly record struct VertexKey(int Block, int SourceVertex);
    private readonly record struct GridCell(int X, int Y, int Z, bool Inside);
    private sealed record TopologyPrimitive(
        ExchangePrimitive Source,
        Vector3[] Positions,
        uint[] Indices);

    private sealed class GltfReader : IDisposable
    {
        private readonly JsonDocument _document;
        private readonly byte[][] _buffers;

        public GltfReader(Stream input, string? baseDirectory)
        {
            try
            {
                _document = JsonDocument.Parse(input);
                Root = _document.RootElement;
                _buffers = LoadBuffers(Root, baseDirectory);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The selected file is not valid glTF JSON.", exception);
            }
        }

        public JsonElement Root { get; }

        public Vector3[] ReadVector3Accessor(int accessorIndex)
        {
            var source = GetAccessor(accessorIndex, "VEC3", FloatComponentType, 12);
            var result = new Vector3[source.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var item = source.GetItem(index);
                result[index] = new Vector3(
                    ReadSingle(item),
                    ReadSingle(item[4..]),
                    ReadSingle(item[8..]));
            }
            return result;
        }

        public uint[] ReadUnsignedAccessor(int accessorIndex)
        {
            var accessor = GetAccessorElement(accessorIndex);
            var componentType = accessor.GetProperty("componentType").GetInt32();
            var componentSize = componentType switch
            {
                5121 => 1,
                5123 => 2,
                UnsignedIntComponentType => 4,
                _ => throw new InvalidDataException(
                    $"Accessor {accessorIndex} is not an unsigned integer accessor.")
            };
            var source = GetAccessor(accessorIndex, "SCALAR", componentType, componentSize);
            var result = new uint[source.Count];
            for (var index = 0; index < result.Length; index++)
            {
                var item = source.GetItem(index);
                result[index] = componentType switch
                {
                    5121 => item[0],
                    5123 => BinaryPrimitives.ReadUInt16LittleEndian(item),
                    _ => BinaryPrimitives.ReadUInt32LittleEndian(item)
                };
            }
            return result;
        }

        public void Dispose() => _document.Dispose();

        private AccessorSource GetAccessor(
            int accessorIndex,
            string expectedType,
            int expectedComponentType,
            int elementSize)
        {
            var accessor = GetAccessorElement(accessorIndex);
            if (accessor.TryGetProperty("sparse", out _))
            {
                throw new InvalidDataException($"Sparse accessor {accessorIndex} is not supported.");
            }
            if (accessor.GetProperty("type").GetString() != expectedType ||
                accessor.GetProperty("componentType").GetInt32() != expectedComponentType)
            {
                throw new InvalidDataException(
                    $"Accessor {accessorIndex} is not {expectedType}/{expectedComponentType}.");
            }
            if (!accessor.TryGetProperty("bufferView", out var viewProperty))
            {
                throw new InvalidDataException($"Accessor {accessorIndex} has no buffer view.");
            }

            var views = Root.GetProperty("bufferViews");
            var viewIndex = viewProperty.GetInt32();
            if ((uint)viewIndex >= (uint)views.GetArrayLength())
            {
                throw new InvalidDataException($"Accessor {accessorIndex} has an invalid buffer view.");
            }
            var view = views[viewIndex];
            var bufferIndex = view.GetProperty("buffer").GetInt32();
            if ((uint)bufferIndex >= (uint)_buffers.Length)
            {
                throw new InvalidDataException($"Buffer view {viewIndex} has an invalid buffer index.");
            }

            var count = accessor.GetProperty("count").GetInt32();
            if (count < 0)
            {
                throw new InvalidDataException($"Accessor {accessorIndex} has a negative count.");
            }
            var viewOffset = GetInt32(view, "byteOffset");
            var viewLength = view.GetProperty("byteLength").GetInt32();
            var accessorOffset = GetInt32(accessor, "byteOffset");
            var stride = view.TryGetProperty("byteStride", out var strideProperty)
                ? strideProperty.GetInt32()
                : elementSize;
            if (stride < elementSize)
            {
                throw new InvalidDataException($"Accessor {accessorIndex} has an invalid byte stride.");
            }

            var start = checked(viewOffset + accessorOffset);
            var end = count == 0
                ? start
                : checked(start + (count - 1) * stride + elementSize);
            var viewEnd = checked(viewOffset + viewLength);
            if (start < viewOffset || end > viewEnd || end > _buffers[bufferIndex].Length)
            {
                throw new InvalidDataException($"Accessor {accessorIndex} points outside its buffer view.");
            }
            return new AccessorSource(_buffers[bufferIndex], start, stride, elementSize, count);
        }

        private JsonElement GetAccessorElement(int accessorIndex)
        {
            if (!Root.TryGetProperty("accessors", out var accessors) ||
                accessors.ValueKind != JsonValueKind.Array ||
                (uint)accessorIndex >= (uint)accessors.GetArrayLength())
            {
                throw new InvalidDataException($"glTF accessor {accessorIndex} is out of range.");
            }
            return accessors[accessorIndex];
        }

        private static byte[][] LoadBuffers(JsonElement root, string? baseDirectory)
        {
            if (!root.TryGetProperty("buffers", out var buffers) || buffers.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("glTF has no buffers array.");
            }

            return buffers.EnumerateArray().Select((buffer, index) =>
            {
                if (!buffer.TryGetProperty("uri", out var uriProperty) ||
                    uriProperty.GetString() is not { } uri)
                {
                    throw new InvalidDataException(
                        $"Buffer {index} has no URI; binary GLB import is not supported in Phase E2.");
                }
                if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var comma = uri.IndexOf(',');
                    if (comma < 0 || !uri[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Buffer {index} uses an unsupported data URI.");
                    }
                    try
                    {
                        return Convert.FromBase64String(uri[(comma + 1)..]);
                    }
                    catch (FormatException exception)
                    {
                        throw new InvalidDataException($"Buffer {index} has invalid base64 data.", exception);
                    }
                }
                if (string.IsNullOrWhiteSpace(baseDirectory))
                {
                    throw new InvalidDataException(
                        $"Buffer {index} is external, but no glTF base directory was supplied.");
                }

                var rootDirectory = Path.GetFullPath(baseDirectory);
                var path = Path.GetFullPath(Path.Combine(
                    rootDirectory,
                    Uri.UnescapeDataString(uri).Replace('/', Path.DirectorySeparatorChar)));
                var relative = Path.GetRelativePath(rootDirectory, path);
                if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Buffer {index} escapes the glTF directory.");
                }
                return File.ReadAllBytes(path);
            }).ToArray();
        }

        private static int GetInt32(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) ? value.GetInt32() : 0;

        private static float ReadSingle(ReadOnlySpan<byte> bytes) =>
            BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes));

        private readonly record struct AccessorSource(
            byte[] Buffer,
            int Start,
            int Stride,
            int ElementSize,
            int Count)
        {
            public ReadOnlySpan<byte> GetItem(int index) =>
                Buffer.AsSpan(checked(Start + index * Stride), ElementSize);
        }
    }
}
