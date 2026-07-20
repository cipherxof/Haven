using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HavenStudio.Formats.Geo;

/// <summary>
/// Re-packs a GEO_BLOCK arena after a variable-sized GEOM record changes while
/// preserving the original active/child graph by object identity.
/// </summary>
public sealed class GeoBlockArenaLayout
{
    private const int OffsetUnit = 0x10;
    private const int ArenaLimit = 0x3C00;
    private const ushort MissingOffset = 0xFFFF;

    private readonly GeoBlock _block;
    private readonly IReadOnlyList<Geom> _records;
    private readonly IReadOnlyDictionary<Geom, Links> _links;
    private readonly GeoVertexHeader? _vertexHeader;
    private readonly Geom? _head;
    private readonly Geom? _tail;

    internal GeoBlockArenaLayout(
        GeoBlock block,
        IReadOnlyList<Geom> records,
        IReadOnlyDictionary<Geom, Links> links,
        GeoVertexHeader? vertexHeader,
        Geom? head,
        Geom? tail)
    {
        _block = block;
        _records = records;
        _links = links;
        _vertexHeader = vertexHeader;
        _head = head;
        _tail = tail;
    }

    public void Rebuild()
    {
        if (_records.Count > byte.MaxValue)
        {
            throw new InvalidDataException(
                $"GEOM block has {_records.Count} arena records; the format limit is {byte.MaxValue}.");
        }

        var units = new Dictionary<Geom, int>();
        var byteOffset = 0;
        foreach (var record in _records)
        {
            // These four fields are the byte representation written by Geom.WriteTo.
            // Keep Flag synchronized because primitive dispatch and any subsequent
            // allocator work consume the packed uint value.
            record.Flag = (uint)(record.Length << 24 |
                                 record.Type << 16 |
                                 record.Field002 << 8 |
                                 record.Field003);
            if ((byteOffset & (OffsetUnit - 1)) != 0)
            {
                throw new InvalidDataException("A GEOM arena record does not begin on a 16-byte boundary.");
            }

            units.Add(record, byteOffset / OffsetUnit);
            record.Offset = checked(_block.FaceOffset + byteOffset);
            byteOffset = checked(byteOffset + GetSerializedSize(record));
        }

        var highWater = CalculateHighWater(_records, _vertexHeader);
        if (highWater > ArenaLimit)
        {
            throw new InvalidDataException(
                $"GEOM block arena requires a 0x{highWater:X} byte high-water mark; " +
                $"the release limit is 0x{ArenaLimit:X}.");
        }

        foreach (var record in _records)
        {
            var links = _links[record];
            record.Next = Relative(record, links.Next);
            record.Prev = Relative(record, links.Previous);
            record.Child = Relative(record, links.Child);
        }

        _block.GeomCount = checked((byte)_records.Count);
        _block.Size = checked((ushort)highWater);
        _block.Free = MissingOffset;
        _block.Head = _head == null ? MissingOffset : checked((ushort)units[_head]);
        _block.Tail = _tail == null ? MissingOffset : checked((ushort)units[_tail]);

        int Relative(Geom source, Geom? target) =>
            target == null ? 0 : checked(units[target] - units[source]);
    }

    internal static int GetSerializedSize(Geom record)
    {
        const int headerSize = 0x20;
        var payloadSize = record.GetPrimType() switch
        {
            Geom.Primitive.GEO_DOT => checked(record.Length * 0x20),
            Geom.Primitive.GEO_LINE => checked(record.Length * 0x30),
            Geom.Primitive.GEO_POLY => checked(record.Length * 0x08),
            Geom.Primitive.GEO_BOX => checked(record.Length * 0x60),
            Geom.Primitive.GEO_FIELD => 0x20,
            Geom.Primitive.GEO_REF => checked(record.Length * 0x70),
            _ => 0
        };
        var size = checked(headerSize + payloadSize + record.Data.Length);
        if ((size & (OffsetUnit - 1)) != 0)
        {
            throw new InvalidDataException(
                $"GEOM record at 0x{record.Offset:X} serializes to non-aligned size 0x{size:X}.");
        }
        return size;
    }

    internal static int CalculateHighWater(
        IReadOnlyList<Geom> records,
        GeoVertexHeader? vertexHeader)
    {
        var size = records.Sum(GetSerializedSize);
        if (vertexHeader != null)
        {
            return checked(size + 0x10 + vertexHeader.Data.Length * 0x10);
        }

        if (records.Count == 0)
        {
            return 0;
        }

        // The last disk record is padded so a following record would begin on
        // a 16-byte boundary. With no vertex table, Size stops at the meaningful
        // plugin payload instead of including that final alignment padding.
        var last = records[^1];
        var pluginUnit = ((last.Flag >> 4) & 0xF) switch
        {
            2 => 0x08,
            3 => 0x20,
            4 => 0x30,
            _ => 0
        };
        var finalPadding = Math.Max(0, last.Data.Length - pluginUnit);
        return checked(size - finalPadding);
    }

    internal sealed record Links(Geom? Next, Geom? Previous, Geom? Child);
}

public static class GeoBlockArenaBuilder
{
    private const int OffsetUnit = 0x10;
    private const ushort MissingOffset = 0xFFFF;

    public static GeoBlockArenaLayout Capture(
        GeoBlock block,
        IReadOnlyList<Geom> records,
        GeoVertexHeader? vertexHeader)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return new GeoBlockArenaLayout(
                block,
                records,
                new Dictionary<Geom, GeoBlockArenaLayout.Links>(),
                vertexHeader,
                null,
                null);
        }

        var byUnit = records.ToDictionary(
            record => ToUnit(record.Offset - block.FaceOffset, "record"),
            record => record);
        Geom? Resolve(Geom source, int relative, string link)
        {
            if (relative == 0)
            {
                return null;
            }

            var sourceUnit = ToUnit(source.Offset - block.FaceOffset, "record");
            if (!byUnit.TryGetValue(checked(sourceUnit + relative), out var target))
            {
                throw new InvalidDataException(
                    $"GEOM {link} link at 0x{source.Offset:X} does not target an arena record.");
            }
            return target;
        }

        var links = records.ToDictionary(
            record => record,
            record => new GeoBlockArenaLayout.Links(
                Resolve(record, record.Next, "next"),
                Resolve(record, record.Prev, "previous"),
                Resolve(record, record.Child, "child")));
        var head = ResolveBlockOffset(block.Head, "head");
        var tail = ResolveBlockOffset(block.Tail, "tail");
        return new GeoBlockArenaLayout(
            block,
            records,
            links,
            vertexHeader,
            head,
            tail);

        Geom? ResolveBlockOffset(ushort offset, string name)
        {
            if (offset == MissingOffset)
            {
                return null;
            }
            if (!byUnit.TryGetValue(offset, out var target))
            {
                throw new InvalidDataException($"GEOM block {name} 0x{offset:X} is not an arena record.");
            }
            return target;
        }
    }

    public static GeoBlockArenaLayout RebuildSequential(
        GeoBlock block,
        IReadOnlyList<Geom> records,
        GeoVertexHeader? vertexHeader)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(records);
        var links = new Dictionary<Geom, GeoBlockArenaLayout.Links>();
        for (var index = 0; index < records.Count; index++)
        {
            links.Add(
                records[index],
                new GeoBlockArenaLayout.Links(
                    index + 1 < records.Count ? records[index + 1] : null,
                    index == 0 ? null : records[index - 1],
                    null));
        }
        var layout = new GeoBlockArenaLayout(
            block,
            records,
            links,
            vertexHeader,
            records.Count == 0 ? null : records[0],
            records.Count == 0 ? null : records[^1]);
        layout.Rebuild();
        return layout;
    }

    private static int ToUnit(int byteOffset, string description)
    {
        if (byteOffset < 0 || byteOffset % OffsetUnit != 0)
        {
            throw new InvalidDataException(
                $"GEOM arena {description} byte offset 0x{byteOffset:X} is not 16-byte aligned.");
        }
        return byteOffset / OffsetUnit;
    }
}
