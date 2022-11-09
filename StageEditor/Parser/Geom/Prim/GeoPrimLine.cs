using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser.Geom.Prim
{
    public class GeoPrimLine
    {
        public float SizeX;
        public float SizeY;
        public float SizeZ;
        public short Pad;
        public ushort Attribute;
        public float WorldX;
        public float WorldY;
        public float WorldZ;
        public uint RootID;
        public float ToWorldX;
        public float ToWorldY;
        public float ToWorldZ;
        public uint ToRootID;

        public GeoPrimLine(BinaryReader reader)
        {
            SizeX = reader.ReadSingle();
            SizeY = reader.ReadSingle();
            SizeZ = reader.ReadSingle();
            Pad = reader.ReadInt16();
            Attribute = reader.ReadUInt16();
            WorldX = reader.ReadSingle();
            WorldY = reader.ReadSingle();
            WorldZ = reader.ReadSingle();
            RootID = reader.ReadUInt32();
            ToWorldX = reader.ReadSingle();
            ToWorldY = reader.ReadSingle();
            ToWorldZ = reader.ReadSingle();
            ToRootID = reader.ReadUInt32();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(SizeX);
            writer.Write(SizeY);
            writer.Write(SizeZ);
            writer.Write(Pad);
            writer.Write(Attribute);
            writer.Write(WorldX);
            writer.Write(WorldY);
            writer.Write(WorldZ);
            writer.Write(RootID);
            writer.Write(ToWorldX);
            writer.Write(ToWorldY);
            writer.Write(ToWorldZ);
            writer.Write(ToRootID);
        }
    }
}
