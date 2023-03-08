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
    public class TxnImage
    {
        public ushort Width;
        public ushort Height;
        public ushort FourCC;
        public ushort Flag;
        public uint Offset;
        public uint OffsetMips;

        public TxnImage(ushort width, ushort height, ushort fourCC, ushort flag, uint offset, uint mipmapOffset)
        {
            Width = width;
            Height = height;
            FourCC = fourCC;
            Flag = flag;
            Offset = offset;
            OffsetMips = mipmapOffset;
        }

        public TxnImage(BinaryReader reader)
        {
            Width = reader.ReadUInt16();
            Height = reader.ReadUInt16();
            FourCC = reader.ReadUInt16();
            Flag = reader.ReadUInt16();
            Offset = reader.ReadUInt32();
            OffsetMips = reader.ReadUInt32();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(FourCC);
            writer.Write(Flag);
            writer.Write(Offset);
            writer.Write(OffsetMips);
        }
    }

    public class TxnInfo
    {
        public uint Flag;
        public uint TexId;
        public uint TriId;
        public ushort Width;
        public ushort Height;
        public ushort OffsetX;
        public ushort OffsetY;
        public uint TxnImageOffset;
        public uint NullBytes2;
        public float ScaleU;
        public float ScaleV;
        public float OffsetU;
        public float OffsetV;
        public float OffsetLOD;

        public TxnInfo(uint materialId, uint objectId, ushort width, ushort height, ushort positionX, ushort positionY, uint offset, float weightX, float weightY, float weightX2, float weightY2)
        {
            Flag = 6;
            TexId = materialId;
            TriId = objectId;
            Width = width;
            Height = height;
            OffsetX = positionX;
            OffsetY = positionY;
            TxnImageOffset = offset;
            NullBytes2 = 0;
            ScaleU = weightX;
            ScaleV = weightY;
            OffsetU = weightX2;
            OffsetV = weightY2;
            OffsetLOD = 0;
        }

        public TxnInfo(BinaryReader reader) 
        {
            Flag = reader.ReadUInt32();
            TexId = reader.ReadUInt32();
            TriId = reader.ReadUInt32();
            Width = reader.ReadUInt16();
            Height = reader.ReadUInt16();
            OffsetX = reader.ReadUInt16();
            OffsetY = reader.ReadUInt16();
            TxnImageOffset = reader.ReadUInt32();
            NullBytes2 = reader.ReadUInt32();
            ScaleU = reader.ReadSingle();
            ScaleV = reader.ReadSingle();
            OffsetU = reader.ReadSingle();
            OffsetV = reader.ReadSingle();
            OffsetLOD = reader.ReadUInt32();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Flag);
            writer.Write(TexId);
            writer.Write(TriId);
            writer.Write(Width);
            writer.Write(Height);   
            writer.Write(OffsetX);
            writer.Write(OffsetY);
            writer.Write(TxnImageOffset);
            writer.Write(NullBytes2);
            writer.Write(ScaleU);
            writer.Write(ScaleV);
            writer.Write(OffsetU);
            writer.Write(OffsetV);
            writer.Write(OffsetLOD);
        }
    }

    public class TxnHeader
    {
        public uint NullBytes = 0;
        public uint Flags = 0;
        public uint TextureCount = 0;
        public uint IndexOffset = 0;
        public uint TextureCount2 = 0;
        public uint IndexOffset2 = 0;
        public uint NullBytes2 = 0;
        public uint NullBytes3 = 0;

        public TxnHeader()
        {
        }
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
        public  List<TxnImage> Images = new List<TxnImage>();
        public  List<TxnInfo> ImageInfo = new List<TxnInfo>();

        public readonly Dictionary<TxnInfo, int> IndexLookup = new Dictionary<TxnInfo, int>();

        public TxnFile()
        {
            Path = "";
            Header = new TxnHeader();
        }

        public TxnFile(string path)
        {
            Path = path;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReaderEx(stream))
                {
                    Header = new TxnHeader(reader);

                    if (Header.NullBytes == 0)
                    {
                        List<uint> offsets = new List<uint>();

                        stream.Seek(Header.IndexOffset, SeekOrigin.Begin);

                        for (int i = 0; i < Header.TextureCount; i++)
                        {
                            offsets.Add((uint)stream.Position);
                            Images.Add(new TxnImage(reader));
                        }

                        stream.Seek(Header.IndexOffset2, SeekOrigin.Begin);

                        for (int i = 0; i < Header.TextureCount2; i++)
                        {
                            var index2 = new TxnInfo(reader);
                            var index1 = offsets.FindIndex(offset => offset == index2.TxnImageOffset);
                            IndexLookup[index2] = index1;
                            ImageInfo.Add(index2);
                        }
                    }
                }
            }
        }

        public int GetIndex(TxnInfo index2)
        {
            int result;
            IndexLookup.TryGetValue(index2, out result);
            return result;
        }

        public List<TxnInfo> GetIndex2List(int index1)
        {
            List<TxnInfo> result = new List<TxnInfo>();

            if (Images.Count <= index1)
            {
                return result;
            }

            int offset = 0x20 + (index1 * 0x10);

            for (int i = 0; i < ImageInfo.Count; i++)
            {
                if (offset == ImageInfo[i].TxnImageOffset)
                {
                    result.Add(ImageInfo[i]);
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

                    Header.TextureCount = (uint)Images.Count;
                    Header.TextureCount2 = (uint)ImageInfo.Count;
                    Header.WriteTo(writer);

                    uint[] offsets = new uint[Images.Count];

                    Header.IndexOffset = (uint)stream.Position;
                    for (int i = 0; i < Images.Count; i++)
                    {
                        offsets[i] = (uint)stream.Position;
                        Images[i].WriteTo(writer);
                    }

                    Header.IndexOffset2 = (uint)stream.Position;
                    for (int i = 0; i < ImageInfo.Count; i++)
                    {
                        var index1 = GetIndex(ImageInfo[i]);
                        ImageInfo[i].TxnImageOffset = offsets[i];
                        ImageInfo[i].WriteTo(writer);
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

            var index = Images[indexNumber];
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
