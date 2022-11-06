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

    }
}
