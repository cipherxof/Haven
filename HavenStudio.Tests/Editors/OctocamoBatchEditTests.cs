using System.Buffers.Binary;
using Avalonia;
using HavenStudio.Editors;
using HavenStudio.Extensions;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;
using OpenTK.Mathematics;
using Xunit.Abstractions;

namespace HavenStudio.Tests.Editors;

public sealed class OctocamoBatchEditTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Batch_uses_identities_at_different_local_slots_and_preserves_other_channel_and_flags(bool material, bool colour)
    {
        using var fixture = new Fixture();
        var a = fixture.Add(0x100, [100, 200], [500, 600], 0x35);
        var b = fixture.Add(0x200, [200, 100], [600, 500], 0x875);
        using var catalog = fixture.Catalog();
        IReadOnlyList<OctocamoPolygonEdit>? committed = null;
        bool? muscle = null;
        var vm = new OctocamoBatchEditViewModel([Candidate(a, catalog), Candidate(b, catalog)], [a, b], catalog,
            (edits, preview) => { committed = edits; muscle = preview; });
        Assert.Contains("0x000064", vm.CurrentMaterial); // same identity, different local slots
        Assert.False(vm.CanApply);
        Assert.False(vm.Apply());
        if (material) vm.SelectedMaterial = vm.Materials.Single(option => option.Hash == 200);
        if (colour) vm.SelectedColour = vm.Colours.Single(option => option.Hash == 600);
        Assert.True(vm.Apply());
        Assert.Equal(material, muscle);
        Assert.NotNull(committed);
        Assert.Equal(2, committed.Count);
        foreach (var edit in committed)
        {
            var local = catalog.GetTable(fixture.Blocks[edit.Polygon])!;
            var expected = edit.Before;
            if (material) expected = OctocamoSurfaceCatalog.WithMaterial(expected, local.Materials.IndexOf(200));
            if (colour) expected = OctocamoSurfaceCatalog.WithColour(expected, local.Colors.IndexOf(600));
            Assert.Equal(expected, edit.After);
            Assert.Equal(edit.Before & 63, edit.After & 63);
            Assert.Equal(edit.Before, edit.Polygon.Poly!.Attribute); // plan/commit callback owns mutation
        }
    }

    [Fact]
    public void Shared_aliases_are_deduplicated_and_conflicting_local_slots_excluded()
    {
        using var fixture = new Fixture();
        var a = fixture.Add(0x100, [100, 200], [500, 600], 0x35);
        var alias = fixture.Add(0x100, [100, 200], [500, 600], 0x35);
        using var catalog = fixture.Catalog();
        var vm = new OctocamoBatchEditViewModel([Candidate(a, catalog), Candidate(alias, catalog)], [a, alias], catalog, (_, _) => { });
        vm.SelectedMaterial = vm.Materials.Single(option => option.Hash == 200);
        Assert.Single(vm.BuildPlan());
        fixture.Geometry.BlockMaterialData[fixture.Blocks[alias]].Materials = [200, 100];
        var incompatible = new OctocamoBatchEditViewModel([Candidate(a, catalog)], [a, alias], catalog, (_, _) => Assert.Fail("Must not commit"));
        Assert.Empty(incompatible.Materials);
        incompatible.SelectedMaterial = new(200, 0, "unsupported");
        Assert.False(incompatible.Apply());
        Assert.Equal((ushort)0x35, a.Poly!.Attribute);
        Assert.Equal((ushort)0x35, alias.Poly!.Attribute);
    }

    [Fact]
    public void Alias_disagreement_missing_faces_and_empty_selection_are_rejected()
    {
        using var fixture = new Fixture();
        var a = fixture.Add(0x100, [100, 200], [500, 600], 0);
        var alias = fixture.Add(0x100, [100, 200], [500, 600], 64);
        using var catalog = fixture.Catalog();
        var vm = new OctocamoBatchEditViewModel([Candidate(a, catalog)], [a, alias], catalog, (_, _) => Assert.Fail("Must not commit"));
        vm.SelectedColour = vm.Colours.Single(option => option.Hash == 600);
        Assert.False(vm.Apply());
        Assert.Contains("disagree", vm.Status);
        Assert.Throws<InvalidOperationException>(() => new OctocamoBatchEditViewModel([], [], catalog, (_, _) => { }));
        Assert.Throws<InvalidOperationException>(() => new OctocamoBatchEditViewModel([Candidate(a, catalog)], [], catalog, (_, _) => { }));
    }

    [Fact]
    public async Task Opt_in_filtered_real_JJ_batch_is_undoable_surgical_and_saved_without_touching_source()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        using var temp = new TempDirectory();
        var path = temp.GetPath("n023a.geom");
        var original = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        var originalDar = File.ReadAllBytes(Path.Combine(stage, "cache.dar"));
        var baselineGeometry = new GeomFile(new MemoryStream(original, false), Endianness.Big);
        GeoStructureValidationResult baseline;
        try { baseline = GeoStructureValidator.Validate(baselineGeometry); }
        finally { baselineGeometry.CloseStream(); }
        File.WriteAllBytes(path, original);
        File.WriteAllBytes(temp.GetPath("cache.dar"), originalDar);
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        await workspace.ScanAsync();
        var host = new SceneHost();
        var editor = new CollisionEditorViewModel(host);
        using var gcx = new GcxEditorViewModel(host);
        using var map = new MapEditorViewModel(host, editor, gcx);
        editor.SetWorkspace(workspace);
        await editor.LoadFromWorkspacePathAsync(WorkspacePath.Physical(path));
        try
        {
            await map.DiscoverOctocamoAsync(workspace);
            map.OctocamoViewEnabled = true;
            var center = new Vector3(-49793.31f, 4000, -244209.67f);
            var view = Matrix4.LookAt(center + new Vector3(0, 10000, 0), center, -Vector3.UnitZ);
            map.SelectOctocamoFacesInBox(new Rect(495, 495, 10, 10), view,
                Matrix4.CreateOrthographic(10000, 10000, 1, 50000), 1000, 1000);
            map.OctocamoFaceFilter = "15BCCD"; // SOIL_B; tests do not initialize the UI name dictionary
            Assert.True(map.FilteredOctocamoBoxFaces.Count > 1, string.Join("\n", map.OctocamoBoxFaces.Select(face => face.MaterialText)));
            map.SelectAllFilteredOctocamoFaces();
            Assert.Equal(map.FilteredOctocamoBoxFaces.ToHashSet(), map.OctocamoBoxFaces.Where(face => face.IsBatchSelected).ToHashSet());
            Assert.True(map.CanEditOctocamoBatch);
            map.OctocamoFaceFilter = "296cc6";
            Assert.False(map.CanEditOctocamoBatch); // a changed search cannot silently retain hidden checked items
            map.OctocamoFaceFilter = "15BCCD";
            map.SelectAllFilteredOctocamoFaces();
            var checkedFaces = map.OctocamoBoxFaces.Where(face => face.IsBatchSelected).ToArray();
            var before = editor.Blocks.SelectMany(block => block.Prims).SelectMany(prim => prim.Children)
                .Where(poly => poly.Poly != null).ToDictionary(poly => poly, poly => poly.Poly!.Attribute);
            var cloth = map.CreateOctocamoBatchEditor();
            cloth.SelectedColour = cloth.Colours.First(option => !cloth.CurrentColour.Contains($"0x{option.Hash:X6}"));
            var previousColours = host.GetLayerModels(SceneLayer.Collision).ToDictionary(model => model, model => model.Colors.ToArray());
            Assert.True(cloth.Apply(), cloth.Status);
            Assert.False(map.OctocamoMusclePatternView);
            Assert.Contains(previousColours, pair => !pair.Value.SequenceEqual(pair.Key.Colors));
            map.Undo();
            Assert.All(before, pair => Assert.Equal(pair.Value, pair.Key.Poly!.Attribute));
            var slot = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_SLOT");
            if (!string.IsNullOrWhiteSpace(slot)) await map.LoadOctocamoPatternSlotAsync(slot);
            var previousUvs = host.GetLayerModels(SceneLayer.Collision).ToDictionary(model => model, model => model.UVs.ToArray());
            var batch = map.CreateOctocamoBatchEditor();
            batch.SelectedMaterial = batch.Materials.Single(option => option.Hash == 0x7A24CE); // TURF_A
            batch.SelectedColour = batch.Colours.First(option => option.Hash != 0x22B939);
            var edits = batch.BuildPlan().ToDictionary(edit => edit.Offset);
            Assert.True(edits.Count > 1);
            Assert.True(batch.Apply(), batch.Status);
            if (!string.IsNullOrWhiteSpace(slot))
            {
                Assert.True(map.OctocamoMusclePatternView);
                Assert.Contains(previousUvs, pair => !pair.Value.SequenceEqual(pair.Key.UVs));
            }
            Assert.True(map.CanUndo);
            Assert.True(editor.IsDirty);
            foreach (var (poly, value) in before)
            {
                var offset = Offset(poly);
                Assert.Equal(edits.TryGetValue(offset, out var edit) ? edit.After : value, poly.Poly!.Attribute);
            }
            Assert.Equal(checkedFaces, map.OctocamoBoxFaces.Where(face => face.IsBatchSelected));
            map.Undo();
            Assert.All(before, pair => Assert.Equal(pair.Value, pair.Key.Poly!.Attribute));
            Assert.False(map.CanUndo);
            map.Redo();
            Assert.All(edits.Values, edit => Assert.Equal(edit.After, edit.Polygon.Poly!.Attribute));
            await map.SaveAsync();
            var saved = File.ReadAllBytes(path);
            Assert.False(editor.IsDirty);
            Assert.Equal(original.Length, saved.Length);
            var changedBytes = edits.Keys.SelectMany(offset => new[] { offset, offset + 1 }).ToHashSet();
            for (var i = 0; i < original.Length; i++)
                if (!changedBytes.Contains(i) && original[i] != saved[i]) Assert.Fail($"Unexpected change at 0x{i:X}");
            Assert.All(edits.Values, edit => Assert.Equal(edit.After, BinaryPrimitives.ReadUInt16BigEndian(saved.AsSpan(edit.Offset, 2))));
            Assert.Equal(original, File.ReadAllBytes(editor.LastSaveBackupPath!));
            Assert.Equal(originalDar, File.ReadAllBytes(temp.GetPath("cache.dar")));
            var reopened = new GeomFile(new MemoryStream(saved, false), Endianness.Big);
            try
            {
                var validated = GeoStructureValidator.Validate(reopened);
                Assert.Equal(baseline.Summary, validated.Summary);
                Assert.Equal(baseline.Issues, validated.Issues); // no new structural issues in the user's existing stage
                foreach (var prim in reopened.BlockFaceData.Values.SelectMany(prims => prims))
                    if (prim.Poly != null)
                        for (var i = 0; i < prim.Poly.Length; i++)
                            if (edits.TryGetValue(prim.Offset + 0x26 + i * 8, out var edit)) Assert.Equal(edit.After, prim.Poly[i].Attribute);
            }
            finally { reopened.CloseStream(); }
            var crypto = new HavenStudio.CryptoService();
            Assert.Equal(saved, crypto.Decrypt(crypto.Encrypt(saved, "stage/n023a"), "stage/n023a"));
            Assert.Equal(original, File.ReadAllBytes(Path.Combine(stage, "n023a.geom")));
            Assert.Equal(originalDar, File.ReadAllBytes(Path.Combine(stage, "cache.dar")));
            // Intervening individual edits must not be clobbered by batch undo.
            var first = edits.Values.First();
            editor.SetPolygonAttributeWithAliases(first.Polygon, (ushort)(first.After ^ 0x800));
            var current = before.Keys.ToDictionary(poly => poly, poly => poly.Poly!.Attribute);
            map.Undo();
            Assert.All(current, pair => Assert.Equal(pair.Value, pair.Key.Poly!.Attribute));
            editor.Clear();
            Assert.False(batch.Apply());
            Assert.Contains("GEOM has changed", batch.Status);
            output.WriteLine($"Filtered batch changed {edits.Count} selectors; undo/redo, alias read-back, exact byte boundaries, unchanged DAR, and stage-key round trip passed.");
        }
        finally { editor.Clear(); }
    }

    private static int Offset(CollisionGeoPrimViewModel poly) => poly.ParentPrim.Prim.Offset + 0x26 + poly.ParentPrim.Children.IndexOf(poly) * 8;
    private static OctocamoFaceCandidate Candidate(CollisionGeoPrimViewModel poly, OctocamoSurfaceCatalog catalog) =>
        new(poly.ParentPrim, poly, default, null, "Fixture", catalog);

    private sealed class Fixture : IDisposable
    {
        public GeomFile Geometry { get; }
        public Dictionary<CollisionGeoPrimViewModel, GeoBlock> Blocks { get; } = [];
        public Fixture()
        {
            var bytes = new byte[0x90];
            W(bytes, 0, 1); W(bytes, 4, (uint)bytes.Length); W(bytes, 0x74, 1);
            Geometry = new GeomFile(new MemoryStream(bytes, false), Endianness.Big);
        }
        public CollisionGeoPrimViewModel Add(int offset, uint[] materials, uint[] colours, ushort attribute)
        {
            var block = new GeoBlock();
            Geometry.GeomGroupBlocks[Geometry.GeomGroups.Single()].Add(block);
            Geometry.BlockMaterialData[block] = new GeoMaterialHeader(0, 0, 0, 0, []) { Materials = materials.ToList(), Colors = colours.ToList() };
            var prim = new CollisionPrimViewModel(new Geom { Offset = offset, Flag = 2, Poly = [new GeoPrimPoly(new byte[6], attribute)] }, 0, () => { }, () => { });
            typeof(CollisionPrimViewModel).GetProperty("ParentBlock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(prim, new CollisionBlockViewModel(block, 0, [prim], () => { }, () => { }, _ => { }));
            Blocks.Add(prim.Children[0], block);
            return prim.Children[0];
        }
        public OctocamoSurfaceCatalog Catalog()
        {
            var octt = new byte[0x810];
            W(octt, 0, 3); W(octt, 4, 2); W(octt, 8, 2);
            W(octt, 16, 500); W(octt, 64, 600);
            W(octt, 0x610, 100); W(octt, 0x614, 1000);
            W(octt, 0x620, 200); W(octt, 0x624, 2000);
            return new OctocamoSurfaceCatalog(Geometry, octt);
        }
        public void Dispose() => Geometry.CloseStream();
        private static void W(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset, 4), value);
    }
}
