using System.Buffers.Binary;
using HavenStudio.Editors;
using HavenStudio.Extensions;
using HavenStudio.Formats.Geo;

namespace HavenStudio.Tests.Editors;

public sealed class OctocamoBatchEditTests
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
