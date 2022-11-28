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

                    int volumeStart = 0x10;

                    foreach (var volume in Header.Volumes)
                    {
                        int baseOffset = volumeStart + 0x28;

                        volume.VolumeOneOffset = (int)stream.Position - baseOffset;

                        foreach (var volumeOne in volume.VolumeOnes)
                        {
                            volumeOne.WriteTo(writer);
                        }

                        volumeStart += 0x30;
                    }

                    stream.Seek(0, SeekOrigin.Begin);
                    Header.WriteTo(writer);
                }
            }
        }

        public void Merge(VlmFile vlm)
        {
            for (int i = 0; i < vlm.Header.Volumes.Count; i++)
            {
                vlm.Header.Volumes[i].AreaID += (Header.VolumeCount);

                for (int n = 0; n < vlm.Header.Volumes[i].VolumeOnes.Count; n++)
                {
                    vlm.Header.Volumes[i].VolumeOnes[n].AreaID += (short)(Header.VolumeCount);
                }
            }

            Header.VolumeCount += vlm.Header.VolumeCount;
            Header.Size += (vlm.Header.Size - 0x10);
            Header.Volumes.InsertRange(0, vlm.Header.Volumes);
        }
    }
}
