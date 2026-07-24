using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HavenStudio.Formats.Lit;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

/// <summary>
/// System-light values installed by NewSystemLightSet.
///
/// The background scene light and the character light are separate: -color
/// feeds the scene light, -chara_color the character light. The character
/// hemisphere options are likewise not a replacement for the LT2/LT3 ambient
/// used by stage geometry.
/// </summary>
public sealed record SceneLightSettings(
    Vector3 Direction,
    Vector3 BackgroundDirectionalColor,
    Vector3 CharacterDirectionalColor,
    Vector2? CharacterHemisphereRotation = null,
    Vector4? CharacterHemisphereFrontColor = null,
    Vector4? CharacterHemisphereBackColor = null,
    AmbientCubeLighting? BackgroundAmbientCube = null)
{
    public Vector3 DirectionalColorFor(LitLightingTarget target) =>
        target == LitLightingTarget.Character
            ? CharacterDirectionalColor
            : BackgroundDirectionalColor;

    public AmbientCubeLighting AmbientCubeFor(
        LitLightingTarget target,
        Vector3 fallbackAmbient)
    {
        if (target == LitLightingTarget.Background && BackgroundAmbientCube is { } cube)
        {
            return cube;
        }

        if (target == LitLightingTarget.Character && CharacterAmbientFloor is { } characterAmbient)
        {
            return AmbientCubeLighting.Uniform(characterAmbient);
        }

        return AmbientCubeLighting.Uniform(fallbackAmbient);
    }

    /// <summary>
    /// Returns the command's two-colour character hemisphere reduced to the
    /// ambient floor used by Haven's current vertex-lighting preview. This value
    /// must never be applied to background/stage geometry.
    /// </summary>
    public Vector3? CharacterAmbientFloor
    {
        get
        {
            if (CharacterHemisphereFrontColor is not { } front ||
                CharacterHemisphereBackColor is not { } back)
            {
                return null;
            }

            var frontColor = ClampNonNegative(front.Xyz);
            var backColor = ClampNonNegative(back.Xyz);
            var frontWeight = MathF.Max(0f, front.W);
            var backWeight = MathF.Max(0f, back.W);
            return frontColor * frontWeight + backColor * backWeight;
        }
    }

    private static Vector3 ClampNonNegative(Vector3 value) => new(
        MathF.Max(0f, value.X),
        MathF.Max(0f, value.Y),
        MathF.Max(0f, value.Z));
}

public static partial class GcxSystemLightParser
{
    private const string DirectionHash = "019D92";
    private const string BackgroundColorHash = "693E58";
    private const string CharacterColorHash = "CDE8EF";
    private const string HemisphereRotationHash = "D47DA7";
    private const string HemisphereFrontHash = "D97162";
    private const string HemisphereBackHash = "7834E3";
    private const string AmbientHash = "563B54";
    private const string AmbientLeftHash = "D417E2";
    private const string AmbientRightHash = "E4FF4E";
    private const string AmbientTopHash = "76C205";
    private const string AmbientBottomHash = "06998A";
    private const string AmbientFrontHash = "29A00E";
    private const string AmbientBackHash = "CF0779";

    public static SceneLightSettings? Parse(IEnumerable<string> decompiledScripts)
    {
        ArgumentNullException.ThrowIfNull(decompiledScripts);
        foreach (var script in decompiledScripts)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                continue;
            }

            foreach (Match command in CommandRegex().Matches(script))
            {
                var text = command.Value;
                if (!TryRead(text, "dir", DirectionHash, 3, out var directionValues))
                {
                    continue;
                }

                var direction = new Vector3(directionValues[0], directionValues[1], directionValues[2]);
                if (!IsFinite(direction) || direction.LengthSquared <= 0.000001f)
                {
                    continue;
                }
                direction = Vector3.Normalize(direction);

                var hasBackgroundColor = TryRead(
                    text,
                    "color",
                    BackgroundColorHash,
                    3,
                    out var backgroundValues);
                var hasCharacterColor = TryRead(
                    text,
                    "chara_color",
                    CharacterColorHash,
                    3,
                    out var characterValues);
                if (!hasBackgroundColor && !hasCharacterColor)
                {
                    continue;
                }

                // The command allows either colour to be omitted. Fall back to the
                // other slot only in that case; never prefer chara_color for stage
                // geometry when both are present.
                var background = ToColor(
                    hasBackgroundColor ? backgroundValues : characterValues);
                var character = ToColor(
                    hasCharacterColor ? characterValues : backgroundValues);

                Vector2? hemisphereRotation = null;
                if (TryRead(
                        text,
                        "hemispherelight_rot",
                        HemisphereRotationHash,
                        2,
                        out var rotationValues))
                {
                    hemisphereRotation = new Vector2(rotationValues[0], rotationValues[1]);
                }

                Vector4? hemisphereFront = null;
                if (TryRead(
                        text,
                        "hemispherelight_frontcolor",
                        HemisphereFrontHash,
                        4,
                        out var frontValues))
                {
                    hemisphereFront = ToColorWithWeight(frontValues);
                }

                Vector4? hemisphereBack = null;
                if (TryRead(
                        text,
                        "hemispherelight_backcolor",
                        HemisphereBackHash,
                        4,
                        out var backValues))
                {
                    hemisphereBack = ToColorWithWeight(backValues);
                }

                AmbientCubeLighting? backgroundAmbientCube = null;
                var hasUniformAmbient = TryRead(
                    text,
                    "ambient",
                    AmbientHash,
                    3,
                    out var ambientValues);
                var uniformAmbient = hasUniformAmbient
                    ? ToColor(ambientValues)
                    : Vector3.Zero;

                var left = ReadOptionalColor(text, "ambient_left", AmbientLeftHash);
                var right = ReadOptionalColor(text, "ambient_right", AmbientRightHash);
                var top = ReadOptionalColor(text, "ambient_top", AmbientTopHash);
                var bottom = ReadOptionalColor(text, "ambient_bottom", AmbientBottomHash);
                var front = ReadOptionalColor(text, "ambient_front", AmbientFrontHash);
                var back = ReadOptionalColor(text, "ambient_back", AmbientBackHash);
                if (hasUniformAmbient || left.HasValue || right.HasValue || top.HasValue ||
                    bottom.HasValue || front.HasValue || back.HasValue)
                {
                    backgroundAmbientCube = new AmbientCubeLighting(
                        left ?? uniformAmbient,
                        right ?? uniformAmbient,
                        top ?? uniformAmbient,
                        bottom ?? uniformAmbient,
                        front ?? uniformAmbient,
                        back ?? uniformAmbient);
                }

                return new SceneLightSettings(
                    direction,
                    background,
                    character,
                    hemisphereRotation,
                    hemisphereFront,
                    hemisphereBack,
                    backgroundAmbientCube);
            }
        }
        return null;
    }

    private static bool TryRead(
        string command,
        string parameter,
        string hash,
        int count,
        out float[] values)
    {
        var match = Regex.Match(
            command,
            ParameterPattern(parameter, hash) + @"\s+" +
            string.Join(@"\s+", EnumerableRepeat(@"(-?\d+(?:\.\d+)?)", count)),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            values = [];
            return false;
        }

        values = new float[count];
        for (var index = 0; index < count; index++)
        {
            if (!float.TryParse(
                    match.Groups[index + 1].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out values[index]))
            {
                values = [];
                return false;
            }
        }
        return true;
    }

    private static string ParameterPattern(string name, string hash) =>
        $@"(?<![A-Za-z0-9_])-\s*(?:{Regex.Escape(name)}|[A-Za-z]?\[{hash}\])";

    private static Vector3? ReadOptionalColor(
        string command,
        string parameter,
        string hash) =>
        TryRead(command, parameter, hash, 3, out var values)
            ? ToColor(values)
            : null;

    private static Vector3 ToColor(float[] values) => new(
        values[0] / 1000f,
        values[1] / 1000f,
        values[2] / 1000f);

    private static Vector4 ToColorWithWeight(float[] values) => new(
        values[0] / 1000f,
        values[1] / 1000f,
        values[2] / 1000f,
        values[3] / 1000f);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static IEnumerable<string> EnumerableRepeat(string value, int count)
    {
        for (var index = 0; index < count; index++)
        {
            yield return value;
        }
    }

    // Normal Haven GCX output starts a command as "NewSystemLightSet \\" or
    // "[71E7D6] \\". The optional "command" form is retained for old fixtures
    // and manually decompiled scripts. Parameters can be on the same line or on
    // following lines.
    [GeneratedRegex(
        @"(?im)^[ \t]*(?:command[ \t]+)?(?:NewSystemLightSet\b|\[71E7D6\])(?<body>.*?)(?=^[ \t]*(?:command[ \t]+)?(?:[A-Za-z_][A-Za-z0-9_]*|\[[0-9A-F]{6}\])[ \t]*(?:\\)?[ \t]*(?:\r?\n|$)|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CommandRegex();
}
