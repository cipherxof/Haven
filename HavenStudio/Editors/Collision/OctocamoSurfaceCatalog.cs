using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HavenStudio.Formats.Geo;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Editors;

/// <summary>Resolves the two independent GEOM polygon selectors against the stage OCTT.</summary>
public sealed class OctocamoSurfaceCatalog : IDisposable
{
    private readonly Dictionary<uint, uint> _patterns = new();
    private readonly HashSet<uint> _ambiguousMaterials = [];
    private readonly Dictionary<uint, Vector3> _colours = new();
    private readonly Dictionary<GeoBlock, GeoMaterialHeader> _tables = new();
    private readonly HashSet<uint>? _registeredPatterns;
    private readonly HashSet<uint>? _registeredMaterials;
    private readonly HashSet<uint> _usedMaterials;
    private readonly byte[] _originalOctt;
    private byte[] _octt;
    private readonly Dictionary<uint, IImage> _previewImages = new();
    private IReadOnlyDictionary<uint, OctocamoPatternPreview> _previewData = new Dictionary<uint, OctocamoPatternPreview>();
    public IEnumerable<uint> PatternHashes => _patterns.Values.Distinct();
    public IReadOnlyCollection<uint> AmbiguousMaterialHashes => _ambiguousMaterials;
    public OctocamoPatternAtlas? PatternAtlas { get; private set; }
    public bool PreviewMusclePatterns { get; set; }
    public bool IsMappingDirty => !_octt.AsSpan().SequenceEqual(_originalOctt);
    public int MappingCount => checked((int)Read(_octt, 4));
    public int AvailableMappingRows
    {
        get
        {
            var available = Enumerable.Range(0, MappingCount)
                .Count(i => !_usedMaterials.Contains(Read(_octt, 0x610 + i * 16)));
            for (var row = MappingCount; row < 32 && 0x610 + (row + 1) * 16 <= _octt.Length; row++)
            {
                if (_octt.AsSpan(0x610 + row * 16, 16).ContainsAnyExcept((byte)0)) break;
                available++;
            }
            return available;
        }
    }
    public IEnumerable<uint> AllMaterialHashes => _usedMaterials.Where(id => id != 0).OrderBy(Name);
    public IEnumerable<uint> AvailablePatternHashes => _previewData.Keys.OrderBy(Name);
    public bool HasPatternLibrary => _previewData.Count > 0;
    public uint GetPatternHash(uint material) => _patterns.GetValueOrDefault(material);
    public bool IsPatternRegistered(uint pattern) => _registeredPatterns?.Contains(pattern) == true;
    public byte[] GetMappingBytes() => _octt.ToArray();
    public byte[] GetOriginalMappingBytes() => _originalOctt.ToArray();
    public void AcceptMappingSave(byte[] committed) => committed.CopyTo(_originalOctt, 0);

    public void ResetMappings()
    {
        _octt = _originalOctt.ToArray();
        ReadPatterns();
        RebuildAtlas();
    }

    /// <summary>Stage-wide OCTT edit only: no polygon selectors or physical materials are changed.</summary>
    public void RemapPattern(uint material, uint pattern)
    {
        if (material == 0 || !_usedMaterials.Contains(material))
            throw new InvalidOperationException("Select a material used by this GEOM.");
        if (_ambiguousMaterials.Contains(material))
            throw new InvalidOperationException("This material has conflicting duplicate OCTT rows. Haven cannot safely choose one row to remap; the table was not changed.");
        if (!_previewData.ContainsKey(pattern))
            throw new InvalidOperationException("Load the online SLOT and choose a successfully decoded pattern.");
        if (!IsPatternRegistered(pattern) || _registeredMaterials?.Contains(material) != true)
            throw new InvalidOperationException("This pattern or material is not in the stage OCTL registry. No change was made; unsupported registry expansion is not guessed.");
        if (_patterns.GetValueOrDefault(material) == pattern) return;
        var row = Enumerable.Range(0, MappingCount).FirstOrDefault(i => Read(_octt, 0x610 + i * 16) == material, -1);
        if (row < 0)
        {
            // Preserve the proven fixed-capacity table. Retire only a material absent
            // from ALL GEOM material tables, never a currently used material.
            row = Enumerable.Range(0, MappingCount).FirstOrDefault(i => !_usedMaterials.Contains(Read(_octt, 0x610 + i * 16)), -1);
            if (row < 0)
            {
                row = MappingCount;
                if (row >= 32 || 0x610 + (row + 1) * 16 > _octt.Length ||
                    _octt.AsSpan(0x610 + row * 16, 16).ContainsAnyExcept((byte)0))
                    throw new InvalidOperationException("OCTT has 32 rows and no unused GEOM-material row can be safely reclaimed. Existing mappings were preserved.");
                BinaryPrimitives.WriteUInt32BigEndian(_octt.AsSpan(4, 4), (uint)(row + 1));
            }
        }
        var offset = 0x610 + row * 16;
        BinaryPrimitives.WriteUInt32BigEndian(_octt.AsSpan(offset, 4), material);
        BinaryPrimitives.WriteUInt32BigEndian(_octt.AsSpan(offset + 4, 4), pattern);
        // These are runtime lookup/cache pointers, not editable material metadata.
        // Native unrelocated rows use zero; do not retain pointers to the old OCTS.
        _octt.AsSpan(offset + 8, 8).Clear();
        ReadPatterns();
        RebuildAtlas();
    }

    private void ReadPatterns()
    {
        _patterns.Clear();
        _ambiguousMaterials.Clear();
        for (var i = 0; i < MappingCount; i++)
        {
            var material = Read(_octt, 0x610 + i * 16);
            var pattern = Read(_octt, 0x614 + i * 16);
            // Retail stages can contain duplicate material rows. Identical rows
            // are harmless; conflicting rows have unknown runtime precedence, so
            // leave only that material unresolved rather than rejecting the stage.
            if (material == 0) continue;
            if (_ambiguousMaterials.Contains(material)) continue;
            if (_patterns.TryGetValue(material, out var previous))
            {
                if (previous == pattern) continue;
                _patterns.Remove(material);
                _ambiguousMaterials.Add(material);
            }
            else _patterns.Add(material, pattern);
        }
    }

    private void RebuildAtlas()
    {
        var needed = _patterns.Values.ToHashSet();
        var previews = _previewData.Where(pair => needed.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        PatternAtlas = previews.Count == 0 ? null : new OctocamoPatternAtlas(previews);
    }

    public void SetPatternPreviews(IReadOnlyDictionary<uint, OctocamoPatternPreview> patterns)
    {
        // Validate buffers before replacing a working library; atlas only needs the
        // stage's <=32 mapped textures, while the picker can browse the full SLOT.
        foreach (var pattern in patterns.Values)
            if (pattern.Width <= 0 || pattern.Height <= 0 || pattern.Width > 4096 || pattern.Height > 4096 ||
                pattern.Rgba.Length != (long)pattern.Width * pattern.Height * 4)
                throw new ArgumentException("Invalid pattern preview buffer.", nameof(patterns));
        Dispose();
        _previewData = new Dictionary<uint, OctocamoPatternPreview>(patterns);
        RebuildAtlas();
    }

    public IImage? PatternImage(uint hash)
    {
        if (_previewImages.TryGetValue(hash, out var existing)) return existing;
        if (!_previewData.TryGetValue(hash, out var pattern)) return null;
        var bitmap = new WriteableBitmap(new PixelSize(pattern.Width, pattern.Height), new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Rgba8888, Avalonia.Platform.AlphaFormat.Unpremul);
        using var frame = bitmap.Lock();
        for (var row = 0; row < pattern.Height; row++)
            Marshal.Copy(pattern.Rgba, row * pattern.Width * 4,
                IntPtr.Add(frame.Address, row * frame.RowBytes), pattern.Width * 4);
        _previewImages[hash] = bitmap;
        return bitmap;
    }

    public void Dispose()
    {
        foreach (var image in _previewImages.Values.OfType<IDisposable>()) image.Dispose();
        _previewImages.Clear();
        _previewData = new Dictionary<uint, OctocamoPatternPreview>();
        PatternAtlas = null;
    }

    public OctocamoSurfaceCatalog(GeomFile geometry, byte[] octt, byte[]? octl = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(octt);
        _originalOctt = octt.ToArray();
        _octt = octt.ToArray();
        _usedMaterials = geometry.GroupMaterialData.Values.Concat(geometry.BlockMaterialData.Values)
            .Concat(geometry.GeomRefBlockMaterial.Values).SelectMany(table => table.Materials)
            .Where(material => material != 0).ToHashSet();
        if (octt.Length < 0x610 || Read(octt, 0) != 3)
            throw new InvalidDataException("Unsupported OctoCamo table version.");
        var materialCount = checked((int)Read(octt, 4));
        var colourCount = checked((int)Read(octt, 8));
        if (materialCount > 32 || colourCount > 32 ||
            0x610 + materialCount * 16 > octt.Length || 16 + colourCount * 48 > 0x610)
            throw new InvalidDataException("Invalid OctoCamo table counts.");
        ReadPatterns();
        if (octl != null)
        {
            if (octl.Length < 16) throw new InvalidDataException("Truncated OctoCamo pattern registry.");
            var count = checked((int)Read(octl, 4));
            if (count < 0 || count > (octl.Length - 16) / 4)
                throw new InvalidDataException("Invalid OctoCamo pattern registry count.");
            _registeredPatterns = Enumerable.Range(0, count)
                .Select(i => Read(octl, 16 + i * 4)).ToHashSet();
            var materialCountInRegistry = checked((int)Read(octl, 8));
            if (materialCountInRegistry < 0 || materialCountInRegistry > (octl.Length - 16 - count * 4) / 4)
                throw new InvalidDataException("Invalid OctoCamo material registry count.");
            _registeredMaterials = Enumerable.Range(0, materialCountInRegistry)
                .Select(i => Read(octl, 16 + (count + i) * 4)).ToHashSet();
        }
        for (var i = 0; i < colourCount; i++)
        {
            var offset = 16 + i * 48;
            var rgb = new Vector3(Float(octt, offset + 16), Float(octt, offset + 20), Float(octt, offset + 24));
            if (float.IsFinite(rgb.X) && float.IsFinite(rgb.Y) && float.IsFinite(rgb.Z))
                _colours[Read(octt, offset)] = rgb;
        }

        foreach (var (group, blocks) in geometry.GeomGroupBlocks)
            foreach (var block in blocks)
                if ((geometry.BlockMaterialData.GetValueOrDefault(block) ??
                    geometry.GroupMaterialData.GetValueOrDefault(group)) is { } table)
                    _tables[block] = table;

        foreach (var (reference, blocks) in geometry.GeomRefBlocks)
        {
            GeoMaterialHeader? inherited = null;
            foreach (var block in blocks)
            {
                inherited = geometry.BlockMaterialData.GetValueOrDefault(block) ??
                    geometry.GeomRefBlockMaterial.GetValueOrDefault(block) ?? inherited;
                if (inherited != null)
                    _tables[block] = inherited;
            }
        }
    }

    public GeoMaterialHeader? GetTable(GeoBlock block) => _tables.GetValueOrDefault(block);

    public IReadOnlyList<OctocamoMaterialOption> Materials(GeoBlock block)
    {
        var table = GetTable(block);
        if (table == null) return [];
        return table.Materials.Take(32).Select((id, slot) =>
        {
            var pattern = _patterns.GetValueOrDefault(id);
            var description = _ambiguousMaterials.Contains(id) ? "ambiguous duplicate OCTT rows" :
                pattern == 0 ? "unmapped" : Name(pattern) +
                (_registeredPatterns != null && !_registeredPatterns.Contains(pattern) ? " (not registered)" : string.Empty);
            return new OctocamoMaterialOption(slot, id, pattern, $"{slot}: {Name(id)} → {description}")
                { Preview = PatternImage(pattern) };
        })
            .Where(option => option.MaterialHash != 0).ToArray();
    }

    public IReadOnlyList<OctocamoColourOption> Colours(GeoBlock block)
    {
        var table = GetTable(block);
        if (table == null) return [];
        return table.Colors.Take(32).Select((id, slot) =>
        {
            var known = _colours.TryGetValue(id, out var rgb);
            return new OctocamoColourOption(slot, id, known ? ToBrush(rgb) : Brushes.Magenta,
                $"{slot}: {Name(id)}{(known ? string.Empty : " (unmapped)")}");
        }).Where(option => option.ColourHash != 0).ToArray();
    }

    public OctocamoSurface Resolve(GeoBlock block, ushort attribute)
    {
        var table = GetTable(block);
        var materialSlot = (attribute >> 6) & 31;
        var colourSlot = (attribute >> 11) & 31;
        var material = table != null && materialSlot < table.Materials.Count ? table.Materials[materialSlot] : 0;
        var colour = table != null && colourSlot < table.Colors.Count ? table.Colors[colourSlot] : 0;
        var hasPattern = _patterns.TryGetValue(material, out var pattern);
        var hasColour = _colours.TryGetValue(colour, out var rgb);
        var mapped = hasPattern && hasColour &&
            (_registeredPatterns == null || _registeredPatterns.Contains(pattern));
        return new OctocamoSurface(materialSlot, colourSlot, material, colour, pattern,
            hasColour ? rgb : new Vector3(0.95f, 0.12f, 0.68f), mapped)
        {
            HasPatternMapping = hasPattern && pattern != 0 && (_registeredPatterns == null || _registeredPatterns.Contains(pattern)),
            HasClothMapping = hasColour
        };
    }

    public static ushort WithMaterial(ushort attribute, int slot) =>
        (ushort)((attribute & ~0x07C0) | ((slot & 31) << 6));

    public static ushort WithColour(ushort attribute, int slot) =>
        (ushort)((attribute & ~0xF800) | ((slot & 31) << 11));

    public static IBrush ToBrush(Vector3 rgb) => new SolidColorBrush(Color.FromRgb(
        Channel(rgb.X), Channel(rgb.Y), Channel(rgb.Z)));

    private static byte Channel(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255);
    private static string Name(uint hash) => $"{DictionaryFile.GetHashString(hash)} (0x{hash:X6})";
    private static uint Read(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
    private static float Float(byte[] bytes, int offset) => BitConverter.UInt32BitsToSingle(Read(bytes, offset));
}

public readonly record struct OctocamoSurface(
    int MaterialSlot, int ColourSlot, uint MaterialHash, uint ColourHash,
    uint PatternHash, Vector3 ClothColour, bool IsMapped)
{
    public bool HasPatternMapping { get; init; }
    public bool HasClothMapping { get; init; }
}

public sealed record OctocamoMaterialOption(int Slot, uint MaterialHash, uint PatternHash, string DisplayName)
{
    public IImage? Preview { get; init; }
}
public sealed record OctocamoColourOption(int Slot, uint ColourHash, IBrush Preview, string DisplayName);
