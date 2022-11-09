using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class BinaryReaderEx : BinaryReader
{

    public bool BigEndian;

    public BinaryReaderEx(Stream stream, bool bigEndian = false) : base(stream) 
    {
        BigEndian = bigEndian;
    }

    public override int ReadInt32()
    {
        var data = base.ReadBytes(4);
        if (BigEndian) Array.Reverse(data);
        return BitConverter.ToInt32(data, 0);
    }

    public override short ReadInt16()
    {
        var data = base.ReadBytes(2);
        if (BigEndian) Array.Reverse(data);
        return BitConverter.ToInt16(data, 0);
    }

    public override float ReadSingle()
    {
        var data = base.ReadBytes(4);
        if (BigEndian) Array.Reverse(data);
        return BitConverter.ToSingle(data, 0);
    }

    public override long ReadInt64()
    {
        var data = base.ReadBytes(8);
        if (BigEndian) Array.Reverse(data);
        return BitConverter.ToInt64(data, 0);
    }

    public override ushort ReadUInt16()
    {
        var data = base.ReadBytes(2);
        if (BigEndian) Array.Reverse(data);
        return BitConverter.ToUInt16(data, 0);
    }

    public override uint ReadUInt32()
    {
        var data = base.ReadBytes(4);
        if (BigEndian) Array.Reverse(data);
        return BitConverter.ToUInt32(data, 0);
    }

    public override ulong ReadUInt64()
    {
        var data = base.ReadBytes(8);
        if (BigEndian) Array.Reverse(data);
        return BitConverter.ToUInt64(data, 0);
    }
}
