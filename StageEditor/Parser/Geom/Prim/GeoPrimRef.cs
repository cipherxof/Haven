using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom.Prim
{
    public class GeoPrimRef
    {
        public float SizeX;
        public float SizeY;
        public float SizeZ;
        public ushort Field00C;
        public ushort BlockCount;
        public float PosX;
        public float PosY;
        public float PosZ;
        public uint RootID;
        public OpenTK.Matrix4 Matrix;
        public ulong Attribute;
        public int BlockOffset;
        public uint Hash;

        public GeoPrimRef(BinaryReaderEx reader)
        {
            SizeX = reader.ReadSingle();
            SizeY = reader.ReadSingle();
            SizeZ = reader.ReadSingle();
            Field00C = reader.ReadUInt16();
            BlockCount = reader.ReadUInt16();
            PosX = reader.ReadSingle();
            PosY = reader.ReadSingle();
            PosZ = reader.ReadSingle();
            RootID = reader.ReadUInt32();
            Matrix = reader.ReadMatrix4();
            Attribute = reader.ReadUInt64();
            BlockOffset = reader.ReadInt32();
            Hash = reader.ReadUInt32();
        }

        public void WriteTo(BinaryWriterEx writer)
        {
            writer.Write(SizeX);
            writer.Write(SizeY);
            writer.Write(SizeZ);
            writer.Write(Field00C);
            writer.Write(BlockCount);
            writer.Write(PosX);
            writer.Write(PosY);
            writer.Write(PosZ);
            writer.Write(RootID);
            writer.Write(Matrix);
            writer.Write(Attribute);
            writer.Write(BlockOffset);
            writer.Write(Hash);
        }
    }
}
