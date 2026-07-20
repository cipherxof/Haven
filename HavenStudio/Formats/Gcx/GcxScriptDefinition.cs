namespace HavenStudio.Formats.Gcx;

public class GcxScriptDefinition
{
    internal int PhysicalOrder { get; set; } = -1;

    public int Type { get; set; }
    public int Offset { get; set; }
    public GcxScript? Script { get; set; }

    public GcxScriptDefinition(int type, int offset)
    {
        Type = type;
        Offset = offset;
    }

    public static GcxScriptDefinition FromPacked(int def)
    {
        int type = (int)((uint)def >> 24);
        int offset = def & 0x00FF_FFFF;
        return new GcxScriptDefinition(type, offset);
    }
}
