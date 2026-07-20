using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class MdnMeshGroup
{
    public int NameHash { get; set; }
    public int Parent { get; set; }
    
    public int Flags { get; set; }

    public int Unk2 { get; set; }
    
    public static MdnMeshGroup ReadFrom(EndianBinaryReader r) => new()
    {
        NameHash = r.ReadInt32(),
        Flags = r.ReadInt32(),
        Parent = r.ReadInt32(),
        Unk2 = r.ReadInt32(),
    };

    public void WriteTo(EndianBinaryWriter w)
    {
        w.WriteInt32(NameHash);
        w.WriteInt32(Flags);
        w.WriteInt32(Parent);
        w.WriteInt32(Unk2);
    }
}