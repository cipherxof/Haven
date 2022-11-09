using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static async Task DecryptFileAsync(string path, string key)
        {
            await RunProcessAsync("bin/SolidEye.exe", $"-dec \"{path}\" -k {key} -o \"{Path.GetDirectoryName(path)}\"");
        }

        public static async Task EncryptFileAsync(string path, string key)
        {
            await RunProcessAsync("bin/SolidEye.exe", $"-enc \"{path}\" -k {key} -o \"{Path.GetDirectoryName(path)}\"");
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
