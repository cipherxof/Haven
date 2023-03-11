using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom.Prim
{
    public class GeoPrimPoly
    {
        public byte[] Data;
        public ushort Attribute;

        public float[] VertexData = new float[3];
        public byte[] FaceData = new byte[4];

        public GeoPrimPoly(BinaryReader reader)
        {
            VertexData[0] = reader.ReadSingle();
            VertexData[1] = reader.ReadSingle();
            VertexData[2] = reader.ReadSingle();
            FaceData[0] = reader.ReadByte();
            FaceData[1] = reader.ReadByte();
            FaceData[2] = reader.ReadByte();
            FaceData[3] = reader.ReadByte();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Data);
            writer.Write(Attribute);
        }
    }
}
