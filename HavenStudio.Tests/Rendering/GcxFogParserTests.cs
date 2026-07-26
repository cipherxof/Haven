using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Rendering;

public sealed class GcxFogParserTests
{
    [Fact]
    public void Parses_named_new_fog_set_with_exact_engine_conversions()
    {
        const string script = """
            NewFogSet \
                -near 125 \
                -far 8125 \
                -rgb 1000 500 0 \
                -viewport -1 \
                -limit 100 900 \
                -before_near 25 \
                -before_far 7025 \
                -before_rgb 250 750 1000 \
                -before_limit 0 1000
            """;

        var settings = Assert.IsType<SceneFogSettings>(GcxFogParser.Parse([script]));
        var fog = settings[0];

        Assert.Equal(125f, fog.Near);
        Assert.Equal(8125f, fog.Far);
        Assert.Equal(255f / 255f, fog.Color.X, 6);
        Assert.Equal(127f / 255f, fog.Color.Y, 6); // engine truncates 127.5
        Assert.Equal(0f, fog.Color.Z, 6);
        Assert.Equal(0.1f, fog.LimitMin, 6);
        Assert.Equal(0.9f, fog.LimitMax, 6);
        Assert.Equal(25f, fog.BeforeNear);
        Assert.Equal(7025f, fog.BeforeFar);
        Assert.Equal(63f / 255f, fog.BeforeColor.X, 6);
        Assert.Equal(191f / 255f, fog.BeforeColor.Y, 6);
        Assert.Equal(1f, fog.BeforeColor.Z, 6);
        Assert.Equal(settings[0], settings[1]);
        Assert.Equal(settings[0], settings[2]);
    }

    [Fact]
    public void Parses_hashed_parameters_and_targets_one_viewport()
    {
        const string script = """
            [DDE914] \
                -[38A092] 200 \
                -[01A492] 4200 \
                -[01D542] 0 1000 0 \
                -[AD95C5] 2 \
                -[F6419A] 250 750
            """;

        var settings = Assert.IsType<SceneFogSettings>(GcxFogParser.Parse([script]));

        Assert.Equal(Mgs4FogState.Default, settings[0]);
        Assert.Equal(Mgs4FogState.Default, settings[1]);
        Assert.Equal(200f, settings[2].Near);
        Assert.Equal(4200f, settings[2].Far);
        Assert.Equal(new Vector4(0f, 1f, 0f, 1f), settings[2].Color);
        Assert.Equal(0.25f, settings[2].LimitMin, 6);
        Assert.Equal(0.75f, settings[2].LimitMax, 6);
    }
    [Fact]
    public void Parses_new_fog_set_directly_from_hashed_bytecode()
    {
        // Real scenerio.gcx bytecode. The command is hashed and therefore must
        // not depend on dictionary/decompiler text.
        var bytes = Convert.FromHexString(
            "8DD56D5BC92B08040614E9DD556E92A038C1596692A40109305705005D0D7242D501015002014F03015402586C9A41F6C1017301576277373201E803596249712909A08601005D0D62F9A12901E80301E80301100358623A1E29C101F401006D62C92B080406D6E7715D0D64929D010106FF019FFC014FFE5D0D63583E690191040183040132035D0D63EFE8CD0120030116030130025868A77DD40168FFDF5D0F686271D9017D0101900102F0012C015D1068E3347801C30101260201C60101BC02006D11C92B0804068C8191577242D501C1C1C10000");
        var document = new HavenStudio.Formats.Gcx.Gcx
        {
            MainScript = new HavenStudio.Formats.Gcx.GcxScript(bytes)
        };

        var settings = Assert.IsType<SceneFogSettings>(GcxFogBytecodeParser.Parse(document));
        Assert.True(settings.TryGetViewport(0, out var fog));
        Assert.Equal(0f, fog.Near);
        Assert.Equal(350000f, fog.Far);
        Assert.Equal(150f / 255f, fog.Color.X, 6);
        Assert.Equal(215f / 255f, fog.Color.Y, 6);
        Assert.Equal(151f / 255f, fog.Color.Z, 6);
        Assert.Equal(0f, fog.LimitMin, 6);
        Assert.Equal(0.371f, fog.LimitMax, 6);
        Assert.Equal(1000f, fog.BeforeNear);
        Assert.Equal(100000f, fog.BeforeFar);
        Assert.Equal(0.5f, fog.BeforeLimitMax, 6);
    }

    [Fact]
    public void Viewport_specific_fog_does_not_configure_haven_viewport_zero()
    {
        const string script = """
            [DDE914] \
                -[38A092] 200 \
                -[01A492] 4200 \
                -[AD95C5] 2
            """;

        var settings = Assert.IsType<SceneFogSettings>(GcxFogParser.Parse([script]));
        Assert.False(settings.IsConfigured(0));
        Assert.True(settings.IsConfigured(2));
    }

}
