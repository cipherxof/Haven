using HavenStudio.Editors.GcxEditing;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dld;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Geo;
using HavenStudio.Formats.Mdn;
using HavenStudio.Formats.Txn;
using HavenStudio.Services;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Editors;

public sealed class GcxEditingTests
{
    [Fact]
    public async Task Document_session_loads_and_saves_physical_documents_through_streams()
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath("script.gcx");
        WriteGcx(path, CreateGcx());
        var session = new GcxDocumentSession();

        Assert.True(await session.LoadAsync(WorkspacePath.Physical(path)));
        Assert.Equal(123, session.Document!.Timestamp);
        Assert.False(session.IsDirty);

        session.Document.Timestamp = 456;
        session.MarkDirty();
        Assert.True(session.IsDirty);
        Assert.True(await session.SaveAsync());
        Assert.False(session.IsDirty);

        using var stream = File.OpenRead(path);
        Assert.Equal(456, GcxFile.Read(stream).Timestamp);
    }

    [Fact]
    public async Task Script_editor_inserts_commands_and_updates_procedure_headers()
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath("script.gcx");
        WriteGcx(path, CreateGcx());
        var session = new GcxDocumentSession();
        await session.LoadAsync(WorkspacePath.Physical(path));
        var editor = new GcxScriptEditor(session);
        var node = new GcxScriptNode("main", session.Document!.MainScript);
        editor.Select(node);

        Assert.True(editor.InsertCommandBytes([0xAA, 0xBB], insertAtStart: true));

        Assert.Equal([0x8E, 0x03, 0x00, 0xAA, 0xBB, 0xFF], node.Script!.Bytes);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public async Task Script_editor_adds_procedures_to_the_document_session()
    {
        using var temp = new TempDirectory();
        var path = temp.GetPath("script.gcx");
        WriteGcx(path, CreateGcx());
        var session = new GcxDocumentSession();
        await session.LoadAsync(WorkspacePath.Physical(path));
        var editor = new GcxScriptEditor(session);

        var added = editor.AddProcedure();

        Assert.NotNull(added);
        Assert.Equal("proc2", added.Name);
        Assert.Equal([0x8E, 0x01, 0x00, 0x00], added.Script!.Bytes);
        Assert.Equal(2, session.Document!.ScriptDefinitions.Count);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void Search_service_moves_across_scripts_and_wraps()
    {
        var first = new GcxScriptNode("first", new GcxScript([0x01]));
        var second = new GcxScriptNode("second", new GcxScript([0x02]));
        var texts = new Dictionary<string, string>
        {
            ["first"] = "alpha one",
            ["second"] = "alpha two"
        };
        var search = new GcxSearchService();

        var firstMatch = search.FindNext("alpha", [first, second], node => texts[node.Name]);
        var secondMatch = search.FindNext("alpha", [first, second], node => texts[node.Name]);
        var wrappedMatch = search.FindNext("alpha", [first, second], node => texts[node.Name]);

        Assert.Same(first, firstMatch!.Node);
        Assert.Same(second, secondMatch!.Node);
        Assert.Same(first, wrappedMatch!.Node);
    }

    [Fact]
    public void Validation_service_reports_only_mismatched_procedure_sizes()
    {
        var document = new Gcx { MainScript = new GcxScript([0x8E, 0x01, 0x00, 0xFF]) };
        document.ScriptDefinitions.Add(new GcxScriptDefinition(0, 0)
        {
            Script = new GcxScript([0x8D, 0x05, 0xFF])
        });

        var errors = new GcxValidationService().GetProcedureSizeErrors(document);

        var error = Assert.Single(errors);
        Assert.Contains("proc1", error);
        Assert.Contains("declared size 5", error);
    }

    [Fact]
    public void Model_reference_scanner_extracts_stage_and_placed_models_from_decompiled_text()
    {
        const string decompiled = """
            NewPutStageModelSet \
                -model stage_model
            NewPutObject \
                -model placed_model
                -pos 10 30 20
                -dir 16384 0 -32768
                -model_glass glass_model
            command foreach \
                -argc 2
                -data unused effect_anchor
                -exec proc {
                    NewPutObject \
                        -model foreach_model
                        -prop_pos $arg2
                }
            next_command \
            """;
        var scanner = new GcxModelReferenceScanner();

        var references = scanner.ScanDecompiledScripts([decompiled]);

        Assert.Contains(StringHash("stage_model"), references.StageModelHashes);
        Assert.Equal(2, references.PlacedModels.Count);
        var placed = references.PlacedModels.Single(model => model.ModelHash == StringHash("placed_model"));
        Assert.Equal(new Vector3(10, 20, 30), placed.Position);
        Assert.Equal(MathF.PI / 2f, placed.Rotation!.Value.X, 5);
        Assert.Equal(0f, placed.Rotation.Value.Y, 5);
        Assert.Equal(-MathF.PI, placed.Rotation.Value.Z, 5);
        Assert.Contains(StringHash("glass_model"), placed.AdditionalModelHashes);
        var foreachModel = references.PlacedModels.Single(model => model.ModelHash == StringHash("foreach_model"));
        Assert.Equal(StringHash("effect_anchor"), foreachModel.PropertyPositionHash);
    }

    [Fact]
    public void Model_reference_scanner_uses_the_prop_effect_operand_without_crossing_lines()
    {
        const string decompiled = """
            command foreach \
                -argc 2 \
                -data object_a effect_a \
                -exec proc {
                    chara [07E1F9] $arg1 \
                        -prop property_group $arg2 \
                        -model car_model \
                        -b[30BDB7] car_collision
                }
            chara [4D1F6D] object_b \
                -model door_model \
                -prop effect_b \
                -flag 131104
            """;
        var scanner = new GcxModelReferenceScanner();

        var references = scanner.ScanDecompiledScripts([decompiled]);

        Assert.Equal(2, references.PlacedModels.Count);
        Assert.Equal(
            StringHash("effect_a"),
            references.PlacedModels.Single(placement =>
                placement.ModelHash == StringHash("car_model")).PropertyPositionHash);
        Assert.Equal(
            StringHash("car_collision"),
            references.PlacedModels.Single(placement =>
                placement.ModelHash == StringHash("car_model")).CollisionReferenceHash);
        Assert.Equal(
            StringHash("effect_b"),
            references.PlacedModels.Single(placement =>
                placement.ModelHash == StringHash("door_model")).PropertyPositionHash);
    }

    [Fact]
    public void Model_reference_scanner_places_prop_pos_leaf_markers_at_the_marker()
    {
        const string decompiled = """
            command foreach \
                -argc 2 \
                -repeat 2 \
                -data NewBlastDrum_GCL[1] [616F22] NewBlastDrum_GCL[2] [616F23] \
                -exec proc {
                    chara NewBlastDrum_GCL $arg1 \
                        -model s01a_drum_a0_sk \
                        -light n012a \
                        -life 1 \
                        -b[88DBE4] [9EBB66] \
                        -prop_pos $arg2 \
                        -p[A0BE78] 75 \
                        -amb_scale 850 927 834 \
                        -coltolsc 1000
                }
            """;
        var first = new GeoEffect
        {
            Name = 0x616F22,
            X = 100,
            Y = 200,
            Z = 300,
            RotationY = 0.5f
        };
        var second = new GeoEffect
        {
            Name = 0x616F23,
            X = -400,
            Y = 500,
            Z = -600,
            RotationZ = -0.25f
        };

        var references = new GcxModelReferenceScanner().ScanDecompiledScriptsWithEffects(
            [decompiled],
            [first, second]);

        Assert.Equal(2, references.PlacedModels.Count);
        Assert.All(references.PlacedModels, placement =>
            Assert.Equal(StringHash("s01a_drum_a0_sk"), placement.ModelHash));
        Assert.Collection(
            references.PlacedModels,
            placement =>
            {
                Assert.Equal(0x616F22u, placement.PropertyPositionHash);
                Assert.Equal(new Vector3(100, 200, 300), placement.Position);
                Assert.Equal(new Vector3(0, 0.5f, 0), placement.Rotation);
                Assert.Same(first, placement.SourceEffect);
            },
            placement =>
            {
                Assert.Equal(0x616F23u, placement.PropertyPositionHash);
                Assert.Equal(new Vector3(-400, 500, -600), placement.Position);
                Assert.Equal(new Vector3(0, 0, -0.25f), placement.Rotation);
                Assert.Same(second, placement.SourceEffect);
            });
    }

    [Fact]
    public void Model_reference_scanner_expands_hashed_foreach_commands()
    {
        const string decompiled = """
            [082BC9] [542B2D] \
                -a[325543] 2 \
                -r[89A17E] 2 \
                -d[3392E1] NewBlastDrum_GCL[1] [616F22] NewBlastDrum_GCL[2] [616F23] \
                -e[346D03] proc {
                    [6592A7] NewBlastDrum_GCL $arg1 \
                        -model [4290F5] \
                        -l[F6297A] [F8CAA7] \
                        -l[37B125] 1 \
                        -b[88DBE4] [9EBB66] \
                        -p[34EBB8] $arg2 \
                        -p[A0BE78] 95 \
                        -a[C7932A] 850 927 834 \
                        -c[7C7362] 1000
                }
            """;
        var first = new GeoEffect { Name = 0x616F22, X = 10, Y = 20, Z = 30 };
        var second = new GeoEffect { Name = 0x616F23, X = 40, Y = 50, Z = 60 };

        var references = new GcxModelReferenceScanner().ScanDecompiledScriptsWithEffects(
            [decompiled],
            [first, second]);

        Assert.Collection(
            references.PlacedModels,
            placement =>
            {
                Assert.Equal(0x4290F5u, placement.ModelHash);
                Assert.Equal(0x616F22u, placement.PropertyPositionHash);
                Assert.Equal(new Vector3(10, 20, 30), placement.Position);
                Assert.Same(first, placement.SourceEffect);
            },
            placement =>
            {
                Assert.Equal(0x4290F5u, placement.ModelHash);
                Assert.Equal(0x616F23u, placement.PropertyPositionHash);
                Assert.Equal(new Vector3(40, 50, 60), placement.Position);
                Assert.Same(second, placement.SourceEffect);
            });
    }

    [Fact]
    public void Model_reference_scanner_places_foreach_eft_arguments()
    {
        const string decompiled = """
            command foreach \
                -argc 2 \
                -repeat 2 \
                -data [20B3F8][1] [305F02] [20B3F8][2] [305F19] \
                -exec proc {
                    chara NewPutObject $arg1 \
                        -model [D0A6D2] \
                        -flag 65536 \
                        -eft $arg2 \
                        -coltolsc 1000 \
                        -amb_scale 549 700 540
                }
            """;
        var first = new GeoEffect { Name = 0x305F02, X = -10, Y = -20, Z = -30 };
        var second = new GeoEffect { Name = 0x305F19, X = 70, Y = 80, Z = 90 };

        var references = new GcxModelReferenceScanner().ScanDecompiledScriptsWithEffects(
            [decompiled],
            [first, second]);

        Assert.Collection(
            references.PlacedModels,
            placement =>
            {
                Assert.Equal(0xD0A6D2u, placement.ModelHash);
                Assert.Equal(0x305F02u, placement.EffectHash);
                Assert.Equal(new Vector3(-10, -20, -30), placement.Position);
                Assert.Same(first, placement.SourceEffect);
            },
            placement =>
            {
                Assert.Equal(0xD0A6D2u, placement.ModelHash);
                Assert.Equal(0x305F19u, placement.EffectHash);
                Assert.Equal(new Vector3(70, 80, 90), placement.Position);
                Assert.Same(second, placement.SourceEffect);
            });
    }

    [Fact]
    public void Model_reference_scanner_gives_eft_precedence_over_pos_and_dir()
    {
        const string decompiled = """
            chara NewPutObject test_object \
                -model test_model \
                -pos 100 200 300 \
                -dir 1000 2000 3000 \
                -eft effect_anchor
            """;
        var effect = new GeoEffect
        {
            Name = unchecked((int)StringHash("effect_anchor")),
            X = -10,
            Y = -20,
            Z = -30,
            RotationY = 0.75f
        };

        var references = new GcxModelReferenceScanner().ScanDecompiledScriptsWithEffects(
            [decompiled],
            [effect]);

        var placement = Assert.Single(references.PlacedModels);
        Assert.Equal(StringHash("effect_anchor"), placement.EffectHash);
        Assert.Equal(new Vector3(-10, -20, -30), placement.Position);
        Assert.Equal(new Vector3(0, 0.75f, 0), placement.Rotation);
        Assert.Same(effect, placement.SourceEffect);
    }

    [Fact]
    public void Model_reference_scanner_resolves_later_foreach_property_arguments()
    {
        const string decompiled = """
            command foreach \
                -argc 4 \
                -repeat 1 \
                -data [D67686][1] s02a_tree_c1 tree_marker COL_s02a_tree_c1 \
                -exec proc {
                    chara NewTestTree_02 $arg1 \
                        -model $arg2 \
                        -prop_pos $arg3 \
                        -collision $arg4
                }
            command foreach \
                -argc 4 \
                -repeat 1 \
                -data [69AF8C][1] [331302] [F1588D] [B0D8A9] \
                -exec proc {
                    chara NewBlastDrum_GCL $arg1 \
                        -model n002a_drum_b0 \
                        -prop_pos $arg4
                }
            """;
        var treeMarker = new GeoEffect
        {
            Name = unchecked((int)StringHash("tree_marker")),
            X = 11,
            Y = 22,
            Z = 33
        };
        var drumMarker = new GeoEffect { Name = 0xB0D8A9, X = 44, Y = 55, Z = 66 };

        var references = new GcxModelReferenceScanner().ScanDecompiledScriptsWithEffects(
            [decompiled],
            [treeMarker, drumMarker]);

        var tree = references.PlacedModels.Single(placement =>
            placement.ModelHash == StringHash("s02a_tree_c1"));
        Assert.Equal(StringHash("tree_marker"), tree.PropertyPositionHash);
        Assert.Equal(StringHash("COL_s02a_tree_c1"), tree.CollisionReferenceHash);
        Assert.Equal(new Vector3(11, 22, 33), tree.Position);

        var drum = references.PlacedModels.Single(placement =>
            placement.ModelHash == StringHash("n002a_drum_b0"));
        Assert.Equal(0xB0D8A9u, drum.PropertyPositionHash);
        Assert.Equal(new Vector3(44, 55, 66), drum.Position);
    }

    [Fact]
    public async Task Project_model_loader_prepares_then_publishes_one_atomic_batch()
    {
        using var temp = new TempDirectory();
        var modelPath = temp.GetPath("stage_model.mdn");
        WriteMinimalMdn(modelPath);
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await workspace.ScanAsync();
        var references = new GcxModelReferences(
            new HashSet<uint> { StringHash("stage_model") },
            Array.Empty<PlacedModelReference>());
        using var loader = new ProjectModelLoader();
        IReadOnlyList<HavenStudio.Rendering.MdnSceneBatch>? published = null;

        var status = await loader.LoadAsync(
            references,
            workspace,
            snapshot,
            (batches, _) =>
            {
                published = batches;
                return Task.CompletedTask;
            });

        Assert.Equal(ProjectModelLoadStatus.Completed, status);
        Assert.Single(published!);
        Assert.Null(published![0].Placement);
    }

    [Fact]
    public async Task Project_model_loader_does_not_publish_cancelled_work()
    {
        using var temp = new TempDirectory();
        var modelPath = temp.GetPath("stage_model.mdn");
        WriteMinimalMdn(modelPath);
        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await workspace.ScanAsync();
        var references = new GcxModelReferences(
            new HashSet<uint> { StringHash("stage_model") },
            Array.Empty<PlacedModelReference>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var loader = new ProjectModelLoader();
        var published = false;

        var status = await loader.LoadAsync(
            references,
            workspace,
            snapshot,
            (_, _) =>
            {
                published = true;
                return Task.CompletedTask;
            },
            cancellationToken: cancellation.Token);

        Assert.Equal(ProjectModelLoadStatus.Cancelled, status);
        Assert.False(published);
    }

    [Fact]
    public async Task Project_model_loader_caches_repeated_model_reads_within_a_load()
    {
        using var temp = new TempDirectory();
        var modelPath = temp.GetPath("placed_model.mdn");
        WriteMinimalMdn(modelPath);
        var innerWorkspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await innerWorkspace.ScanAsync();
        var workspace = new CountingWorkspaceCatalog(innerWorkspace);
        var placed = new PlacedModelReference { ModelHash = StringHash("placed_model") };
        var references = new GcxModelReferences(
            new HashSet<uint>(),
            [placed, new PlacedModelReference { ModelHash = placed.ModelHash }]);
        using var loader = new ProjectModelLoader();
        IReadOnlyList<HavenStudio.Rendering.MdnSceneBatch>? published = null;

        var status = await loader.LoadAsync(
            references,
            workspace,
            snapshot,
            (batches, _) =>
            {
                published = batches;
                return Task.CompletedTask;
            });

        Assert.Equal(ProjectModelLoadStatus.Completed, status);
        Assert.Equal(1, workspace.OpenReadCount);
        Assert.Equal(2, published!.Count);
        Assert.Same(placed, published[0].Placement);
        Assert.NotSame(published[0].Placement, published[1].Placement);
    }

    [Fact]
    public async Task Project_model_loader_reuses_texture_containers_across_stage_models()
    {
        using var temp = new TempDirectory();
        WriteMinimalTexturedMdn(temp.GetPath("stage_model_a.mdn"), 0x12345678);
        WriteMinimalTexturedMdn(temp.GetPath("stage_model_b.mdn"), 0x12345678);
        using (var stream = File.Create(temp.GetPath("textures.dld")))
        {
            new DldFile().Save(stream, Endianness.Big);
        }
        using (var stream = File.Create(temp.GetPath("textures.txn")))
        {
            new TxnFile().Save(stream, Endianness.Big);
        }

        var innerWorkspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await innerWorkspace.ScanAsync();
        var workspace = new CountingWorkspaceCatalog(innerWorkspace);
        var references = new GcxModelReferences(
            new HashSet<uint>
            {
                StringHash("stage_model_a"),
                StringHash("stage_model_b")
            },
            Array.Empty<PlacedModelReference>());
        using var loader = new ProjectModelLoader();

        var status = await loader.LoadAsync(
            references,
            workspace,
            snapshot,
            (_, _) => Task.CompletedTask);

        Assert.Equal(ProjectModelLoadStatus.Completed, status);
        Assert.Equal(2, workspace.GetOpenReadCount(".mdn"));
        Assert.Equal(1, workspace.GetOpenReadCount(".dld"));
        Assert.Equal(1, workspace.GetOpenReadCount(".txn"));
    }

    private static Gcx CreateGcx()
    {
        var document = new Gcx
        {
            Timestamp = 123,
            MainScript = new GcxScript([0x8E, 0x01, 0x00, 0xFF])
        };
        document.ScriptDefinitions.Add(new GcxScriptDefinition(0, 0)
        {
            Script = new GcxScript([0x8D, 0x01, 0xFF])
        });
        return document;
    }

    private static void WriteGcx(string path, Gcx document)
    {
        using var stream = File.Create(path);
        GcxFile.Write(stream, document);
    }

    private static void WriteMinimalMdn(string path)
    {
        var document = new Mdn
        {
            Bounds = new MdnBounds
            {
                MaxX = 1,
                MaxY = 1,
                MaxZ = 1,
                MaxW = 1,
                MinX = -1,
                MinY = -1,
                MinZ = -1,
                MinW = 1
            }
        };
        using var stream = File.Create(path);
        MdnFile.Write(stream, document, Endianness.Big);
    }

    private static void WriteMinimalTexturedMdn(string path, int textureHash)
    {
        var document = new Mdn
        {
            Bounds = new MdnBounds
            {
                MaxX = 1,
                MaxY = 1,
                MaxZ = 1,
                MaxW = 1,
                MinX = -1,
                MinY = -1,
                MinZ = -1,
                MinW = 1
            }
        };
        document.Textures.Add(new MdnTexture { NameHash = textureHash });
        using var stream = File.Create(path);
        MdnFile.Write(stream, document, Endianness.Big);
    }

    private static uint StringHash(string value) => HavenStudio.Utils.String.HashString(value);

    private sealed class CountingWorkspaceCatalog : IWorkspaceCatalog
    {
        private readonly IWorkspaceCatalog _inner;
        private readonly Dictionary<string, int> _openReadCounts =
            new(StringComparer.OrdinalIgnoreCase);

        public CountingWorkspaceCatalog(IWorkspaceCatalog inner)
        {
            _inner = inner;
        }

        public int OpenReadCount { get; private set; }
        public string RootPath => _inner.RootPath;
        public Endianness Endianness => _inner.Endianness;
        public WorkspaceSnapshot? Snapshot => _inner.Snapshot;

        public Task<WorkspaceSnapshot> ScanAsync(
            CancellationToken cancellationToken = default,
            IProgress<WorkspaceScanProgress>? progress = null) =>
            _inner.ScanAsync(cancellationToken, progress);

        public Stream OpenRead(WorkspacePath path)
        {
            OpenReadCount++;
            _openReadCounts[path.Extension] = GetOpenReadCount(path.Extension) + 1;
            return _inner.OpenRead(path);
        }

        public int GetOpenReadCount(string extension) =>
            _openReadCounts.GetValueOrDefault(extension);

        public byte[] ReadAllBytes(WorkspacePath path) => _inner.ReadAllBytes(path);
        public void Replace(WorkspacePath path, ReadOnlySpan<byte> data) => _inner.Replace(path, data);
        public ArchiveDumpService.ExtractSummary ExtractArchive(WorkspacePath archivePath, string outputFolder) =>
            _inner.ExtractArchive(archivePath, outputFolder);
    }
}
