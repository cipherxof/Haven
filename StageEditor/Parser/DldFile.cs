using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser
{
    public enum DldTextureType
    {
        MAIN = 0x02001000,
        MIPS = 0x02031000,
    }

    public class DldTexture
    {
        public uint Type;
        public uint NullBytes;
        public uint HashId;
        public uint ParentDataSize;
        public uint DataSize;
        public uint MipmapCount;
        public uint EntryNumber;
        public uint Padding;
        public byte[] Data = new byte[0];

        public DldTexture(uint type, uint hashId, uint parentDataSize, uint dataSize, uint mipmapCount, uint entryNumber, byte[] data)
        {
            Type = type;
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
            Type = reader.ReadUInt32();
            NullBytes = reader.ReadUInt32();
            HashId = reader.ReadUInt32();
            ParentDataSize = reader.ReadUInt32();
            DataSize = reader.ReadUInt32();
            MipmapCount = reader.ReadUInt32();
            EntryNumber = reader.ReadUInt32();
            Padding = reader.ReadUInt32();

            if (DataSize > 0 && DataSize + reader.BaseStream.Position <= reader.BaseStream.Length)
            {
                Data = reader.ReadBytes((int)DataSize);
            }
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Type);
            writer.Write(NullBytes);
            writer.Write(HashId);
            writer.Write(ParentDataSize);
            writer.Write(DataSize);
            writer.Write(MipmapCount);
            writer.Write(EntryNumber);
            writer.Write(Padding);
            writer.Write(Data);
        }
    }

    public class DldFile
    {
        public List<DldTexture> Textures = new List<DldTexture>();
        public string Name = "";

        public DldFile() { }

        public DldFile(string path)
        {
            Name = Path.GetFileName(path);

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReaderEx(stream, true))
                {
                    while (stream.Position + 0x20 < stream.Length)
                    {
                        var texture = new DldTexture(reader);

                        if (texture.Type != 0)
                        {
                            Textures.Add(texture);
                        }
                    }
                }
            }
        }

        public void Save(string path)
        {
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var writer = new BinaryWriterEx(stream, true))
                {
                    stream.SetLength(0);

                    foreach (var texture in Textures)
                    {
                        texture.WriteTo(writer);
                    }
                }
            }
        }

        public DldTexture? FindTexture(uint objectId, int index, DldTextureType type)
        {
            for (int i = 0; i < Textures.Count; i++)
            {
                var texture = Textures[i];

                if (texture.HashId == objectId && texture.EntryNumber == index && texture.Type == (int)type)
                {
                    return texture;
                }
            }

            return null;
        }
    }
}
