using Haven.Parser;
using Haven.TextureLoaders;
using Joveler.Compression;
using Joveler.Compression.ZLib;
using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Haven
{
    public class Utils
    {
        public static Task<int> RunProcessAsync(string fileName, string args)
        {
            var tcs = new TaskCompletionSource<int>();

            var process = new Process
            {
                StartInfo = {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.Exited += (sender, args) =>
            {
                tcs.SetResult(process.ExitCode);
                process.Dispose();
            };

            process.Start();

            return tcs.Task;
        }

        public static uint HashString(string str)
        {

            foreach (var kvp in Haven.Parser.DictionaryFile.Alias)
            {
                if (string.Equals(kvp.Value, str, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Key;
                }
            }

            // fallback hash
            uint id = 0;
            uint mask = 0x00FFFFFF;

            for (var i = 0; i < str.Length; i++)
            {
                id = ((id >> 19) | (id << 5));
                id += str[i];
                id &= mask;
            }

            return id;
        }

        public static void ExplorerSelectFile(string path)
        {
            try
            {
                string argument = "/select, \"" + path + "\"";

                Process.Start("explorer.exe", argument);
            }
            catch
            {
                MessageBox.Show("ExplorerSelectFile", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void ExplorerOpenDirectory(string path)
        {
            try
            {
                Process.Start("explorer.exe", path);
            }
            catch (Win32Exception win32Exception)
            {
                MessageBox.Show(win32Exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static async Task DecryptFileAsync(string path, string key)
        {
            await RunProcessAsync("bin/SolidEye.exe", $"-dec \"{path}\" -k {key} -o \"{Path.GetDirectoryName(path)}\"");
        }

        public static async Task EncryptFileAsync(string path, string key)
        {
            await RunProcessAsync("bin/SolidEye.exe", $"-enc \"{path}\" -k {key} -o \"{Path.GetDirectoryName(path)}\"");
        }

        public static string GetPathKey(string path)
        {
            var dir = Path.GetDirectoryName(path);

            if (dir == null)
                return "";

            return Directory.GetParent(dir)?.Name + "/" + new DirectoryInfo(dir).Name;
        }


        public static byte[] DeflateBuffer2(byte[] uncompressedBytes, ZLibCompLevel level)
        {
            ZLibCompressOptions compOpts = new ZLibCompressOptions()
            {
                Level = level,
                WindowBits = ZLibWindowBits.Bits15,
                LeaveOpen = false,
                MemLevel = ZLibMemLevel.Level9
            };

            using (MemoryStream fsOrigin = new MemoryStream(uncompressedBytes))
            using (MemoryStream fsComp = new MemoryStream())
            {
                using (DeflateStream zs = new DeflateStream(fsComp, compOpts))
                {
                    fsOrigin.CopyTo(zs);
                }
                return fsComp.ToArray();
            }
        }

        public static byte[] InflateBuffer2(byte[] compressedBytes, int decompressedSize)
        {
            ZLibDecompressOptions decompOpts = new ZLibDecompressOptions()
            {
                WindowBits = ZLibWindowBits.Bits15,
                LeaveOpen = false,
            };

            using (MemoryStream fsComp = new MemoryStream(compressedBytes))
            using (MemoryStream fsDecomp = new MemoryStream())
            using (DeflateStream zs = new DeflateStream(fsComp, decompOpts))
            {
                zs.CopyTo(fsDecomp);

                return fsDecomp.ToArray();
            }
        }

        public static List<DataContainer> Compress(string path)
        {
            List<DataContainer> containers = new List<DataContainer>();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new BinaryReaderEx(stream))
                {
                    byte[] data;

                    while (stream.Length - stream.Position > 0x4000)
                    {
                        data = DeflateBuffer2(reader.ReadBytes(0x4000), ZLibCompLevel.Default);
                        containers.Add(new DataContainer(data.Length, 0x4000, data));
                    }

                    int left = (int)(stream.Length - stream.Position);
                    data = DeflateBuffer2(reader.ReadBytes(left), ZLibCompLevel.Default);
                    containers.Add(new DataContainer(data.Length, left, data));
                }
            }

            return containers;
        }

        public static void RebuildTXN(string dirPath, DldFile cache, DldFile cacheMips, string txnFile)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            FileInfo[] files = dirInfo.GetFiles("*.dds");
            uint objectId;
            var txnHash = dirInfo.Name;
            if (!uint.TryParse(txnHash, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out objectId))
                objectId = HashString(txnHash);

            var texs = cache.Textures.FindAll(t => t.HashId == objectId);

            foreach (var texture in texs)
            {
                cache.RemoveTexture(texture);
            }

            texs = cacheMips.Textures.FindAll(t => t.HashId == objectId);

            foreach (var texture in texs)
            {
                cacheMips.RemoveTexture(texture);
            }

            var txn = new TxnFile();
            uint entry = 0;

            foreach (FileInfo file in files)
            {
                var dds = new ImageDDS(file.FullName);

                string name = file.Name.Replace(".dds", "");

                uint materialId;

                if (!uint.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out materialId))
                    materialId = HashString(name);

                int fcc = (ImageDDS.eFOURCC)dds.PfFourCC == ImageDDS.eFOURCC.DXT1 ? 0x9 : 0xB;
                var index1 = new TxnImage((ushort)dds.Width, (ushort)dds.Height, (ushort)fcc, 0xF1, 0, 0);
                var index2 = new TxnInfo(materialId, objectId, (ushort)dds.Width, (ushort)dds.Height, 0, 0, (uint)(txn.Images.Count * 0x10) + 0x20, 1f, 1f, 0, 0);
                uint mipMapCount = (uint)Math.Log2(Math.Max(dds.Height, dds.Width));

                if (dds.MipMapOffset != 0)
                {
                    int textureSize = dds.MipMapOffset - 0x80;
                    byte[] data = new byte[textureSize];
                    Array.Copy(dds.Data, 0x80, data, 0, textureSize);
                    var tex = new DldTexture(2, DldPriority.Main, objectId, 0, (uint)textureSize, 1, entry, data);

                    int mipsSize = dds.Data.Length - dds.MipMapOffset;
                    byte[] mipsData = new byte[mipsSize];
                    Array.Copy(dds.Data, dds.MipMapOffset, mipsData, 0, mipsSize);
                    var texMips = new DldTexture(2, DldPriority.Mipmaps, objectId, (uint)textureSize, (uint)mipsSize, 0xE, entry, mipsData);
                    cache.Textures.Add(tex);
                    cacheMips.Textures.Add(texMips);
                }
                else
                {
                    int textureSize = dds.Data.Length - 0x80;
                    byte[] data = new byte[textureSize];
                    Array.Copy(dds.Data, 0x80, data, 0, textureSize);
                    var tex = new DldTexture(2, DldPriority.Mipmaps, objectId, 0, (uint)textureSize, 0xF, entry, data);

                    index1.Flag = 0xF0;

                    cacheMips.Textures.Add(tex);
                }

                txn.Images.Add(index1);
                txn.ImageInfo.Add(index2);

                entry++;
            }

            txn.Header.IndexOffset = 0x20;
            txn.Header.IndexOffset2 = (uint)(0x20 + (txn.Images.Count * 0x10));
            txn.Header.TextureCount = (uint)(txn.Images.Count);
            txn.Header.TextureCount2 = (uint)(txn.ImageInfo.Count);
            txn.Header.Flags = 0x120;

            string txnName = DictionaryFile.Lookup.ContainsKey(objectId) ? DictionaryFile.GetHashString(objectId) : objectId.ToString("X4").ToLower();
            txn.Save(txnFile);

            cache.Save(cache.Filename);
            cacheMips.Save(cacheMips.Filename);
        }

        public static void FaceBitCalculation(int extraBit, ref int fa, ref int fb, ref int fc, ref int fd)
        {
            if (extraBit == 170)
            {
                fa = fa + 512;
                fb = fb + 512;
                fc = fc + 512;
                fd = fd + 512;
            }
            else if (extraBit == 169)
            {
                fa = fa + 256;
                fb = fb + 512;
                fc = fc + 512;
                fd = fd + 512;
            }
            else if (extraBit == 168)
            {
                fb = fb + 512;
                fc = fc + 512;
                fd = fd + 512;
            }
            else if (extraBit == 166)
            {
                fa = fa + 512;
                fb = fb + 256;
                fc = fc + 512;
                fd = fd + 512;
            }
            else if (extraBit == 165)
            {
                fa = fa + 256;
                fb = fb + 256;
                fc = fc + 512;
                fd = fd + 512;
            }
            else if (extraBit == 162)
            {
                fa = fa + 512;
                fc = fc + 512;
                fd = fd + 512;
            }
            else if (extraBit == 161)
            {
                fa = fa + 256;
                fc = fc + 512;
                fd = fd + 512;
            }
            else if (extraBit == 154)
            {
                fa = fa + 512;
                fb = fb + 512;
                fc = fc + 256;
                fd = fd + 512;
            }
            else if (extraBit == 149)
            {
                fa = fa + 256;
                fb = fb + 256;
                fc = fc + 256;
                fd = fd + 512;
            }
            else if (extraBit == 150)
            {
                fa = fa + 512;
                fb = fb + 256;
                fc = fc + 256;
                fd = fd + 512;
            }
            else if (extraBit == 145)
            {
                fa = fa + 256;
                fc = fc + 256;
                fd = fd + 512;
            }
            else if (extraBit == 106)
            {
                fa = fa + 512;
                fb = fb + 512;
                fc = fc + 512;
                fd = fd + 256;
            }
            else if (extraBit == 105)
            {
                fa = fa + 256;
                fb = fb + 512;
                fc = fc + 512;
                fd = fd + 256;
            }
            else if (extraBit == 102)
            {
                fa = fa + 512;
                fb = fb + 256;
                fc = fc + 512;
                fd = fd + 256;
            }
            else if (extraBit == 101)
            {
                fa = fa + 256;
                fb = fb + 256;
                fc = fc + 512;
                fd = fd + 256;
            }
            else if (extraBit == 90)
            {
                fa = fa + 512;
                fb = fb + 512;
                fc = fc + 256;
                fd = fd + 256;
            }
            else if (extraBit == 89)
            {
                fa = fa + 256;
                fb = fb + 512;
                fc = fc + 256;
                fd = fd + 256;
            }
            else if (extraBit == 86)
            {
                fa = fa + 512;
                fb = fb + 256;
                fc = fc + 256;
                fd = fd + 256;
            }
            else if (extraBit == 85)
            {
                fa = fa + 256;
                fb = fb + 256;
                fc = fc + 256;
                fd = fd + 256;
            }
            else if (extraBit == 84)
            {
                fb = fb + 256;
                fc = fc + 256;
                fd = fd + 256;
            }
            else if (extraBit == 81)
            {
                fa = fa + 256;
                fc = fc + 256;
                fd = fd + 256;
            }
            else if (extraBit == 80)
            {
                fc = fc + 256;
                fd = fd + 256;
            }
            else if (extraBit == 69)
            {
                fa = fa + 256;
                fb = fb + 256;
                fd = fd + 256;
            }
            else if (extraBit == 68)
            {
                fb = fb + 256;
                fd = fd + 256;
            }
            else if (extraBit == 65)
            {
                fa = fa + 256;
                fd = fd + 256;
            }
            else if (extraBit == 64)
            {
                fd = fd + 256;
            }
            else if (extraBit == 41)
            {
                fa = fa + 256;
                fb = fb + 512;
                fc = fc + 512;
            }
            else if (extraBit == 21)
            {
                fa = fa + 256;
                fb = fb + 256;
                fc = fc + 256;
            }
            else if (extraBit == 20)
            {
                fb = fb + 256;
                fc = fc + 256;
            }
            else if (extraBit == 17)
            {
                fa = fa + 256;
                fc = fc + 256;
            }
            else if (extraBit == 16)
            {
                fc = fc + 256;
            }
            else if (extraBit == 5)
            {
                fa = fa + 256;
                fb = fb + 256;
            }
            else if (extraBit == 4)
            {
                fb = fb + 256;
            }
            else if (extraBit == 1)
            {
                fa = fa + 256;
            }
        }

    }
}
