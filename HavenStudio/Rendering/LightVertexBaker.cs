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
        LogAppliedContrast(model, lighting);
        return true;
    }

    private static int _applyProbeBudget = 20;

    /// <summary>
    /// Measures the LUMINANCE SPREAD of the colours actually written onto a model
    /// (post-bake, pre-GPU), tagged with its vertex count and asset name. Small
    /// props and large architecture are logged separately so a flat result on the
    /// big geometry can be told apart from a flat result everywhere. This looks at
    /// the delivered product, not the math that produced it.
    /// </summary>
    private static void LogAppliedContrast(Model3D model, BakedLighting lighting)
    {
        if (_applyProbeBudget <= 0)
        {
            return;
        }
        _applyProbeBudget--;
        var vc = model.VertexCount;
        float lmin = float.MaxValue, lmax = float.MinValue, lsum = 0f;
        for (var i = 0; i < vc; i++)
        {
            var o = i * 4;
            var lum = 0.30f * lighting.Colors[o] + 0.59f * lighting.Colors[o + 1] + 0.11f * lighting.Colors[o + 2];
            lmin = MathF.Min(lmin, lum); lmax = MathF.Max(lmax, lum); lsum += lum;
        }
        var name = string.IsNullOrEmpty(model.SourceAssetName) ? "(unnamed)" : model.SourceAssetName;
        Mgs4Diagnostics.Log("APPLY",
            $"{name} verts={vc} luminance min/avg/max = {lmin:F3}/{lsum / MathF.Max(1, vc):F3}/{lmax:F3} " +
            $"spread={lmax - lmin:F3} {(vc >= 500 ? "[ARCHITECTURE]" : "[prop]")}");
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
        LogBakeStatistics(colors, shadowedColors, vertexCount);
        return new BakedLighting(colors, shadowedColors);
    }

    private static int _bakeStatBudget = 16;

    /// <summary>
    /// One-line lit/shadowed statistics per baked model. Discriminates the two
    /// remaining contrast suspects: if lit max stays ~1.0-1.5 with a healthy
    /// share of vertices above 1.0, the bake carries the HDR range and the
    /// thief is the shadow application; if lit max hugs 1.0 or below, the
    /// range dies before the GPU.
    /// </summary>
    private static void LogBakeStatistics(float[] colors, float[] shadowedColors, int vertexCount)
    {
        if (_bakeStatBudget <= 0 || vertexCount <= 0 || colors.Length < vertexCount * 4)
        {
            return;
        }
        _bakeStatBudget--;
        float litMin = float.MaxValue, litMax = float.MinValue, litSum = 0f;
        float shMin = float.MaxValue, shMax = float.MinValue, shSum = 0f;
        var above1 = 0;
        for (var i = 0; i < vertexCount; i++)
        {
            var o = i * 4;
            var lit = MathF.Max(colors[o], MathF.Max(colors[o + 1], colors[o + 2]));
            var sh = shadowedColors.Length >= vertexCount * 4
                ? MathF.Max(shadowedColors[o], MathF.Max(shadowedColors[o + 1], shadowedColors[o + 2]))
                : 0f;
            litMin = MathF.Min(litMin, lit); litMax = MathF.Max(litMax, lit); litSum += lit;
            shMin = MathF.Min(shMin, sh); shMax = MathF.Max(shMax, sh); shSum += sh;
            if (lit > 1.0f)
            {
                above1++;
            }
        }
        Mgs4Diagnostics.Log("BAKE",
            $"lit min/avg/max = {litMin:F3}/{litSum / vertexCount:F3}/{litMax:F3} " +
            $"(>1.0: {100f * above1 / vertexCount:F0}%) | " +
            $"shadowed min/avg/max = {shMin:F3}/{shSum / vertexCount:F3}/{shMax:F3} | " +
            $"vertices={vertexCount}");
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
            ProbeGroundNormal(worldPosition, worldNormal, sampledLighting);
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
        // Keep both endpoints of the LT3 lighting equation. The old scalar
        // ShadowWeight discarded chroma and became inaccurate whenever the lit value
        // saturated at 1.0. Interpolating the two RGB results is equivalent to
        // disabling only the projected sun while preserving every other LT3 light.
        // Preserve HDR light energy. LT3 ambient plus sun often exceeds 1.0;
        // clamping before texture modulation erased the contrast that the shadow
        // buffer is meant to remove and made the whole stage too dark.
        // vcolor_to_lsc_scl, the preshader's 5th parameter, is supplied by the
        // caller. Both call sites were traced (build 2739):
        //   .DG_MakePreshadeModel  @0x12E118 -> lvx v31,[pool-32556]; vmr v2,v31
        //   .DG_MakePreshadeModel2 @0x12DA90 -> same default vector
        // and the constant resolves (TOC = .got + 0x8000 = 0xA90D58) to
        //   vcolor_to_lsc_scl = (0.0, 0.0, 0.0, 1.0)
        // The RGB weights are ZERO: the MDN's own vertex colour does not modulate
        // the RGB result on the standard stage path. The engine writes the LIGHTING
        // into the colour buffer (scaled by the pool's (255,255,255,255) and packed
        // to bytes), and the RSX then modulates the texture by it.
        //
        // Haven was computing lighting * baseColor, i.e. multiplying by the MDN
        // vertex colour a second time. That darkened every surface on top of an
        // already-correct lighting term.
        var vcolorWeight = Vector3.Zero;      // (0,0,0) from vcolor_to_lsc_scl
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

    /// <summary>Returns the sun's surface-to-light vector if present in the bake set.</summary>
    private static Vector3? FindSun(SampledLighting lighting)
    {
        var lights = lighting.BakeLights;
        for (var i = 0; i < lights.Count; i++)
        {
            if (lights[i].CastsProjectedShadow)
            {
                return lights[i].Direction;
            }
        }
        return lights.Count > 0 ? lights[0].Direction : (Vector3?)null;
    }

    private static Vector3 ReadWorldNormal(float[] normals, int index, Matrix4 modelMatrix)
    {
        var offset = index * 3;
        var localNormal = new Vector3(normals[offset], normals[offset + 1], normals[offset + 2]);
        var normal = Vector3.TransformNormal(localNormal, modelMatrix);
        return normal.LengthSquared > 0.000001f ? Vector3.Normalize(normal) : Vector3.UnitY;
    }

    private static int _sampleLogBudget = 3;

    private static int _sunProbeUp, _sunProbeUpLit, _sunProbeDown;
    private static float _sunProbeUpNdotL;
    private static bool _sunProbeLogged;

    // [GROUNDNORM] focus on the STREET FLOOR: street region at actual ground level
    // (Y<1000, below the ~2200 structures the earlier budget kept hitting). The
    // earlier data proved up-facing surfaces are lit (N.L=0.92), so the only open
    // question is whether the FLOOR faces up (should be lit) or down (inverted ->
    // dark). Count up vs down floor vertices and dump samples of both with N.L.
    private static int _floorUp, _floorFlat, _floorDown;
    private static int _floorUpBudget = 8, _floorDownBudget = 8;
    private static bool _groundProbeLogged;

    private static void ProbeGroundNormal(Vector3 p, Vector3 n, SampledLighting lighting)
    {
        var inStreet = p.X > 50000f && p.X < 100000f && p.Z > 75000f && p.Z < 130000f;
        if (!inStreet || p.Y > 1000f)
        {
            return;
        }
        var sun = FindSun(lighting);
        var ndl = sun.HasValue ? MathF.Max(0f, Vector3.Dot(n, sun.Value)) : -1f;
        if (n.Y > 0.5f)
        {
            _floorUp++;
            if (_floorUpBudget > 0)
            {
                _floorUpBudget--;
                Mgs4Diagnostics.Log("GROUNDNORM",
                    $"FLOOR-UP pos=({p.X:0},{p.Y:0},{p.Z:0}) n=({n.X:0.00},{n.Y:0.00},{n.Z:0.00}) sunN.L={ndl:0.00}");
            }
        }
        else if (n.Y < -0.5f)
        {
            _floorDown++;
            if (_floorDownBudget > 0)
            {
                _floorDownBudget--;
                Mgs4Diagnostics.Log("GROUNDNORM",
                    $"FLOOR-DOWN pos=({p.X:0},{p.Y:0},{p.Z:0}) n=({n.X:0.00},{n.Y:0.00},{n.Z:0.00}) sunN.L={ndl:0.00}");
            }
        }
        else
        {
            _floorFlat++;
        }
        if (!_groundProbeLogged && _floorUp + _floorFlat + _floorDown >= 1000)
        {
            _groundProbeLogged = true;
            var tot = _floorUp + _floorFlat + _floorDown;
            Mgs4Diagnostics.Log("GROUNDNORM",
                $"STREET FLOOR (Y<1000): up={_floorUp}({100f * _floorUp / tot:F0}%) " +
                $"flat={_floorFlat}({100f * _floorFlat / tot:F0}%) down={_floorDown}({100f * _floorDown / tot:F0}%) " +
                "| up=should be sunlit, down=inverted normals = dark");
        }
    }


    private static ShadedLighting Shade(Vector3 normal, SampledLighting lighting)
    {
        if (_sampleLogBudget > 0)
        {
            _sampleLogBudget--;
            Mgs4Diagnostics.LogSample(Vector3.Zero, normal, lighting);
        }

        // SUN-SIGN PROBE. For every up-facing surface (normal.Y > 0.5) measure the
        // dot with the sun's surface-to-light vector. If up-facing faces get a
        // POSITIVE dot, the sun lights them from above (correct); if negative, the
        // scene is lit from below and the sign is wrong somewhere upstream of here.
        var sun = FindSun(lighting);
        if (sun.HasValue)
        {
            var ndl = Vector3.Dot(normal, sun.Value);
            if (normal.Y > 0.5f)
            {
                _sunProbeUp++;
                _sunProbeUpNdotL += ndl;
                if (ndl > 0f) _sunProbeUpLit++;
            }
            else if (normal.Y < -0.5f)
            {
                _sunProbeDown++;
            }
            if (!_sunProbeLogged && _sunProbeUp + _sunProbeDown >= 2000)
            {
                _sunProbeLogged = true;
                var avg = _sunProbeUp > 0 ? _sunProbeUpNdotL / _sunProbeUp : 0f;
                var litPct = _sunProbeUp > 0 ? 100f * _sunProbeUpLit / _sunProbeUp : 0f;
                Mgs4Diagnostics.Log("SUNSIGN",
                    $"up-facing faces={_sunProbeUp} avg N.L(sun)={avg:F3} lit={litPct:F0}% | " +
                    $"down-facing={_sunProbeDown} | " +
                    (avg > 0f
                        ? "sun lights floors from ABOVE (correct)"
                        : "sun lights floors from BELOW (sign inverted upstream)"));
            }
        }
        var total = lighting.SampleAmbient(normal);
        var projectedShadow = Vector3.Zero;
        // Sum N.L over EVERY in-range LT3 record, matching the engine preshader
        // (DG_MakePreshaderModelUnit loops 1-5). The 3-slot DirectionalLights set
        // is the runtime limit and must not gate the stage bake, or the surface
        // receives near-uniform fill and loses all directional contrast.
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
