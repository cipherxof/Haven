using Avalonia3DControl.Core.Models;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Rendering;

public sealed class Model3DTransformTests
{
    [Fact]
    public void Post_transparent_rendering_is_opt_in()
    {
        var model = new Model3D();

        Assert.False(model.RenderAfterTransparent);

        model.RenderAfterTransparent = true;
        Assert.True(model.RenderAfterTransparent);
    }

    [Fact]
    public void Placement_matrix_applies_xyz_rotation_then_translation()
    {
        var rotation = new Vector3(0.31f, -0.67f, 1.13f);
        var position = new Vector3(10f, 20f, 30f);
        var source = new Vector3(2f, 3f, 4f);
        var model = new Model3D
        {
            Rotation = rotation,
            Position = position,
            Scale = Vector3.One
        };

        var expected = Vector3.TransformPosition(source, Matrix4.CreateRotationX(rotation.X));
        expected = Vector3.TransformPosition(expected, Matrix4.CreateRotationY(rotation.Y));
        expected = Vector3.TransformPosition(expected, Matrix4.CreateRotationZ(rotation.Z));
        expected += position;
        var actual = Vector3.TransformPosition(source, model.GetModelMatrix());

        Assert.Equal(expected.X, actual.X, 5);
        Assert.Equal(expected.Y, actual.Y, 5);
        Assert.Equal(expected.Z, actual.Z, 5);
    }
}
