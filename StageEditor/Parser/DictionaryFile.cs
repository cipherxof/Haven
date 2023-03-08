using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser
{
    public static class DictionaryFile
    {
        public static readonly Dictionary<uint, string> Lookup = new Dictionary<uint, string>();

        public static bool Load(string filename)
        {
            try
            {
                var lines = File.ReadAllLines(filename);

                foreach (var line in lines)
                {
                    var hash = Utils.HashString(line.Replace("\n", ""));
                    Lookup[hash] = line;
                }

                return true;
            }
            catch(Exception e)
            {
                Log.Error("Failed to parse dictionary file {filename} \"{s}\"", filename, e.Message);
                return false;
            }
        }

        public static string GetHashString(uint hash)
        {
            if (Lookup.ContainsKey(hash))
            {
                return Lookup[hash];
            }

            return hash.ToString("X4");
        }

    }
}
