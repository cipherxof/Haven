using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class MdnBone
{
    public int NameHash { get; set; }
    public int Y0 { get; set; }
    public int Parent { get; set; }
    public int W0 { get; set; }

    public float RotX { get; set; }
    public float RotY { get; set; }
    public float RotZ { get; set; }
    public float RotW { get; set; }

    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float PosW { get; set; }

    public float MaxX { get; set; }
    public float MaxY { get; set; }
    public float MaxZ { get; set; }
    public float MaxW { get; set; }

    public float MinX { get; set; }
    public float MinY { get; set; }
    public float MinZ { get; set; }
    public float MinW { get; set; }
    
    public static MdnBone ReadFrom(EndianBinaryReader r) => new()
    {
        NameHash = r.ReadInt32(),
        Y0 = r.ReadInt32(),
        Parent = r.ReadInt32(),
        W0 = r.ReadInt32(),
        RotX = r.ReadSingle(),
        RotY = r.ReadSingle(),
        RotZ = r.ReadSingle(),
        RotW = r.ReadSingle(),
        PosX = r.ReadSingle(),
        PosY = r.ReadSingle(),
        PosZ = r.ReadSingle(),
        PosW = r.ReadSingle(),
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
        w.WriteInt32(NameHash); w.WriteInt32(Y0); w.WriteInt32(Parent); w.WriteInt32(W0);

        w.WriteSingle(RotX); w.WriteSingle(RotY); w.WriteSingle(RotZ); w.WriteSingle(RotW);
        w.WriteSingle(PosX); w.WriteSingle(PosY); w.WriteSingle(PosZ); w.WriteSingle(PosW);
        w.WriteSingle(MaxX); w.WriteSingle(MaxY); w.WriteSingle(MaxZ); w.WriteSingle(MaxW);
        w.WriteSingle(MinX); w.WriteSingle(MinY); w.WriteSingle(MinZ); w.WriteSingle(MinW);
    }
}