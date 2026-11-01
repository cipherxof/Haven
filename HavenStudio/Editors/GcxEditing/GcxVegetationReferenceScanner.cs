using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HavenStudio.Formats.Gcx;

namespace HavenStudio.Editors.GcxEditing;

public sealed record GcxVegetationReference(
    uint ModelHash,
    uint PdlHash,
    float MinimumScale = 1f,
    float MaximumScale = 1f);

public static partial class GcxVegetationReferenceScanner
{
    public static IReadOnlyList<GcxVegetationReference> Scan(Gcx document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sites = new List<GcxPlacementSite>();
        ScanScript(document.MainScript?.Bytes, "main", sites);
        for (var index = 0; index < document.ScriptDefinitions.Count; index++)
        {
            ScanScript(document.ScriptDefinitions[index].Script?.Bytes, $"proc{index + 1}", sites);
        }

        return Scan(sites);
    }

    public static IReadOnlyList<GcxVegetationReference> Scan(IEnumerable<GcxPlacementSite> sites)
    {
        ArgumentNullException.ThrowIfNull(sites);
        return sites
            .Where(site => site.CommandHash == 0x1E20BF &&
                site.VegetationModelHash.HasValue &&
                site.VegetationPdlHash.HasValue)
            .Select(site =>
            {
                var scales = site.VegetationScaleValues;
                var minimumScale = scales.Count > 0 ? scales[0] / 100f : 1f;
                var maximumScale = scales.Count > 1 ? scales[1] / 100f : minimumScale;
                return new GcxVegetationReference(
                    site.VegetationModelHash!.Value,
                    site.VegetationPdlHash!.Value,
                    minimumScale,
                    maximumScale);
            })
            .Distinct()
            .OrderBy(reference => reference.PdlHash)
            .ThenBy(reference => reference.ModelHash)
            .ToArray();
    }

    private static void ScanScript(byte[]? bytes, string name, ICollection<GcxPlacementSite> sites)
    {
        if (bytes is { Length: > 0 })
        {
            GcxDecompiler.Decompile(bytes, name, isMgs3: false, sites);
        }
    }

    public static IReadOnlyList<GcxVegetationReference> Scan(IEnumerable<string> decompiledScripts)
    {
        ArgumentNullException.ThrowIfNull(decompiledScripts);
        var result = new HashSet<GcxVegetationReference>();
        foreach (var script in decompiledScripts)
        {
            var lines = script.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].Contains("NewGrassMng_03", StringComparison.OrdinalIgnoreCase) &&
                    !lines[index].Contains("[6592A7]", StringComparison.OrdinalIgnoreCase))
                    continue;

                var commandIndent = CountIndent(lines[index]);
                uint? modelHash = null;
                uint? pdlHash = null;
                float minimumScale = 1f;
                float maximumScale = 1f;
                var foundScale = false;
                for (var bodyIndex = index + 1; bodyIndex < lines.Length; bodyIndex++)
                {
                    var line = lines[bodyIndex];
                    if (line.Length == 0)
                        continue;
                    if (CountIndent(line) <= commandIndent)
                        break;
                    var modelMatch = ModelParameter().Match(line);
                    if (modelMatch.Success)
                        modelHash = Convert.ToUInt32(modelMatch.Groups[1].Value, 16);
                    var pdlMatch = PdlParameter().Match(line);
                    if (pdlMatch.Success)
                        pdlHash = Convert.ToUInt32(pdlMatch.Groups[1].Value, 16);
                    var scaleMatch = ScaleParameter().Match(line);
                    if (!foundScale && scaleMatch.Success)
                    {
                        minimumScale = Convert.ToSingle(scaleMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / 100f;
                        maximumScale = Convert.ToSingle(scaleMatch.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) / 100f;
                        foundScale = true;
                    }
                }
                if (modelHash.HasValue && pdlHash.HasValue)
                    result.Add(new GcxVegetationReference(modelHash.Value, pdlHash.Value, minimumScale, maximumScale));
            }
        }
        return result.OrderBy(reference => reference.PdlHash).ThenBy(reference => reference.ModelHash).ToArray();
    }

    private static int CountIndent(string line)
    {
        var count = 0;
        while (count < line.Length && char.IsWhiteSpace(line[count]))
            count++;
        return count;
    }

    [GeneratedRegex(@"^[ \t]+-m(?:\[[0-9A-Za-z_]{1,16}\])?[ \t]+\[([0-9A-Fa-f]{1,8})\]", RegexOptions.CultureInvariant)]
    private static partial Regex ModelParameter();

    [GeneratedRegex(@"^[ \t]+-p\[01CCEC\][ \t]+\[([0-9A-Fa-f]{1,8})\]", RegexOptions.CultureInvariant)]
    private static partial Regex PdlParameter();

    [GeneratedRegex(@"^[ \t]+-s\[6311EC\][ \t]+(-?[0-9]+(?:\.[0-9]+)?)[ \t]+(-?[0-9]+(?:\.[0-9]+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex ScaleParameter();
}
