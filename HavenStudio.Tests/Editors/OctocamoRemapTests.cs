using System.Buffers.Binary;
using HavenStudio.Editors;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Geo;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;
using Xunit.Abstractions;

namespace HavenStudio.Tests.Editors;

public sealed class OctocamoRemapTests(ITestOutputHelper output)
{
    [Fact]
    public void Done_applies_selected_proposal_and_refreshes_current_mapping()
    {
        var (geometry, _, catalog) = Fixture([1, 2], [1]);
        try
        {
            var refreshes = 0;
            var model = new OctocamoRemapViewModel(catalog, 2, () => refreshes++, _ => null);
            model.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(model.Materials))
                {
                    // Simulate ListBox clearing its selection while ItemsSource changes.
                    model.SelectedMaterial = null;
                    model.SelectedPattern = null;
                }
            };
            model.Search = "000067";
            Assert.Single(model.Patterns);
            model.SelectedPattern = model.Patterns.Single();
            Assert.True(model.HasPendingProposal);
            Assert.False(catalog.IsMappingDirty);
            Assert.True(model.TryFinish());
            Assert.Equal(103u, catalog.GetPatternHash(2));
            Assert.False(model.HasPendingProposal);
            Assert.True(model.CanReset);
            Assert.Equal(1, refreshes);
            Assert.DoesNotContain("UNASSIGNED", model.SelectedMaterial!.DisplayName);
            Assert.True(model.TryFinish()); // No second mutation for an applied selection.
            Assert.Equal(1, refreshes);
            model.Reset();
            Assert.Equal(0u, catalog.GetPatternHash(2));
            Assert.False(model.CanReset);
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public void Done_does_not_close_when_a_proposed_mapping_cannot_be_created()
    {
        var rows = Enumerable.Range(1, 32).Select(i => (uint)i).ToArray();
        var (geometry, _, catalog) = Fixture([.. rows, 33], rows);
        try
        {
            var model = new OctocamoRemapViewModel(catalog, 33, () => throw new Exception("Must not refresh"), _ => null);
            model.SelectedPattern = model.Patterns.Single(option => option.Hash == 103);
            Assert.False(model.TryFinish());
            Assert.Contains("preserved", model.Status);
            Assert.True(model.HasPendingProposal);
            Assert.False(catalog.IsMappingDirty);
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public void Existing_mapping_changes_only_its_pattern_and_runtime_cache_cells()
    {
        var (geometry, block, catalog) = Fixture([1, 2], [1, 2]);
        try
        {
            var before = catalog.GetMappingBytes();
            var selectors = catalog.GetTable(block)!.Materials.ToArray();
            catalog.RemapPattern(1, 103);
            var after = catalog.GetMappingBytes();
            Assert.True(catalog.IsMappingDirty);
            Assert.Equal(103u, catalog.Resolve(block, 0).PatternHash);
            Assert.Equal(selectors, catalog.GetTable(block)!.Materials);
            Assert.Equal(before[..0x614], after[..0x614]);
            Assert.Equal(before[0x620..], after[0x620..]);
            Assert.Equal(new byte[8], after[0x618..0x620]);
            var crypto = new HavenStudio.CryptoService();
            Assert.Equal(after, crypto.Decrypt(crypto.Encrypt(after, "stage/n023a"), "stage/n023a"));
            catalog.ResetMappings();
            Assert.Equal(before, catalog.GetMappingBytes());
            Assert.False(catalog.IsMappingDirty);
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public void Unassigned_material_can_add_a_row_without_changing_existing_mapping_or_cloth()
    {
        var (geometry, block, catalog) = Fixture([1, 2], [1]);
        try
        {
            var before = catalog.GetMappingBytes();
            var surface = catalog.Resolve(block, 0x40);
            Assert.False(surface.HasPatternMapping);
            Assert.True(surface.HasClothMapping);
            catalog.RemapPattern(2, 103);
            Assert.True(catalog.Resolve(block, 0x40).IsMapped);
            Assert.Equal(surface.ClothColour, catalog.Resolve(block, 0x40).ClothColour);
            Assert.Equal(2, catalog.MappingCount);
            Assert.Equal(before[16..0x620], catalog.GetMappingBytes()[16..0x620]);
            Assert.Equal(101u, catalog.GetPatternHash(1));
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public void Full_table_reuses_only_a_material_absent_from_all_geom_tables()
    {
        var mappings = Enumerable.Range(1, 32).Select(i => (uint)i).ToArray();
        var (geometry, _, catalog) = Fixture([1, 2, 33], mappings);
        try
        {
            catalog.RemapPattern(33, 103);
            Assert.Equal(32, catalog.MappingCount);
            Assert.Equal(103u, catalog.GetPatternHash(33));
            Assert.Equal(101u, catalog.GetPatternHash(1));
            Assert.Equal(102u, catalog.GetPatternHash(2));
            Assert.Equal(0u, catalog.GetPatternHash(3));
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public void Full_used_table_and_unregistered_patterns_are_rejected_without_mutation()
    {
        var mappings = Enumerable.Range(1, 32).Select(i => (uint)i).ToArray();
        var (geometry, _, catalog) = Fixture([.. mappings, 33], mappings);
        try
        {
            var before = catalog.GetMappingBytes();
            Assert.Throws<InvalidOperationException>(() => catalog.RemapPattern(33, 103));
            Assert.Throws<InvalidOperationException>(() => catalog.RemapPattern(1, 999));
            Assert.Equal(before, catalog.GetMappingBytes());
            Assert.False(catalog.IsMappingDirty);
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public async Task Save_patches_dar_in_place_preserves_other_resources_and_keeps_exact_backup()
    {
        var (geometry, _, catalog) = Fixture([1, 2], [1, 2]);
        using var temp = new TempDirectory();
        try
        {
            var dar = new Dar();
            dar.Entries.Add(new DarEntry("before.bin", [1, 2, 3]));
            dar.Entries.Add(new DarEntry("test.octt", catalog.GetMappingBytes()));
            dar.Entries.Add(new DarEntry("after.bin", [4, 5, 6]));
            using var stream = new MemoryStream();
            DarFile.Write(stream, dar);
            var original = stream.ToArray();
            var path = temp.GetPath("cache.dar");
            File.WriteAllBytes(path, original);
            var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
            await workspace.ScanAsync();
            catalog.RemapPattern(1, 103);
            var backup = await OctocamoMappingSave.SaveAsync(workspace, WorkspacePath.ArchiveEntry(path, "test.octt"), catalog);
            Assert.Equal(original, File.ReadAllBytes(backup));
            Assert.False(catalog.IsMappingDirty);
            var saved = File.ReadAllBytes(path);
            Assert.Equal(original.Length, saved.Length);
            var tableOffset = original.AsSpan().IndexOf(dar.Entries[1].Bytes!);
            Assert.Equal(original[..tableOffset], saved[..tableOffset]);
            Assert.Equal(original[(tableOffset + 0x810)..], saved[(tableOffset + 0x810)..]);
            using var checkedStream = new MemoryStream(saved);
            var checkedDar = DarFile.Read(checkedStream);
            Assert.Equal(dar.Entries[0].Bytes, checkedDar.Entries[0].Bytes);
            Assert.Equal(dar.Entries[2].Bytes, checkedDar.Entries[2].Bytes);
            Assert.Equal(catalog.GetMappingBytes(), checkedDar.Entries[1].Bytes);
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public async Task Save_rejects_a_changed_source_table_before_backup_or_write()
    {
        var (geometry, _, catalog) = Fixture([1, 2], [1, 2]);
        using var temp = new TempDirectory();
        try
        {
            var changed = catalog.GetMappingBytes();
            changed[^1] ^= 1;
            var path = temp.GetPath("test.octt");
            File.WriteAllBytes(path, changed);
            var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
            await workspace.ScanAsync();
            catalog.RemapPattern(1, 103);
            await Assert.ThrowsAsync<IOException>(() => OctocamoMappingSave.SaveAsync(workspace, WorkspacePath.Physical(path), catalog));
            Assert.Equal(changed, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(temp.Path));
            Assert.True(catalog.IsMappingDirty);
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public void Conflicting_duplicate_material_rows_do_not_disable_other_stage_mappings()
    {
        var (geometry, _, catalog) = Fixture([1, 2], [1, 2]);
        try
        {
            var bytes = catalog.GetMappingBytes();
            W(bytes, 0x630, 77);
            using var reserved = new OctocamoSurfaceCatalog(geometry, bytes);
            Assert.Equal(0, reserved.AvailableMappingRows);
            W(bytes, 0x630, 1);
            W(bytes, 0x634, 103);
            W(bytes, 4, 3);
            using var duplicate = new OctocamoSurfaceCatalog(geometry, bytes);
            Assert.Contains(1u, duplicate.AmbiguousMaterialHashes);
            Assert.Equal(0u, duplicate.GetPatternHash(1));
            Assert.Equal(102u, duplicate.GetPatternHash(2));
            Assert.Throws<InvalidOperationException>(() => duplicate.RemapPattern(1, 103));
            Assert.False(duplicate.IsMappingDirty);
            Assert.Equal(bytes, duplicate.GetMappingBytes());

            W(bytes, 0x634, 101);
            using var identical = new OctocamoSurfaceCatalog(geometry, bytes);
            Assert.Empty(identical.AmbiguousMaterialHashes);
            Assert.Equal(101u, identical.GetPatternHash(1));
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public async Task Save_rejects_changed_archive_even_when_size_and_timestamp_match_the_cache()
    {
        var (geometry, _, catalog) = Fixture([1], [1]);
        using var temp = new TempDirectory();
        try
        {
            var dar = new Dar();
            dar.Entries.Add(new DarEntry("test.octt", catalog.GetMappingBytes()));
            using var stream = new MemoryStream();
            DarFile.Write(stream, dar);
            var bytes = stream.ToArray();
            var path = temp.GetPath("cache.dar");
            File.WriteAllBytes(path, bytes);
            var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
            await workspace.ScanAsync();
            var entry = WorkspacePath.ArchiveEntry(path, "test.octt");
            Assert.Equal(catalog.GetMappingBytes(), workspace.ReadAllBytes(entry));
            var timestamp = File.GetLastWriteTimeUtc(path);
            var offset = bytes.AsSpan().IndexOf(catalog.GetMappingBytes());
            bytes[offset + 0x614] ^= 1;
            File.WriteAllBytes(path, bytes);
            File.SetLastWriteTimeUtc(path, timestamp);
            catalog.RemapPattern(1, 103);
            await Assert.ThrowsAsync<IOException>(() => OctocamoMappingSave.SaveAsync(workspace, entry, catalog));
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(temp.Path));
        }
        finally { catalog.Dispose(); geometry.CloseStream(); }
    }

    [Fact]
    public async Task Opt_in_real_jj_archive_save_and_encrypted_round_trip_preserve_every_other_byte()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_STAGE");
        var slot = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_SLOT");
        if (string.IsNullOrWhiteSpace(stage) || string.IsNullOrWhiteSpace(slot)) return;
        using var temp = new TempDirectory();
        var source = File.ReadAllBytes(Path.Combine(stage, "cache.dar"));
        var geomBytes = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        var geometry = new GeomFile(new MemoryStream(geomBytes, false), Endianness.Big);
        try
        {
            using var input = new MemoryStream(source, false);
            var entries = DarFile.Read(input).Entries;
            var table = entries.Single(entry => entry.Filename.EndsWith(".octt"));
            using var catalog = new OctocamoSurfaceCatalog(geometry, table.Bytes!,
                entries.Single(entry => entry.Filename.EndsWith(".octl")).Bytes!);
            catalog.SetPatternPreviews(OctocamoPatternLibrary.LoadAll(slot));
            var pattern = catalog.AvailablePatternHashes.First(hash => hash != 0x32B830 && catalog.IsPatternRegistered(hash));
            catalog.RemapPattern(0x15BCCC, pattern);
            var path = temp.GetPath("cache.dar");
            File.WriteAllBytes(path, source);
            var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
            await workspace.ScanAsync();
            var backup = await OctocamoMappingSave.SaveAsync(workspace,
                WorkspacePath.ArchiveEntry(path, table.Filename), catalog);
            var saved = File.ReadAllBytes(path);
            Assert.Equal(source, File.ReadAllBytes(backup));
            Assert.Equal(source.Length, saved.Length);
            var tableOffset = source.AsSpan().IndexOf(table.Bytes!);
            Assert.True(tableOffset >= 0);
            var row = Enumerable.Range(0, 32).Single(i => BinaryPrimitives.ReadUInt32BigEndian(table.Bytes!.AsSpan(0x610 + i * 16, 4)) == 0x15BCCC);
            var from = tableOffset + 0x614 + row * 16;
            Assert.Equal(source[..from], saved[..from]);
            Assert.Equal(source[(from + 12)..], saved[(from + 12)..]);
            var crypto = new HavenStudio.CryptoService();
            var decrypted = crypto.Decrypt(crypto.Encrypt(saved, "stage/n023a"), "stage/n023a");
            Assert.Equal(saved, decrypted);
            using var reopened = new MemoryStream(decrypted, false);
            var checkedEntries = DarFile.Read(reopened).Entries;
            Assert.Equal(entries.Count, checkedEntries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                Assert.Equal(entries[i].Filename, checkedEntries[i].Filename);
                Assert.Equal(entries[i].Filename == table.Filename ? catalog.GetMappingBytes() : entries[i].Bytes,
                    checkedEntries[i].Bytes);
            }
            Assert.Equal(source, File.ReadAllBytes(Path.Combine(stage, "cache.dar")));
            Assert.Equal(geomBytes, File.ReadAllBytes(Path.Combine(stage, "n023a.geom")));
            Assert.False(catalog.IsMappingDirty);
            output.WriteLine($"Real JJ cache save: {saved.Length:N0} bytes, only the remapped row's 12-byte pattern/cache region changed; encrypted stage-key round trip passed.");
        }
        finally { geometry.CloseStream(); }
    }

    [Fact]
    public void Opt_in_full_retail_library_and_soil_remap_are_stage_wide()
    {
        var stage = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_STAGE");
        var slot = Environment.GetEnvironmentVariable("HAVEN_OCTOCAMO_SLOT");
        if (string.IsNullOrWhiteSpace(stage) || string.IsNullOrWhiteSpace(slot)) return;
        var geomBytes = File.ReadAllBytes(Path.Combine(stage, "n023a.geom"));
        var geometry = new GeomFile(new MemoryStream(geomBytes, false), Endianness.Big);
        using var darStream = File.OpenRead(Path.Combine(stage, "cache.dar"));
        var entries = DarFile.Read(darStream).Entries;
        try
        {
            using var catalog = new OctocamoSurfaceCatalog(geometry,
                entries.Single(entry => entry.Filename.EndsWith(".octt")).Bytes!,
                entries.Single(entry => entry.Filename.EndsWith(".octl")).Bytes!);
            var library = OctocamoPatternLibrary.LoadAll(slot);
            catalog.SetPatternPreviews(library);
            var all = catalog.AllMaterialHashes.ToArray();
            var missing = all.Where(hash => catalog.GetPatternHash(hash) == 0).ToArray();
            output.WriteLine($"Decoded SLOT patterns={library.Count}, unregistered={library.Keys.Count(hash => !catalog.IsPatternRegistered(hash))}, GEOM materials={all.Length}, missing={missing.Length}");
            output.WriteLine($"Missing: {string.Join(',', missing.Select(hash => hash.ToString("X6")))}");
            Assert.True(library.Count > catalog.PatternHashes.Count());
            Assert.Equal(317, library.Count);
            Assert.All(library.Keys, hash => Assert.True(catalog.IsPatternRegistered(hash)));
            // The opt-in stage is user-editable, so respect mappings already saved
            // by the remapping workflow instead of assuming the original JJ table.
            var availableBefore = catalog.AvailableMappingRows;
            Assert.InRange(availableBefore, 0, 32);
            var originalSoilPattern = catalog.GetPatternHash(0x15BCCC);
            Assert.NotEqual(0u, originalSoilPattern);
            var block = geometry.GeomBlocks.First(block => catalog.GetTable(block)?.Materials.Contains(0x15BCCC) == true);
            var table = catalog.GetTable(block)!;
            var selector = (ushort)(table.Materials.IndexOf(0x15BCCC) << 6);
            var replacement = library.Keys.First(hash => hash != originalSoilPattern && catalog.IsPatternRegistered(hash));
            var originalOctt = catalog.GetMappingBytes();
            catalog.RemapPattern(0x15BCCC, replacement);
            foreach (var other in geometry.GeomBlocks.Where(block => catalog.GetTable(block)?.Materials.Contains(0x15BCCC) == true))
            {
                var attr = (ushort)(catalog.GetTable(other)!.Materials.IndexOf(0x15BCCC) << 6);
                Assert.Equal(replacement, catalog.Resolve(other, attr).PatternHash);
            }
            using var outputGeom = new MemoryStream();
            geometry.SaveSurgicalEdits(geomBytes, outputGeom, Endianness.Big);
            Assert.Equal(geomBytes, outputGeom.ToArray());
            var afterOctt = catalog.GetMappingBytes();
            var editedRow = Enumerable.Range(0, 32).Single(i => BinaryPrimitives.ReadUInt32BigEndian(originalOctt.AsSpan(0x610 + i * 16, 4)) == 0x15BCCC);
            var from = 0x614 + editedRow * 16;
            Assert.Equal(originalOctt[..from], afterOctt[..from]);
            Assert.Equal(originalOctt[(from + 12)..], afterOctt[(from + 12)..]);
            var successfullyAdded = 0;
            foreach (var material in missing)
            {
                try { catalog.RemapPattern(material, replacement); successfullyAdded++; output.WriteLine($"Added mapping for {material:X6}"); }
                catch (InvalidOperationException exception) { output.WriteLine($"Capacity/registry boundary for {material:X6}: {exception.Message}"); }
            }
            Assert.Equal(Math.Min(availableBefore, missing.Length), successfullyAdded);
            Assert.Equal(0, catalog.AvailableMappingRows);
        }
        finally { geometry.CloseStream(); }
    }

    private static (GeomFile Geometry, GeoBlock Block, OctocamoSurfaceCatalog Catalog) Fixture(uint[] used, uint[] rows)
    {
        var bytes = new byte[0x90];
        W(bytes, 0, 1); W(bytes, 4, (uint)bytes.Length); W(bytes, 0x74, 1);
        var geometry = new GeomFile(new MemoryStream(bytes, false), Endianness.Big);
        var group = geometry.GeomGroups.Single();
        var block = new GeoBlock();
        geometry.GeomGroupBlocks[group].Add(block);
        var table = new GeoMaterialHeader(0, 0, 0, 0, []) { Materials = used.ToList(), Colors = [500] };
        geometry.GroupMaterialData[group] = table;
        var octt = new byte[0x810];
        W(octt, 0, 3); W(octt, 4, (uint)rows.Length); W(octt, 8, 1); W(octt, 16, 500);
        W(octt, 32, BitConverter.SingleToUInt32Bits(0.2f)); W(octt, 36, BitConverter.SingleToUInt32Bits(0.3f)); W(octt, 40, BitConverter.SingleToUInt32Bits(0.4f));
        for (var i = 0; i < rows.Length; i++)
        {
            W(octt, 0x610 + i * 16, rows[i]); W(octt, 0x614 + i * 16, 101u + (uint)i);
            W(octt, 0x618 + i * 16, 0x12345678); W(octt, 0x61C + i * 16, 0x87654321);
        }
        var octl = new byte[16 + (3 + used.Length) * 4];
        W(octl, 0, 3); W(octl, 4, 3); W(octl, 8, (uint)used.Length);
        W(octl, 16, 101); W(octl, 20, 102); W(octl, 24, 103);
        for (var i = 0; i < used.Length; i++) W(octl, 28 + i * 4, used[i]);
        var catalog = new OctocamoSurfaceCatalog(geometry, octt, octl);
        catalog.SetPatternPreviews(new Dictionary<uint, OctocamoPatternPreview>
        {
            [101] = new(1, 1, [1, 2, 3, 255]), [102] = new(1, 1, [4, 5, 6, 255]),
            [103] = new(1, 1, [7, 8, 9, 255]), [999] = new(1, 1, [9, 8, 7, 255])
        });
        return (geometry, block, catalog);
    }
    private static void W(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset, 4), value);
}
