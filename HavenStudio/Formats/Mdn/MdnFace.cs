using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class MdnFace
{
    public short Type { get; set; }
    public short Count { get; set; }
    public int Offset { get; set; }
    public int Group { get; set; }
    public short VertexStart { get; set; }
    public short VertexLength { get; set; }
    
    public static MdnFace ReadFrom(EndianBinaryReader r) => new()
    {
        Type = r.ReadInt16(),
        Count = r.ReadInt16(),
        Offset = r.ReadInt32(),
        Group = r.ReadInt32(),
        VertexStart = r.ReadInt16(),
        VertexLength = r.ReadInt16()
    };

    public void WriteTo(EndianBinaryWriter w)
    {
        w.WriteInt16(Type);
        w.WriteInt16(Count);
        w.WriteInt32(Offset);
        w.WriteInt32(Group);
        w.WriteInt16(VertexStart);
        w.WriteInt16(VertexLength);
    }
}