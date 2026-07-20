using System.Linq;
using HavenStudio.Formats.Geo;
using HavenStudio.Services;
using HavenStudio.Utils;

namespace HavenStudio.Tests.Services;

public sealed class MinimapSpawnsTests
{
    [Fact]
    public void Build_projects_positions_using_the_game_calibration()
    {
        var system = Container("SYSTEM",
            Spawn("PRP_STAGE_SIZE_01", -37000, -117000),
            Spawn("PRP_STAGE_SIZE_02", 78000, -9000),
            Spawn("PRP_STAGE_CENTER", 20500, -63000));
        var rule = Container("RULE",
            Spawn("PRP_CENTER_SPAWN", 20500, -63000),
            Spawn("PRP_CORNER_SPAWN", -37000, -117000));

        var projection = new MinimapProjection(0.060, 0.060, -17500, 63000, 8192, 8192);
        var root = MinimapSpawns.Build(new[] { system, rule }, 256, 256, projection);

        var center = root.Spawns.Single(s => s.WorldX == 20500 && s.WorldZ == -63000);
        Assert.Equal(133.625 / 256.0, center.U, 3);
        Assert.Equal(0.5, center.V, 3);

        var corner = root.Spawns.Single(s => s.WorldX == -37000);
        Assert.Equal(25.8125 / 256.0, corner.U, 3);
        Assert.Equal(26.75 / 256.0, corner.V, 3);
    }

    [Fact]
    public void Build_groups_by_hierarchy_and_excludes_stage_markers()
    {
        var system = Container("SYSTEM",
            Spawn("PRP_STAGE_SIZE_01", -37000, -117000),
            Spawn("PRP_STAGE_SIZE_02", 78000, -9000),
            Spawn("PRP_STAGE_CENTER", 20500, -63000));
        var cbox = Container("CBOX",
            Spawn("PRP_CBOX", 0, -60000),
            Spawn("PRP_CBOX", 10000, -50000));
        var item = Container("ITEM",
            Container("CLAYMORE",
                Spawn("PRP_CLAYMORE_01", 5000, -70000)));

        var root = MinimapSpawns.Build(new[] { system, cbox, item }, 256, 256);

        // Two spawns from CBOX + one from ITEM/CLAYMORE; stage markers excluded.
        Assert.Equal(3, root.SpawnCount);

        // SYSTEM has only stage markers, so it drops out; CBOX and ITEM remain.
        var labels = root.Children.Select(GroupLabel).ToList();
        Assert.Contains("CBOX", labels);
        Assert.Contains("ITEM", labels);
        Assert.DoesNotContain("SYSTEM", labels);

        var itemGroup = root.Children.Single(c => GroupLabel(c) == "ITEM");
        Assert.Equal(1, itemGroup.SpawnCount);
        var claymore = Assert.Single(itemGroup.Children);
        Assert.Equal(1, claymore.SpawnCount);
    }

    [Fact]
    public void Build_falls_back_to_the_spawn_bounding_box_without_stage_markers()
    {
        var group = Container("RULE",
            Spawn("PRP_A", 0, 0),
            Spawn("PRP_B", 100, 200));

        var root = MinimapSpawns.Build(new[] { group }, 256, 256);

        // With a 5% margin the extreme corners sit just inside 0..1.
        var a = root.Spawns.Single(s => s.WorldX == 0);
        var b = root.Spawns.Single(s => s.WorldX == 100);
        Assert.True(a.U > 0 && a.U < b.U && b.U < 1);
        Assert.True(a.V > 0 && a.V < b.V && b.V < 1);
    }

    [Fact]
    public void Build_uses_the_game_scale_and_offsets_on_a_wide_map()
    {
        var system = Container("SYSTEM",
            Spawn("PRP_STAGE_SIZE_01", -100000, -145000),
            Spawn("PRP_STAGE_SIZE_02", 120000, -25000),
            Spawn("PRP_STAGE_CENTER", 10000, -95000));
        var rule = Container("RULE",
            Spawn("PRP_BOUNDS_MIDPOINT", 10000, -85000),
            Spawn("PRP_GAMEPLAY_CENTER", 10000, -95000));

        var projection = new MinimapProjection(0.068, 0.068, 7000, 88500, 16384, 8192);
        var root = MinimapSpawns.Build(new[] { system, rule }, 512, 256, projection);

        var midpoint = root.Spawns.Single(spawn => spawn.WorldZ == -85000);
        Assert.Equal(292.125 / 512.0, midpoint.U, 3);
        Assert.Equal(135.4375 / 256.0, midpoint.V, 3);

        var gameplayCenter = root.Spawns.Single(spawn => spawn.WorldZ == -95000);
        Assert.Equal(292.125 / 512.0, gameplayCenter.U, 3);
        Assert.Equal(114.1875 / 256.0, gameplayCenter.V, 3);
    }

    private static string GroupLabel(SpawnGroup group) => group.Label;

    private static GeoEffect Container(string name, params GeoEffect[] children)
    {
        var effect = new GeoEffect { Name = (int)Register(name), Index = 0 };
        effect.Children.AddRange(children);
        return effect;
    }

    private static GeoEffect Spawn(string name, float x, float z) => new()
    {
        Name = (int)Register(name),
        Index = 0x0002,
        X = x,
        Y = 0,
        Z = z,
        W = 1
    };

    private static uint Register(string name)
    {
        var hash = HavenStudio.Utils.String.HashString(name);
        DictionaryFile.Lookup[hash] = name;
        return hash;
    }
}
