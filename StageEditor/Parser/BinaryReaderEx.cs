using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
public class BinaryReaderEx : BinaryReader
{
    public BinaryReaderEx(Stream stream) : base(stream) { }

    public int ReadInt32BE()
    {
        var data = base.ReadBytes(4);
        Array.Reverse(data);
        return BitConverter.ToInt32(data, 0);
    }

    public Int16 ReadInt16BE()
    {
        var data = base.ReadBytes(2);
        Array.Reverse(data);
        return BitConverter.ToInt16(data, 0);
    }

    public float ReadSingleBE()
    {
        var data = base.ReadBytes(4);
        Array.Reverse(data);
        return BitConverter.ToSingle(data, 0);
    }

    public Int64 ReadInt64BE()
    {
        var data = base.ReadBytes(8);
        Array.Reverse(data);
        return BitConverter.ToInt64(data, 0);
    }

    public UInt16 ReadUInt16BE()
    {
        var data = base.ReadBytes(2);
        Array.Reverse(data);
        return BitConverter.ToUInt16(data, 0);
    }

    public UInt32 ReadUInt32BE()
    {
        var data = base.ReadBytes(4);
        Array.Reverse(data);
        return BitConverter.ToUInt32(data, 0);
    }

    public UInt64 ReadUInt64BE()
    {
        var data = base.ReadBytes(8);
        Array.Reverse(data);
        return BitConverter.ToUInt64(data, 0);
    }

    public Single ReadUSingle()
    {
        var data = base.ReadBytes(4);
        Array.Reverse(data);
        return BitConverter.ToSingle(data, 0);
    }

    public byte[] ReadChunk(int start, int end)
    {
        long pos = base.BaseStream.Position;
        int length = end - start;
        base.BaseStream.Seek(start, SeekOrigin.Begin);
        var data = base.ReadBytes(length);
        base.BaseStream.Seek(pos, SeekOrigin.Begin);
        return data;
    }
}
