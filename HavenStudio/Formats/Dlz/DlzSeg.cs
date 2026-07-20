using System;
using System.Collections.Generic;
using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Dlz;

public class DlzSeg
{
    private byte[] _leadingPadding = Array.Empty<byte>();
    private readonly List<byte[]> _chunkPadding = new();

    public uint Magic;
    public ushort Flag;
    public ushort ChunkCount;
    public uint SizeDecompressed;
    public uint SizeCompressed;

    public List<DlzSegIndex> SegIndex = new List<DlzSegIndex>();
    public List<byte[]> SegData = new List<byte[]>();

    internal uint SerializedSize => SizeCompressed;

    public DlzSeg(EndianBinaryReader reader)
        : this(reader, reader.BaseStream.Length)
    {
    }

    internal DlzSeg(EndianBinaryReader reader, long availableEnd)
    {
        var segmentStart = reader.BaseStream.Position;
        if (availableEnd < segmentStart || availableEnd > reader.BaseStream.Length)
            throw new ArgumentOutOfRangeException(nameof(availableEnd));
        if (availableEnd - segmentStart < 0x10)
            throw new InvalidDataException($"DLZ segment header at 0x{segmentStart:X} is truncated.");

        Magic = reader.ReadUInt32();
        if (Magic != 0x73656773)
            throw new InvalidDataException($"DLZ segment at 0x{segmentStart:X} has invalid magic 0x{Magic:X8}.");

        Flag = reader.ReadUInt16();
        ChunkCount = reader.ReadUInt16();
        SizeDecompressed = reader.ReadUInt32();
        SizeCompressed = BitConverter.ToUInt32(reader.ReadBytes(sizeof(uint)));

        var segmentEnd = checked(segmentStart + SizeCompressed);
        if (segmentEnd > availableEnd)
        {
            throw new InvalidDataException(
                $"DLZ segment at 0x{segmentStart:X} declares {SizeCompressed} bytes, beyond its available range.");
        }

        var indexBytes = (long)ChunkCount * 8;
        if (indexBytes > segmentEnd - reader.BaseStream.Position)
            throw new InvalidDataException($"DLZ segment index at 0x{segmentStart:X} is truncated.");

        for (var i = 0; i < ChunkCount; i++)
        {
            SegIndex.Add(new DlzSegIndex(reader));
        }

        var metadataEnd = reader.BaseStream.Position;
        var chunkStarts = new long[ChunkCount];
        for (var i = 0; i < ChunkCount; i++)
        {
            if (SegIndex[i].ChunkOffset == 0)
                throw new InvalidDataException($"DLZ chunk {i} at 0x{segmentStart:X} has a zero offset.");

            chunkStarts[i] = checked(segmentStart + SegIndex[i].ChunkOffset - 1L);
            if (chunkStarts[i] < metadataEnd || chunkStarts[i] > segmentEnd)
            {
                throw new InvalidDataException(
                    $"DLZ chunk {i} at 0x{chunkStarts[i]:X} starts outside its segment.");
            }

            if (i > 0 && chunkStarts[i] < chunkStarts[i - 1])
            {
                throw new InvalidDataException(
                    $"DLZ chunk offsets are not ordered in segment 0x{segmentStart:X}.");
            }
        }

        var firstChunkStart = ChunkCount > 0 ? chunkStarts[0] : segmentEnd;
        _leadingPadding = ReadPadding(reader, metadataEnd, firstChunkStart, segmentStart);

        for (var i = 0; i < ChunkCount; i++)
        {
            var offset = chunkStarts[i];
            var end = checked(offset + SegIndex[i].SizeCompressed);
            var paddingEnd = i + 1 < ChunkCount ? chunkStarts[i + 1] : segmentEnd;
            if (end > paddingEnd)
            {
                throw new InvalidDataException(
                    $"DLZ chunk {i} at 0x{offset:X} with {SegIndex[i].SizeCompressed} bytes exceeds its segment range.");
            }

            reader.BaseStream.Position = offset;
            var data = reader.ReadBytes(SegIndex[i].SizeCompressed);
            if (data.Length != SegIndex[i].SizeCompressed)
                throw new InvalidDataException($"DLZ chunk {i} at 0x{offset:X} is truncated.");

            SegData.Add(data);
            _chunkPadding.Add(ReadPadding(reader, end, paddingEnd, segmentStart));
        }

        ValidateLayout();
    }

    public DlzSeg(uint magic, ushort flag, ushort chunkCount, uint sizeDecompressed, uint sizeCompressed)
    {
        Magic = magic;
        Flag = flag;
        ChunkCount = chunkCount;
        SizeDecompressed = sizeDecompressed;
        SizeCompressed = sizeCompressed;
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        if (writer is null) throw new ArgumentNullException(nameof(writer));
        ValidateLayout();

        writer.Write(Magic);
        writer.Write(Flag);
        writer.Write(ChunkCount);
        writer.Write(SizeDecompressed);
        writer.Write(BitConverter.GetBytes(SizeCompressed));

        foreach (var index in SegIndex)
        {
            index.WriteTo(writer);
        }

        writer.Write(_leadingPadding);
        for (var i = 0; i < SegData.Count; i++)
        {
            writer.Write(SegData[i]);
            writer.Write(_chunkPadding[i]);
        }
    }

    /// <summary>
    /// Rebuilds chunk offsets, sizes, and padding using the canonical 16-byte layout.
    /// </summary>
    public void Repack()
    {
        if (SegIndex.Count != SegData.Count)
        {
            throw new InvalidOperationException(
                "DLZ segment indexes and compressed data must contain the same number of entries.");
        }

        ChunkCount = checked((ushort)SegData.Count);
        SizeDecompressed = 0;
        var position = checked(0x10 + (ChunkCount * 0x08));

        _leadingPadding = new byte[GetPaddingLength(position, 16)];
        position += _leadingPadding.Length;
        _chunkPadding.Clear();

        for (var i = 0; i < ChunkCount; i++)
        {
            var index = SegIndex[i];
            var data = SegData[i];
            index.SizeCompressed = checked((ushort)data.Length);
            index.ChunkOffset = checked((uint)position + 1);
            SizeDecompressed = checked(SizeDecompressed + index.SizeDecompressed);

            position = checked(position + data.Length);
            var padding = new byte[GetPaddingLength(position, 16)];
            _chunkPadding.Add(padding);
            position += padding.Length;
        }

        SizeCompressed = checked((uint)position);
    }

    internal void ValidateLayout()
    {
        if (ChunkCount != SegIndex.Count || ChunkCount != SegData.Count || ChunkCount != _chunkPadding.Count)
        {
            throw new InvalidOperationException(
                "The DLZ segment structure changed without rebuilding its layout. Call Repack() before saving.");
        }

        long position = 0x10 + ((long)ChunkCount * 0x08) + _leadingPadding.Length;
        for (var i = 0; i < ChunkCount; i++)
        {
            var expectedOffset = checked((uint)position + 1);
            if (SegIndex[i].ChunkOffset != expectedOffset || SegIndex[i].SizeCompressed != SegData[i].Length)
            {
                throw new InvalidOperationException(
                    "The DLZ segment structure changed without rebuilding its layout. Call Repack() before saving.");
            }

            position = checked(position + SegData[i].Length + _chunkPadding[i].Length);
        }

        if (position != SizeCompressed)
        {
            throw new InvalidOperationException(
                "The DLZ segment structure changed without rebuilding its layout. Call Repack() before saving.");
        }
    }

    private static byte[] ReadPadding(
        EndianBinaryReader reader,
        long start,
        long end,
        long segmentStart)
    {
        if (end < start)
            throw new InvalidDataException($"DLZ segment at 0x{segmentStart:X} has overlapping ranges.");

        var length = end - start;
        if (length > int.MaxValue)
            throw new InvalidDataException($"DLZ padding at 0x{start:X} is too large.");

        reader.BaseStream.Position = start;
        var padding = reader.ReadBytes((int)length);
        if (padding.Length != (int)length)
            throw new InvalidDataException($"DLZ padding at 0x{start:X} is truncated.");

        return padding;
    }

    private static int GetPaddingLength(long position, int alignment)
    {
        return checked((int)((alignment - (position % alignment)) % alignment));
    }

    public int GetTotalDecompressedSize()
    {
        var result = 0;
        for (var i = 0; i < SegIndex.Count; i++)
        {
            result += SegIndex[i].SizeDecompressed;
        }

        return result;
    }

    public int CalculateSize()
    {
        Repack();
        return checked((int)SizeCompressed);
    }
}
