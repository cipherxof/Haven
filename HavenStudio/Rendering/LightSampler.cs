using System;
using System.Collections.Generic;
using System.Linq;
using HavenStudio.Formats.Lit;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public readonly record struct DirectionalLightSample(
    Vector3 Direction,
    Vector3 Color,
    bool CastsProjectedShadow = false,
    float SelectionForce = 0f);

public sealed record SampledLighting(
    Vector3 Ambient,
    IReadOnlyList<DirectionalLightSample> DirectionalLights,
    AmbientCubeLighting? AmbientCube = null,
    IReadOnlyList<DirectionalLightSample>? BakeContributors = null)
{
    public Vector3 SampleAmbient(Vector3 normal) =>
        AmbientCube?.Evaluate(normal) ?? Ambient;

    /// <summary>
    /// Every in-range LT3 record contribution at this sample point, BEFORE the
    /// 3-slot reduction. DG_MakePreshaderModelUnit (the stage vertex bake) sums
    /// N.L over all in-range point/spot/line/parallel records per vertex; it does
    /// NOT reduce to three (that limit is DG_LIGHT, the runtime/character path).
    /// Falls back to the reduced set if not populated.
    /// </summary>
    public IReadOnlyList<DirectionalLightSample> BakeLights =>
        BakeContributors ?? DirectionalLights;
    public bool TryGetProjectedShadowLight(out DirectionalLightSample shadowLight)
    {
        for (var index = 0; index < DirectionalLights.Count; index++)
        {
            if (DirectionalLights[index].CastsProjectedShadow)
            {
                shadowLight = DirectionalLights[index];
                return true;
            }
        }

        shadowLight = default;
        return false;
    }
}

/// <summary>
/// Samples Konami LT2/LT3 data into the three directional-light slots exposed by
/// MGS4's DG_LIGHT structure. The formulas and target flags used here come from
/// Konami's fmt_lit.h and are cross-checked against the MGS4 debug ELF.
/// </summary>
public static class LightSampler
{
    public const int MaximumDirectionalLights = 3;
    private static int _samplerLogBudget = 3;
    private static int _ambientLogBudget = 6;
    private static bool _stageAmbientLogPending = true;
    // Engine preshader multiplies the record colour by attenuation only
    // (DG_MakePreshaderModelUnit accumulation loops). The former 1.5x boost was
    // an invented factor; harmless when only 3 lights survived, but wrong once
    // every in-range record is summed for the bake. Neutralised to 1.0.
    private const float PointCenterBoost = 1.0f;
    // TRANSPLANT (Python bench, validated): fraction of the LT3 header ambient
    // applied to STAGE geometry. The header ambient on sm_dd is near-white
    // (1, 1, 0.99); applied at full strength it floods the scene and kills all
    // sun/shadow contrast. 0 = sun + local records only (matches the viz that
    // showed clean shadows and visible sunlight). Raise toward 1 only if
    // deep-shadow areas turn into pure-black voids.
    // 0 flooded contrast (near-white header ambient at full strength = the milky
    // look). But 0 left faces turned away from the sun PURE BLACK. 0.16 = a modest
    // fill floor from the LT3 header ambient colour so nothing is black (texture
    // stays visible in shade) while the sun's N.L directional gradient and the
    // shadows still read - Snake's "light everything, then shadows do the relief",
    // dosed. Raise toward 0.3 for more fill, drop toward 0 for deeper shade.
    private const float StageAmbientScale = 0.16f;

    // Background blackpoints darken the stage bake (they multiply the light by
    // clamp01(dist/range) inside their bounds). The engine applies them, but the
    // 2006 Lighting Editor reference does NOT show them - it produced the dark
    // band along the street that reads as a fake shadow, absent in the dev
    // capture. Off by default so the stage preview matches that reference; the
    // per-vertex darkening only fired when this was implicitly on.
    private const bool ApplyStageBlackPoints = false;
    // Kept for reference: "sunshine" (0x696848) is one of the hs-amb scope names
    // observed in s01a10a.lt3. No longer used as a filter (see AddHemi).
#pragma warning disable IDE0051, CS0414
    private static readonly uint SunshineHash = HavenStudio.Utils.String.HashString("sunshine");
#pragma warning restore IDE0051, CS0414

    public static SampledLighting Sample(
        LitFile file,
        Vector3 position,
        SceneLightSettings? sceneLighting = null,
        LitLightingTarget target = LitLightingTarget.Background,
        uint lightingScopeHash = 0)
    {
        ArgumentNullException.ThrowIfNull(file);

        // NewSystemLightSet owns distinct background and character light slots.
        // TRANSPLANT (Python bench render_v3, validated visually by Snake): the LT3
        // header ambient on sm_dd is near-white (1, 1, 0.99). Added as a uniform
        // floor in Shade() it floods EVERY surface before sun/locals, so lit and
        // shadowed areas both start at ~1.0 -> the flat "overcast" look with no
        // visible sun and ghost shadows. The real stage floor (~0.3) emerges from
        // the summed local records (BakeLights), NOT from a near-white ambient.
        // Scale the header ambient down for stage geometry so sun + local records
        // produce the dev-2006 contrast (deep shadows + bright sunlit ground).
        // Tunable: raise StageAmbientScale toward 1 if deep-shadow voids appear.
        var ambient = target == LitLightingTarget.Character
            ? sceneLighting?.CharacterAmbientFloor ?? file.Ambient.ToScaledVector3()
            : file.Ambient.ToScaledVector3() * StageAmbientScale;
        if (target != LitLightingTarget.Character && _stageAmbientLogPending)
        {
            _stageAmbientLogPending = false;
            var raw = file.Ambient.ToScaledVector3();
            Mgs4Diagnostics.Log("AMBIENT",
                $"stage ambient scale={StageAmbientScale:F2} -> header ({raw.X:F2},{raw.Y:F2},{raw.Z:F2}) " +
                (StageAmbientScale <= 0f
                    ? "SUPPRESSED for stage geometry; the ~0.3 floor comes from the summed local records (engine bake model)"
                    : $"applied at {StageAmbientScale:P0}"));
        }
        var ambientCube = sceneLighting?.AmbientCubeFor(target, ambient) ??
            AmbientCubeLighting.Uniform(ambient);
        // NOTE: the exact MGS4 spatial ambient is applied LAST (see below), not
        // here. Applied at this point it was immediately overwritten by Haven's
        // speculative parallel/hemi blending: the logged sample showed all six
        // faces collapsed to a uniform ~(1.198, 1.188, 1.125) instead of the
        // .abc values (0.363 / 0.558 / 0.298 ...), which removed every trace of
        // directional variation from the ambient term.
        var encodedSunDirection = sceneLighting?.Direction ?? file.Direction.Xyz;
        var rawSunColor = sceneLighting?.DirectionalColorFor(target) ?? file.Color.ToScaledVector3();
        // ENGINE-VERIFIED (build 2739, session "pixel-perfect"): dir.W never
        // scales the light colour. DG_GetLightScene reads +12 exactly once
        // (@0x10D90C) and only for the selection comparisons; and
        // DG_SetSceneLightColor (@0x10B5D4) stores the RGB raw into the scene
        // (+1088..) with no W multiply. W is a PRIORITY weight. The previous
        // sunScale = Direction.W (0.609 on sm_dd) ran the sun at 61% strength.
        const float sunScale = 1f;
        var sunColor = rawSunColor * sunScale;

        var directional = new List<DirectionalLightSample>
        {
            new(
                ToSurfaceLightDirection(encodedSunDirection),
                sunColor,
                CastsProjectedShadow: true,
                SelectionForce: LightForce(sunColor))
        };

        // LIT_PARALLEL is a bounded local directional/ambient definition.
        // The engine keeps it separate from the global six-slot ambient cube;
        // it must therefore be applied with its encoded force instead of
        // replacing the entire stage ambient unconditionally.
        LitParallelLight? selectedParallel = null;
        foreach (var group in file.Groups)
        {
            if (!group.Contains(position))
            {
                continue;
            }

            foreach (var parallel in group.Lights.OfType<LitParallelLight>())
            {
                if (!LitFlags.Applies(parallel.Flag, target) ||
                    !Contains(parallel.BoundsMin.Xyz, parallel.BoundsMax.Xyz, position))
                {
                    continue;
                }

                // Engine rule (preshader parallel loop @0x12D420, build 2739):
                // vsel with no comparison of forces - the LAST record whose bounds
                // contain the point wins, in file order. The previous max-|Force|
                // pick was invented. (What the engine does with the selected
                // CVECTOR - it goes to the preshader's 4th output stream for the
                // runtime shader - is still being reverse engineered; the blend
                // below remains Haven's approximation of that consumer.)
                selectedParallel = parallel;
            }
        }

        if (selectedParallel != null)
        {
            // LIT_PARALLEL contains an explicit force. The previous preview ignored
            // it and replaced the almost-white stage ambient with the very dark
            // local CVECTOR, producing the large black cells visible in sm_dd.
            // DG_GetLightScene starts from DG_LIGHT::ambient_color[6]; preserve that
            // cube and blend the bounded local definition by its encoded force.
            var localForce = Math.Clamp(MathF.Abs(selectedParallel.Force), 0f, 1f);
            var localAmbient = selectedParallel.Ambient.ToScaledVector3();
            ambientCube = ambientCube.BlendToward(
                AmbientCubeLighting.Uniform(localAmbient),
                localForce);
            var localColor = selectedParallel.Color.ToScaledVector3() * localForce;
            if (localColor.LengthSquared > 0f)
            {
                directional.Add(new DirectionalLightSample(
                    ToSurfaceLightDirection(selectedParallel.Direction.Xyz),
                    localColor,
                    SelectionForce: PositiveOrFallback(
                        localForce,
                        LightForce(localColor))));
            }
        }

        AmbientCubeLighting? hemiAccumulator = null;
        var hemiCount = 0;
        List<LitBlackPoint>? activeBlackPoints = null;
        foreach (var group in file.Groups)
        {
            if (!group.Contains(position))
            {
                continue;
            }

            foreach (var light in group.Lights)
            {
                if (!LitFlags.Applies(LitFlags.GetRuntimeFlag(light), target))
                {
                    continue;
                }

                switch (light)
                {
                    case LitPointLight point:
                        AddPoint(directional, position, point);
                        break;
                    case LitSpotLight spot:
                        AddSpot(directional, position, spot);
                        break;
                    case LitLineLight line:
                        AddLine(directional, position, line);
                        break;
                    case LitBlackPoint blackPoint:
                        if ((ApplyStageBlackPoints || target != LitLightingTarget.Background) &&
                            Contains(blackPoint.BoundsMin.Xyz, blackPoint.BoundsMax.Xyz, position))
                        {
                            // Engine (preshader blackpoint loop @0x12D334, build
                            // 2739): EVERY contained blackpoint contributes a
                            // multiplicative factor clamp01(|pos-point| / range);
                            // outside bounds or beyond range the factor is 1.0.
                            // "First match only" and the old range*2 were invented.
                            (activeBlackPoints ??= new List<LitBlackPoint>()).Add(blackPoint);
                        }
                        break;
                    case LitHemiLight hemi:
                        AddHemi(
                            ref hemiAccumulator,
                            ref hemiCount,
                            position,
                            hemi,
                            lightingScopeHash);
                        break;
                }
            }
        }

        if (activeBlackPoints != null)
        {
            // Engine-exact factor per blackpoint: dist / range, clamped to 1.
            // The engine loop multiplies the accumulated LIGHT sums; whether the
            // ambient seed sits inside those accumulators is not yet proven, so
            // the factor is applied to the light contributions only.
            var blackMultiplier = 1f;
            foreach (var blackPoint in activeBlackPoints)
            {
                var range = MathF.Abs(blackPoint.Range);
                if (range <= 0f)
                {
                    continue;
                }
                var distance = (blackPoint.Point.Xyz - position).Length;
                blackMultiplier *= Math.Clamp(distance / range, 0f, 1f);
            }
            for (var index = 0; index < directional.Count; index++)
            {
                directional[index] = directional[index] with
                {
                    Color = directional[index].Color * blackMultiplier,
                    SelectionForce = directional[index].SelectionForce * blackMultiplier
                };
            }
        }

        var valid = directional
            .Where(light => IsFinite(light.Direction) &&
                            IsFinite(light.Color) &&
                            light.Color.LengthSquared > 0f)
            .ToArray();

        // DG_LIGHT in the debug ELF contains exactly three light_dir and three
        // light_color FVECTORs. Keep the projected stage sun and choose the two
        // strongest remaining candidates by Konami's force metric.
        var reduced = new List<DirectionalLightSample>(MaximumDirectionalLights);
        var projectedSun = valid.FirstOrDefault(light => light.CastsProjectedShadow);
        if (projectedSun.CastsProjectedShadow)
        {
            reduced.Add(projectedSun);
        }

        foreach (var light in valid
                     .Where(light => !light.CastsProjectedShadow)
                     .OrderByDescending(light => light.SelectionForce)
                     .ThenByDescending(light => light.Color.LengthSquared))
        {
            if (reduced.Count >= MaximumDirectionalLights)
            {
                break;
            }
            reduced.Add(light);
        }

        if (_samplerLogBudget > 0)
        {
            _samplerLogBudget--;
            Mgs4Diagnostics.Log("SAMPLER",
                $"target={target} position=({position.X:F0},{position.Y:F0},{position.Z:F0}) " +
                $"contributors={valid.Length} (reduced to {MaximumDirectionalLights}) " +
                $"sun=({sunColor.X:F3},{sunColor.Y:F3},{sunColor.Z:F3}) sunScale={sunScale:F3} " +
                $"sceneLighting={(sceneLighting != null ? "GCX" : "LT3 header")} " +
                $"mgs4Cube={(Mgs4AmbientCubeEvaluator.HasData ? "ACTIVE" : "inactive")}");
        }

        // Ambient authority: hs-amb volumes containing this position win where
        // present. The .abc is CHARACTER ambient (ExportAmbcube of the hs-amb) and
        // its single whole-map node flattens the stage, so it applies to Character
        // targets only; the stage takes header*StageAmbientScale (0) + local records.
        {
            if (hemiCount > 0 && hemiAccumulator is { } accumulated)
            {
                ambientCube = Divide(accumulated, hemiCount);
                if (_ambientLogBudget > 0)
                {
                    _ambientLogBudget--;
                    Mgs4Diagnostics.Log("AMBIENT",
                        $"hs-amb volumes applied: {hemiCount} -> T={ambientCube.Top} B={ambientCube.Bottom}");
                }
            }
            else
            {
                if (_ambientLogBudget > 0)
                {
                    _ambientLogBudget--;
                    Mgs4Diagnostics.Log("AMBIENT",
                        $"hs-amb volumes applied: 0 (Character-flagged; target={target}) -> " +
                        (target == LitLightingTarget.Character && Mgs4AmbientCubeEvaluator.HasData
                            ? ".abc cube (Character)" : "header*StageAmbientScale (stage=0)"));
                }
                if (target == LitLightingTarget.Character &&
                    Mgs4AmbientCubeEvaluator.TryEvaluate(position, out var mgs4Cube))
                {
                    ambientCube = mgs4Cube;
                }
            }
        }

        var finalCube = ambientCube.ClampNonNegative();
        // BakeContributors = every in-range record (post-blackpoint), NOT reduced
        // to three. This is what DG_MakePreshaderModelUnit accumulates per vertex;
        // the flat preview came from feeding the 3-slot runtime set to the stage
        // bake. The reduced set is kept for any genuine runtime/character use.
        return new SampledLighting(
            ClampNonNegative(finalCube.Average),
            reduced,
            finalCube,
            valid);
    }

    /// <summary>
    /// LT/GCX directional vectors encode the direction in which light rays travel.
    /// Shading and the shadow camera need the opposite vector: surface to light.
    /// </summary>
    public static Vector3 ToSurfaceLightDirection(Vector3 encodedRayDirection)
    {
        if (!IsFinite(encodedRayDirection) || encodedRayDirection.LengthSquared <= 0.000001f)
        {
            return Vector3.UnitY;
        }

        return -Vector3.Normalize(encodedRayDirection);
    }

    public static float RadialAttenuation(float distance, float radius)
    {
        if (!float.IsFinite(distance) || distance < 0f ||
            !float.IsFinite(radius) || radius <= 0f)
        {
            return 0f;
        }
        return Math.Clamp(1f - distance / radius, 0f, 1f);
    }

    private static void AddPoint(
        ICollection<DirectionalLightSample> directional,
        Vector3 position,
        LitPointLight point)
    {
        var toLight = point.Point.Xyz - position;
        var broadPhaseRange = MathF.Abs(point.ExtendedRange);
        if (float.IsFinite(broadPhaseRange) && broadPhaseRange > 0f &&
            (MathF.Abs(toLight.X) > broadPhaseRange ||
             MathF.Abs(toLight.Y) > broadPhaseRange ||
             MathF.Abs(toLight.Z) > broadPhaseRange))
        {
            return;
        }

        var distance = toLight.Length;
        // DG_GetLightScene (build 2739 @0x10C758) culls on len2 > r_range*r_range
        // and ramps over r_range itself; the previous 2x radius was not engine
        // behaviour and over-extended every point light.
        var attenuation = RadialAttenuation(distance, MathF.Abs(point.Range));
        if (attenuation <= 0f || distance <= 0.0001f)
        {
            return;
        }

        var vectorScale = attenuation * PointCenterBoost;
        var baseColor = point.Color.ToScaledVector3();
        directional.Add(new DirectionalLightSample(
            toLight / distance,
            baseColor * vectorScale,
            SelectionForce: PositiveOrFallback(
                MathF.Abs(point.Point.W) * vectorScale,
                LightForce(baseColor) * vectorScale)));
    }

    private static void AddSpot(
        ICollection<DirectionalLightSample> directional,
        Vector3 position,
        LitSpotLight spot)
    {
        if (!Contains(spot.BoundsMin.Xyz, spot.BoundsMax.Xyz, position))
        {
            return;
        }

        var toLight = spot.Point.Xyz - position;
        var distance = toLight.Length;
        var radial = RadialAttenuation(distance, MathF.Abs(spot.Direction.W));
        if (radial <= 0f || distance <= 0.0001f)
        {
            return;
        }

        var lightToObject = -toLight / distance;
        var cone = ConeAttenuation(
            Vector3.Dot(SafeNormalize(spot.Direction.Xyz), lightToObject),
            spot.Umbra,
            spot.Penumbra);
        if (cone <= 0f)
        {
            return;
        }

        var vectorScale = radial * cone * PointCenterBoost;
        var baseColor = spot.Color.ToScaledVector3();
        directional.Add(new DirectionalLightSample(
            toLight / distance,
            baseColor * vectorScale,
            SelectionForce: PositiveOrFallback(
                MathF.Abs(spot.Point.W) * vectorScale,
                LightForce(baseColor) * vectorScale)));
    }

    private static void AddLine(
        ICollection<DirectionalLightSample> directional,
        Vector3 position,
        LitLineLight line)
    {
        if (!Contains(line.BoundsMin.Xyz, line.BoundsMax.Xyz, position))
        {
            return;
        }

        var start = line.Point.Xyz;
        var axis = SafeNormalize(line.Direction.Xyz);
        var length = float.IsFinite(line.Direction.W) ? line.Direction.W : 0f;
        var segment = axis * length;
        var denominator = segment.LengthSquared;
        var t = denominator <= 0.000001f
            ? 0f
            : Math.Clamp(Vector3.Dot(position - start, segment) / denominator, 0f, 1f);
        var nearest = start + segment * t;
        var toLight = nearest - position;
        var distance = toLight.Length;
        // ENGINE-VERIFIED (line loop @0x12CF24, build 2739): closest point on the
        // segment [P0, P0 + dir*len] via t = clamp(dot(pos-P0, dir), 0, len), then
        // colour * max(0, N.L) * max(0, 1 - d/range). The engine expresses the ramp
        // as dot_u * (1/d - 1/range) with a select to 1/d beyond range, which is
        // algebraically identical and cuts to exactly zero past r_range.
        var attenuation = RadialAttenuation(distance, MathF.Abs(line.Range));
        if (attenuation <= 0f || distance <= 0.0001f)
        {
            return;
        }

        var vectorScale = attenuation * PointCenterBoost;
        var baseColor = line.Color.ToScaledVector3();
        directional.Add(new DirectionalLightSample(
            toLight / distance,
            baseColor * vectorScale,
            SelectionForce: PositiveOrFallback(
                MathF.Abs(line.Point.W) * vectorScale,
                LightForce(baseColor) * vectorScale)));
    }

    /// <summary>
    /// Type-64 hs-amb records in sm_dd are all flagged 0x100 (character only).
    /// They are therefore excluded from stage geometry by the target filter. For
    /// character sampling, retain the two-colour hemisphere approximation while
    /// respecting the LT3 owner/scope hash when one is known.
    /// </summary>
    private static void AddHemi(
        ref AmbientCubeLighting? hemiAccumulator,
        ref int hemiCount,
        Vector3 position,
        LitHemiLight hemi,
        uint lightingScopeHash)
    {
        if (!Contains(hemi.BoundsMin.Xyz, hemi.BoundsMax.Xyz, position))
        {
            return;
        }

        // The old scope filter compared the volume's MetadataScopeHash against the
        // MODEL's asset-name hash. Measured on the real s01a10a.lt3: the volumes are
        // scoped to sub-AREA names (only "sunshine" = 0x696848 resolved; the map
        // model hash "n021a" = 0xF8CE87 matches none of them), so the comparison
        // silently rejected 370 of 371 volumes. The engine's prefilter is AABB +
        // record flags (DG_MakePreshaderModelUnit class passes) - there is no
        // name-scope test on the stage path. Spatial containment above is the gate.

        // A hemisphere light is sky above / ground below, i.e. a DIRECTIONAL
        // ambient - not a uniform add plus a fake directional light. The stage
        // carries 371 of these "hs-amb" volumes (they are the entries filling the
        // list panel of Konami's Lighting Editor), and they are the authored
        // source that ExportAmbcube bakes into .abc files. They are the per-room
        // ambient detail: dark interiors, bright courtyards.
        //
        // Each of the six cube faces takes lerp(ground, sky, (dot(axis,dir)+1)/2),
        // the standard hemisphere-to-cube projection. Colours use the RGBM decode
        // (A is an intensity scale) and the record's own Force values.
        var sky = hemi.ColorSky.ToScaledVector3() * MathF.Max(0f, hemi.ForceSky);
        var ground = hemi.ColorGround.ToScaledVector3() * MathF.Max(0f, hemi.ForceGround);
        if (!IsFinite(sky) || !IsFinite(ground))
        {
            return;
        }

        var axis = SafeNormalize(hemi.Direction.Xyz);
        if (!IsFinite(axis) || axis.LengthSquared <= 0f)
        {
            axis = Vector3.UnitY;
        }

        static Vector3 Face(Vector3 faceAxis, Vector3 up, Vector3 skyColor, Vector3 groundColor)
        {
            var t = Math.Clamp((Vector3.Dot(faceAxis, up) + 1f) * 0.5f, 0f, 1f);
            return groundColor + (skyColor - groundColor) * t;
        }

        var hemiCube = new AmbientCubeLighting(
            Face(-Vector3.UnitX, axis, sky, ground),
            Face(Vector3.UnitX, axis, sky, ground),
            Face(Vector3.UnitY, axis, sky, ground),
            Face(-Vector3.UnitY, axis, sky, ground),
            Face(Vector3.UnitZ, axis, sky, ground),
            Face(-Vector3.UnitZ, axis, sky, ground));

        hemiAccumulator = hemiAccumulator == null
            ? hemiCube
            : Accumulate(hemiAccumulator.Value, hemiCube);
        hemiCount++;
    }

    private static AmbientCubeLighting Accumulate(AmbientCubeLighting a, AmbientCubeLighting b) =>
        new(a.Left + b.Left, a.Right + b.Right, a.Top + b.Top,
            a.Bottom + b.Bottom, a.Front + b.Front, a.Back + b.Back);

    private static AmbientCubeLighting Divide(AmbientCubeLighting c, float d) =>
        new(c.Left / d, c.Right / d, c.Top / d, c.Bottom / d, c.Front / d, c.Back / d);

    /// <summary>
    /// ENGINE-VERIFIED (spot loop @0x12D0F4, build 2739). The preshader computes
    /// cos(theta) = dot(pos - apex, axis) * (1/dist) explicitly (0x12D25C), then a
    /// LINEAR ramp between the two floats stored at +68/+72 of the record:
    /// 0 below the lower threshold, 1 above the upper one. The file stores raw
    /// COSINES; ToCosine passes [-1,1] through unchanged, and the min/max ordering
    /// below reproduces the engine ramp regardless of which field is which.
    /// </summary>
    private static float ConeAttenuation(float cosine, float umbra, float penumbra)
    {
        var inner = ToCosine(umbra);
        var outer = ToCosine(penumbra);
        if (inner < outer)
        {
            (inner, outer) = (outer, inner);
        }
        if (cosine >= inner)
        {
            return 1f;
        }
        if (cosine <= outer || inner <= outer)
        {
            return 0f;
        }
        return (cosine - outer) / (inner - outer);
    }

    private static float ToCosine(float value)
    {
        if (!float.IsFinite(value))
        {
            return 1f;
        }
        if (value is >= -1f and <= 1f)
        {
            return value;
        }
        var radians = value > MathF.PI ? MathHelper.DegreesToRadians(value) : value;
        return MathF.Cos(radians);
    }

    private static float LightForce(Vector3 color) =>
        MathF.Max(0f, color.X * 0.30f + color.Y * 0.59f + color.Z * 0.11f);

    private static float PositiveOrFallback(float value, float fallback) =>
        float.IsFinite(value) && value > 0f ? value : MathF.Max(0f, fallback);

    private static Vector3 SafeNormalize(Vector3 value) =>
        value.LengthSquared > 0.000001f && IsFinite(value)
            ? Vector3.Normalize(value)
            : Vector3.UnitY;

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static Vector3 ClampNonNegative(Vector3 value) => new(
        MathF.Max(0f, value.X),
        MathF.Max(0f, value.Y),
        MathF.Max(0f, value.Z));

    private static bool Contains(Vector3 min, Vector3 max, Vector3 position) =>
        position.X >= MathF.Min(min.X, max.X) && position.X <= MathF.Max(min.X, max.X) &&
        position.Y >= MathF.Min(min.Y, max.Y) && position.Y <= MathF.Max(min.Y, max.Y) &&
        position.Z >= MathF.Min(min.Z, max.Z) && position.Z <= MathF.Max(min.Z, max.Z);
}
