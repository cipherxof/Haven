using OpenTK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom.Volume
{
    public class GeoVolume
    {
        public Vector4 BoundMin;
        public Vector4 BoundMax;
        public int Flag;
        public int AreaID;
        public int VolumeOneOffset;
        public int Field014;

        public List<GeoVolumeOne> VolumeOnes = new List<GeoVolumeOne>();

        public GeoVolume(BinaryReaderEx reader) 
        {
            BoundMin = reader.ReadVector4();
            BoundMax = reader.ReadVector4();
            Flag = reader.ReadInt32();
            AreaID = reader.ReadInt32();
            VolumeOneOffset = reader.ReadInt32();
            Field014 = reader.ReadInt32();

            var pos = reader.BaseStream.Position;

            reader.BaseStream.Seek(VolumeOneOffset - 0x8, SeekOrigin.Current);

            while (true)
            {
                var volumeOne = new GeoVolumeOne(reader);
                VolumeOnes.Add(volumeOne);

                if (volumeOne.NextVolumeOneOffset == 0)
                    break;
            }

            reader.BaseStream.Seek(pos, SeekOrigin.Begin);
        }   
        
        public void WriteTo(BinaryWriterEx writer)
        {
            writer.Write(BoundMin);
            writer.Write(BoundMax);
            writer.Write(Flag);
            writer.Write(AreaID);
            writer.Write(VolumeOneOffset);
            writer.Write(Field014);
        }
    }
}
