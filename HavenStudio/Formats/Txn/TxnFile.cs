using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Txn;

public class TxnFile
{
    public TxnHeader Header = new();
    public readonly string Path;
    public List<TxnImage> Images = new List<TxnImage>();
    public List<TxnInfo> ImageInfo = new List<TxnInfo>();

    public readonly Dictionary<TxnInfo, int> IndexLookup = new Dictionary<TxnInfo, int>();

    public TxnFile()
    {
        Path = "";
    }

    public TxnFile(string path)
        : this(path, EndianBinaryReader.DefaultEndianness)
    {
    }

    public TxnFile(string path, Endianness endianness)
    {
        Path = path;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        ReadFromStream(stream, endianness);
    }

    public TxnFile(Stream stream)
        : this(stream, EndianBinaryReader.DefaultEndianness)
    {
    }

    public TxnFile(Stream stream, Endianness endianness)
    {
        Path = "";
        ReadFromStream(stream, endianness);
    }

    private void ReadFromStream(Stream stream, Endianness endianness)
    {
        if (!stream.CanSeek)
        {
            throw new ArgumentException("TXN reading requires a seekable stream.", nameof(stream));
        }

        if (stream.Length - stream.Position < 0x20)
        {
            throw new InvalidDataException("TXN header is truncated.");
        }

        using var reader = new EndianBinaryReader(stream, endianness, leaveOpen: true);
        Header = new TxnHeader(reader);

        if (Header.NullBytes != 0) return;
        ValidateTable(Header.IndexOffset, Header.TextureCount, 0x10, stream.Length, "image");
        ValidateTable(Header.IndexOffset2, Header.TextureCount2, 0x30, stream.Length, "metadata");
        var offsets = new List<uint>();

        stream.Seek(Header.IndexOffset, SeekOrigin.Begin);

        for (var i = 0; i < Header.TextureCount; i++)
        {
            offsets.Add((uint)stream.Position);
            Images.Add(new TxnImage(reader));
        }

        stream.Seek(Header.IndexOffset2, SeekOrigin.Begin);

        for (var i = 0; i < Header.TextureCount2; i++)
        {
            var index2 = new TxnInfo(reader);
            var index1 = offsets.FindIndex(offset => offset == index2.TxnImageOffset);
            IndexLookup[index2] = index1;
            ImageInfo.Add(index2);
        }
    }

    public int GetIndex(TxnInfo index2)
    {
        IndexLookup.TryGetValue(index2, out var result);
        return result;
    }

    public List<TxnInfo> GetIndex2List(int index1)
    {
        var result = new List<TxnInfo>();

        if (Images.Count <= index1)
        {
            return result;
        }

        var offset = 0x20 + (index1 * 0x10);

        result.AddRange(ImageInfo.Where(t => offset == t.TxnImageOffset));

        return result;
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
            throw new ArgumentException("TXN writing requires a writable, seekable stream.", nameof(stream));

        stream.SetLength(0);
        stream.Position = 0;
        using var writer = new EndianBinaryWriter(stream, endianness, leaveOpen: true);

        Header.TextureCount = (uint)Images.Count;
        Header.TextureCount2 = (uint)ImageInfo.Count;
        Header.WriteTo(writer);

        var offsets = new uint[Images.Count];

        Header.IndexOffset = (uint)stream.Position;
        for (var i = 0; i < Images.Count; i++)
        {
            offsets[i] = (uint)stream.Position;
            Images[i].WriteTo(writer);
        }

        Header.IndexOffset2 = (uint)stream.Position;
        for (var i = 0; i < ImageInfo.Count; i++)
        {
            var index1 = GetIndex(ImageInfo[i]);
            if (index1 >= 0 && index1 < offsets.Length)
            {
                ImageInfo[i].TxnImageOffset = offsets[index1];
            }

            ImageInfo[i].WriteTo(writer);
        }

        writer.Align(0x80);

        stream.Seek(0, SeekOrigin.Begin);
        Header.WriteTo(writer);
        writer.Flush();
    }

    private static void ValidateTable(uint offset, uint count, int entrySize, long streamLength, string tableName)
    {
        var end = checked((ulong)offset + ((ulong)count * (uint)entrySize));
        if (offset < 0x20 || end > (ulong)streamLength)
        {
            throw new InvalidDataException(
                $"TXN {tableName} table at 0x{offset:X} with {count} entries is outside the file.");
        }
    }

    public void RebuildIndexLookup()
    {
        IndexLookup.Clear();

        for (int i = 0; i < ImageInfo.Count; i++)
        {
            var info = ImageInfo[i];
            int imageIndex = Images.FindIndex(image => image.Offset == info.TxnImageOffset);
            if (imageIndex < 0)
            {
                imageIndex = Math.Clamp(i, 0, Images.Count - 1);
            }

            IndexLookup[info] = imageIndex;
        }
    }

    /*public void CreateDdsFromIndex(string filename, int indexNumber, DldTexture? texture, DldTexture? mips)
    {
        if (texture == null)
        {
            return;
        }

        var index = Images[indexNumber];
        var data = texture.Data;

        if (mips != null)
        {
            data = data.Concat(mips.Data).ToArray();
        }

        int mipMapCount = mips == null ? 0 : (int)Math.Log2(Math.Max(index.Height, index.Width));

        DdsFile.Create(filename, index.Height, index.Width, index.FourCC == 11 ? "DXT5" : "DXT1", mipMapCount, data);
    }*/

}
