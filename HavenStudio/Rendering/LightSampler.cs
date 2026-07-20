using System;
using System.Collections.Generic;
using System.Linq;
using HavenStudio.Formats.Lit;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public readonly record struct DirectionalLightSample(Vector3 Direction, Vector3 Color);

public sealed record SampledLighting(
    Vector3 Ambient,
    IReadOnlyList<DirectionalLightSample> DirectionalLights);

public static class LightSampler
{
    public const int MaximumDirectionalLights = 3;

    public static SampledLighting Sample(
        LitFile file,
        Vector3 position,
        SceneLightSettings? sceneLighting = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        var ambient = sceneLighting?.AmbientColor ?? file.Ambient.ToVector3();
        var directional = new List<DirectionalLightSample>
        {
            new(
                sceneLighting?.Direction ?? SafeNormalize(file.Direction.Xyz),
                sceneLighting?.DirectionalColor ?? file.Color.ToVector3())
        };

        LitParallelLight? selectedParallel = null;
        foreach (var group in file.Groups)
        {
            if (!group.Contains(position))
            {
                continue;
            }
            foreach (var parallel in group.Lights.OfType<LitParallelLight>())
            {
                if (!Contains(parallel.BoundsMin.Xyz, parallel.BoundsMax.Xyz, position))
                {
                    continue;
                }
                if (selectedParallel == null || MathF.Abs(parallel.Force) >= MathF.Abs(selectedParallel.Force))
                {
                    selectedParallel = parallel;
                }
            }
        }

        if (selectedParallel != null)
        {
            directional.Add(new DirectionalLightSample(
                SafeNormalize(selectedParallel.Direction.Xyz),
                selectedParallel.Color.ToVector3()));
        }

        var blackMultiplier = 1f;
        foreach (var group in file.Groups)
        {
            if (!group.Contains(position))
            {
                continue;
            }

            foreach (var light in group.Lights)
            {
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
                    {
                        if (!Contains(blackPoint.BoundsMin.Xyz, blackPoint.BoundsMax.Xyz, position))
                        {
                            break;
                        }
                        var distance = (blackPoint.Point.Xyz - position).Length;
                        var attenuation = RadialAttenuation(distance, MathF.Abs(blackPoint.Range) * 2f);
                        blackMultiplier *= 1f - attenuation;
                        break;
                    }
                }
            }
        }

        if (blackMultiplier < 1f)
        {
            ambient *= blackMultiplier;
            for (var index = 0; index < directional.Count; index++)
            {
                directional[index] = directional[index] with
                {
                    Color = directional[index].Color * blackMultiplier
                };
            }
        }

        var reduced = directional
            .Where(light => IsFinite(light.Direction) && IsFinite(light.Color) && light.Color.LengthSquared > 0)
            .OrderByDescending(light => light.Color.LengthSquared)
            .Take(MaximumDirectionalLights)
            .ToArray();
        return new SampledLighting(ClampNonNegative(ambient), reduced);
    }

    public static float RadialAttenuation(float distance, float radius)
    {
        if (!float.IsFinite(distance) || distance < 0 || !float.IsFinite(radius) || radius <= 0)
        {
            return 0;
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
        if (float.IsFinite(broadPhaseRange) && broadPhaseRange > 0 &&
            (MathF.Abs(toLight.X) > broadPhaseRange ||
             MathF.Abs(toLight.Y) > broadPhaseRange ||
             MathF.Abs(toLight.Z) > broadPhaseRange))
        {
            return;
        }
        var distance = toLight.Length;
        var attenuation = RadialAttenuation(distance, MathF.Abs(point.Range));
        if (attenuation <= 0 || distance <= 0.0001f)
        {
            return;
        }
        directional.Add(new DirectionalLightSample(
            toLight / distance,
            point.Color.ToVector3() * attenuation));
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
        if (radial <= 0 || distance <= 0.0001f)
        {
            return;
        }
        var lightToObject = -toLight / distance;
        var cone = ConeAttenuation(
            Vector3.Dot(SafeNormalize(spot.Direction.Xyz), lightToObject),
            spot.Umbra,
            spot.Penumbra);
        if (cone <= 0)
        {
            return;
        }
        directional.Add(new DirectionalLightSample(
            toLight / distance,
            spot.Color.ToVector3() * (cone * radial)));
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
        var attenuation = RadialAttenuation(distance, MathF.Abs(line.Range));
        if (attenuation <= 0 || distance <= 0.0001f)
        {
            return;
        }
        directional.Add(new DirectionalLightSample(
            toLight / distance,
            line.Color.ToVector3() * attenuation));
    }

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
            return 1;
        }
        if (cosine <= outer || inner <= outer)
        {
            return 0;
        }
        return (cosine - outer) / (inner - outer);
    }

    private static float ToCosine(float value)
    {
        if (!float.IsFinite(value))
        {
            return 1;
        }
        if (value is >= -1f and <= 1f)
        {
            return value;
        }
        var radians = value > MathF.PI ? MathHelper.DegreesToRadians(value) : value;
        return MathF.Cos(radians);
    }

    private static Vector3 SafeNormalize(Vector3 value) =>
        value.LengthSquared > 0.000001f && IsFinite(value)
            ? Vector3.Normalize(value)
            : Vector3.UnitY;

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static Vector3 ClampNonNegative(Vector3 value) => new(
        MathF.Max(0, value.X),
        MathF.Max(0, value.Y),
        MathF.Max(0, value.Z));

    private static bool Contains(Vector3 min, Vector3 max, Vector3 position) =>
        position.X >= MathF.Min(min.X, max.X) && position.X <= MathF.Max(min.X, max.X) &&
        position.Y >= MathF.Min(min.Y, max.Y) && position.Y <= MathF.Max(min.Y, max.Y) &&
        position.Z >= MathF.Min(min.Z, max.Z) && position.Z <= MathF.Max(min.Z, max.Z);
}
