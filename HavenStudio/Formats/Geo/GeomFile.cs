using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using HavenStudio.Extensions;
using HavenStudio.Utils;
using OpenTK.Mathematics;
using String = System.String;

// GeomFile has gotten out of hand, needs refactoring...

namespace HavenStudio.Formats.Geo;

    public enum GeoChunkType
    {
        GROUPS = 0,
        REFS = 1,
        UNKOWN = 5,
        PROPS = 6,
        ROUTES = 7,
    }

    [StructLayout(LayoutKind.Sequential)]
    public class GeomUnknown5 // TYPE_5
    {
        int Field000;
        int EntriesCount;
        int Field008;
        int Field00C;
        int Field010;
        int Field014;
        int Field018;
        int Field01C;
        GeomUnknownEntries[] Entries = System.Array.Empty<GeomUnknownEntries>();
    }

    [StructLayout(LayoutKind.Sequential)]
    public class GeomUnknownEntries // TYPE_5
    {
        float Field000;
        float Field004;
        float Field008;
        float Field00C;
        int Field010;
        int Field014;
        int Field018;
        float Field01C;
    }

    public class GeomRefRegionLink
    {
        public uint[] Offsets;

        public GeomRefRegionLink(int offsetCount = 0x1C)
        {
            if (offsetCount < 0) throw new ArgumentOutOfRangeException(nameof(offsetCount));
            Offsets = new uint[offsetCount];
        }

        public static GeomRefRegionLink ReadFrom(EndianBinaryReader reader, int offsetCount)
        {
            ArgumentNullException.ThrowIfNull(reader);
            var links = new GeomRefRegionLink(offsetCount);
            for (var i = 0; i < links.Offsets.Length; i++)
            {
                links.Offsets[i] = reader.ReadUInt32();
            }

            return links;
        }

        public void WriteTo(EndianBinaryWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);
            foreach (var offset in Offsets)
            {
                writer.Write(offset);
            }
        }
    }

    public class GeomFile
    {
        public readonly Stream Stream;
        public readonly EndianBinaryReader Reader;
        public readonly GeoDef Header;

        public readonly List<GeoGroup> GeomGroups = new List<GeoGroup>();
        public readonly List<GeoPrimRef> GeomRefs = new List<GeoPrimRef>();
        public readonly List<GeoEffect> GeoEffects = new List<GeoEffect>();
        public readonly List<GeoBlock> GeomBlocks = new List<GeoBlock>();

        // yikes
        public readonly Dictionary<GeoGroup, List<GeoBlock>> GeomGroupBlocks = new Dictionary<GeoGroup, List<GeoBlock>>();

        public readonly Dictionary<GeoGroup, GeoMaterialHeader> GroupMaterialData =
            new Dictionary<GeoGroup, GeoMaterialHeader>();

        public readonly Dictionary<GeoGroup, List<GeoRadix>> GroupRadixData = new Dictionary<GeoGroup, List<GeoRadix>>();

        public readonly Dictionary<GeoBlock, GeoMaterialHeader> GeomRefBlockMaterial =
            new Dictionary<GeoBlock, GeoMaterialHeader>();

        public readonly Dictionary<GeoBlock, List<Geom>> BlockFaceData = new Dictionary<GeoBlock, List<Geom>>();
        public readonly Dictionary<GeoBlock, GeoVertexHeader> BlockVertexData = new Dictionary<GeoBlock, GeoVertexHeader>();

        public readonly Dictionary<GeoBlock, GeoMaterialHeader> BlockMaterialData =
            new Dictionary<GeoBlock, GeoMaterialHeader>();

        public readonly Dictionary<GeoPrimRef, List<GeoBlock>> GeomRefBlocks = new Dictionary<GeoPrimRef, List<GeoBlock>>();

        private GeomRefRegionLink GeomRefRegionLinks = new GeomRefRegionLink();

        // temp
        public List<GeoBlock> GeomBlocksUnk = new List<GeoBlock>();
        public byte[] GeomChunk5 = new byte[0];
        public byte[] GeomChunk6 = new byte[0];
        public byte[] GeomChunk7 = new byte[0];
        public uint UnkHash = 0;

        public GeomFile(string path, Endianness? endianness = null)
            : this(
                new FileStream(path, FileMode.Open, FileAccess.Read),
                endianness ?? EndianBinaryReader.DefaultEndianness)
        {
        }

        public GeomFile(Stream stream, Endianness endianness)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("GEOM reading requires a readable, seekable stream.", nameof(stream));
            }

            Stream = stream;
            Reader = new EndianBinaryReader(Stream, endianness);
            try
            {
                Header = new GeoDef(Reader);

                Stream.Seek(0x8, SeekOrigin.Current);
                UnkHash = Reader.ReadUInt32();
                Stream.Seek(0x18, SeekOrigin.Current);

                LoadGroups();
                LoadEffects();
                LoadReferences();
                LoadChunk5();
                LoadChunk7();
            }
            catch
            {
                Reader.Dispose();
                throw;
            }

            // Log.Information("Finished loading geom.");
        }

        public void CloseStream()
        {
            Reader.Close();
            Stream.Close();
        }

        public void Clear()
        {
            GeomGroups.Clear();
            GeomRefs.Clear();
            GeoEffects.Clear();
            GeomBlocks.Clear();
            GeomGroupBlocks.Clear();
            GeomRefBlocks.Clear();
            GeomRefBlockMaterial.Clear();
            BlockFaceData.Clear();
            BlockVertexData.Clear();
            GroupMaterialData.Clear();
            GroupRadixData.Clear();
            BlockMaterialData.Clear();
            GeomChunk6 = new byte[0];
        }

        public GeoChunk? GetChunkFromType(GeoChunkType type)
        {
            return Header.Chunks.Find(c => c.Type == (ushort)type);
        }

        private GeoChunk GetRequiredChunk(GeoChunkType type)
        {
            return GetChunkFromType(type)
                ?? throw new InvalidDataException($"GEOM is missing required {type} chunk.");
        }

        private GeoBlock? FindBlockFromOffsets(List<GeoBlock> list, int vertexOffset, int faceOffset)
        {
            return list.Find(block => block.VertexOffset == vertexOffset && block.FaceOffset == faceOffset);
        }

        private void ReadBlockData(GeoBlock block)
        {
            if (block.VertexOffset > Stream.Length)
            {
                // Log.Error("Invalid block vertex offset {offset}!", block.VertexOffset);
                return;
            }

            if (block.FaceOffset > Stream.Length)
            {
                // Log.Error("Invalid block face offset {offset}!", block.FaceOffset);
                return;
            }

            if (block.FaceOffset > Stream.Length)
            {
                // Log.Error("Invalid block material offset {offset}!", block.MaterialOffset);
                return;
            }

            if (block.FaceOffset > 0)
            {
                Stream.Seek(block.FaceOffset, SeekOrigin.Begin);

                BlockFaceData[block] = new List<Geom>();

                for (int n = 0; n < block.GeomCount; n++)
                {
                    var face = new Geom(Reader);

                    BlockFaceData[block].Add(face);
                }
            }

            if (block.VertexOffset > 0)
            {
                Stream.Seek(block.VertexOffset, SeekOrigin.Begin);

                var vert = new GeoVertexHeader(Reader);
                BlockVertexData[block] = vert;
            }

            if (block.MaterialOffset > 0)
            {
                Stream.Seek(block.MaterialOffset, SeekOrigin.Begin);
                BlockMaterialData[block] = new GeoMaterialHeader(Reader);
            }
        }

        private void LoadGroups()
        {
            while (true)
            {
                GeoGroup group = new GeoGroup(Reader);

                GeomGroups.Add(group);
                GeomGroupBlocks[group] = new List<GeoBlock>();

                if (group.Flag == 1)
                    break;
            }

            for (int i = 0; i < GeomGroups.Count; i++)
            {
                GeoGroup group = GeomGroups[i];

                Stream.Seek(group.DataOffset, SeekOrigin.Begin);
                GroupRadixData[group] = ReadRadix(group);

                int indexLength = group.BlockOffset - group.DataOffset;
                indexLength = group.HeadSize - indexLength;
                indexLength = indexLength / 16;
                indexLength = indexLength / 2;

                Stream.Seek(group.BlockOffset, SeekOrigin.Begin);

                for (int y = 0; y < indexLength; y++)
                {
                    var pos = Stream.Position;

                    GeoBlock block = new GeoBlock(Reader);
                    GeomGroupBlocks[group].Add(block);
                    GeomBlocks.Add(block);
                    ReadBlockData(block);

                    Stream.Seek(pos + 0x20, SeekOrigin.Begin);
                }

                if (group.MaterialOffset > 0)
                {
                    Stream.Seek(group.MaterialOffset, SeekOrigin.Begin);

                    GroupMaterialData[group] = new GeoMaterialHeader(Reader);

                    // Log.Debug("Found materials in group {groupNum}: {mats}", i, String.Join(", ", GroupMaterialData[group].Materials.Select(p => DictionaryFile.GetHashString(p)).ToArray()));
                }

            }
        }

        private void LoadChunk5()
        {
            GeoChunk? chunk = GetChunkFromType(GeoChunkType.UNKOWN);
            GeomChunk5 = new byte[0];

            if (chunk == null)
                return;

            Stream.Seek(chunk.DataOffset, SeekOrigin.Begin);
            GeomChunk5 = Reader.ReadBytes(chunk.Size);
        }

        private void LoadChunk7() // training dummy routes
        {
            GeoChunk? chunk = GetChunkFromType(GeoChunkType.ROUTES);
            GeomChunk7 = new byte[0];

            if (chunk == null)
                return;

            Stream.Seek(chunk.DataOffset, SeekOrigin.Begin);
            GeomChunk7 = Reader.ReadBytes((int)(Stream.Length - Stream.Position));
            chunk.Size = GeomChunk7.Length;
        }

        private int ReadEffect(EndianBinaryReader reader, int indexEffect, List<GeoEffect>? effects, GeoEffect? parent)
        {
            reader.BaseStream.Seek(indexEffect, SeekOrigin.Begin);

            int next = reader.ReadInt32();
            int child = reader.ReadInt32();
            int name = reader.ReadInt32();
            int index = reader.ReadInt32();

            var effect = new GeoEffect
            {
                Name = name,
                Index = index,
                ChunkOffset = indexEffect
            };

            var positionSlot = GeoEffectLayout.GetPositionSlot(index);
            if (positionSlot != 0)
            {
                var positionOffset = GeoEffectLayout.GetPositionOffset(effect);
                if (positionOffset < 0 || positionOffset > GeomChunk6.Length - 0x10)
                {
                    throw new InvalidDataException(
                        $"GEOM effect 0x{unchecked((uint)name):X8} position points outside chunk 6 at 0x{positionOffset:X}.");
                }

                reader.BaseStream.Seek(positionOffset, SeekOrigin.Begin);
                effect.X = reader.ReadSingle();
                effect.Y = reader.ReadSingle();
                effect.Z = reader.ReadSingle();
                effect.W = reader.ReadSingle();
            }

            var rotationSlot = GeoEffectLayout.GetRotationSlot(index);
            var rotationOffset = GeoEffectLayout.GetRotationOffset(effect);
            if (rotationSlot != 0 && rotationOffset >= 0 && rotationOffset <= GeomChunk6.Length - 6)
            {
                var rotationData = GeomChunk6.AsSpan(rotationOffset, 6);
                effect.RotationX = GeoEffectChunkPatcher.DecodeAngle(
                    ReadEffectAngle(rotationData));
                effect.RotationY = GeoEffectChunkPatcher.DecodeAngle(
                    ReadEffectAngle(rotationData[2..]));
                effect.RotationZ = GeoEffectChunkPatcher.DecodeAngle(
                    ReadEffectAngle(rotationData[4..]));
            }

            if (child != 0)
            {
                for (int indexChildEffect = indexEffect + child, childNext = -1; childNext != 0; indexChildEffect += childNext)
                {
                    childNext = ReadEffect(reader, indexChildEffect, null, effect);
                }
            }

            if (parent != null)
            {
                parent.Children.Add(effect);
            }

            effects?.Add(effect);

            return next;

            short ReadEffectAngle(ReadOnlySpan<byte> data)
            {
                return Reader.Endianness == Endianness.Big
                    ? System.Buffers.Binary.BinaryPrimitives.ReadInt16BigEndian(data)
                    : System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(data);
            }
        }

        private void LoadEffects()
        {
            GeoChunk? chunk = GetChunkFromType(GeoChunkType.PROPS);
            GeomChunk6 = new byte[0];

            if (chunk == null)
                return;

            Stream.Seek(chunk.DataOffset, SeekOrigin.Begin);
            GeomChunk6 = Reader.ReadBytes(chunk.Size);

            using var ms = new MemoryStream(GeomChunk6, writable: false);
            using var effectReader = new EndianBinaryReader(ms, Reader.Endianness, leaveOpen: true);

            var effects = new List<GeoEffect>();

            for (int indexEffect = 0, next = -1; next != 0; indexEffect += next)
            {
                if (indexEffect < 0 || indexEffect >= effectReader.BaseStream.Length)
                    break;

                next = ReadEffect(effectReader, indexEffect, effects, null);

                if (next == 0)
                    break;
            }

            GeoEffects.AddRange(effects);
        }

        private void LoadReferences()
        {
            GeoChunk? chunk = GetChunkFromType(GeoChunkType.REFS);

            if (chunk == null)
                return;

            Stream.Seek(chunk.DataOffset, SeekOrigin.Begin);

            Debug.WriteLine("Load chunk offset 1 {0:X}", chunk.DataOffset);

            int geoRefSize = 0x70;

            while (Stream.Position < chunk.DataOffset + chunk.Size + geoRefSize)
            {
                GeoPrimRef obj = new GeoPrimRef(Reader);

                if (obj.BlockCount == 0)
                    break;

                GeomRefs.Add(obj);
                GeomRefBlocks[obj] = new List<GeoBlock>();
            }

            Stream.Seek(-0x70, SeekOrigin.Current);

            var regionLinkStart = Stream.Position;
            var regionLinkEnd = GeomRefs.Count > 0
                ? (long)GeomRefs.Min(reference => reference.BlockOffset)
                : checked(regionLinkStart + 0x70);
            var regionLinkLength = regionLinkEnd - regionLinkStart;
            if (regionLinkLength < 0 || regionLinkLength % sizeof(uint) != 0 ||
                regionLinkEnd > checked((long)chunk.DataOffset + chunk.Size))
            {
                throw new InvalidDataException(
                    $"GEOM reference-region link table at 0x{regionLinkStart:X} has an invalid size.");
            }

            GeomRefRegionLinks = GeomRefRegionLink.ReadFrom(
                Reader,
                checked((int)(regionLinkLength / sizeof(uint))));

            List<GeoBlock> ObjectBlocks = new List<GeoBlock>();

            for (int i = 0; i < GeomRefs.Count; i++)
            {
                GeoPrimRef obj = GeomRefs[i];

                Stream.Seek(obj.BlockOffset, SeekOrigin.Begin);

                for (int n = 0; n < obj.BlockCount; n++)
                {
                    GeoBlock block = new GeoBlock(Reader);

                    if (block.Flag != 0x10)
                    {
                        var mats = new GeoMaterialHeader(Reader);
                        GeomRefBlockMaterial[block] = mats;
                    }

                    GeomBlocks.Add(block);
                    ObjectBlocks.Add(block);
                    GeomRefBlocks[obj].Add(block);

                }

                foreach (var block in ObjectBlocks)
                {
                    ReadBlockData(block);
                }
            }

            GeomBlocksUnk = new List<GeoBlock>();

            for (int i = 0; i < GeomRefRegionLinks.Offsets.Length; i++)
            {
                var offset = GeomRefRegionLinks.Offsets[i];

                if (offset > 0)
                {
                    Stream.Seek(offset, SeekOrigin.Begin);
                    GeoBlock block = new GeoBlock(Reader);
                    GeomBlocks.Add(block);
                    GeomBlocksUnk.Add(block);
                    ReadBlockData(block);
                }
            }
        }

        private void WriteEffects(EndianBinaryWriter writer)
        {
            if (GeomChunk6.Length == 0)
                return;

            GeoEffectChunkPatcher.Patch(GeomChunk6, GeoEffects, writer.Endianness);
            writer.Write(GeomChunk6);
        }

        private void WriteBlockData(GeoBlock block, EndianBinaryWriter writer)
        {
            var pos = writer.BaseStream.Position;

            if (block.FaceOffset > 0)
            {
                var faces = BlockFaceData[block];
                writer.BaseStream.Seek(block.Offset, SeekOrigin.Begin);
                block.FaceOffset = (int)pos;
                WriteBlock(block, writer);
                writer.BaseStream.Seek(pos, SeekOrigin.Begin);

                foreach (var face in faces)
                {
                    face.WriteTo(writer);
                }
            }

            if (block.VertexOffset > 0)
            {
                pos = writer.BaseStream.Position;
                writer.BaseStream.Seek(block.Offset, SeekOrigin.Begin);
                block.VertexOffset = (int)pos;
                WriteBlock(block, writer);
                writer.BaseStream.Seek(pos, SeekOrigin.Begin);

                BlockVertexData[block].WriteTo(writer);
            }
        }

        private void WriteBlock(GeoBlock block, EndianBinaryWriter writer)
        {
            block.Offset = (int)writer.BaseStream.Position;

            block.WriteTo(writer);

            if (GeomRefBlockMaterial.ContainsKey(block))
            {
                if (block.MaterialOffset > 0)
                {
                    block.MaterialOffset = (int)writer.BaseStream.Position;
                }

                GeomRefBlockMaterial[block].WriteTo(writer);
            }
        }

        public List<GeoRadix> ReadRadix(GeoGroup group)
        {
            var radixList = new List<GeoRadix>();

            Stream.Seek(group.DataOffset, SeekOrigin.Begin);

            int blockIndex = (group.MaxX * group.MaxY * group.MaxZ) - 1;

            while (blockIndex >= 0)
            {
                var radixOffset = blockIndex * group.RadixSize + group.DataOffset;

                Stream.Seek(radixOffset, SeekOrigin.Begin);

                var radix = new GeoRadix(Reader, group);
                radixList.Add(radix);

                blockIndex = blockIndex - 1;
            }

            radixList.Reverse();

            return radixList;
        }

        public void GetWorldBoundary(ref Vector4 boundaryLow, ref Vector4 boundaryHigh)
        {
            boundaryLow = new Vector4(3.4f, 3.4f, 3.4f, 1.0f);
            boundaryHigh = new Vector4(-3.4f, -3.4f, -3.4f, 1.0f);

            foreach (var group in GeomGroups)
            {
                var vBase = new Vector4(group.BaseX, group.BaseY, group.BaseZ, 1.0f);

                boundaryLow = Vector4.ComponentMin(boundaryLow, vBase);

                var vDiv = new Vector4(group.DivX, group.DivY, group.DivZ, group.DivW);
                var vMax = new Vector4(group.MaxX, group.MaxY, group.MaxZ, 1.0f);

                vMax = Vector4.Multiply(vMax, vDiv);
                vBase = Vector4.Add(vMax, vBase);
                boundaryHigh = Vector4.ComponentMax(boundaryHigh, vBase);
            }
        }

        public void CalculateGroupBoundary(GeoGroup group, ref Vector4 boundaryLow, ref Vector4 gridMax)
        {
            boundaryLow = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, 1.0f);
            var boundaryHigh = new Vector4(float.MinValue, float.MinValue, float.MinValue, 1.0f);
            var div = new Vector4(group.DivX, group.DivY, group.DivZ, group.DivW);

            foreach (var block in GeomGroupBlocks[group])
            {
                if (!BlockVertexData.ContainsKey(block) || BlockVertexData[block].Data.Length == 0)
                    continue;

                var pos = BlockVertexData[block].Data[0];

                boundaryLow = Vector4.ComponentMin(boundaryLow, pos);
                boundaryHigh = Vector4.ComponentMax(boundaryHigh, pos);
            }

            boundaryHigh += div;
            gridMax = Vector4.Divide(Vector4.Subtract(boundaryHigh, boundaryLow), div);
        }

        private void WriteGroup(GeoGroup group, EndianBinaryWriter writer)
        {
            List<GeoBlock> list = new List<GeoBlock>();

            var radixList = GroupRadixData[group];
            var pos = writer.BaseStream.Position;
            group.DataOffset = (int)pos;

            foreach (var radix in radixList)
            {
                radix.WriteTo(writer);
            }

            int bytes = radixList.Count * group.RadixSize;
            int len = (bytes + 0x10 - 1) / 0x10 * 0x10;
            int pad = (len - bytes);
            writer.Write(new byte[pad]);

            var blocks = GeomGroupBlocks[group];

            group.BlockOffset = (int)writer.BaseStream.Position;

            foreach (var block in blocks)
            {
                WriteBlock(block, writer);

                var blockData = FindBlockFromOffsets(GeomGroupBlocks[group], block.VertexOffset, block.FaceOffset);

                if (blockData == null)
                {
                    continue;
                }

                list.Add(blockData);
            }

            group.HeadSize = (int)(writer.BaseStream.Position - pos);

            list.Sort((n1, n2) => n1.FaceOffset.CompareTo(n2.FaceOffset));

            foreach (var block in list)
            {
                WriteBlockData(block, writer);
            }

            if (group.MaterialOffset > 0)
            {
                group.MaterialOffset = (int)writer.BaseStream.Position;
                var mats = GroupMaterialData[group];

                mats.WriteTo(writer);

                pos = writer.BaseStream.Position;
                writer.BaseStream.Seek(group.BlockOffset, SeekOrigin.Begin);
                foreach (var block in blocks)
                {
                    WriteBlock(block, writer);
                }

                writer.BaseStream.Seek(pos, SeekOrigin.Begin);
            }
        }

        private void WriteHeader(EndianBinaryWriter writer)
        {
            Header.WriteTo(writer);

            writer.Write(0x08000000);
            writer.Write(0);
            writer.Write(UnkHash);
            writer.Write(new byte[0x18]);
        }

        public void Merge(GeomFile geomFile)
        {
            GeomGroups.AddRange(geomFile.GeomGroups);

            geomFile.GeomGroupBlocks.ToList().ForEach(x => GeomGroupBlocks.Add(x.Key, x.Value));
            geomFile.GroupMaterialData.ToList().ForEach(x => GroupMaterialData.Add(x.Key, x.Value));
            geomFile.GroupRadixData.ToList().ForEach(x => GroupRadixData.Add(x.Key, x.Value));
            geomFile.GeomRefBlockMaterial.ToList().ForEach(x => GeomRefBlockMaterial.Add(x.Key, x.Value));
            geomFile.BlockFaceData.ToList().ForEach(x => BlockFaceData.Add(x.Key, x.Value));
            geomFile.BlockVertexData.ToList().ForEach(x => BlockVertexData.Add(x.Key, x.Value));
        }

        public void MergeReferences(GeomFile geomFile)
        {
            GeomRefs.AddRange(geomFile.GeomRefs);
            geomFile.GeomRefBlocks.ToList().ForEach(x => GeomRefBlocks.Add(x.Key, x.Value));
            geomFile.GeomRefBlockMaterial.ToList().ForEach(x => GeomRefBlockMaterial.Add(x.Key, x.Value));
            geomFile.BlockFaceData.ToList().ForEach(x => BlockFaceData.Add(x.Key, x.Value));
            geomFile.BlockVertexData.ToList().ForEach(x => BlockVertexData.Add(x.Key, x.Value));
        }

        public void CopySingleRef(GeomFile geomFile, int hash)
        {
            var geoRef = geomFile.GeomRefs.Find(r => r.Hash == hash);

            if (geoRef == null)
                return;

            var geoRefBlock = geomFile.GeomRefBlocks[geoRef];

            GeomRefs.Add(geoRef);
            GeomRefBlocks[geoRef] = geoRefBlock;
            foreach (var block in geoRefBlock)
            {
                GeomRefBlockMaterial[block] = geomFile.GeomRefBlockMaterial[block];
                BlockFaceData[block] = geomFile.BlockFaceData[block];
                BlockVertexData[block] = geomFile.BlockVertexData[block];
            }
        }

        public void MergeExistingProps(GeomFile geomFile)
        {
            var incoming = TreeTraversal.Flatten(geomFile.GeoEffects, effect => effect.Children);
            var current = TreeTraversal.Flatten(GeoEffects, effect => effect.Children).ToList();

            foreach (var effect in incoming)
            {
                var existing = current.Find(e => e.Name == effect.Name && e.Index == effect.Index);

                if (existing != null)
                {
                    existing.X = effect.X;
                    existing.Y = effect.Y;
                    existing.Z = effect.Z;
                    existing.W = effect.W;
                }
            }
        }

        /// <summary>
        /// Replaces this GEOM's effects ("props", chunk 6) with a deep copy of another
        /// GEOM's effects, copying the opaque chunk-6 payload verbatim and adding a PROPS
        /// chunk when this GEOM has none. Returns the number of transported effects.
        /// </summary>
        public int TransportEffectsFrom(GeomFile source)
        {
            ArgumentNullException.ThrowIfNull(source);

            GeoEffects.Clear();
            foreach (var effect in source.GeoEffects)
            {
                GeoEffects.Add(CloneEffect(effect));
            }

            GeomChunk6 = (byte[])source.GeomChunk6.Clone();
            EnsurePropsChunk();

            return TreeTraversal.Flatten(GeoEffects, effect => effect.Children).Count();
        }

        private void EnsurePropsChunk()
        {
            if (GetChunkFromType(GeoChunkType.PROPS) != null)
            {
                return;
            }

            var propsChunk = new GeoChunk(GeoChunkType.PROPS);
            var routesIndex = Header.Chunks.FindIndex(chunk => chunk.Type == (ushort)GeoChunkType.ROUTES);
            if (routesIndex < 0)
            {
                Header.Chunks.Add(propsChunk);
            }
            else
            {
                Header.Chunks.Insert(routesIndex, propsChunk);
            }

            Header.ChunkCount = Header.Chunks.Count;
        }

        private static GeoEffect CloneEffect(GeoEffect source)
        {
            var clone = new GeoEffect
            {
                Name = source.Name,
                Index = source.Index,
                X = source.X,
                Y = source.Y,
                Z = source.Z,
                W = source.W,
                RotationX = source.RotationX,
                RotationY = source.RotationY,
                RotationZ = source.RotationZ,
                ChunkOffset = source.ChunkOffset
            };

            foreach (var child in source.Children)
            {
                clone.Children.Add(CloneEffect(child));
            }

            return clone;
        }

        private void WriteGroupsHeader(EndianBinaryWriter writer)
        {
            foreach (var group in GeomGroups)
            {
                group.WriteTo(writer);
            }
        }

        public void Save(string path, Endianness? endianness = null)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            Save(stream, endianness ?? Reader.Endianness);
        }

        public void Save(Stream stream, Endianness endianness)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite || !stream.CanSeek)
                throw new ArgumentException("GEOM writing requires a writable, seekable stream.", nameof(stream));

            stream.SetLength(0);
            stream.Position = 0;
            using var writer = new EndianBinaryWriter(stream, endianness, leaveOpen: true);
            long position = 0;

            WriteHeader(writer);

            // chunk 0
            GeoChunk chunk = Header.Chunks[0];
            chunk.DataOffset = (int)stream.Position;
            WriteGroupsHeader(writer);

            for (int i = 0; i < GeomGroups.Count; i++)
            {
                GeomGroups[i].Flag = (i == GeomGroups.Count - 1) ? 1 : 0;
                WriteGroup(GeomGroups[i], writer);
            }

            position = stream.Position;
            stream.Seek(chunk.DataOffset, SeekOrigin.Begin);
            WriteGroupsHeader(writer);
            stream.Seek(position, SeekOrigin.Begin);

            chunk.Size = (int)stream.Position - chunk.DataOffset;

            // chunk 1
            chunk = GetRequiredChunk(GeoChunkType.REFS);
            var oldOffset = chunk.DataOffset;
            chunk.DataOffset = (int)stream.Position;
            int diff = chunk.DataOffset - oldOffset;

            var blockPos = stream.Position;
            foreach (var obj in GeomRefs)
            {
                obj.BlockOffset += diff;
                obj.WriteTo(writer);
            }

            for (int i = 0; i < GeomRefRegionLinks.Offsets.Length; i++)
            {
                if (GeomRefRegionLinks.Offsets[i] != 0)
                {
                    GeomRefRegionLinks.Offsets[i] += (uint)diff;
                }
            }
            GeomRefRegionLinks.WriteTo(writer);

            var list = new List<GeoBlock>();
            for (int i = 0; i < GeomRefs.Count; i++)
            {
                GeoPrimRef obj = GeomRefs[i];
                var blocks = GeomRefBlocks[obj];

                var blockDiff = (int)stream.Position - GeomRefs[i].BlockOffset;
                GeomRefs[i].BlockOffset = (int)stream.Position;

                foreach (var block in blocks)
                {
                    if (block.MaterialOffset > 0)
                    {
                        block.MaterialOffset += blockDiff;
                    }

                    WriteBlock(block, writer);

                    var blockData = FindBlockFromOffsets(blocks, block.VertexOffset, block.FaceOffset);

                    if (blockData == null)
                    {
                        continue;
                    }

                    list.Add(blockData);
                }
            }

            list.Sort((n1, n2) => n1.FaceOffset.CompareTo(n2.FaceOffset));
            foreach (var block in list)
            {
                WriteBlockData(block, writer);
            }

            foreach (var block in GeomBlocksUnk)
            {
                WriteBlock(block, writer);
                WriteBlockData(block, writer);
            }

            chunk.Size = (int)stream.Position - chunk.DataOffset;

            stream.Seek(blockPos, SeekOrigin.Begin);
            foreach (var obj in GeomRefs)
            {
                obj.WriteTo(writer);
            }

            // chunk 5
            stream.Seek(0, SeekOrigin.End);
            chunk = GetRequiredChunk(GeoChunkType.UNKOWN);
            chunk.DataOffset = (int)stream.Position;
            writer.Write(GeomChunk5);
            chunk.Size = (int)stream.Position - chunk.DataOffset;

            // chunk 6
            var propsChunk = GetChunkFromType(GeoChunkType.PROPS);
            if (propsChunk != null)
            {
                propsChunk.DataOffset = (int)stream.Position;
                // write props
                WriteEffects(writer);

                propsChunk.Size = (int)stream.Position - propsChunk.DataOffset;
            }

            // chunk 7
            chunk = GetRequiredChunk(GeoChunkType.ROUTES);
            chunk.DataOffset = (int)stream.Position;
            chunk.Size = GeomChunk7.Length;
            writer.Write(GeomChunk7);

            // Update header 
            stream.Seek(0, SeekOrigin.Begin);
            WriteHeader(writer);
            writer.Flush();
        }

    }
