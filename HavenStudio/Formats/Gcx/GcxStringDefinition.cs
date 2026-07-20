namespace HavenStudio.Formats.Gcx;

public class GcxStringDefinition
{
    public int Type { get; set; }
    public int Offset { get; set; }
    public int Index { get; set; }

    public GcxScript? Script { get; set; }
    public string? Value { get; set; }

    public GcxStringDefinition(int type, int offset)
    {
        Type = type;
        Offset = offset;
    }

    public static GcxStringDefinition FromPacked(int def)
    {
        int type = (int)((uint)def >> 24);
        int offset = def & 0x00FF_FFFF;
        return new GcxStringDefinition(type, offset);
    }
}