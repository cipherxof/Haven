using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HavenStudio.Extensions;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;

namespace HavenStudio.Tests.Formats;

public sealed class GeomMeshExchangeTests
{
    [Fact]
    public void Shared_decoder_and_gltf_export_work_after_the_source_stream_is_closed()
    {
        var source = new MemoryStream(BuildGeomFixture(), writable: false);
        var geometry = new GeomFile(source, Endianness.Big);
        geometry.CloseStream();

        var models = GeomSceneBuilder.BuildBlockModels(
            geometry,
            out _,
            out _,
            out var trianglePrimitives,
            out var trianglePolygons);
        var model = Assert.Single(models);
        Assert.Equal(4, model.VertexCount);
        Assert.Equal(6, model.IndexCount);
        Assert.Equal(1.0f, model.Alpha);
        Assert.False(model.BlendEnabled);
        Assert.True(model.WriteDepth);
        Assert.True(model.ForceOpaqueAlpha);
        Assert.Equal([0, 0], trianglePrimitives[model]);
        Assert.Equal([0, 0], trianglePolygons[model]);

        var spatialBlock = Assert.Single(geometry.GeomBlocks);
        var referenceOnlyBlock = new GeoBlock();
        geometry.GeomBlocks.Add(referenceOnlyBlock);
        geometry.BlockVertexData.Add(referenceOnlyBlock, geometry.BlockVertexData[spatialBlock]);
        geometry.BlockFaceData.Add(referenceOnlyBlock, geometry.BlockFaceData[spatialBlock]);

        using var output = new MemoryStream();
        var summary = GeomMeshExchange.ExportGltf(geometry, output);
        Assert.Equal(new GeomMeshExportSummary(1, 1, 4, 2), summary);

        using var document = JsonDocument.Parse(output.ToArray());
        var root = document.RootElement;
        Assert.Equal("2.0", root.GetProperty("asset").GetProperty("version").GetString());
        Assert.Equal("block_0", root.GetProperty("nodes")[0].GetProperty("name").GetString());
        Assert.Equal("block_0", root.GetProperty("meshes")[0].GetProperty("name").GetString());
        Assert.Equal("attr_0x0040", root.GetProperty("materials")[0].GetProperty("name").GetString());

        var primitive = root.GetProperty("meshes")[0].GetProperty("primitives")[0];
        var extras = primitive.GetProperty("extras");
        Assert.Equal("0x0000000000000040", extras.GetProperty("attribute").GetString());
        Assert.Equal("0x0000000000000004", extras.GetProperty("blockAttribute").GetString());
        Assert.Equal("0x01000002", extras.GetProperty("flag").GetString());
        Assert.Equal(1, primitive.GetProperty("attributes").GetProperty("_GEOM_VERTEX").GetInt32());
        Assert.Equal(4, root.GetProperty("accessors")[0].GetProperty("count").GetInt32());
        Assert.Equal(4, root.GetProperty("accessors")[1].GetProperty("count").GetInt32());
        Assert.Equal(6, root.GetProperty("accessors")[2].GetProperty("count").GetInt32());

        var uri = root.GetProperty("buffers")[0].GetProperty("uri").GetString()!;
        var payload = Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..]);
        Assert.Equal(10f, ReadLittleEndianSingle(payload, 0));
        Assert.Equal(20f, ReadLittleEndianSingle(payload, 4));
        Assert.Equal(30f, ReadLittleEndianSingle(payload, 8));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(48, 2)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(50, 2)));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(52, 2)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(56, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(60, 4)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(64, 4)));
    }

    [Fact]
    public void Unmodified_gltf_import_is_byte_identical_and_position_edits_reencode_offsets()
    {
        var canonical = Canonicalize(BuildGeomFixture());
        using var source = new MemoryStream(canonical, writable: false);
        var geometry = new GeomFile(source, Endianness.Big);
        var gltf = GeomMeshExchange.ExportGltf(geometry);

        using (var unmodified = new MemoryStream(gltf, writable: false))
        {
            var noOp = GeomMeshExchange.ImportPositions(geometry, unmodified);
            Assert.Equal(0, noOp.UpdatedVertices);
            Assert.Empty(noOp.Warnings);
        }

        using var noOpOutput = new MemoryStream();
        geometry.Save(noOpOutput, Endianness.Big);
        Assert.Equal(canonical, noOpOutput.ToArray());

        var editedGltf = RewriteFirstPositionX(gltf, 150f);
        using (var edited = new MemoryStream(editedGltf, writable: false))
        {
            var result = GeomMeshExchange.ImportPositions(geometry, edited);
            Assert.Equal(1, result.UpdatedVertices);
            Assert.Contains(result.Warnings, warning => warning.Contains("crossed radix cell", StringComparison.Ordinal));
        }

        var block = Assert.Single(geometry.GeomBlocks);
        var header = geometry.BlockVertexData[block];
        Assert.Equal(140f, header.Data[header.VertexStart].X);
        Assert.Equal(0f, header.Data[header.VertexStart].Y);
        Assert.Equal(0f, header.Data[header.VertexStart].Z);
        geometry.CloseStream();
    }

    [Fact]
    public void Topology_import_rebuilds_vertices_polygons_allocator_and_radix()
    {
        var canonical = Canonicalize(BuildGeomFixture());
        using var source = new MemoryStream(canonical, writable: false);
        var geometry = new GeomFile(source, Endianness.Big);
        var gltf = GeomMeshExchange.ExportGltf(geometry);

        using (var unmodified = new MemoryStream(gltf, writable: false))
        {
            var noOp = GeomMeshExchange.ImportTopology(geometry, unmodified);
            Assert.Equal(0, noOp.UpdatedBlocks);
            Assert.Empty(noOp.Warnings);
        }
        using (var noOpOutput = new MemoryStream())
        {
            geometry.Save(noOpOutput, Endianness.Big);
            Assert.Equal(canonical, noOpOutput.ToArray());
        }

        var editedGltf = RewriteFixtureTopology(gltf);
        using (var edited = new MemoryStream(editedGltf, writable: false))
        {
            var result = GeomMeshExchange.ImportTopology(geometry, edited);
            Assert.Equal(1, result.UpdatedBlocks);
            Assert.Equal(5, result.Vertices);
            Assert.Equal(3, result.Triangles);
            Assert.Contains(result.Warnings, warning => warning.Contains("degenerate", StringComparison.Ordinal));
        }

        var block = Assert.Single(geometry.GeomBlocks);
        var face = Assert.Single(geometry.BlockFaceData[block]);
        Assert.Equal(3, face.Length);
        Assert.Equal(3, face.Poly!.Length);
        Assert.Equal(0xC0, block.Size);
        Assert.Equal(7, geometry.BlockVertexData[block].Length);

        using var rebuiltBytes = new MemoryStream();
        geometry.Save(rebuiltBytes, Endianness.Big);
        using var reloadedSource = new MemoryStream(rebuiltBytes.ToArray(), writable: false);
        var reloaded = new GeomFile(reloadedSource, Endianness.Big);
        var validation = GeoStructureValidator.Validate(reloaded);
        Assert.True(validation.IsValid, string.Join("\n", validation.Issues));
        reloaded.CloseStream();
    }

    [Theory]
    [InlineData(Endianness.Big)]
    [InlineData(Endianness.Little)]
    public void Blender_authored_gltf_import_builds_a_standalone_valid_geom(Endianness endianness)
    {
        using var source = new MemoryStream(BuildGeomFixture(), writable: false);
        var original = new GeomFile(source, Endianness.Big);
        var gltf = GeomMeshExchange.ExportGltf(original);
        original.CloseStream();

        var authoredRoot = JsonNode.Parse(gltf)!.AsObject();
        authoredRoot["nodes"]!.AsArray()[0]!.AsObject()["translation"] =
            new JsonArray(5f, 0f, 0f);
        var authored = Encoding.UTF8.GetBytes(authoredRoot.ToJsonString());
        using var authoredStream = new MemoryStream(authored, writable: false);
        using var output = new MemoryStream();
        var summary = GeomMeshExchange.ImportAsNew(
            authoredStream,
            output,
            cellSize: 50f,
            endianness: endianness);

        Assert.Equal(1, summary.Blocks);
        Assert.Equal(4, summary.Vertices);
        Assert.Equal(2, summary.Triangles);
        Assert.Equal(1, summary.Materials);
        Assert.Equal(50f, summary.CellSize);

        using var rebuiltStream = new MemoryStream(output.ToArray(), writable: false);
        var rebuilt = new GeomFile(rebuiltStream, endianness);
        Assert.Empty(rebuilt.GeomRefs);
        Assert.Empty(rebuilt.GeoEffects);
        var validation = GeoStructureValidator.Validate(rebuilt);
        Assert.True(validation.IsValid, string.Join("\n", validation.Issues));
        var block = Assert.Single(rebuilt.GeomBlocks);
        var face = Assert.Single(rebuilt.BlockFaceData[block]);
        Assert.Equal(GeoCollisionAttributes.Bullet, face.Attribute);
        Assert.True(GeomMeshDecoder.TryDecodeBlock(
            rebuilt.BlockVertexData[block],
            rebuilt.BlockFaceData[block],
            out var decoded));
        Assert.Equal(2, decoded.TriangleCount);
        Assert.Equal(15f, Enumerable.Range(0, decoded.VertexCount)
            .Min(index => decoded.Positions[index * 3]));

        var reexported = GeomMeshExchange.ExportGltf(rebuilt);
        rebuilt.CloseStream();
        using var reexportedStream = new MemoryStream(reexported, writable: false);
        using var secondOutput = new MemoryStream();
        GeomMeshExchange.ImportAsNew(
            reexportedStream,
            secondOutput,
            cellSize: 50f,
            endianness: endianness);
        Assert.Equal(output.ToArray(), secondOutput.ToArray());
    }

    [Fact]
    public void Export_batches_same_material_records_into_one_block_object_and_material_group()
    {
        using var source = new MemoryStream(BuildGeomFixture(), writable: false);
        var geometry = new GeomFile(source, Endianness.Big);
        var block = Assert.Single(geometry.GeomBlocks);
        var faces = geometry.BlockFaceData[block];
        var original = Assert.Single(faces);
        faces.Add(new Geom
        {
            Length = original.Length,
            Type = original.Type,
            Field002 = original.Field002,
            Field003 = original.Field003,
            Name = original.Name,
            Field014 = original.Field014,
            Attribute = original.Attribute,
            Poly = original.Poly!.ToArray(),
            Data = original.Data.ToArray()
        });
        GeoBlockArenaBuilder.RebuildSequential(block, faces, geometry.BlockVertexData[block]);

        using var canonicalOutput = new MemoryStream();
        geometry.Save(canonicalOutput, Endianness.Big);
        geometry.CloseStream();
        using var canonicalSource = new MemoryStream(canonicalOutput.ToArray(), writable: false);
        var canonical = new GeomFile(canonicalSource, Endianness.Big);
        var gltf = GeomMeshExchange.ExportGltf(canonical);

        using var document = JsonDocument.Parse(gltf);
        Assert.Single(document.RootElement.GetProperty("nodes").EnumerateArray());
        Assert.Single(document.RootElement.GetProperty("meshes").EnumerateArray());
        var primitive = Assert.Single(document.RootElement.GetProperty("meshes")[0]
            .GetProperty("primitives").EnumerateArray());
        var indexAccessor = primitive.GetProperty("indices").GetInt32();
        Assert.Equal(12, document.RootElement.GetProperty("accessors")[indexAccessor]
            .GetProperty("count").GetInt32());

        using var imported = new MemoryStream(gltf, writable: false);
        var summary = GeomMeshExchange.ImportTopology(canonical, imported);
        Assert.Equal(1, summary.Primitives);
        Assert.Equal(0, summary.UpdatedBlocks);
        canonical.CloseStream();
    }

    private static byte[] BuildGeomFixture()
    {
        const int groupOffset = 0x80;
        const int radixOffset = 0xC0;
        const int blockOffset = 0xD0;
        const int primitiveOffset = 0xF0;
        const int vertexOffset = 0x120;
        const int refsOffset = 0x190;
        const int fileSize = 0x200;
        using var output = new MemoryStream(new byte[fileSize], writable: true);
        using var writer = new EndianBinaryWriter(output, Endianness.Big, leaveOpen: true);

        writer.Write(1u);
        writer.Write((uint)fileSize);
        writer.Write(5);
        writer.Write(0);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);
        WriteChunk(writer, GeoChunkType.GROUPS, refsOffset - groupOffset, groupOffset);
        WriteChunk(writer, GeoChunkType.REFS, 0x70, refsOffset);
        WriteChunk(writer, GeoChunkType.UNKOWN, 0, fileSize);
        WriteChunk(writer, GeoChunkType.PROPS, 0, fileSize);
        WriteChunk(writer, GeoChunkType.ROUTES, 0, fileSize);
        writer.Write(new byte[8]);
        writer.Write(0x01020304u);
        writer.Write(new byte[0x18]);
        Assert.Equal(groupOffset, output.Position);

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
        writer.Write(0x30);
        writer.Write(1);
        writer.Write((short)1);
        writer.Write((short)0x10);
        writer.Write(radixOffset);
        writer.Write(blockOffset);

        output.Position = radixOffset;
        writer.Write((short)0);
        writer.Write((byte)0);
        writer.Write(new byte[0x0D]);

        output.Position = blockOffset;
        writer.Write((byte)0x01);
        writer.Write((byte)1);
        writer.Write((ushort)0xA0);
        writer.Write((ushort)0);
        writer.Write(ushort.MaxValue);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(vertexOffset);
        writer.Write(primitiveOffset);
        writer.Write(0);
        writer.Write(GeoCollisionAttributes.Floor);

        output.Position = primitiveOffset;
        writer.Write((byte)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)2);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0x10203040u);
        writer.Write(0);
        writer.Write(GeoCollisionAttributes.Bullet);
        writer.Write(new byte[] { 0, 1, 2, 3, 0, 0 });
        writer.Write((ushort)GeoCollisionAttributes.Bullet);
        writer.Write(new byte[8]);

        output.Position = vertexOffset;
        writer.Write(6);
        writer.Write(2);
        writer.Write(1);
        writer.Write(0);
        WriteVector(writer, 10, 20, 30, 1);
        WriteVector(writer, 0, 1, 0, 0);
        WriteVector(writer, 0, 0, 0, 0);
        WriteVector(writer, 1, 0, 0, 0);
        WriteVector(writer, 1, 0, 1, 0);
        WriteVector(writer, 0, 0, 1, 0);

        output.Position = refsOffset;
        writer.Write(new byte[0x70]);
        writer.Flush();
        return output.ToArray();
    }

    private static void WriteChunk(
        EndianBinaryWriter writer,
        GeoChunkType type,
        int size,
        int offset)
    {
        writer.Write((ushort)type);
        writer.Write((ushort)0);
        writer.Write(size);
        writer.Write(offset);
    }

    private static void WriteVector(
        EndianBinaryWriter writer,
        float x,
        float y,
        float z,
        float w)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
        writer.Write(w);
    }

    private static float ReadLittleEndianSingle(byte[] data, int offset) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4)));

    private static byte[] Canonicalize(byte[] sourceBytes)
    {
        using var source = new MemoryStream(sourceBytes, writable: false);
        var geometry = new GeomFile(source, Endianness.Big);
        using var output = new MemoryStream();
        geometry.Save(output, Endianness.Big);
        geometry.CloseStream();
        return output.ToArray();
    }

    private static byte[] RewriteFirstPositionX(byte[] gltf, float x)
    {
        var root = JsonNode.Parse(gltf)!.AsObject();
        var buffer = root["buffers"]!.AsArray()[0]!.AsObject();
        var uri = buffer["uri"]!.GetValue<string>();
        var comma = uri.IndexOf(',');
        var payload = Convert.FromBase64String(uri[(comma + 1)..]);
        BinaryPrimitives.WriteInt32LittleEndian(payload, BitConverter.SingleToInt32Bits(x));
        buffer["uri"] = uri[..(comma + 1)] + Convert.ToBase64String(payload);
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static byte[] RewriteFixtureTopology(byte[] gltf)
    {
        var root = JsonNode.Parse(gltf)!.AsObject();
        var buffer = root["buffers"]!.AsArray()[0]!.AsObject();
        var uri = buffer["uri"]!.GetValue<string>();
        var comma = uri.IndexOf(',');
        var oldPayload = Convert.FromBase64String(uri[(comma + 1)..]);

        var positions = new byte[5 * 12];
        oldPayload.AsSpan(0, 4 * 12).CopyTo(positions);
        BinaryPrimitives.WriteInt32LittleEndian(positions.AsSpan(48), BitConverter.SingleToInt32Bits(10.5f));
        BinaryPrimitives.WriteInt32LittleEndian(positions.AsSpan(52), BitConverter.SingleToInt32Bits(20f));
        BinaryPrimitives.WriteInt32LittleEndian(positions.AsSpan(56), BitConverter.SingleToInt32Bits(30.5f));
        uint[] indices = [0, 1, 4, 1, 4, 2, 2, 3, 4];
        var payload = new byte[positions.Length + indices.Length * sizeof(uint)];
        positions.CopyTo(payload, 0);
        for (var index = 0; index < indices.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                payload.AsSpan(positions.Length + index * sizeof(uint)),
                indices[index]);
        }

        var primitive = root["meshes"]!.AsArray()[0]!["primitives"]!.AsArray()[0]!.AsObject();
        primitive["attributes"]!.AsObject().Remove("_GEOM_VERTEX");
        primitive["indices"] = 1;

        var views = root["bufferViews"]!.AsArray();
        views.Clear();
        views.Add(new JsonObject
        {
            ["buffer"] = 0,
            ["byteOffset"] = 0,
            ["byteLength"] = positions.Length,
            ["target"] = 34962
        });
        views.Add(new JsonObject
        {
            ["buffer"] = 0,
            ["byteOffset"] = positions.Length,
            ["byteLength"] = indices.Length * sizeof(uint),
            ["target"] = 34963
        });

        var accessors = root["accessors"]!.AsArray();
        var positionAccessor = accessors[0]!.AsObject();
        positionAccessor["bufferView"] = 0;
        positionAccessor["count"] = 5;
        accessors.Clear();
        accessors.Add(positionAccessor);
        accessors.Add(new JsonObject
        {
            ["bufferView"] = 1,
            ["componentType"] = 5125,
            ["count"] = indices.Length,
            ["type"] = "SCALAR"
        });

        buffer["byteLength"] = payload.Length;
        buffer["uri"] = uri[..(comma + 1)] + Convert.ToBase64String(payload);
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }
}
