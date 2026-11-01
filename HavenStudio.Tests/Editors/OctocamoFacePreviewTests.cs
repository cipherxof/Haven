using HavenStudio.Editors;
using HavenStudio.Formats.Dar;
using HavenStudio.Rendering;
using OpenTK.Mathematics;
using Xunit.Abstractions;

namespace HavenStudio.Tests.Editors;

public sealed class OctocamoFacePreviewTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Opt_in_selected_face_uv_and_occluding_contact_audit()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_JJ_OCTOCAMO_STAGE");
        var slot = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_SLOT");
        if (string.IsNullOrWhiteSpace(stage) || string.IsNullOrWhiteSpace(slot)) return;
        var host = new SceneHost();
        var editor = new CollisionEditorViewModel(host);
        await editor.LoadFromFilePathAsync(Path.Combine(stage, "n023a.geom"));
        try
        {
            using var stream = File.OpenRead(Path.Combine(stage, "cache.dar"));
            var entries = DarFile.Read(stream).Entries;
            using var catalog = new OctocamoSurfaceCatalog(editor.GeomFile!,
                entries.Single(entry => entry.Filename.EndsWith(".octt")).Bytes!,
                entries.Single(entry => entry.Filename.EndsWith(".octl")).Bytes!);
            catalog.SetPatternPreviews(OctocamoPatternLibrary.LoadAll(slot));
            catalog.PreviewMusclePatterns = true;
            editor.SetOctocamoCatalog(catalog);
            host.SetLayerVisible(SceneLayer.Collision, true);
            var targets = editor.Blocks.SelectMany(block => block.Prims.Where(prim => prim.Prim.Offset + 0x26 == 0x296CC6)
                .Select(prim => (Block: block, Prim: prim))).ToArray();
            Assert.NotEmpty(targets);
            foreach (var target in targets)
                output.WriteLine($"Target block={target.Block.Index}, selector={target.Prim.Children[0].Poly!.Attribute:X4}, material0={catalog.GetTable(target.Block.Block)?.Materials.FirstOrDefault():X6}");
            editor.SetPolygonAttributeWithAliases(targets[0].Prim.Children[0], 0);
            editor.SetOctocamoCatalog(catalog);
            var tested = 0;
            foreach (var model in host.GetLayerModels(SceneLayer.Collision).Where(model => model.Visible))
            for (var triangle = 0; triangle < model.Indices.Length / 3; triangle++)
            {
                editor.TryResolveHit(new SelectionHit(model, triangle, 0), out var hit);
                if (hit.Prim == null || hit.Prim.Prim.Offset + 0x26 != 0x296CC6 || hit.GeoPrim != hit.Prim.Children[0]) continue;
                var surface = catalog.Resolve(hit.Block!.Block, hit.GeoPrim.Poly!.Attribute);
                var bounds = catalog.PatternAtlas!.Bounds(surface.PatternHash);
                for (var corner = 0; corner < 3; corner++)
                {
                    var vertex = (int)model.Indices[triangle * 3 + corner];
                    Assert.InRange(model.UVs[vertex * 2], bounds.U0 - 0.00001f, bounds.U1 + 0.00001f);
                    Assert.InRange(model.UVs[vertex * 2 + 1], bounds.V0 - 0.00001f, bounds.V1 + 0.00001f);
                }
                tested++;
                if (tested != 1) continue;
                Vector3 P(int corner)
                {
                    var i = (int)model.Indices[triangle * 3 + corner] * 3;
                    return new Vector3(model.Positions[i], model.Positions[i + 1], model.Positions[i + 2]);
                }
                var center = (P(0) + P(1) + P(2)) / 3;
                var normal = Vector3.Normalize(Vector3.Cross(P(1) - P(0), P(2) - P(0)));
                output.WriteLine($"Selected triangle at {center}, normal={normal}, pattern={surface.PatternHash:X6}");
                foreach (var other in host.GetLayerModels(SceneLayer.Collision).Where(item => item.Visible))
                for (var t = 0; t < other.Indices.Length / 3; t++)
                {
                    Vector3 Q(int corner)
                    {
                        var i = (int)other.Indices[t * 3 + corner] * 3;
                        return new Vector3(other.Positions[i], other.Positions[i + 1], other.Positions[i + 2]);
                    }
                    var a = Q(0); var b = Q(1); var c = Q(2);
                    if (Math.Abs(Vector3.Dot(center - a, normal)) > 1 ||
                        Math.Abs(Vector3.Dot(b - a, normal)) > 1 || Math.Abs(Vector3.Dot(c - a, normal)) > 1) continue;
                    var ab = b - a; var ac = c - a; var ap = center - a;
                    var d00 = Vector3.Dot(ab, ab); var d01 = Vector3.Dot(ab, ac); var d11 = Vector3.Dot(ac, ac);
                    var denominator = d00 * d11 - d01 * d01;
                    if (Math.Abs(denominator) < 0.001f) continue;
                    var u = (d11 * Vector3.Dot(ap, ab) - d01 * Vector3.Dot(ap, ac)) / denominator;
                    var v = (d00 * Vector3.Dot(ap, ac) - d01 * Vector3.Dot(ap, ab)) / denominator;
                    if (u < -0.0001f || v < -0.0001f || u + v > 1.0001f) continue;
                    editor.TryResolveHit(new SelectionHit(other, t, 0), out var overlapping);
                    var attribute = overlapping.GeoPrim?.Poly?.Attribute ?? 0;
                    var resolved = catalog.Resolve(overlapping.Block!.Block, attribute);
                    output.WriteLine($"Covering center: {other.Name}, face=0x{overlapping.Prim?.Prim.Offset:X}, attr={attribute:X4}, material={resolved.MaterialHash:X6}, pattern={resolved.PatternHash:X6}");
                }
            }
            Assert.True(tested > 0);
            var polygon = targets[0].Prim.Children[0];
            editor.SelectedGeoPrim = polygon;
            var preview = host.GetLayerModels(SceneLayer.Collision)
                .Single(model => model.Name == "SelectedOctocamoFacePreview");
            Assert.True(preview.Visible);
            Assert.True(preview.RenderAfterTransparent);
            Assert.False(preview.WriteDepth);
            Assert.Equal(-2f, preview.DepthBias);
            Assert.Equal(tested * 3, preview.IndexCount);
            Assert.True(editor.TryResolveHit(new SelectionHit(preview, 0, 0), out var previewHit));
            Assert.Same(polygon, previewHit.GeoPrim);
            var turfUvs = preview.UVs.ToArray();
            var neighbours = editor.Blocks.SelectMany(block => block.Prims)
                .Where(prim => prim.Prim.Offset + 0x26 != 0x296CC6)
                .SelectMany(prim => prim.Children).Where(child => child.Poly != null)
                .Select(child => child.Poly!.Attribute).ToArray();
            editor.SetPolygonAttributeWithAliases(polygon, 0x2080);
            Assert.NotEqual(turfUvs, preview.UVs);
            Assert.Equal(neighbours, editor.Blocks.SelectMany(block => block.Prims)
                .Where(prim => prim.Prim.Offset + 0x26 != 0x296CC6)
                .SelectMany(prim => prim.Children).Where(child => child.Poly != null)
                .Select(child => child.Poly!.Attribute).ToArray());
            catalog.PreviewMusclePatterns = false;
            editor.SetOctocamoCatalog(catalog);
            var cloth = preview.Colors.ToArray();
            editor.SetPolygonAttributeWithAliases(polygon, 0x0080);
            Assert.NotEqual(cloth, preview.Colors);
            host.SetLayerVisible(SceneLayer.Collision, false);
            Assert.False(preview.Visible);
            host.SetLayerVisible(SceneLayer.Collision, true);
            Assert.True(preview.Visible);
            editor.ClearSelection();
            Assert.False(preview.Visible);
            Assert.Empty(preview.Indices);
            editor.SelectedGeoPrim = polygon;
            Assert.True(preview.Visible);
            editor.SetOctocamoCatalog(null);
            Assert.False(preview.Visible);
            Assert.Empty(preview.Indices);
        }
        finally { editor.Clear(); }
    }
}
