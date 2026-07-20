using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTK.Mathematics;

namespace HavenStudio.Formats.Geo;

public readonly record struct GeoRadixBoundsRemap(
    int OldMaxX,
    int OldMaxY,
    int OldMaxZ,
    int MinimumX,
    int MinimumY,
    int MinimumZ);

/// <summary>
/// Reorders spatial blocks into cell/type chains and emits the radix records
/// consumed by GEO_GeomBlockSearch.
/// </summary>
public static class GeoRadixBuilder
{
    private const short EmptyCell = 0x7FFF;
    private const byte MissingType = 0xFF;

    public static GeoRadixBoundsRemap EnsureBoundsContainBlockBases(GeomFile geometry, GeoGroup group)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(group);
        ValidateGrid(group);

        var cells = geometry.GeomGroupBlocks[group]
            .Select(block => TryGetUnboundedCell(geometry, group, block, out var cell)
                ? ((int X, int Y, int Z)?)cell
                : null)
            .Where(cell => cell != null)
            .Select(cell => cell!.Value)
            .ToArray();
        if (cells.Length == 0)
        {
            return new GeoRadixBoundsRemap(
                group.MaxX, group.MaxY, group.MaxZ, 0, 0, 0);
        }

        var minX = Math.Min(0, cells.Min(cell => cell.X));
        var minY = Math.Min(0, cells.Min(cell => cell.Y));
        var minZ = Math.Min(0, cells.Min(cell => cell.Z));
        var maxX = Math.Max(group.MaxX - 1, cells.Max(cell => cell.X));
        var maxY = Math.Max(group.MaxY - 1, cells.Max(cell => cell.Y));
        var maxZ = Math.Max(group.MaxZ - 1, cells.Max(cell => cell.Z));
        var remap = new GeoRadixBoundsRemap(
            group.MaxX, group.MaxY, group.MaxZ, minX, minY, minZ);

        group.BaseX += minX * group.DivX;
        group.BaseY += minY * group.DivY;
        group.BaseZ += minZ * group.DivZ;
        group.MaxX = checked(maxX - minX + 1);
        group.MaxY = checked(maxY - minY + 1);
        group.MaxZ = checked(maxZ - minZ + 1);
        return remap;
    }

    public static void Rebuild(
        GeomFile geometry,
        GeoGroup group,
        GeoRadixBoundsRemap? boundsRemap = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(group);
        ValidateGrid(group);
        if (group.TypesCount <= 0 || group.RadixSize < sizeof(short) + group.TypesCount)
        {
            throw new InvalidDataException("GEOM group has an invalid radix type count or stride.");
        }

        var blocks = geometry.GeomGroupBlocks[group];
        var existingCells = BuildExistingCellMap(geometry.GroupRadixData[group], blocks);
        var entries = blocks
            .Select((block, originalIndex) => new Entry(
                block,
                GetCellIndex(
                    geometry,
                    group,
                    block,
                    RemapFallbackCell(existingCells.GetValueOrDefault(block, -1), boundsRemap, group)),
                block.Flag >> 4,
                originalIndex))
            .ToArray();
        var chainOrder = entries
            .GroupBy(entry => (entry.Cell, entry.Type))
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Min(entry => entry.OriginalIndex));
        var ordered = entries
            .OrderBy(entry => chainOrder[(entry.Cell, entry.Type)])
            .ThenBy(entry => entry.OriginalIndex)
            .ToArray();
        if (ordered.Any(entry => entry.Type >= group.TypesCount))
        {
            var invalid = ordered.First(entry => entry.Type >= group.TypesCount);
            throw new InvalidDataException(
                $"GEOM block type {invalid.Type} exceeds group type count {group.TypesCount}.");
        }
        if (ordered.Length > short.MaxValue)
        {
            throw new InvalidDataException("GEOM group has too many blocks for a signed 16-bit radix offset.");
        }

        ReplaceGlobalOrder(geometry.GeomBlocks, blocks, ordered.Select(entry => entry.Block).ToArray());
        blocks.Clear();
        blocks.AddRange(ordered.Select(entry => entry.Block));

        for (var index = 0; index < ordered.Length; index++)
        {
            var endsChain = index == ordered.Length - 1 ||
                ordered[index + 1].Cell != ordered[index].Cell ||
                ordered[index + 1].Type != ordered[index].Type;
            ordered[index].Block.Flag = (byte)(
                (ordered[index].Block.Flag & ~1) |
                (endsChain ? 1 : 0));
        }

        var cellCount = checked(group.MaxX * group.MaxY * group.MaxZ);
        var paddingLength = group.RadixSize - sizeof(short) - group.TypesCount;
        var radices = new List<GeoRadix>(cellCount);
        for (var cell = 0; cell < cellCount; cell++)
        {
            var types = Enumerable.Repeat(MissingType, group.TypesCount).ToArray();
            var cellEntries = ordered
                .Select((entry, index) => (entry, index))
                .Where(pair => pair.entry.Cell == cell)
                .ToArray();
            if (cellEntries.Length == 0)
            {
                radices.Add(new GeoRadix(EmptyCell, types, new byte[paddingLength]));
                continue;
            }

            var first = cellEntries.Min(pair => pair.index);
            foreach (var typeGroup in cellEntries.GroupBy(pair => pair.entry.Type))
            {
                var type = typeGroup.Key;
                var typeStart = typeGroup.Min(pair => pair.index);
                var delta = typeStart - first;
                if (delta >= MissingType)
                {
                    throw new InvalidDataException(
                        $"GEOM radix cell {cell} needs block delta {delta}; the format limit is 254.");
                }
                types[type] = checked((byte)delta);
            }
            radices.Add(new GeoRadix(checked((short)first), types, new byte[paddingLength]));
        }

        geometry.GroupRadixData[group] = radices;
    }

    public static (int X, int Y, int Z) GetUnboundedCell(
        GeomFile geometry,
        GeoGroup group,
        GeoBlock block)
    {
        if (!TryGetUnboundedCell(geometry, group, block, out var cell))
        {
            throw new InvalidDataException("A spatial GEOM block has no valid vertex base row.");
        }

        return cell;
    }

    private static bool TryGetUnboundedCell(
        GeomFile geometry,
        GeoGroup group,
        GeoBlock block,
        out (int X, int Y, int Z) cell)
    {
        cell = default;
        if (!geometry.BlockVertexData.TryGetValue(block, out var header) ||
            header.PositionStart < 0 || header.PositionStart >= header.Data.Length)
        {
            return false;
        }
        var position = header.Data[header.PositionStart];
        cell = (
            (int)MathF.Round((position.X - group.BaseX) / group.DivX),
            (int)MathF.Round((position.Y - group.BaseY) / group.DivY),
            (int)MathF.Round((position.Z - group.BaseZ) / group.DivZ));
        return true;
    }

    private static int GetCellIndex(
        GeomFile geometry,
        GeoGroup group,
        GeoBlock block,
        int fallbackCell)
    {
        if (!TryGetUnboundedCell(geometry, group, block, out var cell))
        {
            if (fallbackCell < 0)
            {
                throw new InvalidDataException(
                    "A spatial GEOM block has neither a vertex base row nor an existing radix cell.");
            }
            return fallbackCell;
        }
        if (cell.X < 0 || cell.X >= group.MaxX ||
            cell.Y < 0 || cell.Y >= group.MaxY ||
            cell.Z < 0 || cell.Z >= group.MaxZ)
        {
            throw new InvalidDataException(
                $"GEOM block base maps outside its group at ({cell.X},{cell.Y},{cell.Z}).");
        }
        return checked(cell.X + cell.Z * group.MaxX + cell.Y * group.MaxX * group.MaxZ);
    }

    private static int RemapFallbackCell(
        int oldCell,
        GeoRadixBoundsRemap? boundsRemap,
        GeoGroup group)
    {
        if (oldCell < 0 || boundsRemap == null)
        {
            return oldCell;
        }

        var remap = boundsRemap.Value;
        var oldCellCount = checked(remap.OldMaxX * remap.OldMaxY * remap.OldMaxZ);
        if (oldCell >= oldCellCount)
        {
            throw new InvalidDataException(
                $"Existing GEOM radix cell {oldCell} is outside its pre-resize grid.");
        }
        var x = oldCell % remap.OldMaxX;
        var z = oldCell / remap.OldMaxX % remap.OldMaxZ;
        var y = oldCell / checked(remap.OldMaxX * remap.OldMaxZ);
        x -= remap.MinimumX;
        y -= remap.MinimumY;
        z -= remap.MinimumZ;
        if (x < 0 || x >= group.MaxX ||
            y < 0 || y >= group.MaxY ||
            z < 0 || z >= group.MaxZ)
        {
            throw new InvalidDataException(
                $"Remapped GEOM radix cell ({x},{y},{z}) is outside the resized grid.");
        }
        return checked(x + z * group.MaxX + y * group.MaxX * group.MaxZ);
    }

    private static Dictionary<GeoBlock, int> BuildExistingCellMap(
        IReadOnlyList<GeoRadix> radices,
        IReadOnlyList<GeoBlock> blocks)
    {
        var result = new Dictionary<GeoBlock, int>();
        for (var cell = 0; cell < radices.Count; cell++)
        {
            var radix = radices[cell];
            if (radix.Offset == EmptyCell)
            {
                continue;
            }
            foreach (var typeOffset in radix.Types.Where(value => value != MissingType))
            {
                for (var blockIndex = radix.Offset + typeOffset; blockIndex < blocks.Count; blockIndex++)
                {
                    result.TryAdd(blocks[blockIndex], cell);
                    if ((blocks[blockIndex].Flag & 1) != 0)
                    {
                        break;
                    }
                }
            }
        }
        return result;
    }

    private static void ReplaceGlobalOrder(
        List<GeoBlock> global,
        IReadOnlyCollection<GeoBlock> oldOrder,
        IReadOnlyList<GeoBlock> newOrder)
    {
        var members = oldOrder.ToHashSet();
        var positions = Enumerable.Range(0, global.Count)
            .Where(index => members.Contains(global[index]))
            .ToArray();
        if (positions.Length != newOrder.Count)
        {
            throw new InvalidDataException("GEOM global/group block collections are inconsistent.");
        }
        for (var index = 0; index < positions.Length; index++)
        {
            global[positions[index]] = newOrder[index];
        }
    }

    private static void ValidateGrid(GeoGroup group)
    {
        if (!(group.DivX > 0) || !(group.DivY > 0) || !(group.DivZ > 0) ||
            group.MaxX <= 0 || group.MaxY <= 0 || group.MaxZ <= 0)
        {
            throw new InvalidDataException("GEOM group has invalid grid divisions or dimensions.");
        }
    }

    private sealed record Entry(GeoBlock Block, int Cell, int Type, int OriginalIndex);
}
