using HavenStudio.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HavenStudio.Formats.Dlz;

public class DlzFile
{
    private const int SegmentSize = 0x20000;
    private const int FileAlignment = 0x800;
    private readonly List<DlzRegion> _layout = new();

    public IReadOnlyList<DlzSeg> Segs => _layout
        .OfType<SegmentRegion>()
        .Select(region => region.Segment)
        .ToArray();

    public DlzFile(string path)
        : this(path, EndianBinaryReader.DefaultEndianness)
    {
    }

    public DlzFile(string path, Endianness endianness)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        ReadFromStream(stream, endianness);
    }

    public DlzFile(Stream stream, Endianness endianness)
    {
        ReadFromStream(stream, endianness);
    }

    public DlzFile(List<DlzDataContainer> containers)
    {
        if (containers is null) throw new ArgumentNullException(nameof(containers));

        var segments = new List<DlzSeg>();
        var segment = CreateSegment();

        foreach (var container in containers)
        {
            if (segment.SizeCompressed + container.SizeCompressed > SegmentSize)
            {
                segments.Add(segment);
                segment = CreateSegment();
            }

            segment.SegIndex.Add(new DlzSegIndex(
                checked((ushort)container.SizeCompressed),
                checked((ushort)container.SizeDecompressed),
                0));
            segment.SegData.Add(container.CompressedData);
            segment.Repack();
        }

        segments.Add(segment);
        RebuildCanonicalLayout(segments);
    }

    public void Save(string path, Endianness endianness)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Save(stream, endianness);
    }

    public void Save(Stream stream, Endianness endianness)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite || !stream.CanSeek)
            throw new ArgumentException("DLZ writing requires a writable, seekable stream.", nameof(stream));

        foreach (var region in _layout)
        {
            region.Validate();
        }

        stream.SetLength(0);
        stream.Position = 0;
        using var writer = new EndianBinaryWriter(stream, endianness, leaveOpen: true);

        foreach (var region in _layout)
        {
            region.WriteTo(writer);
        }

        writer.Flush();
    }

    /// <summary>
    /// Rebuilds all segment offsets and replaces file padding with canonical zero-filled regions.
    /// Call this after making a structural change to segment indexes or compressed data.
    /// </summary>
    public void Repack()
    {
        RebuildCanonicalLayout(Segs);
    }

    public void Unpack(string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Unpack(stream);
    }

    public void Unpack(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));

        foreach (var segment in Segs)
        {
            for (var i = 0; i < segment.ChunkCount; i++)
            {
                Utils.Compression.InflateToStream(segment.SegData[i], stream);
            }
        }
    }

    private static DlzSeg CreateSegment()
    {
        return new DlzSeg(0x73656773, 4, 0, 0, 0);
    }

    private void RebuildCanonicalLayout(IEnumerable<DlzSeg> sourceSegments)
    {
        var segments = sourceSegments.ToArray();
        _layout.Clear();

        long position = 0;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            segment.Repack();
            _layout.Add(new SegmentRegion(segment));
            position = checked(position + segment.SerializedSize);

            var alignment = i == segments.Length - 1 ? FileAlignment : SegmentSize;
            var paddingLength = GetPaddingLength(position, alignment);
            if (paddingLength == 0)
            {
                continue;
            }

            _layout.Add(new OpaqueRegion(new byte[paddingLength]));
            position += paddingLength;
        }
    }

    private void ReadFromStream(Stream stream, Endianness endianness)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException("DLZ reading requires a seekable stream.", nameof(stream));
        if (stream.Length - stream.Position < 0x10)
            throw new InvalidDataException("DLZ header is truncated.");

        _layout.Clear();

        using var reader = new EndianBinaryReader(stream, endianness, leaveOpen: true);
        var fileStart = stream.Position;
        var segmentStarts = FindSegmentStarts(reader, fileStart, stream.Length);
        if (segmentStarts.Count == 0)
        {
            stream.Position = fileStart;
            _ = new DlzSeg(reader, stream.Length);
            throw new InvalidDataException("DLZ contains no segments.");
        }

        var nextRegionStart = fileStart;
        for (var i = 0; i < segmentStarts.Count; i++)
        {
            var segmentStart = segmentStarts[i];
            if (segmentStart > nextRegionStart)
            {
                _layout.Add(new OpaqueRegion(ReadBytes(reader, nextRegionStart, segmentStart)));
            }

            var availableEnd = i + 1 < segmentStarts.Count ? segmentStarts[i + 1] : stream.Length;
            stream.Position = segmentStart;
            var segment = new DlzSeg(reader, availableEnd);
            _layout.Add(new SegmentRegion(segment));
            nextRegionStart = checked(segmentStart + segment.SerializedSize);
        }

        if (nextRegionStart < stream.Length)
        {
            _layout.Add(new OpaqueRegion(ReadBytes(reader, nextRegionStart, stream.Length)));
        }

        stream.Position = stream.Length;
    }

    private static List<long> FindSegmentStarts(EndianBinaryReader reader, long start, long end)
    {
        var result = new List<long>();
        for (var position = start; end - position >= sizeof(uint); position += SegmentSize)
        {
            reader.BaseStream.Position = position;
            if (reader.ReadUInt32() == 0x73656773)
            {
                result.Add(position);
            }
        }

        return result;
    }

    private static byte[] ReadBytes(EndianBinaryReader reader, long start, long end)
    {
        var length = end - start;
        if (length < 0 || length > int.MaxValue)
            throw new InvalidDataException($"Opaque DLZ data at 0x{start:X} has an invalid size.");

        reader.BaseStream.Position = start;
        var data = reader.ReadBytes((int)length);
        if (data.Length != (int)length)
            throw new InvalidDataException($"Opaque DLZ data at 0x{start:X} is truncated.");

        return data;
    }

    private static int GetPaddingLength(long position, int alignment)
    {
        return checked((int)((alignment - (position % alignment)) % alignment));
    }

    private abstract record DlzRegion
    {
        public abstract void Validate();
        public abstract void WriteTo(EndianBinaryWriter writer);
    }

    private sealed record SegmentRegion(DlzSeg Segment) : DlzRegion
    {
        public override void Validate()
        {
            Segment.ValidateLayout();
        }

        public override void WriteTo(EndianBinaryWriter writer)
        {
            Segment.WriteTo(writer);
        }
    }

    private sealed record OpaqueRegion(byte[] Data) : DlzRegion
    {
        public override void Validate()
        {
        }

        public override void WriteTo(EndianBinaryWriter writer)
        {
            writer.Write(Data);
        }
    }
}
