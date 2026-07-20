using System.Collections.Generic;

namespace HavenStudio.Formats.Geo;

public class GeoEffect
{
    public int Name { get; set; }
    public int Index { get; set; }

    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }
    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }

    public int ChunkOffset { get; set; }

    public List<GeoEffect> Children { get; } = new();
}
