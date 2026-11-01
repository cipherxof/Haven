using Avalonia;
using Avalonia3DControl.Core.Models;
using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Rendering;

public sealed class SelectionBoxPickerTests
{
    private static Model3D Triangle(params float[] positions) => new()
    {
        Positions = positions, Indices = [0, 1, 2], VertexCount = 3, IndexCount = 3
    };
    private static IReadOnlyList<SelectionHit> Find(Rect box, params Model3D[] models) =>
        SelectionBoxPicker.FindTriangles(box, Matrix4.Identity, Matrix4.Identity, 100, 100, models);

    [Fact]
    public void Box_includes_all_coincident_and_obscured_triangles_not_just_frontmost()
    {
        var front = Triangle(-1, -1, -0.5f, 1, -1, -0.5f, 0, 1, -0.5f);
        var coincident = Triangle(front.Positions);
        var behind = Triangle(-1, -1, 0.5f, 1, -1, 0.5f, 0, 1, 0.5f);
        var hits = Find(new Rect(48, 48, 4, 4), front, coincident, behind);
        Assert.Equal(3, hits.Count);
        Assert.Equal(new[] { front, coincident, behind }, hits.Select(hit => hit.Model));
        Assert.All(hits, hit => Assert.Equal(0, hit.TriangleIndex));
    }

    [Fact]
    public void Large_face_with_no_on_screen_vertices_still_intersects_the_box()
    {
        var triangle = Triangle(-10, -10, 0, 10, -10, 0, 0, 10, 0);
        Assert.Single(Find(new Rect(48, 48, 4, 4), triangle));
    }

    [Fact]
    public void Edge_crossings_count_without_triangle_vertices_or_box_corners_inside()
    {
        var triangle = Triangle(-0.8f, 0.08f, 0, 0.8f, 0.08f, 0, 0.8f, 0.12f, 0);
        Assert.Single(Find(new Rect(40, 40, 20, 20), triangle));
    }

    [Fact]
    public void Triangle_bounding_box_overlap_alone_does_not_select_it()
    {
        var triangle = Triangle(-1, -1, 0, 1, -1, 0, -1, 1, 0);
        Assert.Empty(Find(new Rect(80, 0, 10, 10), triangle));
    }

    [Fact]
    public void Near_plane_crossing_is_clipped_and_not_discarded()
    {
        var triangle = Triangle(-1, -1, -2, 1, -1, 0, 0, 1, 0);
        Assert.Single(Find(new Rect(48, 48, 4, 4), triangle));
    }

    [Fact]
    public void Outside_depth_range_and_hidden_or_invalid_triangles_are_excluded()
    {
        var beforeNear = Triangle(-1, -1, -2, 1, -1, -2, 0, 1, -2);
        var afterFar = Triangle(-1, -1, 2, 1, -1, 2, 0, 1, 2);
        var hidden = Triangle(-1, -1, 0, 1, -1, 0, 0, 1, 0);
        hidden.Visible = false;
        var invalid = Triangle(-1, -1, 0, 1, -1, 0, 0, 1, 0);
        invalid.Indices = [0, 1, 99];
        Assert.Empty(Find(new Rect(0, 0, 100, 100), beforeNear, afterFar, hidden, invalid));
    }

    [Fact]
    public void Transform_and_logical_pixel_coordinates_are_respected()
    {
        var triangle = Triangle(-0.05f, -0.05f, 0, 0.05f, -0.05f, 0, 0, 0.05f, 0);
        triangle.Position = new Vector3(0.5f, 0.5f, 0);
        Assert.Single(Find(new Rect(70, 20, 10, 10), triangle));
        Assert.Empty(Find(new Rect(45, 45, 10, 10), triangle));
    }

    [Fact]
    public void Perspective_faces_behind_camera_are_excluded()
    {
        var triangle = Triangle(-1, -1, 10, 1, -1, 10, 0, 1, 10);
        var projection = Matrix4.CreatePerspectiveFieldOfView(MathF.PI / 2, 1, 1, 100);
        Assert.Empty(SelectionBoxPicker.FindTriangles(new Rect(0, 0, 100, 100),
            Matrix4.Identity, projection, 100, 100, [triangle]));
    }

    [Fact]
    public void Degenerate_or_nonfinite_triangles_are_excluded()
    {
        Assert.Empty(Find(new Rect(0, 0, 100, 100),
            Triangle(0, 0, 0, 0, 0, 0, 0, 0, 0),
            Triangle(float.NaN, 0, 0, 1, -1, 0, 0, 1, 0)));
    }
}
