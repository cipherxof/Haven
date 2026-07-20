using System;
using System.Collections.Generic;
using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Gcx;

public static class GcxFile
{
    private static ReadOnlySpan<byte> StringSectionPaddingPattern => [0x46, 0x4F, 0x4E];

    public static Gcx Read(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException("GCX reading requires a seekable stream.", nameof(stream));
        if (stream.Length - stream.Position < 0x24) throw new InvalidDataException("GCX file is truncated.");

        var r = new EndianBinaryReader(stream, Endianness.Little, leaveOpen: true);

        var gcx = new Gcx();
        gcx.ReadHeaderFrom(r);

        gcx.ReadScriptDefinitionsFrom(r);
        var headerPosition = r.BaseStream.Position;
        if (r.BaseStream.Length - headerPosition < 0x14)
        {
            throw new InvalidDataException("GCX header is truncated.");
        }

        var header = GcxHeader.ReadFrom(r);
        ValidateHeader(header, headerPosition, r.BaseStream.Length);

        gcx.CryptoSeed = header.CryptoSeed;
        r.BaseStream.Position = headerPosition + header.StringDefsOffset;
        gcx.ReadStringDefinitionsFrom(r, header);
        r.BaseStream.Position = headerPosition + header.StringSectionOffset;
        var stringSection = ReadStringSection(r, header, gcx.CryptoSeed);
        gcx.ResolveStringSection(stringSection, header, StringSectionPaddingPattern);
        r.BaseStream.Position = headerPosition + header.ScriptSectionOffset;
        gcx.ReadNormalScriptsFrom(r);
        gcx.ReadMainScriptFrom(r);

        return gcx;
    }

    private static void ValidateHeader(GcxHeader header, long headerPosition, long streamLength)
    {
        if (header.StringDefsOffset < 0x14 ||
            header.StringSectionOffset < header.StringDefsOffset ||
            header.ScriptSectionOffset < header.StringSectionOffset ||
            header.ScriptSectionOffsetDuplicate != header.ScriptSectionOffset)
        {
            throw new InvalidDataException("GCX header contains inconsistent section offsets.");
        }

        var stringDefinitionsPosition = checked(headerPosition + header.StringDefsOffset);
        var stringSectionPosition = checked(headerPosition + header.StringSectionOffset);
        var scriptSectionPosition = checked(headerPosition + header.ScriptSectionOffset);
        if (stringDefinitionsPosition > streamLength ||
            stringSectionPosition > streamLength ||
            scriptSectionPosition > streamLength - sizeof(int))
        {
            throw new InvalidDataException("GCX section offset is outside the file.");
        }
    }

    public static void Write(Stream stream, Gcx gcx)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException("GCX writing requires a seekable stream for patching offsets.", nameof(stream));
        if (gcx is null) throw new ArgumentNullException(nameof(gcx));

        var w = new EndianBinaryWriter(stream, Endianness.Little, leaveOpen: true);
        
        gcx.WriteHeaderTo(w);
        
        var scriptDefsPos = w.BaseStream.Position;
        gcx.WriteScriptDefinitionsPlaceholderTo(w);
        
        var headerPos = w.BaseStream.Position;
        var header = new GcxHeader();
        header.CryptoSeed = gcx.CryptoSeed;
        header.WritePlaceholderTo(w);
        
        var stringDefsPos = w.BaseStream.Position;
        header.StringDefsOffset = (int)(stringDefsPos - headerPos);
        gcx.WriteStringDefinitionsPlaceholderTo(w);
        
        var stringSectionPos = w.BaseStream.Position;
        header.StringSectionOffset = (int)(stringSectionPos - headerPos);
        
        var stringSection = BuildStringSection(gcx, stringSectionPos);
        w.BaseStream.Write(stringSection, 0, stringSection.Length);

        var scriptSectionLengthPos = w.BaseStream.Position;
        header.ScriptSectionOffset = (int)(scriptSectionLengthPos - headerPos);
        w.WriteInt32(0); // placeholder
        
        var scriptSectionPos = w.BaseStream.Position;
        gcx.WriteNormalScriptsTo(w, scriptSectionPos);

        var scriptSectionLength = (int)(w.BaseStream.Position - scriptSectionPos);
        gcx.WriteMainScriptTo(w);
        
        PatchScriptDefTable(w, scriptDefsPos, gcx.ScriptDefinitions);
        PatchStringDefTable(w, stringDefsPos, gcx.StringDefinitions);
        
        var end = w.BaseStream.Position;
        w.BaseStream.Position = headerPos;
        header.WriteTo(w);
        w.BaseStream.Position = end;
        
        end = w.BaseStream.Position;
        w.BaseStream.Position = scriptSectionLengthPos;
        w.WriteInt32(scriptSectionLength);
        w.BaseStream.Position = end;
    }

    private static byte[] BuildStringSection(Gcx gcx, long stringSectionPosition)
    {
        using var stream = new MemoryStream();
        using (var writer = new EndianBinaryWriter(stream, Endianness.Little, leaveOpen: true))
        {
            gcx.WriteStringSectionTo(writer, 0);
            writer.Flush();
        }

        var padding = gcx.StringSectionPadding;
        if (padding is null)
        {
            var isMgs3 = SettingsStore.Current.IsMgs3;
            var paddingLength = isMgs3
                ? 0
                : (int)((4 - ((stringSectionPosition + stream.Length) % 4)) % 4);
            padding = StringSectionPaddingPattern[..paddingLength].ToArray();
            gcx.StringSectionPadding = padding;
        }
        else if (padding.Length > StringSectionPaddingPattern.Length ||
                 !padding.AsSpan().SequenceEqual(StringSectionPaddingPattern[..padding.Length]))
        {
            throw new InvalidOperationException("GCX string-section padding must be a prefix of the FON marker.");
        }

        stream.Write(padding, 0, padding.Length);
        var bytes = stream.ToArray();
        if (gcx.CryptoSeed == 0)
        {
            return bytes;
        }

        var cipher = new GcxCipher(gcx.CryptoSeed);
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = cipher.Encrypt(bytes[i]);
        }

        return bytes;
    }

    private static void PatchScriptDefTable(EndianBinaryWriter w, long scriptDefsPos,
        IReadOnlyList<GcxScriptDefinition> defs)
    {
        var end = w.BaseStream.Position;
        w.BaseStream.Position = scriptDefsPos;

        foreach (var t in defs)
        {
            var def = ((t.Type & 0xFF) << 24) | (t.Offset & 0x00FF_FFFF);
            w.WriteInt32(def);
        }
        
        w.WriteInt32(unchecked((int)0xFFFF_FFFF));

        w.BaseStream.Position = end;
    }

    private static void PatchStringDefTable(EndianBinaryWriter w, long stringDefsPos, IReadOnlyList<GcxStringDefinition> defs)
    {
        var end = w.BaseStream.Position;
        w.BaseStream.Position = stringDefsPos;

        foreach (var t in defs)
        {
            var def = ((t.Type & 0xFF) << 24) | (t.Offset & 0x00FF_FFFF);
            w.WriteInt32(def);
        }

        w.BaseStream.Position = end;
    }

    private static byte[] ReadStringSection(EndianBinaryReader r, GcxHeader header, int cryptoSeed)
    {
        int stringSectionLength = header.ScriptSectionOffset - header.StringSectionOffset;
        
        byte[] buf = new byte[stringSectionLength];

        if (cryptoSeed != 0)
        {
            var cipher = new GcxCipher(cryptoSeed);
            for (int i = 0; i < buf.Length; i++)
                buf[i] = cipher.Decrypt(r.ReadByte());
        }
        else
        {
            r.ReadExactly(buf);
        }

        return buf;
    }
    
    public static string ToJson(Stream gcxStream)
    {
        var gcx = GcxFile.Read(gcxStream);
        var dto = GcxJsonConverter.ToJsonModel(gcx);
        return GcxJsonIO.Serialize(dto);
    }
    
    public static Gcx FromJson(string json)
    {
        var dto = GcxJsonIO.Deserialize(json);
        return GcxJsonConverter.FromJsonModel(dto);
    }
}
