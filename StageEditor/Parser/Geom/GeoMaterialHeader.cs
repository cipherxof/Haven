using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom
{
    public class GeoMaterialHeader
    {
        public byte MaterialOffset;
        public byte MaterialSize;
        public byte ColorOffset;
        public byte ColorSize;
        public List<uint> Data;

        public GeoMaterialHeader(byte materialOffset, byte materialSize, byte colorOffset, byte colorSize, List<uint> data)
        {
            MaterialOffset = materialOffset;
            MaterialSize = materialSize;
            ColorOffset = colorOffset;
            ColorSize = colorSize;
            Data = data;
        }

        public GeoMaterialHeader(BinaryReader reader)
        {
            var matStart = reader.BaseStream.Position;
            var data = reader.ReadUInt32();
            var bytes = BitConverter.GetBytes(data);

            MaterialOffset = bytes[3];
            MaterialSize = bytes[2];
            ColorOffset = bytes[1];
            ColorSize = bytes[0];
            Data = new List<uint>();

            if (MaterialSize > 0)
            {
                reader.BaseStream.Seek(matStart + ((MaterialOffset + 1) * 4), SeekOrigin.Begin);

                while (true)
                {
                    var mat = reader.ReadUInt32();
                    Data.Add(mat);

                    if (mat == 0)
                        break;
                }
            }

            if (ColorSize > 0)
            {
                reader.BaseStream.Seek(matStart + ((ColorOffset + 1) * 4), SeekOrigin.Begin);

                while (true)
                {
                    var color = reader.ReadUInt32();
                    Data.Add(color);

                    if (color == 0)
                        break;
                }
            }

            int totalBytes = (Data.Count + 1) * 4;
            int len = (totalBytes + 0x10 - 1) / 0x10 * 0x10;
            int pad = (len - totalBytes) / 4;

            for (int i = 0; i < pad; i++)
                Data.Add(0);

            //for (int i = 0; i < 4; i++)
            //    Data.Add(0);
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(MaterialOffset);
            writer.Write(MaterialSize);
            writer.Write(ColorOffset);
            writer.Write(ColorSize);

            foreach (var mat in Data)
            {
                writer.Write(mat);
            }
        }
    }
}
