using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class BinaryWriterEx : BinaryWriter
{
    public static bool DefaultBigEndian = true;

    public bool BigEndian = DefaultBigEndian;

    public BinaryWriterEx(Stream stream, bool? bigEndian = null) : base(stream) 
    {
        if (bigEndian == true) BigEndian = true;
        else if (bigEndian == false) BigEndian = false;
        else if (bigEndian == null) BigEndian = DefaultBigEndian;
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

    public void Write(Vector3 value)
    {
        base.Write(value.X);
        base.Write(value.Y);
        base.Write(value.Z);
    }

    public void Write(Vector4 value)
    {
        base.Write(value.X);
        base.Write(value.Y);
        base.Write(value.Z);
        base.Write(value.W);
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
