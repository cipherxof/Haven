using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom.Volume
{
    public class GeoVolumeVectors
    {
        public Vector4 Pos;
        public Vector4 Norm;

        public GeoVolumeVectors(BinaryReaderEx reader) 
        {
            Pos = reader.ReadVector4();
            Norm = reader.ReadVector4();
        }

        public void WriteTo(BinaryWriterEx writer)
        {
            writer.Write(Pos);
            writer.Write(Norm);
        }
    }
}
