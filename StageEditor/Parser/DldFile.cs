using Serilog.Parsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser
{
    public enum DldPriority
    {
        Main = 0,
        Mipmaps = 3,
    }

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

        public DldTexture(byte type, DldPriority prio, uint hashId, uint parentDataSize, uint dataSize, uint mipmapCount, uint entryNumber, byte[] data)
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

        public DldTexture(BinaryReader reader)
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
                if (DataSize + reader.BaseStream.Position <= reader.BaseStream.Length)
                {
                    Data = reader.ReadBytes((int)DataSize);
                }
                else
                {
                    Data = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                }
            }
        }

        public void WriteTo(BinaryWriter writer)
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

    public class DldFile
    {
        public List<DldTexture> Textures = new List<DldTexture>();
        public readonly string Name = "";
        public readonly string Filename = "";

        public DldFile() { }

        public DldFile(string path)
        {
            Name = Path.GetFileName(path);
            Filename = path;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReaderEx(stream))
                {
                    while (stream.Position + 0x20 < stream.Length)
                    {
                        reader.SeekPadding(0x10);

                        var texture = new DldTexture(reader);

                        if (texture.Type != 0)
                        {
                            Textures.Add(texture);
                        }
                    }
                }
            }
        }

        public void Save(string path, bool? bigEndian = null)
        {
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var writer = new BinaryWriterEx(stream, bigEndian))
                {
                    stream.SetLength(0);

                    foreach (var texture in Textures)
                    {
                        texture.WriteTo(writer);
                    }
                }
            }
        }

        public DldTexture? FindTexture(uint objectId, int index, DldPriority prio)
        {
            for (int i = 0; i < Textures.Count; i++)
            {
                var texture = Textures[i];

                if (texture.HashId == objectId && texture.EntryNumber == index && texture.Priority == (byte)prio)
                {
                    return texture;
                }
            }

            return null;
        }

        public bool RemoveTexture(DldTexture texture)
        {
            for (int i = 0; i < Textures.Count; i++)
            {
                if (Textures[i] == texture)
                {
                    for (int n = 0; n < Textures.Count; n++)
                    {
                        if (Textures[n].HashId == texture.HashId && Textures[n].EntryNumber > texture.EntryNumber)
                        {
                            Textures[n].EntryNumber -= 1;
                        }
                    }
                }
            }

            return Textures.Remove(texture);
        }
    }
}
