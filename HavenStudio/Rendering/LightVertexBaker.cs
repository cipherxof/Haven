using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia3DControl.Core.Models;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public static class LightVertexBaker
{
    public sealed record SpatialBakeInput(
        float[] Positions,
        float[] Normals,
        float[] BaseColors,
        int VertexCount,
        Matrix4 ModelMatrix);

    public sealed record BakedLighting(float[] Colors, float[] ShadowedColors);

    private static readonly ConditionalWeakTable<Model3D, SourceData> Sources = new();

    public static void Register(Model3D model, float[] normals, float[] baseColors)
    {
        ArgumentNullException.ThrowIfNull(model);
        Sources.Remove(model);
        Sources.Add(model, new SourceData((float[])normals.Clone(), (float[])baseColors.Clone()));
    }

    public static bool Apply(Model3D model, SampledLighting lighting)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(lighting);
        if (!Sources.TryGetValue(model, out var source) ||
            source.Normals.Length < model.VertexCount * 3 || model.VertexCount <= 0)
        {
            return false;
        }

        return ApplyBakedLighting(
            model,
            BakeLighting(
                source.Normals,
                source.BaseColors,
                model.VertexCount,
                model.GetModelMatrix(),
                lighting));
    }

    public static bool ApplySpatial(
        Model3D model,
        Func<Vector3, SampledLighting> sampleAtWorldPosition,
        bool modulateBaseColor)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(sampleAtWorldPosition);
        var input = CaptureSpatialBake(model);
        if (input == null)
        {
            return false;
        }

        return ApplyBakedLighting(
            model,
            BakeSpatialLighting(
                input.Positions,
                input.Normals,
                input.BaseColors,
                input.VertexCount,
                input.ModelMatrix,
                sampleAtWorldPosition,
                modulateBaseColor));
    }

    public static SpatialBakeInput? CaptureSpatialBake(Model3D model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!Sources.TryGetValue(model, out var source) ||
            source.Normals.Length < model.VertexCount * 3 ||
            model.Positions.Length < model.VertexCount * 3 ||
            model.VertexCount <= 0)
        {
            return null;
        }

        return new SpatialBakeInput(
            model.Positions,
            source.Normals,
            source.BaseColors,
            model.VertexCount,
            model.GetModelMatrix());
    }

    public static bool ApplyBakedColors(Model3D model, float[] colors)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(colors);
        return ApplyBakedLighting(
            model,
            new BakedLighting(colors, ExtractRgb(colors, model.VertexCount)));
    }

    public static bool ApplyBakedLighting(Model3D model, BakedLighting lighting)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(lighting);
        if (model.VertexCount <= 0 ||
            lighting.Colors.Length != model.VertexCount * 4 ||
            lighting.ShadowedColors.Length != model.VertexCount * 3)
        {
            return false;
        }

        model.Colors = lighting.Colors;
        model.ShadowedColors = lighting.ShadowedColors;
        model.VerticesNeedUpdate = true;
        return true;
    }

    public static bool Restore(Model3D model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!Sources.TryGetValue(model, out var source))
        {
            return false;
        }
        model.Colors = (float[])source.BaseColors.Clone();
        model.ShadowedColors = Array.Empty<float>();
        model.VerticesNeedUpdate = true;
        return true;
    }

    public static float[] BakeColors(
        float[] normals,
        float[] baseColors,
        int vertexCount,
        Matrix4 modelMatrix,
        SampledLighting lighting) =>
        BakeLighting(normals, baseColors, vertexCount, modelMatrix, lighting).Colors;

    public static BakedLighting BakeLighting(
        float[] normals,
        float[] baseColors,
        int vertexCount,
        Matrix4 modelMatrix,
        SampledLighting lighting)
    {
        ArgumentNullException.ThrowIfNull(normals);
        ArgumentNullException.ThrowIfNull(baseColors);
        ArgumentNullException.ThrowIfNull(lighting);
        if (vertexCount < 0 || normals.Length < vertexCount * 3)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexCount));
        }

        var colors = new float[vertexCount * 4];
        var shadowedColors = new float[vertexCount * 3];
        for (var index = 0; index < vertexCount; index++)
        {
            var baseColor = Vector3.One;
            var shaded = Shade(ReadWorldNormal(normals, index, modelMatrix), lighting);
            WriteVertex(
                colors,
                shadowedColors,
                index,
                shaded.Total,
                shaded.ProjectedShadow,
                baseColor,
                ReadAlpha(baseColors, vertexCount, index));
        }
        return new BakedLighting(colors, shadowedColors);
    }

    public static float[] BakeSpatialColors(
        float[] positions,
        float[] normals,
        float[] baseColors,
        int vertexCount,
        Matrix4 modelMatrix,
        Func<Vector3, SampledLighting> sampleAtWorldPosition,
        bool modulateBaseColor,
        CancellationToken cancellationToken = default) =>
        BakeSpatialLighting(
            positions,
            normals,
            baseColors,
            vertexCount,
            modelMatrix,
            sampleAtWorldPosition,
            modulateBaseColor,
            cancellationToken).Colors;

    public static BakedLighting BakeSpatialLighting(
        float[] positions,
        float[] normals,
        float[] baseColors,
        int vertexCount,
        Matrix4 modelMatrix,
        Func<Vector3, SampledLighting> sampleAtWorldPosition,
        bool modulateBaseColor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(normals);
        ArgumentNullException.ThrowIfNull(baseColors);
        ArgumentNullException.ThrowIfNull(sampleAtWorldPosition);
        if (vertexCount < 0 || positions.Length < vertexCount * 3 || normals.Length < vertexCount * 3)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexCount));
        }

        var colors = new float[vertexCount * 4];
        var shadowedColors = new float[vertexCount * 3];
        for (var index = 0; index < vertexCount; index++)
        {
            if ((index & 0xFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            var offset = index * 3;
            var localPosition = new Vector3(
                positions[offset],
                positions[offset + 1],
                positions[offset + 2]);
            var worldPosition = Vector3.TransformPosition(localPosition, modelMatrix);
            var worldNormal = ReadWorldNormal(normals, index, modelMatrix);
            var sampledLighting = sampleAtWorldPosition(worldPosition);
            var shaded = Shade(worldNormal, sampledLighting);
            var baseColor = modulateBaseColor
                ? ReadBaseColor(baseColors, vertexCount, index)
                : Vector3.One;

            WriteVertex(
                colors,
                shadowedColors,
                index,
                shaded.Total,
                shaded.ProjectedShadow,
                baseColor,
                ReadAlpha(baseColors, vertexCount, index));
        }
        return new BakedLighting(colors, shadowedColors);
    }

    private static void WriteVertex(
        float[] colors,
        float[] shadowedColors,
        int index,
        Vector3 totalLighting,
        Vector3 projectedShadowLighting,
        Vector3 baseColor,
        float alpha)
    {
        // Keep both endpoints of the LT3 lighting equation as RGB: interpolating
        // the lit and shadowed results disables only the projected sun while
        // preserving every other LT3 light, and keeps HDR energy (ambient plus
        // sun often exceeds 1.0) instead of clamping before texture modulation.
        //
        // On the standard stage path the vertex-colour weights are zero: the
        // MDN's own vertex colour does not modulate the RGB result. The lighting
        // is written into the colour buffer and the texture is modulated by it
        // downstream, so multiplying by the vertex colour here would apply it a
        // second time.
        var vcolorWeight = Vector3.Zero;
        var modulation = Vector3.One + vcolorWeight * (baseColor - Vector3.One);
        var lit = ClampNonNegative(totalLighting * modulation);
        var shadowed = ClampNonNegative(
            (totalLighting - projectedShadowLighting) * modulation);

        var colorOffset = index * 4;
        colors[colorOffset] = lit.X;
        colors[colorOffset + 1] = lit.Y;
        colors[colorOffset + 2] = lit.Z;
        colors[colorOffset + 3] = alpha;

        var shadowOffset = index * 3;
        shadowedColors[shadowOffset] = shadowed.X;
        shadowedColors[shadowOffset + 1] = shadowed.Y;
        shadowedColors[shadowOffset + 2] = shadowed.Z;
    }

    private static Vector3 ReadWorldNormal(float[] normals, int index, Matrix4 modelMatrix)
    {
        var offset = index * 3;
        var localNormal = new Vector3(normals[offset], normals[offset + 1], normals[offset + 2]);
        var normal = Vector3.TransformNormal(localNormal, modelMatrix);
        return normal.LengthSquared > 0.000001f ? Vector3.Normalize(normal) : Vector3.UnitY;
    }

    private static ShadedLighting Shade(Vector3 normal, SampledLighting lighting)
    {
        var total = lighting.SampleAmbient(normal);
        var projectedShadow = Vector3.Zero;
        // Sum N.L over every in-range LT3 record. The 3-slot DirectionalLights
        // set is the runtime limit and must not gate the stage bake, or the
        // surface receives near-uniform fill and loses all directional contrast.
        foreach (var light in lighting.BakeLights)
        {
            var contribution = light.Color * MathF.Max(0, Vector3.Dot(normal, light.Direction));
            total += contribution;
            if (light.CastsProjectedShadow)
            {
                projectedShadow += contribution;
            }
        }
        return new ShadedLighting(total, projectedShadow);
    }

    private static Vector3 ReadBaseColor(float[] colors, int vertexCount, int index)
    {
        if (colors.Length >= vertexCount * 4)
        {
            var offset = index * 4;
            return new Vector3(colors[offset], colors[offset + 1], colors[offset + 2]);
        }
        if (colors.Length >= vertexCount * 3)
        {
            var offset = index * 3;
            return new Vector3(colors[offset], colors[offset + 1], colors[offset + 2]);
        }
        return Vector3.One;
    }

    private static float ReadAlpha(float[] colors, int vertexCount, int index)
    {
        if (colors.Length >= vertexCount * 4)
        {
            return colors[index * 4 + 3];
        }
        return 1f;
    }

    private static Vector3 ClampNonNegative(Vector3 value) => new(
        MathF.Max(0f, value.X),
        MathF.Max(0f, value.Y),
        MathF.Max(0f, value.Z));

    private static Vector3 Clamp01(Vector3 value) => new(
        Math.Clamp(value.X, 0f, 1f),
        Math.Clamp(value.Y, 0f, 1f),
        Math.Clamp(value.Z, 0f, 1f));

    private static float[] ExtractRgb(float[] colors, int vertexCount)
    {
        var result = new float[Math.Max(0, vertexCount) * 3];
        for (var index = 0; index < vertexCount; index++)
        {
            var sourceOffset = index * 4;
            var destinationOffset = index * 3;
            if (sourceOffset + 2 >= colors.Length)
            {
                break;
            }
            result[destinationOffset] = colors[sourceOffset];
            result[destinationOffset + 1] = colors[sourceOffset + 1];
            result[destinationOffset + 2] = colors[sourceOffset + 2];
        }
        return result;
    }

    private readonly record struct ShadedLighting(Vector3 Total, Vector3 ProjectedShadow);
    private sealed record SourceData(float[] Normals, float[] BaseColors);
}
