using HavenStudio.Formats.Lit;
using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Rendering;

public sealed class LightSamplerTests
{
    [Fact]
    public void Point_light_obeys_group_bounds_and_radial_falloff()
    {
        var file = EmptyFile();
        var group = Group(1, new Vector3(-20), new Vector3(20));
        group.Lights.Add(new LitPointLight
        {
            Point = new Vector4(0, 10, 0, 0),
            Color = new LitColor(200, 100, 50, 0),
            Range = 15,
            ExtendedRange = 20
        });
        file.Groups.Add(group);

        var inside = LightSampler.Sample(file, Vector3.Zero);
        var outsideGroup = LightSampler.Sample(file, new Vector3(100, 0, 0));

        var light = Assert.Single(inside.DirectionalLights);
        Assert.Equal(Vector3.UnitY, light.Direction);
        Assert.Equal(200f / 255f / 3f, light.Color.X, 5);
        Assert.Empty(outsideGroup.DirectionalLights);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 0.6666667f)]
    [InlineData(10, 0.3333333f)]
    [InlineData(15, 0)]
    [InlineData(20, 0)]
    public void Radial_falloff_matches_get_light_scene(float distance, float expected)
    {
        Assert.Equal(expected, LightSampler.RadialAttenuation(distance, 15), 5);
    }

    [Fact]
    public void Reduction_keeps_the_three_strongest_directional_contributions()
    {
        var file = EmptyFile();
        var group = Group(1, new Vector3(-100), new Vector3(100));
        AddPoint(group, new Vector3(10, 0, 0), 40);
        AddPoint(group, new Vector3(0, 10, 0), 80);
        AddPoint(group, new Vector3(0, 0, 10), 120);
        AddPoint(group, new Vector3(-10, 0, 0), 160);
        file.Groups.Add(group);

        var sample = LightSampler.Sample(file, Vector3.Zero);

        Assert.Equal(3, sample.DirectionalLights.Count);
        Assert.Equal([144, 108, 72], sample.DirectionalLights.Select(light => (int)MathF.Round(light.Color.X * 255)));
    }

    [Fact]
    public void Black_point_subtracts_light_within_its_range()
    {
        var file = new LitFile
        {
            Direction = new Vector4(0, 1, 0, 0),
            Color = new LitColor(200, 200, 200, 0),
            Ambient = new LitColor(100, 100, 100, 0)
        };
        var group = Group(8, new Vector3(-10), new Vector3(10));
        group.Lights.Add(new LitBlackPoint
        {
            Point = Vector4.Zero,
            Range = 5
        });
        file.Groups.Add(group);

        var sample = LightSampler.Sample(file, Vector3.Zero);

        Assert.Equal(Vector3.Zero, sample.Ambient);
        Assert.Empty(sample.DirectionalLights);
    }

    [Fact]
    public void Parallel_light_adds_to_system_light_without_replacing_ambient()
    {
        var file = new LitFile
        {
            Direction = new Vector4(1, 0, 0, 0),
            Color = new LitColor(255, 0, 0, 0),
            Ambient = new LitColor(255, 0, 0, 0)
        };
        var group = Group(32, new Vector3(-10), new Vector3(10));
        group.Lights.Add(new LitParallelLight
        {
            Direction = new Vector4(0, 1, 0, 0),
            Color = new LitColor(0, 200, 0, 0),
            Ambient = new LitColor(0, 0, 100, 0),
            Force = 0.5f
        });
        file.Groups.Add(group);

        var sample = LightSampler.Sample(file, Vector3.Zero);

        Assert.Equal(2, sample.DirectionalLights.Count);
        Assert.Contains(sample.DirectionalLights,
            light => light.Direction == Vector3.UnitY && MathF.Abs(light.Color.Y - 200f / 255f) < 0.00001f);
        Assert.Equal(new Vector3(1, 0, 0), sample.Ambient);
    }

    [Fact]
    public void System_light_overrides_header_and_supplies_ambient_floor()
    {
        var file = EmptyFile();
        var settings = new SceneLightSettings(
            -Vector3.UnitY,
            new Vector3(0.8f, 0.79f, 0.56f),
            new Vector3(0.43f, 0.505f, 0.3898f));

        var sample = LightSampler.Sample(file, Vector3.Zero, settings);

        Assert.Equal(settings.AmbientColor, sample.Ambient);
        var sun = Assert.Single(sample.DirectionalLights);
        Assert.Equal(settings.Direction, sun.Direction);
        Assert.Equal(settings.DirectionalColor, sun.Color);
    }

    [Fact]
    public void Line_light_treats_direction_as_an_axis_not_a_world_endpoint()
    {
        var file = EmptyFile();
        var group = Group(4, new Vector3(-100), new Vector3(100));
        group.Lights.Add(new LitLineLight
        {
            BoundsMin = new Vector4(-100, -100, -100, 0),
            BoundsMax = new Vector4(100, 100, 100, 0),
            Point = new Vector4(10, 0, 0, 0),
            Direction = new Vector4(0, 1, 0, 10),
            Color = new LitColor(255, 255, 255, 0),
            Range = 10
        });
        file.Groups.Add(group);

        var sample = LightSampler.Sample(file, new Vector3(12, 5, 0));

        var light = Assert.Single(sample.DirectionalLights);
        Assert.Equal(-Vector3.UnitX, light.Direction);
        Assert.Equal(0.8f, light.Color.X, 5);
    }

    [Fact]
    public void Line_light_clamps_nearest_point_to_direction_w_segment_length()
    {
        var file = EmptyFile();
        var group = Group(4, new Vector3(-100), new Vector3(100));
        group.Lights.Add(new LitLineLight
        {
            BoundsMin = new Vector4(-100, -100, -100, 0),
            BoundsMax = new Vector4(100, 100, 100, 0),
            Point = Vector4.Zero,
            Direction = new Vector4(0, 1, 0, 10),
            Color = new LitColor(255, 255, 255, 0),
            Range = 10
        });
        file.Groups.Add(group);

        var sample = LightSampler.Sample(file, new Vector3(0, 15, 0));

        var light = Assert.Single(sample.DirectionalLights);
        Assert.Equal(-Vector3.UnitY, light.Direction);
        Assert.Equal(0.5f, light.Color.X, 5);
    }

    [Fact]
    public void Vertex_baker_uses_world_normal_and_preserves_alpha()
    {
        float[] normals = [0, 1, 0, 0, -1, 0];
        float[] baseColors = [1, 1, 1, 0.25f, 1, 1, 1, 0.75f];
        var lighting = new SampledLighting(
            new Vector3(0.1f),
            [new DirectionalLightSample(Vector3.UnitY, new Vector3(0.5f))]);

        var colors = LightVertexBaker.BakeColors(normals, baseColors, 2, Matrix4.Identity, lighting);

        Assert.Equal([0.6f, 0.6f, 0.6f], colors[..3]);
        Assert.Equal(0.25f, colors[3]);
        Assert.Equal([0.1f, 0.1f, 0.1f], colors[4..7]);
        Assert.Equal(0.75f, colors[7]);
    }

    [Fact]
    public void Spatial_vertex_baker_samples_each_stage_vertex_and_restores_base_colors()
    {
        var file = EmptyFile();
        var group = Group(1, new Vector3(-100), new Vector3(100));
        group.Lights.Add(new LitPointLight
        {
            Point = new Vector4(0, 5, 0, 0),
            Color = new LitColor(255, 255, 255, 0),
            Range = 10,
            ExtendedRange = 10
        });
        file.Groups.Add(group);
        var baseColors = new[] { 0.5f, 1f, 1f, 0.25f, 1f, 1f, 1f, 0.75f };
        var model = new Avalonia3DControl.Core.Models.Model3D
        {
            Positions = [0, 0, 0, 20, 0, 0],
            Colors = (float[])baseColors.Clone(),
            VertexCount = 2
        };
        LightVertexBaker.Register(model, [0, 1, 0, 0, 1, 0], baseColors);

        var applied = LightVertexBaker.ApplySpatial(
            model,
            position => LightSampler.Sample(file, position),
            modulateBaseColor: true);

        Assert.True(applied);
        Assert.Equal(0.25f, model.Colors[0], 5);
        Assert.Equal(0.5f, model.Colors[1], 5);
        Assert.Equal(0, model.Colors[4]);
        Assert.Equal(0.25f, model.Colors[3]);
        Assert.Equal(0.75f, model.Colors[7]);
        Assert.True(LightVertexBaker.Restore(model));
        Assert.Equal(baseColors, model.Colors);
    }

    private static LitFile EmptyFile() => new()
    {
        Direction = new Vector4(0, 1, 0, 0),
        Color = new LitColor(0, 0, 0, 0),
        Ambient = new LitColor(0, 0, 0, 0)
    };

    private static LitGroup Group(uint type, Vector3 min, Vector3 max) => new()
    {
        Type = type,
        BoundsMin = new Vector4(min, 0),
        BoundsMax = new Vector4(max, 0)
    };

    private static void AddPoint(LitGroup group, Vector3 position, byte strength)
    {
        group.Lights.Add(new LitPointLight
        {
            Point = new Vector4(position, 0),
            Color = new LitColor(strength, 0, 0, 0),
            Range = 100,
            ExtendedRange = 100
        });
    }
}
