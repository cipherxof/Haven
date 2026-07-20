using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class MdnMaterial
{
    public int Flag { get; set; }
    public int NameHash { get; set; }

    public int TextureCount { get; set; }
    public int ColorCount { get; set; }

    public int DiffuseIndex { get; set; }
    public int NormalIndex { get; set; }
    public int SpecularIndex { get; set; }
    public int FilterIndex { get; set; }
    public int AmbientIndex { get; set; }
    public int SpecGradientIndex { get; set; }
    public int WrinkleIndex { get; set; }
    public int UnknownIndex { get; set; }

    public short DiffuseR { get; set; }
    public short DiffuseG { get; set; }
    public short DiffuseB { get; set; }
    public short DiffuseA { get; set; }

    public short SpecularR { get; set; }
    public short SpecularG { get; set; }
    public short SpecularB { get; set; }
    public short SpecularA { get; set; }

    public short UnknownR { get; set; }
    public short UnknownG { get; set; }
    public short UnknownB { get; set; }
    public short UnknownA { get; set; }

    public short UnknownR2 { get; set; }
    public short UnknownG2 { get; set; }
    public short UnknownB2 { get; set; }
    public short UnknownA2 { get; set; }

    public short UnknownR3 { get; set; }
    public short UnknownG3 { get; set; }
    public short UnknownB3 { get; set; }
    public short UnknownA3 { get; set; }

    public short UnknownR4 { get; set; }
    public short UnknownG4 { get; set; }
    public short UnknownB4 { get; set; }
    public short UnknownA4 { get; set; }

    public short UnknownR5 { get; set; }
    public short UnknownG5 { get; set; }
    public short UnknownB5 { get; set; }
    public short UnknownA5 { get; set; }

    public short UnknownR6 { get; set; }
    public short UnknownG6 { get; set; }
    public short UnknownB6 { get; set; }
    public short UnknownA6 { get; set; }

    public static MdnMaterial ReadFrom(EndianBinaryReader r)
    {
        var m = new MdnMaterial
        {
            Flag = r.ReadInt32(),
            NameHash = r.ReadInt32(),
            TextureCount = r.ReadInt32(),
            ColorCount = r.ReadInt32(),
            DiffuseIndex = r.ReadInt32(),
            NormalIndex = r.ReadInt32(),
            SpecularIndex = r.ReadInt32(),
            FilterIndex = r.ReadInt32(),
            AmbientIndex = r.ReadInt32(),
            SpecGradientIndex = r.ReadInt32(),
            WrinkleIndex = r.ReadInt32(),
            UnknownIndex = r.ReadInt32(),

            DiffuseR = r.ReadInt16(),
            DiffuseG = r.ReadInt16(),
            DiffuseB = r.ReadInt16(),
            DiffuseA = r.ReadInt16(),

            SpecularR = r.ReadInt16(),
            SpecularG = r.ReadInt16(),
            SpecularB = r.ReadInt16(),
            SpecularA = r.ReadInt16(),

            UnknownR = r.ReadInt16(),
            UnknownG = r.ReadInt16(),
            UnknownB = r.ReadInt16(),
            UnknownA = r.ReadInt16(),

            UnknownR2 = r.ReadInt16(),
            UnknownG2 = r.ReadInt16(),
            UnknownB2 = r.ReadInt16(),
            UnknownA2 = r.ReadInt16(),

            UnknownR3 = r.ReadInt16(),
            UnknownG3 = r.ReadInt16(),
            UnknownB3 = r.ReadInt16(),
            UnknownA3 = r.ReadInt16(),

            UnknownR4 = r.ReadInt16(),
            UnknownG4 = r.ReadInt16(),
            UnknownB4 = r.ReadInt16(),
            UnknownA4 = r.ReadInt16(),

            UnknownR5 = r.ReadInt16(),
            UnknownG5 = r.ReadInt16(),
            UnknownB5 = r.ReadInt16(),
            UnknownA5 = r.ReadInt16(),

            UnknownR6 = r.ReadInt16(),
            UnknownG6 = r.ReadInt16(),
            UnknownB6 = r.ReadInt16(),
            UnknownA6 = r.ReadInt16(),
        };
        return m;
    }

    public void WriteTo(EndianBinaryWriter w)
    {
        w.WriteInt32(Flag);
        w.WriteInt32(NameHash);
        w.WriteInt32(TextureCount);
        w.WriteInt32(ColorCount);

        w.WriteInt32(DiffuseIndex);
        w.WriteInt32(NormalIndex);
        w.WriteInt32(SpecularIndex);
        w.WriteInt32(FilterIndex);
        w.WriteInt32(AmbientIndex);
        w.WriteInt32(SpecGradientIndex);
        w.WriteInt32(WrinkleIndex);
        w.WriteInt32(UnknownIndex);

        w.WriteInt16(DiffuseR);
        w.WriteInt16(DiffuseG);
        w.WriteInt16(DiffuseB);
        w.WriteInt16(DiffuseA);
        w.WriteInt16(SpecularR);
        w.WriteInt16(SpecularG);
        w.WriteInt16(SpecularB);
        w.WriteInt16(SpecularA);

        w.WriteInt16(UnknownR);
        w.WriteInt16(UnknownG);
        w.WriteInt16(UnknownB);
        w.WriteInt16(UnknownA);
        w.WriteInt16(UnknownR2);
        w.WriteInt16(UnknownG2);
        w.WriteInt16(UnknownB2);
        w.WriteInt16(UnknownA2);
        w.WriteInt16(UnknownR3);
        w.WriteInt16(UnknownG3);
        w.WriteInt16(UnknownB3);
        w.WriteInt16(UnknownA3);
        w.WriteInt16(UnknownR4);
        w.WriteInt16(UnknownG4);
        w.WriteInt16(UnknownB4);
        w.WriteInt16(UnknownA4);
        w.WriteInt16(UnknownR5);
        w.WriteInt16(UnknownG5);
        w.WriteInt16(UnknownB5);
        w.WriteInt16(UnknownA5);
        w.WriteInt16(UnknownR6);
        w.WriteInt16(UnknownG6);
        w.WriteInt16(UnknownB6);
        w.WriteInt16(UnknownA6);
    }
}   