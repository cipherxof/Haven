using System;
using System.IO;
using System.Text;

namespace HavenStudio.Formats.Dds;

public static class DdsFile
{
    public static DdsTextureData Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static DdsTextureData Read(Stream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException("DDS reading requires a seekable stream.", nameof(stream));
        if (stream.Length - stream.Position < 128) throw new InvalidDataException("DDS header is truncated.");

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        var magic = reader.ReadUInt32();
        if (magic != 0x20534444) // "DDS "
        {
            throw new InvalidDataException("Not a DDS file.");
        }

        var headerSize = reader.ReadUInt32();
        if (headerSize != 124)
        {
            throw new InvalidDataException("Unsupported DDS header size.");
        }

        reader.ReadUInt32(); // flags
        int height = (int)reader.ReadUInt32();
        int width = (int)reader.ReadUInt32();
        reader.ReadUInt32(); // pitch/linear size
        reader.ReadUInt32(); // depth
        int mipMapCount = (int)reader.ReadUInt32();
        if (mipMapCount <= 0)
        {
            mipMapCount = 1;
        }

        for (int i = 0; i < 11; i++)
        {
            reader.ReadUInt32();
        }

        var pixelFormatSize = reader.ReadUInt32();
        if (pixelFormatSize != 32)
        {
            throw new InvalidDataException("Unsupported DDS pixel format size.");
        }

        reader.ReadUInt32(); // pfFlags
        var fourCc = new string(reader.ReadChars(4));
        reader.ReadUInt32(); // rgb bit count
        reader.ReadUInt32(); // r mask
        reader.ReadUInt32(); // g mask
        reader.ReadUInt32(); // b mask
        reader.ReadUInt32(); // a mask

        reader.ReadUInt32(); // caps
        reader.ReadUInt32(); // caps2
        reader.ReadUInt32(); // caps3
        reader.ReadUInt32(); // caps4
        reader.ReadUInt32(); // reserved2

        var remainingData = stream.Length - stream.Position;
        if (remainingData > int.MaxValue)
        {
            throw new InvalidDataException("DDS payload is too large to load.");
        }

        var data = reader.ReadBytes((int)remainingData);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("DDS has invalid dimensions.");
        }

        if (string.IsNullOrWhiteSpace(fourCc) || fourCc is not ("DXT1" or "DXT3" or "DXT5"))
        {
            throw new InvalidDataException($"Unsupported DDS FourCC '{fourCc}'.");
        }

        int mainSize = ComputeLevelSize(width, height, fourCc);
        if (mainSize <= 0 || mainSize > data.Length)
        {
            throw new InvalidDataException("DDS data is truncated.");
        }

        var mainData = new byte[mainSize];
        Buffer.BlockCopy(data, 0, mainData, 0, mainSize);

        int mipSize = Math.Max(0, data.Length - mainSize);
        var mipData = new byte[mipSize];
        if (mipSize > 0)
        {
            Buffer.BlockCopy(data, mainSize, mipData, 0, mipSize);
        }

        return new DdsTextureData(width, height, fourCc, mipMapCount, mainData, mipData);
    }

    public static void Create(string path, int height, int width, string fourCc, int mipMapCount, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path must be provided.", nameof(path));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Create(stream, height, width, fourCc, mipMapCount, data);
    }

    public static void Create(Stream stream, int height, int width, string fourCc, int mipMapCount, byte[] data)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (height <= 0 || width <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Invalid texture dimensions.");
        if (string.IsNullOrWhiteSpace(fourCc) || fourCc.Length != 4)
            throw new ArgumentException("FourCC must be a 4 character code.", nameof(fourCc));

        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        WriteHeader(writer, height, width, fourCc, mipMapCount, data.Length);
        writer.Write(data);
        writer.Flush();
    }

    private static void WriteHeader(BinaryWriter writer, int height, int width, string fourCc, int mipMapCount, int dataLength)
    {
        const uint DDSD_CAPS = 0x1;
        const uint DDSD_HEIGHT = 0x2;
        const uint DDSD_WIDTH = 0x4;
        const uint DDSD_PIXELFORMAT = 0x1000;
        const uint DDSD_MIPMAPCOUNT = 0x20000;
        const uint DDSD_LINEARSIZE = 0x80000;

        const uint DDPF_FOURCC = 0x4;

        const uint DDSCAPS_TEXTURE = 0x1000;
        const uint DDSCAPS_COMPLEX = 0x8;
        const uint DDSCAPS_MIPMAP = 0x400000;

        int blockSize = fourCc switch
        {
            "DXT1" => 8,
            "DXT3" => 16,
            "DXT5" => 16,
            _ => 0
        };

        int linearSize = blockSize > 0
            ? ((width + 3) / 4) * ((height + 3) / 4) * blockSize
            : dataLength;

        uint flags = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT | DDSD_LINEARSIZE;
        if (mipMapCount > 1)
        {
            flags |= DDSD_MIPMAPCOUNT;
        }

        writer.Write(new[] { (byte)'D', (byte)'D', (byte)'S', (byte)' ' });
        writer.Write(124u);
        writer.Write(flags);
        writer.Write((uint)height);
        writer.Write((uint)width);
        writer.Write((uint)linearSize);
        writer.Write(0u);
        writer.Write(mipMapCount > 1 ? (uint)mipMapCount : 0u);

        for (int i = 0; i < 11; i++)
        {
            writer.Write(0u);
        }

        writer.Write(32u);
        writer.Write(DDPF_FOURCC);
        writer.Write(MakeFourCC(fourCc));
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);

        uint caps = DDSCAPS_TEXTURE;
        if (mipMapCount > 1)
        {
            caps |= DDSCAPS_COMPLEX | DDSCAPS_MIPMAP;
        }

        writer.Write(caps);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
    }

    private static uint MakeFourCC(string fourCc)
    {
        return (uint)(fourCc[0] | (fourCc[1] << 8) | (fourCc[2] << 16) | (fourCc[3] << 24));
    }

    private static int ComputeLevelSize(int width, int height, string fourCc)
    {
        int blockSize = fourCc switch
        {
            "DXT1" => 8,
            "DXT3" => 16,
            "DXT5" => 16,
            _ => 0
        };

        if (blockSize == 0)
        {
            return 0;
        }

        try
        {
            int blocksWide = checked((width + 3) / 4);
            int blocksHigh = checked((height + 3) / 4);
            return checked(blocksWide * blocksHigh * blockSize);
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("DDS dimensions are too large.", ex);
        }
    }
}
