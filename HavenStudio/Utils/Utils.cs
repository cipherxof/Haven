using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace HavenStudio.Utils;

public class Compression
{
    public const int DefaultCompressionLevel = 6;

    public static byte[] DeflateBuffer(
        byte[] uncompressedBytes,
        int compressionLevel = DefaultCompressionLevel)
    {
        using var inputStream = new MemoryStream(uncompressedBytes);
        using var outputStream = new MemoryStream();
        // SharpZipLib's true flag omits the zlib header/footer, matching zlib windowBits = -15.
        using var deflaterStream = new DeflaterOutputStream(outputStream, new Deflater(compressionLevel, true));
    
        inputStream.CopyTo(deflaterStream);
        deflaterStream.Finish();
    
        return outputStream.ToArray();
    }
    
    public static byte[] InflateBuffer2(byte[] compressedBytes, int decompressedSize)
    {
        byte[] result = new byte[decompressedSize];
    
        // SharpZipLib's true flag selects raw DEFLATE, matching zlib windowBits = -15.
        var inflater = new Inflater(true);
        inflater.SetInput(compressedBytes);
    
        int resultLength = inflater.Inflate(result);
    
        if (resultLength != decompressedSize)
        {
            throw new Exception($"Decompressed size mismatch: expected {decompressedSize}, got {resultLength}");
        }
    
        return result;
    }
    
    public static void InflateToStream(byte[] compressedBytes, Stream output)
    {
        var inflater = new Inflater(true);
        inflater.SetInput(compressedBytes);
    
        byte[] buffer = new byte[4096]; 
        int count;
    
        while ((count = inflater.Inflate(buffer)) > 0)
        {
            output.Write(buffer, 0, count);
        }
    }
    
    public static byte[] InflateBuffer3(byte[] compressedBytes, int decompressedSize)
    {
        byte[] result = new byte[decompressedSize];
    
        var inflater = new Inflater(true);
    
        using var inputStream = new MemoryStream(compressedBytes);
        using var outputStream = new MemoryStream(result, 0, result.Length, true);
        using var inflaterStream = new InflaterInputStream(inputStream, inflater);
    
        inflaterStream.CopyTo(outputStream);
    
        return result;
    }
}

public class String
{
    public static uint HashString(string str)
    {
        uint id = 0;
        uint mask = 0x00FFFFFF;

        for (var i = 0; i < str.Length; i++)
        {
            id = ((id >> 19) | (id << 5));
            id += str[i];
            id &= mask;
        }

        return id;
    }
}
