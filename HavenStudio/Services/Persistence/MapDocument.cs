using System.Collections.Generic;
using HavenStudio.Formats.Gcx;

namespace HavenStudio.Services.Persistence;

public sealed class MapDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public MapDocumentSources Sources { get; set; } = new();
    public List<MapPlacementDocument> Placements { get; set; } = [];
    public MapGeomDocument Geom { get; set; } = new();
    public MapOpaqueDocument Opaque { get; set; } = new();
}

public sealed class MapDocumentSources
{
    public string Gcx { get; set; } = string.Empty;
    public string Geom { get; set; } = string.Empty;
}

public sealed class MapPlacementDocument
{
    public string Id { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public uint ModelHash { get; set; }
    public float[]? Position { get; set; }
    public float[]? DirectionDegrees { get; set; }
    public uint? EffectHash { get; set; }
    public string Source { get; set; } = MapPlacementSources.Position;
    public bool Editable { get; set; }
}

public static class MapPlacementSources
{
    public const string Position = "pos";
    public const string Effect = "effect";
    public const string Foreach = "foreach";
}

public sealed class MapGeomDocument
{
    public List<MapEffectDocument> Effects { get; set; } = [];
    public List<MapCollisionAttributeDocument> CollisionAttributes { get; set; } = [];
}

public sealed class MapEffectDocument
{
    public int ChunkOffset { get; set; }
    public int Name { get; set; }
    public int Index { get; set; }
    public float[] Position { get; set; } = new float[4];
    public float[] RotationDegrees { get; set; } = new float[3];
}

public sealed class MapCollisionAttributeDocument
{
    public string Target { get; set; } = MapCollisionAttributeTargets.Block;
    public int Block { get; set; }
    public int? Primitive { get; set; }
    public int? Polygon { get; set; }
    public ulong Attribute { get; set; }
}

public static class MapCollisionAttributeTargets
{
    public const string Block = "block";
    public const string Primitive = "prim";
    public const string Polygon = "poly";
}

public sealed class MapOpaqueDocument
{
    public GcxJson Gcx { get; set; } = new();
    public string GeomBytesBase64 { get; set; } = string.Empty;
}

public sealed record MapDocumentApplyResult(byte[] GcxBytes, byte[] GeomBytes);
