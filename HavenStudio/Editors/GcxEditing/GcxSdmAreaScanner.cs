using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace HavenStudio.Editors.GcxEditing;

public sealed record GcxSdmAreaReference(
    string DefinitionScript,
    string CallerScript,
    uint PropertyDirectoryHash,
    int AreaMinimum,
    int AreaMaximum,
    int AreaTime,
    int AreaTimer,
    int AreaMove,
    int AreaChange)
{
    // SDM area values are expressed in thousands of GEOM world units. This is
    // consistent across the retail VV (150), custom EE (130), and JJ (150)
    // definitions and their corresponding stage extents.
    public const float WorldUnitsPerAreaUnit = 1000f;
    public float MinimumRadius => AreaMinimum * WorldUnitsPerAreaUnit;
    public float StartingRadius => AreaMaximum * WorldUnitsPerAreaUnit;
}

public static partial class GcxSdmAreaScanner
{
    private const string PropDir = "34BAF7";
    private const string AreaMin = "4466DE";
    private const string AreaMax = "4465E8";
    private const string AreaTime = "905C0D";
    private const string AreaTimer = "0B8224";
    private const string AreaMove = "8CF52D";
    private const string AreaChange = "5A6764";

    public static IReadOnlyList<GcxSdmAreaReference> Scan(
        IReadOnlyDictionary<string, string> scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);
        var result = new List<GcxSdmAreaReference>();
        foreach (var (definitionName, text) in scripts)
        {
            if (!TryParameter(text, 'p', PropDir, "prop_dir", out var directoryToken) ||
                !TryParameter(text, 'a', AreaMin, "area_min", out var minimumToken) ||
                !TryParameter(text, 'a', AreaMax, "area_max", out var maximumToken))
            {
                continue;
            }

            TryParameter(text, 'a', AreaTime, "area_time", out var timeToken);
            TryParameter(text, 'a', AreaTimer, "area_timer", out var timerToken);
            TryParameter(text, 'a', AreaMove, "area_move", out var moveToken);
            TryParameter(text, 'a', AreaChange, "area_change", out var changeToken);

            var argumentSets = UsesArguments(
                    directoryToken, minimumToken, maximumToken,
                    timeToken, timerToken, moveToken, changeToken)
                ? FindCalls(scripts, definitionName)
                : new[] { (Caller: definitionName, Arguments: Array.Empty<string>()) };

            foreach (var (caller, arguments) in argumentSets)
            {
                if (!TryResolveHash(directoryToken, arguments, out var directoryHash) ||
                    !TryResolveInt(minimumToken, arguments, out var minimum) ||
                    !TryResolveInt(maximumToken, arguments, out var maximum) ||
                    maximum <= 0 || minimum < 0 || minimum > maximum)
                {
                    continue;
                }

                result.Add(new GcxSdmAreaReference(
                    definitionName,
                    caller,
                    directoryHash,
                    minimum,
                    maximum,
                    ResolveOptional(timeToken, arguments),
                    ResolveOptional(timerToken, arguments),
                    ResolveOptional(moveToken, arguments),
                    ResolveOptional(changeToken, arguments)));
            }
        }

        return result.Distinct().ToArray();
    }

    private static bool TryParameter(
        string text,
        char prefix,
        string hash,
        string resolvedName,
        out string token)
    {
        var match = Regex.Match(
            text,
            $@"-(?:{Regex.Escape(resolvedName)}|{prefix}\[{hash}\])\s+(?<value>\$arg\d+|\[[0-9A-Fa-f]+\]|-?\d+)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        token = match.Success ? match.Groups["value"].Value : string.Empty;
        return match.Success;
    }

    private static IReadOnlyList<(string Caller, string[] Arguments)> FindCalls(
        IReadOnlyDictionary<string, string> scripts,
        string definitionName)
    {
        var calls = new List<(string Caller, string[] Arguments)>();
        var pattern = new Regex(
            $@"@{Regex.Escape(definitionName)}(?<arguments>[^\r\n}}]*)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        foreach (var (caller, text) in scripts)
        {
            foreach (Match match in pattern.Matches(text))
            {
                var arguments = ValueTokenPattern().Matches(match.Groups["arguments"].Value)
                    .Select(value => value.Value)
                    .ToArray();
                calls.Add((caller, arguments));
            }
        }
        return calls;
    }

    private static bool UsesArguments(params string[] tokens) =>
        tokens.Any(token => token.StartsWith("$arg", StringComparison.OrdinalIgnoreCase));

    private static int ResolveOptional(string token, IReadOnlyList<string> arguments) =>
        TryResolveInt(token, arguments, out var value) ? value : 0;

    private static bool TryResolveHash(
        string token,
        IReadOnlyList<string> arguments,
        out uint value)
    {
        token = ResolveArgument(token, arguments);
        if (token.Length >= 3 && token[0] == '[' && token[^1] == ']')
        {
            return uint.TryParse(
                token.AsSpan(1, token.Length - 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out value);
        }
        return uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryResolveInt(
        string token,
        IReadOnlyList<string> arguments,
        out int value)
    {
        token = ResolveArgument(token, arguments);
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string ResolveArgument(string token, IReadOnlyList<string> arguments)
    {
        if (!token.StartsWith("$arg", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(token.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var oneBased) ||
            oneBased <= 0 || oneBased > arguments.Count)
        {
            return token;
        }
        return arguments[oneBased - 1];
    }

    [GeneratedRegex(@"\[[0-9A-Fa-f]+\]|\$arg\d+|-?\d+", RegexOptions.CultureInvariant)]
    private static partial Regex ValueTokenPattern();
}
