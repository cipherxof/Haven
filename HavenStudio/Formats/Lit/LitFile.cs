using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Extensions;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Lit;

public sealed class LitFile
{
    private const int DefinitionSize = 32;
    private const int PrefixSize = 16;
    private const int GroupSize = 48;
    private const int VariantExtraSize = 16;
    private const int MaximumGroupCount = 100_000;

    public LitVariant Variant { get; set; }
    public bool BigEndian { get; set; }
    public byte[] Prefix { get; set; } = [];
    public Vector4 Direction { get; set; }
    public LitColor Color { get; set; }
    public LitColor Ambient { get; set; }
    public uint HeaderPad { get; set; }
    public List<LitGroup> Groups { get; } = [];

    public static LitFile Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("LIT reading requires a readable stream.", nameof(stream));
        }

        byte[] data;
        try
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            data = copy.ToArray();
        }
        catch (Exception exception) when (exception is not InvalidDataException)
        {
            throw new InvalidDataException("Could not read the LIT stream.", exception);
        }

        if (data.Length < DefinitionSize)
        {
            throw new InvalidDataException("LIT data is truncated before its 32-byte definition header.");
        }

        var candidates = new List<Candidate>();
        AddCandidate(LitVariant.Raw, Endianness.Little);
        AddCandidate(LitVariant.Raw, Endianness.Big);
        if (data.Length >= PrefixSize + DefinitionSize)
        {
            AddCandidate(LitVariant.Prefixed, Endianness.Little);
            AddCandidate(LitVariant.Prefixed, Endianness.Big);
        }

        var best = candidates.OrderByDescending(candidate => candidate.Score).FirstOrDefault();
        if (best == null || best.Score < 8)
        {
            throw new InvalidDataException("Data does not contain a structurally valid LIT definition.");
        }

        try
        {
            return Parse(data, best.Variant, best.Endianness);
        }
        catch (Exception exception) when (exception is EndOfStreamException or OverflowException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException("LIT data is truncated or contains an invalid offset.", exception);
        }

        void AddCandidate(LitVariant variant, Endianness endianness)
        {
            var candidate = ScoreCandidate(data, variant, endianness);
            if (candidate != null)
            {
                candidates.Add(candidate);
            }
        }
    }

    public static bool TryRead(Stream stream, out LitFile? file, out string? error)
    {
        try
        {
            file = Read(stream);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            file = null;
            error = exception.Message;
            return false;
        }
    }

    public void Write(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite)
        {
            throw new ArgumentException("LIT writing requires a writable stream.", nameof(stream));
        }

        ValidateDocument();
        var endianness = BigEndian ? Endianness.Big : Endianness.Little;
        using var writer = new EndianBinaryWriter(stream, endianness, leaveOpen: true);

        if (Variant == LitVariant.Prefixed)
        {
            if (Prefix.Length != PrefixSize)
            {
                throw new InvalidDataException("Prefixed LIT files require exactly 16 prefix bytes.");
            }

            writer.Write(Prefix);
        }

        writer.Write(Direction);
        WriteColor(writer, Color);
        WriteColor(writer, Ambient);
        writer.Write(checked((uint)Groups.Count));
        writer.Write(HeaderPad);

        var recordOffset = checked((uint)((Variant == LitVariant.Prefixed ? PrefixSize : 0) +
            DefinitionSize + Groups.Count * GroupSize));
        foreach (var group in Groups)
        {
            group.LitOffset = recordOffset;
            recordOffset = checked(recordOffset + (uint)group.Lights.Sum(GetWrittenSize));

            writer.Write(group.BoundsMax);
            writer.Write(group.BoundsMin);
            writer.Write(checked((uint)group.Lights.Count));
            writer.Write(group.Type);
            writer.Write(group.LitOffset);
            writer.Write(group.Pad);
        }

        foreach (var group in Groups)
        {
            foreach (var light in group.Lights)
            {
                WriteLight(writer, group.Type, light);
            }
        }
    }

    public byte[] ToArray()
    {
        using var stream = new MemoryStream();
        Write(stream);
        return stream.ToArray();
    }

    public static int GetRecordStride(uint type, LitVariant variant) => type switch
    {
        1 => 32 + (variant == LitVariant.Prefixed ? VariantExtraSize : 0),
        2 or 4 => 80 + (variant == LitVariant.Prefixed ? VariantExtraSize : 0),
        8 or 16 or 32 => 64 + (variant == LitVariant.Prefixed ? VariantExtraSize : 0),
        // Real MGS4 files use 160-byte type-64 records, while the early importer
        // fixture used 352. Infer this raw record's stride from adjacent offsets.
        64 when variant == LitVariant.Prefixed => 0,
        _ => 0
    };

    private static Candidate? ScoreCandidate(byte[] data, LitVariant variant, Endianness endianness)
    {
        var definitionOffset = variant == LitVariant.Prefixed ? PrefixSize : 0;
        if (data.Length < definitionOffset + DefinitionSize)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(data, writable: false);
            using var reader = new EndianBinaryReader(stream, endianness, leaveOpen: true);
            stream.Position = definitionOffset;
            var direction = reader.ReadVector4();
            reader.ReadBytes(8);
            var groupCount = reader.ReadUInt32();
            reader.ReadUInt32();
            if (groupCount > MaximumGroupCount)
            {
                return null;
            }

            var tableEnd = checked((long)definitionOffset + DefinitionSize + groupCount * GroupSize);
            if (tableEnd > data.Length)
            {
                return null;
            }

            var directionLength = new Vector3(direction.X, direction.Y, direction.Z).Length;
            if (!float.IsFinite(directionLength) || directionLength < 0.01f || directionLength > 10f)
            {
                return null;
            }

            var score = MathF.Abs(directionLength - 1f) < 0.05f ? 8 : 2;
            score += groupCount == 0 ? 4 : 1;
            var previousOffset = tableEnd;
            for (var index = 0u; index < groupCount; index++)
            {
                stream.Position = checked(definitionOffset + DefinitionSize + index * GroupSize + 32);
                var count = reader.ReadUInt32();
                var type = reader.ReadUInt32();
                var offset = reader.ReadUInt32();
                reader.ReadUInt32();
                if (count > 1_000_000 || offset < tableEnd || offset > data.Length || offset < previousOffset)
                {
                    return null;
                }

                var stride = GetRecordStride(type, variant);
                if (stride > 0 && checked((long)offset + (long)count * stride) > data.Length)
                {
                    return null;
                }

                previousOffset = offset;
                score += stride > 0 ? 2 : 1;
            }

            return new Candidate(variant, endianness, score);
        }
        catch (Exception exception) when (exception is EndOfStreamException or OverflowException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static LitFile Parse(byte[] data, LitVariant variant, Endianness endianness)
    {
        var definitionOffset = variant == LitVariant.Prefixed ? PrefixSize : 0;
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new EndianBinaryReader(stream, endianness, leaveOpen: true);
        var file = new LitFile
        {
            Variant = variant,
            BigEndian = endianness == Endianness.Big,
            Prefix = variant == LitVariant.Prefixed ? data[..PrefixSize] : [],
        };

        stream.Position = definitionOffset;
        file.Direction = reader.ReadVector4();
        file.Color = ReadColor(reader);
        file.Ambient = ReadColor(reader);
        var groupCount = reader.ReadUInt32();
        file.HeaderPad = reader.ReadUInt32();

        var descriptors = new List<GroupDescriptor>(checked((int)groupCount));
        for (var index = 0; index < groupCount; index++)
        {
            var group = new LitGroup
            {
                BoundsMax = reader.ReadVector4(),
                BoundsMin = reader.ReadVector4()
            };
            var count = reader.ReadUInt32();
            group.Type = reader.ReadUInt32();
            group.LitOffset = reader.ReadUInt32();
            group.Pad = reader.ReadUInt32();
            descriptors.Add(new GroupDescriptor(group, count));
            file.Groups.Add(group);
        }

        for (var index = 0; index < descriptors.Count; index++)
        {
            var descriptor = descriptors[index];
            if (descriptor.Count == 0)
            {
                continue;
            }

            stream.Position = descriptor.Group.LitOffset;
            var stride = GetRecordStride(descriptor.Group.Type, variant);
            if (stride == 0)
            {
                var end = index + 1 < descriptors.Count
                    ? descriptors.Skip(index + 1).Select(item => (long)item.Group.LitOffset)
                        .FirstOrDefault(offset => offset > descriptor.Group.LitOffset)
                    : data.LongLength;
                if (end == 0)
                {
                    end = data.LongLength;
                }

                var byteCount = end - descriptor.Group.LitOffset;
                if (byteCount < 0 || byteCount % descriptor.Count != 0)
                {
                    throw new InvalidDataException(
                        $"LIT group {index} type {descriptor.Group.Type} has no inferable record stride.");
                }

                stride = checked((int)(byteCount / descriptor.Count));
            }

            if (checked((long)descriptor.Group.LitOffset + (long)descriptor.Count * stride) > data.Length)
            {
                throw new InvalidDataException($"LIT group {index} records extend beyond the file.");
            }

            for (var recordIndex = 0u; recordIndex < descriptor.Count; recordIndex++)
            {
                descriptor.Group.Lights.Add(ReadLight(reader, descriptor.Group.Type, variant, stride));
            }
        }

        return file;
    }

    private static LitLight ReadLight(
        EndianBinaryReader reader,
        uint type,
        LitVariant variant,
        int stride)
    {
        if (type == 64 || GetRecordStride(type, variant) == 0)
        {
            return new LitRawLight(ReadExactBytes(reader, stride));
        }

        LitLight light;
        switch (type)
        {
            case 1:
            {
                var point = new LitPointLight
                {
                    Point = reader.ReadVector4(),
                    Color = ReadColor(reader),
                    Range = reader.ReadSingle()
                };
                if (variant == LitVariant.Prefixed)
                {
                    var prefix = ReadExactBytes(reader, 4);
                    point.ExtendedRange = reader.ReadSingle();
                    point.Flag = reader.ReadUInt32();
                    point.VariantExtra = CombineExtra(prefix, ReadExactBytes(reader, 12));
                }
                else
                {
                    point.ExtendedRange = reader.ReadSingle();
                    point.Flag = reader.ReadUInt32();
                }
                light = point;
                break;
            }
            case 2:
            {
                var spot = new LitSpotLight
                {
                    BoundsMax = reader.ReadVector4(),
                    BoundsMin = reader.ReadVector4(),
                    Point = reader.ReadVector4(),
                    Direction = reader.ReadVector4(),
                    Color = ReadColor(reader),
                    Umbra = reader.ReadSingle(),
                    Penumbra = reader.ReadSingle()
                };
                if (variant == LitVariant.Prefixed)
                {
                    var prefix = ReadExactBytes(reader, 4);
                    spot.Flag = reader.ReadUInt32();
                    spot.VariantExtra = CombineExtra(prefix, ReadExactBytes(reader, 12));
                }
                else
                {
                    spot.Flag = reader.ReadUInt32();
                }
                light = spot;
                break;
            }
            case 4:
            {
                var line = new LitLineLight
                {
                    BoundsMax = reader.ReadVector4(),
                    BoundsMin = reader.ReadVector4(),
                    Point = reader.ReadVector4(),
                    Direction = reader.ReadVector4(),
                    Color = ReadColor(reader),
                    Range = reader.ReadSingle(),
                    Pad = reader.ReadUInt32()
                };
                if (variant == LitVariant.Prefixed)
                {
                    var prefix = ReadExactBytes(reader, 4);
                    line.Flag = reader.ReadUInt32();
                    line.VariantExtra = CombineExtra(prefix, ReadExactBytes(reader, 12));
                }
                else
                {
                    line.Flag = reader.ReadUInt32();
                }
                light = line;
                break;
            }
            case 8:
            case 16:
            {
                var blackPoint = new LitBlackPoint
                {
                    BoundsMax = reader.ReadVector4(),
                    BoundsMin = reader.ReadVector4(),
                    Point = reader.ReadVector4()
                };
                if (variant == LitVariant.Prefixed)
                {
                    blackPoint.Pad0 = reader.ReadUInt32();
                    blackPoint.Pad1 = reader.ReadUInt32();
                    var prefix = ReadExactBytes(reader, 4);
                    blackPoint.Range = reader.ReadSingle();
                    blackPoint.Flag = reader.ReadUInt32();
                    blackPoint.VariantExtra = CombineExtra(prefix, ReadExactBytes(reader, 12));
                }
                else
                {
                    blackPoint.Range = reader.ReadSingle();
                    blackPoint.Flag = reader.ReadUInt32();
                    blackPoint.Pad0 = reader.ReadUInt32();
                    blackPoint.Pad1 = reader.ReadUInt32();
                }
                light = blackPoint;
                break;
            }
            case 32:
            {
                var parallel = new LitParallelLight
                {
                    BoundsMax = reader.ReadVector4(),
                    BoundsMin = reader.ReadVector4(),
                    Direction = reader.ReadVector4(),
                    Color = ReadColor(reader),
                    Ambient = ReadColor(reader),
                    Force = reader.ReadSingle()
                };
                if (variant == LitVariant.Prefixed)
                {
                    var prefix = ReadExactBytes(reader, 4);
                    parallel.Flag = reader.ReadUInt32();
                    parallel.VariantExtra = CombineExtra(prefix, ReadExactBytes(reader, 12));
                }
                else
                {
                    parallel.Flag = reader.ReadUInt32();
                }
                light = parallel;
                break;
            }
            default:
                throw new InvalidDataException($"Unsupported typed LIT record type {type}.");
        }

        return light;
    }

    private void WriteLight(EndianBinaryWriter writer, uint type, LitLight light)
    {
        if (light is LitRawLight raw)
        {
            writer.Write(raw.Data);
            return;
        }

        switch (type, light)
        {
            case (1, LitPointLight point):
                writer.Write(point.Point);
                WriteColor(writer, point.Color);
                writer.Write(point.Range);
                WriteVariantExtraPrefix(writer, point, 4);
                writer.Write(point.ExtendedRange);
                writer.Write(point.Flag);
                WriteVariantExtraSuffix(writer, point, 4);
                break;
            case (2, LitSpotLight spot):
                writer.Write(spot.BoundsMax);
                writer.Write(spot.BoundsMin);
                writer.Write(spot.Point);
                writer.Write(spot.Direction);
                WriteColor(writer, spot.Color);
                writer.Write(spot.Umbra);
                writer.Write(spot.Penumbra);
                WriteVariantExtraPrefix(writer, spot, 4);
                writer.Write(spot.Flag);
                WriteVariantExtraSuffix(writer, spot, 4);
                break;
            case (4, LitLineLight line):
                writer.Write(line.BoundsMax);
                writer.Write(line.BoundsMin);
                writer.Write(line.Point);
                writer.Write(line.Direction);
                WriteColor(writer, line.Color);
                writer.Write(line.Range);
                writer.Write(line.Pad);
                WriteVariantExtraPrefix(writer, line, 4);
                writer.Write(line.Flag);
                WriteVariantExtraSuffix(writer, line, 4);
                break;
            case (8 or 16, LitBlackPoint blackPoint):
                writer.Write(blackPoint.BoundsMax);
                writer.Write(blackPoint.BoundsMin);
                writer.Write(blackPoint.Point);
                if (Variant == LitVariant.Prefixed)
                {
                    writer.Write(blackPoint.Pad0);
                    writer.Write(blackPoint.Pad1);
                    WriteVariantExtraPrefix(writer, blackPoint, 4);
                    writer.Write(blackPoint.Range);
                    writer.Write(blackPoint.Flag);
                    WriteVariantExtraSuffix(writer, blackPoint, 4);
                }
                else
                {
                    writer.Write(blackPoint.Range);
                    writer.Write(blackPoint.Flag);
                    writer.Write(blackPoint.Pad0);
                    writer.Write(blackPoint.Pad1);
                }
                break;
            case (32, LitParallelLight parallel):
                writer.Write(parallel.BoundsMax);
                writer.Write(parallel.BoundsMin);
                writer.Write(parallel.Direction);
                WriteColor(writer, parallel.Color);
                WriteColor(writer, parallel.Ambient);
                writer.Write(parallel.Force);
                WriteVariantExtraPrefix(writer, parallel, 4);
                writer.Write(parallel.Flag);
                WriteVariantExtraSuffix(writer, parallel, 4);
                break;
            default:
                throw new InvalidDataException(
                    $"LIT group type {type} cannot contain {light.GetType().Name} records.");
        }

    }

    private void ValidateDocument()
    {
        if (Groups.Count > MaximumGroupCount)
        {
            throw new InvalidDataException($"LIT group count exceeds {MaximumGroupCount}.");
        }

        foreach (var group in Groups)
        {
            foreach (var light in group.Lights)
            {
                if (light is LitRawLight raw)
                {
                    if (raw.Data.Length == 0)
                    {
                        throw new InvalidDataException("Raw LIT records cannot be empty.");
                    }

                    continue;
                }

                var expected = group.Type switch
                {
                    1 => typeof(LitPointLight),
                    2 => typeof(LitSpotLight),
                    4 => typeof(LitLineLight),
                    8 or 16 => typeof(LitBlackPoint),
                    32 => typeof(LitParallelLight),
                    _ => null
                };
                if (expected == null || light.GetType() != expected)
                {
                    throw new InvalidDataException(
                        $"LIT group type {group.Type} cannot contain {light.GetType().Name} records.");
                }
            }
        }
    }

    private int GetWrittenSize(LitLight light)
    {
        if (light is LitRawLight raw)
        {
            return raw.Data.Length;
        }

        return light switch
        {
            LitPointLight => GetRecordStride(1, Variant),
            LitSpotLight => GetRecordStride(2, Variant),
            LitLineLight => GetRecordStride(4, Variant),
            LitBlackPoint => GetRecordStride(8, Variant),
            LitParallelLight => GetRecordStride(32, Variant),
            _ => throw new InvalidDataException($"Unsupported LIT record {light.GetType().Name}.")
        };
    }

    private static LitColor ReadColor(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length != 4)
        {
            throw new EndOfStreamException();
        }

        return new LitColor(bytes[0], bytes[1], bytes[2], bytes[3]);
    }

    private static void WriteColor(BinaryWriter writer, LitColor color)
    {
        writer.Write(color.R);
        writer.Write(color.G);
        writer.Write(color.B);
        writer.Write(color.A);
    }

    private static byte[] ReadExactBytes(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
        {
            throw new EndOfStreamException();
        }

        return bytes;
    }

    private static byte[] CombineExtra(byte[] prefix, byte[] suffix)
    {
        var combined = new byte[prefix.Length + suffix.Length];
        prefix.CopyTo(combined, 0);
        suffix.CopyTo(combined, prefix.Length);
        return combined;
    }

    private void WriteVariantExtraPrefix(EndianBinaryWriter writer, LitLight light, int count)
    {
        if (Variant != LitVariant.Prefixed)
        {
            return;
        }
        ValidateVariantExtra(light);
        writer.Write(light.VariantExtra[..count]);
    }

    private void WriteVariantExtraSuffix(EndianBinaryWriter writer, LitLight light, int offset)
    {
        if (Variant != LitVariant.Prefixed)
        {
            return;
        }
        ValidateVariantExtra(light);
        writer.Write(light.VariantExtra[offset..]);
    }

    private static void ValidateVariantExtra(LitLight light)
    {
        if (light.VariantExtra.Length != VariantExtraSize)
        {
            throw new InvalidDataException("Prefixed typed LIT records require 16 preserved extra bytes.");
        }
    }

    private sealed record Candidate(LitVariant Variant, Endianness Endianness, int Score);
    private sealed record GroupDescriptor(LitGroup Group, uint Count);
}
