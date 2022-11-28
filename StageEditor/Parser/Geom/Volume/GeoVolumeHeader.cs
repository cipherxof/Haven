using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom.Volume
{
    public class GeoVolumeHeader
    {
        public int Magic;
        public uint Size;
        public int Flag;
        public int VolumeCount;
        public List<GeoVolume> Volumes = new List<GeoVolume>();

        public GeoVolumeHeader(BinaryReaderEx reader) 
        {
            Magic = reader.ReadInt32();
            Size = reader.ReadUInt32();
            Flag = reader.ReadInt32();
            VolumeCount = reader.ReadInt32();

            for (int i = 0; i < VolumeCount; i++)
            {
                Volumes.Add(new GeoVolume(reader));
            }
        }

        public void WriteTo(BinaryWriterEx writer)
        {
            writer.Write(Magic);
            writer.Write(Size);
            writer.Write(Flag);
            writer.Write(Volumes.Count);

            foreach (var volume in Volumes)
            {
                volume.WriteTo(writer);
            }
        }
    }
}
