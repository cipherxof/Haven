using System;
using System.IO;
using HavenStudio.Crypto;

namespace HavenStudio;

public sealed class CryptoService
{
    public void Encrypt(string filePath, string keyText)
    {
        var input = File.ReadAllBytes(filePath);
        var outputPath = filePath + ".enc";
        File.WriteAllBytes(outputPath, Encrypt(input, keyText));
    }

    public void Decrypt(string filePath, string keyText)
    {
        var outputPath = filePath + ".dec";
        File.WriteAllBytes(outputPath, Decrypt(File.ReadAllBytes(filePath), keyText));
    }

    public byte[] Encrypt(ReadOnlySpan<byte> input, string keyText)
    {
        ValidateKey(keyText);

        var crypto = new PtsysCrypto(keyText);
        int size = input.Length;
        var buffer = new byte[size + 0x18];
        input.CopyTo(buffer);

        var endBytes = WritePad(buffer, ref size, out int rem);
        crypto.EncryptInPlace(buffer, size);
        AddEndBytes(buffer, ref size, endBytes, rem);
        crypto.AppendChecksum(buffer, ref size);

        return buffer.AsSpan(0, size).ToArray();
    }

    public byte[] Decrypt(ReadOnlySpan<byte> input, string keyText)
    {
        ValidateKey(keyText);

        var crypto = new PtsysCrypto(keyText);
        var buffer = input.ToArray();
        int size = buffer.Length;

        crypto.DecryptInPlace(buffer, size);
        int outSize = Math.Max(0, size - 0x18);
        return buffer.AsSpan(0, outSize).ToArray();
    }

    public void EncryptFolder(string folderPath, string outputFolderPath, string keyText)
    {
        if (string.IsNullOrWhiteSpace(keyText))
        {
            throw new InvalidOperationException("Key cannot be empty.");
        }

        if (!Directory.Exists(outputFolderPath))
        {
            Directory.CreateDirectory(outputFolderPath);
        }

        var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
        var basePath = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var file in files)
        {
            var input = File.ReadAllBytes(file);

            var relativePath = Path.GetRelativePath(basePath, file);
            var outputPath = Path.Combine(outputFolderPath, relativePath);
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllBytes(outputPath, Encrypt(input, keyText));
        }
    }

    public void DecryptFolder(string folderPath, string outputFolderPath, string keyText)
    {
        if (string.IsNullOrWhiteSpace(keyText))
        {
            throw new InvalidOperationException("Key cannot be empty.");
        }

        if (!Directory.Exists(outputFolderPath))
        {
            Directory.CreateDirectory(outputFolderPath);
        }

        var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
        var basePath = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(basePath, file);
            var outputPath = Path.Combine(outputFolderPath, relativePath);
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllBytes(outputPath, Decrypt(File.ReadAllBytes(file), keyText));
        }
    }

    private static void ValidateKey(string keyText)
    {
        if (string.IsNullOrWhiteSpace(keyText))
        {
            throw new InvalidOperationException("Key cannot be empty.");
        }
    }

    private static byte[] WritePad(byte[] buffer, ref int size, out int rem)
    {
        rem = size % 8;
        int padding = 8 - rem;
        int endBytes = size - rem;
        var ptBuffer = new byte[rem];

        for (int i = 0; i < padding; i++)
        {
            buffer[size + i] = (byte)padding;
        }

        for (int i = 0; i < rem; i++)
        {
            ptBuffer[i] = buffer[endBytes + i];
        }

        size += padding;
        return ptBuffer;
    }

    private static void AddEndBytes(byte[] buffer, ref int size, byte[] endBytes, int rem)
    {
        if (rem <= 0)
        {
            return;
        }

        Buffer.BlockCopy(endBytes, 0, buffer, size, rem);
        size += rem;
    }
}
