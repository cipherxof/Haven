using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia3DControl.Core.Models;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

/// <summary>Through-selection of triangles intersecting a logical-pixel rectangle.
/// Clip in homogeneous space: large faces, edge crossings, and near-plane
/// crossings must work even when none of the triangle's vertices is on screen.</summary>
public static class SelectionBoxPicker
{
    public static IReadOnlyList<SelectionHit> FindTriangles(Rect rectangle,
        Matrix4 view, Matrix4 projection, double width, double height,
        IEnumerable<Model3D> models)
    {
        var hits = new List<SelectionHit>();
        if (width <= 1 || height <= 1 || rectangle.Width <= 0 || rectangle.Height <= 0) return hits;
        rectangle = rectangle.Intersect(new Rect(0, 0, width, height));
        if (rectangle.Width <= 0 || rectangle.Height <= 0) return hits;
        var minX = (float)(rectangle.Left * 2 / width - 1);
        var maxX = (float)(rectangle.Right * 2 / width - 1);
        var minY = (float)(1 - rectangle.Bottom * 2 / height);
        var maxY = (float)(1 - rectangle.Top * 2 / height);
        var camera = Matrix4.Invert(view).Row3.Xyz;
        foreach (var model in models)
        {
            if (!model.Visible || model.Indices.Length == 0) continue;
            var transform = model.GetModelMatrix();
            var clipTransform = transform * view * projection;
            var vertices = new Vector4[model.Positions.Length / 3];
            for (var i = 0; i < vertices.Length; i++)
                vertices[i] = Vector4.TransformRow(new Vector4(model.Positions[i * 3],
                    model.Positions[i * 3 + 1], model.Positions[i * 3 + 2], 1), clipTransform);
            for (var i = 0; i + 2 < model.Indices.Length; i += 3)
            {
                var a = model.Indices[i]; var b = model.Indices[i + 1]; var c = model.Indices[i + 2];
                if (a >= vertices.Length || b >= vertices.Length || c >= vertices.Length ||
                    !Intersects(vertices[a], vertices[b], vertices[c], minX, maxX, minY, maxY)) continue;
                Vector3 World(uint index) => Vector3.TransformPosition(new Vector3(
                    model.Positions[index * 3], model.Positions[index * 3 + 1], model.Positions[index * 3 + 2]), transform);
                var distance = ((World(a) + World(b) + World(c)) / 3 - camera).Length;
                hits.Add(new SelectionHit(model, i / 3, distance));
            }
        }
        return hits;
    }

    private static bool Intersects(Vector4 a, Vector4 b, Vector4 c,
        float minX, float maxX, float minY, float maxY)
    {
        if (!Finite(a) || !Finite(b) || !Finite(c)) return false;
        Span<Vector4> polygon = stackalloc Vector4[12];
        Span<Vector4> clipped = stackalloc Vector4[12];
        polygon[0] = a; polygon[1] = b; polygon[2] = c;
        var count = 3;
        for (var plane = 0; plane < 7; plane++)
        {
            var nextCount = 0;
            var previous = polygon[count - 1];
            var previousDistance = Distance(previous, plane, minX, maxX, minY, maxY);
            for (var i = 0; i < count; i++)
            {
                var current = polygon[i];
                var currentDistance = Distance(current, plane, minX, maxX, minY, maxY);
                if ((previousDistance >= 0) != (currentDistance >= 0))
                    clipped[nextCount++] = previous + (current - previous) *
                        (previousDistance / (previousDistance - currentDistance));
                if (currentDistance >= 0) clipped[nextCount++] = current;
                previous = current;
                previousDistance = currentDistance;
            }
            if (nextCount < 3) return false;
            clipped[..nextCount].CopyTo(polygon);
            count = nextCount;
        }
        // Reject degenerate triangles/edge-only contact with the selection box.
        double area = 0;
        for (var i = 0; i < count; i++)
        {
            var p = polygon[i]; var q = polygon[(i + 1) % count];
            area += (double)p.X / p.W * q.Y / q.W - (double)q.X / q.W * p.Y / p.W;
        }
        return Math.Abs(area) > 1e-12;
    }

    private static bool Finite(Vector4 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) &&
        float.IsFinite(p.Z) && float.IsFinite(p.W);
    private static float Distance(Vector4 p, int plane, float minX, float maxX, float minY, float maxY) => plane switch
    {
        0 => p.W - 0.000001f,
        1 => p.X - minX * p.W,
        2 => maxX * p.W - p.X,
        3 => p.Y - minY * p.W,
        4 => maxY * p.W - p.Y,
        5 => p.Z + p.W,
        _ => p.W - p.Z
    };
}
