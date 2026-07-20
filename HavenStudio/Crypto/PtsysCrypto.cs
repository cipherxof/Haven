using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HavenStudio.Crypto;

internal sealed class PtsysCrypto
{
    private static readonly byte[] BaseKey =
    {
        0x74, 0xF6, 0x6D, 0xC2, 0x85, 0x98, 0xF5, 0xD1,
        0x72, 0xAC, 0x2D, 0xCA, 0xCE, 0x55, 0x44, 0xD6,
        0x65, 0xF1, 0x1D, 0x05, 0xBE, 0xA2, 0x05, 0x68,
        0xE7, 0x6C, 0x52, 0x9D, 0xEB, 0x35, 0x89, 0x0E,
        0xC3, 0x32, 0xFF, 0x24, 0xFE, 0x5D, 0x9C, 0x3F,
        0xB3, 0x41, 0x89, 0xCF, 0x47, 0x05, 0x5B, 0x26,
        0xF9, 0xE4, 0xCC, 0x63, 0x9A, 0x46, 0xB5, 0x46,
        0x54, 0x04, 0xDF, 0x41, 0xE6, 0x5B, 0x8E, 0x4E
    };

    private static readonly byte[] FileKey =
    {
        0x53, 0x08, 0x57, 0x88, 0x72, 0x0C, 0xC9, 0x55,
        0xD1, 0xA7, 0x5F, 0xCA, 0x0A, 0x98, 0x8C, 0xED,
        0x84, 0xCF, 0xBA, 0x8B, 0xFD, 0xDA, 0x9A, 0x04,
        0x6A, 0xF0, 0xFB, 0x4D, 0xE0, 0x27, 0xDC, 0x24,
        0xB2, 0xB6, 0x36, 0x11, 0x0D, 0x27, 0xCA, 0x28,
        0x4E, 0x0A, 0xB1, 0x59, 0x12, 0x21, 0x25, 0x93,
        0xB5, 0x2D, 0x94, 0x5C, 0x63, 0x3A, 0x0B, 0x53,
        0x97, 0xD4, 0x1B, 0x64, 0xF7, 0x0E, 0xD1, 0xEE
    };

    private readonly ulong[] _key = new ulong[8];
    private readonly byte[] _saltA = new byte[64];
    private readonly byte[] _saltB = new byte[64];

    public PtsysCrypto(string folderKey)
    {
        Initialize(folderKey);
    }

    public void EncryptInPlace(byte[] buffer, int size)
    {
        DecodeBuffer64(_key, _key[0], 0, size + 0x10, buffer, true);
    }

    public void DecryptInPlace(byte[] buffer, int size)
    {
        DecodeBuffer64(_key, _key[0], 0, size - 0x10, buffer, false);
    }

    public void AppendChecksum(byte[] buffer, ref int size)
    {
        Span<byte> hash1 = stackalloc byte[16];
        Span<byte> hash2 = stackalloc byte[16];

        using (var md5 = MD5.Create())
        {
            md5.TransformBlock(_saltA, 0, _saltA.Length, null, 0);
            md5.TransformFinalBlock(buffer, 0, size);
            md5.Hash!.AsSpan().CopyTo(hash1);
        }

        using (var md5 = MD5.Create())
        {
            md5.TransformBlock(_saltB, 0, _saltB.Length, null, 0);
            md5.TransformFinalBlock(hash1.ToArray(), 0, hash1.Length);
            md5.Hash!.AsSpan().CopyTo(hash2);
        }

        hash2.CopyTo(buffer.AsSpan(size));
        size += 0x10;
    }

    private void Initialize(string folder)
    {
        var filesKey = DecryptKey(FileKey);
        var folderHash = MD5.HashData(Encoding.UTF8.GetBytes(folder));
        var derivedKey = new byte[64];

        for (int i = 0; i < 64; i++)
        {
            byte keyByte = (byte)(filesKey[i] ^ folderHash[i % 0x10]);
            derivedKey[i] = keyByte;
            _saltA[i] = (byte)(keyByte ^ 0x36);
            _saltB[i] = (byte)(keyByte ^ 0x5C);
        }

        for (int i = 0; i < 8; i++)
        {
            _key[i] = BinaryPrimitives.ReadUInt64LittleEndian(derivedKey.AsSpan(i * 8, 8));
        }
    }

    private static byte[] DecryptKey(ReadOnlySpan<byte> key)
    {
        var result = new byte[key.Length];
        Span<byte> keyIv = stackalloc byte[8];
        Span<byte> keyBlowfish = stackalloc byte[BaseKey.Length - 8];

        BaseKey.AsSpan(0, 8).CopyTo(keyIv);
        BaseKey.AsSpan(8).CopyTo(keyBlowfish);

        var blowfish = new Blowfish(keyBlowfish);

        byte[] last = keyIv.ToArray();
        Span<byte> blockIn = stackalloc byte[8];
        Span<byte> blockOut = stackalloc byte[8];

        for (int i = 0; i < key.Length / 8; i++)
        {
            key.Slice(i * 8, 8).CopyTo(blockIn);
            blowfish.DecryptEcb(blockIn, blockOut);
            for (int j = 0; j < 8; j++)
            {
                result[i * 8 + j] = (byte)(blockOut[j] ^ last[j]);
            }
            key.Slice(i * 8, 8).CopyTo(last);
        }

        return result;
    }

    private static void DecodeBuffer64(ulong[] keyA, ulong keyB, int offset, int size, byte[] buffer, bool enc)
    {
        int blockCount = size / 8;
        for (int i = offset; i < blockCount; i++)
        {
            int byteOffset = i * 8;
            ulong interval = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(byteOffset, 8));
            ulong value = interval ^ keyB ^ keyA[(i % 7) + 1];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(byteOffset, 8), value);
            keyB = enc ? value : interval;
        }
    }
}
