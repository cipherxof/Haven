using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public sealed record SceneColorFilterSettings(
    float Mono,
    Vector3 Scale,
    float Brightness,
    float Contrast,
    Vector3 Minimum,
    Vector3 Maximum,
    float Noise)
{
    public static SceneColorFilterSettings Neutral { get; } = new(
        0f, Vector3.One, 0f, 1f, Vector3.Zero, Vector3.One, 0f);

    public bool IsNeutral =>
        MathF.Abs(Mono) < 0.0001f &&
        (Scale - Vector3.One).LengthSquared < 0.0001f &&
        MathF.Abs(Brightness) < 0.0001f &&
        MathF.Abs(Contrast - 1f) < 0.0001f &&
        Minimum.LengthSquared < 0.0001f &&
        (Maximum - Vector3.One).LengthSquared < 0.0001f &&
        MathF.Abs(Noise) < 0.0001f;
}

public static partial class GcxColorFilterParser
{
    public static SceneColorFilterSettings? Parse(IEnumerable<string> decompiledScripts)
    {
        ArgumentNullException.ThrowIfNull(decompiledScripts);
        SceneColorFilterSettings? result = null;
        foreach (var script in decompiledScripts)
        {
            if (string.IsNullOrWhiteSpace(script)) continue;
            foreach (Match command in CommandRegex().Matches(script))
            {
                var current = result ?? SceneColorFilterSettings.Neutral;
                var text = command.Value;
                var mono = ReadScalar(text, current.Mono, "mono", "filter_mono");
                var scale = ReadVector(text, current.Scale, "scale", "scl", "filter_scale");
                var bright = ReadScalar(text, current.Brightness, "bright", "brightness", "filter_bright");
                var contrast = ReadScalar(text, current.Contrast, "contrast", "filter_contrast");
                var minimum = ReadVector(text, current.Minimum, "min", "minimum", "filter_min");
                var maximum = ReadVector(text, current.Maximum, "max", "maximum", "filter_max");
                var noise = ReadScalar(text, current.Noise, "noise", "filter_noise");
                result = new SceneColorFilterSettings(
                    Clamp01(NormalizeScalar(mono, false)),
                    ClampNonNegative(NormalizeVector(scale, true)),
                    NormalizeScalar(bright, false),
                    MathF.Max(0f, NormalizeScalar(contrast, true)),
                    Clamp01(NormalizeVector(minimum, false)),
                    Clamp01(NormalizeVector(maximum, true)),
                    Clamp01(NormalizeScalar(noise, false)));
            }
        }
        return result;
    }

    private static float ReadScalar(string text, float fallback, params string[] names)
    {
        foreach (var name in names)
        {
            var match = Regex.Match(text, $@"(?<![A-Za-z0-9_])-{Regex.Escape(name)}\s+(-?\d+(?:\.\d+)?)", RegexOptions.CultureInvariant);
            if (match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
        }
        return fallback;
    }

    private static Vector3 ReadVector(string text, Vector3 fallback, params string[] names)
    {
        foreach (var name in names)
        {
            var match = Regex.Match(text, $@"(?<![A-Za-z0-9_])-{Regex.Escape(name)}\s+(-?\d+(?:\.\d+)?)\s+(-?\d+(?:\.\d+)?)\s+(-?\d+(?:\.\d+)?)", RegexOptions.CultureInvariant);
            if (!match.Success) continue;
            if (float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                float.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) return new Vector3(x, y, z);
        }
        return fallback;
    }

    private static float NormalizeScalar(float value, bool oneDefault) =>
        MathF.Abs(value) > 4f ? value / 1000f : value;
    private static Vector3 NormalizeVector(Vector3 value, bool oneDefault) => new(
        NormalizeScalar(value.X, oneDefault), NormalizeScalar(value.Y, oneDefault), NormalizeScalar(value.Z, oneDefault));
    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    private static Vector3 Clamp01(Vector3 value) => new(Clamp01(value.X), Clamp01(value.Y), Clamp01(value.Z));
    private static Vector3 ClampNonNegative(Vector3 value) => new(MathF.Max(0f, value.X), MathF.Max(0f, value.Y), MathF.Max(0f, value.Z));

    // Data-driven read straight from the GCX bytecode (not the decompiled text).
    // The color-filter command takes:
    //   -mono, -scale (3), -bright, -contrast,
    //   -min_color_n (3), -max_color_n (3)
    // Each parameter is tagged in the bytecode by its strcode24 name hash; the
    // typed values follow (01=i16, 02/03/04=u8, 08=u16). Scale is stored /128,
    // the colour min/max and the scalars per-mille (/1000). Parameters stored as
    // script variables (varbuf, no inline constant) keep the neutral default.
    private const uint ColorFilterCommandHash = 0x98CBCE;
    private const uint MonoHash = 0x384A2F;
    private const uint ScaleHash = 0x6311EC;
    private const uint BrightHash = 0x562A3F;
    private const uint ContrastHash = 0x7DC777;
    private const uint MinColorHash = 0x8A67B4;
    private const uint MaxColorHash = 0x9467B3;

    public static SceneColorFilterSettings? ParseGcxScripts(IEnumerable<byte[]?> scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        foreach (var bytes in scripts)
        {
            if (bytes == null || bytes.Length == 0) continue;
            int cmd = FindHash(bytes, ColorFilterCommandHash, 0, bytes.Length);
            if (cmd < 0) continue;

            // Bound the parameter search to the command's self-delimited "6d <size>"
            // block so a colliding hash in a later command cannot leak in.
            int blockEnd = bytes.Length;
            for (int k = cmd; k >= Math.Max(0, cmd - 24); k--)
            {
                if (bytes[k] == 0x6D && k + 1 < bytes.Length)
                {
                    blockEnd = Math.Min(bytes.Length, k + 2 + bytes[k + 1]);
                    break;
                }
            }

            var n = SceneColorFilterSettings.Neutral;
            var mono = ReadHashScalar(bytes, MonoHash, cmd, blockEnd, 1000f, n.Mono);
            var scale = ReadHashVector(bytes, ScaleHash, cmd, blockEnd, 128f, n.Scale);
            var bright = ReadHashScalar(bytes, BrightHash, cmd, blockEnd, 1000f, n.Brightness);
            var contrast = ReadHashScalar(bytes, ContrastHash, cmd, blockEnd, 1000f, n.Contrast);
            var minimum = ReadHashVector(bytes, MinColorHash, cmd, blockEnd, 1000f, n.Minimum);
            var maximum = ReadHashVector(bytes, MaxColorHash, cmd, blockEnd, 1000f, n.Maximum);

            var result = new SceneColorFilterSettings(
                Clamp01(mono),
                ClampNonNegative(scale),
                bright,
                MathF.Max(0f, contrast),
                Clamp01(minimum),
                Clamp01(maximum),
                n.Noise);
            return result.IsNeutral ? null : result;
        }
        return null;
    }

    private static int FindHash(byte[] b, uint hash, int start, int end)
    {
        byte b0 = (byte)(hash & 0xFF);
        byte b1 = (byte)((hash >> 8) & 0xFF);
        byte b2 = (byte)((hash >> 16) & 0xFF);
        int limit = Math.Min(end, b.Length) - 3;
        for (int i = Math.Max(0, start); i <= limit; i++)
        {
            if (b[i] == b0 && b[i + 1] == b1 && b[i + 2] == b2) return i;
        }
        return -1;
    }

    private static int ReadTypedValues(byte[] b, int p, int end, float unit, Span<float> outv)
    {
        int got = 0;
        while (p < end && got < outv.Length)
        {
            byte t = b[p];
            if (t == 0x01 && p + 3 <= end) { outv[got++] = (short)(b[p + 1] | (b[p + 2] << 8)) / unit; p += 3; }
            else if ((t == 0x02 || t == 0x03 || t == 0x04) && p + 2 <= end) { outv[got++] = b[p + 1] / unit; p += 2; }
            else if (t == 0x08 && p + 3 <= end) { outv[got++] = (ushort)(b[p + 1] | (b[p + 2] << 8)) / unit; p += 3; }
            else break;
        }
        return got;
    }

    private static float ReadHashScalar(byte[] b, uint hash, int start, int end, float unit, float fallback)
    {
        int idx = FindHash(b, hash, start, end);
        if (idx < 0) return fallback;
        Span<float> v = stackalloc float[1];
        return ReadTypedValues(b, idx + 3, end, unit, v) >= 1 ? v[0] : fallback;
    }

    private static Vector3 ReadHashVector(byte[] b, uint hash, int start, int end, float unit, Vector3 fallback)
    {
        int idx = FindHash(b, hash, start, end);
        if (idx < 0) return fallback;
        Span<float> v = stackalloc float[3];
        return ReadTypedValues(b, idx + 3, end, unit, v) >= 3 ? new Vector3(v[0], v[1], v[2]) : fallback;
    }

    // Haven's GCX decompiler writes a command at the beginning of a line as
    // "NewColorFilterSet \\" (resolved command table) or "[98CBCE] \\".
    // Older experimental builds searched for the literal word "command", which
    // never appears in normal decompilation and therefore disabled the preview.
    [GeneratedRegex(@"(?im)^[ \t]*(?:NewColorFilter(?:Set)?|\[98CBCE\])[ \t]*\\[ \t]*\r?\n(?<body>.*?)(?=^[ \t]*(?:[A-Za-z_][A-Za-z0-9_]*|\[[0-9A-F]{6}\])[ \t]*\\[ \t]*$|\z)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CommandRegex();
}
