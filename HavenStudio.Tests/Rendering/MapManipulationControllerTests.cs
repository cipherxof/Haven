using HavenStudio.Rendering;
using Avalonia3DControl.Core.Models;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Rendering;

public sealed class MapManipulationControllerTests
{
    [Fact]
    public void Click_requires_less_than_four_pixels_of_movement()
    {
        var host = new SceneHost();
        var controller = new MapManipulationController(host);
        controller.PointerPressed(new Avalonia.Point(10, 10), target: null);
        controller.TryUpdate(new Avalonia.Point(13, 12), host.ViewportControl, false, out _);

        Assert.True(controller.PointerReleased().IsClick);

        controller.PointerPressed(new Avalonia.Point(10, 10), target: null);
        controller.TryUpdate(new Avalonia.Point(14, 10), host.ViewportControl, false, out _);

        Assert.False(controller.PointerReleased().IsClick);
    }

    [Fact]
    public void Ground_plane_drag_preserves_entity_height()
    {
        var initial = new Vector3(10, 5, 20);

        var success = MapManipulationController.TryCalculatePlaneDrag(
            initial,
            new Vector3(0, 10, 0),
            -Vector3.UnitY,
            new Vector3(3, 10, 4),
            -Vector3.UnitY,
            Vector3.UnitY,
            out var position);

        Assert.True(success);
        Assert.Equal(new Vector3(13, 5, 24), position);
    }

    [Fact]
    public void Camera_facing_plane_drag_can_change_height()
    {
        var initial = new Vector3(10, 5, 20);

        var success = MapManipulationController.TryCalculatePlaneDrag(
            initial,
            new Vector3(0, 0, 30),
            -Vector3.UnitZ,
            new Vector3(3, 7, 30),
            -Vector3.UnitZ,
            Vector3.UnitZ,
            out var position);

        Assert.True(success);
        Assert.Equal(new Vector3(13, 12, 20), position);
    }

    [Fact]
    public void Axis_drag_changes_only_the_constrained_axis()
    {
        var initial = new Vector3(10, 5, 20);

        var success = MapManipulationController.TryCalculateAxisDrag(
            initial,
            new Vector3(2, 5, 30),
            -Vector3.UnitZ,
            new Vector3(7, 50, 30),
            -Vector3.UnitZ,
            Vector3.UnitX,
            Vector3.UnitZ,
            out var position);

        Assert.True(success);
        Assert.Equal(new Vector3(15, 5, 20), position);
    }

    [Fact]
    public void Height_axis_drag_preserves_horizontal_position()
    {
        var initial = new Vector3(10, 5, 20);

        var success = MapManipulationController.TryCalculateAxisDrag(
            initial,
            new Vector3(0, 0, 30),
            -Vector3.UnitZ,
            new Vector3(100, 12, 30),
            -Vector3.UnitZ,
            Vector3.UnitY,
            Vector3.UnitZ,
            out var position);

        Assert.True(success);
        Assert.Equal(new Vector3(10, 17, 20), position);
    }

    [Fact]
    public void Group_preview_preserves_each_models_offset_from_the_anchor()
    {
        var host = new SceneHost();
        var controller = new MapManipulationController(host);
        var first = new Model3D { Position = new Vector3(10, 5, 20) };
        var second = new Model3D { Position = new Vector3(14, 8, 18) };
        var target = new MapManipulationTarget(
            new object(),
            new Vector3(12, 6.5f, 19),
            [first, second]);

        controller.PreviewPosition(target, new Vector3(22, 16.5f, 29));

        Assert.Equal(new Vector3(20, 15, 30), first.Position);
        Assert.Equal(new Vector3(24, 18, 28), second.Position);
    }
}
