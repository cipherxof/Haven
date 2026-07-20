using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HavenStudio.Utils;

namespace HavenStudio.Formats.Geo;

public sealed record GeoStructureIssue(string Structure, int Offset, string Message)
{
    public override string ToString() => $"{Structure} @ 0x{Offset:X}: {Message}";
}

public sealed record GeoStructureSummary(
    int Groups,
    int Radices,
    int RadixReferences,
    int Blocks,
    int Geoms,
    int Effects);

public sealed class GeoStructureValidationResult
{
    internal GeoStructureValidationResult(
        List<GeoStructureIssue> issues,
        GeoStructureSummary summary)
    {
        Issues = new ReadOnlyCollection<GeoStructureIssue>(issues);
        Summary = summary;
    }

    public IReadOnlyList<GeoStructureIssue> Issues { get; }
    public GeoStructureSummary Summary { get; }
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Checks the disk relationships consumed by the MGO2 GEOM loader. This deliberately
/// validates stored links and radix predictions without rewriting or normalizing them.
/// </summary>
public static class GeoStructureValidator
{
    private const short EmptyRadix = 0x7FFF;
    private const byte MissingType = 0xFF;
    private const ushort MissingBlockOffset = 0xFFFF;
    private const int BlockStride = 0x20;
    private const int GeomOffsetUnit = 0x10;
    private const int ReleaseArenaSize = 0x3C00;

    public static GeoStructureValidationResult Validate(GeomFile geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var issues = new List<GeoStructureIssue>();
        var radixCount = 0;
        var radixReferences = 0;
        var geoms = 0;

        ValidateChunks(geometry, issues);

        foreach (var group in geometry.GeomGroups)
        {
            var radices = geometry.GroupRadixData[group];
            var blocks = geometry.GeomGroupBlocks[group];
            radixCount += radices.Count;
            ValidateGroup(group, radices, blocks, issues, ref radixReferences);
        }

        // Reference-region blocks use the same GEO_BLOCK arena allocator as group
        // blocks, so characterize both chunk 0 and chunk 1 rather than validating
        // only the blocks reachable from the spatial radix.
        foreach (var block in geometry.GeomBlocks)
        {
            geoms += ValidateBlock(geometry, block, issues);
        }

        var effects = 0;
        foreach (var effect in TreeTraversal.Flatten(geometry.GeoEffects, effect => effect.Children))
        {
            effects++;
            ValidateEffect(effect, geometry.GeomChunk6.Length, issues);
        }

        return new GeoStructureValidationResult(
            issues,
            new GeoStructureSummary(
                geometry.GeomGroups.Count,
                radixCount,
                radixReferences,
                geometry.GeomBlocks.Count,
                geoms,
                effects));
    }

    private static void ValidateChunks(GeomFile geometry, List<GeoStructureIssue> issues)
    {
        if (geometry.Header.Chunks.Count == 0 || geometry.Header.Chunks[0].Type != (ushort)GeoChunkType.GROUPS)
        {
            issues.Add(new GeoStructureIssue("header", 0x20, "chunk 0 (GROUPS) is not the first chunk"));
        }

        var seenTypes = new HashSet<ushort>();
        foreach (var chunk in geometry.Header.Chunks)
        {
            if (!seenTypes.Add(chunk.Type))
            {
                issues.Add(new GeoStructureIssue("chunk", chunk.DataOffset, $"duplicate chunk type {chunk.Type}"));
            }

            if (chunk.DataOffset < 0 || chunk.Size < 0 ||
                chunk.DataOffset > geometry.Stream.Length - chunk.Size)
            {
                issues.Add(new GeoStructureIssue(
                    "chunk",
                    chunk.DataOffset,
                    $"type {chunk.Type} range (size 0x{chunk.Size:X}) is outside the file"));
            }
        }
    }

    private static void ValidateGroup(
        GeoGroup group,
        IReadOnlyList<GeoRadix> radices,
        IReadOnlyList<GeoBlock> blocks,
        List<GeoStructureIssue> issues,
        ref int radixReferences)
    {
        if (group.MaxX <= 0 || group.MaxY <= 0 || group.MaxZ <= 0)
        {
            issues.Add(new GeoStructureIssue("group", group.DataOffset, "grid dimensions must be positive"));
            return;
        }

        int expectedRadices;
        try
        {
            expectedRadices = checked(group.MaxX * group.MaxY * group.MaxZ);
        }
        catch (OverflowException)
        {
            issues.Add(new GeoStructureIssue("group", group.DataOffset, "grid cell count overflows Int32"));
            return;
        }

        if (radices.Count != expectedRadices)
        {
            issues.Add(new GeoStructureIssue(
                "group",
                group.DataOffset,
                $"grid predicts {expectedRadices} radix records but {radices.Count} were read"));
        }

        if (group.TypesCount < 0 || group.RadixSize < 2 + group.TypesCount)
        {
            issues.Add(new GeoStructureIssue(
                "group",
                group.DataOffset,
                $"radix size {group.RadixSize} cannot hold offset plus {group.TypesCount} type bytes"));
            return;
        }

        var predictedBlockBytes = group.HeadSize - (group.BlockOffset - group.DataOffset);
        if (predictedBlockBytes < 0 || predictedBlockBytes % BlockStride != 0 ||
            predictedBlockBytes / BlockStride != blocks.Count)
        {
            issues.Add(new GeoStructureIssue(
                "group",
                group.BlockOffset,
                $"head size predicts {predictedBlockBytes / BlockStride} blocks but {blocks.Count} were read"));
        }

        for (var radixIndex = 0; radixIndex < radices.Count; radixIndex++)
        {
            var radix = radices[radixIndex];
            var radixOffset = group.DataOffset + radixIndex * group.RadixSize;
            if (radix.Types.Length != group.TypesCount)
            {
                issues.Add(new GeoStructureIssue(
                    "radix",
                    radixOffset,
                    $"expected {group.TypesCount} type bytes, found {radix.Types.Length}"));
                continue;
            }

            if (radix.Offset == EmptyRadix)
            {
                if (radix.Types.Any(typeOffset => typeOffset != MissingType))
                {
                    issues.Add(new GeoStructureIssue("radix", radixOffset, "empty cell has a populated type offset"));
                }
                continue;
            }

            if (radix.Offset < 0)
            {
                issues.Add(new GeoStructureIssue("radix", radixOffset, $"negative first-block index {radix.Offset}"));
                continue;
            }

            for (var type = 0; type < radix.Types.Length; type++)
            {
                var typeOffset = radix.Types[type];
                if (typeOffset == MissingType)
                {
                    continue;
                }

                radixReferences++;
                var firstBlock = radix.Offset + typeOffset;
                if ((uint)firstBlock >= (uint)blocks.Count)
                {
                    issues.Add(new GeoStructureIssue(
                        "radix",
                        radixOffset,
                        $"type {type} predicts block {firstBlock}, outside {blocks.Count} blocks"));
                    continue;
                }

                var blockIndex = firstBlock;
                while (true)
                {
                    var block = blocks[blockIndex];
                    var storedType = block.Flag >> 4;
                    if (storedType != type)
                    {
                        issues.Add(new GeoStructureIssue(
                            "radix",
                            radixOffset,
                            $"type {type} predicts block {blockIndex}, whose stored type is {storedType}"));
                        break;
                    }

                    if ((block.Flag & 1) != 0)
                    {
                        break;
                    }

                    blockIndex++;
                    if (blockIndex >= blocks.Count)
                    {
                        issues.Add(new GeoStructureIssue(
                            "radix",
                            radixOffset,
                            $"type {type} block chain has no terminating flag"));
                        break;
                    }
                }
            }
        }
    }

    private static int ValidateBlock(
        GeomFile geometry,
        GeoBlock block,
        List<GeoStructureIssue> issues)
    {
        if (!geometry.BlockFaceData.TryGetValue(block, out var faces))
        {
            if (block.GeomCount != 0)
            {
                issues.Add(new GeoStructureIssue(
                    "block",
                    block.Offset,
                    $"stores {block.GeomCount} GEOMs without a face arena"));
            }
            return 0;
        }

        if (faces.Count != block.GeomCount)
        {
            issues.Add(new GeoStructureIssue(
                "block",
                block.Offset,
                $"n_geom is {block.GeomCount}, parser found {faces.Count}"));
        }

        if (faces.Count == 0)
        {
            if (block.Head != MissingBlockOffset || block.Tail != MissingBlockOffset)
            {
                issues.Add(new GeoStructureIssue("block", block.Offset, "empty active list does not use 0xFFFF head/tail"));
            }
            if (block.Free != MissingBlockOffset)
            {
                issues.Add(new GeoStructureIssue("block", block.Offset, "empty free list does not use 0xFFFF"));
            }
            if (block.Size != 0)
            {
                issues.Add(new GeoStructureIssue("block", block.Offset, $"empty arena has non-zero high-water size 0x{block.Size:X}"));
            }
            return 0;
        }

        if (block.Free != MissingBlockOffset)
        {
            issues.Add(new GeoStructureIssue(
                "block",
                block.Offset,
                $"serialized block contains an unexpected allocator free-list head {FormatOffset(block.Free)}"));
        }

        var byArenaUnit = new Dictionary<int, Geom>();
        foreach (var face in faces)
        {
            var relativeOffset = face.Offset - block.FaceOffset;
            if (relativeOffset < 0 || relativeOffset % GeomOffsetUnit != 0)
            {
                issues.Add(new GeoStructureIssue(
                    "geom",
                    face.Offset,
                    $"record is not 16-byte aligned relative to arena 0x{block.FaceOffset:X}"));
                continue;
            }

            if (!byArenaUnit.TryAdd(relativeOffset / GeomOffsetUnit, face))
            {
                issues.Add(new GeoStructureIssue("geom", face.Offset, "duplicate arena offset"));
            }
        }

        if (block.Head == MissingBlockOffset || !byArenaUnit.TryGetValue(block.Head, out var current))
        {
            issues.Add(new GeoStructureIssue("block", block.Offset, $"head {FormatOffset(block.Head)} is not an active GEOM"));
            return faces.Count;
        }

        var visited = new HashSet<Geom>();
        TraverseList(current, isRootList: true);

        if (visited.Count != faces.Count)
        {
            issues.Add(new GeoStructureIssue(
                "block",
                block.Offset,
                $"active/child graph reaches {visited.Count} of {faces.Count} stored GEOMs"));
        }

        if (block.Size == 0 || block.Size > ReleaseArenaSize)
        {
            issues.Add(new GeoStructureIssue(
                "block",
                block.Offset,
                $"arena high-water size 0x{block.Size:X} is outside 1..0x{ReleaseArenaSize:X}"));
        }
        else
        {
            geometry.BlockVertexData.TryGetValue(block, out var vertexHeader);
            var predictedSize = GeoBlockArenaLayout.CalculateHighWater(faces, vertexHeader);
            if (block.Size != predictedSize)
            {
                issues.Add(new GeoStructureIssue(
                    "block",
                    block.Offset,
                    $"arena high-water size is 0x{block.Size:X}; packed records/vertices predict 0x{predictedSize:X}"));
            }
        }

        return faces.Count;

        void TraverseList(Geom first, bool isRootList)
        {
            Geom? previous = null;
            var item = first;
            while (true)
            {
                if (!visited.Add(item))
                {
                    issues.Add(new GeoStructureIssue("geom", item.Offset, "active/child graph contains a cycle"));
                    return;
                }

                var currentUnit = (item.Offset - block.FaceOffset) / GeomOffsetUnit;
                if (previous == null)
                {
                    if (item.Prev != 0)
                    {
                        issues.Add(new GeoStructureIssue("geom", item.Offset, "sibling-list head has a non-zero previous link"));
                    }
                }
                else
                {
                    var previousUnit = (previous.Offset - block.FaceOffset) / GeomOffsetUnit;
                    if (currentUnit + item.Prev != previousUnit)
                    {
                        issues.Add(new GeoStructureIssue("geom", item.Offset, "previous link does not return to prior sibling"));
                    }
                }

                if (item.Child != 0)
                {
                    var childUnit = currentUnit + item.Child;
                    if (byArenaUnit.TryGetValue(childUnit, out var child))
                    {
                        TraverseList(child, isRootList: false);
                    }
                    else
                    {
                        issues.Add(new GeoStructureIssue(
                            "geom",
                            item.Offset,
                            $"child link predicts missing arena unit 0x{childUnit:X}"));
                    }
                }

                if (item.Next == 0)
                {
                    if (isRootList && currentUnit != block.Tail)
                    {
                        issues.Add(new GeoStructureIssue(
                            "block",
                            block.Offset,
                            $"tail is {FormatOffset(block.Tail)}, root list ends at 0x{currentUnit:X}"));
                    }
                    return;
                }

                var nextUnit = currentUnit + item.Next;
                if (!byArenaUnit.TryGetValue(nextUnit, out var next))
                {
                    issues.Add(new GeoStructureIssue(
                        "geom",
                        item.Offset,
                        $"next link predicts missing arena unit 0x{nextUnit:X}"));
                    return;
                }

                previous = item;
                item = next;
            }
        }
    }

    private static void ValidateEffect(
        GeoEffect effect,
        int chunkSize,
        List<GeoStructureIssue> issues)
    {
        ValidateEffectSlot("position", GeoEffectLayout.GetPositionSlot(effect.Index), 0x10);
        ValidateEffectSlot("rotation", GeoEffectLayout.GetRotationSlot(effect.Index), 6);
        ValidateEffectSlot("scale", GeoEffectLayout.GetScaleSlot(effect.Index), 0x10);

        void ValidateEffectSlot(string name, int slot, int payloadSize)
        {
            if (slot == 0)
            {
                return;
            }

            var offset = checked(effect.ChunkOffset + slot * 8);
            if (offset < 0 || offset > chunkSize - payloadSize)
            {
                issues.Add(new GeoStructureIssue(
                    "effect",
                    effect.ChunkOffset,
                    $"{name} slot {slot} points outside chunk 6 at 0x{offset:X}"));
            }
        }
    }

    private static string FormatOffset(ushort value) =>
        value == MissingBlockOffset ? "0xFFFF" : $"0x{value:X}";
}
