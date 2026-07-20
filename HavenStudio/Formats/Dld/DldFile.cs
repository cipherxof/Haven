using System;
using System.Collections.Generic;
using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Dld;

public enum DldPriority
{
    Main = 0,
    Mipmaps = 3,
}

public class DldFile
{
    public List<DldTexture> Textures = new List<DldTexture>();
    public readonly string Name = "";
    public readonly string Filename = "";

    public DldFile()
    {
    }

    public DldFile(string path)
        : this(path, EndianBinaryReader.DefaultEndianness)
    {
    }

    public DldFile(string path, Endianness endianness)
    {
        Name = Path.GetFileName(path);
        Filename = path;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        ReadFromStream(stream, endianness);
    }

    public DldFile(Stream stream, Endianness endianness)
    {
        ReadFromStream(stream, endianness);
    }

    public void Save(string path, Endianness endianness)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Save(stream, endianness);
    }

    public void Save(Stream stream, Endianness endianness)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite || !stream.CanSeek)
            throw new ArgumentException("DLD writing requires a writable, seekable stream.", nameof(stream));

        stream.SetLength(0);
        stream.Position = 0;
        using var writer = new EndianBinaryWriter(stream, endianness, leaveOpen: true);
        foreach (var texture in Textures)
        {
            texture.WriteTo(writer);
        }

        writer.Flush();
    }

    private void ReadFromStream(Stream stream, Endianness endianness)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("DLD reading requires a seekable stream.", nameof(stream));
        }

        using var reader = new EndianBinaryReader(stream, endianness, leaveOpen: true);
        while (stream.Position < stream.Length)
        {
            var alignedPosition = checked((stream.Position + 0xF) & ~0xFL);
            if (alignedPosition > stream.Length || stream.Length - alignedPosition < 0x20)
            {
                break;
            }

            stream.Position = alignedPosition;
            var texture = new DldTexture(reader);
            if (texture.Type != 0)
            {
                Textures.Add(texture);
            }
        }
    }

    public DldTexture? FindTexture(uint objectId, int index, DldPriority prio)
    {
        for (int i = 0; i < Textures.Count; i++)
        {
            var texture = Textures[i];

            if (texture.HashId == objectId && texture.EntryNumber == index && texture.Priority == (byte)prio)
            {
                return texture;
            }
        }

        return null;
    }

    public bool RemoveTexture(DldTexture texture)
    {
        for (int i = 0; i < Textures.Count; i++)
        {
            if (Textures[i] == texture)
            {
                for (int n = 0; n < Textures.Count; n++)
                {
                    if (Textures[n].HashId == texture.HashId && Textures[n].EntryNumber > texture.EntryNumber)
                    {
                        Textures[n].EntryNumber -= 1;
                    }
                }
            }
        }

        return Textures.Remove(texture);
    }
}
