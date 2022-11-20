using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class BinaryWriterEx : BinaryWriter
{
    public bool BigEndian;

    public BinaryWriterEx(Stream stream, bool bigEndian = false) : base(stream) 
    {
        BigEndian = bigEndian;
    }

    public override void Write(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        base.Write(BitConverter.ToInt32(bytes, 0));
    }

    public override void Write(short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        base.Write(BitConverter.ToInt16(bytes, 0));
    }

    public override void Write(ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        base.Write(BitConverter.ToUInt16(bytes, 0));
    }

    public override void Write(uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        base.Write(BitConverter.ToUInt32(bytes, 0));
    }

    public override void Write(ulong value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        base.Write(BitConverter.ToUInt64(bytes, 0));
    }

    public override void Write(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BigEndian) Array.Reverse(bytes);
        base.Write(BitConverter.ToSingle(bytes, 0));
    }

    public void Align(int size)
    {
        int skip = (size - ((int)BaseStream.Position % size));
        if (skip != size)
        {
            BaseStream.Write(new byte[skip]);
        }
    }
}
