using System.Globalization;
using HavenStudio.Editors;
using HavenStudio.Formats.Geo;

namespace HavenStudio.Tests.Rendering;

public sealed class EffectSelectionEntityTests
{
    [Fact]
    public void Batch_rotation_replaces_mixed_y_values_and_preserves_x_z()
    {
        var first = CreateEffect(rotationX: 0.1f, rotationY: 0f, rotationZ: 0.3f);
        var second = CreateEffect(rotationX: 0.4f, rotationY: MathF.PI / 2f, rotationZ: 0.6f);
        var effects = new[] { first, second };
        var selection = new EffectSelectionEntity(
            effects,
            degrees =>
            {
                var radians = degrees * MathF.PI / 180f;
                foreach (var effect in effects)
                {
                    effect.SetRotation(effect.RotationX, radians, effect.RotationZ);
                }
                return null;
            });

        Assert.True(selection.HasMixedRotationY);
        Assert.Equal(string.Empty, selection.RotationYText);

        Assert.True(selection.TryUpdateRotationYDegrees(45f, out var error));

        Assert.Null(error);
        Assert.False(selection.HasMixedRotationY);
        Assert.Equal(45f, float.Parse(selection.RotationYText, CultureInfo.CurrentCulture), 3);
        Assert.Equal(0.1f, first.RotationX, 4);
        Assert.Equal(0.3f, first.RotationZ, 4);
        Assert.Equal(0.4f, second.RotationX, 4);
        Assert.Equal(0.6f, second.RotationZ, 4);
    }

    [Fact]
    public void Batch_rotation_surfaces_callback_errors()
    {
        var selection = new EffectSelectionEntity(
            new[] { CreateEffect(0f, 0f, 0f) },
            _ => "Rotation storage is unavailable.");

        Assert.False(selection.TryUpdateRotationYDegrees(90f, out var error));

        Assert.Equal("Rotation storage is unavailable.", error);
        Assert.True(selection.HasRotationEditStatus);
        Assert.Equal(error, selection.RotationEditStatus);
    }

    private static CollisionEffectViewModel CreateEffect(
        float rotationX,
        float rotationY,
        float rotationZ)
    {
        var effect = new GeoEffect
        {
            RotationX = rotationX,
            RotationY = rotationY,
            RotationZ = rotationZ
        };
        return new CollisionEffectViewModel(effect, () => { }, () => { }, _ => { });
    }
}
