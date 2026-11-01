using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Formats.Gcx;
using HavenStudio.Utils;

namespace HavenStudio.Editors.GcxEditing;

/// <summary>
/// Rewrites the literal area_max argument in an SDM wrapper call. The writer
/// identifies the value through its immediately preceding prop_dir string code
/// and refuses ambiguous layouts rather than patching a coincidental number.
/// </summary>
public static class GcxSdmAreaWriter
{
    public static byte[] Write(
        byte[] script,
        uint propertyDirectoryHash,
        int expectedMaximum,
        int newMaximum)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (newMaximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newMaximum),
                "The SDM starting radius must be positive.");
        }

        var site = FindSingleSite(script, propertyDirectoryHash, expectedMaximum);
        if (site.Value == newMaximum)
        {
            return script.ToArray();
        }

        var patched = script.ToArray();
        if (Fits(site, newMaximum))
        {
            Patch(patched, site, newMaximum);
        }
        else
        {
            var root = ReadBlock(script, 0, 0x80);
            if (site.Offset < root.Start || site.Offset + site.Width > root.End)
            {
                throw new InvalidDataException("The SDM area literal is outside the procedure body.");
            }

            var replacement = GcxCommandBuilder.Int32LiteralBytes(newMaximum);
            var payload = Replace(
                script[root.Start..root.End],
                site.Offset - root.Start,
                site.Width,
                replacement);
            var rootBytes = Encode(script[0], payload);
            patched = Replace(script, 0, root.End, rootBytes);
        }

        _ = FindSingleSite(patched, propertyDirectoryHash, newMaximum);
        return patched;
    }

    private static GcxLiteralSite FindSingleSite(
        byte[] script,
        uint propertyDirectoryHash,
        int expectedMaximum)
    {
        var prefix = new byte[]
        {
            0x06,
            (byte)(propertyDirectoryHash & 0xFF),
            (byte)((propertyDirectoryHash >> 8) & 0xFF),
            (byte)((propertyDirectoryHash >> 16) & 0xFF)
        };
        var matches = new List<GcxLiteralSite>();
        for (var offset = 0; offset <= script.Length - prefix.Length; offset++)
        {
            if (!script.AsSpan(offset, prefix.Length).SequenceEqual(prefix))
            {
                continue;
            }
            if (TryReadLiteral(script, offset + prefix.Length, out var literal) &&
                literal.Value == expectedMaximum)
            {
                matches.Add(literal);
            }
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidDataException(
                "The writable SDM area_max argument is no longer present. Reload the map."),
            _ => throw new InvalidDataException(
                "More than one matching SDM area_max argument was found; no bytes were changed.")
        };
    }

    private static bool TryReadLiteral(byte[] bytes, int offset, out GcxLiteralSite site)
    {
        site = null!;
        if (offset < 0 || offset >= bytes.Length)
        {
            return false;
        }

        var tag = bytes[offset];
        if (tag >= 0xC0)
        {
            site = new GcxLiteralSite(offset, 1, GcxLiteralEncoding.PackedNumber, (tag & 63) - 1);
            return true;
        }
        if (tag == 1 && offset + 3 <= bytes.Length)
        {
            site = new GcxLiteralSite(offset, 3, GcxLiteralEncoding.Int16,
                BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 1, 2)));
            return true;
        }
        if (tag == 2 && offset + 2 <= bytes.Length)
        {
            site = new GcxLiteralSite(offset, 2, GcxLiteralEncoding.UInt8, bytes[offset + 1]);
            return true;
        }
        if (tag == 8 && offset + 3 <= bytes.Length)
        {
            site = new GcxLiteralSite(offset, 3, GcxLiteralEncoding.UInt16,
                BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 1, 2)));
            return true;
        }
        if (tag == 9 && offset + 5 <= bytes.Length)
        {
            site = new GcxLiteralSite(offset, 5, GcxLiteralEncoding.Int32,
                BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 1, 4)));
            return true;
        }
        return false;
    }

    private static bool Fits(GcxLiteralSite site, int value) => site.Encoding switch
    {
        GcxLiteralEncoding.PackedNumber => value is >= -1 and <= 62,
        GcxLiteralEncoding.Int16 => value is >= short.MinValue and <= short.MaxValue,
        GcxLiteralEncoding.Int32 => true,
        GcxLiteralEncoding.UInt8 => value is >= 0 and <= byte.MaxValue,
        GcxLiteralEncoding.UInt16 => value is >= 0 and <= ushort.MaxValue,
        _ => false
    };

    private static void Patch(byte[] bytes, GcxLiteralSite site, int value)
    {
        switch (site.Encoding)
        {
            case GcxLiteralEncoding.PackedNumber:
                bytes[site.Offset] = (byte)(0xC0 | (value + 1));
                break;
            case GcxLiteralEncoding.Int16:
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(site.Offset + 1, 2), (short)value);
                break;
            case GcxLiteralEncoding.Int32:
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(site.Offset + 1, 4), value);
                break;
            case GcxLiteralEncoding.UInt8:
                bytes[site.Offset + 1] = (byte)value;
                break;
            case GcxLiteralEncoding.UInt16:
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(site.Offset + 1, 2), (ushort)value);
                break;
            default:
                throw new InvalidDataException("Unsupported GCX literal encoding.");
        }
    }

    private static (int Start, int End) ReadBlock(byte[] bytes, int offset, int expectedTag)
    {
        if (offset < 0 || offset >= bytes.Length || (bytes[offset] & 0xF0) != expectedTag)
        {
            throw new InvalidDataException("Invalid GCX procedure block.");
        }
        var code = bytes[offset] & 0x0F;
        var prefixLength = code <= 12 ? 1 : code == 13 ? 2 : code == 14 ? 3 : 0;
        if (prefixLength == 0 || offset + prefixLength > bytes.Length)
        {
            throw new InvalidDataException("Invalid GCX block prefix.");
        }
        var payloadLength = code <= 12
            ? code
            : code == 13
                ? bytes[offset + 1]
                : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 1, 2));
        var start = offset + prefixLength;
        var end = start + payloadLength;
        if (end > bytes.Length)
        {
            throw new InvalidDataException("Truncated GCX procedure block.");
        }
        return (start, end);
    }

    private static byte[] Replace(byte[] bytes, int offset, int count, byte[] replacement)
    {
        using var stream = new MemoryStream();
        stream.Write(bytes.AsSpan(0, offset));
        stream.Write(replacement);
        stream.Write(bytes.AsSpan(offset + count));
        return stream.ToArray();
    }

    private static byte[] Encode(byte originalTag, byte[] payload)
    {
        var oldCode = originalTag & 0x0F;
        var code = oldCode == 14 || payload.Length > byte.MaxValue
            ? 14
            : oldCode == 13 || payload.Length > 12
                ? 13
                : payload.Length;
        if (payload.Length > ushort.MaxValue)
        {
            throw new InvalidDataException("GCX procedure block is too large.");
        }
        var prefixLength = code == 14 ? 3 : code == 13 ? 2 : 1;
        var result = new byte[prefixLength + payload.Length];
        result[0] = (byte)((originalTag & 0xF0) | code);
        if (code == 14)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(1, 2), (ushort)payload.Length);
        }
        else if (code == 13)
        {
            result[1] = (byte)payload.Length;
        }
        payload.CopyTo(result, prefixLength);
        return result;
    }
}
