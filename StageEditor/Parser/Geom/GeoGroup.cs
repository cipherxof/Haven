using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom
{
    public class GeoGroup
    {
        public float BaseX;
        public float BaseY;
        public float BaseZ;
        public int MaterialOffset;
        public float DivX;
        public float DivY;
        public float DivZ;
        public float DivW;
        public int MaxX;
        public int MaxY;
        public int MaxZ;
        public int HeadSize;
        public int Flag;
        public short TypesCount;
        public short RadixSize;
        public int DataOffset;
        public int BlockOffset;

        public GeoGroup(BinaryReader reader)
        {
            BaseX = reader.ReadSingle();
            BaseY = reader.ReadSingle();
            BaseZ = reader.ReadSingle();
            reader.ReadInt32();
            DivX = reader.ReadUInt16();
            DivY = reader.ReadUInt16();
            DivZ = reader.ReadUInt16();
            reader.ReadUInt16();
            MaxX = reader.ReadUInt16();
            MaxY = reader.ReadUInt16();
            MaxZ = reader.ReadUInt16();
            reader.ReadUInt16();
            Flag = reader.ReadInt32();
            TypesCount = reader.ReadInt16();
            RadixSize = reader.ReadInt16();
            DataOffset = reader.ReadInt32();
            BlockOffset = reader.ReadInt32();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(BaseX);
            writer.Write(BaseY);
            writer.Write(BaseZ);
            writer.Write(MaterialOffset);
            writer.Write(DivX);
            writer.Write(DivY);
            writer.Write(DivZ);
            writer.Write(DivW);
            writer.Write(MaxX);
            writer.Write(MaxY);
            writer.Write(MaxZ);
            writer.Write(HeadSize);
            writer.Write(Flag);
            writer.Write(TypesCount);
            writer.Write(RadixSize);
            writer.Write(DataOffset);
            writer.Write(BlockOffset);
        }
    }
}
