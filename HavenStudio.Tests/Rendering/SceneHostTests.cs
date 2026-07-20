using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Materials;
using HavenStudio.Editors;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Formats.Mdn;
using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Rendering;

public sealed class SceneHostTests
{
    [Fact]
    public void Collision_and_lights_are_hidden_by_default()
    {
        var host = new SceneHost();

        Assert.False(host.IsLayerVisible(SceneLayer.Collision));
        Assert.False(host.IsLayerVisible(SceneLayer.Lights));
        Assert.True(host.IsLayerVisible(SceneLayer.VisualModels));
        Assert.True(host.IsLayerVisible(SceneLayer.Effects));
    }

    [Fact]
    public void Rotating_a_model_does_not_rotate_its_world_position()
    {
        var model = new Model3D
        {
            Position = new Vector3(70750f, 0f, -57325.184f),
            Rotation = new Vector3(0f, -1.8093303f, 0f),
            Scale = Vector3.One
        };

        var transformedOrigin = Vector3.TransformPosition(Vector3.Zero, model.GetModelMatrix());

        Assert.Equal(model.Position.X, transformedOrigin.X, 3);
        Assert.Equal(model.Position.Y, transformedOrigin.Y, 3);
        Assert.Equal(model.Position.Z, transformedOrigin.Z, 3);
    }

    [Fact]
    public void Replacing_one_layer_preserves_the_other_layers()
    {
        var host = new SceneHost();
        var originalCollision = new Model3D { Name = "collision" };
        var replacementCollision = new Model3D { Name = "replacement" };
        var effect = new Model3D { Name = "effect" };

        host.ReplaceLayer(SceneLayer.Collision, [originalCollision]);
        host.ReplaceLayer(SceneLayer.Effects, [effect]);
        host.ReplaceLayer(SceneLayer.Collision, [replacementCollision]);

        Assert.Equal([replacementCollision], host.GetLayerModels(SceneLayer.Collision));
        Assert.Equal([effect], host.GetLayerModels(SceneLayer.Effects));
        Assert.DoesNotContain(originalCollision, host.Scene.Models);
        Assert.Contains(replacementCollision, host.Scene.Models);
        Assert.Contains(effect, host.Scene.Models);
    }

    [Fact]
    public void Layer_visibility_restores_each_models_own_visibility()
    {
        var host = new SceneHost();
        var visible = new Model3D { Visible = true };
        var hidden = new Model3D { Visible = false };
        host.ReplaceLayer(SceneLayer.Collision, [visible, hidden]);

        host.SetLayerVisible(SceneLayer.Collision, false);
        Assert.False(visible.Visible);
        Assert.False(hidden.Visible);

        host.SetLayerVisible(SceneLayer.Collision, true);
        Assert.True(visible.Visible);
        Assert.False(hidden.Visible);

        host.SetLayerVisible(SceneLayer.Collision, false);
        host.SetModelVisible(SceneLayer.Collision, visible, false);
        host.SetLayerVisible(SceneLayer.Collision, true);
        Assert.False(visible.Visible);
        Assert.False(hidden.Visible);
    }

    [Fact]
    public void Layer_render_mode_only_affects_models_in_that_layer()
    {
        var host = new SceneHost();
        var visual = new Model3D();
        var collision = new Model3D();
        var effect = new Model3D { RenderModeOverride = RenderMode.Point };
        host.ReplaceLayer(SceneLayer.VisualModels, [visual]);
        host.ReplaceLayer(SceneLayer.Collision, [collision]);
        host.ReplaceLayer(SceneLayer.Effects, [effect]);

        host.SetLayerRenderMode(SceneLayer.Collision, RenderMode.Line);

        Assert.Null(visual.RenderModeOverride);
        Assert.Equal(RenderMode.Line, collision.RenderModeOverride);
        Assert.Equal(RenderMode.Point, effect.RenderModeOverride);

        host.SetLayerRenderMode(SceneLayer.Collision, null);
        Assert.Null(collision.RenderModeOverride);
    }

    [Fact]
    public void Visual_model_batches_keep_their_placement_association()
    {
        var host = new SceneHost();
        var placement = new PlacedModelReference { ModelHash = 0x12345678 };
        var model = new Model3D();
        var collision = new Model3D();
        host.ReplaceLayer(SceneLayer.Collision, [collision]);
        var batch = new MdnSceneBatch(
            new Mdn(),
            [model],
            new Dictionary<uint, ResolvedTexture>(),
            placement);

        host.ReplaceModels([batch]);

        Assert.True(host.TryGetPlacement(model, out var resolved));
        Assert.Same(placement, resolved);
        Assert.Equal([model], host.GetLayerModels(SceneLayer.VisualModels));
        Assert.Equal([collision], host.GetLayerModels(SceneLayer.Collision));
        Assert.Contains(collision, host.Scene.Models);
    }

    [Fact]
    public void Stage_models_and_placements_have_independent_visibility()
    {
        var host = new SceneHost();
        var placement = new PlacedModelReference { ModelHash = 0x12345678 };
        var stageModel = new Model3D { Name = "stage" };
        var placementModel = new Model3D { Name = "placement" };
        host.ReplaceModels(
        [
            Batch(stageModel, null),
            Batch(placementModel, placement)
        ]);

        host.SetPlacementsVisible(false);
        Assert.True(stageModel.Visible);
        Assert.False(placementModel.Visible);

        host.SetStageModelsVisible(false);
        host.SetPlacementsVisible(true);
        Assert.False(stageModel.Visible);
        Assert.True(placementModel.Visible);

        host.SetStageModelsVisible(true);
        Assert.True(stageModel.Visible);
        Assert.True(placementModel.Visible);

        host.SetPlacementsVisible(false);
        var replacement = new Model3D { Name = "replacement placement" };
        host.ReplacePlacementModels(
            [placement],
            [Batch(replacement, placement)]);
        Assert.True(stageModel.Visible);
        Assert.False(replacement.Visible);

        static MdnSceneBatch Batch(Model3D model, PlacedModelReference? placement)
        {
            return new MdnSceneBatch(
                new Mdn(),
                [model],
                new Dictionary<uint, ResolvedTexture>(),
                placement);
        }
    }

    [Fact]
    public void Replacing_placement_models_preserves_stage_and_other_placements()
    {
        var host = new SceneHost();
        var editedPlacement = new PlacedModelReference { ModelHash = 0x111111 };
        var otherPlacement = new PlacedModelReference { ModelHash = 0x222222 };
        var stageModel = new Model3D { Name = "stage" };
        var oldEditedModel = new Model3D { Name = "old edited" };
        var otherModel = new Model3D { Name = "other" };
        host.ReplaceModels(
        [
            Batch(stageModel, null),
            Batch(oldEditedModel, editedPlacement),
            Batch(otherModel, otherPlacement)
        ]);
        var replacement = new Model3D { Name = "replacement" };

        host.ReplacePlacementModels(
            [editedPlacement],
            [Batch(replacement, editedPlacement)]);

        Assert.Equal([stageModel, otherModel, replacement], host.GetLayerModels(SceneLayer.VisualModels));
        Assert.DoesNotContain(oldEditedModel, host.Scene.Models);
        Assert.Contains(stageModel, host.Scene.Models);
        Assert.Contains(otherModel, host.Scene.Models);
        Assert.Contains(replacement, host.Scene.Models);
        Assert.False(host.TryGetPlacement(stageModel, out _));
        Assert.True(host.TryGetPlacement(otherModel, out var resolvedOther));
        Assert.Same(otherPlacement, resolvedOther);
        Assert.True(host.TryGetPlacement(replacement, out var resolvedReplacement));
        Assert.Same(editedPlacement, resolvedReplacement);

        static MdnSceneBatch Batch(Model3D model, PlacedModelReference? placement)
        {
            return new MdnSceneBatch(
                new Mdn(),
                [model],
                new Dictionary<uint, ResolvedTexture>(),
                placement);
        }
    }

    [Fact]
    public void Map_outline_tracks_placement_batches()
    {
        var host = new SceneHost();
        var collisionEditor = new CollisionEditorViewModel(host);
        using var gcxEditor = new GcxEditorViewModel(host);
        using var mapEditor = new MapEditorViewModel(host, collisionEditor, gcxEditor);
        Assert.False(mapEditor.InspectorExpanded);
        mapEditor.ToggleInspector();
        Assert.False(mapEditor.InspectorExpanded);
        var placement = new PlacedModelReference { ModelHash = 0x12345678 };
        var model = new Model3D();
        var batch = new MdnSceneBatch(
            new Mdn(),
            [model],
            new Dictionary<uint, ResolvedTexture>(),
            placement);

        host.ReplaceModels([batch]);

        var entity = Assert.IsType<PlacementEntity>(Assert.Single(mapEditor.Outline[0].Children));
        Assert.Same(placement, entity.Placement);
        mapEditor.SelectedOutlineItem = entity;
        Assert.Same(entity, mapEditor.SelectedEntity);
        Assert.True(mapEditor.InspectorExpanded);

        mapEditor.SelectAt(new Avalonia.Point(0, 0), host.ViewportControl);
        Assert.Same(entity, mapEditor.SelectedEntity);
        Assert.True(mapEditor.InspectorExpanded);

        mapEditor.SelectedOutlineItem = null;
        Assert.Null(mapEditor.SelectedEntity);
        Assert.False(mapEditor.InspectorExpanded);
    }
}
