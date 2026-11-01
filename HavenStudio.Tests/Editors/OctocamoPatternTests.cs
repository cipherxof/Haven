using System.Buffers.Binary;
using HavenStudio.Editors;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;

namespace HavenStudio.Tests.Editors;

public sealed class OctocamoPatternTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(2048)]
    public void Truncated_slot_is_rejected(int length)
    {
        var bytes = new byte[length];
        if (length >= 10) BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(8, 2), 1);
        Assert.Throws<InvalidDataException>(() => OctocamoPatternLibrary.Load(bytes, [0xF563A9u]));
    }

    [Fact]
    public void Atlas_preserves_rgb_and_separates_missing_patterns()
    {
        var atlas = new OctocamoPatternAtlas(new Dictionary<uint, OctocamoPatternPreview>
        {
            [123] = new(1, 1, [12, 34, 56, 0])
        });
        var bounds = atlas.Bounds(123);
        var missing = atlas.Bounds(999);
        Assert.NotEqual(bounds, missing);
        Assert.True(bounds.U0 > missing.U1);
        var pixel = ((int)(bounds.V0 * atlas.Height) * atlas.Width + (int)(bounds.U0 * atlas.Width)) * 4;
        Assert.Equal(new byte[] { 12, 34, 56, 255 }, atlas.Rgba[pixel..(pixel + 4)]);
        Assert.Equal(new byte[] { 242, 31, 173, 255 }, atlas.Rgba[..4]);
        Assert.InRange(bounds.U1, 0, 1);
        Assert.InRange(bounds.V1, 0, 1);
    }

    [Fact]
    public void Atlas_rejects_inconsistent_rgba_buffers()
    {
        Assert.Throws<ArgumentException>(() => new OctocamoPatternAtlas(
            new Dictionary<uint, OctocamoPatternPreview> { [123] = new(2, 2, [1, 2, 3, 4]) }));
    }

    [Fact]
    public void Opt_in_retail_slot_decodes_plastic_diffuse_without_recolouring_it()
    {
        var slot = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_SLOT");
        if (string.IsNullOrWhiteSpace(slot)) return;
        var patterns = OctocamoPatternLibrary.Load(slot, [0xF563A9u]);
        var image = Assert.Single(patterns).Value;
        Assert.Equal(256, image.Width);
        Assert.Equal(256, image.Height);
        Assert.Equal(256 * 256 * 4, image.Rgba.Length);
        var pixels = Enumerable.Range(0, image.Width * image.Height).Select(i =>
            (R: image.Rgba[i * 4], G: image.Rgba[i * 4 + 1], B: image.Rgba[i * 4 + 2])).ToArray();
        Assert.Contains(pixels, pixel => pixel.R > pixel.G + 15 && pixel.R > pixel.B + 15);
        Assert.Contains(pixels, pixel => pixel.R > 150 && pixel.G > 150 && pixel.B > 150);
    }

    [Fact]
    public void Opt_in_stage_patterns_decode_and_material_edits_update_uvs_without_changing_geometry()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_STAGE");
        var slot = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_SLOT");
        if (string.IsNullOrWhiteSpace(stage) || string.IsNullOrWhiteSpace(slot)) return;
        using var archive = File.OpenRead(Path.Combine(stage, "cache.dar"));
        var entries = DarFile.Read(archive).Entries;
        var original = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        var geometry = new GeomFile(new MemoryStream(original, false), Endianness.Big);
        try
        {
            using var catalog = new OctocamoSurfaceCatalog(geometry,
                entries.Single(entry => entry.Filename.EndsWith(".octt")).Bytes!,
                entries.Single(entry => entry.Filename.EndsWith(".octl")).Bytes!);
            var patterns = OctocamoPatternLibrary.Load(slot, catalog.PatternHashes);
            // PLAS_A may have been remapped by the user; its currently assigned
            // pattern, not the original retail diffuse, must be decoded here.
            Assert.Contains(catalog.GetPatternHash(0xE1D8C5), patterns.Keys);
            Assert.True(patterns.Count > 1);
            catalog.SetPatternPreviews(patterns);
            var (block, face) = geometry.BlockFaceData.Select(pair =>
                (pair.Key, Face: pair.Value.FirstOrDefault(face => face.Name == 0xEC34F0 && face.Poly is { Length: > 0 })))
                .First(pair => pair.Face != null);
            var polygon = face!.Poly![0];
            var beforeAttribute = polygon.Attribute;
            var beforePattern = catalog.Resolve(block, beforeAttribute).PatternHash;
            float[] positions = [0, 0, 0, 10, 0, 0, 0, 10, 0];
            uint[] indices = [0, 1, 2];
            float[] Uvs() => GeomSceneBuilder.BuildOctocamoTextureUvs(positions, indices, [0], [0], [face], block, catalog);
            var before = Uvs();
            Assert.All(before, value => Assert.True(float.IsFinite(value) && value >= 0 && value <= 1));
            var nextSlot = Enumerable.Range(0, 32).First(slotIndex =>
            {
                var surface = catalog.Resolve(block, OctocamoSurfaceCatalog.WithMaterial(beforeAttribute, slotIndex));
                return surface.PatternHash != beforePattern && patterns.ContainsKey(surface.PatternHash);
            });
            polygon.Attribute = OctocamoSurfaceCatalog.WithMaterial(beforeAttribute, nextSlot);
            Assert.NotEqual(before, Uvs());
            Assert.Equal(new float[] { 0, 0, 0, 10, 0, 0, 0, 10, 0 }, positions);
            Assert.Equal(new uint[] { 0, 1, 2 }, indices);
            polygon.Attribute = beforeAttribute;
            using var output = new MemoryStream();
            geometry.SaveSurgicalEdits(original, output, Endianness.Big);
            Assert.Equal(original, output.ToArray());
        }
        finally { geometry.CloseStream(); }
    }
}
