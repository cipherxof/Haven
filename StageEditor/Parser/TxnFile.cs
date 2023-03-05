using Haven.Parser.Geom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser
{
    public class TxnIndex
    {
        public ushort Width;
        public ushort Height;
        public ushort FourCC;
        public ushort Flag;
        public uint Offset;
        public uint MipMapOffset;

        public TxnIndex(ushort width, ushort height, ushort fourCC, ushort flag, uint offset, uint mipmapOffset)
        {
            Width = width;
            Height = height;
            FourCC = fourCC;
            Flag = flag;
            Offset = offset;
            MipMapOffset = mipmapOffset;
        }

        public TxnIndex(BinaryReader reader)
        {
            Width = reader.ReadUInt16();
            Height = reader.ReadUInt16();
            FourCC = reader.ReadUInt16();
            Flag = reader.ReadUInt16();
            Offset = reader.ReadUInt32();
            MipMapOffset = reader.ReadUInt32();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(FourCC);
            writer.Write(Flag);
            writer.Write(Offset);
            writer.Write(MipMapOffset);
        }
    }

    public class TxnIndex2
    {
        public uint Unknown;
        public uint MaterialId;
        public uint ObjectId;
        public ushort Width;
        public ushort Height;
        public ushort PositionX;
        public ushort PositionY;
        public uint Offset;
        public uint NullBytes2;
        public float WeightX;
        public float WeightY;
        public float WeightX2;
        public float WeightY2;
        public uint NullBytes3;

        public TxnIndex2(uint materialId, uint objectId, ushort width, ushort height, ushort positionX, ushort positionY, uint offset, float weightX, float weightY, float weightX2, float weightY2)
        {
            Unknown = 0;
            MaterialId = materialId;
            ObjectId = objectId;
            Width = width;
            Height = height;
            PositionX = positionX;
            PositionY = positionY;
            Offset = offset;
            NullBytes2 = 0;
            WeightX = weightX;
            WeightY = weightY;
            WeightX2 = weightX2;
            WeightY2 = weightY2;
            NullBytes3 = 0;
        }

        public TxnIndex2(BinaryReader reader) 
        {
            Unknown = reader.ReadUInt32();
            MaterialId = reader.ReadUInt32();
            ObjectId = reader.ReadUInt32();
            Width = reader.ReadUInt16();
            Height = reader.ReadUInt16();
            PositionX = reader.ReadUInt16();
            PositionY = reader.ReadUInt16();
            Offset = reader.ReadUInt32();
            NullBytes2 = reader.ReadUInt32();
            WeightX = reader.ReadSingle();
            WeightY = reader.ReadSingle();
            WeightX2 = reader.ReadSingle();
            WeightY2 = reader.ReadSingle();
            NullBytes3 = reader.ReadUInt32();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Unknown);
            writer.Write(MaterialId);
            writer.Write(ObjectId);
            writer.Write(Width);
            writer.Write(Height);   
            writer.Write(PositionX);
            writer.Write(PositionY);
            writer.Write(Offset);
            writer.Write(NullBytes2);
            writer.Write(WeightX);
            writer.Write(WeightY);
            writer.Write(WeightX2);
            writer.Write(WeightY2);
            writer.Write(NullBytes3);
        }
    }

    public class TxnHeader
    {
        public uint NullBytes;
        public uint Flags;
        public uint TextureCount;
        public uint IndexOffset;
        public uint TextureCount2;
        public uint IndexOffset2;
        public uint NullBytes2;
        public uint NullBytes3;

        public TxnHeader(BinaryReader reader)
        {
            NullBytes = reader.ReadUInt32();
            Flags = reader.ReadUInt32();
            TextureCount = reader.ReadUInt32();
            IndexOffset = reader.ReadUInt32();
            TextureCount2 = reader.ReadUInt32();
            IndexOffset2 = reader.ReadUInt32();
            NullBytes2 = reader.ReadUInt32();
            NullBytes3 = reader.ReadUInt32();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(NullBytes);
            writer.Write(Flags);
            writer.Write(TextureCount);
            writer.Write(IndexOffset);
            writer.Write(TextureCount2);
            writer.Write(IndexOffset2);
            writer.Write(NullBytes2);
            writer.Write(NullBytes3);
        }
    }

    public class TxnFile
    {
        public readonly TxnHeader Header;
        public readonly string Path;
        public  List<TxnIndex> Indicies = new List<TxnIndex>();
        public  List<TxnIndex2> Indicies2 = new List<TxnIndex2>();

        public readonly Dictionary<TxnIndex2, int> IndexLookup = new Dictionary<TxnIndex2, int>();

        public TxnFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReaderEx(stream))
                {
                    Header = new TxnHeader(reader);
                    Path = path;

                    if (Header.TextureCount != Header.TextureCount2)
                    {
                        Debug.WriteLine($"TXN \"{Path}\" mismatching texture counts!");
                    }

                    if (Header.NullBytes == 0)
                    {
                        List<uint> offsets = new List<uint>();

                        stream.Seek(Header.IndexOffset, SeekOrigin.Begin);

                        for (int i = 0; i < Header.TextureCount; i++)
                        {
                            offsets.Add((uint)stream.Position);
                            Indicies.Add(new TxnIndex(reader));
                        }

                        stream.Seek(Header.IndexOffset2, SeekOrigin.Begin);

                        for (int i = 0; i < Header.TextureCount2; i++)
                        {
                            var index2 = new TxnIndex2(reader);
                            var index1 = offsets.FindIndex(offset => offset == index2.Offset);
                            IndexLookup[index2] = index1;
                            Indicies2.Add(index2);
                        }
                    }
                }
            }
        }

        public int GetIndex(TxnIndex2 index2)
        {
            int result;
            IndexLookup.TryGetValue(index2, out result);
            return result;
        }

        public List<TxnIndex2> GetIndex2List(int index1)
        {
            List<TxnIndex2> result = new List<TxnIndex2>();

            if (Indicies.Count <= index1)
            {
                return result;
            }

            int offset = 0x20 + (index1 * 0x10);

            for (int i = 0; i < Indicies2.Count; i++)
            {
                if (offset == Indicies2[i].Offset)
                {
                    result.Add(Indicies2[i]);
                }
            }

            return result;
        }

        public void Save(string path, bool? bigEndian = null)
        {
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var writer = new BinaryWriterEx(stream, bigEndian))
                {
                    stream.SetLength(0);

                    Header.TextureCount = (uint)Indicies.Count;
                    Header.TextureCount2 = (uint)Indicies2.Count;
                    Header.WriteTo(writer);

                    uint[] offsets = new uint[Indicies.Count];

                    Header.IndexOffset = (uint)stream.Position;
                    for (int i = 0; i < Indicies.Count; i++)
                    {
                        offsets[i] = (uint)stream.Position;
                        Indicies[i].WriteTo(writer);
                    }

                    Header.IndexOffset2 = (uint)stream.Position;
                    for (int i = 0; i < Indicies2.Count; i++)
                    {
                        var index1 = GetIndex(Indicies2[i]);
                        Indicies2[i].Offset = offsets[i];
                        Indicies2[i].WriteTo(writer);
                    }

                    writer.Align(0x80);

                    stream.Seek(0, SeekOrigin.Begin);
                    Header.WriteTo(writer);
                }
            }
        }

        public void CreateDdsFromIndex(string filename, int indexNumber, DldTexture? texture, DldTexture? mips)
        {
            if (texture == null)
            {
                return;
            }

            var index = Indicies[indexNumber];
            var data = texture.Data;

            if (mips != null)
            {
                data = data.Concat(mips.Data).ToArray();
            }

            int mipMapCount = mips == null ? 0 : (int)Math.Log2(Math.Max(index.Height, index.Width));

            DdsFile.Create(filename, index.Height, index.Width, index.FourCC == 11 ? "DXT5" : "DXT1", mipMapCount, data);
        }

    }
}
