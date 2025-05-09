using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Haven.Parser.Geom;
using Haven.Parser.Geom.Prim;
using Haven.Render;
using OpenTK;
using Serilog;

// GeomFile has gotten out of hand, needs refactoring...

namespace Haven.Parser
{
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
        GeomUnknownEntries[] Entries;
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

    [StructLayout(LayoutKind.Sequential)]
    public class GeomProp // GEO_EFFECT
    {
        public int Size;
        public int Flag;
        public uint Hash;
        public uint Field00C;
        public float X;
        public float Z;
        public float Y;
        public float W;
        public byte[] Data;
    }

    public class GeomPropGroup
    {
        public readonly List<GeomProp> Children = new List<GeomProp>();
        public readonly uint Hash;
        public readonly int Size;

        public GeomPropGroup(uint hash, int size)
        {
            Hash = hash;
            Size = size;
        }
    }

    public class GeomRefRegionLink
    {
        public uint[] Offsets = new uint[0x1C]; // this can actually be larger
    }

    public class GeomFile
    {
        public readonly Stream Stream;
        public readonly BinaryReaderEx Reader;
        public readonly GeoDef Header;

        public readonly List<GeoGroup> GeomGroups = new List<GeoGroup>();
        public readonly List<GeoPrimRef> GeomRefs = new List<GeoPrimRef>();
        public readonly List<GeomProp> GeomProps = new List<GeomProp>();
        public readonly List<GeomPropGroup> GeomPropGroups = new List<GeomPropGroup>();
        public readonly List<GeoBlock> GeomBlocks = new List<GeoBlock>();

        // yikes
        public readonly Dictionary<GeoGroup, List<GeoBlock>> GeomGroupBlocks = new Dictionary<GeoGroup, List<GeoBlock>>();
        public readonly Dictionary<GeoGroup, GeoMaterialHeader> GroupMaterialData = new Dictionary<GeoGroup, GeoMaterialHeader>();
        public readonly Dictionary<GeoGroup, List<GeoRadix>> GroupRadixData = new Dictionary<GeoGroup, List<GeoRadix>>();
        public readonly Dictionary<GeoBlock, GeoMaterialHeader> GeomRefBlockMaterial = new Dictionary<GeoBlock, GeoMaterialHeader>();
        public readonly Dictionary<GeoBlock, List<Geom.Geom>> BlockFaceData = new Dictionary<GeoBlock, List<Geom.Geom>>();
        public readonly Dictionary<GeoBlock, GeoVertexHeader> BlockVertexData = new Dictionary<GeoBlock, GeoVertexHeader>();
        public readonly Dictionary<GeoBlock, GeoMaterialHeader> BlockMaterialData = new Dictionary<GeoBlock, GeoMaterialHeader>();
        public readonly Dictionary<GeoPrimRef, List<GeoBlock>> GeomRefBlocks = new Dictionary<GeoPrimRef, List<GeoBlock>>();
        
        private GeomRefRegionLink GeomRefRegionLinks = new GeomRefRegionLink();

        // temp
        public List<GeoBlock> GeomBlocksUnk = new List<GeoBlock>();
        public byte[] GeomChunk5 = new byte[0];
        public byte[] GeomChunk7 = new byte[0];
        public uint UnkHash = 0;

        public GeomFile(string path, bool? isBigEndian = null)
        {
            Log.Information("Loading geom \"{path}\" in {endianness} mode", path, isBigEndian == null ? (BinaryReaderEx.DefaultBigEndian ? "BE" : "LE") : (isBigEndian == true? "BE" : "LE"));

            Stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            Reader = new BinaryReaderEx(Stream, isBigEndian);
            Header = new GeoDef(Reader);

            Stream.Seek(0x8, SeekOrigin.Current);
            UnkHash = Reader.ReadUInt32();
            Stream.Seek(0x18, SeekOrigin.Current);

            LoadGroups();
            LoadProps();
            LoadReferences();
            LoadChunk5();
            LoadChunk7();

            Log.Information("Finished loading geom.");
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
            GeomProps.Clear();
            GeomPropGroups.Clear();
            GeomBlocks.Clear();
            GeomGroupBlocks.Clear();
            GeomRefBlocks.Clear();
            GeomRefBlockMaterial.Clear();
            BlockFaceData.Clear();
            BlockVertexData.Clear();
            GroupMaterialData.Clear();
            GroupRadixData.Clear();
            BlockMaterialData.Clear();
        }

        public GeoChunk? GetChunkFromType(GeoChunkType type)
        {
            return Header.Chunks.Find(c => c.Type == (ushort)type);
        }

        private GeoBlock? FindBlockFromOffsets(List<GeoBlock> list, int vertexOffset, int faceOffset)
        {
            return list.Find(block => block.VertexOffset == vertexOffset && block.FaceOffset == faceOffset);
        }

        private void ReadBlockData(GeoBlock block)
        {
            if (block.VertexOffset > Stream.Length)
            {
                Log.Error("Invalid block vertex offset {offset}!", block.VertexOffset);
                return;
            }

            if (block.FaceOffset > Stream.Length)
            {
                Log.Error("Invalid block face offset {offset}!", block.FaceOffset);
                return;
            }

            if (block.FaceOffset > Stream.Length)
            {
                Log.Error("Invalid block material offset {offset}!", block.MaterialOffset);
                return;
            }

            if (block.FaceOffset > 0)
            {
                Stream.Seek(block.FaceOffset, SeekOrigin.Begin);

                BlockFaceData[block] = new List<Geom.Geom>();

                for (int n = 0; n < block.GeomCount; n++)
                {
                    var face = new Geom.Geom(Reader);

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

                    Log.Debug("Found materials in group {groupNum}: {mats}", i, String.Join(", ", GroupMaterialData[group].Materials.Select(p => DictionaryFile.GetHashString(p)).ToArray()));
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

        private void LoadPropEntry(GeoChunk chunk, long offset, int entrySize, GeomPropGroup group)
        {
            if (offset > chunk.DataOffset + chunk.Size) return;

            Stream.Seek(offset, SeekOrigin.Begin);

            while (Stream.Position < chunk.DataOffset + chunk.Size && Stream.Position < offset + entrySize)
            {
                var prop = new GeomProp();
                prop.Size = Reader.ReadInt32();
                prop.Flag = Reader.ReadInt32();
                prop.Hash = Reader.ReadUInt32();
                prop.Field00C = Reader.ReadUInt32();
                prop.Data = new byte[0];

                if (Stream.Position >= chunk.DataOffset + chunk.Size)
                {
                    group.Children.Add(prop);
                    GeomProps.Add(prop);
                    break;
                }

                if (prop.Flag > 0)
                {
                    var newPropGroup = new GeomPropGroup(prop.Hash, prop.Size);
                    GeomPropGroups.Add(group);
                    GeomProps.Add(prop);

                    LoadPropEntry(chunk, Stream.Position, prop.Size, newPropGroup);

                    continue;
                }

                if (prop.Size == 0)
                {
                    var bytesRemaining = entrySize <= 0 ? 0x10 : (int)(entrySize - ((Stream.Position - offset) + 0x10));

                    if (bytesRemaining < 0 || bytesRemaining + Stream.Position > chunk.DataOffset + chunk.Size)
                    {
                        group.Children.Add(prop);
                        GeomProps.Add(prop);
                        break;
                    }

                    if (bytesRemaining >= 0x10)
                    {
                        prop.X = Reader.ReadSingle();
                        prop.Z = Reader.ReadSingle();
                        prop.Y = Reader.ReadSingle();
                        prop.W = Reader.ReadSingle();

                        bytesRemaining -= 0x10;
                    }

                    if (bytesRemaining > 0)
                    {
                        prop.Data = Reader.ReadBytes(bytesRemaining);
                    }
                }
                else if (prop.Size >= 0x20)
                {
                    prop.X = Reader.ReadSingle();
                    prop.Z = Reader.ReadSingle();
                    prop.Y = Reader.ReadSingle();
                    prop.W = Reader.ReadSingle();
                    prop.Data = Reader.ReadBytes(prop.Size - 0x20);
                }
                else if (prop.Size - 0x10 > 0)
                {
                    prop.Data = Reader.ReadBytes(prop.Size - 0x10);
                }

                group.Children.Add(prop);
                GeomProps.Add(prop);
            }
        }

        private void LoadProps()
        {
            GeoChunk? chunk = GetChunkFromType(GeoChunkType.PROPS);

            if (chunk == null)
                return;

            GeomPropGroup root = new GeomPropGroup(0, 0);
            GeomPropGroups.Add(root);
            LoadPropEntry(chunk, chunk.DataOffset, chunk.Size, root);
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

            // region link
            for (int i = 0; i < GeomRefRegionLinks.Offsets.Length; i++)
            {
                GeomRefRegionLinks.Offsets[i] = Reader.ReadUInt32();
            }

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

        private void WriteProps(Stream stream, BinaryWriterEx writer)
        {
            foreach (var spawn in GeomProps)
            {
                writer.Write(spawn.Size);
                writer.Write(spawn.Flag);
                writer.Write(spawn.Hash);
                writer.Write(spawn.Field00C);

                if (spawn.Flag > 0)
                    continue;

                if (spawn.X != 0 || spawn.Y != 0 || spawn.Z != 0 || spawn.W != 0) // todo: find the correct logic for this
                {
                    writer.Write(spawn.X);
                    writer.Write(spawn.Z);
                    writer.Write(spawn.Y);
                    writer.Write(spawn.W);
                }

                writer.Write(spawn.Data);
            }
        }

        private void WriteBlockData(GeoBlock block, BinaryWriterEx writer)
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

        private void WriteBlock(GeoBlock block, BinaryWriterEx writer)
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

            while(blockIndex >= 0)
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

        private void WriteGroup(GeoGroup group, BinaryWriterEx writer)
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

        private void WriteHeader(BinaryWriterEx writer)
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
            foreach (var prop in geomFile.GeomProps)
            {
                var existingProp = GeomProps.Find(p => p.Hash == prop.Hash);

                if (existingProp != null)
                {
                    existingProp.X = prop.X;
                    existingProp.Y = prop.Y;
                    existingProp.Z = prop.Z;

                    if (existingProp.Data.Length == prop.Data.Length)
                    {
                        existingProp.Data = prop.Data;
                    }
                }
            }
        }

        private void WriteGroupsHeader(BinaryWriterEx writer)
        {
            foreach (var group in GeomGroups)
            {
                group.WriteTo(writer);
            }
        }

        public void Save(string path, bool? bigEndian = null)
        {
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            var writer = new BinaryWriterEx(stream, bigEndian);
            long position = 0;

            stream.SetLength(0);

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
            chunk = GetChunkFromType(GeoChunkType.REFS);
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
                writer.Write(GeomRefRegionLinks.Offsets[i]);
            }

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
            chunk = GetChunkFromType(GeoChunkType.UNKOWN);
            chunk.DataOffset = (int)stream.Position;
            writer.Write(GeomChunk5);
            chunk.Size = (int)stream.Position - chunk.DataOffset;

            // chunk 6
            chunk = GetChunkFromType(GeoChunkType.PROPS);
            chunk.DataOffset = (int)stream.Position;

            if (chunk != null)
            {
                // write props
                WriteProps(stream, writer);

                chunk.Size = (int)stream.Position - chunk.DataOffset;
            }

            // chunk 7
            chunk = GetChunkFromType(GeoChunkType.ROUTES);
            chunk.DataOffset = (int)stream.Position;
            chunk.Size = GeomChunk7.Length;
            writer.Write(GeomChunk7);

            // Update header 
            stream.Seek(0, SeekOrigin.Begin);
            WriteHeader(writer);

            stream.Close();
            writer.Close();
        }

    }
}
