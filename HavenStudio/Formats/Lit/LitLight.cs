using System;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Lit;

public enum LitVariant
{
    Raw,
    Prefixed
}

public readonly record struct LitColor(byte R, byte G, byte B, byte A)
{
    public Vector3 ToVector3() => new(R / 255f, G / 255f, B / 255f);

    public static LitColor FromVector3(Vector3 color, byte alpha = 0) => new(
        (byte)Math.Clamp((int)MathF.Round(color.X * 255f), 0, 255),
        (byte)Math.Clamp((int)MathF.Round(color.Y * 255f), 0, 255),
        (byte)Math.Clamp((int)MathF.Round(color.Z * 255f), 0, 255),
        alpha);
}

public abstract class LitLight
{
    public byte[] VariantExtra { get; set; } = [];
}

public sealed class LitPointLight : LitLight
{
    public Vector4 Point { get; set; }
    public LitColor Color { get; set; }
    public float Range { get; set; }
    public float ExtendedRange { get; set; }
    public uint Flag { get; set; }
}

public sealed class LitSpotLight : LitLight
{
    public Vector4 BoundsMax { get; set; }
    public Vector4 BoundsMin { get; set; }
    public Vector4 Point { get; set; }
    public Vector4 Direction { get; set; }
    public LitColor Color { get; set; }
    public float Umbra { get; set; }
    public float Penumbra { get; set; }
    public uint Flag { get; set; }
}

public sealed class LitLineLight : LitLight
{
    public Vector4 BoundsMax { get; set; }
    public Vector4 BoundsMin { get; set; }
    public Vector4 Point { get; set; }
    public Vector4 Direction { get; set; }
    public LitColor Color { get; set; }
    public float Range { get; set; }
    public uint Pad { get; set; }
    public uint Flag { get; set; }
}

public sealed class LitBlackPoint : LitLight
{
    public Vector4 BoundsMax { get; set; }
    public Vector4 BoundsMin { get; set; }
    public Vector4 Point { get; set; }
    public float Range { get; set; }
    public uint Flag { get; set; }
    public uint Pad0 { get; set; }
    public uint Pad1 { get; set; }
}

public sealed class LitParallelLight : LitLight
{
    public Vector4 BoundsMax { get; set; }
    public Vector4 BoundsMin { get; set; }
    public Vector4 Direction { get; set; }
    public LitColor Color { get; set; }
    public LitColor Ambient { get; set; }
    public float Force { get; set; }
    public uint Flag { get; set; }
}

public sealed class LitRawLight : LitLight
{
    public LitRawLight(byte[] data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public byte[] Data { get; set; }
}
