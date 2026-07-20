using System.Collections.Generic;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class MdnFaceBuffer
{
    public List<short> Indices { get; } = new();
    
    public static MdnFaceBuffer ReadFrom(EndianBinaryReader r, IReadOnlyList<MdnFace> faces, MdnVertexIndex vi)
    {
        var fb = new MdnFaceBuffer();

        for (int j = 0; j < vi.FaceSectionCount; j++)
        {
            var face = faces[vi.FaceSectionStart + j];
            for (int k = 0; k < (face.Count / 3); k++)
            {
                fb.Indices.Add(r.ReadInt16());
                fb.Indices.Add(r.ReadInt16());
                fb.Indices.Add(r.ReadInt16());
            }
        }

        return fb;
    }

    public void WriteTo(EndianBinaryWriter w, IReadOnlyList<MdnFace> faces, MdnVertexIndex vi)
    {
        int idx = 0;

        for (int j = 0; j < vi.FaceSectionCount; j++)
        {
            var face = faces[vi.FaceSectionStart + j];
            for (int k = 0; k < (face.Count / 3); k++)
            {
                w.WriteInt16(Indices[idx++]);
                w.WriteInt16(Indices[idx++]);
                w.WriteInt16(Indices[idx++]);
            }
        }
    }
}