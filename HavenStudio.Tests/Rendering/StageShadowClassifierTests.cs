using Avalonia3DControl.Core.Models;
using HavenStudio.Rendering;

namespace HavenStudio.Tests.Rendering;

public sealed class StageShadowClassifierTests
{
    [Theory]
    [InlineData("s01a13a_central")]
    [InlineData("s01a13a_north.mdn")]
    [InlineData("s01a13a_south")]
    [InlineData("s01a13a_west")]
    [InlineData("s01a14a_breakbuilding_bg")]
    [InlineData("s001a_realshadow_0")]
    [InlineData("s01a14a_d1090_shadow_obj")]
    public void Architectural_assets_are_directional_shadow_casters(string assetName)
    {
        Assert.True(StageShadowClassifier.IsArchitecturalCaster(assetName));
    }

    [Theory]
    [InlineData("s01a13a_ground")]
    [InlineData("s01a13a_enkei")]
    [InlineData("s01a13a_nv_obj")]
    [InlineData("s01a13a_close")]
    [InlineData("s01a13a_statue_son")]
    [InlineData("s01a12a_komono")]
    [InlineData("s01a11a_object")]
    [InlineData("s01_sky")]
    public void Non_building_stage_layers_are_receiver_only(string assetName)
    {
        Assert.False(StageShadowClassifier.IsArchitecturalCaster(assetName));
    }

    [Fact]
    public void Placed_gameplay_object_never_enters_static_building_caster_list()
    {
        var model = OpaqueModel();
        StageShadowClassifier.Apply(model, "s01a13a_north", isPlacedObject: true);

        Assert.False(model.CastsShadow);
        Assert.True(model.ReceivesShadow);
    }

    [Fact]
    public void Ground_receives_but_does_not_cast()
    {
        var model = OpaqueModel();
        StageShadowClassifier.Apply(model, "s01a13a_ground", isPlacedObject: false);

        Assert.False(model.CastsShadow);
        Assert.True(model.ReceivesShadow);
    }

    private static Model3D OpaqueModel() => new()
    {
        WriteDepth = true,
        BlendEnabled = false,
        Indices = [0, 1, 2]
    };
}
