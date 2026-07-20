using System.Collections.Generic;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class MdnBonePalette
{
    public short Unknown { get; set; }
    public List<byte> BoneIds { get; } = new();

    public static MdnBonePalette ReadFrom(EndianBinaryReader r)
    {
        var bp = new MdnBonePalette();

        r.Skip(0x2);
        bp.Unknown = r.ReadInt16();
        short count = r.ReadInt16();
        r.Skip(0x2);

        for (int i = 0; i < count; i++)
            bp.BoneIds.Add(r.ReadByte());

        r.Skip(0x20 - count);
        return bp;
    }

    public void WriteTo(EndianBinaryWriter w)
    {
        w.WriteZero(2);
        w.WriteInt16(Unknown);
        w.WriteInt16((short)BoneIds.Count);
        w.WriteZero(2);

        foreach (var id in BoneIds)
            w.Write(id);

        w.WriteZero(0x28 - 0x8 - BoneIds.Count); 
    }
}