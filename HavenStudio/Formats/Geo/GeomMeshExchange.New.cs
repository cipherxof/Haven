using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using HavenStudio.Extensions;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Geo;

public sealed record GeomMeshNewImportSummary(
    int Blocks,
    int Vertices,
    int Triangles,
    int Materials,
    int RadixCells,
    float CellSize,
    IReadOnlyList<string> Warnings);

public static partial class GeomMeshExchange
{
    private const int NewGeomArenaLimit = 0x3C00;
    private const float DefaultNewGeomCellSize = 100f;

    public static GeomMeshNewImportSummary ImportAsNew(
        string gltfPath,
        Stream destination,
        float cellSize = DefaultNewGeomCellSize,
        Endianness endianness = Endianness.Big)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gltfPath);
        var fullPath = Path.GetFullPath(gltfPath);
        using var input = File.OpenRead(fullPath);
        return ImportAsNew(
            input,
            destination,
            cellSize,
            Path.GetDirectoryName(fullPath),
            endianness);
    }

    public static GeomMeshNewImportSummary ImportAsNew(
        Stream gltf,
        Stream destination,
        float cellSize = DefaultNewGeomCellSize,
        string? baseDirectory = null,
        Endianness endianness = Endianness.Big)
    {
        ArgumentNullException.ThrowIfNull(gltf);
        ArgumentNullException.ThrowIfNull(destination);
        if (!gltf.CanRead)
        {
            throw new ArgumentException("glTF import requires a readable stream.", nameof(gltf));
        }
        if (!destination.CanWrite || !destination.CanSeek)
        {
            throw new ArgumentException(
                "New GEOM output requires a writable, seekable stream.",
                nameof(destination));
        }
        if (!(cellSize > 0) || !float.IsFinite(cellSize))
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), "GEOM cell size must be finite and positive.");
        }

        var warnings = new List<string>();
        using var reader = new GltfReader(gltf, baseDirectory);
        var triangles = ReadAuthoredTriangles(reader, warnings);
        if (triangles.Count == 0)
        {
            throw new InvalidDataException(
                "The glTF has no triangle meshes using attr_0xNNNN collision materials.");
        }

        var bounds = CalculateBounds(triangles);
        var grid = BuildGrid(bounds, cellSize);
        var drafts = BuildBlockDrafts(triangles, grid, warnings);
        using var template = new MemoryStream(BuildEmptyGeomTemplate(endianness), writable: false);
        var geometry = new GeomFile(template, endianness);
        try
        {
            PopulateNewGeometry(geometry, drafts, grid, warnings);
            using var compiled = new MemoryStream();
            geometry.Save(compiled, endianness);
            var compiledBytes = compiled.ToArray();
            using (var validationStream = new MemoryStream(compiledBytes, writable: false))
            {
                var validationGeometry = new GeomFile(validationStream, endianness);
                try
                {
                    var validation = GeoStructureValidator.Validate(validationGeometry);
                    if (!validation.IsValid)
                    {
                        throw new InvalidDataException(
                            "The generated GEOM did not pass structural validation:\n" +
                            string.Join("\n", validation.Issues.Take(10)));
                    }
                }
                finally
                {
                    validationGeometry.CloseStream();
                }
            }

            destination.SetLength(0);
            destination.Position = 0;
            destination.Write(compiledBytes);
            destination.Position = 0;
        }
        finally
        {
            geometry.CloseStream();
        }

        return new GeomMeshNewImportSummary(
            drafts.Count,
            drafts.Sum(draft => draft.Vertices.Count),
            triangles.Count,
            triangles.Select(triangle => triangle.Attribute).Distinct().Count(),
            checked(grid.MaxX * grid.MaxY * grid.MaxZ),
            cellSize,
            warnings.ToArray());
    }

    private static List<AuthoredTriangle> ReadAuthoredTriangles(
        GltfReader reader,
        ICollection<string> warnings)
    {
        var root = reader.Root;
        if (!root.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("meshes", out var meshes) || meshes.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("glTF has no nodes/meshes arrays.");
        }

        var materials = root.TryGetProperty("materials", out var materialArray) &&
            materialArray.ValueKind == JsonValueKind.Array
                ? materialArray
                : default;
        var childNodes = nodes.EnumerateArray()
            .Where(node => node.TryGetProperty("children", out _))
            .SelectMany(node => node.GetProperty("children").EnumerateArray())
            .Select(index => index.GetInt32())
            .ToHashSet();
        var roots = GetSceneRoots(root, nodes, childNodes);
        var result = new List<AuthoredTriangle>();
        var ignoredMaterials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var zeroAreaTriangles = 0;
        var stack = new HashSet<int>();
        foreach (var rootNode in roots)
        {
            VisitNode(rootNode, Transform4.Identity);
        }
        if (zeroAreaTriangles != 0)
        {
            warnings.Add($"Skipped {zeroAreaTriangles} zero-area collision triangles.");
        }
        return result;

        void VisitNode(int nodeIndex, Transform4 parent)
        {
            if ((uint)nodeIndex >= (uint)nodes.GetArrayLength())
            {
                throw new InvalidDataException($"glTF node index {nodeIndex} is out of range.");
            }
            if (!stack.Add(nodeIndex))
            {
                throw new InvalidDataException("glTF node hierarchy contains a cycle.");
            }

            var node = nodes[nodeIndex];
            var transform = Transform4.Multiply(parent, ReadNodeTransform(node));
            if (node.TryGetProperty("mesh", out var meshProperty))
            {
                var meshIndex = meshProperty.GetInt32();
                if ((uint)meshIndex >= (uint)meshes.GetArrayLength())
                {
                    throw new InvalidDataException($"glTF node {nodeIndex} has an invalid mesh index.");
                }
                var mesh = meshes[meshIndex];
                if (!mesh.TryGetProperty("primitives", out var primitives) ||
                    primitives.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException($"glTF mesh {meshIndex} has no primitives array.");
                }

                foreach (var primitive in primitives.EnumerateArray())
                {
                    if (primitive.TryGetProperty("mode", out var mode) && mode.GetInt32() != TriangleMode)
                    {
                        continue;
                    }
                    if (!TryReadCollisionAttribute(primitive, materials, out var attribute, out var materialName))
                    {
                        if (ignoredMaterials.Add(materialName))
                        {
                            warnings.Add(
                                $"Ignored material '{materialName}'; collision materials must be named attr_0xNNNN.");
                        }
                        continue;
                    }
                    if (!primitive.TryGetProperty("attributes", out var attributes) ||
                        !attributes.TryGetProperty("POSITION", out var positionProperty))
                    {
                        throw new InvalidDataException("A collision mesh primitive has no POSITION accessor.");
                    }

                    var localPositions = reader.ReadVector3Accessor(positionProperty.GetInt32());
                    var positions = localPositions.Select(transform.TransformPoint).ToArray();
                    if (positions.Any(position =>
                        !float.IsFinite(position.X) ||
                        !float.IsFinite(position.Y) ||
                        !float.IsFinite(position.Z)))
                    {
                        throw new InvalidDataException("A collision mesh contains a non-finite transformed vertex.");
                    }
                    var indices = primitive.TryGetProperty("indices", out var indexProperty)
                        ? reader.ReadUnsignedAccessor(indexProperty.GetInt32())
                        : Enumerable.Range(0, positions.Length).Select(index => (uint)index).ToArray();
                    if (indices.Length == 0 || indices.Length % 3 != 0)
                    {
                        throw new InvalidDataException("A collision mesh must contain complete triangles.");
                    }
                    if (indices.Any(index => index >= positions.Length))
                    {
                        throw new InvalidDataException("A collision mesh contains an out-of-range triangle index.");
                    }

                    for (var offset = 0; offset < indices.Length; offset += 3)
                    {
                        var a = positions[indices[offset]];
                        var b = positions[indices[offset + 1]];
                        var c = positions[indices[offset + 2]];
                        if (Vector3.Cross(b - a, c - a).LengthSquared <= 1e-10f)
                        {
                            zeroAreaTriangles++;
                            continue;
                        }
                        result.Add(new AuthoredTriangle(a, b, c, attribute));
                    }
                }
            }

            if (node.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    VisitNode(child.GetInt32(), transform);
                }
            }
            stack.Remove(nodeIndex);
        }
    }

    private static int[] GetSceneRoots(
        JsonElement root,
        JsonElement nodes,
        IReadOnlySet<int> childNodes)
    {
        if (root.TryGetProperty("scenes", out var scenes) && scenes.ValueKind == JsonValueKind.Array &&
            scenes.GetArrayLength() != 0)
        {
            var sceneIndex = root.TryGetProperty("scene", out var sceneProperty)
                ? sceneProperty.GetInt32()
                : 0;
            if ((uint)sceneIndex >= (uint)scenes.GetArrayLength())
            {
                throw new InvalidDataException("glTF selects an invalid scene index.");
            }
            var scene = scenes[sceneIndex];
            if (scene.TryGetProperty("nodes", out var sceneNodes))
            {
                return sceneNodes.EnumerateArray().Select(node => node.GetInt32()).ToArray();
            }
        }
        return Enumerable.Range(0, nodes.GetArrayLength())
            .Where(index => !childNodes.Contains(index))
            .ToArray();
    }

    private static bool TryReadCollisionAttribute(
        JsonElement primitive,
        JsonElement materials,
        out ulong attribute,
        out string materialName)
    {
        attribute = 0;
        materialName = "(none)";
        if (materials.ValueKind != JsonValueKind.Array ||
            !primitive.TryGetProperty("material", out var materialProperty))
        {
            return false;
        }
        var materialIndex = materialProperty.GetInt32();
        if ((uint)materialIndex >= (uint)materials.GetArrayLength())
        {
            throw new InvalidDataException("A glTF primitive has an invalid material index.");
        }
        var material = materials[materialIndex];
        materialName = material.TryGetProperty("name", out var nameProperty)
            ? nameProperty.GetString() ?? "(unnamed)"
            : "(unnamed)";
        const string prefix = "attr_0x";
        if (!materialName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var digits = materialName.AsSpan(prefix.Length);
        var suffix = digits.IndexOf('.');
        if (suffix >= 0)
        {
            digits = digits[..suffix];
        }
        return digits.Length is > 0 and <= 16 &&
            ulong.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out attribute);
    }

    private static Bounds3 CalculateBounds(IReadOnlyList<AuthoredTriangle> triangles)
    {
        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        foreach (var triangle in triangles)
        {
            minimum = Vector3.ComponentMin(minimum, triangle.A);
            minimum = Vector3.ComponentMin(minimum, triangle.B);
            minimum = Vector3.ComponentMin(minimum, triangle.C);
            maximum = Vector3.ComponentMax(maximum, triangle.A);
            maximum = Vector3.ComponentMax(maximum, triangle.B);
            maximum = Vector3.ComponentMax(maximum, triangle.C);
        }
        return new Bounds3(minimum, maximum);
    }

    private static NewGeomGrid BuildGrid(Bounds3 bounds, float cellSize)
    {
        var basePosition = new Vector3(
            MathF.Floor(bounds.Minimum.X / cellSize) * cellSize,
            MathF.Floor(bounds.Minimum.Y / cellSize) * cellSize,
            MathF.Floor(bounds.Minimum.Z / cellSize) * cellSize);
        var maxX = checked((int)MathF.Floor((bounds.Maximum.X - basePosition.X) / cellSize) + 1);
        var maxY = checked((int)MathF.Floor((bounds.Maximum.Y - basePosition.Y) / cellSize) + 1);
        var maxZ = checked((int)MathF.Floor((bounds.Maximum.Z - basePosition.Z) / cellSize) + 1);
        var cells = checked((long)maxX * maxY * maxZ);
        if (cells > 16_000_000)
        {
            throw new InvalidDataException(
                $"The authored mesh needs {cells:N0} radix cells at size {cellSize}; choose a larger cell size.");
        }
        return new NewGeomGrid(basePosition, cellSize, maxX, maxY, maxZ);
    }

    private static List<NewBlockDraft> BuildBlockDrafts(
        IReadOnlyList<AuthoredTriangle> triangles,
        NewGeomGrid grid,
        ICollection<string> warnings)
    {
        var result = new List<NewBlockDraft>();
        foreach (var cellGroup in triangles
            .GroupBy(triangle => grid.GetCell((triangle.A + triangle.B + triangle.C) / 3f))
            .OrderBy(group => group.Key.Y)
            .ThenBy(group => group.Key.Z)
            .ThenBy(group => group.Key.X))
        {
            NewBlockDraft? draft = null;
            foreach (var triangle in cellGroup.OrderBy(item => item.Attribute))
            {
                if (draft == null || !draft.TryAdd(triangle))
                {
                    draft = new NewBlockDraft(cellGroup.Key);
                    if (!draft.TryAdd(triangle))
                    {
                        throw new InvalidDataException("A collision triangle cannot fit in an empty GEOM block.");
                    }
                    result.Add(draft);
                }
            }
        }
        if (result.GroupBy(block => block.Cell).Any(group => group.Count() > 1))
        {
            warnings.Add("Some radix cells required multiple GEOM blocks to stay within arena limits.");
        }
        return result;
    }

    private static void PopulateNewGeometry(
        GeomFile geometry,
        IReadOnlyList<NewBlockDraft> drafts,
        NewGeomGrid grid,
        ICollection<string> warnings)
    {
        var group = geometry.GeomGroups.Single();
        group.BaseX = grid.Base.X;
        group.BaseY = grid.Base.Y;
        group.BaseZ = grid.Base.Z;
        group.DivX = grid.CellSize;
        group.DivY = grid.CellSize;
        group.DivZ = grid.CellSize;
        group.DivW = 1f;
        group.MaxX = grid.MaxX;
        group.MaxY = grid.MaxY;
        group.MaxZ = grid.MaxZ;
        group.TypesCount = 1;
        group.RadixSize = 0x10;
        group.MaterialOffset = 0;

        geometry.GeomBlocks.Clear();
        geometry.GeomGroupBlocks[group].Clear();
        geometry.BlockFaceData.Clear();
        geometry.BlockVertexData.Clear();
        var polygonWarnings = new List<string>();
        for (var blockIndex = 0; blockIndex < drafts.Count; blockIndex++)
        {
            var draft = drafts[blockIndex];
            var block = BuildBlock(draft, grid, blockIndex, polygonWarnings, out var faces, out var header);
            geometry.GeomBlocks.Add(block);
            geometry.GeomGroupBlocks[group].Add(block);
            geometry.BlockFaceData.Add(block, faces);
            geometry.BlockVertexData.Add(block, header);
        }
        if (polygonWarnings.Count != 0)
        {
            warnings.Add(
                $"{polygonWarnings.Count} collision material groups contained unpaired triangles; " +
                "those triangles were encoded as degenerate GEOM quads.");
        }

        var radixCount = checked(grid.MaxX * grid.MaxY * grid.MaxZ);
        geometry.GroupRadixData[group] = Enumerable.Range(0, radixCount)
            .Select(_ => new GeoRadix(0x7FFF, [0xFF], new byte[13]))
            .ToList();
        GeoRadixBuilder.Rebuild(geometry, group);
    }

    private static GeoBlock BuildBlock(
        NewBlockDraft draft,
        NewGeomGrid grid,
        int blockIndex,
        ICollection<string> warnings,
        out List<Geom> faces,
        out GeoVertexHeader header)
    {
        var blockBase = grid.GetCellBase(draft.Cell);
        var data = new Vector4[draft.Vertices.Count + 2];
        data[0] = new Vector4(blockBase, 1f);
        data[1] = new Vector4(0f, 1f, 0f, 0f);
        for (var index = 0; index < draft.Vertices.Count; index++)
        {
            data[index + 2] = new Vector4(draft.Vertices[index] - blockBase, 0f);
        }
        header = new GeoVertexHeader(data);

        faces = [];
        foreach (var attributeGroup in draft.Triangles
            .GroupBy(triangle => triangle.Attribute)
            .OrderBy(group => group.Key))
        {
            var indices = attributeGroup
                .SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C })
                .ToArray();
            var polygons = BuildPolygons(
                $"cell_{draft.Cell.X}_{draft.Cell.Y}_{draft.Cell.Z}/attr_0x{attributeGroup.Key:X}",
                indices,
                draft.Vertices,
                [],
                warnings);
            for (var offset = 0; offset < polygons.Count; offset += byte.MaxValue)
            {
                var chunk = polygons.Skip(offset).Take(byte.MaxValue).ToArray();
                var face = new Geom
                {
                    Length = checked((byte)chunk.Length),
                    Type = 0,
                    Field002 = 0,
                    Field003 = 2,
                    Name = 0,
                    Field014 = 0,
                    Attribute = attributeGroup.Key,
                    Poly = chunk,
                    Data = (chunk.Length & 1) != 0 ? new byte[8] : []
                };
                face.Flag = (uint)(face.Length << 24 | face.Type << 16 | face.Field002 << 8 | face.Field003);
                faces.Add(face);
            }
        }

        if (faces.Count > byte.MaxValue)
        {
            throw new InvalidDataException($"Generated block {blockIndex} exceeds the 255-record GEOM limit.");
        }
        var block = new GeoBlock
        {
            Flag = 1,
            GeomCount = checked((byte)faces.Count),
            Free = ushort.MaxValue,
            Head = faces.Count == 0 ? ushort.MaxValue : (ushort)0,
            Pad = 0,
            FaceOffset = checked(blockIndex * 2 + 1),
            VertexOffset = checked(blockIndex * 2 + 2),
            MaterialOffset = 0,
            Attribute = faces.Aggregate(0UL, (value, face) => value | face.Attribute)
        };

        var byteOffset = 0;
        for (var index = 0; index < faces.Count; index++)
        {
            var face = faces[index];
            face.Offset = checked(block.FaceOffset + byteOffset);
            var units = GeoBlockArenaLayout.GetSerializedSize(face) / 0x10;
            face.Next = index == faces.Count - 1 ? 0 : units;
            face.Prev = index == 0
                ? 0
                : -(GeoBlockArenaLayout.GetSerializedSize(faces[index - 1]) / 0x10);
            face.Child = 0;
            byteOffset = checked(byteOffset + GeoBlockArenaLayout.GetSerializedSize(face));
        }
        block.Tail = faces.Count == 0
            ? ushort.MaxValue
            : checked((ushort)((faces[^1].Offset - block.FaceOffset) / 0x10));
        block.Size = checked((ushort)GeoBlockArenaLayout.CalculateHighWater(faces, header));
        if (block.Size > NewGeomArenaLimit)
        {
            throw new InvalidDataException(
                $"Generated block {blockIndex} needs a 0x{block.Size:X} arena, above 0x{NewGeomArenaLimit:X}.");
        }
        return block;
    }

    private static Transform4 ReadNodeTransform(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var matrix))
        {
            var values = matrix.EnumerateArray().Select(value => value.GetSingle()).ToArray();
            if (values.Length != 16 || values.Any(value => !float.IsFinite(value)))
            {
                throw new InvalidDataException("A glTF node has an invalid transform matrix.");
            }
            return new Transform4(values);
        }

        var translation = ReadVector(node, "translation", [0f, 0f, 0f]);
        var rotation = ReadVector(node, "rotation", [0f, 0f, 0f, 1f]);
        var scale = ReadVector(node, "scale", [1f, 1f, 1f]);
        return Transform4.FromTrs(translation, rotation, scale);

        static float[] ReadVector(JsonElement source, string property, float[] fallback)
        {
            if (!source.TryGetProperty(property, out var value))
            {
                return fallback;
            }
            var result = value.EnumerateArray().Select(component => component.GetSingle()).ToArray();
            if (result.Length != fallback.Length || result.Any(component => !float.IsFinite(component)))
            {
                throw new InvalidDataException($"A glTF node has an invalid {property} transform.");
            }
            return result;
        }
    }

    private static byte[] BuildEmptyGeomTemplate(Endianness endianness)
    {
        const int groupOffset = 0x80;
        const int radixOffset = 0xC0;
        const int refsOffset = 0xD0;
        const int tailOffset = 0x140;
        using var output = new MemoryStream(new byte[tailOffset], writable: true);
        using var writer = new EndianBinaryWriter(output, endianness, leaveOpen: true);
        writer.Write(1u);
        writer.Write((uint)tailOffset);
        writer.Write(5);
        writer.Write(0);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);
        WriteChunk(GeoChunkType.GROUPS, refsOffset - groupOffset, groupOffset);
        WriteChunk(GeoChunkType.REFS, tailOffset - refsOffset, refsOffset);
        WriteChunk(GeoChunkType.UNKOWN, 0, tailOffset);
        WriteChunk(GeoChunkType.PROPS, 0, tailOffset);
        WriteChunk(GeoChunkType.ROUTES, 0, tailOffset);
        writer.Write(new byte[8]);
        writer.Write(0u);
        writer.Write(new byte[0x18]);

        output.Position = groupOffset;
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0);
        writer.Write(100f);
        writer.Write(100f);
        writer.Write(100f);
        writer.Write(1f);
        writer.Write(1);
        writer.Write(1);
        writer.Write(1);
        writer.Write(0x10);
        writer.Write(1);
        writer.Write((short)1);
        writer.Write((short)0x10);
        writer.Write(radixOffset);
        writer.Write(refsOffset);

        output.Position = radixOffset;
        writer.Write((short)0x7FFF);
        writer.Write((byte)0xFF);
        writer.Write(new byte[13]);
        writer.Flush();
        return output.ToArray();

        void WriteChunk(GeoChunkType type, int size, int offset)
        {
            writer.Write((ushort)type);
            writer.Write((ushort)0);
            writer.Write(size);
            writer.Write(offset);
        }
    }

    private sealed class NewBlockDraft((int X, int Y, int Z) cell)
    {
        private readonly Dictionary<Vector3, int> _vertexLookup = [];

        public (int X, int Y, int Z) Cell { get; } = cell;
        public List<Vector3> Vertices { get; } = [];
        public List<LocalTriangle> Triangles { get; } = [];

        public bool TryAdd(AuthoredTriangle triangle)
        {
            var newVertices = new[] { triangle.A, triangle.B, triangle.C }
                .Where(position => !_vertexLookup.ContainsKey(position))
                .Distinct()
                .ToArray();
            var nextVertexCount = Vertices.Count + newVertices.Length;
            var counts = Triangles.GroupBy(item => item.Attribute)
                .ToDictionary(group => group.Key, group => group.Count());
            counts[triangle.Attribute] = counts.GetValueOrDefault(triangle.Attribute) + 1;
            var faceBytes = counts.Sum(pair => EstimateFaceBytes(pair.Value));
            var recordCount = counts.Sum(pair => (pair.Value + byte.MaxValue - 1) / byte.MaxValue);
            var highWater = checked(faceBytes + 0x10 + (nextVertexCount + 2) * 0x10);
            if (nextVertexCount > 1024 || recordCount > byte.MaxValue || highWater > NewGeomArenaLimit)
            {
                return false;
            }

            Triangles.Add(new LocalTriangle(
                AddVertex(triangle.A),
                AddVertex(triangle.B),
                AddVertex(triangle.C),
                triangle.Attribute));
            return true;
        }

        private int AddVertex(Vector3 position)
        {
            if (_vertexLookup.TryGetValue(position, out var index))
            {
                return index;
            }
            index = Vertices.Count;
            Vertices.Add(position);
            _vertexLookup.Add(position, index);
            return index;
        }

        private static int EstimateFaceBytes(int triangleCount)
        {
            var total = 0;
            while (triangleCount != 0)
            {
                var count = Math.Min(byte.MaxValue, triangleCount);
                total = checked(total + 0x20 + count * 8 + ((count & 1) != 0 ? 8 : 0));
                triangleCount -= count;
            }
            return total;
        }
    }

    private readonly record struct AuthoredTriangle(Vector3 A, Vector3 B, Vector3 C, ulong Attribute);
    private readonly record struct LocalTriangle(int A, int B, int C, ulong Attribute);
    private readonly record struct Bounds3(Vector3 Minimum, Vector3 Maximum);
    private readonly record struct NewGeomGrid(
        Vector3 Base,
        float CellSize,
        int MaxX,
        int MaxY,
        int MaxZ)
    {
        public (int X, int Y, int Z) GetCell(Vector3 position) => (
            Math.Clamp((int)MathF.Floor((position.X - Base.X) / CellSize), 0, MaxX - 1),
            Math.Clamp((int)MathF.Floor((position.Y - Base.Y) / CellSize), 0, MaxY - 1),
            Math.Clamp((int)MathF.Floor((position.Z - Base.Z) / CellSize), 0, MaxZ - 1));

        public Vector3 GetCellBase((int X, int Y, int Z) cell) => new(
            Base.X + cell.X * CellSize,
            Base.Y + cell.Y * CellSize,
            Base.Z + cell.Z * CellSize);
    }

    private readonly record struct Transform4(float[] Values)
    {
        public static Transform4 Identity { get; } = new(
        [
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        ]);

        public Vector3 TransformPoint(Vector3 point)
        {
            var x = Values[0] * point.X + Values[4] * point.Y + Values[8] * point.Z + Values[12];
            var y = Values[1] * point.X + Values[5] * point.Y + Values[9] * point.Z + Values[13];
            var z = Values[2] * point.X + Values[6] * point.Y + Values[10] * point.Z + Values[14];
            var w = Values[3] * point.X + Values[7] * point.Y + Values[11] * point.Z + Values[15];
            return w == 0f || w == 1f ? new Vector3(x, y, z) : new Vector3(x / w, y / w, z / w);
        }

        public static Transform4 Multiply(Transform4 left, Transform4 right)
        {
            var result = new float[16];
            for (var column = 0; column < 4; column++)
            {
                for (var row = 0; row < 4; row++)
                {
                    for (var inner = 0; inner < 4; inner++)
                    {
                        result[column * 4 + row] +=
                            left.Values[inner * 4 + row] * right.Values[column * 4 + inner];
                    }
                }
            }
            return new Transform4(result);
        }

        public static Transform4 FromTrs(float[] translation, float[] rotation, float[] scale)
        {
            var x = rotation[0];
            var y = rotation[1];
            var z = rotation[2];
            var w = rotation[3];
            var length = MathF.Sqrt(x * x + y * y + z * z + w * w);
            if (!(length > 0))
            {
                throw new InvalidDataException("A glTF node has a zero-length rotation quaternion.");
            }
            x /= length;
            y /= length;
            z /= length;
            w /= length;
            return new Transform4(
            [
                (1 - 2 * (y * y + z * z)) * scale[0],
                (2 * (x * y + z * w)) * scale[0],
                (2 * (x * z - y * w)) * scale[0],
                0,
                (2 * (x * y - z * w)) * scale[1],
                (1 - 2 * (x * x + z * z)) * scale[1],
                (2 * (y * z + x * w)) * scale[1],
                0,
                (2 * (x * z + y * w)) * scale[2],
                (2 * (y * z - x * w)) * scale[2],
                (1 - 2 * (x * x + y * y)) * scale[2],
                0,
                translation[0], translation[1], translation[2], 1
            ]);
        }
    }
}
