using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Tests.Rendering;

public sealed class GeomSceneBuilderTests
{
    [Fact]
    public void Collision_attribute_palette_uses_semantic_flag_priority()
    {
        var floor = GeomSceneBuilder.GetCollisionAttributeColor(GeoCollisionAttributes.Floor);
        var water = GeomSceneBuilder.GetCollisionAttributeColor(GeoCollisionAttributes.Water);
        var stairs = GeomSceneBuilder.GetCollisionAttributeColor(GeoCollisionAttributes.Stairway);
        var floorAndPlayer = GeomSceneBuilder.GetCollisionAttributeColor(
            GeoCollisionAttributes.Floor | GeoCollisionAttributes.Player);

        Assert.NotEqual(floor, water);
        Assert.NotEqual(floor, stairs);
        Assert.NotEqual(water, stairs);
        Assert.Equal(floor, floorAndPlayer);
    }

    [Fact]
    public void Collision_vertex_colors_follow_each_triangles_primitive_type()
    {
        float[] positions =
        [
            0, 0, 0,
            1, 0, 0,
            0, 1, 0,
            2, 0, 0,
            3, 0, 0,
            2, 1, 0
        ];
        uint[] indices = [0, 1, 2, 3, 4, 5];

        var colors = GeomSceneBuilder.BuildCollisionVertexColors(
            positions,
            indices,
            [0, 1],
            [GeoCollisionAttributes.Floor, GeoCollisionAttributes.Water]);

        Assert.Equal(positions.Length / 3 * 4, colors.Length);
        Assert.NotEqual(ReadColor(colors, 0), ReadColor(colors, 3));
        Assert.Equal(1.0f, colors[3]);
        Assert.Equal(1.0f, colors[15]);
    }

    [Fact]
    public void Collision_triangle_filter_keeps_only_primitives_with_the_selected_flag()
    {
        uint[] indices =
        [
            0, 1, 2,
            3, 4, 5,
            6, 7, 8,
            9, 10, 11
        ];
        int[] primitiveIndices = [0, 1, 2, 3];
        int[] polygonIndices = [10, 11, 12, 13];
        ulong[] attributes =
        [
            GeoCollisionAttributes.Floor,
            GeoCollisionAttributes.Floor | GeoCollisionAttributes.Player,
            GeoCollisionAttributes.Water,
            0
        ];

        var floor = GeomSceneBuilder.FilterCollisionTriangles(
            indices,
            primitiveIndices,
            polygonIndices,
            attributes,
            GeoCollisionAttributes.Floor);
        var noFlags = GeomSceneBuilder.FilterCollisionTriangles(
            indices,
            primitiveIndices,
            polygonIndices,
            attributes,
            0);

        Assert.Equal([0u, 1, 2, 3, 4, 5], floor.Indices);
        Assert.Equal([0, 1], floor.PrimitiveIndices);
        Assert.Equal([10, 11], floor.PolygonIndices);
        Assert.Equal([9u, 10, 11], noFlags.Indices);
        Assert.Equal([3], noFlags.PrimitiveIndices);
    }

    private static Vector3 ReadColor(float[] colors, int vertex)
    {
        var offset = vertex * 4;
        return new Vector3(colors[offset], colors[offset + 1], colors[offset + 2]);
    }
}
