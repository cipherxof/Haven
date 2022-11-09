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

        public GeoPrimPoly(BinaryReader reader)
        {
            Data = reader.ReadBytes(6);
            Attribute = reader.ReadUInt16();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Data);
            writer.Write(Attribute);
        }
    }
}
