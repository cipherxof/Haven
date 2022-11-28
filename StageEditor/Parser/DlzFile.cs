using Haven.Parser.Geom.Volume;
using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser
{
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

        public int GetTotalDecompressedSize()
        {
            int result = 0;
            for (int i = 0; i < SegIndex.Count; i++)
            {
                result += SegIndex[i].SizeDecompressed;
            }
            return result;
        }

    }

    public class DlzFile
    {
        public List<DlzSeg> Segs = new List<DlzSeg>();

        public DlzFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReaderEx(stream, true))
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

        public void Unpack(string path)
        {
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var writer = new BinaryWriterEx(stream, true))
                {
                    stream.SetLength(0);

                    foreach (var seg in Segs)
                    {
                        for (int i = 0; i < seg.ChunkCount; i++)
                        {
                            var index = seg.SegIndex[i];

                            writer.Write(Utils.InflateBuffer(seg.SegData[i], index.SizeDecompressed));
                        }
                    }
                }
            }
        }
    }
}
