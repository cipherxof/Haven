using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class BinaryReaderEx : BinaryReader
{

    public static bool DefaultBigEndian = true;

    public bool BigEndian = DefaultBigEndian;

    public BinaryReaderEx(Stream stream, bool? bigEndian = null) : base(stream)
    {
        if (bigEndian == true) BigEndian = true;
        else if (bigEndian == false) BigEndian = false;
        else if (bigEndian == null) BigEndian = DefaultBigEndian;
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

    public OpenTK.Vector3 ReadVector3()
    {
        OpenTK.Vector3 result = new OpenTK.Vector3(0, 0, 0);
        result.X = ReadSingle();
        result.Y = ReadSingle();
        result.Z = ReadSingle();
        return result;
    }

    public OpenTK.Vector4 ReadVector4()
    {
        OpenTK.Vector4 result = new OpenTK.Vector4(0, 0, 0, 0);
        result.X = ReadSingle();
        result.Y = ReadSingle();
        result.Z = ReadSingle();
        result.W = ReadSingle();
        return result;
    }

    public void SeekPadding(int pad)
    {
        int offset = (pad - ((int)BaseStream.Position % pad));

        if (offset != pad)
        {
            BaseStream.Seek(offset, SeekOrigin.Current);
        }
    }
}
