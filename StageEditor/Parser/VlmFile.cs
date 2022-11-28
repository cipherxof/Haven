using Haven.Parser.Geom.Volume;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser
{

    public class VlmFile
    {
        public GeoVolumeHeader Header;
        public List<GeoVolumeOne> VolumeOnes = new List<GeoVolumeOne>();

        public VlmFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReaderEx(stream, true))
                {
                    Header = new GeoVolumeHeader(reader);
                }
            }
        }

        public void Save(string path)
        {
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var writer = new BinaryWriterEx(stream, true))
                {
                    stream.SetLength(0);
                    Header.WriteTo(writer);

                    foreach (var volume in Header.Volumes)
                    {
                        foreach (var volumeOne in volume.VolumeOnes)
                        {
                            volumeOne.WriteTo(writer);
                        }
                    }
                }
            }
        }
    }
}
