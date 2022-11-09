﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace Haven.Parser.Geom
{
    public class GeoChunk
    {
        public ushort Type;
        public ushort Pad;
        public int Size;
        public int DataOffset;

        public GeoChunk(BinaryReader reader)
        {
            Type = reader.ReadUInt16();
            Pad = reader.ReadUInt16();
            Size = reader.ReadInt32();
            DataOffset = reader.ReadInt32();
        }
    }
}
