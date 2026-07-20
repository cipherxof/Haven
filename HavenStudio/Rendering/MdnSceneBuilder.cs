using System;
using System.Collections.Generic;
using Avalonia3DControl.Core.Models;
using HavenStudio.Formats.Mdn;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public static class MdnSceneBuilder
{
    public static List<Model3D> BuildModels(Mdn mdn)
    {
        var models = new List<Model3D>();

        for (int i = 0; i < mdn.VertexBuffers.Count; i++)
        {
            var vb = mdn.VertexBuffers[i];
            var vi = mdn.VertexIndices[i];
            var fb = mdn.FaceBuffers[i];

            var positionElement = vb.GetElementByType(MdnVertexBuffer.TypePositions);
            var positions = positionElement.GetFloatData();
            if (positions.Count < vi.VertexCount * 3)
            {
                continue;
            }

            var positionData = new float[vi.VertexCount * 3];
            positions.CopyTo(0, positionData, 0, positionData.Length);

            var colorData = BuildColors(vb, vi.VertexCount);
            var normalData = BuildNormals(vb, vi.VertexCount);
            var uvData = BuildUvs(vb, vi.VertexCount);

            int baseOffsetBytes = int.MaxValue;
            for (int j = 0; j < vi.FaceSectionCount; j++)
            {
                var face = mdn.Faces[vi.FaceSectionStart + j];
                if (face.Offset < baseOffsetBytes)
                {
                    baseOffsetBytes = face.Offset;
                }
            }
            if (baseOffsetBytes == int.MaxValue)
            {
                baseOffsetBytes = 0;
            }

            // Group packets by (material, render-state) while preserving the order in which
            // they first appear in the file, so the engine's painter-order layering (opaque
            // first, then blend layers in packet order) is reproduced by the two-pass renderer.
            var batches = new List<PacketBatch>();
            var lookup = new Dictionary<(int Group, int Flag), PacketBatch>();

            for (int j = 0; j < vi.FaceSectionCount; j++)
            {
                var face = mdn.Faces[vi.FaceSectionStart + j];
                int start = (face.Offset - baseOffsetBytes) / 2;
                int count = face.Count;
                if (start < 0 || start + count > fb.Indices.Count)
                {
                    continue;
                }

                int flag = face.Type & 0xFFFF;
                var key = (face.Group, flag);
                if (!lookup.TryGetValue(key, out var batch))
                {
                    batch = new PacketBatch(face.Group, flag);
                    lookup[key] = batch;
                    batches.Add(batch);
                }

                for (int k = 0; k < count; k++)
                {
                    batch.Indices.Add((uint)(ushort)fb.Indices[start + k]);
                }
            }

            if (batches.Count == 0)
            {
                var indices = new uint[fb.Indices.Count];
                for (int idx = 0; idx < fb.Indices.Count; idx++)
                {
                    indices[idx] = (uint)(ushort)fb.Indices[idx];
                }

                var model = new Model3D
                {
                    Name = $"MDN_Mesh_{i}",
                    Positions = positionData,
                    Colors = colorData,
                    UVs = uvData,
                    Indices = indices,
                    VertexCount = vi.VertexCount,
                    IndexCount = indices.Length,
                    Color = new Vector3(1.0f, 1.0f, 1.0f),
                    Alpha = 1.0f,
                    MaterialIndex = -1
                };
                models.Add(model);
                LightVertexBaker.Register(model, normalData, colorData);

                continue;
            }

            foreach (var batch in batches)
            {
                var indices = batch.Indices.ToArray();
                var model = new Model3D
                {
                    Name = $"MDN_Mesh_{i}_Mat_{batch.Group}_F{batch.Flag:X4}",
                    Positions = positionData,
                    Colors = colorData,
                    UVs = uvData,
                    Indices = indices,
                    VertexCount = vi.VertexCount,
                    IndexCount = indices.Length,
                    Color = new Vector3(1.0f, 1.0f, 1.0f),
                    Alpha = 1.0f,
                    MaterialIndex = batch.Group
                };
                ApplyPacketFlag(model, batch.Flag);
                models.Add(model);
                LightVertexBaker.Register(model, normalData, colorData);
            }
        }

        return models;
    }

    private sealed class PacketBatch
    {
        public PacketBatch(int group, int flag)
        {
            Group = group;
            Flag = flag;
        }

        public int Group { get; }
        public int Flag { get; }
        public List<uint> Indices { get; } = new();
    }

    // MDN packet flag bits, decoded from DG_ChainModel / _DG_DrawModelStage / DG_SetBlendMode.
    private const int FlagBlendEnable  = 0x10;   // bits 0-3 select the blend equation
    private const int FlagNoDepthWrite = 0x200;  // terrain/decal blend layers
    private const int FlagAlphaTest50  = 0x400;  // foliage cutout

    private static void ApplyPacketFlag(Model3D model, int flag)
    {
        if ((flag & FlagBlendEnable) != 0)
        {
            // Blend layer: composite over what is already drawn using the vertex-alpha mask,
            // skip depth writes when bit 0x200 is set, and discard fully transparent texels
            // (the engine turns on an alpha-test NOTEQUAL 0 whenever blending is enabled).
            model.BlendEnabled = true;
            model.BlendMode = (ModelBlendMode)(flag & 0xF);
            model.UseVertexAlpha = true;
            model.ForceOpaqueAlpha = false;
            model.WriteDepth = (flag & FlagNoDepthWrite) == 0;
            model.AlphaTestRef = 1.0f / 255.0f;
        }
        else if ((flag & FlagAlphaTest50) != 0)
        {
            // Alpha-tested cutout (foliage): hard 50% cut against the texture alpha, solid
            // pixels, normal depth write, no blending.
            model.BlendEnabled = false;
            model.UseVertexAlpha = false;
            model.ForceOpaqueAlpha = true;
            model.WriteDepth = true;
            model.AlphaTestRef = 0.5f;
        }
        else
        {
            // Opaque: solid, depth-writing, coverage taken from neither vertex nor texture alpha.
            model.BlendEnabled = false;
            model.UseVertexAlpha = false;
            model.ForceOpaqueAlpha = true;
            model.WriteDepth = true;
            model.AlphaTestRef = 0.0f;
        }
    }

    private static float[] BuildNormals(MdnVertexBuffer vb, int vertexCount)
    {
        var element = vb.GetElementByType(MdnVertexBuffer.TypeNormals);
        var normals = new float[vertexCount * 3];
        switch (element.Format)
        {
            case MdnVertexBuffer.FormatFloat:
            {
                var data = element.GetFloatData();
                if (data.Count < normals.Length)
                {
                    return Array.Empty<float>();
                }
                data.CopyTo(0, normals, 0, normals.Length);
                break;
            }
            case MdnVertexBuffer.FormatByte8:
            case MdnVertexBuffer.FormatByte9:
            {
                var data = element.GetByteData();
                if (data.Count < vertexCount * 4)
                {
                    return Array.Empty<float>();
                }
                for (var index = 0; index < vertexCount; index++)
                {
                    var source = index * 4;
                    var target = index * 3;
                    normals[target] = data[source] / 127.5f - 1f;
                    normals[target + 1] = data[source + 1] / 127.5f - 1f;
                    normals[target + 2] = data[source + 2] / 127.5f - 1f;
                }
                break;
            }
            case MdnVertexBuffer.FormatDecTri:
            {
                var data = element.GetIntData();
                if (data.Count < vertexCount)
                {
                    return Array.Empty<float>();
                }
                for (var index = 0; index < vertexCount; index++)
                {
                    var packed = data[index];
                    var target = index * 3;
                    normals[target] = SignExtend10(packed) / 511f;
                    normals[target + 1] = SignExtend10(packed >> 10) / 511f;
                    normals[target + 2] = SignExtend10(packed >> 20) / 511f;
                }
                break;
            }
            case MdnVertexBuffer.FormatHalfFloat:
            {
                var data = element.GetShortData();
                if (data.Count < vertexCount * 2)
                {
                    return Array.Empty<float>();
                }
                for (var index = 0; index < vertexCount; index++)
                {
                    var target = index * 3;
                    var x = (float)BitConverter.UInt16BitsToHalf((ushort)data[index * 2]);
                    var y = (float)BitConverter.UInt16BitsToHalf((ushort)data[index * 2 + 1]);
                    normals[target] = x;
                    normals[target + 1] = y;
                    normals[target + 2] = MathF.Sqrt(MathF.Max(0, 1f - x * x - y * y));
                }
                break;
            }
            default:
                return Array.Empty<float>();
        }

        for (var index = 0; index < vertexCount; index++)
        {
            var offset = index * 3;
            var normal = new Vector3(normals[offset], normals[offset + 1], normals[offset + 2]);
            if (normal.LengthSquared > 0.000001f)
            {
                normal = Vector3.Normalize(normal);
                normals[offset] = normal.X;
                normals[offset + 1] = normal.Y;
                normals[offset + 2] = normal.Z;
            }
        }
        return normals;

        static int SignExtend10(int value)
        {
            value &= 0x3FF;
            return (value & 0x200) != 0 ? value - 0x400 : value;
        }
    }

    private static float[] BuildColors(MdnVertexBuffer vb, int vertexCount)
    {
        var element = vb.GetElementByType(MdnVertexBuffer.TypeColors);
        if (element == null)
        {
            return Array.Empty<float>();
        }

        // Check if format has 3 or 4 components
        if (element.Format == MdnVertexBuffer.FormatFloat)
        {
            var data = element.GetFloatData();
            // Try RGBA first (4 components)
            if (data.Count >= vertexCount * 4)
            {
                var colors = new float[vertexCount * 4];
                data.CopyTo(0, colors, 0, colors.Length);
                return colors;
            }
            // Fall back to RGB (3 components)
            else if (data.Count >= vertexCount * 3)
            {
                var colors = new float[vertexCount * 3];
                data.CopyTo(0, colors, 0, colors.Length);
                return colors;
            }
        }
        else if (element.Format == MdnVertexBuffer.FormatByte8 || element.Format == MdnVertexBuffer.FormatByte9)
        {
            var data = element.GetByteData();
            if (data.Count >= vertexCount * 4)
            {
                // RGBA. MDN vertex colors are baked lighting on the PS2-era x2-modulate scale
                // where byte 0x80 == 1.0, so RGB decodes as byte/128 (clamped); real stage data
                // never exceeds 0x7F, so this stays within [0,1]. Dividing by 255 instead renders
                // the whole stage at half brightness. Alpha is the terrain layer blend mask and
                // keeps the ordinary 0-255 scale.
                var colors = new float[vertexCount * 4];
                for (int i = 0; i < vertexCount; i++)
                {
                    int src = i * 4;
                    int dst = i * 4;
                    colors[dst] = MathF.Min(1f, data[src] / 128f);       // R
                    colors[dst + 1] = MathF.Min(1f, data[src + 1] / 128f); // G
                    colors[dst + 2] = MathF.Min(1f, data[src + 2] / 128f); // B
                    colors[dst + 3] = data[src + 3] / 255f;                // A (layer blend mask)
                }
                return colors;
            }
        }

        return Array.Empty<float>();
    }

    private static float[] BuildUvs(MdnVertexBuffer vb, int vertexCount)
    {
        var element = vb.GetElementByType(MdnVertexBuffer.TypeTextureCoords0);
        if (element == null)
        {
            return Array.Empty<float>();
        }

        if (element.Format == MdnVertexBuffer.FormatFloat)
        {
            var data = element.GetFloatData();
            if (data.Count >= vertexCount * 2)
            {
                int stride = data.Count >= vertexCount * 3 ? 3 : 2;
                var uvs = new float[vertexCount * 2];
                for (int i = 0; i < vertexCount; i++)
                {
                    int src = i * stride;
                    int dst = i * 2;
                    uvs[dst] = data[src];
                    uvs[dst + 1] = data[src + 1];
                }
                return uvs;
            }
        }
        else if (element.Format == MdnVertexBuffer.FormatHalfFloat)
        {
            var data = element.GetShortData();
            if (data.Count >= vertexCount * 2)
            {
                var uvs = new float[vertexCount * 2];
                for (int i = 0; i < vertexCount; i++)
                {
                    int src = i * 2;
                    int dst = i * 2;
                    uvs[dst] = (float)BitConverter.UInt16BitsToHalf((ushort)data[src]);
                    uvs[dst + 1] = (float)BitConverter.UInt16BitsToHalf((ushort)data[src + 1]);
                }
                return uvs;
            }
        }
        else if (element.Format == MdnVertexBuffer.FormatByte8 || element.Format == MdnVertexBuffer.FormatByte9)
        {
            var data = element.GetByteData();
            if (data.Count >= vertexCount * 2)
            {
                var uvs = new float[vertexCount * 2];
                for (int i = 0; i < vertexCount; i++)
                {
                    int src = i * 2;
                    int dst = i * 2;
                    uvs[dst] = data[src] / 255f;
                    uvs[dst + 1] = (data[src + 1] / 255f);
                }
                return uvs;
            }
        }

        return Array.Empty<float>();
    }
}
