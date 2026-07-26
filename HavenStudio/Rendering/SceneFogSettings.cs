using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

/// <summary>
/// Immutable MGS4 viewport-fog snapshot reconstructed from NewFogSet.
/// Distances are kept in engine/world units. Colours are normalized to 0..1.
/// </summary>
public readonly record struct Mgs4FogState(
    float Near,
    float Far,
    Vector4 Color,
    float LimitMin,
    float LimitMax,
    float BeforeNear,
    float BeforeFar,
    Vector4 BeforeColor,
    float BeforeLimitMin,
    float BeforeLimitMax)
{
    public static Mgs4FogState Default { get; } = new(
        0f,
        10000f,
        new Vector4(0f, 0f, 0f, 1f),
        0f,
        1f,
        0f,
        10000f,
        new Vector4(0f, 0f, 0f, 1f),
        0f,
        1f);

    public Mgs4FogState Interpolate(float weight)
    {
        var t = Math.Clamp(weight, 0f, 1f);
        return this with
        {
            Near = BeforeNear + (Near - BeforeNear) * t,
            Far = BeforeFar + (Far - BeforeFar) * t,
            Color = Vector4.Lerp(BeforeColor, Color, t),
            LimitMin = BeforeLimitMin + (LimitMin - BeforeLimitMin) * t,
            LimitMax = BeforeLimitMax + (LimitMax - BeforeLimitMax) * t
        };
    }
}

/// <summary>
/// The engine stores three viewports. Haven currently renders the first
/// logical viewport but preserves all three states so viewport-specific GCX data
/// is not discarded.
/// </summary>
public sealed class SceneFogSettings
{
    private readonly Mgs4FogState[] _viewports;
    private readonly bool[] _configured;

    public SceneFogSettings(
        Mgs4FogState viewport0,
        Mgs4FogState viewport1,
        Mgs4FogState viewport2,
        bool viewport0Configured = true,
        bool viewport1Configured = true,
        bool viewport2Configured = true)
    {
        _viewports = [viewport0, viewport1, viewport2];
        _configured = [viewport0Configured, viewport1Configured, viewport2Configured];
    }

    public Mgs4FogState this[int viewport] => _viewports[Math.Clamp(viewport, 0, 2)];
    public IReadOnlyList<Mgs4FogState> Viewports => _viewports;
    public bool HasAnyConfiguredViewport => _configured[0] || _configured[1] || _configured[2];

    public bool IsConfigured(int viewport) => _configured[Math.Clamp(viewport, 0, 2)];

    public bool TryGetViewport(int viewport, out Mgs4FogState state)
    {
        var index = Math.Clamp(viewport, 0, 2);
        state = _viewports[index];
        return _configured[index];
    }

    /// <summary>
    /// Neutral, unconfigured state. It is retained for tests/editing but must never
    /// be installed as active fog merely because a GCX command was not decoded.
    /// </summary>
    public static SceneFogSettings Default { get; } = new(
        Mgs4FogState.Default,
        Mgs4FogState.Default,
        Mgs4FogState.Default,
        false,
        false,
        false);
}

/// <summary>
/// Parses the fog command options, including unresolved hash names.
/// </summary>
public static partial class GcxFogParser
{
    private const string Number = @"(-?\d+(?:\.\d+)?)";

    public static SceneFogSettings? Parse(IEnumerable<string> decompiledScripts)
    {
        ArgumentNullException.ThrowIfNull(decompiledScripts);

        var states = new[]
        {
            Mgs4FogState.Default,
            Mgs4FogState.Default,
            Mgs4FogState.Default
        };
        var configured = new bool[3];
        var found = false;

        foreach (var script in decompiledScripts)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                continue;
            }

            foreach (Match command in CommandRegex().Matches(script))
            {
                found = true;
                var text = command.Value;

                // NewFogSet initializes these command-local defaults before reading options.
                var near = ReadScalar(text, 0f, "near", "38A092");
                var far = ReadScalar(text, 10000f, "far", "01A492");
                if (!float.IsFinite(near)) near = 0f;
                if (!float.IsFinite(far)) far = 10000f;
                if (far <= near + 0.0001f) far = near + 1f;

                var color = ReadRgb(text, new Vector4(0f, 0f, 0f, 1f), "rgb", "01D542");
                var viewport = ReadInteger(text, -1, "viewport", "AD95C5");
                var (limitMin, limitMax) = ReadLimit(text, 0f, 1f, "limit", "F6419A");

                var beforeNear = ReadScalar(text, near, "before_near", "323777");
                var beforeFar = ReadScalar(text, far, "before_far", "297149");
                if (!float.IsFinite(beforeNear)) beforeNear = near;
                if (!float.IsFinite(beforeFar) || beforeFar <= beforeNear + 0.0001f)
                {
                    beforeFar = MathF.Max(beforeNear + 1f, far);
                }

                var beforeColor = ReadRgb(text, color, "before_rgb", "29A1F9");
                var (beforeLimitMin, beforeLimitMax) = ReadLimit(
                    text,
                    limitMin,
                    limitMax,
                    "before_limit",
                    "291E3A");

                var state = new Mgs4FogState(
                    near,
                    far,
                    color,
                    limitMin,
                    limitMax,
                    beforeNear,
                    beforeFar,
                    beforeColor,
                    beforeLimitMin,
                    beforeLimitMax);

                if (viewport < 0)
                {
                    states[0] = state;
                    states[1] = state;
                    states[2] = state;
                    configured[0] = true;
                    configured[1] = true;
                    configured[2] = true;
                }
                else if (viewport <= 2)
                {
                    states[viewport] = state;
                    configured[viewport] = true;
                }
            }
        }

        return found
            ? new SceneFogSettings(
                states[0],
                states[1],
                states[2],
                configured[0],
                configured[1],
                configured[2])
            : null;
    }

    private static float ReadScalar(string text, float fallback, string name, string hash)
    {
        var match = Regex.Match(
            text,
            ParameterPattern(name, hash) + @"\s+" + Number,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && float.TryParse(
            match.Groups[1].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }

    private static int ReadInteger(string text, int fallback, string name, string hash)
    {
        var match = Regex.Match(
            text,
            ParameterPattern(name, hash) + @"\s+(-?\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(
            match.Groups[1].Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }

    private static Vector4 ReadRgb(string text, Vector4 fallback, string name, string hash)
    {
        var match = Regex.Match(
            text,
            ParameterPattern(name, hash) + @"\s+" + Number + @"\s+" + Number + @"\s+" + Number,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success ||
            !float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ||
            !float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var g) ||
            !float.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
        {
            return fallback;
        }

        // Exact NewFogSet conversion: trunc(component * 255 / 1000), then normalize
        // the packed byte for the OpenGL shader.
        return new Vector4(ToPackedByte(r) / 255f, ToPackedByte(g) / 255f, ToPackedByte(b) / 255f, 1f);
    }

    private static (float Min, float Max) ReadLimit(
        string text,
        float fallbackMin,
        float fallbackMax,
        string name,
        string hash)
    {
        var match = Regex.Match(
            text,
            ParameterPattern(name, hash) + @"\s+" + Number + @"\s+" + Number,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success ||
            !float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var min) ||
            !float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var max))
        {
            return (fallbackMin, fallbackMax);
        }

        // Values are stored per-mille.
        min /= 1000f;
        max /= 1000f;
        if (!float.IsFinite(min) || !float.IsFinite(max))
        {
            return (fallbackMin, fallbackMax);
        }
        return (Math.Clamp(min, 0f, 1f), Math.Clamp(max, 0f, 1f));
    }

    private static string ParameterPattern(string name, string hash) =>
        $@"(?<![A-Za-z0-9_])-\s*(?:{Regex.Escape(name)}|[A-Za-z]?\[{hash}\])";

    private static float ToPackedByte(float component)
    {
        var scaled = MathF.Truncate(component * 255f / 1000f);
        return Math.Clamp(scaled, 0f, 255f);
    }

    [GeneratedRegex(
        @"(?im)^[ \t]*(?:command[ \t]+)?(?:NewFog(?:Set)?|\[DDE914\])[ \t]*(?:\\)?[ \t]*\r?\n(?<body>.*?)(?=^[ \t]*(?:command[ \t]+)?(?:[A-Za-z_][A-Za-z0-9_]*|\[[0-9A-F]{6}\])[ \t]*(?:\\)?[ \t]*$|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CommandRegex();
}
