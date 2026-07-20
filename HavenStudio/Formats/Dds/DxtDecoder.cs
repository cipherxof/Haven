using System;
using System.IO;

namespace HavenStudio.Formats.Dds;

public static class DxtDecoder
{
    public static byte[] DecodeToRgba(int width, int height, string fourCc, byte[] data)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "DXT dimensions must be positive.");
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var blockSize = fourCc switch
        {
            "DXT1" => 8,
            "DXT3" or "DXT5" => 16,
            _ => throw new NotSupportedException($"Unsupported DDS format '{fourCc}'.")
        };
        var blocksWide = checked((width + 3) / 4);
        var blocksHigh = checked((height + 3) / 4);
        var requiredBytes = checked((long)blocksWide * blocksHigh * blockSize);
        if (requiredBytes > data.Length)
        {
            throw new InvalidDataException(
                $"{fourCc} data is truncated: {requiredBytes} bytes are required, but {data.Length} are available.");
        }

        _ = checked(width * height * 4);
        return fourCc switch
        {
            "DXT1" => DecodeDxt1(width, height, data),
            "DXT3" => DecodeDxt3(width, height, data),
            "DXT5" => DecodeDxt5(width, height, data),
            _ => throw new NotSupportedException($"Unsupported DDS format '{fourCc}'.")
        };
    }

    private static byte[] DecodeDxt1(int width, int height, byte[] data)
    {
        var rgba = new byte[width * height * 4];
        int blocksWide = (width + 3) / 4;
        int blocksHigh = (height + 3) / 4;

        int offset = 0;
        Span<uint> colors = stackalloc uint[4];
        for (int by = 0; by < blocksHigh; by++)
        {
            for (int bx = 0; bx < blocksWide; bx++)
            {
                ushort c0 = ReadUInt16LE(data, offset);
                ushort c1 = ReadUInt16LE(data, offset + 2);
                uint indices = ReadUInt32LE(data, offset + 4);
                offset += 8;

                colors[0] = ExpandRgb565(c0, 255);
                colors[1] = ExpandRgb565(c1, 255);

                if (c0 > c1)
                {
                    colors[2] = Lerp(colors[0], colors[1], 2, 1, 3);
                    colors[3] = Lerp(colors[0], colors[1], 1, 2, 3);
                }
                else
                {
                    colors[2] = Lerp(colors[0], colors[1], 1, 1, 2);
                    colors[3] = 0x00000000;
                }

                WriteColorBlock(rgba, width, height, bx, by, indices, colors);
            }
        }

        return rgba;
    }

    private static byte[] DecodeDxt3(int width, int height, byte[] data)
    {
        var rgba = new byte[width * height * 4];
        int blocksWide = (width + 3) / 4;
        int blocksHigh = (height + 3) / 4;

        int offset = 0;
        Span<uint> colors = stackalloc uint[4];
        for (int by = 0; by < blocksHigh; by++)
        {
            for (int bx = 0; bx < blocksWide; bx++)
            {
                ulong alphaBits = ReadUInt64LE(data, offset);
                ushort c0 = ReadUInt16LE(data, offset + 8);
                ushort c1 = ReadUInt16LE(data, offset + 10);
                uint indices = ReadUInt32LE(data, offset + 12);
                offset += 16;

                colors[0] = ExpandRgb565(c0, 255);
                colors[1] = ExpandRgb565(c1, 255);
                colors[2] = Lerp(colors[0], colors[1], 2, 1, 3);
                colors[3] = Lerp(colors[0], colors[1], 1, 2, 3);

                for (int py = 0; py < 4; py++)
                {
                    for (int px = 0; px < 4; px++)
                    {
                        int x = bx * 4 + px;
                        int y = by * 4 + py;
                        if (x >= width || y >= height)
                        {
                            continue;
                        }

                        int pixelIndex = py * 4 + px;
                        int colorIndex = (int)((indices >> (pixelIndex * 2)) & 0x3);
                        uint color = colors[colorIndex];

                        int alpha4 = (int)((alphaBits >> (pixelIndex * 4)) & 0xF);
                        byte alpha = (byte)((alpha4 << 4) | alpha4);
                        WritePixel(rgba, width, x, y, color, alpha);
                    }
                }
            }
        }

        return rgba;
    }

    private static byte[] DecodeDxt5(int width, int height, byte[] data)
    {
        var rgba = new byte[width * height * 4];
        int blocksWide = (width + 3) / 4;
        int blocksHigh = (height + 3) / 4;

        int offset = 0;
        Span<byte> alphaTable = stackalloc byte[8];
        Span<uint> colors = stackalloc uint[4];
        for (int by = 0; by < blocksHigh; by++)
        {
            for (int bx = 0; bx < blocksWide; bx++)
            {
                byte a0 = data[offset];
                byte a1 = data[offset + 1];
                ulong alphaIndices = 0;
                for (int i = 0; i < 6; i++)
                {
                    alphaIndices |= (ulong)data[offset + 2 + i] << (8 * i);
                }

                ushort c0 = ReadUInt16LE(data, offset + 8);
                ushort c1 = ReadUInt16LE(data, offset + 10);
                uint colorIndices = ReadUInt32LE(data, offset + 12);
                offset += 16;

                BuildAlphaTable(a0, a1, alphaTable);

                colors[0] = ExpandRgb565(c0, 255);
                colors[1] = ExpandRgb565(c1, 255);
                colors[2] = Lerp(colors[0], colors[1], 2, 1, 3);
                colors[3] = Lerp(colors[0], colors[1], 1, 2, 3);

                for (int py = 0; py < 4; py++)
                {
                    for (int px = 0; px < 4; px++)
                    {
                        int x = bx * 4 + px;
                        int y = by * 4 + py;
                        if (x >= width || y >= height)
                        {
                            continue;
                        }

                        int pixelIndex = py * 4 + px;
                        int cidx = (int)((colorIndices >> (pixelIndex * 2)) & 0x3);
                        int aidx = (int)((alphaIndices >> (pixelIndex * 3)) & 0x7);
                        WritePixel(rgba, width, x, y, colors[cidx], alphaTable[aidx]);
                    }
                }
            }
        }

        return rgba;
    }

    private static void BuildAlphaTable(byte a0, byte a1, Span<byte> table)
    {
        table[0] = a0;
        table[1] = a1;
        if (a0 > a1)
        {
            table[2] = (byte)((6 * a0 + 1 * a1) / 7);
            table[3] = (byte)((5 * a0 + 2 * a1) / 7);
            table[4] = (byte)((4 * a0 + 3 * a1) / 7);
            table[5] = (byte)((3 * a0 + 4 * a1) / 7);
            table[6] = (byte)((2 * a0 + 5 * a1) / 7);
            table[7] = (byte)((1 * a0 + 6 * a1) / 7);
        }
        else
        {
            table[2] = (byte)((4 * a0 + 1 * a1) / 5);
            table[3] = (byte)((3 * a0 + 2 * a1) / 5);
            table[4] = (byte)((2 * a0 + 3 * a1) / 5);
            table[5] = (byte)((1 * a0 + 4 * a1) / 5);
            table[6] = 0;
            table[7] = 255;
        }
    }

    private static void WriteColorBlock(byte[] rgba, int width, int height, int bx, int by, uint indices, Span<uint> colors)
    {
        for (int py = 0; py < 4; py++)
        {
            for (int px = 0; px < 4; px++)
            {
                int x = bx * 4 + px;
                int y = by * 4 + py;
                if (x >= width || y >= height)
                {
                    continue;
                }

                int pixelIndex = py * 4 + px;
                int colorIndex = (int)((indices >> (pixelIndex * 2)) & 0x3);
                uint color = colors[colorIndex];
                byte alpha = (byte)((color >> 24) & 0xFF);
                WritePixel(rgba, width, x, y, color, alpha);
            }
        }
    }

    private static void WritePixel(byte[] rgba, int width, int x, int y, uint color, byte alpha)
    {
        int offset = ((y * width) + x) * 4;
        rgba[offset + 0] = (byte)((color >> 16) & 0xFF);
        rgba[offset + 1] = (byte)((color >> 8) & 0xFF);
        rgba[offset + 2] = (byte)(color & 0xFF);
        rgba[offset + 3] = alpha;
    }

    private static uint ExpandRgb565(ushort value, byte alpha)
    {
        int r = (value >> 11) & 0x1F;
        int g = (value >> 5) & 0x3F;
        int b = value & 0x1F;

        byte rr = (byte)((r << 3) | (r >> 2));
        byte gg = (byte)((g << 2) | (g >> 4));
        byte bb = (byte)((b << 3) | (b >> 2));

        return ((uint)alpha << 24) | ((uint)rr << 16) | ((uint)gg << 8) | bb;
    }

    private static uint Lerp(uint c0, uint c1, int w0, int w1, int denom)
    {
        int a0 = (int)((c0 >> 24) & 0xFF);
        int r0 = (int)((c0 >> 16) & 0xFF);
        int g0 = (int)((c0 >> 8) & 0xFF);
        int b0 = (int)(c0 & 0xFF);

        int a1 = (int)((c1 >> 24) & 0xFF);
        int r1 = (int)((c1 >> 16) & 0xFF);
        int g1 = (int)((c1 >> 8) & 0xFF);
        int b1 = (int)(c1 & 0xFF);

        byte a = (byte)((a0 * w0 + a1 * w1) / denom);
        byte r = (byte)((r0 * w0 + r1 * w1) / denom);
        byte g = (byte)((g0 * w0 + g1 * w1) / denom);
        byte b = (byte)((b0 * w0 + b1 * w1) / denom);
        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static ushort ReadUInt16LE(byte[] data, int offset)
    {
        return (ushort)(data[offset] | (data[offset + 1] << 8));
    }

    private static uint ReadUInt32LE(byte[] data, int offset)
    {
        return (uint)(data[offset]
            | (data[offset + 1] << 8)
            | (data[offset + 2] << 16)
            | (data[offset + 3] << 24));
    }

    private static ulong ReadUInt64LE(byte[] data, int offset)
    {
        ulong result = 0;
        for (int i = 0; i < 8; i++)
        {
            result |= (ulong)data[offset + i] << (8 * i);
        }

        return result;
    }
}
