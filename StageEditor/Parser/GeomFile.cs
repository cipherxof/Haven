using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
using System.Security.Policy;

namespace Haven.Parser
{
    public enum ChunkType
    {
        TYPE_0 = 0,
        TYPE_1 = 1,
        TYPE_5 = 5,
        TYPE_6 = 6,
        TYPE_7 = 7,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Geom
    {
        public GeomHeader Header;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GeomHeader
    {
        public uint Version;
        public uint FileSize;
        public int ChunkCount;
        public int Pad;
        public float X;
        public float Y;
        public float Z;
        public float Trans;
        public GeomChunk[] Chunks;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GeomChunk
    {
        public ushort Type;
        public ushort Pad;
        public int Size;
        public int DataOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class GeomGroup
    {
        public float BaseX;
        public float BaseY;
        public float BaseZ;
        public int MaterialOffset;
        public float MaxX;
        public float MaxY;
        public float MaxZ;
        public int HeadSize;
        public int Field024;
        public int Field028;
        public int Field02C;
        public int TotalSize;
        public int Flag;
        public int Field038;
        public int Field03C;
        public int DataOffset;
        public int IndexOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class GeomObject
    {
        public float Field000;
        public float Field004;
        public float Field008;
        public uint Type;
        public float Field010;
        public float Field014;
        public float Field018;
        public uint Pad;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x40)]
        public byte[] Field020;

        public uint Field03C;
        public uint Field040;
        public int IndexOffset;
        public uint Hash;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class GeomIndex
    {
        public byte Type;
        public byte Chunks;
        public ushort Size;
        public ushort Lines;
        public ushort Pad;
        public uint Field010;
        public uint VertexOffset;
        public uint FaceOffset;
    }

    public class GeomProp
    {
        public int Size;
        public int Flag;
        public uint Hash;
        public ushort Field00C;
        public byte Field010;
        public byte Field014;
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

    public class GeomFile
    {
        public readonly Stream Stream;
        public readonly BinaryReaderEx Reader;
        public readonly Geom Geom;
        public readonly List<GeomGroup> GeomGroups = new List<GeomGroup>();
        public readonly List<GeomObject> GeomObjects = new List<GeomObject>();
        public readonly List<GeomProp> GeomProps = new List<GeomProp>();
        public readonly List<GeomPropGroup> GeomPropGroups = new List<GeomPropGroup>();
        public readonly Dictionary<GeomGroup, List<GeomIndex>> GeomGroupsIndex = new Dictionary<GeomGroup, List<GeomIndex>>();
        public readonly Dictionary<GeomObject, List<GeomIndex>> GeomObjectsIndex = new Dictionary<GeomObject, List<GeomIndex>>();

        public GeomFile(string path)
        {
            Geom = new Geom();
            Stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            Reader = new BinaryReaderEx(Stream);

            Geom.Header.Version = Reader.ReadUInt32BE();

            if (Geom.Header.Version != 0x0BF68BFE)
                throw new Exception("Invalid geom version! This can happen if the geom failed to decrypt, please ensure it's in the proper folder hierarchy.");

            Geom.Header.FileSize = Reader.ReadUInt32BE();
            Geom.Header.ChunkCount = Reader.ReadInt32BE();
            Geom.Header.Pad = Reader.ReadInt32BE();
            Geom.Header.X = Reader.ReadSingleBE();
            Geom.Header.Y = Reader.ReadSingleBE();
            Geom.Header.Z = Reader.ReadSingleBE();
            Geom.Header.Trans = Reader.ReadSingleBE();

            Geom.Header.Chunks = new GeomChunk[Geom.Header.ChunkCount];

            for (int i = 0; i < Geom.Header.ChunkCount; i++)
            {
                GeomChunk chunk = Geom.Header.Chunks[i];
                chunk.Type = Reader.ReadUInt16BE();
                chunk.Pad = Reader.ReadUInt16BE();
                chunk.Size = Reader.ReadInt32BE();
                chunk.DataOffset = Reader.ReadInt32BE();
                Geom.Header.Chunks[i] = chunk;
            }

            for (int i = 0; i < Geom.Header.ChunkCount - 1; i++)
            {
                GeomChunk chunk = Geom.Header.Chunks[i];
                GeomChunk nextChunk = Geom.Header.Chunks[i + 1];
                
                var newSize = nextChunk.DataOffset - chunk.DataOffset;

                if (newSize > chunk.Size)
                {
                    Geom.Header.Chunks[i].Size = newSize;
                }
            }

            Stream.Seek(36, SeekOrigin.Current);

            LoadGroups();
            WriteDebugFile();
            LoadProps();
            LoadObjects();
        }

        public void CloseStream()
        {
            Reader.Close();
            Stream.Close();
        }

        public GeomChunk? GetChunkFromType(ChunkType type)
        {
            for (int i = 0; i < Geom.Header.ChunkCount; i++)
            {
                if (Geom.Header.Chunks[i].Type == (ushort)type)
                {
                    return Geom.Header.Chunks[i];
                }
            }

            return null;
        }

        private GeomIndex ReadIndex()
        {
            GeomIndex index = new GeomIndex();
            index.Type = Reader.ReadByte();
            index.Chunks = Reader.ReadByte();
            index.Size = Reader.ReadUInt16BE();
            index.Lines = Reader.ReadUInt16BE();
            index.Pad = Reader.ReadUInt16BE();
            index.Field010 = Reader.ReadUInt32BE();
            index.VertexOffset = Reader.ReadUInt32BE();
            index.FaceOffset = Reader.ReadUInt32BE();
            return index;
        }

        private void LoadGroups()
        {
            while (true)
            {
                GeomGroup group = new GeomGroup();

                group.BaseX = Reader.ReadSingleBE();
                group.BaseY = Reader.ReadSingleBE();
                group.BaseZ = Reader.ReadSingleBE();
                group.MaterialOffset = Reader.ReadInt32BE();
                group.MaxX = Reader.ReadSingleBE();
                group.MaxY = Reader.ReadSingleBE();
                group.MaxZ = Reader.ReadSingleBE();
                group.HeadSize = Reader.ReadInt32BE();
                group.Field024 = Reader.ReadInt32BE();
                group.Field028 = Reader.ReadInt32BE();
                group.Field02C = Reader.ReadInt32BE();
                group.TotalSize = Reader.ReadInt32BE();
                group.Flag = Reader.ReadInt32BE();
                group.Field038 = Reader.ReadInt16BE();
                group.Field03C = Reader.ReadInt16BE();
                group.DataOffset = Reader.ReadInt32BE();
                group.IndexOffset = Reader.ReadInt32BE();

                GeomGroups.Add(group);
                GeomGroupsIndex[group] = new List<GeomIndex>();

                if (group.Flag == 1)
                    break;
            }

            for (int i = 0; i < GeomGroups.Count; i++)
            {
                GeomGroup group = GeomGroups[i];

                int indexLength = group.IndexOffset - group.DataOffset;
                indexLength = group.TotalSize - indexLength;
                indexLength = indexLength / 16;
                indexLength = indexLength / 2;

                Stream.Seek(group.IndexOffset, SeekOrigin.Begin);

                for (int y = 0; y < indexLength; y++)
                {
                    var pos = Stream.Position;

                    GeomIndex index = ReadIndex();
                    GeomGroupsIndex[group].Add(index);

                    Stream.Seek(pos + 0x20, SeekOrigin.Begin);
                }
            }
        }

        private void LoadPropEntry(GeomChunk chunk, long offset, int entrySize, GeomPropGroup group)
        {
            Stream.Seek(offset, SeekOrigin.Begin);

            while (Stream.Position < chunk.DataOffset + chunk.Size && Stream.Position < offset + entrySize)
            {
                var prop = new GeomProp();
                prop.Size = Reader.ReadInt32BE();
                prop.Flag = Reader.ReadInt32BE();
                prop.Hash = Reader.ReadUInt32BE();
                prop.Field00C = Reader.ReadUInt16BE();
                prop.Field010 = Reader.ReadByte();
                prop.Field014 = Reader.ReadByte();
                prop.Data = new byte[0];

                if (prop.Flag > 0)
                {
                    var newPropGroup = new GeomPropGroup(prop.Hash, prop.Size);
                    GeomPropGroups.Add(group);
                    GeomProps.Add(prop);

                    //Debug.WriteLine($"GROUP {DictionaryFile.GetHashString(prop.Hash)} {prop.Size}");
                    LoadPropEntry(chunk, Stream.Position, prop.Size, newPropGroup);

                    continue;
                }

                if (prop.Size == 0)
                {
                    var bytesRemaining = entrySize <= 0 ? 0x10 : (int)(entrySize - ((Stream.Position - offset) + 0x10));

                    //Debug.WriteLine($"{DictionaryFile.GetHashString(prop.Hash)} {bytesRemaining}");

                    if (bytesRemaining < 0 || bytesRemaining + Stream.Position > chunk.DataOffset + chunk.Size)
                    {
                        group.Children.Add(prop);
                        GeomProps.Add(prop);
                        return;
                    }

                    if (bytesRemaining >= 0x10)
                    {
                        prop.X = Reader.ReadSingleBE();
                        prop.Z = Reader.ReadSingleBE();
                        prop.Y = Reader.ReadSingleBE();
                        prop.W = Reader.ReadSingleBE();

                        bytesRemaining -= 0x10;
                    }

                    if (bytesRemaining > 0)
                    {
                        prop.Data = Reader.ReadBytes(bytesRemaining);
                    }
                }
                else if (prop.Size >= 0x20)
                {
                    prop.X = Reader.ReadSingleBE();
                    prop.Z = Reader.ReadSingleBE();
                    prop.Y = Reader.ReadSingleBE();
                    prop.W = Reader.ReadSingleBE();
                    prop.Data = Reader.ReadBytes(prop.Size - 0x20);
                }
                else if (prop.Size - 0x10 > 0)
                {
                    prop.Data = Reader.ReadBytes(prop.Size - 0x10);
                }

                group.Children.Add(prop);
                GeomProps.Add(prop);

                //Debug.WriteLine($"{DictionaryFile.GetHashString(prop.Hash)} {prop.X} {prop.Y} {prop.Z} {prop.Size}");
            }
        }

        private void LoadProps()
        {
            var chunkData = GetChunkFromType(ChunkType.TYPE_6);

            if (chunkData == null)
                return;

            GeomChunk chunk = chunkData.Value;

            GeomPropGroup root = new GeomPropGroup(0, 0);
            GeomPropGroups.Add(root);
            LoadPropEntry(chunk, chunk.DataOffset, chunk.Size, root);
        }

        private void LoadObjects()
        {
            var chunkData = GetChunkFromType(ChunkType.TYPE_1);

            if (chunkData == null)
                return;

            GeomChunk chunk = chunkData.Value;

            Stream.Seek(chunk.DataOffset, SeekOrigin.Begin);

            var strSize = Marshal.SizeOf(typeof(GeomObject));

            while (Stream.Position < chunk.DataOffset + chunk.Size + strSize)
            {
                GeomObject obj = new GeomObject();

                obj.Field000 = Reader.ReadSingleBE();
                obj.Field004 = Reader.ReadSingleBE();
                obj.Field008 = Reader.ReadSingleBE();
                obj.Type = Reader.ReadUInt32BE();

                if (obj.Type == 0)
                    break;

                obj.Field010 = Reader.ReadSingleBE();
                obj.Field014 = Reader.ReadSingleBE();
                obj.Field018 = Reader.ReadSingleBE();
                obj.Pad = Reader.ReadUInt32BE();
                obj.Field020 = Reader.ReadBytes(0x40);
                obj.Field03C = Reader.ReadUInt32BE();
                obj.Field040 = Reader.ReadUInt32BE();
                obj.IndexOffset = Reader.ReadInt32BE();
                obj.Hash = Reader.ReadUInt32BE();

                GeomObjects.Add(obj);
                GeomObjectsIndex[obj] = new List<GeomIndex>();
            }

            for (int i = 0; i < GeomObjects.Count; i++)
            {
                GeomObject obj = GeomObjects[i];

                Stream.Seek(obj.IndexOffset, SeekOrigin.Begin);

                GeomIndex index = ReadIndex();

                GeomObjectsIndex[obj].Add(index);
            }
        }

        private void WriteProps(Stream stream, BinaryWriterEx writer)
        {
            foreach (var spawn in GeomProps)
            {
                writer.WriteInt32BE(spawn.Size);
                writer.WriteInt32BE(spawn.Flag);
                writer.WriteUInt32BE(spawn.Hash);
                writer.WriteUInt16BE(spawn.Field00C);
                writer.Write(spawn.Field010);
                writer.Write(spawn.Field014);

                if (spawn.Flag > 0)
                    continue;

                if (spawn.X != 0 || spawn.Y != 0 || spawn.Z != 0 || spawn.W != 0) // todo: find the correct logic for this
                {
                    writer.WriteSingleBE(spawn.X);
                    writer.WriteSingleBE(spawn.Z);
                    writer.WriteSingleBE(spawn.Y);
                    writer.WriteSingleBE(spawn.W);
                }

                writer.Write(spawn.Data);
            }
        }

        public void Save(string path)
        {
            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            var writer = new BinaryWriterEx(stream);

            stream.SetLength(0);
            Stream.Seek(0, SeekOrigin.Begin);

            var chunkData = GetChunkFromType(ChunkType.TYPE_6);

            if (chunkData == null)
                return;

            GeomChunk chunk = chunkData.Value;

            var bytes = Reader.ReadBytes(chunk.DataOffset);
            Array.Reverse(bytes);
            writer.WriteBytes(bytes);
            WriteProps(stream, writer);

            Stream.Seek(chunk.DataOffset + chunk.Size, SeekOrigin.Begin);
            int bytesRemaining = (int)(Stream.Length - Stream.Position);
            if (bytesRemaining > 0)
            {
                bytes = Reader.ReadBytes(bytesRemaining);
                Array.Reverse(bytes);
                writer.WriteBytes(bytes);
            }

            stream.Close();
            writer.Close();
        }

        public void WriteDebugFile()
        {
            File.WriteAllText("debug_geom.txt", "");

            for (int i = 0; i < Geom.Header.ChunkCount; i++)
            {
                GeomChunk chunk = Geom.Header.Chunks[i];
                File.AppendAllText("debug_geom.txt", String.Format("Geom 0x{0:X}, Size: 0x{1:X}; Offset: 0x{2:X}" + Environment.NewLine, chunk.Type, chunk.Size, chunk.DataOffset));
            }

            foreach (var group in GeomGroups)
            {
                File.AppendAllText("debug_geom.txt", String.Format("GeomGroup 0x{0:X}, Index Start: 0x{1:X}; Total Size: 0x{2:X}, MaterialOffset: 0x{3:X}" + Environment.NewLine, group.DataOffset, group.IndexOffset, group.TotalSize, group.MaterialOffset));
            }

            foreach (var obj in GeomObjects)
            {
                File.AppendAllText("debug_geom.txt", String.Format("GeomObject 0x{0:X}, Index Start: 0x{1:X}, Type: 0x{2:X}" + Environment.NewLine, obj.Hash, obj.IndexOffset, obj.Type));
            }
        }
    }
}
