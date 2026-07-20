using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Dlz;

public class DlzSegIndex
{
    public ushort SizeCompressed;
    public ushort SizeDecompressed;
    public uint ChunkOffset;

    public DlzSegIndex(EndianBinaryReader reader)
    {
        SizeCompressed = reader.ReadUInt16();
        SizeDecompressed = reader.ReadUInt16();
        ChunkOffset = reader.ReadUInt32();
    }

    public DlzSegIndex(ushort sizeCompressed, ushort sizeDecompressed, uint chunkOffset)
    {
        SizeCompressed = sizeCompressed;
        SizeDecompressed = sizeDecompressed;
        ChunkOffset = chunkOffset;
    }

    public void WriteTo(BinaryWriter writer)
    {
        writer.Write(SizeCompressed);
        writer.Write(SizeDecompressed);
        writer.Write(ChunkOffset);
    }
}