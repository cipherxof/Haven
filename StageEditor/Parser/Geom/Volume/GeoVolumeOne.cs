using OpenTK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom.Volume
{
    public class GeoVolumeOne
    {
        public Vector4 BoundMin;
        public Vector4 BoundMax;
        public int NextVolumeOneOffset;
        public int Flag;
        public short AreaID;
        public short VectorCount;
        public byte Field018;
        public byte Field019;
        public short Field020;
        public List<GeoVolumeVectors> Vectors = new List<GeoVolumeVectors>();

        public GeoVolumeOne(BinaryReaderEx reader) 
        {
            BoundMin = reader.ReadVector4();
            BoundMax = reader.ReadVector4();
            NextVolumeOneOffset = reader.ReadInt32();
            Flag = reader.ReadInt32();
            AreaID = reader.ReadInt16();
            VectorCount = reader.ReadInt16();
            Field018 = reader.ReadByte();
            Field019 = reader.ReadByte();
            Field020 = reader.ReadInt16();

            for (int i = 0; i < VectorCount; i++)
            {
                Vectors.Add(new GeoVolumeVectors(reader));
            }
        }

        public void WriteTo(BinaryWriterEx writer)
        {
            writer.Write(BoundMin);
            writer.Write(BoundMax);
            writer.Write(NextVolumeOneOffset);
            writer.Write(Flag);
            writer.Write(AreaID);
            writer.Write(VectorCount);
            writer.Write(Field018);
            writer.Write(Field019);
            writer.Write(Field020);

            foreach (var vectors in Vectors)
            {
                vectors.WriteTo(writer);
            }
        }
    }
}
