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

        model.Colors = BakeColors(
            source.Normals,
            source.BaseColors,
            model.VertexCount,
            model.GetModelMatrix(),
            lighting);
        model.VerticesNeedUpdate = true;
        return true;
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

        var colors = BakeSpatialColors(
            input.Positions,
            input.Normals,
            input.BaseColors,
            input.VertexCount,
            input.ModelMatrix,
            sampleAtWorldPosition,
            modulateBaseColor);
        return ApplyBakedColors(model, colors);
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
        if (model.VertexCount <= 0 || colors.Length != model.VertexCount * 4)
        {
            return false;
        }

        model.Colors = colors;
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
        model.VerticesNeedUpdate = true;
        return true;
    }

    public static float[] BakeColors(
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
        for (var index = 0; index < vertexCount; index++)
        {
            var colorOffset = index * 4;
            var color = Shade(ReadWorldNormal(normals, index, modelMatrix), lighting);
            colors[colorOffset] = Math.Clamp(color.X, 0, 1);
            colors[colorOffset + 1] = Math.Clamp(color.Y, 0, 1);
            colors[colorOffset + 2] = Math.Clamp(color.Z, 0, 1);
            colors[colorOffset + 3] = ReadAlpha(baseColors, vertexCount, index);
        }
        return colors;
    }

    public static float[] BakeSpatialColors(
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
            var color = Shade(
                ReadWorldNormal(normals, index, modelMatrix),
                sampleAtWorldPosition(worldPosition));
            if (modulateBaseColor)
            {
                color *= ReadBaseColor(baseColors, vertexCount, index);
            }

            var colorOffset = index * 4;
            colors[colorOffset] = Math.Clamp(color.X, 0, 1);
            colors[colorOffset + 1] = Math.Clamp(color.Y, 0, 1);
            colors[colorOffset + 2] = Math.Clamp(color.Z, 0, 1);
            colors[colorOffset + 3] = ReadAlpha(baseColors, vertexCount, index);
        }
        return colors;
    }

    private static Vector3 ReadWorldNormal(float[] normals, int index, Matrix4 modelMatrix)
    {
        var offset = index * 3;
        var localNormal = new Vector3(normals[offset], normals[offset + 1], normals[offset + 2]);
        var normal = Vector3.TransformNormal(localNormal, modelMatrix);
        return normal.LengthSquared > 0.000001f ? Vector3.Normalize(normal) : Vector3.UnitY;
    }

    private static Vector3 Shade(Vector3 normal, SampledLighting lighting)
    {
        var color = lighting.Ambient;
        foreach (var light in lighting.DirectionalLights)
        {
            color += light.Color * MathF.Max(0, Vector3.Dot(normal, light.Direction));
        }
        return color;
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

    private sealed record SourceData(float[] Normals, float[] BaseColors);
}
