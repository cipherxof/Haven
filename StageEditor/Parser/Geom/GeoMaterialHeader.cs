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
        public List<uint> Materials = new List<uint>();
        public List<uint> Colors = new List<uint>();

        public GeoMaterialHeader(byte materialOffset, byte materialSize, byte colorOffset, byte colorSize, List<uint> data)
        {
            MaterialOffset = materialOffset;
            MaterialSize = materialSize;
            ColorOffset = colorOffset;
            ColorSize = colorSize;
        }

        public GeoMaterialHeader(BinaryReaderEx reader)
        {
            var matStart = reader.BaseStream.Position;
            var data = reader.ReadUInt32();
            var bytes = BitConverter.GetBytes(data);

            if (!reader.BigEndian)
            {
                Array.Reverse(bytes);
            }

            MaterialOffset = bytes[3];
            MaterialSize = bytes[2];
            ColorOffset = bytes[1];
            ColorSize = bytes[0];

            if (MaterialSize > 0)
            {
                reader.BaseStream.Seek(matStart + ((MaterialOffset + 1) * 4), SeekOrigin.Begin);

                while (true)
                {
                    var mat = reader.ReadUInt32();
                    Materials.Add(mat);

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
                    Colors.Add(color);

                    if (color == 0)
                        break;
                }
            }
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(MaterialOffset);
            writer.Write(MaterialSize);
            writer.Write(ColorOffset);
            writer.Write(ColorSize);

            foreach (var mat in Materials)
            {
                writer.Write(mat);
            }

            foreach (var color in Colors)
            {
                writer.Write(color);
            }

            int padding = (16 - ((int)writer.BaseStream.Position % 16));
            if (padding != 16)
                writer.Write(new byte[padding]);
        }
    }
}
