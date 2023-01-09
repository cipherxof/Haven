﻿using System;

namespace Haven.Parser.Geom
{
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

        public GeoDef(BinaryReader reader)
        {
            Version = reader.ReadUInt32();

           // if (Version != 0x0BF68BFE)
            //    throw new Exception("Invalid geom version! This can happen if the geom failed to decrypt, please ensure it's in the proper folder hierarchy.");

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

        public void WriteTo(BinaryWriter writer)
        {

        }
    }
}
