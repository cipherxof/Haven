using Avalonia;
using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Rendering;

public sealed class SelectionRaycasterTests
{
    [Fact]
    public void Project_to_viewport_maps_clip_space_to_logical_screen_coordinates()
    {
        var projected = SelectionRaycaster.TryProjectToViewport(
            new Vector3(0.5f, 0.5f, 0),
            Matrix4.Identity,
            Matrix4.Identity,
            viewportWidth: 100,
            viewportHeight: 50,
            out var screen);

        Assert.True(projected);
        Assert.Equal(new Point(75, 12.5), screen);
    }

    [Fact]
    public void Project_to_viewport_rejects_points_outside_the_clip_volume()
    {
        var projected = SelectionRaycaster.TryProjectToViewport(
            new Vector3(2, 0, 0),
            Matrix4.Identity,
            Matrix4.Identity,
            viewportWidth: 100,
            viewportHeight: 50,
            out _);

        Assert.False(projected);
    }
}
