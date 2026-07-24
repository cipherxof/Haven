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
    /// Every in-range LT3 record contribution at this sample point, before the
    /// 3-slot reduction. The stage vertex bake sums N.L over all in-range
    /// point/spot/line/parallel records per vertex; it does not reduce to three
    /// (that limit is the runtime/character path). Falls back to the reduced set
    /// if not populated.
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
/// Samples Konami LT2/LT3 data into the three directional-light slots used by
/// the MGS4 lighting model. Formulas and target flags follow the LT3 layout.
/// </summary>
public static class LightSampler
{
    public const int MaximumDirectionalLights = 3;
    // The record colour is multiplied by attenuation only (no extra centre
    // boost), so every in-range record sums correctly for the bake.
    private const float PointCenterBoost = 1.0f;
    // Fraction of the LT3 header ambient applied to stage geometry. The header
    // ambient on sm_dd is near-white; at full strength it floods the scene and
    // kills sun/shadow contrast. 0.16 keeps a modest fill floor so shaded faces
    // stay visible while the sun gradient and shadows still read. Raise toward
    // 0.3 for more fill, drop toward 0 for deeper shade.
    private const float StageAmbientScale = 0.16f;

    // Background blackpoints darken the bake by multiplying the light by
    // clamp01(dist/range) inside their bounds. Off by default: they produce a
    // dark band along the street that reads as a fake shadow and does not match
    // the 2006 Lighting Editor reference.
    private const bool ApplyStageBlackPoints = false;
    // Kept for reference: "sunshine" is one of the hs-amb scope names. No longer
    // used as a filter (see AddHemi).
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
        // Direction.W is a priority weight, not a colour scale, so the sun runs
        // at full strength.
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

                // The last record whose bounds contain the point wins, in file
                // order (no force comparison).
                selectedParallel = parallel;
            }
        }

        if (selectedParallel != null)
        {
            // LIT_PARALLEL carries an explicit force. Preserve the ambient cube
            // and blend the bounded local definition by that force, rather than
            // replacing the stage ambient outright.
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
                            // Every contained blackpoint contributes a
                            // multiplicative factor clamp01(|pos-point| / range);
                            // outside bounds or beyond range the factor is 1.0.
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

        // Three directional slots: keep the projected stage sun and choose the
        // two strongest remaining candidates by the force metric.
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

        // Ambient authority: hs-amb volumes containing this position take
        // precedence where present. The .abc cube applies to Character targets
        // only; the stage takes header*StageAmbientScale plus local records.
        {
            if (hemiCount > 0 && hemiAccumulator is { } accumulated)
            {
                ambientCube = Divide(accumulated, hemiCount);
            }
            else
            {
                if (target == LitLightingTarget.Character &&
                    Mgs4AmbientCubeEvaluator.TryEvaluate(position, out var mgs4Cube))
                {
                    ambientCube = mgs4Cube;
                }
            }
        }

        var finalCube = ambientCube.ClampNonNegative();
        // BakeContributors = every in-range record (post-blackpoint), not reduced
        // to three: this is what the stage bake accumulates per vertex. The
        // reduced set is kept for runtime/character use.
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
        // Cull on distance^2 > range^2 and ramp over the range itself.
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
        // Closest point on the segment [P0, P0 + dir*len] via
        // t = clamp(dot(pos-P0, dir), 0, len), then colour * max(0, N.L) *
        // max(0, 1 - d/range), cutting to zero past the range.
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

        // Volumes are gated by spatial containment (AABB), not a name-scope test.

        // A hemisphere light is sky above / ground below, i.e. a directional
        // ambient - not a uniform add plus a fake directional light. These
        // "hs-amb" volumes are the per-room ambient detail (dark interiors,
        // bright courtyards) and the authored source baked into .abc files.
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
    /// cos(theta) = dot(pos - apex, axis) / dist, then a linear ramp between the
    /// two cosine thresholds stored in the record: 0 below the lower threshold,
    /// 1 above the upper one. The file stores raw cosines; the min/max ordering
    /// below reproduces the ramp regardless of which field is which.
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
