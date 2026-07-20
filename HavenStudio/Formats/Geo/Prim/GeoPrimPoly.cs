using System;
using HavenStudio.Extensions;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Geo;

public class GeoPrimPoly
{
    public byte[] Data;
    public ushort Attribute;

    public GeoPrimPoly(EndianBinaryReader reader)
    {
        Data = reader.ReadBytes(6);
        Attribute = reader.ReadUInt16();
    }

    public GeoPrimPoly(byte[] data, ushort attribute)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != 6)
        {
            throw new ArgumentException("A GEOM polygon record must contain six data bytes.", nameof(data));
        }

        Data = data;
        Attribute = attribute;
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(Data);
        writer.Write(Attribute);
    }
}
