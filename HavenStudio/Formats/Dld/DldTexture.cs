using System.IO;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Dld;

public class DldTexture
{
    public byte Type;
    public byte Priority;
    public byte Alignment;
    public byte Pad;
    public uint NullBytes;
    public uint HashId;
    public uint ParentDataSize;
    public uint DataSize;
    public uint MipmapCount;
    public uint EntryNumber;
    public uint Padding;
    public byte[] Data = new byte[0];

    public DldTexture(byte type, DldPriority prio, uint hashId, uint parentDataSize, uint dataSize,
        uint mipmapCount, uint entryNumber, byte[] data)
    {
        Type = type;
        Priority = (byte)prio;
        Alignment = 0x10;
        Pad = 0;
        NullBytes = 0;
        HashId = hashId;
        ParentDataSize = parentDataSize;
        DataSize = dataSize;
        MipmapCount = mipmapCount;
        EntryNumber = entryNumber;
        Padding = 0;
        Data = data;
    }

    public DldTexture(EndianBinaryReader reader)
    {
        Type = reader.ReadByte();
        Priority = reader.ReadByte();
        Alignment = reader.ReadByte();
        Pad = reader.ReadByte();
        NullBytes = reader.ReadUInt32();
        HashId = reader.ReadUInt32();
        ParentDataSize = reader.ReadUInt32();
        DataSize = reader.ReadUInt32();
        MipmapCount = reader.ReadUInt32();
        EntryNumber = reader.ReadUInt32();
        Padding = reader.ReadUInt32();

        if (DataSize > 0)
        {
            var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (DataSize > int.MaxValue || DataSize > (ulong)remaining)
            {
                throw new InvalidDataException(
                    $"DLD texture payload at 0x{reader.BaseStream.Position:X} is truncated (declared {DataSize} bytes, {remaining} available).");
            }

            Data = reader.ReadBytes((int)DataSize);
        }
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(Type);
        writer.Write(Priority);
        writer.Write(Alignment);
        writer.Write(Pad);
        writer.Write(NullBytes);
        writer.Write(HashId);
        writer.Write(ParentDataSize);
        writer.Write(DataSize);
        writer.Write(MipmapCount);
        writer.Write(EntryNumber);
        writer.Write(Padding);
        writer.Write(Data);

        int padding = (16 - ((int)writer.BaseStream.Position % 16));
        if (padding != 16)
            writer.Write(new byte[padding]);
    }
}
