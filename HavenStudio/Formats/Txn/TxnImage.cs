using HavenStudio.Extensions;

namespace HavenStudio.Formats.Txn;

public class TxnImage
{
    public ushort Width;
    public ushort Height;
    public ushort FourCC;
    public ushort Flag;
    public uint Offset;
    public uint OffsetMips;

    public TxnImage(ushort width, ushort height, ushort fourCC, ushort flag, uint offset, uint mipmapOffset)
    {
        Width = width;
        Height = height;
        FourCC = fourCC;
        Flag = flag;
        Offset = offset;
        OffsetMips = mipmapOffset;
    }

    public TxnImage(EndianBinaryReader reader)
    {
        Width = reader.ReadUInt16();
        Height = reader.ReadUInt16();
        FourCC = reader.ReadUInt16();
        Flag = reader.ReadUInt16();
        Offset = reader.ReadUInt32();
        OffsetMips = reader.ReadUInt32();
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(Width);
        writer.Write(Height);
        writer.Write(FourCC);
        writer.Write(Flag);
        writer.Write(Offset);
        writer.Write(OffsetMips);
    }
}