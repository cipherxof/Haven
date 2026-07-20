using HavenStudio.Extensions;

namespace HavenStudio.Formats.Geo;

public class GeoChunk
{
    public ushort Type;
    public ushort Pad;
    public int Size;
    public int DataOffset;

    public GeoChunk()
    {
    }

    public GeoChunk(GeoChunkType type)
    {
        Type = (ushort)type;
    }

    public GeoChunk(EndianBinaryReader reader)
    {
        Type = reader.ReadUInt16();
        Pad = reader.ReadUInt16();
        Size = reader.ReadInt32();
        DataOffset = reader.ReadInt32();
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(Type);
        writer.Write(Pad);
        writer.Write(Size);
        writer.Write(DataOffset);
    }
}
