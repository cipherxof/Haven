using System.Collections.Generic;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Lit;

public sealed class LitGroup
{
    public Vector4 BoundsMax { get; set; }
    public Vector4 BoundsMin { get; set; }
    public uint Type { get; set; }
    public uint LitOffset { get; internal set; }
    public uint Pad { get; set; }
    public List<LitLight> Lights { get; } = [];

    public bool Contains(Vector3 point) =>
        point.X >= BoundsMin.X && point.X <= BoundsMax.X &&
        point.Y >= BoundsMin.Y && point.Y <= BoundsMax.Y &&
        point.Z >= BoundsMin.Z && point.Z <= BoundsMax.Z;
}
