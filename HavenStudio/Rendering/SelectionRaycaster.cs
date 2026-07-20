using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia3DControl;
using Avalonia3DControl.Core.Models;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public static class SelectionRaycaster
{
    public static Model3D? PickModel(Point point, OpenGL3DControl control, IEnumerable<Model3D> models)
    {
        if (!TryGetPickRay(point, control, out var rayOrigin, out var rayDirection))
        {
            return null;
        }

        float bestDistance = float.MaxValue;
        Model3D? bestModel = null;

        foreach (var model in models)
        {
            if (!RayIntersectsModel(rayOrigin, rayDirection, model, out var distance))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestModel = model;
            }
        }

        return bestModel;
    }

    public static bool TryPickTriangle(Point point, OpenGL3DControl control, IEnumerable<Model3D> models, out SelectionHit hit)
    {
        hit = default;
        if (!TryGetPickRay(point, control, out var rayOrigin, out var rayDirection))
        {
            return false;
        }

        float bestDistance = float.MaxValue;
        Model3D? bestModel = null;
        int bestTriangle = -1;
        var cameraPos = control.Scene.Camera.Position;

        foreach (var model in models)
        {
            if (!RayIntersectsModel(rayOrigin, rayDirection, model, out var distance, out var triangleIndex))
            {
                continue;
            }

            var hitPoint = rayOrigin + rayDirection * distance;
            var cameraDistance = (hitPoint - cameraPos).Length;
            if (cameraDistance < bestDistance)
            {
                bestDistance = cameraDistance;
                bestModel = model;
                bestTriangle = triangleIndex;
            }
        }

        if (bestModel == null || bestTriangle < 0)
        {
            return false;
        }

        hit = new SelectionHit(bestModel, bestTriangle, bestDistance);
        return true;
    }

    public static bool TryPickPoint(
        Point point,
        OpenGL3DControl control,
        IEnumerable<Model3D> models,
        out Vector3 hitPoint)
    {
        hitPoint = Vector3.Zero;
        if (!TryGetPickRay(point, control, out var origin, out var direction))
        {
            return false;
        }

        var bestDistance = float.MaxValue;
        foreach (var model in models)
        {
            if (model.Visible &&
                RayIntersectsModel(origin, direction, model, out var distance) &&
                distance >= 0 && distance < bestDistance)
            {
                bestDistance = distance;
            }
        }

        if (bestDistance == float.MaxValue)
        {
            return false;
        }
        hitPoint = origin + direction * bestDistance;
        return true;
    }

    public static bool TryGetPickRay(Point point, OpenGL3DControl control, out Vector3 origin, out Vector3 direction)
    {
        origin = Vector3.Zero;
        direction = -Vector3.UnitZ;

        var topLevel = TopLevel.GetTopLevel(control);
        var renderScaling = topLevel?.RenderScaling ?? 1.0;

        var pixelWidth = (float)(control.Bounds.Width * renderScaling);
        var pixelHeight = (float)(control.Bounds.Height * renderScaling);

        if (pixelWidth <= 1 || pixelHeight <= 1)
        {
            return false;
        }

        var camera = control.Scene.Camera;
        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();

        // Scale point to pixel coordinates
        float x = (float)(point.X * renderScaling);
        float y = (float)(point.Y * renderScaling);

        // Unproject near and far points (like old code did)
        var nearWorld = Unproject(x, y, 0.0f, view, projection, pixelWidth, pixelHeight);
        var farWorld = Unproject(x, y, 1.0f, view, projection, pixelWidth, pixelHeight);

        origin = nearWorld;
        direction = Vector3.Normalize(farWorld - nearWorld);
        return true;
    }

    private static Vector3 Unproject(float screenX, float screenY, float depth, Matrix4 view, Matrix4 projection, float viewportWidth, float viewportHeight)
    {
        // Convert screen coords to NDC [-1, 1]
        float ndcX = (2.0f * screenX) / viewportWidth - 1.0f;
        float ndcY = 1.0f - (2.0f * screenY) / viewportHeight;
        float ndcZ = 2.0f * depth - 1.0f;

        // Invert projection * view (note: OpenTK uses row-major, so we invert each and multiply in reverse)
        var invProj = Matrix4.Invert(projection);
        var invView = Matrix4.Invert(view);

        // Transform from clip space to view space to world space
        var clipPos = new Vector4(ndcX, ndcY, ndcZ, 1.0f);
        var viewPos = Vector4.TransformRow(clipPos, invProj);
        viewPos /= viewPos.W;
        var worldPos = Vector4.TransformRow(viewPos, invView);

        return worldPos.Xyz;
    }

    private static bool RayIntersectsModel(Vector3 origin, Vector3 direction, Model3D model, out float distance)
    {
        return RayIntersectsModel(origin, direction, model, out distance, out _);
    }

    private static bool RayIntersectsModel(Vector3 origin, Vector3 direction, Model3D model, out float distance, out int triangleIndex)
    {
        distance = float.MaxValue;
        triangleIndex = -1;

        var indices = model.Indices;
        var positions = model.Positions;
        if (indices.Length == 0 || positions.Length == 0)
        {
            return false;
        }

        var modelMatrix = model.GetModelMatrix();

        // GetBoundingBox() already returns world-space bounds (transforms internally)
        var bounds = model.GetBoundingBox();
        var worldMin = bounds.Min;
        var worldMax = bounds.Max;

        // Expand thin/flat AABBs by a small epsilon to handle 2D geometry (like effect flags)
        const float epsilon = 0.5f;
        var size = worldMax - worldMin;
        if (size.X < epsilon) { worldMin.X -= epsilon; worldMax.X += epsilon; }
        if (size.Y < epsilon) { worldMin.Y -= epsilon; worldMax.Y += epsilon; }
        if (size.Z < epsilon) { worldMin.Z -= epsilon; worldMax.Z += epsilon; }

        if (!RayIntersectsAabb(origin, direction, worldMin, worldMax, out _))
        {
            return false;
        }

        bool hit = false;

        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int i0 = (int)indices[i] * 3;
            int i1 = (int)indices[i + 1] * 3;
            int i2 = (int)indices[i + 2] * 3;

            if (i0 + 2 >= positions.Length || i1 + 2 >= positions.Length || i2 + 2 >= positions.Length)
            {
                continue;
            }

            var v0 = Vector3.TransformPosition(new Vector3(positions[i0], positions[i0 + 1], positions[i0 + 2]), modelMatrix);
            var v1 = Vector3.TransformPosition(new Vector3(positions[i1], positions[i1 + 1], positions[i1 + 2]), modelMatrix);
            var v2 = Vector3.TransformPosition(new Vector3(positions[i2], positions[i2 + 1], positions[i2 + 2]), modelMatrix);

            if (RayIntersectsTriangle(origin, direction, v0, v1, v2, out var t))
            {
                hit = true;
                if (t < distance)
                {
                    distance = t;
                    triangleIndex = i / 3;
                }
            }
        }

        return hit;
    }

    private static bool RayIntersectsAabb(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, out float tmin)
    {
        tmin = 0;
        float tmax = float.MaxValue;

        if (!AxisTest(origin.X, direction.X, min.X, max.X, ref tmin, ref tmax))
        {
            return false;
        }

        if (!AxisTest(origin.Y, direction.Y, min.Y, max.Y, ref tmin, ref tmax))
        {
            return false;
        }

        if (!AxisTest(origin.Z, direction.Z, min.Z, max.Z, ref tmin, ref tmax))
        {
            return false;
        }

        return tmax >= MathF.Max(tmin, 0);
    }

    private static bool AxisTest(float origin, float direction, float min, float max, ref float tmin, ref float tmax)
    {
        if (MathF.Abs(direction) < 1e-6f)
        {
            return origin >= min && origin <= max;
        }

        float inv = 1.0f / direction;
        float t1 = (min - origin) * inv;
        float t2 = (max - origin) * inv;

        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        tmin = MathF.Max(tmin, t1);
        tmax = MathF.Min(tmax, t2);
        return tmin <= tmax;
    }

    private static bool RayIntersectsTriangle(Vector3 origin, Vector3 direction, Vector3 v0, Vector3 v1, Vector3 v2, out float t)
    {
        t = 0;
        const float epsilon = 1e-6f;

        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var pvec = Vector3.Cross(direction, edge2);
        float det = Vector3.Dot(edge1, pvec);
        if (det > -epsilon && det < epsilon)
        {
            return false;
        }

        float invDet = 1.0f / det;
        var tvec = origin - v0;
        float u = Vector3.Dot(tvec, pvec) * invDet;
        if (u < 0 || u > 1)
        {
            return false;
        }

        var qvec = Vector3.Cross(tvec, edge1);
        float v = Vector3.Dot(direction, qvec) * invDet;
        if (v < 0 || u + v > 1)
        {
            return false;
        }

        t = Vector3.Dot(edge2, qvec) * invDet;
        return t > epsilon;
    }
}

public readonly struct SelectionHit
{
    public SelectionHit(Model3D model, int triangleIndex, float distance)
    {
        Model = model;
        TriangleIndex = triangleIndex;
        Distance = distance;
    }

    public Model3D Model { get; }
    public int TriangleIndex { get; }
    public float Distance { get; }
}
