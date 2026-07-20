using System;
using System.Collections.Generic;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Materials;
using HavenStudio.Editors;
using HavenStudio.Formats.Lit;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public static class LightSceneBuilder
{
    private const float MarkerRadius = 75f;
    private const int Segments = 16;

    public static IReadOnlyList<Model3D> BuildEntity(LightEntity entity, bool includeGroupBounds)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var models = new List<Model3D>();
        var color = entity.GetColor()?.ToVector3() ?? new Vector3(0.22f, 0.22f, 0.28f);
        var position = entity.GetPosition() ?? Vector3.Zero;

        if (entity.IsGlobal)
        {
            models.Add(BuildArrow(
                position,
                SafeDirection(entity.Session.Document.Direction.Xyz),
                500f,
                color,
                $"{entity.FileName}:global"));
        }
        else
        {
            models.Add(BuildOctahedron(position, MarkerRadius, color, $"{entity.FileName}:{entity.DisplayName}"));
            switch (entity.Light)
            {
                case LitPointLight point:
                    AddRangeSphere(models, point.Point.Xyz, point.Range, color, "range");
                    AddRangeSphere(models, point.Point.Xyz, point.ExtendedRange, color * 0.65f, "extended range");
                    break;
                case LitSpotLight spot:
                    models.Add(BuildCone(
                        spot.Point.Xyz,
                        SafeDirection(spot.Direction.Xyz),
                        ResolveSpotLength(spot, entity.Group),
                        ResolveSpotAngle(spot.Penumbra),
                        color,
                        "spot cone"));
                    break;
                case LitLineLight line:
                    models.Add(BuildArrow(
                        line.Point.Xyz,
                        SafeDirection(line.Direction.Xyz),
                        ResolveLineMarkerLength(line.Range),
                        color,
                        "line light"));
                    break;
                case LitBlackPoint blackPoint:
                    AddRangeSphere(models, blackPoint.Point.Xyz, blackPoint.Range,
                        new Vector3(0.08f, 0.08f, 0.11f), "black range");
                    break;
                case LitParallelLight parallel:
                    models.Add(BuildArrow(
                        position,
                        SafeDirection(parallel.Direction.Xyz),
                        400f,
                        color,
                        "parallel direction"));
                    break;
            }
        }

        if (includeGroupBounds && entity.Group is { } group)
        {
            models.Add(BuildBounds(group.BoundsMin.Xyz, group.BoundsMax.Xyz,
                new Vector3(1f, 0.82f, 0.24f), "light group bounds"));
        }

        foreach (var model in models)
        {
            for (var index = 0; index + 2 < model.Positions.Length; index += 3)
            {
                model.Positions[index] -= position.X;
                model.Positions[index + 1] -= position.Y;
                model.Positions[index + 2] -= position.Z;
            }
            model.Position = position;
            model.MaterialIndex = -1;
            model.Visible = true;
        }

        return models;
    }

    public static Model3D BuildBounds(Vector3 min, Vector3 max, Vector3 color, string name)
    {
        float[] positions =
        [
            min.X, min.Y, min.Z, max.X, min.Y, min.Z, max.X, max.Y, min.Z, min.X, max.Y, min.Z,
            min.X, min.Y, max.Z, max.X, min.Y, max.Z, max.X, max.Y, max.Z, min.X, max.Y, max.Z
        ];
        uint[] indices =
        [
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            3, 2, 6, 3, 6, 7,
            0, 3, 7, 0, 7, 4,
            1, 5, 6, 1, 6, 2
        ];
        return CreateModel(name, positions, indices, color, RenderMode.Line, 0.9f);
    }

    private static void AddRangeSphere(
        ICollection<Model3D> models,
        Vector3 center,
        float radius,
        Vector3 color,
        string name)
    {
        if (float.IsFinite(radius) && radius > 0)
        {
            models.Add(BuildSphere(center, radius, color, name));
        }
    }

    private static Model3D BuildOctahedron(Vector3 center, float radius, Vector3 color, string name)
    {
        float[] positions =
        [
            center.X, center.Y + radius, center.Z,
            center.X, center.Y - radius, center.Z,
            center.X + radius, center.Y, center.Z,
            center.X - radius, center.Y, center.Z,
            center.X, center.Y, center.Z + radius,
            center.X, center.Y, center.Z - radius
        ];
        uint[] indices =
        [
            0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2,
            1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5
        ];
        return CreateModel(name, positions, indices, color, null, 1f);
    }

    private static Model3D BuildSphere(Vector3 center, float radius, Vector3 color, string name)
    {
        var positions = new List<float>((Segments + 1) * (Segments + 1) * 3);
        var indices = new List<uint>(Segments * Segments * 6);
        for (var latitude = 0; latitude <= Segments; latitude++)
        {
            var phi = MathF.PI * latitude / Segments;
            var y = MathF.Cos(phi);
            var ring = MathF.Sin(phi);
            for (var longitude = 0; longitude <= Segments; longitude++)
            {
                var theta = 2f * MathF.PI * longitude / Segments;
                positions.Add(center.X + radius * ring * MathF.Cos(theta));
                positions.Add(center.Y + radius * y);
                positions.Add(center.Z + radius * ring * MathF.Sin(theta));
            }
        }

        for (var latitude = 0; latitude < Segments; latitude++)
        {
            for (var longitude = 0; longitude < Segments; longitude++)
            {
                var current = (uint)(latitude * (Segments + 1) + longitude);
                var next = current + (uint)Segments + 1;
                indices.Add(current);
                indices.Add(next);
                indices.Add(current + 1);
                indices.Add(current + 1);
                indices.Add(next);
                indices.Add(next + 1);
            }
        }

        return CreateModel(name, positions.ToArray(), indices.ToArray(), color, RenderMode.Line, 0.45f);
    }

    private static Model3D BuildCone(
        Vector3 tip,
        Vector3 direction,
        float length,
        float angle,
        Vector3 color,
        string name)
    {
        var baseCenter = tip + direction * length;
        var radius = MathF.Tan(Math.Clamp(angle, 0.01f, 1.45f)) * length;
        var up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) < 0.9f ? Vector3.UnitY : Vector3.UnitZ;
        var right = Vector3.Normalize(Vector3.Cross(direction, up));
        up = Vector3.Normalize(Vector3.Cross(right, direction));
        var positions = new List<float>((Segments + 2) * 3) { tip.X, tip.Y, tip.Z };
        for (var index = 0; index <= Segments; index++)
        {
            var angleAround = 2f * MathF.PI * index / Segments;
            var point = baseCenter + right * (MathF.Cos(angleAround) * radius) + up * (MathF.Sin(angleAround) * radius);
            positions.Add(point.X);
            positions.Add(point.Y);
            positions.Add(point.Z);
        }

        var indices = new List<uint>(Segments * 3);
        for (var index = 0; index < Segments; index++)
        {
            indices.Add(0);
            indices.Add((uint)index + 1);
            indices.Add((uint)index + 2);
        }

        return CreateModel(name, positions.ToArray(), indices.ToArray(), color, RenderMode.Line, 0.65f);
    }

    private static Model3D BuildArrow(
        Vector3 origin,
        Vector3 direction,
        float length,
        Vector3 color,
        string name)
    {
        var end = origin + direction * length;
        var up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        var side = Vector3.Normalize(Vector3.Cross(direction, up)) * MathF.Max(length * 0.035f, 20f);
        var headBase = end - direction * MathF.Max(length * 0.2f, 80f);
        float[] positions =
        [
            origin.X - side.X, origin.Y - side.Y, origin.Z - side.Z,
            origin.X + side.X, origin.Y + side.Y, origin.Z + side.Z,
            headBase.X + side.X, headBase.Y + side.Y, headBase.Z + side.Z,
            headBase.X - side.X, headBase.Y - side.Y, headBase.Z - side.Z,
            end.X, end.Y, end.Z
        ];
        uint[] indices = [0, 1, 2, 0, 2, 3, 3, 2, 4];
        return CreateModel(name, positions, indices, color, null, 0.9f);
    }

    private static Model3D CreateModel(
        string name,
        float[] positions,
        uint[] indices,
        Vector3 color,
        RenderMode? renderMode,
        float alpha)
    {
        var colors = new float[positions.Length / 3 * 4];
        for (var index = 0; index < colors.Length / 4; index++)
        {
            colors[index * 4] = color.X;
            colors[index * 4 + 1] = color.Y;
            colors[index * 4 + 2] = color.Z;
            colors[index * 4 + 3] = alpha;
        }

        return new Model3D
        {
            Name = name,
            Positions = positions,
            Colors = colors,
            Indices = indices,
            VertexCount = positions.Length / 3,
            IndexCount = indices.Length,
            Color = color,
            Alpha = alpha,
            RenderModeOverride = renderMode
        };
    }

    private static Vector3 SafeDirection(Vector3 direction) =>
        direction.LengthSquared > 0.000001f &&
        float.IsFinite(direction.X) && float.IsFinite(direction.Y) && float.IsFinite(direction.Z)
            ? Vector3.Normalize(direction)
            : -Vector3.UnitY;

    private static float ResolveSpotLength(LitSpotLight spot, LitGroup? group)
    {
        if (group == null)
        {
            return 500f;
        }

        var diagonal = (group.BoundsMax.Xyz - group.BoundsMin.Xyz).Length;
        return float.IsFinite(diagonal) && diagonal > 1 ? Math.Clamp(diagonal * 0.35f, 200f, 3000f) : 500f;
    }

    private static float ResolveLineMarkerLength(float range)
    {
        // Line-light dir.xyz is a normalized axis, not a second world-space point.
        // Keep the direction marker useful without allowing a large influence range
        // to cover the stage with marker geometry.
        return float.IsFinite(range)
            ? Math.Clamp(MathF.Abs(range), 300f, 3000f)
            : 750f;
    }

    private static float ResolveSpotAngle(float value)
    {
        if (!float.IsFinite(value))
        {
            return MathF.PI / 4f;
        }
        if (value is >= -1f and <= 1f)
        {
            return MathF.Acos(Math.Clamp(value, -1f, 1f));
        }
        if (value > MathF.PI)
        {
            return MathHelper.DegreesToRadians(Math.Clamp(value, 1f, 85f));
        }
        return Math.Clamp(value, 0.01f, 1.45f);
    }
}
