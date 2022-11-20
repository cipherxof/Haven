using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;

namespace Haven.Parser
{
    public static class DdsFile
    {
        public static void Create(string path, uint height, uint width, string fourCC, int mipMapCount, byte[] data)
        {
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (var writer = new BinaryWriterEx(stream, false))
                {
                    stream.SetLength(0);

                    writer.Write(0x20534444);
                    writer.Write(0x7C);
                    writer.Write(0x00021007);
                    writer.Write(height);
                    writer.Write(width);
                    writer.Write(0); // pitch
                    writer.Write(0); // depth
                    writer.Write(mipMapCount);
                    for (int i = 0; i < 11; i++)
                    {
                        writer.Write(0);
                    }
                    writer.Write(0x20); // pixel format size
                    writer.Write(0x05); // flags
                    writer.Write(Encoding.UTF8.GetBytes(fourCC));
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0x00401008);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(data);
                }
            }
        }
    }
}
