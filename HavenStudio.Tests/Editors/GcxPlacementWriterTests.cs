using System.IO;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Formats.Gcx;
using HavenStudio.Utils;
using HavenStudio.Windows;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Editors;

public sealed class GcxPlacementWriterTests
{
    [Fact]
    public void Recorder_captures_command_span_and_int32_literal_sites()
    {
        var script = WrapProcedure(GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x123456u,
            ["x"] = 10,
            ["z"] = 30,
            ["y"] = 20
        }));
        var sites = new List<GcxPlacementSite>();

        GcxDecompiler.Decompile(script, "main", placementSites: sites);

        var site = Assert.Single(sites);
        Assert.Equal(0x07A516u, site.CommandHash);
        Assert.Equal(3, site.CommandOffset);
        Assert.Equal(script.Length - 4, site.CommandLength);
        Assert.Equal(0x123456u, site.ModelHash);
        Assert.Equal(0x123456u, site.Model!.Value);
        Assert.True(site.Editable);
        Assert.True(site.ModelHashEditable);
        Assert.Equal([10, 30, 20], site.Position!.Components.Select(component => component.Value));
        Assert.All(site.Position.Components, component =>
        {
            Assert.Equal(5, component.Width);
            Assert.Equal(GcxLiteralEncoding.Int32, component.Encoding);
        });
    }

    [Fact]
    public void Model_hash_writer_updates_the_direct_model_parameter_in_place()
    {
        var script = WrapProcedure(GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x123456u,
            ["x"] = 10,
            ["z"] = 30,
            ["y"] = 20
        }));

        var result = GcxPlacementWriter.WriteModelHash(
            script,
            RecordSingleSite(script),
            0x654321u);

        Assert.False(result.CommandResized);
        Assert.Equal(script.Length, result.Bytes.Length);
        Assert.Equal(0x654321u, RecordSingleSite(result.Bytes).ModelHash);
    }

    [Fact]
    public void Effect_hash_writer_updates_the_direct_effect_parameter_in_place()
    {
        var command = GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x123456u,
            ["eft"] = 0x112233u
        });
        // The command builder emits the numeric 0x09 form. Real scripts also use the
        // direct string-code 0x06 form, which is the size-preserving writable variant.
        var effectCode = Array.FindLastIndex(command, value => value == 0x09);
        Assert.True(effectCode >= 0);
        command[effectCode] = 0x06;
        var script = WrapProcedure(command);
        var site = RecordSingleSite(script);
        var effectSite = Assert.IsType<GcxStringCodeSite>(site.Effect);

        var result = GcxPlacementWriter.WriteEffectHash(script, site, 0x654321u);

        Assert.False(result.CommandResized);
        Assert.Equal((byte)0x21, result.Bytes[effectSite.ValueOffset]);
        Assert.Equal((byte)0x43, result.Bytes[effectSite.ValueOffset + 1]);
        Assert.Equal((byte)0x65, result.Bytes[effectSite.ValueOffset + 2]);
    }

    [Fact]
    public void Duplicate_writer_inserts_a_direct_placement_after_its_source()
    {
        var first = GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x111111u,
            ["x"] = 10,
            ["z"] = 30,
            ["y"] = 20
        });
        var second = GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x222222u,
            ["x"] = 40,
            ["z"] = 60,
            ["y"] = 50
        });
        var script = WrapProcedure(first, second);
        var sites = RecordSites(script);

        var result = GcxPlacementWriter.DuplicatePlacement(script, sites[0]);

        var rewrittenSites = RecordSites(result.Bytes);
        Assert.Equal([0x111111u, 0x111111u, 0x222222u], rewrittenSites.Select(site => site.ModelHash));
        Assert.Equal(result.Bytes.Length - 3, result.Bytes[1] | result.Bytes[2] << 8);
    }

    [Fact]
    public void Duplicate_writer_retargets_a_direct_effect_hash_only_in_the_copy()
    {
        var command = GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x111111u,
            ["eft"] = 0x222222u
        });
        command[Array.FindLastIndex(command, value => value == 0x09)] = 0x06;
        var script = WrapProcedure(command);
        var source = RecordSingleSite(script);

        var result = GcxPlacementWriter.DuplicatePlacement(
            script,
            source,
            transformSourceSite: source.Effect,
            replacementTransformHash: 0x222223u);

        Assert.Equal(
            [0x222222u, 0x222223u],
            RecordSites(result.Bytes).Select(site => site.EffectHash));
    }

    [Fact]
    public void Duplicate_writer_inserts_a_foreach_row_after_its_source_and_increments_repeat()
    {
        var script = WrapProcedure(BuildForeachNewPutObjectCommand(
            (0x111111u, 0xAAAAAAu),
            (0x222222u, 0xBBBBBBu)));
        var document = new Gcx { MainScript = new GcxScript(script) };
        var references = new GcxModelReferenceScanner().Scan(document, geometry: null, isMgs3: false);
        var source = references.PlacedModels[0];

        var result = GcxPlacementWriter.DuplicatePlacement(
            script,
            source.Binding!.Site,
            source.Binding.ForeachRowIndex);

        var rewrittenSites = RecordSites(result.Bytes);
        var rewrittenSite = Assert.Single(rewrittenSites);
        Assert.Equal(3, rewrittenSite.ForeachRowCount);
        Assert.Equal(3, rewrittenSite.Foreach!.Repeat.Value);
        var rewrittenDocument = new Gcx { MainScript = new GcxScript(result.Bytes) };
        var rewrittenReferences = new GcxModelReferenceScanner().Scan(
            rewrittenDocument,
            geometry: null,
            isMgs3: false);
        Assert.Equal(
            [0x111111u, 0x111111u, 0x222222u],
            rewrittenReferences.PlacedModels.Select(placement => placement.ModelHash));
        Assert.Equal(
            [0xAAAAAAu, 0xAAAAAAu, 0xBBBBBBu],
            rewrittenReferences.PlacedModels.Select(placement => placement.CollisionReferenceHash));
        Assert.Equal([0, 1, 2], rewrittenReferences.PlacedModels
            .Select(placement => placement.Binding!.ForeachRowIndex));
        Assert.Equal(result.Bytes.Length - 3, result.Bytes[1] | result.Bytes[2] << 8);
    }

    [Fact]
    public void New_test_tree_foreach_records_collision_and_retargets_the_copied_property_marker()
    {
        var script = WrapProcedure(BuildForeachNewTestTreeCommand(
            (0x111111u, 0x222221u, 0x333331u),
            (0x111112u, 0x222222u, 0x333332u)));
        var document = new Gcx { MainScript = new GcxScript(script) };
        var references = new GcxModelReferenceScanner().Scan(
            document,
            geometry: null,
            isMgs3: false);
        Assert.True(
            references.PlacedModels.Count > 0,
            GcxDecompiler.Decompile(script, "main"));
        var source = references.PlacedModels[0];

        Assert.Equal(0x333331u, source.CollisionReferenceHash);
        Assert.Equal(0x222221u, source.PropertyPositionHash);
        Assert.Equal(0x333331u, source.Binding!.CollisionReferenceSite!.Value);
        Assert.Equal(0x222221u, source.Binding.TransformSourceSite!.Value);

        var collisionEdit = GcxPlacementWriter.WriteCollisionReference(
            script,
            source.Binding.CollisionReferenceSite,
            0x333333u);
        var collisionEdited = new GcxModelReferenceScanner().Scan(
            new Gcx { MainScript = new GcxScript(collisionEdit.Bytes) },
            geometry: null,
            isMgs3: false);
        Assert.Equal(0x333333u, collisionEdited.PlacedModels[0].CollisionReferenceHash);

        var result = GcxPlacementWriter.DuplicatePlacement(
            script,
            source.Binding.Site,
            source.Binding.ForeachRowIndex,
            source.Binding.TransformSourceSite,
            0x222223u);
        var rewritten = new GcxModelReferenceScanner().Scan(
            new Gcx { MainScript = new GcxScript(result.Bytes) },
            geometry: null,
            isMgs3: false);

        Assert.Equal(
            [0x222221u, 0x222223u, 0x222222u],
            rewritten.PlacedModels.Select(placement => placement.PropertyPositionHash));
        Assert.Equal(
            [0x333331u, 0x333331u, 0x333332u],
            rewritten.PlacedModels.Select(placement => placement.CollisionReferenceHash));
    }

    [Fact]
    public void Writer_patches_same_width_literals_in_script_xyz_order()
    {
        var script = WrapProcedure(BuildPackedPositionCommand(1, 2, 3));
        var site = RecordSingleSite(script);

        var result = GcxPlacementWriter.WritePosition(script, site, new Vector3(10, 20, 30));

        Assert.False(result.CommandResized);
        Assert.Equal(script.Length, result.Bytes.Length);
        var rewritten = RecordSingleSite(result.Bytes);
        Assert.Equal([10, 30, 20], rewritten.Position!.Components.Select(component => component.Value));
        Assert.All(rewritten.Position.Components, component =>
            Assert.Equal(GcxLiteralEncoding.PackedNumber, component.Encoding));
    }

    [Fact]
    public void Writer_reencodes_command_when_a_value_does_not_fit_recorded_width()
    {
        var script = WrapProcedure(BuildPackedPositionCommand(1, 2, 3));
        var site = RecordSingleSite(script);

        var result = GcxPlacementWriter.WritePosition(script, site, new Vector3(100, 200, 300));

        Assert.True(result.CommandResized);
        Assert.True(result.Bytes.Length > script.Length);
        Assert.Equal(result.Bytes.Length - 3, result.Bytes[1] | result.Bytes[2] << 8);
        var rewritten = RecordSingleSite(result.Bytes);
        Assert.Equal([100, 300, 200], rewritten.Position!.Components.Select(component => component.Value));
        Assert.All(rewritten.Position.Components, component =>
            Assert.Equal(GcxLiteralEncoding.Int32, component.Encoding));
    }

    [Fact]
    public void Collision_reference_writer_patches_an_existing_hash_in_place()
    {
        var script = WrapProcedure(GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x123456u,
            ["collision"] = 0x111111u
        }));
        var site = RecordSingleSite(script);

        var result = GcxPlacementWriter.WriteCollisionReference(script, site, 0xABCDEFu);

        Assert.False(result.CommandResized);
        Assert.Equal(script.Length, result.Bytes.Length);
        var rewritten = RecordSingleSite(result.Bytes);
        Assert.Equal(0xABCDEFu, rewritten.CollisionReferenceHash);
        Assert.Equal(0xABCDEFu, rewritten.CollisionReference!.Value);
    }

    [Fact]
    public void Collision_reference_writer_can_patch_but_not_remove_a_nested_literal_site()
    {
        var script = WrapProcedure(GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x123456u,
            ["collision"] = 0x111111u
        }));
        var recorded = RecordSingleSite(script);
        var nested = new GcxPlacementSite
        {
            CommandHash = recorded.CommandHash,
            CommandName = recorded.CommandName,
            CommandOffset = recorded.CommandOffset,
            CommandLength = recorded.CommandLength,
            CollisionReference = recorded.CollisionReference,
            CollisionReferenceHash = recorded.CollisionReferenceHash,
            CollisionReferenceEditable = true,
            IsNested = true
        };

        var result = GcxPlacementWriter.WriteCollisionReference(script, nested, 0x982F38u);

        Assert.False(result.CommandResized);
        Assert.Equal(0x982F38u, RecordSingleSite(result.Bytes).CollisionReferenceHash);
        Assert.Contains(
            "cannot be removed safely",
            Assert.Throws<InvalidOperationException>(() =>
                GcxPlacementWriter.WriteCollisionReference(result.Bytes, nested, null)).Message);
    }

    [Fact]
    public void Collision_reference_writer_adds_and_removes_the_ref_parameter()
    {
        var script = WrapProcedure(GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x123456u
        }));

        var added = GcxPlacementWriter.WriteCollisionReference(
            script,
            RecordSingleSite(script),
            0x654321u);

        Assert.True(added.CommandResized);
        Assert.Equal(0x654321u, RecordSingleSite(added.Bytes).CollisionReferenceHash);
        Assert.Equal(added.Bytes.Length - 3, added.Bytes[1] | added.Bytes[2] << 8);

        var removed = GcxPlacementWriter.WriteCollisionReference(
            added.Bytes,
            RecordSingleSite(added.Bytes),
            null);

        Assert.True(removed.CommandResized);
        Assert.Null(RecordSingleSite(removed.Bytes).CollisionReferenceHash);
        Assert.Equal(script, removed.Bytes);
    }

    [Fact]
    public void Recorder_captures_int16_literal_widths()
    {
        var script = WrapProcedure(BuildInt16PositionCommand(-1000, 2000, -3000));

        var site = RecordSingleSite(script);

        Assert.Equal([-1000, 2000, -3000], site.Position!.Components.Select(component => component.Value));
        Assert.All(site.Position.Components, component =>
        {
            Assert.Equal(3, component.Width);
            Assert.Equal(GcxLiteralEncoding.Int16, component.Encoding);
        });
    }

    [Fact]
    public void Recorder_marks_nested_placements_read_only()
    {
        var inner = BuildPackedPositionCommand(1, 2, 3);
        var nestedPayload = new byte[4 + inner.Length];
        nestedPayload[0] = (byte)'x';
        nestedPayload[1] = 0x01;
        nestedPayload[2] = 0x02;
        nestedPayload[3] = 0x03;
        inner.CopyTo(nestedPayload, 4);
        var parameter = GcxCommandBuilder.WrapTaggedPayload(0x50, nestedPayload);
        var outerPayload = new List<byte>
        {
            0xA7, 0x92, 0x65,
            0x04,
            0x06, 0x2D, 0x2B, 0x54
        };
        outerPayload.AddRange(parameter);
        outerPayload.Add(0x00);
        var script = WrapProcedure(GcxCommandBuilder.WrapTaggedPayload(0x60, outerPayload.ToArray()));

        var site = RecordSingleSite(script);

        Assert.True(site.IsNested);
        Assert.False(site.Editable);
        Assert.Contains("read-only", site.ReadOnlyReason);
    }

    [Fact]
    public void Scanner_cross_checks_recorded_xyz_values_and_binds_the_source_site()
    {
        var script = WrapProcedure(GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x123456u,
            ["x"] = 10,
            ["z"] = 30,
            ["y"] = 20
        }));
        var document = new Gcx { MainScript = new GcxScript(script) };

        var references = new GcxModelReferenceScanner().Scan(document, geometry: null, isMgs3: false);

        Assert.True(references.PlacedModels.Count == 1, GcxDecompiler.Decompile(script, "main"));
        var placement = references.PlacedModels[0];
        Assert.Equal(new Vector3(10, 20, 30), placement.Position);
        Assert.NotNull(placement.Binding);
        Assert.Equal(
            [10, 30, 20],
            placement.Binding!.Site.Position!.Components.Select(component => component.Value));
    }

    [Fact]
    public void Scanner_decodes_script_xyz_game_angles_to_renderer_radians()
    {
        var references = new GcxModelReferenceScanner().ScanDecompiledScripts(
        [
            """
            NewPutObject \
                -model placed_model \
                -dir 16384 4096 -32768
            """
        ]);

        var rotation = Assert.Single(references.PlacedModels).Rotation!.Value;
        Assert.Equal(MathF.PI / 2f, rotation.X, 5);
        Assert.Equal(MathF.PI / 8f, rotation.Y, 5);
        Assert.Equal(-MathF.PI, rotation.Z, 5);
    }

    [Fact]
    public void Scanner_resolves_foreach_model_arguments_without_treating_data_as_stage_models()
    {
        const string decompiled = """
            command foreach \
                -argc 4 \
                -repeat 2 \
                -data tree_1 model_a collision_a prop_a tree_2 model_b collision_b prop_b \
                -exec proc {
                    chara NewTestTree_02 $arg1 \
                        -model $arg2 \
                        -collision $arg3 \
                        -prop_pos $arg4
                }
            """;

        var references = new GcxModelReferenceScanner().ScanDecompiledScripts([decompiled]);

        Assert.Equal(
            [
                HavenStudio.Utils.String.HashString("model_a"),
                HavenStudio.Utils.String.HashString("model_b")
            ],
            references.PlacedModels.Select(placement => placement.ModelHash));
        Assert.Empty(references.StageModelHashes);
        Assert.All(references.PlacedModels, placement => Assert.NotNull(placement.PropertyPositionHash));
    }

    [Fact]
    public void Scanner_resolves_only_stage_model_argument_from_stage_foreach_data()
    {
        const string decompiled = """
            command foreach \
                -argc 2 \
                -repeat 2 \
                -data stage_1 stage_model_a stage_2 stage_model_b \
                -exec proc {
                    chara NewPutStageModelSet $arg1 \
                        -model $arg2
                }
            """;

        var references = new GcxModelReferenceScanner().ScanDecompiledScripts([decompiled]);

        Assert.True(references.StageModelHashes.SetEquals(
        [
            HavenStudio.Utils.String.HashString("stage_model_a"),
            HavenStudio.Utils.String.HashString("stage_model_b")
        ]));
        Assert.Empty(references.PlacedModels);
    }

    [Fact]
    public void Writer_patches_a_foreach_data_model_site()
    {
        var script = GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [0x06, 0x11, 0x22, 0x33, 0x00]);
        var valueOffset = Array.IndexOf(script, (byte)0x11);
        var modelSite = new GcxStringCodeSite(
            ParameterOffset: 0,
            ParameterLength: script.Length,
            ValueOffset: valueOffset,
            Value: 0x332211);

        var result = GcxPlacementWriter.WriteModelHash(script, modelSite, 0xA1B2C3);

        Assert.False(result.CommandResized);
        Assert.Equal(0xC3, result.Bytes[valueOffset]);
        Assert.Equal(0xB2, result.Bytes[valueOffset + 1]);
        Assert.Equal(0xA1, result.Bytes[valueOffset + 2]);
    }

    [Fact]
    public void Scanner_reads_and_binds_collision_reference_hashes()
    {
        var script = WrapProcedure(GcxCommandBuilder.BuildNewPutObject(new Dictionary<string, object>
        {
            ["model"] = 0x123456u,
            ["collision"] = 0x654321u
        }));
        var document = new Gcx { MainScript = new GcxScript(script) };

        var placement = Assert.Single(
            new GcxModelReferenceScanner().Scan(document, geometry: null, isMgs3: false).PlacedModels);

        Assert.Equal(0x654321u, placement.CollisionReferenceHash);
        Assert.True(placement.Binding!.Site.CollisionReferenceEditable);
    }

    [Fact]
    public void Scanner_uses_audited_model_placement_commands_data_driven()
    {
        const string decompiled = """
            chara NewSky sky_instance \
                -model sky_model \
                -pos 10 30 20
            chara NewPutStageModelSet stage_instance \
                -model prop_model \
                -eft prop_anchor
            """;

        var references = new GcxModelReferenceScanner().ScanDecompiledScripts([decompiled]);

        Assert.Equal(2, references.PlacedModels.Count);
        Assert.Contains(references.PlacedModels, placement =>
            placement.ModelHash == HavenStudio.Utils.String.HashString("sky_model") &&
            placement.Position == new Vector3(10, 20, 30));
        Assert.Contains(references.PlacedModels, placement =>
            placement.ModelHash == HavenStudio.Utils.String.HashString("prop_model") &&
            placement.EffectHash == HavenStudio.Utils.String.HashString("prop_anchor"));
        Assert.DoesNotContain(HavenStudio.Utils.String.HashString("prop_model"), references.StageModelHashes);
    }

    [Fact]
    public void Add_object_dialog_builds_new_put_object_for_selected_proc_and_world_position()
    {
        var viewModel = new InsertCommandDialogViewModel();
        viewModel.ConfigureNewPutObject(
            0x123456,
            new Vector3(10, 20, 30),
            ["main", "proc1"],
            "proc1");

        var command = viewModel.BuildCommand();
        var script = WrapProcedure(command);
        var references = new GcxModelReferenceScanner().Scan(
            new Gcx { MainScript = new GcxScript(script) },
            geometry: null,
            isMgs3: false);

        Assert.Equal("proc1", viewModel.SelectedTargetProcedure);
        Assert.True(references.PlacedModels.Count == 1, GcxDecompiler.Decompile(script, "main"));
        var placement = references.PlacedModels[0];
        Assert.Equal(0x123456u, placement.ModelHash);
        Assert.Equal(new Vector3(10, 20, 30), placement.Position);
        Assert.True(placement.Binding!.Site.Editable);
    }

    [Fact]
    public void Procedure_size_update_rejects_silent_size_clamping()
    {
        Assert.Throws<InvalidDataException>(() =>
            GcxScriptEditor.UpdateProcSize(CreateProcedureBytes(258, 0x8D)));
        Assert.Throws<InvalidDataException>(() =>
            GcxScriptEditor.UpdateProcSize(CreateProcedureBytes(ushort.MaxValue + 4, 0x8E)));
    }

    private static GcxPlacementSite RecordSingleSite(byte[] script)
    {
        var sites = new List<GcxPlacementSite>();
        GcxDecompiler.Decompile(script, "main", placementSites: sites);
        return Assert.Single(sites);
    }

    private static List<GcxPlacementSite> RecordSites(byte[] script)
    {
        var sites = new List<GcxPlacementSite>();
        GcxDecompiler.Decompile(script, "main", placementSites: sites);
        return sites;
    }

    private static byte[] WrapProcedure(params byte[][] commands)
    {
        var commandLength = commands.Sum(command => command.Length);
        var script = new byte[commandLength + 4];
        script[0] = 0x8E;
        var bodyLength = commandLength + 1;
        script[1] = (byte)(bodyLength & 0xFF);
        script[2] = (byte)((bodyLength >> 8) & 0xFF);
        var offset = 3;
        foreach (var command in commands)
        {
            command.CopyTo(script, offset);
            offset += command.Length;
        }
        script[^1] = 0x00;
        return script;
    }

    private static byte[] BuildForeachNewPutObjectCommand(
        params (uint ModelHash, uint CollisionHash)[] rows)
    {
        var nestedPayload = new List<byte>();
        AddHash(nestedPayload, 0x6592A7); // chara
        nestedPayload.Add(0x08);
        nestedPayload.Add(0x06);
        AddHash(nestedPayload, 0x07A516); // NewPutObject
        nestedPayload.Add(0x06);
        AddHash(nestedPayload, 0xAFB954); // instance name
        nestedPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [(byte)'m', 0x13, 0x1D, 0x09, 0x41])); // -model $arg1
        nestedPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [(byte)'r', 0x36, 0x9B, 0x84, 0x42])); // -ref $arg2
        nestedPayload.Add(0x00);
        var nestedCommand = GcxCommandBuilder.WrapTaggedPayload(0x60, nestedPayload.ToArray());
        var execProc = GcxCommandBuilder.WrapTaggedPayload(0x80, nestedCommand);

        var outerPayload = new List<byte>();
        AddHash(outerPayload, 0x082BC9); // command
        outerPayload.Add(0x04);
        outerPayload.Add(0x06);
        AddHash(outerPayload, 0x542B2D); // foreach
        outerPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [(byte)'a', 0x43, 0x55, 0x32, EncodePacked(2)])); // -argc 2
        outerPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [(byte)'r', 0x7E, 0xA1, 0x89, EncodePacked(rows.Length)])); // -repeat N
        var dataPayload = new List<byte> { (byte)'d', 0xE1, 0x92, 0x33 };
        foreach (var row in rows)
        {
            dataPayload.Add(0x06);
            AddHash(dataPayload, row.ModelHash);
            dataPayload.Add(0x06);
            AddHash(dataPayload, row.CollisionHash);
        }
        outerPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(0x50, dataPayload.ToArray()));
        var execPayload = new List<byte> { (byte)'e', 0x03, 0x6D, 0x34 };
        execPayload.AddRange(execProc);
        outerPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(0x50, execPayload.ToArray()));
        outerPayload.Add(0x00);
        return GcxCommandBuilder.WrapTaggedPayload(0x60, outerPayload.ToArray());
    }

    private static byte[] BuildForeachNewTestTreeCommand(
        params (uint ModelHash, uint PropertyHash, uint CollisionHash)[] rows)
    {
        var nestedPayload = new List<byte>();
        AddHash(nestedPayload, 0x6592A7); // chara
        nestedPayload.Add(0x05);
        nestedPayload.Add(0x06);
        AddHash(nestedPayload, 0x656B68); // NewTestTree_02
        nestedPayload.Add(0x41); // instance $arg1
        nestedPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [(byte)'m', 0x13, 0x1D, 0x09, 0x42])); // -model $arg2
        nestedPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [(byte)'p', 0xB8, 0xEB, 0x34, 0x43])); // -prop_pos $arg3
        nestedPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [(byte)'c', 0x2D, 0xC6, 0x31, 0x44])); // -collision $arg4
        nestedPayload.Add(0x00);
        var nestedCommand = GcxCommandBuilder.WrapTaggedPayload(0x60, nestedPayload.ToArray());
        var execProc = GcxCommandBuilder.WrapTaggedPayload(0x80, nestedCommand);

        var outerPayload = new List<byte>();
        AddHash(outerPayload, 0x082BC9); // command
        outerPayload.Add(0x04);
        outerPayload.Add(0x06);
        AddHash(outerPayload, 0x542B2D); // foreach
        outerPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [(byte)'a', 0x43, 0x55, 0x32, EncodePacked(4)])); // -argc 4
        outerPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(
            0x50,
            [(byte)'r', 0x7E, 0xA1, 0x89, EncodePacked(rows.Length)])); // -repeat N
        var dataPayload = new List<byte> { (byte)'d', 0xE1, 0x92, 0x33 };
        for (var index = 0; index < rows.Length; index++)
        {
            dataPayload.Add(0x06);
            AddHash(dataPayload, (uint)(0x440000 + index));
            dataPayload.Add(0x06);
            AddHash(dataPayload, rows[index].ModelHash);
            dataPayload.Add(0x06);
            AddHash(dataPayload, rows[index].PropertyHash);
            dataPayload.Add(0x06);
            AddHash(dataPayload, rows[index].CollisionHash);
        }
        outerPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(0x50, dataPayload.ToArray()));
        var execPayload = new List<byte> { (byte)'e', 0x03, 0x6D, 0x34 };
        execPayload.AddRange(execProc);
        outerPayload.AddRange(GcxCommandBuilder.WrapTaggedPayload(0x50, execPayload.ToArray()));
        outerPayload.Add(0x00);
        return GcxCommandBuilder.WrapTaggedPayload(0x60, outerPayload.ToArray());
    }

    private static void AddHash(ICollection<byte> bytes, uint hash)
    {
        bytes.Add((byte)hash);
        bytes.Add((byte)(hash >> 8));
        bytes.Add((byte)(hash >> 16));
    }

    private static byte[] BuildPackedPositionCommand(int x, int z, int y)
    {
        return
        [
            0x6D, 0x1E,
            0xA7, 0x92, 0x65, 0x08,
            0x06, 0x16, 0xA5, 0x07,
            0x06, 0x54, 0xB9, 0xAF,
            0x58, 0x6D, 0x13, 0x1D, 0x09, 0x06, 0x56, 0x34, 0x12,
            0x57, 0x70, 0x53, 0xCE, 0x01,
            EncodePacked(x), EncodePacked(z), EncodePacked(y),
            0x00
        ];
    }

    private static byte[] BuildInt16PositionCommand(short x, short z, short y)
    {
        var result = new List<byte>
        {
            0x6D, 0x25,
            0xA7, 0x92, 0x65, 0x08,
            0x06, 0x16, 0xA5, 0x07,
            0x06, 0x54, 0xB9, 0xAF,
            0x58, 0x6D, 0x13, 0x1D, 0x09, 0x06, 0x56, 0x34, 0x12,
            0x5D, 0x0D, 0x70, 0x53, 0xCE, 0x01
        };
        AddInt16(result, x);
        AddInt16(result, z);
        AddInt16(result, y);
        result.Add(0x00);
        return result.ToArray();
    }

    private static void AddInt16(ICollection<byte> bytes, short value)
    {
        bytes.Add(0x01);
        bytes.Add((byte)(value & 0xFF));
        bytes.Add((byte)((value >> 8) & 0xFF));
    }

    private static byte EncodePacked(int value)
    {
        Assert.InRange(value, -1, 62);
        return (byte)(0xC0 | value + 1);
    }

    private static byte[] CreateProcedureBytes(int length, byte header)
    {
        var bytes = new byte[length];
        bytes[0] = header;
        return bytes;
    }
}
