using System.Collections.Generic;
using HavenStudio.Extensions;

namespace HavenStudio.Formats.Geo;

public class GeoDef
{
    public uint Version;
    public uint FileSize;
    public int ChunkCount;
    public int Pad;
    public float BaseX;
    public float BaseY;
    public float BaseZ;
    public float BaseW;
    public List<GeoChunk> Chunks;

    public GeoDef(EndianBinaryReader reader)
    {
        Version = reader.ReadUInt32();
        FileSize = reader.ReadUInt32();
        ChunkCount = reader.ReadInt32();
        Pad = reader.ReadInt32();
        BaseX = reader.ReadSingle();
        BaseY = reader.ReadSingle();
        BaseZ = reader.ReadSingle();
        BaseW = reader.ReadSingle();

        Chunks = new List<GeoChunk>();

        for (int i = 0; i < ChunkCount; i++)
        {
            GeoChunk chunk = new GeoChunk(reader);
            Chunks.Add(chunk);
            
            if (i > 0)
            {
                GeoChunk prevChunk = Chunks[i - 1];

                var actualSize = chunk.DataOffset - prevChunk.DataOffset;

                if (actualSize > prevChunk.Size)
                {
                    Chunks[i - 1].Size = actualSize;
                }
            }

            Chunks[i] = chunk;
        }
    }

    public void WriteTo(EndianBinaryWriter writer)
    {
        writer.Write(Version);
        writer.Write((uint)writer.BaseStream.Length);
        writer.Write(Chunks.Count);
        writer.Write(Pad);
        writer.Write(BaseX);
        writer.Write(BaseY);
        writer.Write(BaseZ);
        writer.Write(BaseW);

        Chunks.ForEach(chunk => chunk.WriteTo(writer));
    }
}
