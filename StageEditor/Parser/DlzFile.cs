using Haven.Parser.Geom.Volume;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser
{
    public class DataContainer // todo: move this
    {
        public int SizeCompressed;
        public int SizeDecompressed;
        public byte[] CompressedData;

        public DataContainer(int sizeCompressed, int sizeDecompressed, byte[] compressedData)
        {
            SizeCompressed = sizeCompressed;
            SizeDecompressed = sizeDecompressed;
            CompressedData = compressedData;
        }
    }

    public class DlzSegIndex
    {
        public ushort SizeCompressed;
        public ushort SizeDecompressed;
        public uint ChunkOffset;

        public DlzSegIndex(BinaryReaderEx reader)
        {
            SizeCompressed = reader.ReadUInt16();
            SizeDecompressed = reader.ReadUInt16();
            ChunkOffset = reader.ReadUInt32();
        }

        public DlzSegIndex(ushort sizeCompressed, ushort sizeDecompressed, uint chunkOffset)
        {
            SizeCompressed = sizeCompressed;
            SizeDecompressed = sizeDecompressed;
            ChunkOffset = chunkOffset;
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(SizeCompressed);
            writer.Write(SizeDecompressed);
            writer.Write(ChunkOffset);
        }
    }

    public class DlzSeg
    {
        public uint Magic;
        public ushort Flag;
        public ushort ChunkCount;
        public uint SizeDecompressed;
        public uint SizeCompressed;

        public List<DlzSegIndex> SegIndex = new List<DlzSegIndex>();
        public List<byte[]> SegData = new List<byte[]>();

        public DlzSeg(BinaryReaderEx reader)
        {
            int fileBeginning = (int)reader.BaseStream.Position;

            Magic = reader.ReadUInt32();
            Flag = reader.ReadUInt16();
            ChunkCount = reader.ReadUInt16();
            SizeDecompressed = reader.ReadUInt32();
            SizeCompressed = BitConverter.ToUInt32(reader.ReadBytes(4));

            for (int i = 0; i < ChunkCount; i++)
            {
                SegIndex.Add(new DlzSegIndex(reader));
            }

            if (reader.BaseStream.Position % 16 != 0)
                reader.BaseStream.Seek(0x08, SeekOrigin.Current);

            for (int i = 0; i < ChunkCount; i++)
            {
                var offset = (SegIndex[i].ChunkOffset - 1 + fileBeginning);
                reader.BaseStream.Seek(offset, SeekOrigin.Begin);

                SegData.Add(reader.ReadBytes(SegIndex[i].SizeCompressed));
            }
        }

        public DlzSeg(uint magic, ushort flag, ushort chunkCount, uint sizeDecompressed, uint sizeCompressed)
        {
            Magic = magic;
            Flag = flag;
            ChunkCount = chunkCount;
            SizeDecompressed = sizeDecompressed;
            SizeCompressed = sizeCompressed;
        }

        public void WriteTo(BinaryWriterEx writer)
        {
            writer.Write(Magic);
            writer.Write(Flag);
            writer.Write(ChunkCount);
            writer.Write(SizeDecompressed);
            writer.Write(BitConverter.GetBytes(SizeCompressed));

            foreach (var index in SegIndex)
            {
                index.WriteTo(writer);
            }

            if (writer.BaseStream.Position % 16 != 0)
            {
                writer.Write(new byte[0x8]);
            }

            foreach (var data in SegData)
            {
                writer.Write(data);

                int padding = (16 - ((int)writer.BaseStream.Position % 16));
                if (padding != 16)
                    writer.Write(new byte[padding]);
            }
        }

        public int GetTotalDecompressedSize()
        {
            int result = 0;
            for (int i = 0; i < SegIndex.Count; i++)
            {
                result += SegIndex[i].SizeDecompressed;
            }
            return result;
        }

        public int CalculateSize()
        {
            int size = 0x10;
            int chunks = ChunkCount;
            size += (chunks * 0x08);


            //byte alignment
            if (size % 16 != 0)
            {
                size += 0x08;
            }

            //chunk sizes
            for (int i = 0; i < chunks; i++)
            {
                SegIndex[i].ChunkOffset = ((uint)(size + 1));

                size += SegIndex[i].SizeCompressed;
                int skip = (16 - (size % 16));
                if (skip != 16)
                {
                    size += skip;
                }

            }

            return size;
        }

    }

    public class DlzFile
    {
        public List<DlzSeg> Segs = new List<DlzSeg>();

        public DlzFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReaderEx(stream))
                {
                    DlzSeg seg;

                    while(stream.Length - stream.Position > 0x20000)
                    {
                        var segStart = stream.Position;
                        seg = new DlzSeg(reader);

                        int pos = (int)(stream.Position - segStart);
                        int padding = 0x20000 - pos;

                        stream.Seek(padding, SeekOrigin.Current);
                        Segs.Add(seg);
                    }
                    seg = new DlzSeg(reader);
                    Segs.Add(seg);
                }
            }
        }

        public DlzFile(List<DataContainer> containers)
        {
            DlzSeg seg = new DlzSeg(0x73656773, 4, 0, 0, 0);

            int y = 0;

            for (int i = 0; i < containers.Count; i++)
            {
                if (seg.SizeCompressed + containers[i].SizeCompressed > 0x20000)
                {
                    Segs.Add(seg);

                    y = 0;
                    seg = new DlzSeg(0x73656773, 4, 0, 0, 0);
                }

                DlzSegIndex index = new DlzSegIndex(0, 0, 0);

                index.SizeCompressed = (ushort)containers[i].SizeCompressed;
                index.SizeDecompressed = (ushort)containers[i].SizeDecompressed;
                index.ChunkOffset = 0;

                seg.SegData.Add(containers[i].CompressedData);
                seg.ChunkCount = (ushort)(y + 1);

                seg.SegIndex.Add(index);

                seg.SizeDecompressed += (uint)containers[i].SizeDecompressed;
                seg.SizeCompressed = (uint)seg.CalculateSize();

                y++;
            }

            Segs.Add(seg);
        }

        public void Save(string path, bool? bigEndian = null)
        {
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var writer = new BinaryWriterEx(stream, bigEndian))
                {
                    long padding = 0;

                    stream.SetLength(0);

                    for (int i = 0; i < Segs.Count; i++)
                    {
                        Segs[i].WriteTo(writer);

                        if (i != Segs.Count - 1)
                        {
                            padding = (131072 - (stream.Position % 131072));
                            if (padding != 131072)
                            {
                                writer.Write(new byte[padding]);
                            }
                        }
                    }

                    padding = (2048 - (stream.Position % 2048));
                    if (padding != 2048)
                    {
                        writer.Write(new byte[padding]);
                    }
                }
            }
        }

        public void Unpack(string path, bool? bigEndian = null)
        {
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var writer = new BinaryWriterEx(stream, bigEndian))
                {
                    stream.SetLength(0);

                    foreach (var seg in Segs)
                    {
                        for (int i = 0; i < seg.ChunkCount; i++)
                        {
                            var index = seg.SegIndex[i];

                            writer.Write(Utils.InflateBuffer2(seg.SegData[i], index.SizeDecompressed));
                        }
                    }
                }
            }
        }
    }
}
