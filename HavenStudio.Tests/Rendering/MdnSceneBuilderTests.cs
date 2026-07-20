using System.Linq;
using Avalonia3DControl.Core.Models;
using HavenStudio.Formats.Mdn;
using HavenStudio.Rendering;
using Xunit;

namespace HavenStudio.Tests.Rendering;

public sealed class MdnSceneBuilderTests
{
    [Fact]
    public void BuildModels_SplitsPacketsByRenderStateInFileOrder()
    {
        // Three consecutive packets: two opaque sharing a material (must merge), then a
        // blend+no-depth-write layer, then an alpha-tested cutout.
        var mdn = BuildMesh(
            vertexCount: 4,
            packets: new[]
            {
                (Flag: 0x8000, Group: 0),
                (Flag: 0x8000, Group: 0),
                (Flag: 0x8210, Group: 1),
                (Flag: 0x8400, Group: 2),
            });

        var models = MdnSceneBuilder.BuildModels(mdn);

        Assert.Equal(3, models.Count);

        // Packet order is preserved so the two-pass renderer layers correctly.
        var opaque = models[0];
        Assert.Equal(6, opaque.Indices.Length); // two same-state packets merged
        Assert.False(opaque.BlendEnabled);
        Assert.True(opaque.WriteDepth);
        Assert.True(opaque.ForceOpaqueAlpha);
        Assert.False(opaque.UseVertexAlpha);
        Assert.Equal(0f, opaque.AlphaTestRef);

        var blendLayer = models[1];
        Assert.True(blendLayer.BlendEnabled);
        Assert.Equal(ModelBlendMode.Alpha, blendLayer.BlendMode);
        Assert.False(blendLayer.WriteDepth);         // flag bit 0x200
        Assert.True(blendLayer.UseVertexAlpha);
        Assert.False(blendLayer.ForceOpaqueAlpha);
        Assert.True(blendLayer.AlphaTestRef > 0f && blendLayer.AlphaTestRef < 0.01f);

        var cutout = models[2];
        Assert.False(cutout.BlendEnabled);
        Assert.True(cutout.WriteDepth);
        Assert.True(cutout.ForceOpaqueAlpha);
        Assert.Equal(0.5f, cutout.AlphaTestRef);
    }

    [Fact]
    public void BuildModels_DecodesAdditiveBlendMode()
    {
        var mdn = BuildMesh(4, new[] { (Flag: 0x8011, Group: 0) }); // bit 0x10 + mode 1
        var model = Assert.Single(MdnSceneBuilder.BuildModels(mdn));
        Assert.True(model.BlendEnabled);
        Assert.Equal(ModelBlendMode.Additive, model.BlendMode);
    }

    [Fact]
    public void BuildModels_ScalesVertexColorsByOneOverOneTwentyEight()
    {
        // R=0x40 (half on the 0x80==1.0 scale), G=0x80 (full), B=0x00, A=0xFF (opaque mask).
        var mdn = BuildMesh(
            vertexCount: 4,
            packets: new[] { (Flag: 0x8000, Group: 0) },
            colorRgba: new byte[] { 0x40, 0x80, 0x00, 0xFF });

        var model = Assert.Single(MdnSceneBuilder.BuildModels(mdn));

        Assert.Equal(0.5f, model.Colors[0], 3);   // R 0x40 / 128
        Assert.Equal(1.0f, model.Colors[1], 3);   // G 0x80 / 128
        Assert.Equal(0.0f, model.Colors[2], 3);   // B
        Assert.Equal(1.0f, model.Colors[3], 3);   // A 0xFF / 255 (kept on 0-255 scale)
    }

    private static Mdn BuildMesh(
        int vertexCount,
        (int Flag, int Group)[] packets,
        byte[]? colorRgba = null)
    {
        var mdn = new Mdn();

        var vb = new MdnVertexBuffer();
        var positions = vb.GetElementByType(MdnVertexBuffer.TypePositions);
        positions.Format = MdnVertexBuffer.FormatFloat;
        vb.Elements.Add(positions);
        var colors = vb.GetElementByType(MdnVertexBuffer.TypeColors);
        colors.Format = MdnVertexBuffer.FormatByte8;
        vb.Elements.Add(colors);

        var rgba = colorRgba ?? new byte[] { 0x7F, 0x7F, 0x7F, 0xFF };
        for (int v = 0; v < vertexCount; v++)
        {
            positions.GetFloatData().Add(v);
            positions.GetFloatData().Add(0f);
            positions.GetFloatData().Add(0f);

            colors.GetByteData().Add(rgba[0]);
            colors.GetByteData().Add(rgba[1]);
            colors.GetByteData().Add(rgba[2]);
            colors.GetByteData().Add(rgba[3]);
        }

        var vi = new MdnVertexIndex
        {
            FaceSectionStart = 0,
            FaceSectionCount = packets.Length,
            VertexCount = vertexCount,
        };

        var fb = new MdnFaceBuffer();
        for (int p = 0; p < packets.Length; p++)
        {
            mdn.Faces.Add(new MdnFace
            {
                Type = (short)packets[p].Flag,
                Count = 3,
                Offset = p * 6, // three 16-bit indices per packet
                Group = packets[p].Group,
            });

            // Indices are ushort values in-range for the vertex buffer.
            fb.Indices.Add((short)(p % vertexCount));
            fb.Indices.Add((short)((p + 1) % vertexCount));
            fb.Indices.Add((short)((p + 2) % vertexCount));
        }

        mdn.VertexBuffers.Add(vb);
        mdn.VertexIndices.Add(vi);
        mdn.FaceBuffers.Add(fb);
        return mdn;
    }
}
