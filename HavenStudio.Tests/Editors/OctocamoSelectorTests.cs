using HavenStudio.Editors;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;

namespace HavenStudio.Tests.Editors;

public sealed class OctocamoSelectorTests
{
    [Fact]
    public async Task Opt_in_real_stage_selected_face_retains_live_cloth_preview()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        var host = new SceneHost();
        var editor = new CollisionEditorViewModel(host);
        await editor.LoadFromFilePathAsync(Path.Combine(stage, "n023a.geom"));
        Assert.True(editor.HasGeomLoaded);
        try
        {
            using var archiveStream = File.OpenRead(Path.Combine(stage, "cache.dar"));
            var entries = DarFile.Read(archiveStream).Entries;
            var catalog = new OctocamoSurfaceCatalog(editor.GeomFile!,
                entries.Single(entry => entry.Filename.EndsWith(".octt")).Bytes!,
                entries.Single(entry => entry.Filename.EndsWith(".octl")).Bytes!);
            var originalIndexCount = host.GetLayerModels(SceneLayer.Collision).Sum(model => model.IndexCount);
            editor.SetOctocamoCatalog(catalog);
            Assert.True(host.GetLayerModels(SceneLayer.Collision).Sum(model => model.IndexCount) < originalIndexCount);
            var block = editor.Blocks.First(block => block.Prims.Any(primitive => primitive.Prim.Name == 0xEC34F0));
            var primitive = block.Prims.First(primitive => primitive.Prim.Name == 0xEC34F0);
            var polygon = primitive.Children[0];
            editor.SelectedGeoPrim = polygon;
            var model = host.GetLayerModels(SceneLayer.Collision).Single(model => model.Name == $"GeomBlock_{block.Index}");
            var before = model.Colors.ToArray();
            var selection = new OctocamoSelection(polygon, block.Block, catalog, () => { }, editor.SetPolygonAttributeWithAliases);
            selection.SelectedColour = selection.Colours.First(option => option.Slot != selection.SelectedColour?.Slot &&
                catalog.Resolve(block.Block, OctocamoSurfaceCatalog.WithColour(polygon.Poly!.Attribute, option.Slot)).IsMapped &&
                option.Preview.ToString() != selection.SelectedColour?.Preview.ToString());
            Assert.NotEqual(before, model.Colors);
            Assert.True(editor.IsDirty);
            var slot = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_SLOT");
            if (!string.IsNullOrWhiteSpace(slot))
            {
                catalog.SetPatternPreviews(OctocamoPatternLibrary.Load(slot, catalog.PatternHashes));
                catalog.PreviewMusclePatterns = true;
                var oldShading = host.ViewportControl.CurrentShadingMode;
                editor.SetOctocamoCatalog(catalog);
                Assert.Equal(Avalonia3DControl.Materials.ShadingMode.Texture, host.ViewportControl.CurrentShadingMode);
                Assert.Equal(model.VertexCount * 2, model.UVs.Length);
                var previousUvs = model.UVs.ToArray();
                selection.SelectedMaterial = selection.Materials.First(option =>
                    option.PatternHash != selection.SelectedMaterial?.PatternHash && option.PatternHash != 0 &&
                    catalog.Resolve(block.Block, OctocamoSurfaceCatalog.WithMaterial(polygon.Poly!.Attribute, option.Slot)).IsMapped);
                Assert.NotEqual(previousUvs, model.UVs);
                Assert.True(model.VerticesNeedUpdate);
                Assert.All(model.Colors, colour => Assert.Equal(1f, colour));
                editor.SetOctocamoCatalog(null);
                Assert.Equal(oldShading, host.ViewportControl.CurrentShadingMode);
            }
            editor.SetOctocamoCatalog(null);
            Assert.Equal(originalIndexCount, host.GetLayerModels(SceneLayer.Collision).Sum(model => model.IndexCount));
        }
        finally { editor.Clear(); }
    }

    [Fact]
    public void Octocamo_filter_keeps_only_player_contact_and_preserves_polygon_pick_indices()
    {
        var result = GeomSceneBuilder.FilterCollisionTriangles(
            new uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, [0, 1, 2], [4, 7, 9],
            [GeoCollisionAttributes.Bullet, GeoCollisionAttributes.Player | GeoCollisionAttributes.Lean,
                GeoCollisionAttributes.Player | GeoCollisionAttributes.Floor], GeoCollisionAttributes.Player);
        Assert.Equal(new uint[] { 3, 4, 5, 6, 7, 8 }, result.Indices);
        Assert.Equal(new[] { 1, 2 }, result.PrimitiveIndices);
        Assert.Equal(new[] { 7, 9 }, result.PolygonIndices);
    }

    [Fact]
    public void Opt_in_real_stage_alias_selector_save_does_not_get_overwritten()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        var original = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        var geometry = new GeomFile(new MemoryStream(original, writable: false), Endianness.Big);
        try
        {
            var aliases = geometry.BlockFaceData.Values.SelectMany(faces => faces)
                .Where(face => face.Poly is { Length: > 0 })
                .GroupBy(face => face.Offset)
                .FirstOrDefault(group => group.Distinct().Count() > 1);
            if (aliases == null) return;
            var face = aliases.First();
            face.Poly![0].Attribute ^= 0x800;
            using var output = new MemoryStream();
            geometry.SaveSurgicalEdits(original, output, Endianness.Big);
            Assert.Equal(face.Poly[0].Attribute,
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(
                    output.ToArray().AsSpan(face.Offset + 0x20 + 6, 2)));
        }
        finally { geometry.CloseStream(); }
    }

    [Fact]
    public void Opt_in_real_stage_surgical_save_persists_cabinet_selector()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        var original = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        var geometry = new GeomFile(new MemoryStream(original, writable: false), Endianness.Big);
        try
        {
            var face = geometry.BlockFaceData.Values.SelectMany(faces => faces)
                .First(face => face.Name == 0xEC34F0 && face.Poly is { Length: > 0 });
            var polygon = face.Poly![0];
            var previous = polygon.Attribute;
            polygon.Attribute = OctocamoSurfaceCatalog.WithColour(previous,
                ((previous >> 11) + 1) & 31);
            using var output = new MemoryStream();
            geometry.SaveSurgicalEdits(original, output, Endianness.Big);
            var position = face.Offset + 0x20 + 6;
            var saved = output.ToArray();
            Assert.Equal(polygon.Attribute,
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(saved.AsSpan(position, 2)));
            Assert.Equal(original[..position], saved[..position]);
            Assert.Equal(original[(position + 2)..], saved[(position + 2)..]);
            var crypto = new HavenStudio.CryptoService();
            Assert.Equal(saved, crypto.Decrypt(crypto.Encrypt(saved, "stage/n023a"), "stage/n023a"));
        }
        finally
        {
            geometry.CloseStream();
        }
    }

    [Fact]
    public void Opt_in_real_stage_dropdowns_refresh_preview_and_round_trip()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        var original = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        var geometry = new GeomFile(new MemoryStream(original, writable: false), Endianness.Big);
        try
        {
            using var archiveStream = File.OpenRead(Path.Combine(stage, "cache.dar"));
            var entries = DarFile.Read(archiveStream).Entries;
            var catalog = new OctocamoSurfaceCatalog(geometry,
                entries.Single(entry => entry.Filename.EndsWith(".octt")).Bytes!,
                entries.Single(entry => entry.Filename.EndsWith(".octl")).Bytes!);
            var (block, face) = geometry.BlockFaceData
                .Select(pair => (pair.Key, Face: pair.Value.FirstOrDefault(face =>
                    face.Name == 0xEC34F0 && face.Poly is { Length: > 0 })))
                .First(pair => pair.Face != null);
            Assert.NotNull(face);
            var changed = 0;
            var refreshed = 0;
            var prim = new CollisionPrimViewModel(face, 0, () => changed++, () => { });
            var selection = new OctocamoSelection(prim.Children[0], block, catalog, () => refreshed++);
            var positions = new float[9];
            var indices = new uint[] { 0, 1, 2 };
            float[] Preview() => GeomSceneBuilder.BuildOctocamoVertexColors(
                positions, indices, [0], [0], [face], block, catalog);
            var before = Preview();
            var oldColour = selection.SelectedColour;
            selection.SelectedColour = selection.Colours.First(option =>
                option.Slot != oldColour?.Slot &&
                catalog.Resolve(block, OctocamoSurfaceCatalog.WithColour(face.Poly![0].Attribute,
                    option.Slot)).IsMapped &&
                option.Preview.ToString() != oldColour?.Preview.ToString());
            Assert.NotEqual(before, Preview());
            var oldMaterial = selection.SelectedMaterial;
            selection.SelectedMaterial = selection.Materials.First(option =>
                option.Slot != oldMaterial?.Slot && option.PatternHash != 0);
            Assert.Equal(2, changed);
            Assert.Equal(2, refreshed);
            using var output = new MemoryStream();
            geometry.SaveSurgicalEdits(original, output, Endianness.Big);
            var saved = output.ToArray();
            var position = face.Offset + 0x20 + 6;
            Assert.Equal(face.Poly![0].Attribute,
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(saved.AsSpan(position, 2)));
            Assert.NotEqual(original[position..(position + 2)], saved[position..(position + 2)]);
        }
        finally
        {
            geometry.CloseStream();
        }
    }

    [Fact]
    public void Opt_in_stage_smoke_test_resolves_real_contact_materials()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        using var archiveStream = File.OpenRead(Path.Combine(stage, "cache.dar"));
        var entries = DarFile.Read(archiveStream).Entries;
        var table = entries.Single(entry =>
            entry.Filename.EndsWith(".octt", StringComparison.OrdinalIgnoreCase)).Bytes!;
        var registry = entries.Single(entry =>
            entry.Filename.EndsWith(".octl", StringComparison.OrdinalIgnoreCase)).Bytes!;
        using var geometryStream = File.OpenRead(Path.Combine(stage, "n023a.geom"));
        var geometry = new GeomFile(geometryStream, Endianness.Big);
        try
        {
            var catalog = new OctocamoSurfaceCatalog(geometry, table, registry);
            var contacts = geometry.GeomGroupBlocks.Values.SelectMany(blocks => blocks)
                .SelectMany(block => geometry.BlockFaceData.GetValueOrDefault(block, [])
                    .Where(face => (face.Attribute & GeoCollisionAttributes.Player) != 0)
                    .SelectMany(face => (face.Poly ?? []).Select(poly => catalog.Resolve(block, poly.Attribute))))
                .ToArray();
            Assert.NotEmpty(contacts);
            Assert.Contains(contacts, contact => contact.IsMapped && contact.PatternHash != 0);
        }
        finally
        {
            geometry.CloseStream();
        }
    }

    [Theory]
    [InlineData(0x6B80, 0, 0x6800)]
    [InlineData(0x6B80, 15, 0x6BC0)]
    [InlineData(0xFFFF, 0, 0xF83F)]
    public void Material_edit_preserves_colour_and_other_flags(ushort before, int slot, ushort expected)
    {
        var actual = OctocamoSurfaceCatalog.WithMaterial(before, slot);
        Assert.Equal(expected, actual);
        Assert.Equal(before & ~0x07C0, actual & ~0x07C0);
    }

    [Theory]
    [InlineData(0x4AC0, 2, 0x12C0)]
    [InlineData(0xFFFF, 0, 0x07FF)]
    [InlineData(0x0000, 31, 0xF800)]
    public void Colour_edit_preserves_material_and_other_flags(ushort before, int slot, ushort expected)
    {
        var actual = OctocamoSurfaceCatalog.WithColour(before, slot);
        Assert.Equal(expected, actual);
        Assert.Equal(before & ~0xF800, actual & ~0xF800);
    }
}
