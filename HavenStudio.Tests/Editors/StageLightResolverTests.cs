using HavenStudio.Editors.Lighting;
using HavenStudio.Extensions;
using HavenStudio.Formats.Lit;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Editors;

public sealed class StageLightResolverTests
{
    [Fact]
    public async Task Resolver_ignores_preview_and_maps_the_dominant_stage_asset_family()
    {
        using var temp = new TempDirectory();
        WriteLit(temp.GetPath("MGS4_Preview.lt3"), groupCount: 0, lightsPerGroup: 0);
        WriteLit(temp.GetPath("s01a10a.lt3"), groupCount: 2, lightsPerGroup: 3);
        WriteLit(temp.GetPath("z99a99a.lt3"), groupCount: 12, lightsPerGroup: 12);
        for (var index = 0; index < 48; index++)
        {
            await File.WriteAllBytesAsync(temp.GetPath($"s01a_asset_{index:D2}.mdn"), [1]);
        }
        for (var index = 0; index < 2; index++)
        {
            await File.WriteAllBytesAsync(temp.GetPath($"z99a_asset_{index:D2}.mdn"), [1]);
        }

        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await workspace.ScanAsync();
        var documents = snapshot.WithExtension(".lt3")
            .Select(file => LitDocumentSession.Load(workspace, file.Path))
            .ToList();

        var selection = StageLightResolver.Resolve(workspace, documents, "n021a");

        Assert.NotNull(selection.Primary);
        Assert.Equal("s01a10a.lt3", selection.Primary!.DisplayName);
        Assert.Contains("matching assets", selection.Reason);
        Assert.False(selection.Primary.DisplayName.Contains("preview", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Resolver_prefers_an_exact_stage_name_over_a_larger_unrelated_file()
    {
        using var temp = new TempDirectory();
        WriteLit(temp.GetPath("n012a.lt3"), groupCount: 1, lightsPerGroup: 1);
        WriteLit(temp.GetPath("s03a10a.lt3"), groupCount: 30, lightsPerGroup: 20);

        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await workspace.ScanAsync();
        var documents = snapshot.WithExtension(".lt3")
            .Select(file => LitDocumentSession.Load(workspace, file.Path))
            .ToList();

        var selection = StageLightResolver.Resolve(workspace, documents, "n012a");

        Assert.Equal("n012a.lt3", selection.Primary!.DisplayName);
        Assert.Contains("exact stage name", selection.Reason);
    }

    [Fact]
    public async Task Resolver_uses_structural_content_and_size_only_as_final_tie_breakers()
    {
        using var temp = new TempDirectory();
        WriteLit(temp.GetPath("alpha.lt3"), groupCount: 1, lightsPerGroup: 1);
        WriteLit(temp.GetPath("beta.lt3"), groupCount: 3, lightsPerGroup: 4);

        var workspace = new WorkspaceCatalog(temp.Path, Endianness.Big);
        var snapshot = await workspace.ScanAsync();
        var documents = snapshot.WithExtension(".lt3")
            .Select(file => LitDocumentSession.Load(workspace, file.Path))
            .ToList();

        var selection = StageLightResolver.Resolve(workspace, documents, "unmatched_stage");

        Assert.Equal("beta.lt3", selection.Primary!.DisplayName);
        Assert.Contains("3 groups / 12 lights", selection.Reason);
    }

    [Theory]
    [InlineData("s01a10a", "s01a")]
    [InlineData("s01a13a_west", "s01a")]
    [InlineData("n021a", "n021a")]
    [InlineData("mo_st01_d", "mo")]
    public void Asset_family_extraction_is_deterministic(string stem, string expected)
    {
        Assert.Equal(expected, StageLightResolver.ExtractAssetFamily(stem));
    }

    private static void WriteLit(string path, int groupCount, int lightsPerGroup)
    {
        var file = new LitFile
        {
            Variant = LitVariant.Raw,
            BigEndian = true,
            Direction = new Vector4(0, -1, 0, 0),
            Color = new LitColor(180, 180, 180, 0),
            Ambient = new LitColor(64, 64, 64, 0)
        };

        for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            var group = new LitGroup
            {
                Type = 1,
                BoundsMin = new Vector4(-100, -100, -100, 1),
                BoundsMax = new Vector4(100, 100, 100, 1)
            };
            for (var lightIndex = 0; lightIndex < lightsPerGroup; lightIndex++)
            {
                group.Lights.Add(new LitPointLight
                {
                    Point = new Vector4(lightIndex, groupIndex, 0, 1),
                    Color = new LitColor(255, 255, 255, 0),
                    Range = 100,
                    ExtendedRange = 200
                });
            }
            file.Groups.Add(group);
        }

        using var stream = File.Create(path);
        file.Write(stream);
    }
}
