using Avalonia3DControl.Core.Models;
using HavenStudio.Editors;
using HavenStudio.Editors.Lighting;
using HavenStudio.Extensions;
using HavenStudio.Rendering;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;

namespace HavenStudio.Tests.Rendering;

public sealed class LightingMapIntegrationTests
{
    [Fact]
    public async Task Map_editor_discovers_lights_builds_outline_and_toggles_layer()
    {
        using var temp = new TempDirectory();
        var stagePath = temp.GetPath("mo_st01_d.lt2");
        var skyPath = temp.GetPath("mo_st01_sky_d.lt2");
        await File.WriteAllBytesAsync(stagePath, File.ReadAllBytes(FixturePath("mo_st01_d.lt2")));
        await File.WriteAllBytesAsync(skyPath, File.ReadAllBytes(FixturePath("mo_st01_sky_d.lt2")));
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Little);
        await workspace.ScanAsync();
        var host = new SceneHost();
        var collision = new CollisionEditorViewModel(host);
        using var gcx = new GcxEditorViewModel(host);
        using var map = new MapEditorViewModel(host, collision, gcx);

        await map.DiscoverLightsAsync(workspace, "mo_st01");

        Assert.True(map.HasLights);
        Assert.Equal("mo_st01_d.lt2", map.PrimaryLightDocument!.DisplayName);
        Assert.Equal(2, map.LightDocuments.Count);
        Assert.Equal(2, map.Outline[3].Children.Count);
        var stage = Assert.IsType<LightFileOutline>(map.Outline[3].Children[0]);
        Assert.Equal(82, stage.Children.Count);
        Assert.NotEmpty(host.GetLayerModels(SceneLayer.Lights));

        map.LightsVisible = false;
        Assert.All(host.GetLayerModels(SceneLayer.Lights), model => Assert.False(model.Visible));
        map.LightsVisible = true;
        Assert.Contains(host.GetLayerModels(SceneLayer.Lights), model => model.Visible);
    }

    [Fact]
    public async Task Map_editor_loads_real_n012a_lt3_without_detector_errors()
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath("n012a.lt3");
        await File.WriteAllBytesAsync(path, File.ReadAllBytes(FixturePath("n012a.lt3")));
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        await workspace.ScanAsync();
        var host = new SceneHost();
        var collision = new CollisionEditorViewModel(host);
        using var gcx = new GcxEditorViewModel(host);
        using var map = new MapEditorViewModel(host, collision, gcx);

        await map.DiscoverLightsAsync(workspace, "n012a");

        Assert.True(map.HasLights);
        Assert.Equal(209, map.PrimaryLightDocument!.Document.Groups.Count);
        Assert.Empty(map.ManipulationStatus);
        Assert.NotEmpty(host.GetLayerModels(SceneLayer.Lights));
    }

    [Fact]
    public async Task Game_lighting_spatially_shades_stage_models_and_restores_baked_colors()
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath("n012a.lt3");
        await File.WriteAllBytesAsync(path, File.ReadAllBytes(FixturePath("n012a.lt3")));
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        await workspace.ScanAsync();
        var host = new SceneHost();
        var collision = new CollisionEditorViewModel(host);
        using var gcx = new GcxEditorViewModel(host);
        using var map = new MapEditorViewModel(host, collision, gcx);
        var bakedColors = new[] { 1f, 1f, 1f, 0.6f };
        var stageModel = new Model3D
        {
            Positions = [0, 0, 0],
            Colors = (float[])bakedColors.Clone(),
            VertexCount = 1
        };
        LightVertexBaker.Register(stageModel, [0, 1, 0], bakedColors);
        host.ReplaceLayer(SceneLayer.VisualModels, [stageModel]);
        await map.DiscoverLightsAsync(workspace, "n012a");

        map.GameLightingEnabled = true;
        await map.LightingUpdateTask;

        Assert.False(bakedColors.SequenceEqual(stageModel.Colors));
        Assert.Equal(0.6f, stageModel.Colors[3]);

        map.GameLightingEnabled = false;

        Assert.Equal(bakedColors, stageModel.Colors);
    }

    [Fact]
    public async Task Selected_light_visual_includes_group_bounds_and_uses_local_marker_coordinates()
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath("sample.lt2");
        File.WriteAllBytes(path, File.ReadAllBytes(FixturePath("mo_st01_d.lt2")));
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Little);
        await workspace.ScanAsync();
        var session = LitDocumentSession.Load(workspace, WorkspacePath.Physical(path));
        var pointGroupIndex = session.Document.Groups.FindIndex(group => group.Type == 1);
        var entity = new LightEntity(session, pointGroupIndex, 0, "point");

        var models = LightSceneBuilder.BuildEntity(entity, includeGroupBounds: true);

        Assert.Contains(models, model => model.Name == "light group bounds");
        Assert.All(models, model => Assert.Equal(entity.GetPosition()!.Value, model.Position));
    }

    [Fact]
    public async Task Line_light_marker_uses_normalized_direction_instead_of_aiming_at_world_origin()
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath("n012a.lt3");
        await File.WriteAllBytesAsync(path, File.ReadAllBytes(FixturePath("n012a.lt3")));
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        await workspace.ScanAsync();
        var session = LitDocumentSession.Load(workspace, WorkspacePath.Physical(path));
        var groupIndex = session.Document.Groups.FindIndex(group => group.Type == 4);
        var entity = new LightEntity(session, groupIndex, 0, "line");

        var marker = Assert.Single(LightSceneBuilder.BuildEntity(entity, includeGroupBounds: false),
            model => model.Name == "line light");
        var bounds = marker.GetBoundingBox();

        Assert.True(bounds.Max.X < -50_000);
        Assert.True(bounds.Max.Z < -100_000);
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Lit", name);
}
