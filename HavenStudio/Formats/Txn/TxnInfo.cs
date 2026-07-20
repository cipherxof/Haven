using HavenStudio.Extensions;

namespace HavenStudio.Formats.Txn;

    public class TxnInfo
    {
        public uint Flag;
        public uint TexId;
        public uint TriId;
        public ushort Width;
        public ushort Height;
        public ushort OffsetX;
        public ushort OffsetY;
        public uint TxnImageOffset;
        public uint NullBytes2;
        public float ScaleU;
        public float ScaleV;
        public float OffsetU;
        public float OffsetV;
        public float OffsetLOD;

        public TxnInfo(uint materialId, uint objectId, ushort width, ushort height, ushort positionX, ushort positionY, uint offset, float weightX, float weightY, float weightX2, float weightY2)
        {
            Flag = 6;
            TexId = materialId;
            TriId = objectId;
            Width = width;
            Height = height;
            OffsetX = positionX;
            OffsetY = positionY;
            TxnImageOffset = offset;
            NullBytes2 = 0;
            ScaleU = weightX;
            ScaleV = weightY;
            OffsetU = weightX2;
            OffsetV = weightY2;
            OffsetLOD = 0;
        }

        public TxnInfo(EndianBinaryReader reader) 
        {
            Flag = reader.ReadUInt32();
            TexId = reader.ReadUInt32();
            TriId = reader.ReadUInt32();
            Width = reader.ReadUInt16();
            Height = reader.ReadUInt16();
            OffsetX = reader.ReadUInt16();
            OffsetY = reader.ReadUInt16();
            TxnImageOffset = reader.ReadUInt32();
            NullBytes2 = reader.ReadUInt32();
            ScaleU = reader.ReadSingle();
            ScaleV = reader.ReadSingle();
            OffsetU = reader.ReadSingle();
            OffsetV = reader.ReadSingle();
            OffsetLOD = reader.ReadUInt32();
        }

        public void WriteTo(EndianBinaryWriter writer)
        {
            writer.Write(Flag);
            writer.Write(TexId);
            writer.Write(TriId);
            writer.Write(Width);
            writer.Write(Height);   
            writer.Write(OffsetX);
            writer.Write(OffsetY);
            writer.Write(TxnImageOffset);
            writer.Write(NullBytes2);
            writer.Write(ScaleU);
            writer.Write(ScaleV);
            writer.Write(OffsetU);
            writer.Write(OffsetV);
            writer.Write(OffsetLOD);
        }
    }