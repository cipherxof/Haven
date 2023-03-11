using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom
{
    public class GeoBlock
    {
        public byte Flag;
        public byte GeomCount;
        public ushort Size;
        public ushort Tail;
        public ushort Free;
        public ushort Head;
        public ushort Pad;
        public int VertexOffset;
        public int FaceOffset;
        public int MaterialOffset;
        public ulong Attribute;

        public int Offset;

        public GeoBlock(BinaryReader reader)
        {
            Offset = (int)reader.BaseStream.Position;

            Flag = reader.ReadByte();
            GeomCount = reader.ReadByte();
            Size = reader.ReadUInt16();
            Tail = reader.ReadUInt16();
            Free = reader.ReadUInt16();
            FaceOffset = reader.ReadUInt16();
            reader.ReadBytes(0xe); // ??
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Flag);
            writer.Write(GeomCount);
            writer.Write(Size);
            writer.Write(Tail);
            writer.Write(Free);
            writer.Write((uint)Pad);
            writer.Write(VertexOffset);
            writer.Write(FaceOffset);
            writer.Write(MaterialOffset);
            writer.Write(Attribute);
        }
    }
}
