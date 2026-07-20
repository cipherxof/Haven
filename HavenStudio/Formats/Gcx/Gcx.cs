using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Gcx;

public class Gcx
{
    public int Timestamp { get; set; }
    public int CryptoSeed { get; set; }

    /// <summary>
    /// Exact marker bytes between the string and script sections. A null value requests
    /// canonical padding when a newly constructed document is written.
    /// </summary>
    public byte[]? StringSectionPadding { get; set; }

    public List<GcxScriptDefinition> ScriptDefinitions { get; } = new();
    public List<GcxStringDefinition> StringDefinitions { get; } = new();

    public GcxScript MainScript { get; set; } = new(Array.Empty<byte>());

    public void ReadHeaderFrom(EndianBinaryReader r)
    {
        Timestamp = r.ReadInt32(); 
    }

    public void WriteHeaderTo(EndianBinaryWriter w)
    {
        w.WriteInt32(Timestamp);
    }

    public void ReadScriptDefinitionsFrom(EndianBinaryReader r)
    {
        ScriptDefinitions.Clear();

        while (true)
        {
            if (r.BaseStream.Length - r.BaseStream.Position < sizeof(int))
            {
                throw new InvalidDataException("GCX script-definition table is not terminated.");
            }

            int def = r.ReadInt32();
            if (def == unchecked((int)0xFFFF_FFFF))
                break;

            ScriptDefinitions.Add(GcxScriptDefinition.FromPacked(def));
        }
    }

    public void WriteScriptDefinitionsPlaceholderTo(EndianBinaryWriter w)
    {
        w.WriteZero(ScriptDefinitions.Count * 4);
        w.WriteInt32(unchecked((int)0xFFFF_FFFF));
    }

    public void ReadStringDefinitionsFrom(EndianBinaryReader r, GcxHeader header)
    {
        StringDefinitions.Clear();

        int tableLength = header.StringSectionOffset - header.StringDefsOffset;
        if (tableLength < 0 || tableLength % sizeof(int) != 0)
        {
            throw new InvalidDataException("GCX string-definition table has an invalid length.");
        }

        int numDefs = tableLength / sizeof(int);
        for (int i = 0; i < numDefs; i++)
        {
            int packed = r.ReadInt32();
            var def = GcxStringDefinition.FromPacked(packed);
            StringDefinitions.Add(def);
        }
    }

    public void WriteStringDefinitionsPlaceholderTo(EndianBinaryWriter w)
    {
        w.WriteZero(StringDefinitions.Count * 4);
    }

    public void ResolveStringSection(byte[] stringSection, GcxHeader header, ReadOnlySpan<byte> paddingBytes)
    {
        var stringSectionLength = header.ScriptSectionOffset - header.StringSectionOffset;
        var paddingLength = GetPad(stringSection, paddingBytes);
        var contentLength = stringSectionLength - paddingLength;
        StringSectionPadding = stringSection.AsSpan(contentLength, paddingLength).ToArray();

        for (int i = 0; i < StringDefinitions.Count; i++)
        {
            var def = StringDefinitions[i];
            var next = (i + 1 < StringDefinitions.Count) ? StringDefinitions[i + 1] : null;

            int offset = def.Offset;
            int length;

            if (next != null)
            {
                length = next.Offset - offset;
            }
            else
            {
                length = contentLength - offset;
            }

            if (offset < 0 || length < 0 || offset > stringSection.Length - length)
            {
                throw new InvalidDataException(
                    $"GCX string definition {i} references invalid range 0x{offset:X}+0x{length:X}.");
            }

            def.Index = i;

            if (def.Type == 0x80)
            {
                def.Value = Encoding.UTF8.GetString(stringSection, offset, length);
            }
            else
            {
                var bytes = new byte[length];
                Buffer.BlockCopy(stringSection, offset, bytes, 0, length);
                def.Script = new GcxScript(bytes);
            }
        }
    }

    public void WriteStringSectionTo(EndianBinaryWriter w, long stringSectionPos)
    {
        for (int i = 0; i < StringDefinitions.Count; i++)
        {
            var def = StringDefinitions[i];

            int offset = checked((int)(w.BaseStream.Position - stringSectionPos));
            def.Offset = offset;

            if (def.Type == 0x80)
            {
                if (def.Value is null) def.Value = string.Empty;
                var bytes = Encoding.UTF8.GetBytes(def.Value);
                w.BaseStream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                if (def.Script is null) def.Script = new GcxScript(Array.Empty<byte>());
                var bytes = def.Script.Bytes;
                w.BaseStream.Write(bytes, 0, bytes.Length);
            }
        }
    }

    public void ReadNormalScriptsFrom(EndianBinaryReader r)
    {
        int scriptSectionLength = r.ReadInt32();
        if (scriptSectionLength < 0 || scriptSectionLength > r.BaseStream.Length - r.BaseStream.Position - sizeof(int))
        {
            throw new InvalidDataException($"GCX script section has invalid length {scriptSectionLength}.");
        }

        var scriptSectionPosition = r.BaseStream.Position;
        var physicalOffsets = ScriptDefinitions
            .Select(definition => definition.Offset)
            .Distinct()
            .OrderBy(offset => offset)
            .ToArray();

        if (physicalOffsets.Length > 0 && physicalOffsets[0] != 0)
        {
            throw new InvalidDataException("GCX script data must start at offset zero.");
        }

        for (int i = 0; i < ScriptDefinitions.Count; i++)
        {
            var def = ScriptDefinitions[i];
            var physicalIndex = Array.BinarySearch(physicalOffsets, def.Offset);
            var endOffset = physicalIndex + 1 < physicalOffsets.Length
                ? physicalOffsets[physicalIndex + 1]
                : scriptSectionLength;
            var length = endOffset - def.Offset;

            if (def.Offset < 0 || length < 0 || def.Offset > scriptSectionLength - length)
            {
                throw new InvalidDataException(
                    $"GCX script definition {i} references invalid range 0x{def.Offset:X}+0x{length:X}.");
            }

            byte[] bytes = new byte[length];
            r.BaseStream.Position = checked(scriptSectionPosition + def.Offset);
            r.ReadExactly(bytes);
            def.Script = new GcxScript(bytes);
            def.PhysicalOrder = physicalIndex;
        }

        r.BaseStream.Position = checked(scriptSectionPosition + scriptSectionLength);
    }

    public void WriteNormalScriptsTo(EndianBinaryWriter w, long scriptSectionPos)
    {
        var orderedDefinitions = ScriptDefinitions
            .Select((definition, index) => new
            {
                Definition = definition,
                TableIndex = index,
                OriginalPhysicalOrder = definition.PhysicalOrder
            })
            .OrderBy(item => item.OriginalPhysicalOrder < 0 ? int.MaxValue : item.OriginalPhysicalOrder)
            .ThenBy(item => item.TableIndex)
            .ToArray();

        var itemIndex = 0;
        var physicalOrder = 0;
        while (itemIndex < orderedDefinitions.Length)
        {
            var originalPhysicalOrder = orderedDefinitions[itemIndex].OriginalPhysicalOrder;
            var groupEnd = itemIndex + 1;
            if (originalPhysicalOrder >= 0)
            {
                while (groupEnd < orderedDefinitions.Length &&
                       orderedDefinitions[groupEnd].OriginalPhysicalOrder == originalPhysicalOrder)
                {
                    groupEnd++;
                }
            }

            var firstBytes = orderedDefinitions[itemIndex].Definition.Script?.Bytes ?? Array.Empty<byte>();
            var sharesPhysicalScript = true;
            for (var i = itemIndex + 1; i < groupEnd; i++)
            {
                var bytes = orderedDefinitions[i].Definition.Script?.Bytes ?? Array.Empty<byte>();
                if (!firstBytes.AsSpan().SequenceEqual(bytes))
                {
                    sharesPhysicalScript = false;
                    break;
                }
            }

            if (sharesPhysicalScript)
            {
                var offset = checked((int)(w.BaseStream.Position - scriptSectionPos));
                for (var i = itemIndex; i < groupEnd; i++)
                {
                    orderedDefinitions[i].Definition.Offset = offset;
                    orderedDefinitions[i].Definition.PhysicalOrder = physicalOrder;
                }

                w.BaseStream.Write(firstBytes, 0, firstBytes.Length);
                physicalOrder++;
            }
            else
            {
                for (var i = itemIndex; i < groupEnd; i++)
                {
                    var definition = orderedDefinitions[i].Definition;
                    var bytes = definition.Script?.Bytes ?? Array.Empty<byte>();
                    definition.Offset = checked((int)(w.BaseStream.Position - scriptSectionPos));
                    definition.PhysicalOrder = physicalOrder++;
                    w.BaseStream.Write(bytes, 0, bytes.Length);
                }
            }

            itemIndex = groupEnd;
        }
    }

    public void ReadMainScriptFrom(EndianBinaryReader r)
    {
        if (r.BaseStream.Length - r.BaseStream.Position < sizeof(int))
        {
            throw new InvalidDataException("GCX main-script length is missing.");
        }

        int mainLen = r.ReadInt32();
        if (mainLen < 0 || mainLen > r.BaseStream.Length - r.BaseStream.Position)
        {
            throw new InvalidDataException($"GCX main script has invalid length {mainLen}.");
        }

        byte[] bytes = new byte[mainLen];
        r.ReadExactly(bytes);
        MainScript = new GcxScript(bytes);
    }

    public void WriteMainScriptTo(EndianBinaryWriter w)
    {
        var bytes = MainScript?.Bytes ?? Array.Empty<byte>();
        w.WriteInt32(bytes.Length);
        w.BaseStream.Write(bytes, 0, bytes.Length);
    }

    private static int GetPad(ReadOnlySpan<byte> section, ReadOnlySpan<byte> padBytes)
    {
        for (var length = Math.Min(section.Length, padBytes.Length); length > 0; length--)
        {
            if (section[^length..].SequenceEqual(padBytes[..length])) return length;
        }

        return 0;
    }
}
