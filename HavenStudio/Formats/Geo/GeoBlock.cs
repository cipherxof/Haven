using HavenStudio.Extensions;

namespace HavenStudio.Formats.Geo;

public class GeoBlock
{
    public byte Flag;
    public byte GeomCount;
    public ushort Size;
    public ushort Tail;
    public ushort Free;
    public ushort Head;
    public ushort Pad;
    public int VertexOffset;
    public int FaceOffset;
    public int MaterialOffset;
    public ulong Attribute;

    public int Offset;

    public GeoBlock()
    {
    }

    public GeoBlock(EndianBinaryReader reader)
    {
        Offset = (int)reader.BaseStream.Position;

        Flag = reader.ReadByte();
        GeomCount = reader.ReadByte();
        Size = reader.ReadUInt16();
        Tail = reader.ReadUInt16();
        Free = reader.ReadUInt16();
        Head = reader.ReadUInt16();
        Pad = reader.ReadUInt16();
        VertexOffset = reader.ReadInt32();
        FaceOffset = reader.ReadInt32();
        MaterialOffset = reader.ReadInt32();
        Attribute = reader.ReadUInt64();
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(Flag);
        writer.Write(GeomCount);
        writer.Write(Size);
        writer.Write(Tail);
        writer.Write(Free);
        writer.Write(Head);
        writer.Write(Pad);
        writer.Write(VertexOffset);
        writer.Write(FaceOffset);
        writer.Write(MaterialOffset);
        writer.Write(Attribute);
    }
}
