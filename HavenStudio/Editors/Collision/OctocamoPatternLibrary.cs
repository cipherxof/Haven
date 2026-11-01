using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dds;
using HavenStudio.Formats.Dld;
using HavenStudio.Formats.Dlz;
using HavenStudio.Formats.Txn;

namespace HavenStudio.Editors;

public sealed record OctocamoPatternPreview(int Width, int Height, byte[] Rgba);

/// <summary>Read-only extraction of OCTS diffuse previews from retail online SLOT pages.</summary>
public static class OctocamoPatternLibrary
{
    private sealed record Resource(int Extension, uint Hash, int Offset, int Length);

    public static IReadOnlyDictionary<uint, OctocamoPatternPreview> Load(string path, IEnumerable<uint> requested)
        => Load(File.ReadAllBytes(path), requested);

    public static IReadOnlyDictionary<uint, OctocamoPatternPreview> LoadAll(string path)
        => LoadCore(File.ReadAllBytes(path), null);

    public static IReadOnlyDictionary<uint, OctocamoPatternPreview> Load(byte[] bytes, IEnumerable<uint> requested)
        => LoadCore(bytes, requested ?? throw new ArgumentNullException(nameof(requested)));

    private static IReadOnlyDictionary<uint, OctocamoPatternPreview> LoadCore(byte[] bytes, IEnumerable<uint>? requested)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var wanted = requested?.ToHashSet();
        var result = new Dictionary<uint, OctocamoPatternPreview>();
        if (bytes.Length < 0x800) throw new InvalidDataException("Truncated OctoCamo SLOT.");
        var pages = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8, 2));
        var position = 0x800;
        for (var page = 0; page < pages; page++)
        {
            var count = checked((int)Read(bytes, position));
            if (count < 1 || count > 10000 || position + 8L + count * 16L > bytes.Length)
                throw new InvalidDataException($"Invalid SLOT page {page}.");
            var cursor = Align(checked(position + 8 + count * 16));
            var section = 0;
            var resources = new List<Resource>();
            for (var i = 0; i < count; i++)
            {
                var entry = position + 8 + i * 16;
                var id = Read(bytes, entry);
                var extension = (int)(id >> 24);
                var hash = id & 0xFFFFFF;
                var relative = Offset(bytes, entry + 8);
                if (extension == 0x7F)
                {
                    if (hash != 0) section = cursor = Align(cursor);
                    else cursor = checked(section + relative);
                }
                else if (extension == 0x7E)
                    throw new InvalidDataException("Compressed SLOT pages are not supported for OctoCamo previews.");
                else if (id != 0 && i + 1 < count)
                {
                    var end = Offset(bytes, entry + 24);
                    var start = checked(section + relative);
                    var length = checked(end - relative);
                    if (length < 0 || start < 0 || start + (long)length > bytes.Length)
                        throw new InvalidDataException("SLOT resource range is invalid.");
                    resources.Add(new Resource(extension, hash, start, length));
                }
            }
            position = Align(cursor);
            var selected = resources.Where(resource => resource.Extension == 0x26 && (wanted == null || wanted.Contains(resource.Hash))).ToArray();
            if (selected.Length == 0) continue;
            var dlds = new List<DldFile>();
            foreach (var resource in resources.Where(resource => resource.Extension == 0x21))
            {
                using var packed = Slice(bytes, resource);
                var dlz = new DlzFile(packed, Endianness.Big);
                using var unpacked = new MemoryStream();
                dlz.Unpack(unpacked);
                unpacked.Position = 0;
                dlds.Add(new DldFile(unpacked, Endianness.Big));
            }
            foreach (var octs in selected)
            {
                if (octs.Length < 0x18 || Read(bytes, octs.Offset) != octs.Hash)
                    throw new InvalidDataException("OCTS identity mismatch.");
                var txnHash = Read(bytes, octs.Offset + 4);
                var diffuse = Read(bytes, octs.Offset + 0x10);
                var resource = resources.FirstOrDefault(resource => resource.Extension == 3 && resource.Hash == txnHash);
                if (resource == null) continue;
                using var stream = Slice(bytes, resource);
                var txn = new TxnFile(stream, Endianness.Big);
                var info = txn.ImageInfo.FirstOrDefault(info => info.TexId == diffuse);
                if (info == null) continue;
                var index = txn.GetIndex(info);
                if (index < 0 || index >= txn.Images.Count) continue;
                var image = txn.Images[index];
                var payload = dlds.Select(dld => dld.FindTexture(info.TriId, index, DldPriority.Main))
                    .FirstOrDefault(texture => texture != null);
                var format = image.FourCC switch { 9 => "DXT1", 10 => "DXT3", 11 => "DXT5", _ => null };
                if (payload == null || format == null || image.Width == 0 || image.Height == 0) continue;
                if (image.Width > 4096 || image.Height > 4096)
                    throw new InvalidDataException("OctoCamo preview dimensions exceed the supported limit.");
                result[octs.Hash] = new OctocamoPatternPreview(image.Width, image.Height,
                    DxtDecoder.DecodeToRgba(image.Width, image.Height, format, payload.Data));
            }
        }
        return result;
    }

    private static MemoryStream Slice(byte[] bytes, Resource resource) =>
        new(bytes.AsSpan(resource.Offset, resource.Length).ToArray(), writable: false);
    private static uint Read(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 4L > bytes.Length) throw new InvalidDataException("Truncated SLOT index.");
        return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
    }
    private static int Offset(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 8L > bytes.Length) throw new InvalidDataException("Truncated SLOT offset.");
        return checked((int)BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(offset, 8)));
    }
    private static int Align(int value) => checked(value + 0x7FF) & ~0x7FF;
}

public sealed class OctocamoPatternAtlas
{
    public const int TileSize = 128;
    public const int Columns = 8;
    private readonly Dictionary<uint, int> _cells = new();
    public int Width { get; } = Columns * TileSize;
    public int Height { get; }
    public byte[] Rgba { get; }

    public OctocamoPatternAtlas(IReadOnlyDictionary<uint, OctocamoPatternPreview> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Count > 256) throw new ArgumentException("Too many OctoCamo preview patterns.", nameof(patterns));
        foreach (var preview in patterns.Values)
            if (preview.Width <= 0 || preview.Height <= 0 || preview.Width > 4096 || preview.Height > 4096 ||
                preview.Rgba.Length != (long)preview.Width * preview.Height * 4)
                throw new ArgumentException("Invalid RGBA preview dimensions or buffer.", nameof(patterns));
        Height = Math.Max(1, (patterns.Count + 1 + Columns - 1) / Columns) * TileSize;
        Rgba = new byte[Width * Height * 4];
        var cell = 0;
        PaintCell(cell++, null); // missing-texture diagnostic, not a fabricated pattern
        foreach (var (hash, preview) in patterns.OrderBy(pair => pair.Key))
        {
            _cells[hash] = cell;
            PaintCell(cell++, preview);
        }
    }

    public (float U0, float V0, float U1, float V1) Bounds(uint pattern)
    {
        var cell = _cells.GetValueOrDefault(pattern);
        var x = (cell % Columns) * TileSize;
        var y = (cell / Columns) * TileSize;
        return ((x + 0.5f) / Width, (y + 0.5f) / Height,
            (x + TileSize - 0.5f) / Width, (y + TileSize - 0.5f) / Height);
    }

    private void PaintCell(int cell, OctocamoPatternPreview? pattern)
    {
        for (var y = 0; y < TileSize; y++)
        for (var x = 0; x < TileSize; x++)
        {
            var target = (((cell / Columns) * TileSize + y) * Width + (cell % Columns) * TileSize + x) * 4;
            if (pattern == null)
            {
                Rgba[target] = 242; Rgba[target + 1] = 31; Rgba[target + 2] = 173;
            }
            else
            {
                var source = ((y * pattern.Height / TileSize) * pattern.Width + x * pattern.Width / TileSize) * 4;
                Buffer.BlockCopy(pattern.Rgba, source, Rgba, target, 3);
            }
            Rgba[target + 3] = 255;
        }
    }
}
