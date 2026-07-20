using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Geo;

public static class GeoEffectChunkPatcher
{
    // The engine uses a signed 16-bit game angle: 0x4000 is 90 degrees and
    // the full unsigned 16-bit range represents one turn.
    private const float AngleToInt16 = 32768f / MathF.PI;

    public static void Patch(byte[] chunk, IEnumerable<GeoEffect> effects, Endianness endianness)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(effects);

        foreach (var effect in Flatten(effects))
        {
            var positionSlot = GeoEffectLayout.GetPositionSlot(effect.Index);
            if (positionSlot != 0)
            {
                var positionOffset = GeoEffectLayout.GetPositionOffset(effect);
                EnsureRange(chunk, positionOffset, 0x10, effect);
                WriteSingle(chunk, positionOffset, effect.X, endianness);
                WriteSingle(chunk, positionOffset + 4, effect.Y, endianness);
                WriteSingle(chunk, positionOffset + 8, effect.Z, endianness);
                WriteSingle(chunk, positionOffset + 12, effect.W, endianness);
            }

            var rotationSlot = GeoEffectLayout.GetRotationSlot(effect.Index);
            if (rotationSlot == 0)
            {
                continue;
            }

            var rotationOffset = GeoEffectLayout.GetRotationOffset(effect);
            EnsureRange(chunk, rotationOffset, 6, effect);
            WriteInt16(chunk, rotationOffset, EncodeAngle(effect.RotationX), endianness);
            WriteInt16(chunk, rotationOffset + 2, EncodeAngle(effect.RotationY), endianness);
            WriteInt16(chunk, rotationOffset + 4, EncodeAngle(effect.RotationZ), endianness);
        }
    }

    public static float DecodeAngle(short value)
    {
        return value * MathF.PI / 32768f;
    }

    public static short EncodeAngle(float value)
    {
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException("GEOM effect rotation must be a finite value.");
        }

        var normalized = value % (2f * MathF.PI);
        if (normalized >= MathF.PI)
        {
            normalized -= 2f * MathF.PI;
        }
        else if (normalized < -MathF.PI)
        {
            normalized += 2f * MathF.PI;
        }

        var scaled = MathF.Round(normalized * AngleToInt16);
        return scaled >= 32768f ? short.MinValue : checked((short)scaled);
    }

    private static void WriteSingle(byte[] chunk, int offset, float value, Endianness endianness)
    {
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException("GEOM effect position must be a finite value.");
        }

        var bits = BitConverter.SingleToInt32Bits(value);
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(offset, 4), bits);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(chunk.AsSpan(offset, 4), bits);
        }
    }

    private static void WriteInt16(byte[] chunk, int offset, short value, Endianness endianness)
    {
        if (endianness == Endianness.Big)
        {
            BinaryPrimitives.WriteInt16BigEndian(chunk.AsSpan(offset, 2), value);
        }
        else
        {
            BinaryPrimitives.WriteInt16LittleEndian(chunk.AsSpan(offset, 2), value);
        }
    }

    private static void EnsureRange(byte[] chunk, int offset, int length, GeoEffect effect)
    {
        if (offset < 0 || length < 0 || offset > chunk.Length - length)
        {
            throw new InvalidDataException(
                $"GEOM effect 0x{unchecked((uint)effect.Name):X8} points outside chunk 6 at 0x{offset:X}.");
        }
    }

    private static IEnumerable<GeoEffect> Flatten(IEnumerable<GeoEffect> effects)
    {
        foreach (var effect in effects)
        {
            yield return effect;
            foreach (var child in Flatten(effect.Children))
            {
                yield return child;
            }
        }
    }
}
