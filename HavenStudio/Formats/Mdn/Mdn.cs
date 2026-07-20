using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Mdn;

public sealed class Mdn
{
    public bool BigEndian { get; set; } = true;
    public int TxnFilenameHash { get; set; }
    
    public MdnHeader Header { get; private set; } = new();
    public MdnBounds? Bounds { get; set; }

    public List<MdnBone> Bones { get; } = new();
    public List<MdnMeshGroup> MeshGroups { get; } = new();
    public List<MdnVertexIndex> VertexIndices { get; } = new();
    public List<MdnFace> Faces { get; } = new();
    public List<MdnMaterial> Materials { get; } = new();
    public List<MdnTexture> Textures { get; } = new();
    public List<MdnBonePalette> BonePalettes { get; } = new();
    public List<MdnVertexBuffer> VertexBuffers { get; } = new();
    public List<MdnFaceBuffer> FaceBuffers { get; } = new();

    public void ReadFrom(Stream stream)
    {
        Stream s = stream;
        if (!stream.CanSeek)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            s = ms;

            // Proceed reading from buffered stream; but we must not dispose ms until we're done.
            // So we do the work in a helper.
            ReadFromSeekable(ms);
            return;
        }

        ReadFromSeekable(s);
    }

    private void ReadFromSeekable(Stream stream)
    {
        // Endian detection: if int32 at 0x5c (big-endian) != fileSize, then file is little-endian.
        long start = stream.Position;
        long fileSize = stream.Length - start;

        BigEndian = DetectBigEndian(stream, start, (int)fileSize);
        var r = new EndianBinaryReader(stream, BigEndian ? Endianness.Big : Endianness.Little);

        // Magic
        int magic = r.ReadInt32();
        if (magic != MdnFile.Magic)
            throw new InvalidDataException("This is not an MDN file.");

        TxnFilenameHash = r.ReadInt32();

        // Counts + offsets + buffer info + file size are part of header
        Header = MdnHeader.ReadFrom(r);

        if (Header.MeshCount != Header.VertexDefinitionCount)
            throw new InvalidDataException(
                $"meshCount ({Header.MeshCount}) != vertexDefinitionCount ({Header.VertexDefinitionCount})");

        // Bounds
        Bounds = MdnBounds.ReadFrom(r);

        // Bones
        Bones.Clear();
        for (int i = 0; i < Header.BoneCount; i++)
            Bones.Add(MdnBone.ReadFrom(r));

        // Groups
        MeshGroups.Clear();
        for (int i = 0; i < Header.MeshGroupCount; i++)
            MeshGroups.Add(MdnMeshGroup.ReadFrom(r));

        // Vertex indices
        VertexIndices.Clear();
        for (int i = 0; i < Header.MeshCount; i++)
            VertexIndices.Add(MdnVertexIndex.ReadFrom(r));

        // Faces
        Faces.Clear();
        for (int i = 0; i < Header.FaceCount; i++)
            Faces.Add(MdnFace.ReadFrom(r));

        // Vertex buffer definitions
        VertexBuffers.Clear();
        for (int i = 0; i < Header.MeshCount; i++)
            VertexBuffers.Add(MdnVertexBuffer.ReadDefinitionFrom(r));

        // Materials
        Materials.Clear();
        for (int i = 0; i < Header.MaterialCount; i++)
            Materials.Add(MdnMaterial.ReadFrom(r));

        // Textures
        Textures.Clear();
        for (int i = 0; i < Header.TextureCount; i++)
            Textures.Add(MdnTexture.ReadFrom(r));

        // Bone palettes
        BonePalettes.Clear();
        for (int i = 0; i < Header.BonePaletteCount; i++)
            BonePalettes.Add(MdnBonePalette.ReadFrom(r));

        r.Align(0x10);

        // Vertex buffer payload
        for (int i = 0; i < Header.MeshCount; i++)
        {
            var vb = VertexBuffers[i];
            var vi = VertexIndices[i];
            vb.ReadVertexDataFrom(r, vi.VertexCount);
            r.Align(0x10);
        }

        // Face buffers
        FaceBuffers.Clear();
        for (int i = 0; i < Header.MeshCount; i++)
            FaceBuffers.Add(MdnFaceBuffer.ReadFrom(r, Faces, VertexIndices[i]));

        r.Align(0x10);
    }

    public void WriteTo(Stream stream, Endianness endianness)
    {
        if (!stream.CanSeek)
            throw new ArgumentException(
                "WriteTo requires a seekable stream so offsets can be patched (use a MemoryStream).", nameof(stream));

        using var w = new EndianBinaryWriter(stream, endianness, leaveOpen: true);
        BigEndian = endianness == Endianness.Big;

        // Magic + hash
        w.WriteInt32(MdnFile.Magic);
        w.WriteInt32(TxnFilenameHash);

        // Counts (derived from lists)
        var header = new MdnHeader
        {
            BoneCount = Bones.Count,
            MeshGroupCount = MeshGroups.Count,
            MeshCount = VertexIndices.Count,
            FaceCount = Faces.Count,
            VertexDefinitionCount = VertexIndices.Count,
            MaterialCount = Materials.Count,
            TextureCount = Textures.Count,
            BonePaletteCount = BonePalettes.Count,
        };

        if (VertexBuffers.Count != header.MeshCount)
            throw new InvalidDataException("VertexBuffers.Count must match VertexIndices.Count.");
        if (FaceBuffers.Count != header.MeshCount)
            throw new InvalidDataException("FaceBuffers.Count must match VertexIndices.Count.");

        // Offsets placeholders
        long countsPos = w.BaseStream.Position;
        header.WriteCountsAndOffsetsPlaceholdersTo(w);
        long offsetsPos = countsPos + 0x20;

        // Buffer info placeholders
        long bufferInfoPos = w.BaseStream.Position;
        header.WriteBufferInfoPlaceholdersTo(w);

        // File size placeholder
        long fileSizePos = w.BaseStream.Position;
        w.WriteInt32(0);

        // Bounds
        (Bounds ?? throw new InvalidDataException("MDN bounds are required.")).WriteTo(w);

        // Bones offset
        header.BonesOffset = (int)w.BaseStream.Position;
        foreach (var b in Bones) b.WriteTo(w);

        // Groups offset
        header.MeshGroupsOffset = (int)w.BaseStream.Position;
        foreach (var g in MeshGroups) g.WriteTo(w);

        // Vertex indices offset
        header.MeshesOffset = (int)w.BaseStream.Position;
        foreach (var vi in VertexIndices) vi.WriteTo(w);

        // Faces offset
        header.FacesOffset = (int)w.BaseStream.Position;
        foreach (var f in Faces) f.WriteTo(w);

        // Vertex definitions offset
        header.VertexElementsOffset = (int)w.BaseStream.Position;
        long vertexDefinitionsStart = w.BaseStream.Position;
        for (int i = 0; i < VertexBuffers.Count; i++)
            VertexBuffers[i].WriteDefinitionTo(w);

        // Materials offset
        header.MaterialsOffset = (int)w.BaseStream.Position;
        foreach (var m in Materials) m.WriteTo(w);

        // Textures offset
        header.TexturesOffset = (int)w.BaseStream.Position;
        foreach (var t in Textures) t.WriteTo(w);

        // Bone palettes offset
        header.BonePalettesOffset = (int)w.BaseStream.Position;
        foreach (var bp in BonePalettes) bp.WriteTo(w);

        w.Align(0x10);

        // Vertex buffer payload offset
        header.VertexBufferOffset = (int)w.BaseStream.Position;

        for (int i = 0; i < header.MeshCount; i++)
        {
            int rel = (int)(w.BaseStream.Position - header.VertexBufferOffset);
            long patchPos = vertexDefinitionsStart + i * 0x30 + 0x0C;
            long save = w.BaseStream.Position;
            w.BaseStream.Position = patchPos;
            w.WriteInt32(rel);
            w.BaseStream.Position = save;

            VertexBuffers[i].WriteVertexDataTo(w, VertexIndices[i].VertexCount);
            w.Align(0x10);
        }

        header.VertexBufferSize = (int)(w.BaseStream.Position - header.VertexBufferOffset);

        // Face buffer payload offset
        header.FaceBufferOffset = (int)w.BaseStream.Position;
        for (int i = 0; i < header.MeshCount; i++)
            FaceBuffers[i].WriteTo(w, Faces, VertexIndices[i]);

        w.Align(0x10);
        header.FaceBufferSize = (int)(w.BaseStream.Position - header.FaceBufferOffset);

        // File size
        header.FileSize = (int)w.BaseStream.Position;

        // Patch header offsets + buffer info + file size
        long end = w.BaseStream.Position;

        w.BaseStream.Position = offsetsPos;
        header.WriteOffsetsTo(w);

        w.BaseStream.Position = bufferInfoPos;
        header.WriteBufferInfoTo(w);

        w.BaseStream.Position = fileSizePos;
        w.WriteInt32(header.FileSize);

        w.BaseStream.Position = end;

        Header = header;
        w.Flush();
    }

    private static bool DetectBigEndian(Stream stream, long basePos, int fileSize)
    {
        long save = stream.Position;
        try
        {
            stream.Position = basePos + 0x5C;
            Span<byte> tmp = stackalloc byte[4];
            ReadExactly(stream, tmp);
            int be = BinaryPrimitives.ReadInt32BigEndian(tmp);
            return be == fileSize;
        }
        finally
        {
            stream.Position = save;
        }
    }

    internal static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        int readTotal = 0;
        while (readTotal < buffer.Length)
        {
            int n = stream.Read(buffer.Slice(readTotal));
            if (n <= 0) throw new EndOfStreamException();
            readTotal += n;
        }
    }
}
