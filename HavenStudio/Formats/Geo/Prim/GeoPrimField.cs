using HavenStudio.Extensions;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Geo;

public class GeoPrimField
{
    public float SizeX;
    public float SizeY;
    public float SizeZ;
    public short Pad;
    public ushort Attribute;
    public float WorldX;
    public float WorldY;
    public float WorldZ;
    public uint RootID;

    public GeoPrimField(EndianBinaryReader reader)
    {
        SizeX = reader.ReadSingle();
        SizeY = reader.ReadSingle();
        SizeZ = reader.ReadSingle();
        Pad = reader.ReadInt16();
        Attribute = reader.ReadUInt16();
        WorldX = reader.ReadSingle();
        WorldY = reader.ReadSingle();
        WorldZ = reader.ReadSingle();
        RootID = reader.ReadUInt32();
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(SizeX);
        writer.Write(SizeY);
        writer.Write(SizeZ);
        writer.Write(Pad);
        writer.Write(Attribute);
        writer.Write(WorldX);
        writer.Write(WorldY);
        writer.Write(WorldZ);
        writer.Write(RootID);
    }
}
