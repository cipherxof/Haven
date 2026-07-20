using HavenStudio.Extensions;

namespace HavenStudio.Formats.Gcx;

public class GcxHeader
{
    public int ScriptSectionOffset { get; set; }
    public int StringDefsOffset { get; set; }
    public int StringSectionOffset { get; set; }
    public int ScriptSectionOffsetDuplicate { get; set; }
    public int CryptoSeed { get; set; }

    public static GcxHeader ReadFrom(EndianBinaryReader r)
    {
        return new GcxHeader
        {
            ScriptSectionOffset = r.ReadInt32(),
            StringDefsOffset = r.ReadInt32(),
            StringSectionOffset = r.ReadInt32(),
            ScriptSectionOffsetDuplicate = r.ReadInt32(),
            CryptoSeed = r.ReadInt32(),
        };
    }

    public void WritePlaceholderTo(EndianBinaryWriter w)
    {
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(CryptoSeed);
    }

    public void WriteTo(EndianBinaryWriter w)
    {
        w.WriteInt32(ScriptSectionOffset);
        w.WriteInt32(StringDefsOffset);
        w.WriteInt32(StringSectionOffset);
        w.WriteInt32(ScriptSectionOffset); // duplicate
        w.WriteInt32(CryptoSeed);
    }
}