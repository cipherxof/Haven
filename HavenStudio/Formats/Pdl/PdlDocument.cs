using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Pdl;

public sealed class PdlInstance
{
    public PdlInstance(Vector3 position, float scale)
    {
        Position = position;
        Scale = scale;
    }

    public Vector3 Position { get; set; }
    public float Scale { get; }
}

public sealed class PdlGroup
{
    public PdlGroup(int index, IReadOnlyList<PdlInstance> instances)
    {
        Index = index;
        Instances = instances;
    }

    public int Index { get; }
    public IReadOnlyList<PdlInstance> Instances { get; }
    public Vector3 Centroid => Instances.Count == 0
        ? Vector3.Zero
        : new Vector3(
            (float)Instances.Average(instance => (double)instance.Position.X),
            (float)Instances.Average(instance => (double)instance.Position.Y),
            (float)Instances.Average(instance => (double)instance.Position.Z));
}

public sealed class PdlDocument
{
    public const int HeaderSize = 0x40;
    public const int RecordSize = 0x10;
    private readonly byte[] _sourceBytes;

    private PdlDocument(byte[] sourceBytes, IReadOnlyList<PdlGroup> groups)
    {
        _sourceBytes = sourceBytes;
        Groups = groups;
    }

    public IReadOnlyList<PdlGroup> Groups { get; }

    public static PdlDocument Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize || (bytes.Length - HeaderSize) % RecordSize != 0)
            throw new InvalidDataException("PDL size is not a 0x40-byte header followed by 0x10-byte records.");

        var totalRecords = (bytes.Length - HeaderSize) / RecordSize;
        var counts = new List<int>();
        var countedRecords = 0;
        for (var index = 0; index < HeaderSize / 4; index++)
        {
            var count = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(index * 4, 4));
            if (count == 0)
                break;
            if (count < 0 || count > totalRecords - countedRecords)
                throw new InvalidDataException($"PDL group {index + 1} has an invalid record count of {count}.");
            counts.Add(count);
            countedRecords += count;
        }
        if (counts.Count == 0 || countedRecords != totalRecords)
            throw new InvalidDataException($"PDL group counts describe {countedRecords} of {totalRecords} records.");

        var source = bytes.ToArray();
        var groups = new List<PdlGroup>(counts.Count);
        var row = 0;
        for (var groupIndex = 0; groupIndex < counts.Count; groupIndex++)
        {
            var instances = new List<PdlInstance>(counts[groupIndex]);
            for (var localIndex = 0; localIndex < counts[groupIndex]; localIndex++, row++)
            {
                var offset = HeaderSize + row * RecordSize;
                instances.Add(new PdlInstance(
                    new Vector3(
                        ReadSingle(source, offset),
                        ReadSingle(source, offset + 4),
                        ReadSingle(source, offset + 8)),
                    ReadSingle(source, offset + 12)));
            }
            groups.Add(new PdlGroup(groupIndex, instances));
        }
        return new PdlDocument(source, groups);
    }

    public byte[] WritePositions()
    {
        var output = (byte[])_sourceBytes.Clone();
        var row = 0;
        foreach (var group in Groups)
        {
            foreach (var instance in group.Instances)
            {
                var offset = HeaderSize + row * RecordSize;
                WriteSingle(output, offset, instance.Position.X);
                WriteSingle(output, offset + 4, instance.Position.Y);
                WriteSingle(output, offset + 8, instance.Position.Z);
                row++;
            }
        }
        return output;
    }

    private static float ReadSingle(ReadOnlySpan<byte> bytes, int offset) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(offset, 4)));

    private static void WriteSingle(Span<byte> bytes, int offset, float value) =>
        BinaryPrimitives.WriteInt32BigEndian(bytes.Slice(offset, 4), BitConverter.SingleToInt32Bits(value));
}
