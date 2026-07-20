using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Dar;

public class Dar
{
    public List<DarEntry> Entries { get; } = new();

    public void ReadFrom(EndianBinaryReader r)
    {
        Entries.Clear();

        if (!r.BaseStream.CanSeek)
        {
            throw new ArgumentException("DAR reading requires a seekable stream.", nameof(r));
        }

        if (r.BaseStream.Length - r.BaseStream.Position < 4)
        {
            throw new InvalidDataException("DAR header is truncated.");
        }

        int numFiles = r.ReadInt32();
        if (numFiles < 0 || numFiles > r.BaseStream.Length / 5)
        {
            throw new InvalidDataException($"DAR contains an invalid entry count of {numFiles}.");
        }

        for (int i = 0; i < numFiles; i++)
        {
            string filename = ReadFilename(r, i);
            
            AlignAndRequire(r, 4, sizeof(int), $"DAR size field for entry {i}");
            int length = r.ReadInt32();
            if (length < 0)
            {
                throw new InvalidDataException($"DAR entry {i} has a negative size.");
            }
            
            AlignAndRequire(r, 16, checked((long)length + 1), $"DAR payload for entry {i}");
            var bytes = new byte[length];
            r.ReadExactly(bytes);
            
            _ = r.ReadByte();

            Entries.Add(new DarEntry(filename, bytes));
        }
    }

    private static string ReadFilename(EndianBinaryReader reader, int entryIndex)
    {
        using var bytes = new MemoryStream();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var value = reader.ReadByte();
            if (value == 0)
            {
                return Encoding.Latin1.GetString(bytes.ToArray());
            }

            bytes.WriteByte(value);
        }

        throw new InvalidDataException($"DAR filename for entry {entryIndex} is not null-terminated.");
    }

    private static void AlignAndRequire(
        EndianBinaryReader reader,
        int alignment,
        long requiredBytes,
        string context)
    {
        var position = reader.BaseStream.Position;
        var alignedPosition = checked((position + alignment - 1) / alignment * alignment);
        if (alignedPosition > reader.BaseStream.Length || requiredBytes > reader.BaseStream.Length - alignedPosition)
        {
            throw new InvalidDataException($"{context} at 0x{alignedPosition:X} is truncated.");
        }

        reader.BaseStream.Position = alignedPosition;
    }

    public void WriteTo(EndianBinaryWriter w)
    {
        w.WriteInt32(Entries.Count);

        foreach (var entry in Entries)
        {
            w.WriteCString(entry.Filename, Encoding.Latin1);

            var bytes = entry.Bytes ?? Array.Empty<byte>();
            
            w.Align(4);
            w.WriteInt32(bytes.Length);
            
            w.Align(16);
            w.BaseStream.Write(bytes, 0, bytes.Length);
            
            w.Write((byte)0x00);
        }
    }
}
