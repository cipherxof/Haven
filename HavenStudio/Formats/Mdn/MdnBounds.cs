using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class MdnBounds
{
    public float MaxX { get; set; }
    public float MaxY { get; set; }
    public float MaxZ { get; set; }
    public float MaxW { get; set; }

    public float MinX { get; set; }
    public float MinY { get; set; }
    public float MinZ { get; set; }
    public float MinW { get; set; }
    
    public static MdnBounds ReadFrom(EndianBinaryReader r) => new()
    {
        MaxX = r.ReadSingle(),
        MaxY = r.ReadSingle(),
        MaxZ = r.ReadSingle(),
        MaxW = r.ReadSingle(),
        MinX = r.ReadSingle(),
        MinY = r.ReadSingle(),
        MinZ = r.ReadSingle(),
        MinW = r.ReadSingle(),
    };

    public void WriteTo(EndianBinaryWriter w)
    {
        w.WriteSingle(MaxX); w.WriteSingle(MaxY); w.WriteSingle(MaxZ); w.WriteSingle(MaxW);
        w.WriteSingle(MinX); w.WriteSingle(MinY); w.WriteSingle(MinZ); w.WriteSingle(MinW);
    }
}