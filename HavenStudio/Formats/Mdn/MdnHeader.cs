namespace HavenStudio.Formats.Mdn;

using HavenStudio.Extensions;

public sealed class MdnHeader
{
    // Counts
    public int BoneCount { get; set; }
    public int MeshGroupCount { get; set; }
    public int MeshCount { get; set; }
    public int FaceCount { get; set; }
    public int VertexDefinitionCount { get; set; }
    public int MaterialCount { get; set; }
    public int TextureCount { get; set; }
    public int BonePaletteCount { get; set; }

    // Offsets
    public int BonesOffset { get; set; }
    public int MeshGroupsOffset { get; set; }
    public int MeshesOffset { get; set; }
    public int FacesOffset { get; set; }
    public int VertexElementsOffset { get; set; }
    public int MaterialsOffset { get; set; }
    public int TexturesOffset { get; set; }
    public int BonePalettesOffset { get; set; }

    // Buffer offsets/sizes
    public int VertexBufferOffset { get; set; }
    public int VertexBufferSize { get; set; }
    public int FaceBufferOffset { get; set; }
    public int FaceBufferSize { get; set; }

    public int FileSize { get; set; }

    public static MdnHeader ReadFrom(EndianBinaryReader r)
    {
        var h = new MdnHeader
        {
            BoneCount = r.ReadInt32(),
            MeshGroupCount = r.ReadInt32(),
            MeshCount = r.ReadInt32(),
            FaceCount = r.ReadInt32(),
            VertexDefinitionCount = r.ReadInt32(),
            MaterialCount = r.ReadInt32(),
            TextureCount = r.ReadInt32(),
            BonePaletteCount = r.ReadInt32(),

            BonesOffset = r.ReadInt32(),
            MeshGroupsOffset = r.ReadInt32(),
            MeshesOffset = r.ReadInt32(),
            FacesOffset = r.ReadInt32(),
            VertexElementsOffset = r.ReadInt32(),
            MaterialsOffset = r.ReadInt32(),
            TexturesOffset = r.ReadInt32(),
            BonePalettesOffset = r.ReadInt32(),

            VertexBufferOffset = r.ReadInt32(),
            VertexBufferSize = r.ReadInt32(),
            FaceBufferOffset = r.ReadInt32(),
            FaceBufferSize = r.ReadInt32(),
        };

        r.Skip(4);

        h.FileSize = r.ReadInt32();
        return h;
    }
    
    public void WriteCountsAndOffsetsPlaceholdersTo(EndianBinaryWriter w)
    {
        w.WriteInt32(BoneCount);
        w.WriteInt32(MeshGroupCount);
        w.WriteInt32(MeshCount);
        w.WriteInt32(FaceCount);
        w.WriteInt32(VertexDefinitionCount);
        w.WriteInt32(MaterialCount);
        w.WriteInt32(TextureCount);
        w.WriteInt32(BonePaletteCount);

        // offsets placeholder (0x20)
        w.WriteZero(0x20);
    }

    public void WriteOffsetsTo(EndianBinaryWriter w)
    {
        w.WriteInt32(BonesOffset);
        w.WriteInt32(MeshGroupsOffset);
        w.WriteInt32(MeshesOffset);
        w.WriteInt32(FacesOffset);
        w.WriteInt32(VertexElementsOffset);
        w.WriteInt32(MaterialsOffset);
        w.WriteInt32(TexturesOffset);
        w.WriteInt32(BonePalettesOffset);
    }

    public void WriteBufferInfoPlaceholdersTo(EndianBinaryWriter w)
    {
        w.WriteZero(0x14); // vertexBufferOffset, vertexBufferSize, faceBufferOffset, faceBufferSize, pad?
    }

    public void WriteBufferInfoTo(EndianBinaryWriter w)
    {
        w.WriteInt32(VertexBufferOffset);
        w.WriteInt32(VertexBufferSize);
        w.WriteInt32(FaceBufferOffset);
        w.WriteInt32(FaceBufferSize);
        w.WriteZero(4);
    }
}