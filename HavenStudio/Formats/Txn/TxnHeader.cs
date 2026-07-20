using HavenStudio.Extensions;

namespace HavenStudio.Formats.Txn;

public class TxnHeader
{
    public uint NullBytes = 0;
    public uint Flags = 0;
    public uint TextureCount = 0;
    public uint IndexOffset = 0;
    public uint TextureCount2 = 0;
    public uint IndexOffset2 = 0;
    public uint NullBytes2 = 0;
    public uint NullBytes3 = 0;

    public TxnHeader()
    {
    }
    public TxnHeader(EndianBinaryReader reader)
    {
        NullBytes = reader.ReadUInt32();
        Flags = reader.ReadUInt32();
        TextureCount = reader.ReadUInt32();
        IndexOffset = reader.ReadUInt32();
        TextureCount2 = reader.ReadUInt32();
        IndexOffset2 = reader.ReadUInt32();
        NullBytes2 = reader.ReadUInt32();
        NullBytes3 = reader.ReadUInt32();
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(NullBytes);
        writer.Write(Flags);
        writer.Write(TextureCount);
        writer.Write(IndexOffset);
        writer.Write(TextureCount2);
        writer.Write(IndexOffset2);
        writer.Write(NullBytes2);
        writer.Write(NullBytes3);
    }
}