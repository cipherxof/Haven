using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Geo;
using HavenStudio.Utils;
using OpenTK.Mathematics;
using Serilog;

namespace HavenStudio.Editors.GcxEditing;

public sealed class PlacedModelReference
{
    public uint ModelHash { get; set; }
    public Vector3? Position { get; set; }
    public Vector3? Rotation { get; set; }
    public uint? EffectHash { get; set; }
    public GeoEffect? SourceEffect { get; set; }
    public uint? CollisionReferenceHash { get; set; }
    public uint? PropertyPositionHash { get; set; }
    public GcxPlacementBinding? Binding { get; set; }
    public List<uint> AdditionalModelHashes { get; } = [];
    internal int? ForeachGroupIndex { get; set; }
}

public sealed class GcxPlacementBinding
{
    public GcxPlacementBinding(
        GcxScript script,
        string scriptName,
        GcxPlacementSite site,
        GcxStringCodeSite? modelSite = null,
        GcxStringCodeSite? transformSourceSite = null,
        GcxStringCodeSite? collisionReferenceSite = null,
        int? foreachRowIndex = null)
    {
        Script = script ?? throw new ArgumentNullException(nameof(script));
        ScriptName = scriptName ?? throw new ArgumentNullException(nameof(scriptName));
        Site = site ?? throw new ArgumentNullException(nameof(site));
        ModelSite = modelSite ?? site.Model;
        TransformSourceSite = transformSourceSite ?? site.Effect ?? site.PropertyPosition;
        CollisionReferenceSite = collisionReferenceSite ?? site.CollisionReference;
        ForeachRowIndex = foreachRowIndex;
    }

    public GcxScript Script { get; }
    public string ScriptName { get; }
    public GcxPlacementSite Site { get; set; }
    public GcxStringCodeSite? ModelSite { get; set; }
    public GcxStringCodeSite? TransformSourceSite { get; set; }
    public GcxStringCodeSite? CollisionReferenceSite { get; set; }
    public int? ForeachRowIndex { get; }
}

public sealed class GcxModelReferences
{
    public static GcxModelReferences Empty { get; } = new(new HashSet<uint>(), Array.Empty<PlacedModelReference>());

    public GcxModelReferences(
        IReadOnlySet<uint> stageModelHashes,
        IReadOnlyList<PlacedModelReference> placedModels)
    {
        StageModelHashes = stageModelHashes;
        PlacedModels = placedModels;
    }

    public IReadOnlySet<uint> StageModelHashes { get; }
    public IReadOnlyList<PlacedModelReference> PlacedModels { get; }

    public IEnumerable<uint> RequiredModelHashes()
    {
        foreach (var hash in StageModelHashes)
        {
            yield return hash;
        }

        foreach (var placed in PlacedModels)
        {
            if (placed.ModelHash != 0)
            {
                yield return placed.ModelHash;
            }

            foreach (var hash in placed.AdditionalModelHashes)
            {
                if (hash != 0)
                {
                    yield return hash;
                }
            }
        }
    }
}

public sealed class GcxModelReferenceScanner
{
    private const uint CollisionBodyParameterHash = 0x30BDB7;
    private const uint CollisionParameterHash = 0x31C62D;
    private const uint EffectParameterHash = 0x01A134;
    private static readonly ILogger Log = Serilog.Log.ForContext<GcxModelReferenceScanner>();
    private static readonly IReadOnlyList<string> ModelPlacementCommands =
        GcxPlacementCommandCatalog.AuditedCommands
            .Where(command => command.IsModelPlacement)
            .Select(command => command.Name)
            .ToArray();

    private static readonly Regex ModelParameter = new(
        @"^\s+-model\s+(\S+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex PositionParameter = new(
        @"^\s+-pos\s+(\S+)\s+(\S+)\s+(\S+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex DirectionParameter = new(
        @"^\s+-dir\s+(\S+)\s+(\S+)\s+(\S+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex EffectParameter = new(
        $@"^\s+-(?:eft|e|e\[{EffectParameterHash:X6}\])\s+(\S+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ReferenceParameter = new(
        $@"^[ \t]+-(?:ref|collision|b\[{CollisionBodyParameterHash:X6}\])[ \t]+([^\s\\]+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex TestTreeCollisionParameter = new(
        $@"^[ \t]+-(?:collision|c\[{CollisionParameterHash:X6}\])[ \t]+([^\s\\]+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex GlassModelParameter = new(
        @"^\s+-model_glass\s+(\S+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex CharaBlock = new(
        @"-exec\s+proc\s*\{(.*?)\}",
        RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex CharaCommand = new(
        @"^\s+chara\s+\S+(?:\s+\S+)?(.*?)(?=^\s+chara\s+|^\s*\}|$)",
        RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ForeachData = new(
        @"^\s+-(?:data|d\[3392E1\])\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ForeachArgumentCount = new(
        @"^\s+-(?:argc|a\[325543\])\s+(\d+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ForeachCommand = new(
        @"^[ \t]*(?:(?:command|\[082BC9\])[ \t]+(?:foreach|NewForeach|\[542B2D\]))(?=[ \t\\\r\n]|$)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ForeachExec = new(
        @"^[ \t]+-(?:exec|e\[346D03\])[ \t]+proc\b",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex PropertyPositionParameter = new(
        $@"^[ \t]+-(?:prop_pos|p\[{Utils.String.HashString("prop_pos"):X6}\])[ \t]+([^\s\\]+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex PropertyParameter = new(
        $@"^[ \t]+-(?:prop|p\[{Utils.String.HashString("prop"):X6}\])[ \t]+([^\s\\]+)(?:[ \t]+([^\s\\]+))?",
        RegexOptions.Multiline | RegexOptions.Compiled);
    public GcxModelReferences Scan(
        Gcx document,
        GeomFile? geometry,
        bool isMgs3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var scripts = new List<RecordedScript>();

        ScanScript(document.MainScript, "main");
        for (var index = 0; index < document.ScriptDefinitions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanScript(document.ScriptDefinitions[index].Script, $"proc{index + 1}");
        }

        var stageHashes = new HashSet<uint>();
        var placedModels = new List<PlacedModelReference>();
        var effects = BuildEffectLookup(geometry);
        foreach (var recorded in scripts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localReferences = ScanDecompiledScripts(
                [recorded.Decompiled],
                effects,
                cancellationToken);
            stageHashes.UnionWith(localReferences.StageModelHashes);
            BindRecordedSites(
                localReferences.PlacedModels,
                recorded.Script,
                recorded.Name,
                recorded.Sites);
            placedModels.AddRange(localReferences.PlacedModels);
        }
        return new GcxModelReferences(stageHashes, placedModels);

        void ScanScript(GcxScript? script, string scriptName)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = script?.Bytes;
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            var sites = new List<GcxPlacementSite>();
            var decompiled = GcxDecompiler.Decompile(bytes, scriptName, isMgs3, sites);
            scripts.Add(new RecordedScript(script!, scriptName, decompiled, sites));
        }
    }

    public GcxModelReferences ScanDecompiledScripts(
        IEnumerable<string> decompiledScripts,
        GeomFile? geometry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decompiledScripts);
        return ScanDecompiledScripts(
            decompiledScripts,
            BuildEffectLookup(geometry),
            cancellationToken);
    }

    public GcxModelReferences ScanDecompiledScriptsWithEffects(
        IEnumerable<string> decompiledScripts,
        IEnumerable<GeoEffect> effects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decompiledScripts);
        ArgumentNullException.ThrowIfNull(effects);
        return ScanDecompiledScripts(
            decompiledScripts,
            BuildEffectLookup(effects),
            cancellationToken);
    }

    private static GcxModelReferences ScanDecompiledScripts(
        IEnumerable<string> decompiledScripts,
        EffectLookup effects,
        CancellationToken cancellationToken)
    {
        var stageHashes = new HashSet<uint>();
        var placedModels = new List<PlacedModelReference>();
        foreach (var decompiled in decompiledScripts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(decompiled))
            {
                continue;
            }

            ScanStageModels(decompiled, stageHashes, cancellationToken);
            ScanPlacedModels(decompiled, placedModels, effects, cancellationToken);
            ScanForeachModels(decompiled, placedModels, effects, cancellationToken);
        }

        return new GcxModelReferences(stageHashes, placedModels);
    }

    private static void ScanStageModels(
        string decompiled,
        ISet<uint> stageHashes,
        CancellationToken cancellationToken)
    {
        var foreachBodies = FindForeachBodyRanges(decompiled).ToList();
        foreach (var commandBlock in EnumerateCommandBlocks(decompiled, "NewPutStageModelSet", trackBraces: false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (foreachBodies.Any(range => commandBlock.Index > range.Start && commandBlock.Index < range.End))
            {
                continue;
            }
            if (EffectParameter.IsMatch(commandBlock.Content) || PositionParameter.IsMatch(commandBlock.Content))
            {
                continue;
            }
            foreach (Match match in ModelParameter.Matches(commandBlock.Content))
            {
                AddHash(stageHashes, match.Groups[1].Value);
            }
        }

        foreach (var foreachBlock in EnumerateForeachBlocks(decompiled))
        {
            foreach (var commandBlock in EnumerateCommandBlocks(
                foreachBlock.ExecBody,
                "NewPutStageModelSet",
                trackBraces: true))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (EffectParameter.IsMatch(commandBlock.Content) ||
                    PositionParameter.IsMatch(commandBlock.Content) ||
                    PropertyPositionParameter.IsMatch(commandBlock.Content) ||
                    PropertyParameter.IsMatch(commandBlock.Content))
                {
                    continue;
                }

                var modelMatch = ModelParameter.Match(commandBlock.Content);
                if (!modelMatch.Success)
                {
                    continue;
                }
                for (var dataIndex = 0;
                    dataIndex + foreachBlock.ArgumentCount <= foreachBlock.Values.Count;
                    dataIndex += foreachBlock.ArgumentCount)
                {
                    var hash = ResolveArgumentHash(
                        modelMatch.Groups[1].Value,
                        foreachBlock.Values,
                        dataIndex,
                        foreachBlock.ArgumentCount);
                    if (hash != 0)
                    {
                        stageHashes.Add(hash);
                    }
                }
            }
        }
    }

    private static void ScanPlacedModels(
        string decompiled,
        ICollection<PlacedModelReference> placedModels,
        EffectLookup effects,
        CancellationToken cancellationToken)
    {
        var foreachBodies = FindForeachBodyRanges(decompiled).ToList();
        foreach (var commandName in ModelPlacementCommands)
        {
            foreach (var commandBlock in EnumerateCommandBlocks(decompiled, commandName, trackBraces: true))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (foreachBodies.Any(range => commandBlock.Index > range.Start && commandBlock.Index < range.End))
                {
                    continue;
                }

                var block = commandBlock.Content;
                var placed = new PlacedModelReference();

                var modelMatch = ModelParameter.Match(block);
                if (modelMatch.Success)
                {
                    placed.ModelHash = ParseHash(modelMatch.Groups[1].Value);
                }

                var positionMatch = PositionParameter.Match(block);
                if (positionMatch.Success &&
                    int.TryParse(positionMatch.Groups[1].Value, out var x) &&
                    int.TryParse(positionMatch.Groups[2].Value, out var z) &&
                    int.TryParse(positionMatch.Groups[3].Value, out var y))
                {
                    placed.Position = new Vector3(x, y, z);
                }

                var directionMatch = DirectionParameter.Match(block);
                if (directionMatch.Success &&
                    int.TryParse(directionMatch.Groups[1].Value, out var rotationX) &&
                    int.TryParse(directionMatch.Groups[2].Value, out var rotationY) &&
                    int.TryParse(directionMatch.Groups[3].Value, out var rotationZ))
                {
                    // The engine consumes these as signed 16-bit X/Y/Z game angles.
                    placed.Rotation = ScriptDirectionToRadians(rotationX, rotationY, rotationZ);
                }

                var referenceMatch = commandName == "NewTestTree_02"
                    ? TestTreeCollisionParameter.Match(block)
                    : ReferenceParameter.Match(block);
                if (referenceMatch.Success)
                {
                    var hash = ParseHash(referenceMatch.Groups[1].Value);
                    placed.CollisionReferenceHash = hash == 0 ? null : hash;
                }

                AddAdditionalModel(placed, GlassModelParameter.Match(block));
                var execMatch = CharaBlock.Match(block);
                if (execMatch.Success)
                {
                    foreach (Match charaMatch in CharaCommand.Matches(execMatch.Groups[1].Value))
                    {
                        var chara = charaMatch.Groups[0].Value;
                        AddAdditionalModel(placed, ModelParameter.Match(chara));
                        AddAdditionalModel(placed, GlassModelParameter.Match(chara));
                    }
                }

                var effectMatch = EffectParameter.Match(block);
                if (effectMatch.Success)
                {
                    var hash = ParseHash(effectMatch.Groups[1].Value);
                    placed.EffectHash = hash == 0 ? null : hash;

                    // NewPutObject tests -eft before -pos. When -eft is present the
                    // effect transform owns placement, even if the command also has
                    // literal position/direction parameters.
                    placed.Position = null;
                    placed.Rotation = null;
                    ResolveTransformFromEffect(placed, hash, effects);
                }

                IReadOnlyList<PlacedModelReference> resolvedPlacements = [placed];
                if (!effectMatch.Success &&
                    placed.Position == null &&
                    TryGetPropertyReference(block, out var property))
                {
                    resolvedPlacements = ResolvePropertyPlacements(placed, property, effects);
                }

                foreach (var resolvedPlacement in resolvedPlacements)
                {
                    var isPrimaryObjectCommand = commandName == "NewPutObject";
                    var hasTransform = resolvedPlacement.Position != null ||
                        resolvedPlacement.Rotation != null ||
                        resolvedPlacement.EffectHash != null ||
                        resolvedPlacement.PropertyPositionHash != null ||
                        resolvedPlacement.SourceEffect != null;
                    if ((isPrimaryObjectCommand || hasTransform) &&
                        (resolvedPlacement.ModelHash != 0 ||
                         resolvedPlacement.AdditionalModelHashes.Count > 0))
                    {
                        placedModels.Add(resolvedPlacement);
                    }
                }
            }
        }
    }

    private static void ScanForeachModels(
        string decompiled,
        ICollection<PlacedModelReference> placedModels,
        EffectLookup effects,
        CancellationToken cancellationToken)
    {
        var foreachGroupIndex = 0;
        foreach (var foreachBlock in EnumerateForeachBlocks(decompiled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execBody = foreachBlock.ExecBody;
            var modelMatch = ModelParameter.Match(execBody);
            if (!modelMatch.Success)
            {
                continue;
            }

            var glassMatch = GlassModelParameter.Match(execBody);
            var isPrimaryObjectCommand = execBody.Contains("NewPutObject", StringComparison.Ordinal);
            for (var dataIndex = 0;
                dataIndex + foreachBlock.ArgumentCount <= foreachBlock.Values.Count;
                dataIndex += foreachBlock.ArgumentCount)
            {
                var rowGroupIndex = foreachGroupIndex++;
                var modelHash = ResolveArgumentHash(
                    modelMatch.Groups[1].Value,
                    foreachBlock.Values,
                    dataIndex,
                    foreachBlock.ArgumentCount);
                if (modelHash == 0)
                {
                    continue;
                }
                var placed = new PlacedModelReference { ModelHash = modelHash };
                placed.ForeachGroupIndex = rowGroupIndex;
                if (glassMatch.Success)
                {
                    AddAdditionalModel(
                        placed,
                        ResolveArgumentHash(
                            glassMatch.Groups[1].Value,
                            foreachBlock.Values,
                            dataIndex,
                            foreachBlock.ArgumentCount));
                }

                var positionMatch = PositionParameter.Match(execBody);
                if (positionMatch.Success &&
                    TryResolveArgumentInt(positionMatch.Groups[1].Value, foreachBlock, dataIndex, out var x) &&
                    TryResolveArgumentInt(positionMatch.Groups[2].Value, foreachBlock, dataIndex, out var z) &&
                    TryResolveArgumentInt(positionMatch.Groups[3].Value, foreachBlock, dataIndex, out var y))
                {
                    placed.Position = new Vector3(x, y, z);
                }

                var directionMatch = DirectionParameter.Match(execBody);
                if (directionMatch.Success &&
                    TryResolveArgumentInt(directionMatch.Groups[1].Value, foreachBlock, dataIndex, out var rotationX) &&
                    TryResolveArgumentInt(directionMatch.Groups[2].Value, foreachBlock, dataIndex, out var rotationY) &&
                    TryResolveArgumentInt(directionMatch.Groups[3].Value, foreachBlock, dataIndex, out var rotationZ))
                {
                    placed.Rotation = ScriptDirectionToRadians(rotationX, rotationY, rotationZ);
                }

                var referenceMatch = execBody.Contains("NewTestTree_02", StringComparison.Ordinal)
                    ? TestTreeCollisionParameter.Match(execBody)
                    : ReferenceParameter.Match(execBody);
                if (referenceMatch.Success)
                {
                    var hash = ResolveArgumentHash(
                        referenceMatch.Groups[1].Value,
                        foreachBlock.Values,
                        dataIndex,
                        foreachBlock.ArgumentCount);
                    placed.CollisionReferenceHash = hash == 0 ? null : hash;
                }

                var effectMatch = EffectParameter.Match(execBody);
                if (effectMatch.Success)
                {
                    var hash = ResolveArgumentHash(
                        effectMatch.Groups[1].Value,
                        foreachBlock.Values,
                        dataIndex,
                        foreachBlock.ArgumentCount);
                    placed.EffectHash = hash == 0 ? null : hash;
                    placed.Position = null;
                    placed.Rotation = null;
                    ResolveTransformFromEffect(placed, hash, effects);
                }

                IReadOnlyList<PlacedModelReference> resolvedPlacements = [placed];
                if (!effectMatch.Success &&
                    placed.Position == null &&
                    TryGetPropertyReference(
                        execBody,
                        foreachBlock,
                        dataIndex,
                        out var property))
                {
                    resolvedPlacements = ResolvePropertyPlacements(placed, property, effects);
                }

                foreach (var resolvedPlacement in resolvedPlacements)
                {
                    var hasTransform = resolvedPlacement.Position != null ||
                        resolvedPlacement.Rotation != null || resolvedPlacement.EffectHash != null ||
                        resolvedPlacement.PropertyPositionHash != null ||
                        resolvedPlacement.SourceEffect != null;
                    if (isPrimaryObjectCommand || hasTransform)
                    {
                        placedModels.Add(resolvedPlacement);
                    }
                }
            }
        }
    }

    private static IEnumerable<ForeachBlock> EnumerateForeachBlocks(string decompiled)
    {
        var cursor = 0;
        while (cursor < decompiled.Length)
        {
            var commandMatch = ForeachCommand.Match(decompiled, cursor);
            if (!commandMatch.Success)
            {
                yield break;
            }

            var commandIndex = commandMatch.Index;
            var nextCommandMatch = ForeachCommand.Match(decompiled, commandIndex + commandMatch.Length);
            var execMatch = ForeachExec.Match(decompiled, commandIndex + commandMatch.Length);
            var execBelongsToCommand = execMatch.Success &&
                (!nextCommandMatch.Success || execMatch.Index < nextCommandMatch.Index);
            var openBrace = execBelongsToCommand
                ? decompiled.IndexOf('{', execMatch.Index + execMatch.Length)
                : -1;
            var closeBrace = openBrace < 0 ? -1 : FindMatchingBrace(decompiled, openBrace);
            if (!execBelongsToCommand || openBrace < 0 || closeBrace < 0)
            {
                cursor = commandIndex + commandMatch.Length;
                continue;
            }

            var blockEnd = closeBrace + 1;
            var block = decompiled[commandIndex..blockEnd];
            cursor = Math.Max(blockEnd, commandIndex + 1);
            var argumentCountMatch = ForeachArgumentCount.Match(block);
            var dataMatch = ForeachData.Match(block);
            if (!argumentCountMatch.Success ||
                !int.TryParse(argumentCountMatch.Groups[1].Value, out var argumentCount) ||
                argumentCount <= 0 ||
                !dataMatch.Success)
            {
                continue;
            }

            var values = dataMatch.Groups[1].Value
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Where(value => value is not ("command" or "foreach" or "\\"))
                .ToArray();
            if (values.Length < argumentCount)
            {
                continue;
            }

            yield return new ForeachBlock(
                decompiled[(openBrace + 1)..closeBrace],
                values,
                argumentCount);
        }
    }

    private static int FindMatchingBrace(string text, int openBrace)
    {
        var depth = 0;
        for (var index = openBrace; index < text.Length; index++)
        {
            if (text[index] == '{')
            {
                depth++;
            }
            else if (text[index] == '}' && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static IEnumerable<(int Start, int End)> FindForeachBodyRanges(string text)
    {
        var cursor = 0;
        while (cursor < text.Length)
        {
            var commandMatch = ForeachCommand.Match(text, cursor);
            if (!commandMatch.Success)
            {
                yield break;
            }

            var commandIndex = commandMatch.Index;
            var nextCommandMatch = ForeachCommand.Match(text, commandIndex + commandMatch.Length);
            var execMatch = ForeachExec.Match(text, commandIndex + commandMatch.Length);
            var execBelongsToCommand = execMatch.Success &&
                (!nextCommandMatch.Success || execMatch.Index < nextCommandMatch.Index);
            var openBrace = execBelongsToCommand
                ? text.IndexOf('{', execMatch.Index + execMatch.Length)
                : -1;
            var closeBrace = openBrace < 0 ? -1 : FindMatchingBrace(text, openBrace);
            if (openBrace >= 0 && closeBrace >= 0)
            {
                yield return (openBrace, closeBrace);
                cursor = closeBrace + 1;
            }
            else
            {
                cursor = commandIndex + commandMatch.Length;
            }
        }
    }

    private static void BindRecordedSites(
        IReadOnlyList<PlacedModelReference> placements,
        GcxScript script,
        string scriptName,
        IEnumerable<GcxPlacementSite> recordedSites)
    {
        var claimedForeachGroups = new HashSet<int>();
        foreach (var site in recordedSites.Where(site => site.IsModelPlacement))
        {
            if (site.IsNested && site.ForeachRowCount > 0)
            {
                BindForeachRows(site);
                continue;
            }
            if (site.ModelHash is not { } modelHash || modelHash == 0)
            {
                continue;
            }

            var position = GetWorldPosition(site.Position);
            var matches = placements.Where(candidate =>
                candidate.Binding == null &&
                candidate.ModelHash == modelHash &&
                (position == null || candidate.Position == position) &&
                (site.EffectHash == null || candidate.EffectHash == site.EffectHash) &&
                (site.PropertyPositionHash == null ||
                    candidate.PropertyPositionHash == site.PropertyPositionHash) &&
                (site.CollisionReferenceHash == null ||
                    candidate.CollisionReferenceHash == site.CollisionReferenceHash))
                .ToList();
            if (!site.IsNested && site.PropertyPositionHash == null && matches.Count > 1)
            {
                matches.RemoveRange(1, matches.Count - 1);
            }
            foreach (var placement in matches)
            {
                placement.Binding = new GcxPlacementBinding(script, scriptName, site);
            }
        }

        void BindForeachRows(GcxPlacementSite site)
        {
            if (site.CommandName == "NewPutStageModelSet" &&
                site.Position == null && site.EffectHash == null)
            {
                return;
            }

            for (var row = 0; row < site.ForeachRowCount; row++)
            {
                var modelSite = row < site.ForeachModelSites.Count
                    ? site.ForeachModelSites[row]
                    : null;
                var transformSourceSite = row < site.ForeachTransformSites.Count
                    ? site.ForeachTransformSites[row]
                    : null;
                var collisionReferenceSite = row < site.ForeachCollisionReferenceSites.Count
                    ? site.ForeachCollisionReferenceSites[row]
                    : null;
                var modelHash = site.ModelHash ?? modelSite?.Value;
                if (modelHash is not > 0)
                {
                    continue;
                }

                var group = placements
                    .Where(candidate =>
                        candidate.Binding == null &&
                        candidate.ForeachGroupIndex is { } groupIndex &&
                        !claimedForeachGroups.Contains(groupIndex) &&
                        candidate.ModelHash == modelHash &&
                        (site.EffectHash == null || candidate.EffectHash == site.EffectHash) &&
                        (site.CollisionReferenceHash == null ||
                         candidate.CollisionReferenceHash == site.CollisionReferenceHash))
                    .Select(candidate => candidate.ForeachGroupIndex!.Value)
                    .FirstOrDefault(-1);
                if (group < 0)
                {
                    continue;
                }

                claimedForeachGroups.Add(group);
                foreach (var placement in placements.Where(candidate =>
                    candidate.Binding == null && candidate.ForeachGroupIndex == group))
                {
                    placement.Binding = new GcxPlacementBinding(
                        script,
                        scriptName,
                        site,
                        modelSite ?? site.Model,
                        transformSourceSite,
                        collisionReferenceSite,
                        row);
                }
            }
        }
    }

    private static Vector3? GetWorldPosition(GcxVectorSite? position)
    {
        if (position?.HasThreeLiteralComponents != true)
        {
            return null;
        }

        var values = position.Components;
        return new Vector3(values[0].Value, values[2].Value, values[1].Value);
    }

    private static EffectLookup BuildEffectLookup(GeomFile? geometry)
    {
        return BuildEffectLookup(geometry?.GeoEffects ?? []);
    }

    private static EffectLookup BuildEffectLookup(IEnumerable<GeoEffect> rootEffects)
    {
        var positions = new Dictionary<uint, Vector3>();
        var rotations = new Dictionary<uint, Vector3>();
        var sources = new Dictionary<uint, GeoEffect>();
        var propertySources = new Dictionary<uint, List<GeoEffect>>();

        foreach (var effect in TreeTraversal.Flatten(rootEffects, effect => effect.Children))
        {
            var hash = (uint)effect.Name;
            sources.TryAdd(hash, effect);
            positions.TryAdd(hash, new Vector3(effect.X, effect.Y, effect.Z));
            rotations.TryAdd(hash, new Vector3(
                effect.RotationX,
                effect.RotationY,
                effect.RotationZ));
            if (effect.Children.Count > 0)
            {
                if (!propertySources.TryGetValue(hash, out var children))
                {
                    children = [];
                    propertySources[hash] = children;
                }
                children.AddRange(effect.Children);
            }
        }

        Log.Debug("Built GCX effect lookup with {PositionCount} positions and {RotationCount} rotations.", positions.Count, rotations.Count);
        return new EffectLookup(positions, rotations, sources, propertySources);
    }

    private static IEnumerable<CommandBlock> EnumerateCommandBlocks(string text, string command, bool trackBraces)
    {
        var cursor = 0;
        while (cursor < text.Length)
        {
            var commandIndex = text.IndexOf(command, cursor, StringComparison.Ordinal);
            if (commandIndex < 0)
            {
                yield break;
            }

            var lineStart = text.LastIndexOf('\n', Math.Max(0, commandIndex - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var linePrefix = text.AsSpan(lineStart, commandIndex - lineStart).Trim();
            var afterCommand = commandIndex + command.Length;
            var hasValidPrefix = linePrefix.IsEmpty ||
                linePrefix.SequenceEqual("chara") ||
                linePrefix.SequenceEqual("command") ||
                !linePrefix.Contains(' ');
            var hasValidSuffix = afterCommand >= text.Length ||
                char.IsWhiteSpace(text[afterCommand]) ||
                text[afterCommand] == '\\';
            if (!hasValidPrefix || !hasValidSuffix)
            {
                cursor = afterCommand;
                continue;
            }

            var contentStart = afterCommand;
            var blockEnd = FindCommandBlockEnd(text, contentStart, trackBraces);
            yield return new CommandBlock(commandIndex, text[contentStart..blockEnd]);
            cursor = Math.Max(blockEnd, contentStart);
        }
    }

    private static int FindCommandBlockEnd(string text, int start, bool trackBraces)
    {
        var braceDepth = 0;
        for (var index = start; index < text.Length; index++)
        {
            if (trackBraces)
            {
                if (text[index] == '{')
                {
                    braceDepth++;
                }
                else if (text[index] == '}')
                {
                    braceDepth--;
                }
            }

            if (text[index] != '\n' || braceDepth > 0)
            {
                continue;
            }

            var nextLineEnd = text.IndexOf('\n', index + 1);
            if (nextLineEnd < 0)
            {
                return text.Length;
            }

            var line = text.AsSpan(index + 1, nextLineEnd - index - 1).TrimStart();
            if (line.IsEmpty || line[0] == '-')
            {
                continue;
            }

            if (line.Contains(" \\", StringComparison.Ordinal) ||
                line.StartsWith("proc ", StringComparison.Ordinal) ||
                line.StartsWith("command ", StringComparison.Ordinal) ||
                line.StartsWith("chara ", StringComparison.Ordinal) ||
                line.StartsWith("trap ", StringComparison.Ordinal) ||
                line.StartsWith("mesg ", StringComparison.Ordinal) ||
                line.StartsWith("load ", StringComparison.Ordinal) ||
                line.StartsWith("print ", StringComparison.Ordinal) ||
                line.StartsWith("switch ", StringComparison.Ordinal) ||
                line.StartsWith("if ", StringComparison.Ordinal) ||
                line.StartsWith("return ", StringComparison.Ordinal) ||
                line.StartsWith("($", StringComparison.Ordinal))
            {
                return index;
            }
        }

        return text.Length;
    }

    private static void ResolveTransformFromEffect(PlacedModelReference placed, uint hash, EffectLookup effects)
    {
        if (hash == 0)
        {
            return;
        }

        if (effects.Sources.TryGetValue(hash, out var source))
        {
            placed.SourceEffect = source;
        }

        if (placed.Position == null && effects.Positions.TryGetValue(hash, out var position))
        {
            placed.Position = position;
        }

        if (placed.Rotation == null && effects.Rotations.TryGetValue(hash, out var rotation))
        {
            placed.Rotation = rotation;
        }
    }

    private static IReadOnlyList<PlacedModelReference> ExpandPropertyPlacements(
        PlacedModelReference placement,
        uint propertyHash,
        EffectLookup effects)
    {
        placement.PropertyPositionHash = propertyHash == 0 ? null : propertyHash;
        if (propertyHash == 0)
        {
            return [placement];
        }

        if (!effects.PropertySources.TryGetValue(propertyHash, out var sources) ||
            sources.Count == 0)
        {
            // prop_pos accepts both a property directory and an individual marker.
            // Direct markers have no children, so use the referenced effect itself.
            ResolveTransformFromEffect(placement, propertyHash, effects);
            return [placement];
        }

        return sources.Select(source =>
        {
            var expanded = ClonePlacement(placement);
            expanded.SourceEffect = source;
            expanded.Position = new Vector3(source.X, source.Y, source.Z);
            expanded.Rotation = new Vector3(
                source.RotationX,
                source.RotationY,
                source.RotationZ);
            return expanded;
        }).ToArray();
    }

    private static IReadOnlyList<PlacedModelReference> ResolvePropertyPlacements(
        PlacedModelReference placement,
        PropertyReference property,
        EffectLookup effects)
    {
        if (property.ExpandChildren)
        {
            return ExpandPropertyPlacements(placement, property.Hash, effects);
        }

        placement.PropertyPositionHash = property.Hash;
        ResolveTransformFromEffect(placement, property.Hash, effects);
        return [placement];
    }

    private static PlacedModelReference ClonePlacement(PlacedModelReference source)
    {
        var clone = new PlacedModelReference
        {
            ModelHash = source.ModelHash,
            Position = source.Position,
            Rotation = source.Rotation,
            EffectHash = source.EffectHash,
            SourceEffect = source.SourceEffect,
            CollisionReferenceHash = source.CollisionReferenceHash,
            PropertyPositionHash = source.PropertyPositionHash,
            Binding = source.Binding
        };
        clone.ForeachGroupIndex = source.ForeachGroupIndex;
        clone.AdditionalModelHashes.AddRange(source.AdditionalModelHashes);
        return clone;
    }

    private static bool TryGetPropertyReference(string block, out PropertyReference property)
    {
        var match = PropertyPositionParameter.Match(block);
        if (match.Success)
        {
            var hash = ParseHash(match.Groups[1].Value);
            property = new PropertyReference(hash, ExpandChildren: true);
            return hash != 0;
        }

        match = PropertyParameter.Match(block);
        var value = GetPropertyEffectValue(match);
        var effectHash = value == null ? 0 : ParseHash(value);
        property = new PropertyReference(effectHash, ExpandChildren: false);
        return effectHash != 0;
    }

    private static bool TryGetPropertyReference(
        string block,
        ForeachBlock foreachBlock,
        int dataIndex,
        out PropertyReference property)
    {
        var match = PropertyPositionParameter.Match(block);
        if (match.Success)
        {
            var hash = ResolveArgumentHash(
                match.Groups[1].Value,
                foreachBlock.Values,
                dataIndex,
                foreachBlock.ArgumentCount);
            property = new PropertyReference(hash, ExpandChildren: true);
            return hash != 0;
        }

        match = PropertyParameter.Match(block);
        var value = GetPropertyEffectValue(match);
        var effectHash = value == null
            ? 0
            : ResolveArgumentHash(
                value,
                foreachBlock.Values,
                dataIndex,
                foreachBlock.ArgumentCount);
        property = new PropertyReference(effectHash, ExpandChildren: false);
        return effectHash != 0;
    }

    private static string? GetPropertyEffectValue(Match match)
    {
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[2].Success
            ? match.Groups[2].Value
            : match.Groups[1].Value;
    }

    private static uint ResolveArgumentHash(
        string value,
        IReadOnlyList<string> data,
        int dataIndex,
        int argumentCount)
    {
        if (value.StartsWith("$arg", StringComparison.Ordinal) &&
            int.TryParse(value.AsSpan(4), out var argumentIndex) &&
            argumentIndex >= 1 && argumentIndex <= argumentCount)
        {
            return ParseHash(data[dataIndex + argumentIndex - 1]);
        }

        return ParseHash(value);
    }

    private static bool TryResolveArgumentInt(
        string value,
        ForeachBlock block,
        int dataIndex,
        out int result)
    {
        if (value.StartsWith("$arg", StringComparison.Ordinal) &&
            int.TryParse(value.AsSpan(4), out var argumentIndex) &&
            argumentIndex >= 1 && argumentIndex <= block.ArgumentCount)
        {
            value = block.Values[dataIndex + argumentIndex - 1];
        }
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static void AddAdditionalModel(PlacedModelReference placed, Match match)
    {
        if (!match.Success)
        {
            return;
        }

        var hash = ParseHash(match.Groups[1].Value);
        if (hash != 0 && hash != placed.ModelHash && !placed.AdditionalModelHashes.Contains(hash))
        {
            placed.AdditionalModelHashes.Add(hash);
        }
    }

    private static void AddAdditionalModel(PlacedModelReference placed, uint hash)
    {
        if (hash != 0 && hash != placed.ModelHash && !placed.AdditionalModelHashes.Contains(hash))
        {
            placed.AdditionalModelHashes.Add(hash);
        }
    }

    private static void AddHash(ISet<uint> hashes, string value)
    {
        var hash = ParseHash(value);
        if (hash != 0)
        {
            hashes.Add(hash);
        }
    }

    internal static uint ParseHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (value.StartsWith('[') && value.EndsWith(']') &&
            uint.TryParse(value.AsSpan(1, value.Length - 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
        {
            return hash;
        }

        return Utils.String.HashString(value);
    }

    private static Vector3 ScriptDirectionToRadians(int x, int y, int z)
    {
        return new Vector3(
            GeoEffectChunkPatcher.DecodeAngle(unchecked((short)x)),
            GeoEffectChunkPatcher.DecodeAngle(unchecked((short)y)),
            GeoEffectChunkPatcher.DecodeAngle(unchecked((short)z)));
    }

    private sealed record EffectLookup(
        IReadOnlyDictionary<uint, Vector3> Positions,
        IReadOnlyDictionary<uint, Vector3> Rotations,
        IReadOnlyDictionary<uint, GeoEffect> Sources,
        IReadOnlyDictionary<uint, List<GeoEffect>> PropertySources);

    private sealed record RecordedScript(
        GcxScript Script,
        string Name,
        string Decompiled,
        IReadOnlyList<GcxPlacementSite> Sites);

    private sealed record ForeachBlock(
        string ExecBody,
        IReadOnlyList<string> Values,
        int ArgumentCount);

    private readonly record struct PropertyReference(uint Hash, bool ExpandChildren);

    private readonly record struct CommandBlock(int Index, string Content);
}
