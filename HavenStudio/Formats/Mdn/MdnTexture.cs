using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class MdnTexture
{
    public int NameHash { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    
    public static MdnTexture ReadFrom(EndianBinaryReader r)
    {
        var t = new MdnTexture
        {
            NameHash = r.ReadInt32(),
            X = r.ReadSingle(),
            Y = r.ReadSingle(),
            Z = r.ReadSingle()
        };
        r.Skip(0x10);
        return t;
    }

    public void WriteTo(EndianBinaryWriter w)
    {
        w.WriteInt32(NameHash);
        w.WriteSingle(X);
        w.WriteSingle(Y);
        w.WriteSingle(Z);
        w.WriteZero(0x10);
    }
}