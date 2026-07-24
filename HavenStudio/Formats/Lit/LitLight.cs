using System;
using System.Buffers.Binary;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Lit;

public enum LitVariant
{
    Raw,
    Prefixed
}

/// <summary>The runtime target selected by the LIT target flags.</summary>
public enum LitLightingTarget
{
    Background,
    Character,
    Shadow
}

/// <summary>
/// Target and state bits used by Konami's LIT/LT2/LT3 runtime.
/// The values are confirmed by the original fmt_lit.h and by the MGS4 debug ELF.
/// </summary>
public static class LitFlags
{
    public const uint Character = 0x0100;
    public const uint Background = 0x0200;
    public const uint Shadow = 0x0400;
    public const uint Disable = 0x8000;
    private const uint TargetMask = Character | Background | Shadow;

    public static bool Applies(uint flag, LitLightingTarget target)
    {
        if ((flag & Disable) != 0)
        {
            return false;
        }

        // Stage vertex bake: the engine preshader tests the record flag with
        //   rlwinm (bit 22) -> flag & 0x0200  (must be SET)
        //   rlwinm (bit 16) -> flag & 0x8000  (must be CLEAR)
        // and skips the record otherwise (DG_MakePreshaderModelUnit build 2739,
        // prefilter passes @0x129E14 and friends). There is NO "no target bits
        // means all targets" fallback on this path: in the real s01a10a.lt3, 409
        // of 562 line records carry no target bit and the engine skips every one.
        // Accepting them made Haven accumulate ~450 records where the engine uses
        // 23 - the cause of the washed-out, contrast-free preview.
        if (target == LitLightingTarget.Background)
        {
            return (flag & Background) != 0;
        }

        // Older/raw fixtures can have no explicit target bits. Preserve the original
        // all-target behaviour for those records; real MGS4 LT3 records set a target.
        if ((flag & TargetMask) == 0)
        {
            return true;
        }

        var required = target switch
        {
            LitLightingTarget.Background => Background,
            LitLightingTarget.Character => Character,
            LitLightingTarget.Shadow => Shadow,
            _ => 0u
        };
        return required != 0 && (flag & required) != 0;
    }

    public static uint GetRuntimeFlag(LitLight light) => light switch
    {
        LitPointLight point => point.Flag,
        LitSpotLight spot => spot.Flag,
        LitLineLight line => line.Flag,
        LitBlackPoint blackPoint => blackPoint.Flag,
        LitParallelLight parallel => parallel.Flag,
        LitHemiLight hemi => hemi.RuntimeFlag,
        _ => 0u
    };
}

public readonly record struct LitColor(byte R, byte G, byte B, byte A)
{
    public Vector3 ToVector3() => new(R / 255f, G / 255f, B / 255f);

    /// <summary>
    /// Konami CVECTOR is an RGBM-style encoding: RGB are mantissa bytes and the
    /// A byte is an intensity SCALE, not an alpha.
    ///
    /// Engine evidence (DG_GetLightScene, build 2739 @0x10CBF4-0x10CD14):
    ///   lbz  r9,3(rec)      ; A
    ///   fcfid / frsp -> f10 ; (float)A
    ///   lbz  r9,1(rec)      ; a colour channel
    ///   fmuls f0,f10,f0     ; channel * A
    ///   fdivs f0,f0,f12     ; / 255
    ///   fmuls f9,f11,f0     ; * attenuation
    /// The same A also scales the intensity used for the qualification threshold.
    ///
    /// Ignoring A (the previous <see cref="ToVector3"/> behaviour) is wrong in
    /// both directions: in the real s01a10a.lt3, 261 of 562 line records carry
    /// A = 0 and contribute NOTHING in the engine while Haven lit them at full
    /// colour, and the stage sun carries A = 2 so it was half as bright as it
    /// should be.
    /// </summary>
    public Vector3 ToScaledVector3() => new(
        R / 255f * A,
        G / 255f * A,
        B / 255f * A);

    public static LitColor FromVector3(Vector3 color, byte alpha = 0) => new(
        (byte)Math.Clamp((int)MathF.Round(color.X * 255f), 0, 255),
        (byte)Math.Clamp((int)MathF.Round(color.Y * 255f), 0, 255),
        (byte)Math.Clamp((int)MathF.Round(color.Z * 255f), 0, 255),
        alpha);
}

public abstract class LitLight
{
    /// <summary>The 16 extra bytes present in prefixed MGS4 LT3 records.</summary>
    public byte[] VariantExtra { get; set; } = [];

    /// <summary>First four bytes of the LT3 extension (record-specific value/padding).</summary>
    public uint VariantPrefix { get; internal set; }

    /// <summary>Konami 24-bit hash naming the light class (point, spot, line, black, hs-amb...).</summary>
    public uint MetadataNameHash { get; internal set; }

    /// <summary>Konami 24-bit hash identifying the LT3 scope/owner (local, sunshine, s01a13a...).</summary>
    public uint MetadataScopeHash { get; internal set; }

    /// <summary>Final reserved word in the LT3 extension.</summary>
    public uint MetadataReserved { get; internal set; }
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

public class LitRawLight : LitLight
{
    public LitRawLight(byte[] data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public byte[] Data { get; set; }
}

/// <summary>
/// MGS4 LT3 type-64/320 record (160 bytes, big-endian). The debug data and the
/// sm_dd stage prove that the real runtime flag is the 32-bit word at +0x90.
/// The colour alpha bytes at +0x83/+0x87 are colour padding/auxiliary bytes, not
/// the Character/Background target flags. Treating them as flags caused Haven to
/// apply hundreds of character-only hs-amb volumes to the stage background.
///
/// Confirmed layout:
///   +0x00 boundsMax       (FVECTOR)
///   +0x10 boundsMin       (FVECTOR)
///   +0x20 center          (FVECTOR)
///   +0x30 sizes           (FVECTOR)
///   +0x40..+0x6F auxiliary/precomputed data (preserved)
///   +0x70 direction       (FVECTOR)
///   +0x80 colour A        (CVECTOR)
///   +0x84 colour B        (CVECTOR)
///   +0x88 force A         (float)
///   +0x8C force B         (float)
///   +0x90 runtime flag    (uint32: 0x100 character, 0x200 background, ...)
///   +0x94 name hash       (24-bit Konami hash; e.g. hs-amb)
///   +0x98 scope hash      (24-bit Konami hash; e.g. s01a13a/sunshine)
///   +0x9C reserved        (uint32)
/// </summary>
public sealed class LitHemiLight : LitRawLight
{
    public const int RecordSize = 160;

    public LitHemiLight(byte[] data) : base(data)
    {
        if (data.Length != RecordSize)
        {
            throw new ArgumentException(
                $"Hemispheric LIT record must be {RecordSize} bytes, got {data.Length}.",
                nameof(data));
        }

        RuntimeFlag = ReadUInt32BE(0x90);
        MetadataNameHash = ReadUInt32BE(0x94) & 0x00FFFFFFu;
        MetadataScopeHash = ReadUInt32BE(0x98) & 0x00FFFFFFu;
        MetadataReserved = ReadUInt32BE(0x9C);
    }

    private float ReadFloatBE(int offset) =>
        BinaryPrimitives.ReadSingleBigEndian(Data.AsSpan(offset, 4));

    private uint ReadUInt32BE(int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(Data.AsSpan(offset, 4));

    public Vector4 BoundsMax => new(
        ReadFloatBE(0x00), ReadFloatBE(0x04), ReadFloatBE(0x08), ReadFloatBE(0x0C));

    public Vector4 BoundsMin => new(
        ReadFloatBE(0x10), ReadFloatBE(0x14), ReadFloatBE(0x18), ReadFloatBE(0x1C));

    public Vector4 Center => new(
        ReadFloatBE(0x20), ReadFloatBE(0x24), ReadFloatBE(0x28), ReadFloatBE(0x2C));

    public Vector4 Direction => new(
        ReadFloatBE(0x70), ReadFloatBE(0x74), ReadFloatBE(0x78), ReadFloatBE(0x7C));

    public LitColor ColorSky => new(Data[0x80], Data[0x81], Data[0x82], Data[0x83]);

    public LitColor ColorGround => new(Data[0x84], Data[0x85], Data[0x86], Data[0x87]);

    public float ForceSky => ReadFloatBE(0x88);

    public float ForceGround => ReadFloatBE(0x8C);

    public uint RuntimeFlag { get; }
}
