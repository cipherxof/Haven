using HavenStudio.Editors;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Formats.Gcx;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Editors;

public sealed class GcxSdmAreaScannerTests
{
    [Fact]
    public void Resolves_hashed_parameters_and_wrapper_arguments()
    {
        var scripts = new Dictionary<string, string>
        {
            ["proc57"] = """
                proc proc57 {
                  -p[34BAF7] $arg1
                  -a[4466DE] 4
                  -a[4465E8] $arg2
                  -a[905C0D] 150
                  -a[0B8224] 180
                  -a[8CF52D] 200
                  -a[5A6764] 0
                }
                """,
            ["proc56"] = "@proc57 [60961A] 150"
        };

        var area = Assert.Single(GcxSdmAreaScanner.Scan(scripts));

        Assert.Equal("proc57", area.DefinitionScript);
        Assert.Equal("proc56", area.CallerScript);
        Assert.Equal(0x0060961Au, area.PropertyDirectoryHash);
        Assert.Equal(4, area.AreaMinimum);
        Assert.Equal(150, area.AreaMaximum);
        Assert.Equal(150_000f, area.StartingRadius);
        Assert.Equal(4_000f, area.MinimumRadius);
        Assert.Equal(150, area.AreaTime);
        Assert.Equal(180, area.AreaTimer);
        Assert.Equal(200, area.AreaMove);
        Assert.Equal(0, area.AreaChange);
    }

    [Fact]
    public void Accepts_dictionary_resolved_parameter_names()
    {
        var scripts = new Dictionary<string, string>
        {
            ["proc55"] = """
                -prop_dir [60961A]
                -area_min 4
                -area_max 130
                -area_time 150
                -area_timer 180
                -area_move 200
                -area_change 0
                """
        };

        var area = Assert.Single(GcxSdmAreaScanner.Scan(scripts));

        Assert.Equal(130, area.AreaMaximum);
        Assert.Equal(130_000f, area.StartingRadius);
        Assert.Equal("proc55", area.CallerScript);
    }

    [Fact]
    public void Rejects_invalid_or_unresolved_areas()
    {
        var scripts = new Dictionary<string, string>
        {
            ["invalid"] = "-prop_dir [60961A] -area_min 9 -area_max 4",
            ["uncalled"] = "-prop_dir $arg1 -area_min 4 -area_max $arg2"
        };

        Assert.Empty(GcxSdmAreaScanner.Scan(scripts));
    }

    [Fact]
    public void Entity_builds_start_and_final_boundaries_at_the_stage_center()
    {
        var source = new GcxSdmAreaReference("proc57", "proc56", 0x60961A, 4, 150, 150, 180, 200, 0);
        var center = new Vector3(-64149.445f, 10305.827f, -252659.36f);

        var entity = new SdmAreaEntity(source, center);

        Assert.Equal(2, entity.Models.Count);
        Assert.All(entity.Models, model => Assert.Equal(center, model.Position));
        Assert.Contains(entity.Models[0].Positions.Where((_, index) => index % 3 == 0),
            x => MathF.Abs(x) >= 149_000f);
        Assert.Equal("SDM area", entity.DisplayName);
    }

    [Fact]
    public void Writer_changes_only_the_JJ_wrapper_radius_literal_when_width_fits()
    {
        var original = Convert.FromHexString("8B793900061A966002960000");

        var updated = GcxSdmAreaWriter.Write(original, 0x60961A, 150, 75);

        Assert.Equal(original.Length, updated.Length);
        var changed = Enumerable.Range(0, original.Length)
            .Where(index => original[index] != updated[index])
            .ToArray();
        Assert.Equal([9], changed);
        Assert.Equal(0x4B, updated[9]);
        Assert.Contains("@proc57 [60961A] 75", GcxDecompiler.Decompile(updated, "proc56"));
        Assert.Equal(150, original[9]);
    }

    [Fact]
    public void Writer_promotes_the_literal_and_repairs_the_procedure_length()
    {
        var original = Convert.FromHexString("8B793900061A966002960000");

        var updated = GcxSdmAreaWriter.Write(original, 0x60961A, 150, 300);

        Assert.Contains("@proc57 [60961A] 300", GcxDecompiler.Decompile(updated, "proc56"));
        Assert.Equal(updated, GcxSdmAreaWriter.Write(updated, 0x60961A, 300, 300));
    }

    [Fact]
    public void Writer_rejects_stale_and_ambiguous_sites_without_mutating_input()
    {
        var original = Convert.FromHexString("8B793900061A966002960000");
        var duplicate = original.Concat(original).ToArray();

        Assert.Throws<InvalidDataException>(() =>
            GcxSdmAreaWriter.Write(original, 0x60961A, 149, 75));
        Assert.Throws<InvalidDataException>(() =>
            GcxSdmAreaWriter.Write(duplicate, 0x60961A, 150, 75));
        Assert.Equal(Convert.FromHexString("8B793900061A966002960000"), original);
    }

    [Fact]
    public void Entity_edit_updates_the_overlay_immediately()
    {
        var source = new GcxSdmAreaReference("proc57", "proc56", 0x60961A, 4, 150, 150, 180, 200, 0);
        SdmAreaEntity? entity = null;
        entity = new SdmAreaEntity(source, Vector3.Zero, (target, value) =>
        {
            target.ApplyAreaMaximum(value);
            return null;
        });

        entity.AreaMaximum = 75;

        Assert.Equal(75, entity.Source.AreaMaximum);
        Assert.Contains(entity.Models[0].Positions.Where((_, index) => index % 3 == 0),
            x => MathF.Abs(x) is >= 74_000f and <= 76_000f);
    }
}
