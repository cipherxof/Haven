using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HavenStudio.Formats.Geo;

public sealed record GeomMeshExportSummary(
    int Blocks,
    int Primitives,
    int Vertices,
    int Triangles);

/// <summary>
/// GEOM collision mesh exchange shared by the locked-position, topology, and
/// new-document Blender workflows.
/// </summary>
public static partial class GeomMeshExchange
{
    private const int ArrayBufferTarget = 34962;
    private const int ElementArrayBufferTarget = 34963;
    private const int FloatComponentType = 5126;
    private const int UnsignedShortComponentType = 5123;
    private const int UnsignedIntComponentType = 5125;
    private const int TriangleMode = 4;

    public static byte[] ExportGltf(GeomFile geometry)
    {
        using var output = new MemoryStream();
        ExportGltf(geometry, output);
        return output.ToArray();
    }

    public static GeomMeshExportSummary ExportGltf(GeomFile geometry, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("glTF export requires a writable stream.", nameof(destination));
        }

        var primitives = DecodePrimitives(geometry);
        var materialIndices = new Dictionary<ulong, int>();
        foreach (var primitive in primitives)
        {
            if (!materialIndices.TryGetValue(primitive.Attribute, out var materialIndex))
            {
                materialIndex = materialIndices.Count;
                materialIndices.Add(primitive.Attribute, materialIndex);
            }
            primitive.MaterialIndex = materialIndex;
        }

        using var buffer = new MemoryStream();
        using (var binary = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            foreach (var primitive in primitives)
            {
                Align4(buffer, binary);
                primitive.PositionByteOffset = checked((int)buffer.Position);
                foreach (var component in primitive.Positions)
                {
                    binary.Write(component);
                }

                Align4(buffer, binary);
                primitive.SourceVertexByteOffset = checked((int)buffer.Position);
                foreach (var sourceVertex in primitive.SourceVertexIndices)
                {
                    binary.Write(checked((ushort)sourceVertex));
                }

                Align4(buffer, binary);
                primitive.IndexByteOffset = checked((int)buffer.Position);
                foreach (var index in primitive.Indices)
                {
                    binary.Write(index);
                }
            }
        }

        var bufferBytes = buffer.ToArray();
        var jsonOptions = new JsonWriterOptions { Indented = true };
        using (var writer = new Utf8JsonWriter(destination, jsonOptions))
        {
            WriteDocument(writer, geometry, primitives, materialIndices, bufferBytes);
        }

        return new GeomMeshExportSummary(
            primitives.Select(primitive => primitive.BlockIndex).Distinct().Count(),
            primitives.Count,
            primitives.Sum(primitive => primitive.Positions.Length / 3),
            primitives.Sum(primitive => primitive.Indices.Length / 3));
    }

    private static List<ExchangePrimitive> DecodePrimitives(GeomFile geometry)
    {
        var result = new List<ExchangePrimitive>();
        var spatialBlocks = geometry.GeomGroupBlocks.Values
            .SelectMany(blocks => blocks)
            .ToHashSet();
        for (var blockIndex = 0; blockIndex < geometry.GeomBlocks.Count; blockIndex++)
        {
            var block = geometry.GeomBlocks[blockIndex];
            if (!spatialBlocks.Contains(block) ||
                !geometry.BlockVertexData.TryGetValue(block, out var vertices) ||
                !geometry.BlockFaceData.TryGetValue(block, out var faces) ||
                !GeomMeshDecoder.TryDecodeBlock(vertices, faces, out var decoded))
            {
                continue;
            }

            var builders = new Dictionary<ulong, PrimitiveBuilder>();
            for (var triangleIndex = 0; triangleIndex < decoded.TriangleCount; triangleIndex++)
            {
                var primitiveIndex = decoded.PrimitiveIndices[triangleIndex];
                if ((uint)primitiveIndex >= (uint)faces.Count)
                {
                    continue;
                }

                var face = faces[primitiveIndex];
                if (!builders.TryGetValue(face.Attribute, out var builder))
                {
                    builder = new PrimitiveBuilder(decoded.Positions, decoded.SourceVertexIndices);
                    builders.Add(face.Attribute, builder);
                }

                var indexOffset = triangleIndex * 3;
                builder.AddTriangle(
                    decoded.Indices[indexOffset],
                    decoded.Indices[indexOffset + 1],
                    decoded.Indices[indexOffset + 2],
                    primitiveIndex);
            }

            foreach (var pair in builders.OrderBy(pair => pair.Key))
            {
                var primitiveIndices = pair.Value.SourcePrimitiveIndices.Order().ToArray();
                var face = faces[primitiveIndices[0]];
                if (pair.Value.Indices.Count == 0)
                {
                    continue;
                }

                result.Add(new ExchangePrimitive(
                    blockIndex,
                    primitiveIndices[0],
                    primitiveIndices,
                    block.Attribute,
                    face.Attribute,
                    face.Flag,
                    face.Name,
                    pair.Value.Positions.ToArray(),
                    pair.Value.Indices.ToArray(),
                    pair.Value.SourceVertexIndices.ToArray()));
            }
        }

        return result;
    }

    private static void WriteDocument(
        Utf8JsonWriter writer,
        GeomFile geometry,
        IReadOnlyList<ExchangePrimitive> primitives,
        IReadOnlyDictionary<ulong, int> materialIndices,
        byte[] buffer)
    {
        var blocks = primitives
            .GroupBy(primitive => primitive.BlockIndex)
            .OrderBy(group => group.Key)
            .Select(group => new ExportBlock(group.Key, group.ToArray()))
            .ToArray();
        var primitiveIndices = primitives
            .Select((primitive, index) => (primitive, index))
            .ToDictionary(pair => pair.primitive, pair => pair.index);

        writer.WriteStartObject();

        writer.WritePropertyName("asset");
        writer.WriteStartObject();
        writer.WriteString("version", "2.0");
        writer.WriteString("generator", "HavenStudio GEOM mesh exchange");
        writer.WriteEndObject();

        writer.WriteNumber("scene", 0);
        writer.WritePropertyName("scenes");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("name", "GEOM collision");
        writer.WritePropertyName("nodes");
        writer.WriteStartArray();
        for (var index = 0; index < blocks.Length; index++)
        {
            writer.WriteNumberValue(index);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WritePropertyName("nodes");
        writer.WriteStartArray();
        for (var index = 0; index < blocks.Length; index++)
        {
            writer.WriteStartObject();
            writer.WriteString("name", blocks[index].StableName);
            writer.WriteNumber("mesh", index);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("meshes");
        writer.WriteStartArray();
        foreach (var block in blocks)
        {
            writer.WriteStartObject();
            writer.WriteString("name", block.StableName);
            writer.WritePropertyName("primitives");
            writer.WriteStartArray();
            foreach (var primitive in block.Primitives)
            {
                var primitiveIndex = primitiveIndices[primitive];
                writer.WriteStartObject();
                writer.WritePropertyName("attributes");
                writer.WriteStartObject();
                writer.WriteNumber("POSITION", primitiveIndex * 3);
                writer.WriteNumber("_GEOM_VERTEX", primitiveIndex * 3 + 1);
                writer.WriteEndObject();
                writer.WriteNumber("indices", primitiveIndex * 3 + 2);
                writer.WriteNumber("material", primitive.MaterialIndex);
                writer.WriteNumber("mode", TriangleMode);
                writer.WritePropertyName("extras");
                writer.WriteStartObject();
                writer.WriteNumber("block", primitive.BlockIndex);
                writer.WriteNumber("primitive", primitive.PrimitiveIndex);
                writer.WritePropertyName("sourcePrimitives");
                writer.WriteStartArray();
                foreach (var sourcePrimitive in primitive.SourcePrimitiveIndices)
                {
                    writer.WriteNumberValue(sourcePrimitive);
                }
                writer.WriteEndArray();
                writer.WriteString("blockAttribute", Hex64(primitive.BlockAttribute));
                writer.WriteString("attribute", Hex64(primitive.Attribute));
                writer.WriteString("flag", $"0x{primitive.Flag:X8}");
                writer.WriteString("nameHash", $"0x{primitive.Name:X8}");
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("materials");
        writer.WriteStartArray();
        foreach (var pair in materialIndices.OrderBy(pair => pair.Value))
        {
            writer.WriteStartObject();
            writer.WriteString("name", $"attr_0x{pair.Key:X4}");
            writer.WriteBoolean("doubleSided", true);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("buffers");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteNumber("byteLength", buffer.Length);
        writer.WriteString("uri", "data:application/octet-stream;base64," + Convert.ToBase64String(buffer));
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WritePropertyName("bufferViews");
        writer.WriteStartArray();
        foreach (var primitive in primitives)
        {
            WriteBufferView(
                writer,
                primitive.PositionByteOffset,
                primitive.Positions.Length * sizeof(float),
                ArrayBufferTarget);
            WriteBufferView(
                writer,
                primitive.SourceVertexByteOffset,
                primitive.SourceVertexIndices.Length * sizeof(ushort),
                ArrayBufferTarget);
            WriteBufferView(
                writer,
                primitive.IndexByteOffset,
                primitive.Indices.Length * sizeof(uint),
                ElementArrayBufferTarget);
        }
        writer.WriteEndArray();

        writer.WritePropertyName("accessors");
        writer.WriteStartArray();
        for (var index = 0; index < primitives.Count; index++)
        {
            var primitive = primitives[index];
            WritePositionAccessor(writer, index * 3, primitive);
            writer.WriteStartObject();
            writer.WriteNumber("bufferView", index * 3 + 1);
            writer.WriteNumber("componentType", UnsignedShortComponentType);
            writer.WriteNumber("count", primitive.SourceVertexIndices.Length);
            writer.WriteString("type", "SCALAR");
            writer.WriteEndObject();
            writer.WriteStartObject();
            writer.WriteNumber("bufferView", index * 3 + 2);
            writer.WriteNumber("componentType", UnsignedIntComponentType);
            writer.WriteNumber("count", primitive.Indices.Length);
            writer.WriteString("type", "SCALAR");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("extras");
        writer.WriteStartObject();
        writer.WriteString("havenStudioFormat", "MGO2_GEOM");
        writer.WritePropertyName("groups");
        writer.WriteStartArray();
        for (var groupIndex = 0; groupIndex < geometry.GeomGroups.Count; groupIndex++)
        {
            var group = geometry.GeomGroups[groupIndex];
            writer.WriteStartObject();
            writer.WriteNumber("index", groupIndex);
            WriteVector3(writer, "base", group.BaseX, group.BaseY, group.BaseZ);
            WriteVector3(writer, "div", group.DivX, group.DivY, group.DivZ);
            writer.WritePropertyName("max");
            writer.WriteStartArray();
            writer.WriteNumberValue(group.MaxX);
            writer.WriteNumberValue(group.MaxY);
            writer.WriteNumberValue(group.MaxZ);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteEndObject();
        writer.Flush();
    }

    private static void WriteBufferView(Utf8JsonWriter writer, int offset, int length, int target)
    {
        writer.WriteStartObject();
        writer.WriteNumber("buffer", 0);
        writer.WriteNumber("byteOffset", offset);
        writer.WriteNumber("byteLength", length);
        writer.WriteNumber("target", target);
        writer.WriteEndObject();
    }

    private static void WritePositionAccessor(
        Utf8JsonWriter writer,
        int bufferView,
        ExchangePrimitive primitive)
    {
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var minZ = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var maxZ = float.NegativeInfinity;
        for (var offset = 0; offset < primitive.Positions.Length; offset += 3)
        {
            var x = primitive.Positions[offset];
            var y = primitive.Positions[offset + 1];
            var z = primitive.Positions[offset + 2];
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
            {
                throw new InvalidDataException($"{primitive.StableName} contains a non-finite position.");
            }
            minX = MathF.Min(minX, x);
            minY = MathF.Min(minY, y);
            minZ = MathF.Min(minZ, z);
            maxX = MathF.Max(maxX, x);
            maxY = MathF.Max(maxY, y);
            maxZ = MathF.Max(maxZ, z);
        }

        writer.WriteStartObject();
        writer.WriteNumber("bufferView", bufferView);
        writer.WriteNumber("componentType", FloatComponentType);
        writer.WriteNumber("count", primitive.Positions.Length / 3);
        writer.WriteString("type", "VEC3");
        WriteVector3(writer, "min", minX, minY, minZ);
        WriteVector3(writer, "max", maxX, maxY, maxZ);
        writer.WriteEndObject();
    }

    private static void WriteVector3(Utf8JsonWriter writer, string property, float x, float y, float z)
    {
        writer.WritePropertyName(property);
        writer.WriteStartArray();
        writer.WriteNumberValue(x);
        writer.WriteNumberValue(y);
        writer.WriteNumberValue(z);
        writer.WriteEndArray();
    }

    private static void Align4(Stream stream, BinaryWriter writer)
    {
        while ((stream.Position & 3) != 0)
        {
            writer.Write((byte)0);
        }
    }

    private static string Hex64(ulong value) => $"0x{value:X16}";

    private sealed class PrimitiveBuilder(float[] sourcePositions, int[] decodedSourceVertexIndices)
    {
        private readonly Dictionary<uint, uint> _vertexMap = new();

        public List<float> Positions { get; } = [];
        public List<uint> Indices { get; } = [];
        public List<int> SourceVertexIndices { get; } = [];
        public HashSet<int> SourcePrimitiveIndices { get; } = [];

        public void AddTriangle(uint a, uint b, uint c, int primitiveIndex)
        {
            Indices.Add(AddVertex(a));
            Indices.Add(AddVertex(b));
            Indices.Add(AddVertex(c));
            SourcePrimitiveIndices.Add(primitiveIndex);
        }

        private uint AddVertex(uint sourceIndex)
        {
            if (_vertexMap.TryGetValue(sourceIndex, out var localIndex))
            {
                return localIndex;
            }

            var sourceOffset = checked((int)sourceIndex * 3);
            if (sourceOffset < 0 || sourceOffset > sourcePositions.Length - 3)
            {
                throw new InvalidDataException($"Decoded GEOM vertex {sourceIndex} is out of range.");
            }

            localIndex = checked((uint)(Positions.Count / 3));
            Positions.Add(sourcePositions[sourceOffset]);
            Positions.Add(sourcePositions[sourceOffset + 1]);
            Positions.Add(sourcePositions[sourceOffset + 2]);
            SourceVertexIndices.Add(decodedSourceVertexIndices[sourceIndex]);
            _vertexMap.Add(sourceIndex, localIndex);
            return localIndex;
        }
    }

    private sealed class ExchangePrimitive(
        int blockIndex,
        int primitiveIndex,
        int[] sourcePrimitiveIndices,
        ulong blockAttribute,
        ulong attribute,
        uint flag,
        uint name,
        float[] positions,
        uint[] indices,
        int[] sourceVertexIndices)
    {
        public int BlockIndex { get; } = blockIndex;
        public int PrimitiveIndex { get; } = primitiveIndex;
        public int[] SourcePrimitiveIndices { get; } = sourcePrimitiveIndices;
        public ulong BlockAttribute { get; } = blockAttribute;
        public ulong Attribute { get; } = attribute;
        public uint Flag { get; } = flag;
        public uint Name { get; } = name;
        public float[] Positions { get; } = positions;
        public uint[] Indices { get; } = indices;
        public int[] SourceVertexIndices { get; } = sourceVertexIndices;
        public string StableName => $"block_{BlockIndex}/attr_0x{Attribute:X16}";
        public int MaterialIndex { get; set; }
        public int PositionByteOffset { get; set; }
        public int SourceVertexByteOffset { get; set; }
        public int IndexByteOffset { get; set; }
    }

    private sealed record ExportBlock(int BlockIndex, IReadOnlyList<ExchangePrimitive> Primitives)
    {
        public string StableName => $"block_{BlockIndex}";
    }
}
