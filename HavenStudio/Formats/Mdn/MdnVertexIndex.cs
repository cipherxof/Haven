using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class MdnVertexIndex
{
    public int MeshGroupIndex { get; set; }
    public int Unk2 { get; set; }
    public int FaceSectionCount { get; set; }
    public int FaceSectionStart { get; set; }
    public int VertexId { get; set; }
    public int BonePaletteId { get; set; }
    public int VertexCount { get; set; }

    public float MaxX { get; set; }
    public float MaxY { get; set; }
    public float MaxZ { get; set; }
    public float MaxW { get; set; }

    public float MinX { get; set; }
    public float MinY { get; set; }
    public float MinZ { get; set; }
    public float MinW { get; set; }

    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float PosW { get; set; }
    
    public static MdnVertexIndex ReadFrom(EndianBinaryReader r)
    {
        var vi = new MdnVertexIndex
        {
            MeshGroupIndex = r.ReadInt32(),
            Unk2 = r.ReadInt32(),
            FaceSectionCount = r.ReadInt32(),
            FaceSectionStart = r.ReadInt32(),
            VertexId = r.ReadInt32(),
            BonePaletteId = r.ReadInt32(),
            VertexCount = r.ReadInt32()
        };

        r.Skip(4);

        vi.MaxX = r.ReadSingle();
        vi.MaxY = r.ReadSingle();
        vi.MaxZ = r.ReadSingle();
        vi.MaxW = r.ReadSingle();

        vi.MinX = r.ReadSingle();
        vi.MinY = r.ReadSingle();
        vi.MinZ = r.ReadSingle();
        vi.MinW = r.ReadSingle();

        vi.PosX = r.ReadSingle();
        vi.PosY = r.ReadSingle();
        vi.PosZ = r.ReadSingle();
        vi.PosW = r.ReadSingle();

        return vi;
    }

    public void WriteTo(EndianBinaryWriter w)
    {
        w.WriteInt32(MeshGroupIndex);
        w.WriteInt32(Unk2);
        w.WriteInt32(FaceSectionCount);
        w.WriteInt32(FaceSectionStart);
        w.WriteInt32(VertexId);
        w.WriteInt32(BonePaletteId);
        w.WriteInt32(VertexCount);
        w.WriteZero(4);

        w.WriteSingle(MaxX); w.WriteSingle(MaxY); w.WriteSingle(MaxZ); w.WriteSingle(MaxW);
        w.WriteSingle(MinX); w.WriteSingle(MinY); w.WriteSingle(MinZ); w.WriteSingle(MinW);
        w.WriteSingle(PosX); w.WriteSingle(PosY); w.WriteSingle(PosZ); w.WriteSingle(PosW);
    }
}