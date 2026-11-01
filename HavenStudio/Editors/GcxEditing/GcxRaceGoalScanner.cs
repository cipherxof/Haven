using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HavenStudio.Editors.GcxEditing;

public static partial class GcxRaceGoalScanner
{
    // MGO2 maps reuse this index-to-effect order, while each map supplies its
    // own A52C7F link rows. This is an index schema, not a shared link table.
    private static readonly uint[] StandardRaceGoalOrder =
    [
        0x31E379, 0x31E37A,
        0x8FB28D, 0x8FB28E, 0x8FB28F, 0x8FB290, 0x8FB291,
        0x8FB292, 0x8FB293, 0x8FB294, 0x8FB295, 0x8FB2AC
    ];

    public static IReadOnlyDictionary<uint, IReadOnlySet<uint>> FindGoalLinks(
        IEnumerable<string> decompiledScripts,
        IEnumerable<uint> knownRaceGoalHashes)
    {
        ArgumentNullException.ThrowIfNull(decompiledScripts);
        ArgumentNullException.ThrowIfNull(knownRaceGoalHashes);
        var scripts = decompiledScripts.ToArray();
        var known = knownRaceGoalHashes.ToHashSet();
        var indexedGoals = Array.Empty<uint>();

        foreach (var script in scripts)
        {
            foreach (Match data in ForeachDataParameter().Matches(script))
            {
                var values = HashValue().Matches(data.Groups[1].Value)
                    .Select(match => Convert.ToUInt32(match.Groups[1].Value, 16))
                    .ToArray();
                if (values.Length < 5 || values.Length % 5 != 0)
                {
                    continue;
                }

                var nodes = values.Where((_, index) => index % 5 == 0).ToArray();
                if (nodes.Count(known.Contains) > indexedGoals.Count(known.Contains))
                {
                    indexedGoals = nodes;
                }
            }
        }

        // Some live documents do not preserve the foreach registration payload
        // in decompiled text. The goal index schema itself is common across the
        // inspected retail/custom maps, so recover only that mapping; link rows
        // below still come exclusively from the currently loaded GCX.
        if (indexedGoals.Count(known.Contains) < StandardRaceGoalOrder.Length &&
            StandardRaceGoalOrder.All(known.Contains))
        {
            indexedGoals = StandardRaceGoalOrder;
        }

        if (indexedGoals.Length == 0)
        {
            return new Dictionary<uint, IReadOnlySet<uint>>();
        }

        var linksByIndex = new Dictionary<int, HashSet<int>>();
        foreach (var script in scripts)
        {
            foreach (Match command in GoalLinkCommand().Matches(script))
            {
                var baseIndex = int.Parse(command.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                linksByIndex[baseIndex] = NumberValue().Matches(command.Groups[2].Value)
                    .Select(match => int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture))
                    .ToHashSet();
            }
        }

        var result = new Dictionary<uint, IReadOnlySet<uint>>();
        foreach (var (baseIndex, links) in linksByIndex)
        {
            if (baseIndex < 0 || baseIndex >= indexedGoals.Length || !known.Contains(indexedGoals[baseIndex]))
            {
                continue;
            }
            result[indexedGoals[baseIndex]] = links
                .Where(index => index >= 0 && index < indexedGoals.Length)
                .Select(index => indexedGoals[index])
                .Where(known.Contains)
                .ToHashSet();
        }
        return result;
    }

    public static IReadOnlySet<uint> FindRegisteredGoals(
        IEnumerable<string> decompiledScripts,
        IEnumerable<uint> knownRaceGoalHashes)
    {
        ArgumentNullException.ThrowIfNull(decompiledScripts);
        ArgumentNullException.ThrowIfNull(knownRaceGoalHashes);
        var candidates = knownRaceGoalHashes.ToHashSet();
        var result = new HashSet<uint>();
        if (candidates.Count == 0)
        {
            return result;
        }

        foreach (var script in decompiledScripts)
        {
            foreach (Match data in ForeachDataParameter().Matches(script))
            {
                foreach (Match hash in HashValue().Matches(data.Groups[1].Value))
                {
                    var value = Convert.ToUInt32(hash.Groups[1].Value, 16);
                    if (candidates.Contains(value))
                    {
                        result.Add(value);
                    }
                }
            }

            // Some MGO2 foreach payloads are rendered with value tokens split
            // across lines or nested formatting that does not survive the
            // pretty-printer uniformly. Race base hashes are unique GCX effect
            // references, so retain a direct token fallback for known goals.
            foreach (var candidate in candidates)
            {
                if (script.Contains($"[{candidate:X6}]", StringComparison.OrdinalIgnoreCase) ||
                    script.Contains($"[{candidate:X8}]", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(candidate);
                }
            }
        }

        return result;
    }

    [GeneratedRegex(@"-(?:data|d\[3392E1\])(?:[ \t]*\\?[ \t]*\r?\n)?((?:[ \t]+\[[0-9A-Fa-f]{1,8}\])+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ForeachDataParameter();

    [GeneratedRegex(@"\[([0-9A-Fa-f]{1,8})\]", RegexOptions.CultureInvariant)]
    private static partial Regex HashValue();

    [GeneratedRegex(@"\[A52C7F\][ \t]*\\?[ \t]*\r?\n[ \t]*-(?:base|b\[3292C5\])[ \t]+([0-9]+)[ \t]*\\?[ \t]*\r?\n[ \t]*-(?:link|l\[37B22B\])[ \t]+((?:[0-9]+[ \t]*)+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex GoalLinkCommand();

    [GeneratedRegex(@"[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NumberValue();
}
