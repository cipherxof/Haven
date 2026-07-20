using HavenStudio.Extensions;
using HavenStudio.Formats.Dld;
using HavenStudio.Formats.Txn;
using HavenStudio.Services;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;
using HavenStudio.Utils;

namespace HavenStudio.Tests.Services;

public sealed class MinimapServiceTests
{
    private const uint TriId = 0x00777001;
    private const uint GridTexId = 0x00515000;
    private const uint MapTexId = 0x00515001;

    [Fact]
    public async Task Load_decodes_map_levels_and_skips_the_grid()
    {
        DictionaryFile.Lookup[GridTexId] = "map_test_grid_alp";
        DictionaryFile.Lookup[MapTexId] = "map_n012a_alp";

        using var temp = new TempDirectory();
        var root = temp.GetPath("workspace");
        Directory.CreateDirectory(root);
        WriteOnlineMap(Path.Combine(root, "online_map.txn"));
        WriteDld(Path.Combine(root, "online_map.dld"));

        var catalog = new WorkspaceCatalog(root, Endianness.Big);
        await catalog.ScanAsync();

        var textures = MinimapService.Load(catalog);

        var level = Assert.Single(textures);
        Assert.Equal("map_n012a_alp", level.Label);
        Assert.Equal(4, level.Width);
        Assert.Equal(4, level.Height);
        Assert.Equal(4 * 4 * 4, level.Rgba.Length);
        var projection = Assert.IsType<MinimapProjection>(level.Projection);
        Assert.Equal(0.068, projection.ScaleX, 3);
        Assert.Equal(7000, projection.OffsetX);
        Assert.Equal(88500, projection.OffsetZ);
        Assert.Equal(16384, projection.CanvasWidth);
        Assert.DoesNotContain(textures, texture => texture.Label.Contains("grid"));
    }

    [Fact]
    public async Task Load_returns_empty_when_no_online_map_is_present()
    {
        using var temp = new TempDirectory();
        var root = temp.GetPath("workspace");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "unrelated.txt"), new byte[] { 1 });

        var catalog = new WorkspaceCatalog(root, Endianness.Big);
        await catalog.ScanAsync();

        Assert.Empty(MinimapService.Load(catalog));
    }

    [Theory]
    [InlineData("sm_ll", 0.110, 0.050, -8000.0, 0.0)]
    [InlineData("n020a", 0.070, 0.100, -4000.0, 104000.0)]
    [InlineData("sm_dd", 0.070, 0.070, -23000.0, -11500.0)]
    [InlineData("n022a", 0.175, 0.0675, -17000.0, -20000.0)]
    [InlineData("n024a", 0.0575, 0.0725, -9000.0, -18000.0)]
    public async Task Load_prefers_the_workspace_stage_for_custom_map_calibration(
        string stage,
        double scaleX,
        double scaleZ,
        double offsetX,
        double offsetZ)
    {
        DictionaryFile.Lookup[GridTexId] = "map_n012a_grid_alp";
        DictionaryFile.Lookup[MapTexId] = "map_n012a_alp";

        using var temp = new TempDirectory();
        var root = temp.GetPath(stage);
        Directory.CreateDirectory(root);
        WriteOnlineMap(Path.Combine(root, "online_map.txn"));
        WriteDld(Path.Combine(root, "online_map.dld"));

        var catalog = new WorkspaceCatalog(root, Endianness.Big);
        await catalog.ScanAsync();

        var level = Assert.Single(MinimapService.Load(catalog));
        var projection = Assert.IsType<MinimapProjection>(level.Projection);
        Assert.Equal(scaleX, projection.ScaleX, 4);
        Assert.Equal(scaleZ, projection.ScaleZ, 4);
        Assert.Equal(offsetX, projection.OffsetX);
        Assert.Equal(offsetZ, projection.OffsetZ);
    }

    private static void WriteOnlineMap(string path)
    {
        var txn = new TxnFile();
        txn.Images.Add(new TxnImage(4, 4, 9, 1, 0, 0));
        txn.Images.Add(new TxnImage(4, 4, 9, 1, 0, 0));

        var gridInfo = new TxnInfo(GridTexId, TriId, 4, 4, 0, 0, 0, 1f, 1f, 0f, 0f);
        var mapInfo = new TxnInfo(MapTexId, TriId, 4, 4, 0, 0, 0, 1f, 1f, 0f, 0f);
        txn.ImageInfo.Add(gridInfo);
        txn.IndexLookup[gridInfo] = 0;
        txn.ImageInfo.Add(mapInfo);
        txn.IndexLookup[mapInfo] = 1;

        using var stream = File.Create(path);
        txn.Save(stream, Endianness.Big);
    }

    private static void WriteDld(string path)
    {
        var dld = new DldFile();
        dld.Textures.Add(new DldTexture(0x91, DldPriority.Main, TriId, 8, 8, 1, 0, new byte[8]));
        dld.Textures.Add(new DldTexture(0x91, DldPriority.Main, TriId, 8, 8, 1, 1, new byte[8]));

        using var stream = File.Create(path);
        dld.Save(stream, Endianness.Big);
    }
}
