using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using OpenTK.Mathematics;

namespace HavenStudio.Extensions;

public enum Endianness
{
    Little,
    Big
}

public class EndianBinaryReader(Stream input, Endianness? endianness = null, bool? leaveOpen = false)
    : BinaryReader(input, Encoding.UTF8, leaveOpen ?? false)
{
    public static Endianness DefaultEndianness = Endianness.Big;

    public Endianness Endianness { get; set; } = endianness ?? DefaultEndianness;

    public void Skip(int byteCount)
    {
        if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        if (byteCount == 0) return;

        var s = BaseStream;
        if (s.CanSeek)
        {
            s.Seek(byteCount, SeekOrigin.Current);
            return;
        }

        Span<byte> tmp = stackalloc byte[256];
        int remaining = byteCount;
        while (remaining > 0)
        {
            int chunk = Math.Min(tmp.Length, remaining);
            ReadExactly(tmp.Slice(0, chunk));
            remaining -= chunk;
        }
    }

    public void Align(int alignment)
    {
        if (alignment <= 0) throw new ArgumentOutOfRangeException(nameof(alignment));
        long pos = BaseStream.Position;
        long mod = pos % alignment;
        if (mod != 0) Skip((int)(alignment - mod));
    }

    public override short ReadInt16()
    {
        Span<byte> b = stackalloc byte[2];
        ReadExactly(b);
        return Endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt16BigEndian(b)
            : BinaryPrimitives.ReadInt16LittleEndian(b);
    }

    public override ushort ReadUInt16()
    {
        Span<byte> b = stackalloc byte[2];
        ReadExactly(b);
        return Endianness == Endianness.Big
            ? BinaryPrimitives.ReadUInt16BigEndian(b)
            : BinaryPrimitives.ReadUInt16LittleEndian(b);
    }

    public override int ReadInt32()
    {
        Span<byte> b = stackalloc byte[4];
        ReadExactly(b);
        return Endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt32BigEndian(b)
            : BinaryPrimitives.ReadInt32LittleEndian(b);
    }

    public override uint ReadUInt32()
    {
        Span<byte> b = stackalloc byte[4];
        ReadExactly(b);
        return Endianness == Endianness.Big
            ? BinaryPrimitives.ReadUInt32BigEndian(b)
            : BinaryPrimitives.ReadUInt32LittleEndian(b);
    }

    public override float ReadSingle()
    {
        int bits = ReadInt32();
        return BitConverter.Int32BitsToSingle(bits);
    }

    public override long ReadInt64()
    {
        Span<byte> b = stackalloc byte[8];
        ReadExactly(b);
        return Endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt64BigEndian(b)
            : BinaryPrimitives.ReadInt64LittleEndian(b);
    }

    public override ulong ReadUInt64()
    {
        Span<byte> b = stackalloc byte[8];
        ReadExactly(b);
        return Endianness == Endianness.Big
            ? BinaryPrimitives.ReadUInt64BigEndian(b)
            : BinaryPrimitives.ReadUInt64LittleEndian(b);
    }

    public override void ReadExactly(Span<byte> buffer)
    {
        int readTotal = 0;
        while (readTotal < buffer.Length)
        {
            int n = BaseStream.Read(buffer.Slice(readTotal));
            if (n <= 0) throw new EndOfStreamException();
            readTotal += n;
        }
    }

    public string ReadCString(Encoding enc)
    {
        if (enc is null) throw new ArgumentNullException(nameof(enc));

        using var ms = new MemoryStream();
        while (true)
        {
            int b = this.BaseStream.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            if (b == 0) break;
            ms.WriteByte((byte)b);
        }
        return enc.GetString(ms.ToArray());
    }

    public Vector3 ReadVector3()
    {
        var result = new Vector3(0, 0, 0);
        result.X = ReadSingle();
        result.Y = ReadSingle();
        result.Z = ReadSingle();
        return result;
    }

    public Vector4 ReadVector4()
    {
        var result = new Vector4(0, 0, 0, 0);
        result.X = ReadSingle();
        result.Y = ReadSingle();
        result.Z = ReadSingle();
        result.W = ReadSingle();
        return result;
    }

    public Matrix4 ReadMatrix4()
    {
        var result = new Matrix4();
        result.Row0 = ReadVector4();
        result.Row1 = ReadVector4();
        result.Row2 = ReadVector4();
        result.Row3 = ReadVector4();
        return result;
    }
}

public class EndianBinaryWriter : BinaryWriter
{
    public static Endianness DefaultEndianness = Endianness.Big;

    public Endianness Endianness { get; set; }

    public EndianBinaryWriter(Stream output, Endianness? endianness = null, bool? leaveOpen = null)
        : base(output, Encoding.UTF8, leaveOpen ?? false)
    {
        Endianness = endianness ?? DefaultEndianness;
    }

    public void Align(int alignment)
    {
        if (alignment <= 0) throw new ArgumentOutOfRangeException(nameof(alignment));
        long pos = BaseStream.Position;
        long mod = pos % alignment;
        if (mod != 0) WriteZero((int)(alignment - mod));
    }

    public void WriteZero(int byteCount)
    {
        if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        if (byteCount == 0) return;

        Span<byte> zero = stackalloc byte[256];
        int remaining = byteCount;
        while (remaining > 0)
        {
            int chunk = Math.Min(zero.Length, remaining);
            BaseStream.Write(zero.Slice(0, chunk));
            remaining -= chunk;
        }
    }

    public override void Write(short value) => WriteInt16(value);
    public override void Write(ushort value) => WriteUInt16(value);
    public override void Write(int value) => WriteInt32(value);
    public override void Write(uint value) => WriteUInt32(value);
    public override void Write(long value) => WriteInt64(value);
    public override void Write(ulong value) => WriteUInt64(value);
    public override void Write(float value) => WriteSingle(value);
    public override void Write(byte value) => base.Write(value);
    public override void Write(byte[] buffer) => base.Write(buffer);
    public void Write(Matrix4 value) => WriteMatrix4(value);
    public void Write(Vector3 value) => WriteVec3(value);
    public void Write(Vector4 value) => WriteVec4(value);
    
    public void WriteInt16(short value)
    {
        Span<byte> b = stackalloc byte[2];
        if (Endianness == Endianness.Big) BinaryPrimitives.WriteInt16BigEndian(b, value);
        else BinaryPrimitives.WriteInt16LittleEndian(b, value);
        BaseStream.Write(b);
    }

    public void WriteUInt16(ushort value)
    {
        Span<byte> b = stackalloc byte[2];
        if (Endianness == Endianness.Big) BinaryPrimitives.WriteUInt16BigEndian(b, value);
        else BinaryPrimitives.WriteUInt16LittleEndian(b, value);
        BaseStream.Write(b);
    }

    public void WriteInt32(int value)
    {
        Span<byte> b = stackalloc byte[4];
        if (Endianness == Endianness.Big) BinaryPrimitives.WriteInt32BigEndian(b, value);
        else BinaryPrimitives.WriteInt32LittleEndian(b, value);
        BaseStream.Write(b);
    }

    public void WriteUInt32(uint value)
    {
        Span<byte> b = stackalloc byte[4];
        if (Endianness == Endianness.Big) BinaryPrimitives.WriteUInt32BigEndian(b, value);
        else BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        BaseStream.Write(b);
    }

    public void WriteSingle(float value)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        WriteInt32(bits);
    }

    public void WriteInt64(long value)
    {
        Span<byte> b = stackalloc byte[8];
        if (Endianness == Endianness.Big) BinaryPrimitives.WriteInt64BigEndian(b, value);
        else BinaryPrimitives.WriteInt64LittleEndian(b, value);
        BaseStream.Write(b);
    }

    public void WriteUInt64(ulong value)
    {
        Span<byte> b = stackalloc byte[8];
        if (Endianness == Endianness.Big) BinaryPrimitives.WriteUInt64BigEndian(b, value);
        else BinaryPrimitives.WriteUInt64LittleEndian(b, value);
        BaseStream.Write(b);
    }
    
    public void WriteBytes(byte[] value)
    {
        BaseStream.Write(value);
    }

    public void WriteCString(string? value, Encoding enc)
    {
        if (enc is null) throw new ArgumentNullException(nameof(enc));
        value ??= string.Empty;
        var bytes = enc.GetBytes(value);
        this.BaseStream.Write(bytes, 0, bytes.Length);
        this.Write((byte)0x00);
    }

    public void WriteVec3(Vector3 value)
    {
        Write(value.X);
        Write(value.Y);
        Write(value.Z);
    }

    public void WriteVec4(Vector4 value)
    {
        Write(value.X);
        Write(value.Y);
        Write(value.Z);
        Write(value.W);
    }

    public void WriteMatrix4(Matrix4 value)
    {
        WriteVec4(value.Row0);
        WriteVec4(value.Row1);
        WriteVec4(value.Row2);
        WriteVec4(value.Row3);
    }
}
