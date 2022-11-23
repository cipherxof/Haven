using Haven.Render;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser
{
    public class DciHeader
    {
        public uint Magic;
        public int Field004;
        public int Entries;
        public int Field00C;

        public DciHeader(BinaryReader reader)
        {
            Magic = reader.ReadUInt32();
            Field004 = reader.ReadInt32();
            Entries = reader.ReadInt32();
            Field00C = reader.ReadInt32();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Magic);
            writer.Write(Field004);
            writer.Write(Entries);
            writer.Write(Field00C);
        }
    }

    public class DciEntry
    {
        public uint Hash;
        public uint Offset;
        public ushort Field008;
        public ushort AliasCount;

        public DciEntry(BinaryReader reader)
        {
            Hash = reader.ReadUInt32();
            Offset = reader.ReadUInt32();
            Field008 = reader.ReadUInt16();
            AliasCount = reader.ReadUInt16();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Hash);
            writer.Write(Offset);
            writer.Write(Field008);
            writer.Write(AliasCount);
        }
    }

    public class DciAlias
    {
        public short DldEntry;
        public short TxnIndex;

        public DciAlias(short dldEntry, short txnIndex)
        {
            DldEntry = dldEntry;
            TxnIndex = txnIndex;
        }

        public DciAlias(BinaryReader reader)
        {
            DldEntry = reader.ReadInt16();
            TxnIndex = reader.ReadInt16();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(DldEntry);
            writer.Write(TxnIndex);
        }
    }

    public class DciFile
    {
        public DciHeader Header;
        public List<DciEntry> Entries = new List<DciEntry>();
        public Dictionary<DciEntry, List<DciAlias>> Aliases = new Dictionary<DciEntry, List<DciAlias>>();

        public DciFile(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReaderEx(stream, true))
                {
                    Header = new DciHeader(reader);

                    for (int i = 0; i < Header.Entries; i++)
                    {
                        Entries.Add(new DciEntry(reader));
                    }

                    foreach (var entry in Entries)
                    {
                        Aliases[entry] = new List<DciAlias>();

                        if (entry.Offset > 1)
                        {
                            stream.Seek(entry.Offset, SeekOrigin.Begin);

                            for (int n = 0; n < entry.AliasCount; n++)
                            {
                                Aliases[entry].Add(new DciAlias(reader));
                            }
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

                    Header.Entries = Entries.Count;
                    Header.WriteTo(writer);

                    var posEntries = stream.Position;

                    foreach (var entry in Entries)
                    {
                        entry.WriteTo(writer);
                    }

                    foreach (var entry in Entries)
                    {
                        if (entry.Offset > 1)
                        {
                            entry.Offset = (ushort)stream.Position;

                            foreach (var alias in Aliases[entry])
                            {
                                alias.WriteTo(writer);
                            }
                        }
                    }

                    stream.Seek(posEntries, SeekOrigin.Begin);

                    foreach (var entry in Entries)
                    {
                        entry.WriteTo(writer);
                    }
                }
            }
        }
    }
}
