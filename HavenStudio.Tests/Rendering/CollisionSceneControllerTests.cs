using HavenStudio.Editors;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;

namespace HavenStudio.Tests.Rendering;

public sealed class CollisionSceneControllerTests
{
    [Fact]
    public void Changing_race_selection_restores_previous_beacon_shape()
    {
        var host = new SceneHost();
        var controller = new CollisionSceneController(host);
        var first = CreateEffect(0x8FB28D);
        var second = CreateEffect(0x8FB28E);
        controller.RebuildEffectModels([first, second]);
        Assert.True(controller.TryGetEffectModel(first, out var firstModel));

        controller.SetRelatedEffectHighlight([first]);
        Assert.Equal(20000.0f, MaximumY(firstModel));

        controller.SetEffectSelection([second]);
        controller.SetRelatedEffectHighlight([second]);

        Assert.Equal(250.0f, MaximumY(firstModel));
    }

    [Fact]
    public void Selected_race_goal_uses_an_orange_twenty_thousand_unit_beacon()
    {
        var host = new SceneHost();
        var controller = new CollisionSceneController(host);
        var selected = CreateEffect(0x8FB28D);
        controller.RebuildEffectModels([selected]);
        Assert.True(controller.TryGetEffectModel(selected, out var model));

        controller.SetEffectSelection([selected]);
        controller.SetRaceEffectHighlights([selected], [], []);

        Assert.Equal(20000.0f, MaximumY(model));
        Assert.Equal(new OpenTK.Mathematics.Vector3(1.0f, 0.55f, 0.05f), model.Color);
    }

    [Fact]
    public void Available_and_unavailable_goals_use_green_and_red_beacons()
    {
        var host = new SceneHost();
        var controller = new CollisionSceneController(host);
        var available = CreateEffect(0x8FB28D);
        var unavailable = CreateEffect(0x8FB28E);
        controller.RebuildEffectModels([available, unavailable]);
        Assert.True(controller.TryGetEffectModel(available, out var availableModel));
        Assert.True(controller.TryGetEffectModel(unavailable, out var unavailableModel));

        controller.SetRaceEffectHighlights([], [available], [unavailable]);

        Assert.Equal(20000.0f, MaximumY(availableModel));
        Assert.Equal(20000.0f, MaximumY(unavailableModel));
        Assert.Equal(new OpenTK.Mathematics.Vector3(0.10f, 1.0f, 0.20f), availableModel.Color);
        Assert.Equal(new OpenTK.Mathematics.Vector3(0.95f, 0.15f, 0.15f), unavailableModel.Color);
    }

    [Fact]
    public void Selected_non_race_effect_remains_a_normal_sized_marker()
    {
        var host = new SceneHost();
        var controller = new CollisionSceneController(host);
        var ordinaryEffect = CreateEffect(0x123456);
        controller.RebuildEffectModels([ordinaryEffect]);
        Assert.True(controller.TryGetEffectModel(ordinaryEffect, out var model));

        controller.SetEffectSelection([ordinaryEffect]);
        controller.SetRaceEffectHighlights([], [], []);

        Assert.Equal(250.0f, MaximumY(model));
    }

    private static CollisionEffectViewModel CreateEffect(uint hash) => new(
        new GeoEffect { Name = unchecked((int)hash), W = 1.0f },
        () => { },
        () => { },
        _ => { });

    private static float MaximumY(Avalonia3DControl.Core.Models.Model3D model) =>
        Enumerable.Range(0, model.Positions.Length / 3)
            .Max(index => model.Positions[(index * 3) + 1]);
}
