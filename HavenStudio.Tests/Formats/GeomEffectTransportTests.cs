using System.Buffers.Binary;
using HavenStudio.Extensions;
using HavenStudio.Formats.Geo;

namespace HavenStudio.Tests.Formats;

public sealed class GeomEffectTransportTests
{
    [Fact]
    public void TransportEffectsFrom_deep_copies_the_source_effects_and_chunk()
    {
        var source = LoadGeom();
        source.GeomChunk6 = BuildSingleEffectChunk(name: 0x11223344);
        source.GeoEffects.Clear();
        source.GeoEffects.Add(new GeoEffect { Name = 0x11223344, Index = 0, ChunkOffset = 0 });
        var target = LoadGeom();

        var transported = target.TransportEffectsFrom(source);

        Assert.Equal(1, transported);
        var effect = Assert.Single(target.GeoEffects);
        Assert.Equal(0x11223344, effect.Name);
        Assert.NotSame(source.GeoEffects[0], effect);
        Assert.Equal(source.GeomChunk6, target.GeomChunk6);
        Assert.NotSame(source.GeomChunk6, target.GeomChunk6);

        var reloaded = SaveAndReload(target);
        Assert.Equal(0x11223344, Assert.Single(reloaded.GeoEffects).Name);
    }

    [Fact]
    public void TransportEffectsFrom_adds_a_props_chunk_when_the_target_has_none()
    {
        var source = LoadGeom();
        source.GeomChunk6 = BuildSingleEffectChunk(name: 0x55);
        source.GeoEffects.Clear();
        source.GeoEffects.Add(new GeoEffect { Name = 0x55, Index = 0, ChunkOffset = 0 });

        var target = LoadGeom();
        target.Header.Chunks.RemoveAll(chunk => chunk.Type == (ushort)GeoChunkType.PROPS);
        Assert.Null(target.GetChunkFromType(GeoChunkType.PROPS));

        target.TransportEffectsFrom(source);

        var propsChunk = target.GetChunkFromType(GeoChunkType.PROPS);
        Assert.NotNull(propsChunk);
        var routesIndex = target.Header.Chunks.FindIndex(chunk => chunk.Type == (ushort)GeoChunkType.ROUTES);
        var propsIndex = target.Header.Chunks.IndexOf(propsChunk!);
        Assert.True(propsIndex < routesIndex);

        var reloaded = SaveAndReload(target);
        Assert.Equal(0x55, Assert.Single(reloaded.GeoEffects).Name);
    }

    [Fact]
    public void TransportEffectsFrom_replaces_existing_target_effects()
    {
        var source = LoadGeom();
        source.GeomChunk6 = BuildSingleEffectChunk(name: 0xAA);
        source.GeoEffects.Clear();
        source.GeoEffects.Add(new GeoEffect { Name = 0xAA, Index = 0, ChunkOffset = 0 });

        var target = LoadGeom();
        target.GeomChunk6 = BuildSingleEffectChunk(name: 0xBB);
        target.GeoEffects.Clear();
        target.GeoEffects.Add(new GeoEffect { Name = 0xBB, Index = 0, ChunkOffset = 0 });

        target.TransportEffectsFrom(source);

        Assert.Equal(0xAA, Assert.Single(target.GeoEffects).Name);
    }

    private static GeomFile LoadGeom() =>
        new(new MemoryStream(BuildGeomFixture(), writable: false), Endianness.Big);

    private static GeomFile SaveAndReload(GeomFile geometry)
    {
        using var output = new MemoryStream();
        geometry.Save(output, Endianness.Big);
        geometry.CloseStream();
        return new GeomFile(new MemoryStream(output.ToArray(), writable: false), Endianness.Big);
    }

    private static byte[] BuildSingleEffectChunk(int name)
    {
        var chunk = new byte[0x10];
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(0), 0); // next
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(4), 0); // child
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(8), name);
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(12), 0); // index (no position/rotation slots)
        return chunk;
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

    private static void WriteChunk(EndianBinaryWriter writer, GeoChunkType type, int size, int offset)
    {
        writer.Write((ushort)type);
        writer.Write((ushort)0);
        writer.Write(size);
        writer.Write(offset);
    }

    private static void WriteVector(EndianBinaryWriter writer, float x, float y, float z, float w)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);
        writer.Write(w);
    }
}
