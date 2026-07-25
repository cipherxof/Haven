using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Editors.Lighting;

/// <summary>
/// Resolves the LT2/LT3 that actually belongs to the loaded stage.
///
/// MGS4 archives frequently contain several lighting files together, including
/// MGS4_Preview.lt3 and sky passes. Selecting the first file alphabetically is
/// therefore not reliable. The resolver combines direct stage-name/hash evidence,
/// the dominant model family present beside the map, and the structural richness
/// of each LIT document. File size is only the final tie-breaker.
/// </summary>
public static class StageLightResolver
{
    private static readonly string[] ReferenceExtensions = [".gcx", ".cnf"];
    private static readonly string[] AssetExtensions = [".mdn", ".txn", ".vlm", ".octt"];

    public static StageLightSelection Resolve(
        IWorkspaceCatalog workspace,
        IReadOnlyList<LitDocumentSession> documents,
        string? preferredStageStem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            return StageLightSelection.Empty;
        }

        var snapshot = workspace.Snapshot;
        var normalizedStage = NormalizeStem(preferredStageStem);
        var stageFamily = ExtractAssetFamily(normalizedStage);
        var referencePayloads = LoadReferencePayloads(workspace, snapshot, cancellationToken);
        var familyCounts = BuildFamilyCounts(snapshot);

        var evaluated = documents
            .Select(document => Evaluate(
                document,
                normalizedStage,
                stageFamily,
                referencePayloads,
                familyCounts,
                cancellationToken))
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.GroupCount)
            .ThenByDescending(candidate => candidate.LightCount)
            .ThenByDescending(candidate => candidate.ByteLength)
            .ThenBy(candidate => candidate.Document.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = evaluated[0];
        return new StageLightSelection(
            selected.Document,
            selected.BuildReason(),
            evaluated);
    }

    private static StageLightCandidate Evaluate(
        LitDocumentSession document,
        string normalizedStage,
        string stageFamily,
        IReadOnlyList<byte[]> referencePayloads,
        IReadOnlyDictionary<FamilyLocation, int> familyCounts,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stem = NormalizeStem(Path.GetFileNameWithoutExtension(document.DisplayName));
        var family = ExtractAssetFamily(stem);
        var groupCount = document.Document.Groups.Count;
        var lightCount = document.Document.Groups.Sum(group => group.Lights.Count);
        var score = 0L;
        var evidence = new List<string>();

        if (document.IsPreviewPass)
        {
            score -= 1_000_000;
            evidence.Add("generic preview excluded");
        }

        if (document.IsSkyPass)
        {
            score -= 250_000;
            evidence.Add("sky pass");
        }

        if (normalizedStage.Length > 0)
        {
            if (stem.Equals(normalizedStage, StringComparison.OrdinalIgnoreCase))
            {
                score += 300_000;
                evidence.Add($"exact stage name {normalizedStage}");
            }
            else if (stem.StartsWith(normalizedStage, StringComparison.OrdinalIgnoreCase) ||
                     normalizedStage.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
            {
                score += 140_000;
                evidence.Add($"stage-name prefix {normalizedStage}");
            }
        }

        if (stageFamily.Length > 0 && family.Equals(stageFamily, StringComparison.OrdinalIgnoreCase))
        {
            score += 80_000;
            evidence.Add($"stage family {family}");
        }

        var location = FamilyLocation.For(document.Path, family);
        familyCounts.TryGetValue(location, out var colocatedFamilyAssets);
        if (colocatedFamilyAssets > 0)
        {
            var familyBonus = Math.Min(colocatedFamilyAssets, 400) * 500L;
            score += familyBonus;
            evidence.Add($"{colocatedFamilyAssets} matching assets in {LocationName(document.Path)}");
        }

        var globalFamilyAssets = familyCounts
            .Where(pair => pair.Key.Family.Equals(family, StringComparison.OrdinalIgnoreCase))
            .Sum(pair => pair.Value);
        if (globalFamilyAssets > colocatedFamilyAssets)
        {
            score += Math.Min(globalFamilyAssets - colocatedFamilyAssets, 200) * 100L;
        }

        var reference = FindReference(document.DisplayName, stem, referencePayloads);
        switch (reference)
        {
            case ReferenceStrength.Ascii:
                score += 500_000;
                evidence.Add("explicit filename reference");
                break;
            case ReferenceStrength.StrongHash:
                score += 220_000;
                evidence.Add("explicit 24-bit hash reference");
                break;
            case ReferenceStrength.WeakHash:
                score += 35_000;
                evidence.Add("possible packed hash reference");
                break;
        }

        // A real stage LT normally contains many groups and records. This safely
        // rejects tiny preview definitions even when their filenames are unusual.
        score += Math.Min(groupCount, 2_000) * 250L;
        score += Math.Min(lightCount, 20_000) * 20L;

        // Size is intentionally low weight: it is useful as a tie-breaker, not as
        // the primary mapping rule. A large unrelated sky/preview file must not win.
        score += Math.Min(document.OriginalBytes.LongLength, 4_000_000) / 256L;

        if (groupCount > 0)
        {
            evidence.Add($"{groupCount} groups / {lightCount} lights");
        }
        evidence.Add($"{document.OriginalBytes.LongLength:N0} bytes");

        return new StageLightCandidate(
            document,
            score,
            family,
            colocatedFamilyAssets,
            groupCount,
            lightCount,
            document.OriginalBytes.LongLength,
            evidence);
    }

    private static IReadOnlyDictionary<FamilyLocation, int> BuildFamilyCounts(WorkspaceSnapshot? snapshot)
    {
        var counts = new Dictionary<FamilyLocation, int>();
        if (snapshot == null)
        {
            return counts;
        }

        foreach (var file in snapshot.Files)
        {
            if (!AssetExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var family = ExtractAssetFamily(NormalizeStem(Path.GetFileNameWithoutExtension(file.Name)));
            if (family.Length == 0)
            {
                continue;
            }

            var key = FamilyLocation.For(file.Path, family);
            counts.TryGetValue(key, out var current);
            counts[key] = current + 1;
        }

        return counts;
    }

    private static IReadOnlyList<byte[]> LoadReferencePayloads(
        IWorkspaceCatalog workspace,
        WorkspaceSnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot == null)
        {
            return [];
        }

        var payloads = new List<byte[]>();
        foreach (var file in snapshot.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase) ||
                file.Length <= 0 || file.Length > 16 * 1024 * 1024)
            {
                continue;
            }

            try
            {
                payloads.Add(workspace.ReadAllBytes(file.Path));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                // Discovery must remain usable when an unrelated script/config file
                // cannot be read. Structural and family evidence still apply.
            }
        }

        return payloads;
    }

    private static ReferenceStrength FindReference(
        string fileName,
        string stem,
        IReadOnlyList<byte[]> payloads)
    {
        if (payloads.Count == 0 || stem.Length == 0)
        {
            return ReferenceStrength.None;
        }

        var names = new[]
        {
            Encoding.ASCII.GetBytes(fileName),
            Encoding.ASCII.GetBytes(fileName.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(stem),
            Encoding.ASCII.GetBytes(stem.ToLowerInvariant())
        };
        foreach (var payload in payloads)
        {
            foreach (var name in names)
            {
                if (name.Length > 0 && Contains(payload, name))
                {
                    return ReferenceStrength.Ascii;
                }
            }
        }

        var hashNames = new[]
        {
            stem,
            stem.ToLowerInvariant(),
            fileName,
            fileName.ToLowerInvariant()
        }.Distinct(StringComparer.Ordinal).ToArray();
        var strongPatterns = new List<byte[]>();
        var weakPatterns = new List<byte[]>();
        foreach (var hashName in hashNames)
        {
            var hash = HavenStudio.Utils.String.HashString(hashName);
            var high = (byte)(hash >> 16);
            var middle = (byte)(hash >> 8);
            var low = (byte)hash;
            strongPatterns.Add([0, high, middle, low]);
            strongPatterns.Add([low, middle, high, 0]);
            weakPatterns.Add([high, middle, low]);
            weakPatterns.Add([low, middle, high]);
        }

        foreach (var payload in payloads)
        {
            if (strongPatterns.Any(pattern => Contains(payload, pattern)))
            {
                return ReferenceStrength.StrongHash;
            }
        }
        foreach (var payload in payloads)
        {
            if (weakPatterns.Any(pattern => Contains(payload, pattern)))
            {
                return ReferenceStrength.WeakHash;
            }
        }

        return ReferenceStrength.None;
    }

    private static bool Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        return needle.Length > 0 && haystack.IndexOf(needle) >= 0;
    }

    public static string NormalizeStem(string? stem)
    {
        var normalized = (stem ?? string.Empty).Trim().ToLowerInvariant();
        string[] suffixes = ["_sky_d", "_sky", "_d"];
        foreach (var suffix in suffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                return normalized[..^suffix.Length];
            }
        }
        return normalized;
    }

    public static string ExtractAssetFamily(string? stem)
    {
        var normalized = NormalizeStem(stem);
        if (normalized.Length >= 4 &&
            char.IsLetter(normalized[0]) &&
            char.IsDigit(normalized[1]) &&
            char.IsDigit(normalized[2]) &&
            char.IsLetter(normalized[3]))
        {
            // s01a10a.lt3 -> s01a, matching s01a*.mdn assets.
            return normalized[..4];
        }

        if (normalized.Length >= 5 &&
            char.IsLetter(normalized[0]) &&
            char.IsDigit(normalized[1]) &&
            char.IsDigit(normalized[2]) &&
            char.IsDigit(normalized[3]) &&
            char.IsLetter(normalized[4]))
        {
            // n021a.geom -> n021a.
            return normalized[..5];
        }

        var separator = normalized.IndexOf('_');
        return separator > 0 ? normalized[..separator] : normalized;
    }

    private static string LocationName(WorkspacePath path) => path.IsArchiveEntry
        ? Path.GetFileName(path.PhysicalPath)
        : Path.GetDirectoryName(path.PhysicalPath) ?? "workspace";

    private enum ReferenceStrength
    {
        None,
        WeakHash,
        StrongHash,
        Ascii
    }

    private readonly record struct FamilyLocation(string PhysicalPath, string Family)
    {
        public static FamilyLocation For(WorkspacePath path, string family) => new(
            path.IsArchiveEntry
                ? path.PhysicalPath.ToLowerInvariant()
                : (Path.GetDirectoryName(path.PhysicalPath) ?? path.PhysicalPath).ToLowerInvariant(),
            family.ToLowerInvariant());
    }
}

public sealed record StageLightSelection(
    LitDocumentSession? Primary,
    string Reason,
    IReadOnlyList<StageLightCandidate> Candidates)
{
    public static StageLightSelection Empty { get; } = new(null, "no lighting file", []);
}

public sealed record StageLightCandidate(
    LitDocumentSession Document,
    long Score,
    string Family,
    int ColocatedFamilyAssets,
    int GroupCount,
    int LightCount,
    long ByteLength,
    IReadOnlyList<string> Evidence)
{
    public string BuildReason()
    {
        var useful = Evidence
            .Where(item => !item.Equals("generic preview excluded", StringComparison.OrdinalIgnoreCase))
            .Take(4);
        return string.Join(", ", useful);
    }
}
