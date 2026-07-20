using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Rendering;

public sealed class GcxSystemLightParserTests
{
    [Fact]
    public void Parses_system_direction_character_color_and_hemisphere_floor()
    {
        const string script = """
            command NewSystemLightSet \
                -dir -250 -865 -433 \
                -color 1169 1155 818 \
                -chara_color 800 790 560 \
                -hemispherelight_rot -152 30 \
                -hemispherelight_frontcolor 381 400 240 300 \
                -hemispherelight_backcolor 451 550 454 700
            """;

        var lighting = Assert.IsType<SceneLightSettings>(GcxSystemLightParser.Parse([script]));

        Assert.Equal(Vector3.Normalize(new Vector3(-250, -865, -433)), lighting.Direction);
        Assert.Equal(new Vector3(0.8f, 0.79f, 0.56f), lighting.DirectionalColor);
        var ambient = Assert.IsType<Vector3>(lighting.AmbientColor);
        Assert.Equal(0.43f, ambient.X, 5);
        Assert.Equal(0.505f, ambient.Y, 5);
        Assert.Equal(0.3898f, ambient.Z, 5);
    }

    [Fact]
    public void Leaves_ambient_unspecified_when_hemisphere_colors_are_absent()
    {
        const string script = "command NewSystemLightSet -dir 0 -1000 0 -color 1000 1000 1000";

        var lighting = Assert.IsType<SceneLightSettings>(GcxSystemLightParser.Parse([script]));

        Assert.Null(lighting.AmbientColor);
    }
}
