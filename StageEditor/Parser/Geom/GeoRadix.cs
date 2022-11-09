using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom
{
    public class GeoRadix
    {
        public short Offset;
        public byte[] Types;

        public GeoRadix(BinaryReader reader, GeoGroup group)
        {
            Offset = reader.ReadInt16();
            Types = reader.ReadBytes(group.TypesCount);
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Offset);
            writer.Write(Types);
        }
    }
}
