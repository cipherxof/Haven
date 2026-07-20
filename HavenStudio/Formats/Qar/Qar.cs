using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Qar;

public class Qar
{
    public List<QarEntry> Entries { get; } = new();

    public void ReadFrom(EndianBinaryReader r)
    {
        Entries.Clear();
        
        long fileLen = r.BaseStream.Length;
        if (fileLen < 4) throw new InvalidDataException("QAR too small.");

        long endPos = r.BaseStream.Position;

        r.BaseStream.Position = fileLen - 4;
        int headerIndex = r.ReadInt32();
        if (headerIndex < 0 || headerIndex > fileLen - 8)
        {
            throw new InvalidDataException($"QAR header offset 0x{headerIndex:X} is outside the file.");
        }
        
        r.BaseStream.Position = headerIndex;
        
        int fileCount = r.ReadInt16();
        if (fileCount < 0)
        {
            throw new InvalidDataException($"QAR contains an invalid entry count of {fileCount}.");
        }

        r.Skip(2);

        var tableEnd = checked((long)headerIndex + 4 + ((long)fileCount * 8));
        if (tableEnd > fileLen - 4)
        {
            throw new InvalidDataException("QAR entry table is truncated.");
        }

        var sizes = new int[fileCount];
        long totalDataSize = 0;
        for (int i = 0; i < fileCount; i++)
        {
            var e = new QarEntry();
            e.Info = r.ReadInt32();
            sizes[i] = r.ReadInt32();
            if (sizes[i] < 0)
            {
                throw new InvalidDataException($"QAR entry {i} has a negative size.");
            }

            totalDataSize = checked(totalDataSize + sizes[i]);
            if (totalDataSize > headerIndex)
            {
                throw new InvalidDataException("QAR entry data extends into the header.");
            }

            Entries.Add(e);
        }
        
        for (int i = 0; i < fileCount; i++)
        {
            Entries[i].Filename = ReadFilename(r, fileLen - 4, i);
        }
        
        r.BaseStream.Position = 0;
        for (int i = 0; i < fileCount; i++)
        {
            int size = sizes[i];
            var data = new byte[size];
            r.ReadExactly(data);
            Entries[i].Data = data;
        }
        
        r.BaseStream.Position = endPos;
    }

    private static string ReadFilename(EndianBinaryReader reader, long tableLimit, int entryIndex)
    {
        using var bytes = new MemoryStream();
        while (reader.BaseStream.Position < tableLimit)
        {
            var value = reader.ReadByte();
            if (value == 0)
            {
                return Encoding.Latin1.GetString(bytes.ToArray());
            }

            bytes.WriteByte(value);
        }

        throw new InvalidDataException($"QAR filename for entry {entryIndex} is not null-terminated.");
    }

    public void WriteTo(EndianBinaryWriter w)
    {
        foreach (var e in Entries)
        {
            var data = e.Data ?? Array.Empty<byte>();
            w.BaseStream.Write(data, 0, data.Length);
        }
        
        int headerIndex = checked((int)w.BaseStream.Position);
        
        if (Entries.Count > short.MaxValue) throw new InvalidOperationException("Too many QAR entries.");
        w.WriteInt16((short)Entries.Count);
        w.WriteInt16(0); // pad
        
        foreach (var e in Entries)
        {
            w.WriteInt32(e.Info);
            w.WriteInt32((e.Data ?? Array.Empty<byte>()).Length);
        }

        foreach (var e in Entries)
        {
            w.WriteCString(e.Filename ?? string.Empty, Encoding.Latin1);
        }

        var footerPadding = (4 - (w.BaseStream.Position % 4)) % 4;
        if (footerPadding > 0)
        {
            w.Write(new byte[footerPadding]);
        }

        w.WriteInt32(headerIndex);
    }
}
