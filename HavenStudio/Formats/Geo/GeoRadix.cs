using HavenStudio.Extensions;

namespace HavenStudio.Formats.Geo;

public class GeoRadix
{
    public short Offset;
    public byte[] Types;
    public byte[] Padding;

    public GeoRadix(short offset, byte[] types, byte[]? padding = null)
    {
        Offset = offset;
        Types = types;
        Padding = padding ?? [];
    }

    public GeoRadix(EndianBinaryReader reader, GeoGroup group)
    {
        Offset = reader.ReadInt16();
        Types = reader.ReadBytes(group.TypesCount);
        var paddingLength = group.RadixSize - sizeof(short) - group.TypesCount;
        Padding = paddingLength > 0 ? reader.ReadBytes(paddingLength) : [];
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(Offset);
        writer.Write(Types);
        writer.Write(Padding);
    }
}
