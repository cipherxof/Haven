using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Formats.Gcx;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Editors.GcxEditing;

public sealed record GcxCameraReference(GcxScript Script, string ScriptName, int TableIndex, int RowIndex,
    Vector3 Position, Vector3 Target, int Extra);

public static class GcxCameraWriter
{
    public static IReadOnlyList<GcxCameraReference> ScanDocument(Gcx document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var cameras = new List<GcxCameraReference>();
        Add(document.MainScript, "main");
        for (var index = 0; index < document.ScriptDefinitions.Count; index++)
            if (document.ScriptDefinitions[index].Script is { } script)
                Add(script, $"proc{index + 1}");
        return cameras;

        void Add(GcxScript script, string name)
        {
            var tables = Scan(script.Bytes);
            for (var table = 0; table < tables.Count; table++)
                for (var row = 0; row < tables[table].Count; row++)
                    cameras.Add(new GcxCameraReference(script, name, table, row,
                        Vector(tables[table], row * 7), Vector(tables[table], row * 7 + 3),
                        tables[table].Literals[row * 7 + 6].Value));
        }
    }

    public static IReadOnlyList<GcxCameraTableSite> Scan(byte[] bytes)
    {
        var sites = new List<GcxCameraTableSite>();
        GcxDecompiler.Decompile(bytes, "camera scan", cameraSites: sites);
        return sites;
    }

    public static Vector3 Vector(GcxCameraTableSite site, int index) => new(
        site.Literals[index].Value, site.Literals[index+1].Value, site.Literals[index+2].Value);

    public static byte[] Write(byte[] bytes, int tableIndex, int rowIndex, Vector3 eye, Vector3 target)
    {
        var sites = Scan(bytes);
        if (tableIndex < 0 || tableIndex >= sites.Count || rowIndex < 0 || rowIndex >= sites[tableIndex].Count)
            throw new InvalidDataException("The camera table is no longer present. Reload the map.");
        var site = sites[tableIndex];
        var values = new[] { eye.X, eye.Y, eye.Z, target.X, target.Y, target.Z }
            .Select(value => !float.IsFinite(value) || value < int.MinValue || (double)value > int.MaxValue
                ? throw new InvalidDataException("Camera coordinates must be finite 32-bit integers.")
                : checked((int)MathF.Round(value))).ToArray();
        if (values.Take(3).SequenceEqual(values.Skip(3))) throw new InvalidDataException("The camera and look-at point must differ after rounding.");
        var literals = site.Literals.Skip(rowIndex*7).Take(6).ToArray();
        if (literals.Select(l => l.Value).SequenceEqual(values)) return bytes.ToArray();
        var patched = bytes.ToArray();
        bool fits = literals.Select((literal, i) => Fits(literal, values[i])).All(v => v);
        if (fits && !site.NeedsLengthRepair)
        {
            for (int i = 0; i < 6; i++) Patch(patched, literals[i], values[i]);
            return patched;
        }

        var root = Block(bytes, 0, 0x80);
        var command = Block(bytes, site.CommandOffset, 0x60);
        var declaredParameter = Block(bytes, site.ParameterOffset, 0x50);
        var parameter = (Start: declaredParameter.Start, End: site.ParameterOffset+site.ParameterLength);
        // Do not invalidate live placement bindings after this command. Retail JJ/VV
        // put this table last; unsupported mixed layouts remain safely read-only on resize.
        if (bytes.AsSpan(command.End, root.End-command.End).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Resizing this camera table would shift later commands. Use coordinates within the existing literal widths.");
        byte[] payload = bytes[parameter.Start..parameter.End];
        for (int i = 5; i >= 0; i--)
            payload = Replace(payload, literals[i].Offset-parameter.Start, literals[i].Width,
                GcxCommandBuilder.Int32LiteralBytes(values[i]));
        var parameterBytes = Encode(bytes[site.ParameterOffset], payload);
        var commandPayload = Replace(bytes[command.Start..command.End], site.ParameterOffset-command.Start,
            site.ParameterLength, parameterBytes);
        var commandBytes = Encode(bytes[site.CommandOffset], commandPayload);
        var rootPayload = Replace(bytes[root.Start..root.End], site.CommandOffset-root.Start,
            site.CommandLength, commandBytes);
        var rootBytes = Encode(bytes[0], rootPayload);
        if (rootBytes.Length-rootPayload.Length != root.Start)
            throw new InvalidDataException("Resizing would change the procedure header width.");
        var result = Replace(bytes, 0, root.End, rootBytes);
        var verified = Scan(result);
        if (verified.Count != sites.Count || verified[tableIndex].Count != site.Count)
            throw new InvalidDataException("Camera table validation failed.");
        return result;
    }

    private static bool Fits(GcxLiteralSite s, int v) => s.Encoding switch
    {
        GcxLiteralEncoding.PackedNumber => v >= -1 && v <= 62,
        GcxLiteralEncoding.Int16 => v >= short.MinValue && v <= short.MaxValue,
        GcxLiteralEncoding.Int32 => true,
        GcxLiteralEncoding.UInt8 => v >= 0 && v <= byte.MaxValue,
        GcxLiteralEncoding.UInt16 => v >= 0 && v <= ushort.MaxValue,
        _ => false
    };
    private static void Patch(byte[] b, GcxLiteralSite s, int v)
    {
        if (s.Encoding == GcxLiteralEncoding.PackedNumber) b[s.Offset] = (byte)(0xC0 | (v+1));
        else if (s.Encoding == GcxLiteralEncoding.UInt8) b[s.Offset+1] = (byte)v;
        else if (s.Encoding == GcxLiteralEncoding.UInt16) BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(s.Offset+1,2), (ushort)v);
        else if (s.Encoding == GcxLiteralEncoding.Int16) BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(s.Offset+1,2), (short)v);
        else BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(s.Offset+1,4), v);
    }
    private static byte[] Replace(byte[] b, int offset, int count, byte[] replacement)
    {
        using var stream = new MemoryStream();
        stream.Write(b.AsSpan(0,offset)); stream.Write(replacement); stream.Write(b.AsSpan(offset+count));
        return stream.ToArray();
    }
    private static (int Start, int End) Block(byte[] b, int offset, int tag)
    {
        if (offset < 0 || offset >= b.Length || (b[offset]&0xF0) != tag) throw new InvalidDataException("Invalid GCX block.");
        int code = b[offset]&15, prefix = code <= 12 ? 1 : code == 13 ? 2 : code == 14 ? 3 : 0;
        if (prefix == 0 || offset+prefix > b.Length) throw new InvalidDataException("Invalid GCX prefix.");
        int size = code <= 12 ? code : code == 13 ? b[offset+1] : BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(offset+1,2));
        if (offset+prefix+size > b.Length) throw new InvalidDataException("Truncated GCX block.");
        return (offset+prefix, offset+prefix+size);
    }
    private static byte[] Encode(byte tag, byte[] payload)
    {
        int code = (tag&15) == 14 || payload.Length > 255 ? 14 : (tag&15) == 13 || payload.Length > 12 ? 13 : payload.Length;
        if (payload.Length > ushort.MaxValue) throw new InvalidDataException("Camera block too large.");
        int prefix = code == 14 ? 3 : code == 13 ? 2 : 1;
        var b = new byte[prefix+payload.Length]; b[0] = (byte)((tag&0xF0)|code);
        if (code == 14) BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(1,2), (ushort)payload.Length);
        else if (code == 13) b[1] = (byte)payload.Length;
        payload.CopyTo(b,prefix); return b;
    }
}
