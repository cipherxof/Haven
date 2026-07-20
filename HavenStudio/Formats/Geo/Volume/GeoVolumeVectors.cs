using HavenStudio.Extensions;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Geo;

public class GeoVolumeVectors
{
    public Vector4 Pos;
    public Vector4 Norm;

    public GeoVolumeVectors(EndianBinaryReader reader)
    {
        Pos = reader.ReadVector4();
        Norm = reader.ReadVector4();
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(Pos);
        writer.Write(Norm);
    }
}
