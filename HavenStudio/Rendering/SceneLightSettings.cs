using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public sealed record SceneLightSettings(
    Vector3 Direction,
    Vector3 DirectionalColor,
    Vector3? AmbientColor);

public static partial class GcxSystemLightParser
{
    public static SceneLightSettings? Parse(IEnumerable<string> decompiledScripts)
    {
        ArgumentNullException.ThrowIfNull(decompiledScripts);
        foreach (var script in decompiledScripts)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                continue;
            }

            var command = CommandRegex().Match(script);
            if (!command.Success ||
                !TryRead(command.Value, "dir", 3, out var directionValues))
            {
                continue;
            }

            var direction = new Vector3(directionValues[0], directionValues[1], directionValues[2]);
            if (direction.LengthSquared <= 0.000001f)
            {
                continue;
            }
            direction = Vector3.Normalize(direction);

            var hasCharacterColor = TryRead(command.Value, "chara_color", 3, out var characterValues);
            var hasSceneColor = TryRead(command.Value, "color", 3, out var sceneValues);
            if (!hasCharacterColor && !hasSceneColor)
            {
                continue;
            }
            var directionalValues = hasCharacterColor ? characterValues : sceneValues;
            var directional = new Vector3(
                directionalValues[0], directionalValues[1], directionalValues[2]) / 1000f;

            Vector3? ambient = null;
            if (TryRead(command.Value, "hemispherelight_frontcolor", 4, out var front) &&
                TryRead(command.Value, "hemispherelight_backcolor", 4, out var back))
            {
                var frontColor = new Vector3(front[0], front[1], front[2]) / 1000f;
                var backColor = new Vector3(back[0], back[1], back[2]) / 1000f;
                var frontWeight = MathF.Max(0, front[3] / 1000f);
                var backWeight = MathF.Max(0, back[3] / 1000f);
                ambient = frontColor * frontWeight + backColor * backWeight;
            }

            return new SceneLightSettings(direction, directional, ambient);
        }
        return null;
    }

    private static bool TryRead(string command, string parameter, int count, out float[] values)
    {
        var match = Regex.Match(
            command,
            $@"(?<![A-Za-z0-9_])-{Regex.Escape(parameter)}\s+" +
            string.Join(@"\s+", EnumerableRepeat(@"(-?\d+(?:\.\d+)?)", count)),
            RegexOptions.CultureInvariant);
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

    private static IEnumerable<string> EnumerableRepeat(string value, int count)
    {
        for (var index = 0; index < count; index++)
        {
            yield return value;
        }
    }

    [GeneratedRegex(@"command\s+NewSystemLightSet\b(?<body>.*?)(?=\r?\n\s*command\s|\z)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CommandRegex();
}
