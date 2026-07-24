using System;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

/// <summary>
/// Six-direction ambient cube. NewSystemLightSet addresses the slots as left,
/// right, top, bottom, front and back. A uniform cube exactly reproduces a
/// conventional single ambient colour, while a directional cube preserves the
/// normal-dependent fill.
/// </summary>
public readonly record struct AmbientCubeLighting(
    Vector3 Left,
    Vector3 Right,
    Vector3 Top,
    Vector3 Bottom,
    Vector3 Front,
    Vector3 Back)
{
    public static AmbientCubeLighting Uniform(Vector3 color) =>
        new(color, color, color, color, color, color);

    public Vector3 Average =>
        (Left + Right + Top + Bottom + Front + Back) / 6f;

    /// <summary>
    /// Evaluates the six directional lobes with squared normal components.
    /// The weights sum to one for a normalized normal, so a uniform cube is
    /// identical to a scalar ambient colour and cannot change legacy results.
    /// </summary>
    public Vector3 Evaluate(Vector3 normal)
    {
        if (!IsFinite(normal) || normal.LengthSquared <= 0.000001f)
        {
            normal = Vector3.UnitY;
        }
        else
        {
            normal = Vector3.Normalize(normal);
        }

        var x2 = normal.X * normal.X;
        var y2 = normal.Y * normal.Y;
        var z2 = normal.Z * normal.Z;
        return (normal.X >= 0f ? Right : Left) * x2 +
               (normal.Y >= 0f ? Top : Bottom) * y2 +
               (normal.Z >= 0f ? Front : Back) * z2;
    }

    public AmbientCubeLighting Multiply(float scale) => new(
        Left * scale,
        Right * scale,
        Top * scale,
        Bottom * scale,
        Front * scale,
        Back * scale);

    public AmbientCubeLighting AddUniform(Vector3 color) => new(
        Left + color,
        Right + color,
        Top + color,
        Bottom + color,
        Front + color,
        Back + color);

    public AmbientCubeLighting BlendToward(AmbientCubeLighting target, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return new AmbientCubeLighting(
            Blend(Left, target.Left, amount),
            Blend(Right, target.Right, amount),
            Blend(Top, target.Top, amount),
            Blend(Bottom, target.Bottom, amount),
            Blend(Front, target.Front, amount),
            Blend(Back, target.Back, amount));
    }

    public AmbientCubeLighting ClampNonNegative() => new(
        Clamp(Left), Clamp(Right), Clamp(Top), Clamp(Bottom), Clamp(Front), Clamp(Back));

    private static Vector3 Blend(Vector3 from, Vector3 to, float amount) =>
        from + (to - from) * amount;

    private static Vector3 Clamp(Vector3 value) => new(
        MathF.Max(0f, value.X),
        MathF.Max(0f, value.Y),
        MathF.Max(0f, value.Z));

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
