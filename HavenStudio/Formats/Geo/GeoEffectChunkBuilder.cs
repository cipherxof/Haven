using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Geo;

/// <summary>
/// Captures the opaque per-effect payloads in chunk 6, then rebuilds the
/// next/child tree while preserving those payloads by effect identity.
/// </summary>
public sealed class GeoEffectChunkLayout
{
    private readonly Dictionary<GeoEffect, byte[]> _recordData;
    private readonly Endianness _endianness;

    internal GeoEffectChunkLayout(
        IReadOnlyDictionary<GeoEffect, byte[]> recordData,
        Endianness endianness)
    {
        _recordData = new Dictionary<GeoEffect, byte[]>(recordData);
        _endianness = endianness;
    }

    public void CloneRecord(GeoEffect source, GeoEffect clone)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(clone);
        if (!_recordData.TryGetValue(source, out var record))
        {
            throw new InvalidDataException("The source GEOM effect has no captured chunk-6 record.");
        }
        if (!_recordData.TryAdd(clone, record.ToArray()))
        {
            throw new InvalidOperationException("The cloned GEOM effect already has chunk-6 record data.");
        }
    }

    public byte[] Rebuild(IReadOnlyList<GeoEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        using var output = new MemoryStream();
        var visited = new HashSet<GeoEffect>();
        WriteList(output, effects, visited);
        var result = output.ToArray();
        GeoEffectChunkPatcher.Patch(result, effects, _endianness);
        return result;
    }

    private void WriteList(
        MemoryStream output,
        IReadOnlyList<GeoEffect> effects,
        ISet<GeoEffect> visited)
    {
        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            if (!visited.Add(effect))
            {
                throw new InvalidDataException("GEOM effect hierarchy contains a duplicate or cycle.");
            }

            var nodeStart = checked((int)output.Position);
            effect.ChunkOffset = nodeStart;
            var record = BuildRecordData(effect);
            output.Write(record);
            if (effect.Children.Count != 0)
            {
                WriteList(output, effect.Children, visited);
            }
            var nodeEnd = checked((int)output.Position);
            var next = index == effects.Count - 1 ? 0 : checked(nodeEnd - nodeStart);
            var child = effect.Children.Count == 0 ? 0 : record.Length;
            WriteInt32(output.GetBuffer().AsSpan(nodeStart, 4), next);
            WriteInt32(output.GetBuffer().AsSpan(nodeStart + 4, 4), child);
        }
    }

    private byte[] BuildRecordData(GeoEffect effect)
    {
        var requiredLength = GetRequiredRecordLength(effect.Index);
        var original = _recordData.GetValueOrDefault(effect);
        var record = new byte[Math.Max(requiredLength, original?.Length ?? 0x10)];
        original?.CopyTo(record, 0);
        WriteInt32(record.AsSpan(8, 4), effect.Name);
        WriteInt32(record.AsSpan(12, 4), effect.Index);
        return record;
    }

    private static int GetRequiredRecordLength(int index)
    {
        var length = 0x10;
        var positionSlot = GeoEffectLayout.GetPositionSlot(index);
        var rotationSlot = GeoEffectLayout.GetRotationSlot(index);
        var scaleSlot = GeoEffectLayout.GetScaleSlot(index);
        if (positionSlot != 0)
        {
            length = Math.Max(length, checked(positionSlot * 8 + 0x10));
        }
        if (rotationSlot != 0)
        {
            length = Math.Max(length, checked(rotationSlot * 8 + 6));
        }
        if (scaleSlot != 0)
        {
            length = Math.Max(length, checked(scaleSlot * 8 + 0x10));
        }
        return checked((length + 7) & ~7);
    }

    private void WriteInt32(Span<byte> destination, int value)
    {
        if (_endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteInt32BigEndian(destination, value);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, value);
        }
    }
}

public static class GeoEffectChunkBuilder
{
    public static GeoEffectChunkLayout Capture(
        byte[] chunk,
        IReadOnlyList<GeoEffect> effects,
        Endianness endianness)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(effects);
        var records = new Dictionary<GeoEffect, byte[]>();
        if (effects.Count == 0)
        {
            if (chunk.Length != 0)
            {
                throw new InvalidDataException("GEOM chunk 6 contains data but no parsed effect root.");
            }
            return new GeoEffectChunkLayout(records, endianness);
        }
        if (effects[0].ChunkOffset != 0)
        {
            throw new InvalidDataException("The first GEOM effect does not begin at chunk-6 offset zero.");
        }

        CaptureList(effects, chunk.Length);
        return new GeoEffectChunkLayout(records, endianness);

        void CaptureList(IReadOnlyList<GeoEffect> siblings, int boundary)
        {
            for (var index = 0; index < siblings.Count; index++)
            {
                var effect = siblings[index];
                var offset = effect.ChunkOffset;
                EnsureRange(offset, 0x10);
                var storedNext = ReadInt32(chunk.AsSpan(offset, 4));
                var storedChild = ReadInt32(chunk.AsSpan(offset + 4, 4));
                var expectedNext = index == siblings.Count - 1
                    ? 0
                    : checked(siblings[index + 1].ChunkOffset - offset);
                if (storedNext != expectedNext)
                {
                    throw new InvalidDataException(
                        $"GEOM effect at 0x{offset:X} stores next 0x{storedNext:X}; expected 0x{expectedNext:X}.");
                }

                var nextBoundary = storedNext == 0 ? boundary : checked(offset + storedNext);
                var expectedChild = effect.Children.Count == 0
                    ? 0
                    : checked(effect.Children[0].ChunkOffset - offset);
                if (storedChild != expectedChild)
                {
                    throw new InvalidDataException(
                        $"GEOM effect at 0x{offset:X} stores child 0x{storedChild:X}; expected 0x{expectedChild:X}.");
                }

                var recordEnd = storedChild == 0 ? nextBoundary : checked(offset + storedChild);
                if (recordEnd < offset + 0x10 || recordEnd > nextBoundary || nextBoundary > boundary)
                {
                    throw new InvalidDataException(
                        $"GEOM effect at 0x{offset:X} has an invalid record/subtree boundary.");
                }
                if (!records.TryAdd(effect, chunk.AsSpan(offset, recordEnd - offset).ToArray()))
                {
                    throw new InvalidDataException("GEOM effect hierarchy contains a duplicate or cycle.");
                }
                if (effect.Children.Count != 0)
                {
                    CaptureList(effect.Children, nextBoundary);
                }
            }
        }

        void EnsureRange(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > chunk.Length - length)
            {
                throw new InvalidDataException(
                    $"GEOM effect record at 0x{offset:X} is outside chunk 6.");
            }
        }

        int ReadInt32(ReadOnlySpan<byte> source) => endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt32BigEndian(source)
            : BinaryPrimitives.ReadInt32LittleEndian(source);
    }
}
