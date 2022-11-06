using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class BinaryWriterEx : BinaryWriter
{
    public BinaryWriterEx(Stream stream) : base(stream) { }

    public void WriteInt32BE(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        base.Write(BitConverter.ToInt32(bytes, 0));
    }

    public void WriteUInt16BE(UInt16 value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        base.Write(BitConverter.ToUInt16(bytes, 0));
    }

    public void WriteUInt32BE(UInt32 value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        base.Write(BitConverter.ToUInt32(bytes, 0));
    }

    public void WriteUInt64BE(UInt64 value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        base.Write(BitConverter.ToUInt64(bytes, 0));
    }

    public void WriteSingleBE(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        base.Write(BitConverter.ToSingle(bytes, 0));
    }

    public void WriteBytes(byte[] bytes)
    {
        Array.Reverse(bytes);
        base.Write(bytes);
    }
}
