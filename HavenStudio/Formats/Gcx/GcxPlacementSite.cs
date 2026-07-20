using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenStudio.Formats.Gcx;

public enum GcxLiteralEncoding
{
    PackedNumber,
    Int16,
    Int32
}

public sealed record GcxLiteralSite(
    int Offset,
    int Width,
    GcxLiteralEncoding Encoding,
    int Value);

public sealed record GcxStringCodeSite(
    int ParameterOffset,
    int ParameterLength,
    int ValueOffset,
    uint Value);

public sealed record GcxForeachRowSite(int Offset, int Length);

public sealed class GcxForeachSite
{
    public required int CommandOffset { get; init; }
    public required int CommandLength { get; init; }
    public required int DataParameterOffset { get; init; }
    public required int DataParameterLength { get; init; }
    public required int RepeatParameterOffset { get; init; }
    public required int RepeatParameterLength { get; init; }
    public required GcxLiteralSite Repeat { get; init; }
    public required IReadOnlyList<GcxForeachRowSite> Rows { get; init; }
}

public sealed class GcxVectorSite
{
    public GcxVectorSite(
        int parameterOffset,
        int parameterLength,
        int parameterPayloadOffset,
        IEnumerable<GcxLiteralSite> components)
    {
        ParameterOffset = parameterOffset;
        ParameterLength = parameterLength;
        ParameterPayloadOffset = parameterPayloadOffset;
        Components = components?.ToArray() ?? throw new ArgumentNullException(nameof(components));
    }

    public int ParameterOffset { get; }
    public int ParameterLength { get; }
    public int ParameterPayloadOffset { get; }
    public IReadOnlyList<GcxLiteralSite> Components { get; }
    public bool HasThreeLiteralComponents => Components.Count == 3;
}

public sealed class GcxPlacementSite
{
    public required uint CommandHash { get; init; }
    public required string CommandName { get; init; }
    public required int CommandOffset { get; init; }
    public required int CommandLength { get; init; }
    public uint? ModelHash { get; init; }
    public uint? EffectHash { get; init; }
    public uint? CollisionReferenceHash { get; init; }
    public uint? PropertyPositionHash { get; init; }
    public GcxVectorSite? Position { get; init; }
    public GcxVectorSite? Direction { get; init; }
    public GcxStringCodeSite? Model { get; init; }
    public GcxStringCodeSite? Effect { get; init; }
    public GcxStringCodeSite? PropertyPosition { get; init; }
    public IReadOnlyList<GcxStringCodeSite?> ForeachModelSites { get; init; } = [];
    public IReadOnlyList<GcxStringCodeSite?> ForeachTransformSites { get; init; } = [];
    public IReadOnlyList<GcxStringCodeSite?> ForeachCollisionReferenceSites { get; init; } = [];
    public int ForeachRowCount { get; init; }
    public GcxForeachSite? Foreach { get; init; }
    public GcxStringCodeSite? CollisionReference { get; init; }
    public bool IsNested { get; init; }
    public bool IsModelPlacement { get; init; }
    public bool Editable { get; init; }
    public bool ModelHashEditable { get; init; }
    public bool CollisionReferenceEditable { get; init; }
    public string? ReadOnlyReason { get; init; }
}

public sealed record GcxPlacementCommandDefinition(
    uint Hash,
    string Name,
    bool IsModelPlacement,
    bool SupportsCommandReencoding = false);

public static class GcxPlacementCommandCatalog
{
    // Audited against the local MGO2 stage corpus. Several commands use spatially named
    // parameters for screens, cameras, regions, or lights; they stay in the catalog so a
    // future corpus change is visible without misclassifying them as model placements.
    private static readonly GcxPlacementCommandDefinition[] Definitions =
    [
        new(0x07A516, "NewPutObject", IsModelPlacement: true, SupportsCommandReencoding: true),
        new(0x7E641F, "NewPutStageModelSet", IsModelPlacement: true),
        new(0xCE2D24, "NewSky", IsModelPlacement: true),
        new(0x7187BE, "[7187BE]", IsModelPlacement: true),
        new(0x9396D5, "[9396D5]", IsModelPlacement: true),
        new(0x07E1F9, "[07E1F9]", IsModelPlacement: true),
        new(0x2808D7, "[2808D7]", IsModelPlacement: true),
        new(0x192604, "[192604]", IsModelPlacement: true),
        new(0x9EF3C9, "[9EF3C9]", IsModelPlacement: true),
        new(0x3EF709, "[3EF709]", IsModelPlacement: true),
        new(0x4D1F6D, "[4D1F6D]", IsModelPlacement: true),
        new(0x71EF12, "[71EF12]", IsModelPlacement: true),
        new(0xF213EE, "[F213EE]", IsModelPlacement: true),
        new(0x656B68, "NewTestTree_02", IsModelPlacement: true),
        new(0x9ADA66, "NewBlastDrum_GCL", IsModelPlacement: true),
        new(0x1E20BF, "NewGrassMng_03", IsModelPlacement: false),
        new(0x000000, "[000000]", IsModelPlacement: false),
        new(0x01E4DB, "[01E4DB]", IsModelPlacement: false),
        new(0x0550FE, "[0550FE]", IsModelPlacement: false),
        new(0x6C6999, "[6C6999]", IsModelPlacement: false),
        new(0x761441, "[761441]", IsModelPlacement: false),
        new(0x767E15, "[767E15]", IsModelPlacement: false),
        new(0xA72D3D, "[A72D3D]", IsModelPlacement: false),
        new(0xB607C5, "[B607C5]", IsModelPlacement: false),
        new(0xBD8D95, "[BD8D95]", IsModelPlacement: false),
        new(0xC12982, "[C12982]", IsModelPlacement: false),
        new(0x43F718, "COM_SetCamera", IsModelPlacement: false),
        new(0x71E7D6, "NewSystemLightSet", IsModelPlacement: false),
        new(0x47071E, "[47071E]", IsModelPlacement: false),
        new(0xB03162, "[B03162]", IsModelPlacement: false)
    ];

    private static readonly IReadOnlyDictionary<uint, GcxPlacementCommandDefinition> ByHash =
        Definitions.ToDictionary(definition => definition.Hash);

    public static IReadOnlyList<GcxPlacementCommandDefinition> AuditedCommands => Definitions;

    public static bool TryGet(uint hash, out GcxPlacementCommandDefinition definition)
    {
        return ByHash.TryGetValue(hash, out definition!);
    }
}
