using Avalonia;
using Avalonia3DControl.Materials;
using HavenStudio.Editors;
using HavenStudio.Extensions;
using HavenStudio.Rendering;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;
using OpenTK.Mathematics;
using Xunit.Abstractions;

namespace HavenStudio.Tests.Editors;

public sealed class OctocamoBoxSelectionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Opt_in_real_JJ_box_lists_overlaps_and_selects_exact_selector_without_editing()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        using var temp = new TempDirectory();
        var path = temp.GetPath("n023a.geom");
        var original = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        File.WriteAllBytes(path, original);
        File.Copy(Path.Combine(stage, "cache.dar"), temp.GetPath("cache.dar"));
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        await workspace.ScanAsync();
        var host = new SceneHost();
        var editor = new CollisionEditorViewModel(host);
        using var gcx = new GcxEditorViewModel(host);
        using var map = new MapEditorViewModel(host, editor, gcx);
        editor.SetWorkspace(workspace);
        map.SdmAreaVisible = true;
        await editor.LoadFromWorkspacePathAsync(WorkspacePath.Physical(path));
        Assert.False(map.SdmAreaVisible);
        try
        {
            await map.DiscoverOctocamoAsync(workspace);
            map.OctocamoViewEnabled = true;
            var center = new Vector3(-49793.31f, 4000, -244209.67f);
            var view = Matrix4.LookAt(center + new Vector3(0, 10000, 0), center, -Vector3.UnitZ);
            var projection = Matrix4.CreateOrthographic(10000, 10000, 1, 50000);
            map.SelectOctocamoFacesInBox(new Rect(495, 495, 10, 10), view, projection, 1000, 1000);
            var target = Assert.Single(map.OctocamoBoxFaces, face => face.SelectorOffset == 0x296CC6);
            Assert.Contains(map.OctocamoBoxFaces, face => face.SelectorOffset == 0x29D746);
            Assert.True(map.OctocamoBoxFaces.Count >= 16);
            Assert.Equal(map.OctocamoBoxFaces.Count, map.OctocamoBoxFaces.Select(face => (face.Instance, face.SelectorOffset)).Distinct().Count());
            Assert.Null(map.SelectedOctocamoFace);
            Assert.False(editor.IsDirty);
            target.IsBatchSelected = true;
            var outline = Assert.Single(host.GetLayerModels(SceneLayer.OctocamoSelection));
            Assert.True(outline.RenderOnTop);
            Assert.Equal(RenderMode.Line, outline.RenderModeOverride);
            Assert.True(outline.IndexCount >= 3);
            Assert.Equal(outline.Indices.Length, outline.IndexCount);
            Assert.False(editor.IsDirty);
            target.IsBatchSelected = false;
            Assert.Empty(host.GetLayerModels(SceneLayer.OctocamoSelection));
            map.OctocamoFaceFilter = "296cc6";
            Assert.Same(target, Assert.Single(map.FilteredOctocamoBoxFaces));
            map.SelectAllFilteredOctocamoFaces();
            Assert.True(target.IsBatchSelected);
            Assert.Single(host.GetLayerModels(SceneLayer.OctocamoSelection));
            map.OctocamoFaceFilter = string.Empty;
            Assert.Empty(host.GetLayerModels(SceneLayer.OctocamoSelection));
            map.SelectedOctocamoFace = target;
            var selected = Assert.IsType<PrimEntity>(map.SelectedEntity);
            Assert.Same(target.Polygon, selected.GeoPrim);
            Assert.Contains("0x296CC6", selected.Octocamo!.ScopeText);
            var focus = Assert.Single(host.GetLayerModels(SceneLayer.OctocamoFocus));
            Assert.True(focus.RenderOnTop);
            Assert.True(focus.RenderOnTopOrder > outline.RenderOnTopOrder);
            Assert.Equal(RenderMode.Line, focus.RenderModeOverride);
            Assert.True(focus.IndexCount >= 3);
            Assert.NotEqual(host.GetLayerModels(SceneLayer.OctocamoSelection).FirstOrDefault(), focus);
            target.IsBatchSelected = true;
            Assert.Single(host.GetLayerModels(SceneLayer.OctocamoSelection));
            Assert.Same(focus, Assert.Single(host.GetLayerModels(SceneLayer.OctocamoFocus)));
            target.IsBatchSelected = false;
            Assert.Empty(host.GetLayerModels(SceneLayer.OctocamoSelection));
            Assert.Same(focus, Assert.Single(host.GetLayerModels(SceneLayer.OctocamoFocus)));
            map.SelectedOctocamoFace = null; // transient ListBox refresh
            Assert.Same(target, map.SelectedOctocamoFace);
            Assert.False(editor.IsDirty);
            Assert.Contains("block", target.DetailsText);
            editor.ClearSelection();
            Assert.Null(map.SelectedOctocamoFace);
            Assert.Empty(host.GetLayerModels(SceneLayer.OctocamoFocus));
            Assert.NotEmpty(map.OctocamoBoxFaces);
            map.SelectedOctocamoFace = target;
            var other = map.OctocamoBoxFaces.First(face => face.SelectorOffset == 0x29D746);
            map.SelectedOctocamoFace = other;
            Assert.Same(other.Polygon, Assert.IsType<PrimEntity>(map.SelectedEntity).GeoPrim);
            Assert.Same(focus, Assert.Single(host.GetLayerModels(SceneLayer.OctocamoFocus)));
            editor.SelectedGeoPrim = target.Polygon; // Same selection event as a direct face click.
            Assert.Same(focus, Assert.Single(host.GetLayerModels(SceneLayer.OctocamoFocus)));
            map.ClearOctocamoFaceBox();
            Assert.Same(focus, Assert.Single(host.GetLayerModels(SceneLayer.OctocamoFocus)));
            editor.ClearSelection();
            Assert.Empty(host.GetLayerModels(SceneLayer.OctocamoFocus));
            Assert.False(editor.IsDirty);
            Assert.Equal(original, File.ReadAllBytes(path));
            output.WriteLine($"Found {map.OctocamoBoxFaces.Count} independent polygons in a 10px box; exact target and covering SOIL_B polygon are both selectable without mutation.");
            host.SetLayerVisible(SceneLayer.Collision, false);
            map.SelectOctocamoFacesInBox(new Rect(495, 495, 10, 10), view, projection, 1000, 1000);
            Assert.Empty(map.OctocamoBoxFaces);
            host.SetLayerVisible(SceneLayer.Collision, true);
            map.SelectOctocamoFacesInBox(new Rect(495, 495, 10, 10), view, projection, 1000, 1000);
            Assert.NotEmpty(map.OctocamoBoxFaces);
            map.OctocamoViewEnabled = false;
            Assert.Empty(map.OctocamoBoxFaces);
            Assert.Empty(host.GetLayerModels(SceneLayer.OctocamoSelection));
            Assert.Empty(host.GetLayerModels(SceneLayer.OctocamoFocus));
            Assert.Null(map.SelectedOctocamoFace);
        }
        finally { editor.Clear(); }
    }
}
