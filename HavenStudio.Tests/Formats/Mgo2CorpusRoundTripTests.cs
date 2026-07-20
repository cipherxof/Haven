using System.Buffers.Binary;
using System.Text;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dar;
using HavenStudio.Formats.Dlz;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Geo;
using HavenStudio.Formats.Mdn;
using HavenStudio.Formats.Qar;
using HavenStudio.Formats.Txn;
using HavenStudio.Services.Persistence;
using HavenStudio.Tests.TestSupport;
using HavenStudio.Utils;

namespace HavenStudio.Tests.Formats;

public sealed class Mgo2CorpusRoundTripTests
{
    private const string CorpusDirectoryEnvironmentVariable = "HAVENSTUDIO_MGO2_CORPUS_DIRECTORY";
    private const string DefaultCorpusDirectory =
        "/home/trigger/.config/rpcs3/dev_hdd0/game/NPMG00020/USRDIR/o/dl/p/stage";
    private const int ComparisonBufferSize = 64 * 1024;
    private const int MaximumReportedIssuesPerFormat = 10;

    private static readonly IReadOnlyDictionary<string, Action<Stream, Stream>> RoundTrips =
        new Dictionary<string, Action<Stream, Stream>>(StringComparer.OrdinalIgnoreCase)
        {
            [".dar"] = RoundTripDar,
            [".dlz"] = RoundTripDlz,
            [".gcx"] = RoundTripGcx,
            [".geom"] = RoundTripGeom,
            [".mdn"] = RoundTripMdn,
            [".qar"] = RoundTripQar,
            [".txn"] = RoundTripTxn
        };

    [Fact]
    [Trait("Category", "Corpus")]
    public void Supported_mgo2_files_round_trip_to_identical_bytes()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            // This project uses xUnit v2, which cannot report a runtime skip. Treat an absent
            // private corpus as a no-op so the portable test suite remains runnable.
            return;
        }

        var files = Directory
            .EnumerateFiles(corpusDirectory, "*", SearchOption.AllDirectories)
            .Where(path => RoundTrips.ContainsKey(Path.GetExtension(path)))
            .OrderBy(path => Path.GetRelativePath(corpusDirectory, path), StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(files);

        using var temp = new TempDirectory();
        var summaries = RoundTrips.Keys.ToDictionary(
            extension => extension,
            _ => new FormatSummary(),
            StringComparer.OrdinalIgnoreCase);
        var issues = new List<string>();
        var reportedIssueCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var issueCount = 0;

        foreach (var sourcePath in files)
        {
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            var relativePath = Path.GetRelativePath(corpusDirectory, sourcePath);
            var outputPath = temp.GetPath($"roundtrip{extension}");
            var decryptedOutputPath = temp.GetPath($"roundtrip.decrypted{extension}");
            var summary = summaries[extension];
            summary.Scanned++;

            try
            {
                RoundTripFile(sourcePath, outputPath, decryptedOutputPath, extension);

                var difference = FindFirstDifference(sourcePath, outputPath);
                if (difference is null)
                {
                    summary.Identical++;
                    continue;
                }

                summary.Mismatched++;
                issueCount++;
                AddIssue(
                    issues,
                    reportedIssueCounts,
                    extension,
                    $"{relativePath}: {difference}");
            }
            catch (Exception exception)
            {
                summary.Errors++;
                issueCount++;
                AddIssue(
                    issues,
                    reportedIssueCounts,
                    extension,
                    $"{relativePath}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        if (issueCount > 0)
        {
            Assert.Fail(BuildFailureMessage(files.Length, issueCount, summaries, issues));
        }
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void Mgo2_maps_round_trip_through_map_json_to_identical_bytes()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            return;
        }

        DictionaryFile.Load("dictionary.txt", "dictionary-aliases.txt");
        CommandFile.Load("commands.txt");
        var crypto = new CryptoService();
        var checkedMaps = 0;
        var issues = new List<string>();
        foreach (var gcxPath in Directory.EnumerateFiles(corpusDirectory, "*.gcx", SearchOption.AllDirectories))
        {
            var geomPath = Directory
                .EnumerateFiles(Path.GetDirectoryName(gcxPath)!, "*.geom", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (geomPath == null)
            {
                continue;
            }

            checkedMaps++;
            try
            {
                var folderKey = BuildMgo2FolderKey(gcxPath);
                var gcxBytes = crypto.Decrypt(File.ReadAllBytes(gcxPath), folderKey);
                var geomBytes = crypto.Decrypt(File.ReadAllBytes(geomPath), folderKey);
                var relativeGcx = Path.GetRelativePath(corpusDirectory, gcxPath);
                var relativeGeom = Path.GetRelativePath(corpusDirectory, geomPath);
                var document = MapDocumentBuilder.Build(
                    gcxBytes,
                    geomBytes,
                    new MapDocumentSources { Gcx = relativeGcx, Geom = relativeGeom },
                    Endianness.Big,
                    isMgs3: false);
                var restoredDocument = MapJsonIO.Deserialize(MapJsonIO.Serialize(document));
                var restored = MapDocumentApplier.Apply(
                    restoredDocument,
                    Endianness.Big,
                    isMgs3: false);
                if (!gcxBytes.AsSpan().SequenceEqual(restored.GcxBytes))
                {
                    issues.Add($"{relativeGcx}: GCX bytes changed");
                }
                if (!geomBytes.AsSpan().SequenceEqual(restored.GeomBytes))
                {
                    issues.Add($"{relativeGeom}: GEOM bytes changed");
                }
            }
            catch (Exception exception)
            {
                issues.Add(
                    $"{Path.GetRelativePath(corpusDirectory, gcxPath)}: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.True(checkedMaps > 0, "The MGO2 corpus did not contain a GCX/GEOM map pair.");
        Assert.True(
            issues.Count == 0,
            "Map JSON round-trip issues:\n" + string.Join("\n", issues.Take(20)));
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void Gcx_recorded_literal_sites_match_decompiled_model_placements()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            return;
        }

        DictionaryFile.Load("dictionary.txt", "dictionary-aliases.txt");
        CommandFile.Load("commands.txt");
        var issues = new List<string>();
        var checkedSites = 0;
        var checkedCollisionReferences = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(corpusDirectory, "*.gcx", SearchOption.AllDirectories))
        {
            var decrypted = new CryptoService().Decrypt(
                File.ReadAllBytes(sourcePath),
                BuildMgo2FolderKey(sourcePath));
            using var stream = new MemoryStream(decrypted, writable: false);
            var document = GcxFile.Read(stream);
            CheckScript(document.MainScript?.Bytes, "main");
            for (var index = 0; index < document.ScriptDefinitions.Count; index++)
            {
                CheckScript(document.ScriptDefinitions[index].Script?.Bytes, $"proc{index + 1}");
            }

            void CheckScript(byte[]? bytes, string scriptName)
            {
                if (bytes == null || bytes.Length == 0)
                {
                    return;
                }
                var sites = new List<GcxPlacementSite>();
                var decompiled = GcxDecompiler.Decompile(bytes, scriptName, isMgs3: false, sites);
                var placements = new GcxModelReferenceScanner()
                    .ScanDecompiledScripts([decompiled])
                    .PlacedModels;
                foreach (var site in sites.Where(site =>
                    site.IsModelPlacement &&
                    !site.IsNested &&
                    site.ModelHash is > 0 &&
                    site.Position?.HasThreeLiteralComponents == true))
                {
                    checkedSites++;
                    var values = site.Position!.Components;
                    var worldPosition = new OpenTK.Mathematics.Vector3(
                        values[0].Value,
                        values[2].Value,
                        values[1].Value);
                    if (!placements.Any(placement =>
                        placement.ModelHash == site.ModelHash &&
                        placement.Position == worldPosition))
                    {
                        issues.Add(
                            $"{Path.GetRelativePath(corpusDirectory, sourcePath)}:{scriptName} " +
                            $"{site.CommandName} 0x{site.ModelHash:X6} at {worldPosition}");
                    }
                }

                foreach (var site in sites.Where(site =>
                    site.IsModelPlacement &&
                    !site.IsNested &&
                    site.ModelHash is > 0 &&
                    site.CollisionReferenceHash is > 0))
                {
                    checkedCollisionReferences++;
                    if (!placements.Any(placement =>
                        placement.ModelHash == site.ModelHash &&
                        placement.CollisionReferenceHash == site.CollisionReferenceHash))
                    {
                        issues.Add(
                            $"{Path.GetRelativePath(corpusDirectory, sourcePath)}:{scriptName} " +
                            $"{site.CommandName} 0x{site.ModelHash:X6} collision reference " +
                            $"0x{site.CollisionReferenceHash:X6}");
                    }
                }
            }
        }

        Assert.True(checkedSites > 0, "The MGO2 corpus did not contain a direct literal model placement.");
        Assert.True(
            checkedCollisionReferences > 0,
            "The MGO2 corpus did not contain a model placement with a collision reference.");
        Assert.True(issues.Count == 0, "Recorded/regex placement mismatches:\n" + string.Join("\n", issues.Take(20)));
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void Gcx_foreach_model_cells_are_recorded_as_writable_sites()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            return;
        }

        DictionaryFile.Load("dictionary.txt", "dictionary-aliases.txt");
        CommandFile.Load("commands.txt");
        var recorded = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(corpusDirectory, "*.gcx", SearchOption.AllDirectories))
        {
            var decrypted = new CryptoService().Decrypt(
                File.ReadAllBytes(sourcePath),
                BuildMgo2FolderKey(sourcePath));
            using var stream = new MemoryStream(decrypted, writable: false);
            var document = GcxFile.Read(stream);
            CheckScript(document.MainScript?.Bytes, "main");
            for (var index = 0; index < document.ScriptDefinitions.Count; index++)
            {
                CheckScript(document.ScriptDefinitions[index].Script?.Bytes, $"proc{index + 1}");
            }

            void CheckScript(byte[]? bytes, string scriptName)
            {
                if (bytes == null || bytes.Length == 0)
                {
                    return;
                }

                var sites = new List<GcxPlacementSite>();
                GcxDecompiler.Decompile(bytes, scriptName, isMgs3: false, sites);
                foreach (var modelSite in sites.SelectMany(site => site.ForeachModelSites).OfType<GcxStringCodeSite>())
                {
                    var replacement = modelSite.Value == 0x123456 ? 0x654321u : 0x123456u;
                    var rewritten = GcxPlacementWriter.WriteModelHash(bytes, modelSite, replacement);
                    Assert.Equal((byte)(replacement & 0xFF), rewritten.Bytes[modelSite.ValueOffset]);
                    Assert.Equal((byte)((replacement >> 8) & 0xFF), rewritten.Bytes[modelSite.ValueOffset + 1]);
                    Assert.Equal((byte)((replacement >> 16) & 0xFF), rewritten.Bytes[modelSite.ValueOffset + 2]);
                    recorded++;
                }
            }
        }

        Assert.True(recorded > 0, "The MGO2 corpus did not expose a writable foreach model cell.");
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void Geom_effect_rotations_use_the_file_endianness()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            return;
        }

        var checkedRotations = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(corpusDirectory, "*.geom", SearchOption.AllDirectories))
        {
            var decrypted = new CryptoService().Decrypt(
                File.ReadAllBytes(sourcePath),
                BuildMgo2FolderKey(sourcePath));
            using var stream = new MemoryStream(decrypted, writable: false);
            var geometry = new GeomFile(stream, Endianness.Big);
            foreach (var effect in TreeTraversal.Flatten(geometry.GeoEffects, effect => effect.Children))
            {
                var rotationSlot = (effect.Index >> 10) & 0x3FF;
                var rotationOffset = effect.ChunkOffset + rotationSlot * 8;
                if (rotationSlot == 0 || rotationOffset < 0 ||
                    rotationOffset > geometry.GeomChunk6.Length - 6)
                {
                    continue;
                }

                var data = geometry.GeomChunk6.AsSpan(rotationOffset, 6);
                Assert.Equal(
                    GeoEffectChunkPatcher.DecodeAngle(BinaryPrimitives.ReadInt16BigEndian(data)),
                    effect.RotationX);
                Assert.Equal(
                    GeoEffectChunkPatcher.DecodeAngle(BinaryPrimitives.ReadInt16BigEndian(data[2..])),
                    effect.RotationY);
                Assert.Equal(
                    GeoEffectChunkPatcher.DecodeAngle(BinaryPrimitives.ReadInt16BigEndian(data[4..])),
                    effect.RotationZ);
                checkedRotations++;
            }
            geometry.CloseStream();
        }

        Assert.True(checkedRotations > 0, "The MGO2 corpus did not contain an effect rotation slot.");
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void Geom_engine_structures_match_radix_allocator_and_effect_predictions()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            return;
        }

        var files = Directory
            .EnumerateFiles(corpusDirectory, "*.geom", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(corpusDirectory, path), StringComparer.Ordinal)
            .ToArray();
        var issues = new List<string>();
        var checkedFiles = 0;
        var checkedRadixReferences = 0;
        var checkedGeoms = 0;
        var checkedEffects = 0;

        foreach (var sourcePath in files)
        {
            try
            {
                var decrypted = new CryptoService().Decrypt(
                    File.ReadAllBytes(sourcePath),
                    BuildMgo2FolderKey(sourcePath));
                using var stream = new MemoryStream(decrypted, writable: false);
                var geometry = new GeomFile(stream, Endianness.Big);
                var result = GeoStructureValidator.Validate(geometry);
                checkedFiles++;
                checkedRadixReferences += result.Summary.RadixReferences;
                checkedGeoms += result.Summary.Geoms;
                checkedEffects += result.Summary.Effects;
                foreach (var issue in result.Issues.Take(10))
                {
                    issues.Add($"{Path.GetRelativePath(corpusDirectory, sourcePath)}: {issue}");
                }
                geometry.CloseStream();
            }
            catch (Exception exception)
            {
                issues.Add(
                    $"{Path.GetRelativePath(corpusDirectory, sourcePath)}: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        Assert.Equal(files.Length, checkedFiles);
        Assert.True(checkedRadixReferences > 0, "No populated radix type references were checked.");
        Assert.True(checkedGeoms > 0, "No block allocator GEOM links were checked.");
        Assert.True(checkedEffects > 0, "No chunk-6 effects were checked.");
        Assert.True(
            issues.Count == 0,
            "GEOM structure characterization issues:\n" + string.Join("\n", issues.Take(40)));
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void Geom_allocator_and_radix_builders_reproduce_the_corpus()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            return;
        }

        var checkedFiles = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(corpusDirectory, "*.geom", SearchOption.AllDirectories))
        {
            var decrypted = new CryptoService().Decrypt(
                File.ReadAllBytes(sourcePath),
                BuildMgo2FolderKey(sourcePath));
            using var source = new MemoryStream(decrypted, writable: false);
            var geometry = new GeomFile(source, Endianness.Big);

            foreach (var block in geometry.GeomBlocks)
            {
                if (geometry.BlockFaceData.TryGetValue(block, out var records))
                {
                    geometry.BlockVertexData.TryGetValue(block, out var vertexHeader);
                    GeoBlockArenaBuilder.Capture(block, records, vertexHeader).Rebuild();
                }
            }
            foreach (var group in geometry.GeomGroups)
            {
                GeoRadixBuilder.Rebuild(geometry, group);
            }
            if (geometry.GetChunkFromType(GeoChunkType.PROPS) != null)
            {
                var effectLayout = GeoEffectChunkBuilder.Capture(
                    geometry.GeomChunk6,
                    geometry.GeoEffects,
                    Endianness.Big);
                geometry.GeomChunk6 = effectLayout.Rebuild(geometry.GeoEffects);
            }

            using var rebuilt = new MemoryStream();
            geometry.Save(rebuilt, Endianness.Big);
            geometry.CloseStream();
            Assert.True(
                decrypted.AsSpan().SequenceEqual(rebuilt.ToArray()),
                $"GEOM builders changed {Path.GetRelativePath(corpusDirectory, sourcePath)}.");
            checkedFiles++;
        }

        Assert.True(checkedFiles > 0, "No GEOM corpus files were checked.");
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void Geom_mesh_decoder_covers_every_polygon_record()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            return;
        }

        var issues = new List<string>();
        var polygonCount = 0;
        Span<byte> encoded = stackalloc byte[5];
        foreach (var sourcePath in Directory
            .EnumerateFiles(corpusDirectory, "*.geom", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(corpusDirectory, path), StringComparer.Ordinal))
        {
            var decrypted = new CryptoService().Decrypt(
                File.ReadAllBytes(sourcePath),
                BuildMgo2FolderKey(sourcePath));
            using var stream = new MemoryStream(decrypted, writable: false);
            var geometry = new GeomFile(stream, Endianness.Big);
            for (var blockIndex = 0; blockIndex < geometry.GeomBlocks.Count; blockIndex++)
            {
                var block = geometry.GeomBlocks[blockIndex];
                if (!geometry.BlockVertexData.TryGetValue(block, out var vertices) ||
                    !geometry.BlockFaceData.TryGetValue(block, out var faces))
                {
                    continue;
                }

                var expectedPolygons = faces.Sum(face => face.Poly?.Length ?? 0);
                if (expectedPolygons == 0)
                {
                    continue;
                }

                polygonCount += expectedPolygons;
                foreach (var polygon in faces.SelectMany(face => face.Poly ?? []))
                {
                    var a = polygon.Data[0] + 1;
                    var b = polygon.Data[1] + 1;
                    var c = polygon.Data[2] + 1;
                    var d = polygon.Data[3] + 1;
                    GeomUtils.FaceBitCalculation(polygon.Data[4], ref a, ref b, ref c, ref d);
                    GeomUtils.EncodeFaceIndices(a - 1, b - 1, c - 1, d - 1, encoded);
                    if (!encoded.SequenceEqual(polygon.Data.AsSpan(0, 5)))
                    {
                        issues.Add(
                            $"{Path.GetRelativePath(corpusDirectory, sourcePath)}: block {blockIndex} " +
                            $"face-bit inverse {Convert.ToHexString(polygon.Data.AsSpan(0, 5))} -> " +
                            $"({a - 1},{b - 1},{c - 1},{d - 1}) -> {Convert.ToHexString(encoded)}");
                    }
                }
                if (!GeomMeshDecoder.TryDecodeBlock(vertices, faces, out var decoded))
                {
                    issues.Add($"{Path.GetRelativePath(corpusDirectory, sourcePath)}: block {blockIndex} did not decode");
                    continue;
                }

                if (decoded.TriangleCount != expectedPolygons * 2)
                {
                    issues.Add(
                        $"{Path.GetRelativePath(corpusDirectory, sourcePath)}: block {blockIndex} " +
                        $"decoded {decoded.TriangleCount} triangles for {expectedPolygons} quads");
                }
            }
            geometry.CloseStream();
        }

        Assert.True(polygonCount > 0, "No GEOM polygon records were checked.");
        Assert.True(
            issues.Count == 0,
            "GEOM mesh decoder coverage issues:\n" + string.Join("\n", issues.Take(40)));
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void Gcx_property_placements_resolve_to_geom_effect_transforms()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            return;
        }

        DictionaryFile.Load("dictionary.txt", "dictionary-aliases.txt");
        CommandFile.Load("commands.txt");
        var checkedPlacements = 0;
        foreach (var gcxPath in Directory.EnumerateFiles(corpusDirectory, "*.gcx", SearchOption.AllDirectories))
        {
            var geometryPath = Directory
                .EnumerateFiles(Path.GetDirectoryName(gcxPath)!, "*.geom", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (geometryPath == null)
            {
                continue;
            }

            var geometryBytes = new CryptoService().Decrypt(
                File.ReadAllBytes(geometryPath),
                BuildMgo2FolderKey(geometryPath));
            using var geometryStream = new MemoryStream(geometryBytes, writable: false);
            var geometry = new GeomFile(geometryStream, Endianness.Big);
            var gcxBytes = new CryptoService().Decrypt(
                File.ReadAllBytes(gcxPath),
                BuildMgo2FolderKey(gcxPath));
            using var gcxStream = new MemoryStream(gcxBytes, writable: false);
            var document = GcxFile.Read(gcxStream);
            var references = new GcxModelReferenceScanner().Scan(document, geometry, isMgs3: false);
            var effects = TreeTraversal.Flatten(geometry.GeoEffects, effect => effect.Children).ToArray();
            foreach (var placement in references.PlacedModels.Where(placement =>
                placement.PropertyPositionHash is > 0 && placement.SourceEffect != null))
            {
                var source = placement.SourceEffect!;
                Assert.Equal(
                    new OpenTK.Mathematics.Vector3(source.X, source.Y, source.Z),
                    placement.Position);
                Assert.Equal(
                    new OpenTK.Mathematics.Vector3(
                        source.RotationX,
                        source.RotationY,
                        source.RotationZ),
                    placement.Rotation);
                Assert.True(
                    unchecked((uint)source.Name) == placement.PropertyPositionHash ||
                    effects.Any(parent =>
                        unchecked((uint)parent.Name) == placement.PropertyPositionHash &&
                        parent.Children.Contains(source)));
                checkedPlacements++;
            }
            geometry.CloseStream();
        }

        Assert.True(checkedPlacements > 0, "The MGO2 corpus did not resolve a property placement.");
    }

    [Fact]
    [Trait("Category", "Corpus")]
    public void N012a_blast_drums_resolve_prop_pos_leaf_markers()
    {
        var corpusDirectory = GetCorpusDirectory();
        if (!Directory.Exists(corpusDirectory))
        {
            return;
        }

        var geometryPath = Directory
            .EnumerateFiles(corpusDirectory, "n012a.geom", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (geometryPath == null)
        {
            return;
        }
        var gcxPath = Path.Combine(Path.GetDirectoryName(geometryPath)!, "scenerio.gcx");
        Assert.True(File.Exists(gcxPath), "The n012a corpus folder is missing scenerio.gcx.");

        DictionaryFile.Load("dictionary.txt", "dictionary-aliases.txt");
        CommandFile.Load("commands.txt");
        var geometryBytes = new CryptoService().Decrypt(
            File.ReadAllBytes(geometryPath),
            BuildMgo2FolderKey(geometryPath));
        using var geometryStream = new MemoryStream(geometryBytes, writable: false);
        var geometry = new GeomFile(geometryStream, Endianness.Big);
        try
        {
            var gcxBytes = new CryptoService().Decrypt(
                File.ReadAllBytes(gcxPath),
                BuildMgo2FolderKey(gcxPath));
            using var gcxStream = new MemoryStream(gcxBytes, writable: false);
            var document = GcxFile.Read(gcxStream);
            var drumHash = HavenStudio.Utils.String.HashString("s01a_drum_a0_sk");
            var references = new GcxModelReferenceScanner()
                .Scan(document, geometry, isMgs3: false);
            var drums = references.PlacedModels
                .Where(placement =>
                    placement.ModelHash == drumHash &&
                    placement.PropertyPositionHash is 0x616F22u or 0x616F23u)
                .OrderBy(placement => placement.PropertyPositionHash)
                .ToArray();

            Assert.True(
                drums.Length == 2,
                $"Expected two n012a drums, found {drums.Length}. " +
                $"Model matches: {references.PlacedModels.Count(placement => placement.ModelHash == drumHash)}; " +
                "property matches: " +
                string.Join(", ", references.PlacedModels
                    .Where(placement => placement.PropertyPositionHash is 0x616F22u or 0x616F23u)
                    .Select(placement =>
                        $"0x{placement.ModelHash:X6}/0x{placement.PropertyPositionHash:X6}")) +
                "\nDecompiled commands:\n" + FindDrumCommands(document));
            Assert.Equal([0x616F22u, 0x616F23u], drums.Select(drum => drum.PropertyPositionHash!.Value));
            Assert.All(drums, drum =>
            {
                Assert.NotNull(drum.SourceEffect);
                Assert.Equal(
                    new OpenTK.Mathematics.Vector3(
                        drum.SourceEffect!.X,
                        drum.SourceEffect.Y,
                        drum.SourceEffect.Z),
                    drum.Position);
                Assert.Equal(drum.PropertyPositionHash, unchecked((uint)drum.SourceEffect.Name));
            });
        }
        finally
        {
            geometry.CloseStream();
        }

        static string FindDrumCommands(Gcx document)
        {
            var matches = new List<string>();
            Add(document.MainScript?.Bytes, "main");
            for (var index = 0; index < document.ScriptDefinitions.Count; index++)
            {
                Add(document.ScriptDefinitions[index].Script?.Bytes, $"proc{index + 1}");
            }
            return string.Join("\n---\n", matches.Take(4));

            void Add(byte[]? bytes, string name)
            {
                if (bytes == null || bytes.Length == 0)
                {
                    return;
                }
                var text = GcxDecompiler.Decompile(bytes, name, isMgs3: false);
                var index = text.IndexOf("NewBlastDrum", StringComparison.Ordinal);
                if (index < 0)
                {
                    return;
                }
                var start = Math.Max(0, index - 300);
                matches.Add($"{name}:\n{text[start..Math.Min(text.Length, index + 700)]}");
            }
        }
    }

    private static string GetCorpusDirectory()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable(CorpusDirectoryEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredDirectory)
            ? DefaultCorpusDirectory
            : Path.GetFullPath(configuredDirectory);
    }

    private static void RoundTripFile(
        string sourcePath,
        string outputPath,
        string decryptedOutputPath,
        string extension)
    {
        var crypto = new CryptoService();
        var folderKey = BuildMgo2FolderKey(sourcePath);
        var decrypted = crypto.Decrypt(File.ReadAllBytes(sourcePath), folderKey);

        using (var source = new MemoryStream(decrypted, writable: false))
        using (var output = OpenWrite(decryptedOutputPath))
        {
            RoundTrips[extension](source, output);
            output.Flush();
        }

        var rewrittenPlaintext = File.ReadAllBytes(decryptedOutputPath);
        File.WriteAllBytes(outputPath, crypto.Encrypt(rewrittenPlaintext, folderKey));
    }

    private static FileStream OpenWrite(string path)
    {
        return new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            ComparisonBufferSize,
            FileOptions.SequentialScan);
    }

    private static string BuildMgo2FolderKey(string filePath)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(filePath)!);
        var parentName = directory.Parent?.Name ?? string.Empty;
        return $"{parentName}/{directory.Name}";
    }

    private static void RoundTripDar(Stream source, Stream output)
    {
        var document = DarFile.Read(source, Endianness.Big);
        DarFile.Write(output, document, Endianness.Big);
    }

    private static void RoundTripDlz(Stream source, Stream output)
    {
        var document = new DlzFile(source, Endianness.Big);
        document.Save(output, Endianness.Big);
    }

    private static void RoundTripGcx(Stream source, Stream output)
    {
        var document = GcxFile.Read(source);
        GcxFile.Write(output, document);
    }

    private static void RoundTripGeom(Stream source, Stream output)
    {
        var document = new GeomFile(source, Endianness.Big);
        try
        {
            document.Save(output, Endianness.Big);
        }
        finally
        {
            document.CloseStream();
        }
    }

    private static void RoundTripMdn(Stream source, Stream output)
    {
        var document = MdnFile.Read(source);
        MdnFile.Write(output, document);
    }

    private static void RoundTripQar(Stream source, Stream output)
    {
        var document = QarFile.Read(source, Endianness.Big);
        QarFile.Write(output, document, Endianness.Big);
    }

    private static void RoundTripTxn(Stream source, Stream output)
    {
        var document = new TxnFile(source, Endianness.Big);
        document.Save(output, Endianness.Big);
    }

    private static ByteDifference? FindFirstDifference(string expectedPath, string actualPath)
    {
        using var expected = new FileStream(
            expectedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ComparisonBufferSize,
            FileOptions.SequentialScan);
        using var actual = new FileStream(
            actualPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ComparisonBufferSize,
            FileOptions.SequentialScan);

        var expectedBuffer = new byte[ComparisonBufferSize];
        var actualBuffer = new byte[ComparisonBufferSize];
        var commonLength = Math.Min(expected.Length, actual.Length);
        long offset = 0;

        while (offset < commonLength)
        {
            var count = (int)Math.Min(ComparisonBufferSize, commonLength - offset);
            expected.ReadExactly(expectedBuffer.AsSpan(0, count));
            actual.ReadExactly(actualBuffer.AsSpan(0, count));

            for (var index = 0; index < count; index++)
            {
                if (expectedBuffer[index] != actualBuffer[index])
                {
                    return new ByteDifference(
                        offset + index,
                        expected.Length,
                        actual.Length,
                        expectedBuffer[index],
                        actualBuffer[index]);
                }
            }

            offset += count;
        }

        if (expected.Length != actual.Length)
        {
            return new ByteDifference(
                commonLength,
                expected.Length,
                actual.Length,
                ExpectedByte: ReadByteOrNull(expected),
                ActualByte: ReadByteOrNull(actual));
        }

        return null;
    }

    private static byte? ReadByteOrNull(Stream stream)
    {
        var value = stream.ReadByte();
        return value >= 0 ? (byte)value : null;
    }

    private static void AddIssue(
        List<string> issues,
        IDictionary<string, int> reportedIssueCounts,
        string extension,
        string issue)
    {
        reportedIssueCounts.TryGetValue(extension, out var reportedCount);
        if (reportedCount < MaximumReportedIssuesPerFormat)
        {
            issues.Add(issue);
            reportedIssueCounts[extension] = reportedCount + 1;
        }
    }

    private static string BuildFailureMessage(
        int fileCount,
        int issueCount,
        IReadOnlyDictionary<string, FormatSummary> summaries,
        IReadOnlyCollection<string> issues)
    {
        var message = new StringBuilder();
        message.AppendLine($"MGO2 corpus round trip failed for {issueCount} of {fileCount} supported files.");
        message.AppendLine("Format summary (scanned / identical / mismatched / errors):");

        foreach (var pair in summaries.Where(pair => pair.Value.Scanned > 0).OrderBy(pair => pair.Key))
        {
            var summary = pair.Value;
            message.AppendLine(
                $"  {pair.Key}: {summary.Scanned} / {summary.Identical} / " +
                $"{summary.Mismatched} / {summary.Errors}");
        }

        message.AppendLine("Issues:");
        foreach (var issue in issues)
        {
            message.AppendLine($"  {issue}");
        }

        if (issueCount > issues.Count)
        {
            message.AppendLine($"  ... {issueCount - issues.Count} additional issues omitted.");
        }

        return message.ToString();
    }

    private sealed class FormatSummary
    {
        public int Scanned { get; set; }
        public int Identical { get; set; }
        public int Mismatched { get; set; }
        public int Errors { get; set; }
    }

    private sealed record ByteDifference(
        long Offset,
        long ExpectedLength,
        long ActualLength,
        byte? ExpectedByte,
        byte? ActualByte)
    {
        public override string ToString()
        {
            var expected = ExpectedByte.HasValue ? $"0x{ExpectedByte.Value:X2}" : "end of file";
            var actual = ActualByte.HasValue ? $"0x{ActualByte.Value:X2}" : "end of file";
            return $"first difference at 0x{Offset:X}: expected {expected}, actual {actual}; " +
                   $"lengths {ExpectedLength} and {ActualLength} bytes";
        }
    }
}
