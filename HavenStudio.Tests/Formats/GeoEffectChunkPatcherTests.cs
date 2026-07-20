using System.Buffers.Binary;
using HavenStudio.Extensions;
using HavenStudio.Formats.Geo;

namespace HavenStudio.Tests.Formats;

public sealed class GeoEffectChunkPatcherTests
{
    [Fact]
    public void Game_angle_quarter_turn_decodes_to_ninety_degrees()
    {
        Assert.Equal(MathF.PI / 2f, GeoEffectChunkPatcher.DecodeAngle(0x4000), 6);
        Assert.Equal(-MathF.PI, GeoEffectChunkPatcher.DecodeAngle(short.MinValue), 6);
    }

    [Fact]
    public void Patch_preserves_chunk_bytes_when_effect_is_unchanged()
    {
        var chunk = Enumerable.Range(0, 96).Select(value => (byte)value).ToArray();
        const int chunkOffset = 8;
        const int rotationSlot = 5;
        const int positionOffset = chunkOffset + 0x10;
        const int rotationOffset = chunkOffset + rotationSlot * 8;
        WriteBigEndianSingle(chunk, positionOffset, 1.25f);
        WriteBigEndianSingle(chunk, positionOffset + 4, -2.5f);
        WriteBigEndianSingle(chunk, positionOffset + 8, 3.75f);
        WriteBigEndianSingle(chunk, positionOffset + 12, 1f);
        BinaryPrimitives.WriteInt16BigEndian(chunk.AsSpan(rotationOffset), 1234);
        BinaryPrimitives.WriteInt16BigEndian(chunk.AsSpan(rotationOffset + 2), -2345);
        BinaryPrimitives.WriteInt16BigEndian(chunk.AsSpan(rotationOffset + 4), 32767);
        var original = chunk.ToArray();
        var effect = new GeoEffect
        {
            ChunkOffset = chunkOffset,
            Index = 2 | (rotationSlot << 10),
            X = 1.25f,
            Y = -2.5f,
            Z = 3.75f,
            W = 1f,
            RotationX = GeoEffectChunkPatcher.DecodeAngle(1234),
            RotationY = GeoEffectChunkPatcher.DecodeAngle(-2345),
            RotationZ = GeoEffectChunkPatcher.DecodeAngle(32767)
        };

        GeoEffectChunkPatcher.Patch(chunk, [effect], Endianness.Big);

        Assert.Equal(original, chunk);
    }

    [Fact]
    public void Patch_changes_only_the_edited_position_and_rotation_fields()
    {
        var chunk = Enumerable.Repeat((byte)0xCC, 96).ToArray();
        const int chunkOffset = 8;
        const int rotationSlot = 5;
        const int positionOffset = chunkOffset + 0x10;
        const int rotationOffset = chunkOffset + rotationSlot * 8;
        WriteBigEndianSingle(chunk, positionOffset, 1f);
        WriteBigEndianSingle(chunk, positionOffset + 4, 2f);
        WriteBigEndianSingle(chunk, positionOffset + 8, 3f);
        WriteBigEndianSingle(chunk, positionOffset + 12, 4f);
        BinaryPrimitives.WriteInt16BigEndian(chunk.AsSpan(rotationOffset), 100);
        BinaryPrimitives.WriteInt16BigEndian(chunk.AsSpan(rotationOffset + 2), 200);
        BinaryPrimitives.WriteInt16BigEndian(chunk.AsSpan(rotationOffset + 4), 300);
        var original = chunk.ToArray();
        var expected = chunk.ToArray();
        WriteBigEndianSingle(expected, positionOffset, -8f);
        BinaryPrimitives.WriteInt16BigEndian(expected.AsSpan(rotationOffset + 2), -400);
        var effect = new GeoEffect
        {
            ChunkOffset = chunkOffset,
            Index = 2 | (rotationSlot << 10),
            X = -8f,
            Y = 2f,
            Z = 3f,
            W = 4f,
            RotationX = GeoEffectChunkPatcher.DecodeAngle(100),
            RotationY = GeoEffectChunkPatcher.DecodeAngle(-400),
            RotationZ = GeoEffectChunkPatcher.DecodeAngle(300)
        };

        GeoEffectChunkPatcher.Patch(chunk, [effect], Endianness.Big);

        Assert.Equal(expected, chunk);
        var changedOffsets = chunk
            .Select((value, index) => (value, index))
            .Where(item => item.value != original[item.index])
            .Select(item => item.index)
            .ToArray();
        Assert.NotEmpty(changedOffsets);
        Assert.All(changedOffsets, offset => Assert.True(
            offset >= positionOffset && offset < positionOffset + 4
            || offset >= rotationOffset + 2 && offset < rotationOffset + 4));
        Assert.Equal(-8f, ReadBigEndianSingle(chunk, positionOffset));
        Assert.Equal(-400, BinaryPrimitives.ReadInt16BigEndian(chunk.AsSpan(rotationOffset + 2)));
    }

    [Fact]
    public void Patch_updates_nested_effects()
    {
        var chunk = new byte[96];
        var parent = new GeoEffect();
        parent.Children.Add(new GeoEffect
        {
            ChunkOffset = 32,
            Index = 2,
            X = 10f,
            Y = 20f,
            Z = 30f,
            W = 1f
        });

        GeoEffectChunkPatcher.Patch(chunk, [parent], Endianness.Little);

        Assert.Equal(10f, BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32LittleEndian(chunk.AsSpan(48, 4))));
    }

    [Fact]
    public void Patch_uses_the_packed_position_slot_instead_of_a_fixed_offset()
    {
        var chunk = Enumerable.Repeat((byte)0xCC, 128).ToArray();
        const int chunkOffset = 8;
        const int positionSlot = 6;
        var effect = new GeoEffect
        {
            ChunkOffset = chunkOffset,
            Index = positionSlot,
            X = 10f,
            Y = 20f,
            Z = 30f,
            W = 1f
        };

        GeoEffectChunkPatcher.Patch(chunk, [effect], Endianness.Little);

        var positionOffset = chunkOffset + positionSlot * 8;
        Assert.Equal(10f, BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32LittleEndian(chunk.AsSpan(positionOffset, 4))));
        Assert.Equal(0xCCCCCCCCu, BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(chunkOffset + 0x10, 4)));
    }

    [Fact]
    public void Structural_builder_preserves_payloads_and_rebuilds_next_child_links()
    {
        var chunk = new byte[0x60];
        WriteEffectHeader(chunk, 0x00, next: 0x40, child: 0x20, name: 1, index: 2);
        WriteEffectHeader(chunk, 0x20, next: 0, child: 0, name: 2, index: 2);
        WriteEffectHeader(chunk, 0x40, next: 0, child: 0, name: 3, index: 2);
        WriteBigEndianSingle(chunk, 0x10, 1f);
        WriteBigEndianSingle(chunk, 0x1C, 1f);
        WriteBigEndianSingle(chunk, 0x30, 2f);
        WriteBigEndianSingle(chunk, 0x3C, 1f);
        WriteBigEndianSingle(chunk, 0x50, 3f);
        WriteBigEndianSingle(chunk, 0x5C, 1f);
        var first = new GeoEffect { ChunkOffset = 0, Name = 1, Index = 2, X = 1f, W = 1f };
        first.Children.Add(new GeoEffect { ChunkOffset = 0x20, Name = 2, Index = 2, X = 2f, W = 1f });
        var last = new GeoEffect { ChunkOffset = 0x40, Name = 3, Index = 2, X = 3f, W = 1f };
        var roots = new List<GeoEffect> { first, last };

        var layout = GeoEffectChunkBuilder.Capture(chunk, roots, Endianness.Big);
        Assert.Equal(chunk, layout.Rebuild(roots));

        roots.Remove(last);
        var deleted = layout.Rebuild(roots);
        Assert.Equal(0x40, deleted.Length);
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(deleted));
        Assert.Equal(0x20, BinaryPrimitives.ReadInt32BigEndian(deleted.AsSpan(4)));

        layout = GeoEffectChunkBuilder.Capture(deleted, roots, Endianness.Big);
        roots.Add(new GeoEffect { Name = 4, Index = 2, X = 9f, W = 1f });
        var added = layout.Rebuild(roots);
        Assert.Equal(0x60, added.Length);
        Assert.Equal(0x40, BinaryPrimitives.ReadInt32BigEndian(added));
        Assert.Equal(0x40, roots[1].ChunkOffset);
        Assert.Equal(4, BinaryPrimitives.ReadInt32BigEndian(added.AsSpan(0x48)));
        Assert.Equal(9f, ReadBigEndianSingle(added, 0x50));
    }

    [Fact]
    public void Structural_builder_clones_the_source_effects_opaque_record_data()
    {
        var chunk = new byte[0x20];
        WriteEffectHeader(chunk, 0, next: 0, child: 0, name: 1, index: 0);
        chunk[0x17] = 0xA5;
        var source = new GeoEffect { Name = 1, Index = 0 };
        var roots = new List<GeoEffect> { source };
        var layout = GeoEffectChunkBuilder.Capture(chunk, roots, Endianness.Big);
        var clone = new GeoEffect { Name = 2, Index = 0 };

        layout.CloneRecord(source, clone);
        roots.Add(clone);
        var rebuilt = layout.Rebuild(roots);

        Assert.Equal(0x40, rebuilt.Length);
        Assert.Equal(0xA5, rebuilt[0x17]);
        Assert.Equal(0xA5, rebuilt[0x37]);
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(rebuilt.AsSpan(0x28)));
    }

    private static void WriteBigEndianSingle(byte[] destination, int offset, float value)
    {
        BinaryPrimitives.WriteInt32BigEndian(
            destination.AsSpan(offset, 4),
            BitConverter.SingleToInt32Bits(value));
    }

    private static void WriteEffectHeader(
        byte[] destination,
        int offset,
        int next,
        int child,
        int name,
        int index)
    {
        BinaryPrimitives.WriteInt32BigEndian(destination.AsSpan(offset), next);
        BinaryPrimitives.WriteInt32BigEndian(destination.AsSpan(offset + 4), child);
        BinaryPrimitives.WriteInt32BigEndian(destination.AsSpan(offset + 8), name);
        BinaryPrimitives.WriteInt32BigEndian(destination.AsSpan(offset + 12), index);
    }

    private static float ReadBigEndianSingle(byte[] source, int offset)
    {
        return BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32BigEndian(source.AsSpan(offset, 4)));
    }
}
