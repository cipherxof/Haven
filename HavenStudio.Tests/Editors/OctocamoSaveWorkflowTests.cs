using System.Buffers.Binary;
using HavenStudio.Editors;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;
using Xunit.Abstractions;

namespace HavenStudio.Tests.Editors;

public sealed class OctocamoSaveWorkflowTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Opt_in_map_inspector_changes_preview_saves_and_reopens_exact_face()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        using var temp = new TempDirectory();
        var path = temp.GetPath("n023a.geom");
        var before = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        File.WriteAllBytes(path, before);
        File.Copy(Path.Combine(stage, "cache.dar"), temp.GetPath("cache.dar"));
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
            var block = editor.Blocks.First(block => block.Prims.Any(prim =>
                prim.Prim.Offset + 0x26 == 0x296CC6 && prim.Children.Count > 0));
            var primitive = block.Prims.First(prim => prim.Prim.Offset + 0x26 == 0x296CC6);
            editor.SelectedPrim = primitive;
            editor.SelectedGeoPrim = primitive.Children[0];
            var selection = Assert.IsType<PrimEntity>(map.SelectedEntity).Octocamo!;
            Assert.True(selection.CanEdit);
            var model = host.GetLayerModels(SceneLayer.Collision).Single(model => model.Name == $"GeomBlock_{block.Index}");
            var colours = model.Colors.ToArray();
            selection.SelectedColour = selection.Colours.First(option => option.Slot != selection.SelectedColour!.Slot &&
                option.Preview.ToString() != selection.SelectedColour.Preview.ToString());
            Assert.NotEqual(colours, model.Colors);
            Assert.False(map.OctocamoMusclePatternView);
            selection.SelectedMaterial = selection.Materials.First(option =>
                option.Slot != selection.SelectedMaterial!.Slot && option.PatternHash != 0);
            var expected = primitive.Children[0].Poly!.Attribute;
            Assert.True(editor.IsDirty);
            await map.SaveAsync();
            var saved = File.ReadAllBytes(path);
            Assert.NotEqual(before, saved);
            Assert.Equal(expected, BinaryPrimitives.ReadUInt16BigEndian(saved.AsSpan(0x296CC6, 2)));
            Assert.Equal(before[..0x296CC6], saved[..0x296CC6]);
            Assert.Equal(before[0x296CC8..], saved[0x296CC8..]);
            Assert.False(editor.IsDirty);
            Assert.Contains(path, map.LastOctocamoSaveReport);
            Assert.Equal(before, File.ReadAllBytes(editor.LastSaveBackupPath!));
            using var parsed = new MemoryStream(saved, false);
            var reopened = new GeomFile(parsed, Endianness.Big);
            try
            {
                Assert.All(reopened.BlockFaceData.Values.SelectMany(faces => faces).Where(face => face.Offset + 0x26 == 0x296CC6),
                    face => Assert.Equal(expected, face.Poly![0].Attribute));
            }
            finally { reopened.CloseStream(); }
            var crypto = new HavenStudio.CryptoService();
            Assert.Equal(saved, crypto.Decrypt(crypto.Encrypt(saved, "stage/n023a"), "stage/n023a"));
            output.WriteLine($"Inspector face 0x296CC6: saved 0x{expected:X4}, read-back and reparse matched. Exact rollback backup retained.");
        }
        finally { editor.Clear(); }
    }

    [Fact]
    public async Task Opt_in_geom_save_refuses_external_changes_without_overwrite()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        using var temp = new TempDirectory();
        var path = temp.GetPath("n023a.geom");
        var bytes = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        File.WriteAllBytes(path, bytes);
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        await workspace.ScanAsync();
        var session = new GeomDocumentSession();
        session.SetWorkspace(workspace);
        await session.LoadAsync(WorkspacePath.Physical(path));
        try
        {
            var face = session.Document!.BlockFaceData.Values.SelectMany(faces => faces).First(face => face.Poly is { Length: > 0 });
            face.Poly![0].Attribute ^= 0x800;
            session.MarkDirty();
            bytes[^1] ^= 1;
            File.WriteAllBytes(path, bytes);
            await Assert.ThrowsAsync<IOException>(() => session.SaveAsync());
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.True(session.IsDirty);
            Assert.Single(Directory.GetFiles(temp.Path));
        }
        finally { session.Unload(); }
    }

    [Fact]
    public void Opt_in_selector_requests_the_edited_preview_channel()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        if (string.IsNullOrWhiteSpace(stage)) return;
        using var darStream = File.OpenRead(Path.Combine(stage, "cache.dar"));
        var entries = DarFile.Read(darStream).Entries;
        var geometry = new GeomFile(new MemoryStream(File.ReadAllBytes(Path.Combine(stage, "n023a.geom")), false), Endianness.Big);
        try
        {
            using var catalog = new OctocamoSurfaceCatalog(geometry,
                entries.Single(entry => entry.Filename.EndsWith(".octt")).Bytes!,
                entries.Single(entry => entry.Filename.EndsWith(".octl")).Bytes!);
            var pair = geometry.BlockFaceData.First(pair => pair.Value.Any(face => face.Offset + 0x26 == 0x296CC6));
            var face = pair.Value.First(face => face.Offset + 0x26 == 0x296CC6);
            var primitive = new CollisionPrimViewModel(face, 0, () => { }, () => { });
            var channels = new List<bool>();
            var selection = new OctocamoSelection(primitive.Children[0], pair.Key, catalog, () => { },
                showMusclePreview: muscle => channels.Add(muscle));
            selection.SelectedColour = selection.Colours.First(option => option.Slot != selection.SelectedColour!.Slot);
            selection.SelectedMaterial = selection.Materials.First(option => option.Slot != selection.SelectedMaterial!.Slot);
            Assert.Equal(new[] { false, true }, channels);
        }
        finally { geometry.CloseStream(); }
    }
}
