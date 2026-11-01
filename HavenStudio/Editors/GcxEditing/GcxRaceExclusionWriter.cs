using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HavenStudio.Editors.GcxEditing;

/// <summary>Replaces the twelve numeric A52C7F exclusion rows in a flat procedure.
/// Does not compile decompiled text or alter other commands. Unsupported layouts fail closed.</summary>
public static class GcxRaceExclusionWriter
{
    private static ReadOnlySpan<byte> CommandHeader => [0xC9,0x2B,0x08,0x04,0x06,0x7F,0x2C,0xA5];

    public static byte[] Replace(byte[] script, IReadOnlyList<IReadOnlyList<int>> exclusions)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(exclusions);
        if (exclusions.Count != 12 || exclusions.Any(r => r == null || r.Count == 0 || r.Count > 12
            || r.Any(i => i < 0 || i >= 12) || r.Distinct().Count() != r.Count))
            throw new ArgumentException("Expected 12 non-empty exclusion lists of distinct indices 0..11.", nameof(exclusions));
        var root = Block(script, 0, 0x80);
        var seen = new HashSet<int>();
        using var body = new MemoryStream();
        int position = root.Start;
        while (position < root.End)
        {
            if (script[position] == 0)
            {
                if (script.AsSpan(position, root.End-position).ContainsAnyExcept((byte)0))
                    throw new InvalidDataException("Unexpected nonzero content after procedure terminator.");
                body.Write(script.AsSpan(position, root.End-position));
                break;
            }
            var command = Block(script, position, 0x60);
            if (command.End > root.End) throw new InvalidDataException("Command exceeds procedure.");
            if (!script.AsSpan(command.Start, command.End-command.Start).StartsWith(CommandHeader))
                body.Write(script.AsSpan(position, command.End-position));
            else
            {
                var baseParam = Block(script, command.Start+CommandHeader.Length, 0x50);
                if (baseParam.End-baseParam.Start != 5
                    || !script.AsSpan(baseParam.Start,4).SequenceEqual(new byte[] {0x62,0xC5,0x92,0x32}))
                    throw new InvalidDataException("Unexpected RACE base parameter.");
                int index = Literal(script[baseParam.Start+4]);
                if (index < 0 || index >= 12 || !seen.Add(index))
                    throw new InvalidDataException("Duplicate or invalid RACE base index.");
                var link = Block(script, baseParam.End, 0x50);
                if (link.End-link.Start < 5 || link.End != command.End-1 || script[link.End] != 0
                    || !script.AsSpan(link.Start,4).SequenceEqual(new byte[] {0x6C,0x2B,0xB2,0x37}))
                    throw new InvalidDataException("Unexpected RACE link parameter or command trailer.");
                foreach (byte value in script.AsSpan(link.Start+4, link.End-link.Start-4))
                    if (Literal(value) is < 0 or >= 12) throw new InvalidDataException("Invalid old exclusion index.");
                using var parameter = new MemoryStream();
                parameter.Write(script.AsSpan(link.Start,4));
                foreach (int value in exclusions[index]) parameter.WriteByte((byte)(0xC0 | (value+1)));
                using var payload = new MemoryStream();
                payload.Write(script.AsSpan(command.Start, baseParam.End-command.Start));
                payload.Write(Encode(script[baseParam.End], parameter.ToArray()));
                payload.WriteByte(0);
                body.Write(Encode(script[position], payload.ToArray()));
            }
            position = command.End;
        }
        if (seen.Count != 12) throw new InvalidDataException($"Expected 12 RACE rows, found {seen.Count}.");
        using var output = new MemoryStream();
        output.Write(Encode(script[0], body.ToArray()));
        output.Write(script.AsSpan(root.End)); // preserve physical script padding
        return output.ToArray();
    }

    private static int Literal(byte value) => value >= 0xC0 ? (value & 63)-1
        : throw new InvalidDataException("Expected a packed numeric literal.");

    private static (int Start, int End) Block(byte[] bytes, int offset, int tag)
    {
        if (offset < 0 || offset >= bytes.Length || (bytes[offset]&0xF0) != tag)
            throw new InvalidDataException("Unexpected GCX block tag or offset.");
        int code = bytes[offset]&15, prefix = code <= 12 ? 1 : code == 13 ? 2 : code == 14 ? 3 : 0;
        if (prefix == 0 || offset+prefix > bytes.Length) throw new InvalidDataException("Invalid GCX block prefix.");
        int length = code <= 12 ? code : code == 13 ? bytes[offset+1]
            : BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset+1,2));
        int start = offset+prefix, end = start+length;
        if (end > bytes.Length) throw new InvalidDataException("GCX block exceeds script.");
        return (start,end);
    }

    private static byte[] Encode(byte originalTag, byte[] payload)
    {
        // Preserve extended prefix width, promote short forms only when necessary.
        int old = originalTag&15;
        int code = old == 14 || payload.Length > 255 ? 14 : old == 13 || payload.Length > 12 ? 13 : payload.Length;
        if (payload.Length > ushort.MaxValue) throw new InvalidDataException("GCX block too large.");
        int prefix = code == 14 ? 3 : code == 13 ? 2 : 1;
        var result = new byte[prefix+payload.Length];
        result[0] = (byte)((originalTag&0xF0)|code);
        if (code == 14) BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(1,2),(ushort)payload.Length);
        else if (code == 13) result[1] = (byte)payload.Length;
        payload.CopyTo(result, prefix);
        return result;
    }
}
