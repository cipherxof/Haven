using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haven.Parser
{
    public static class DictionaryFile
    {
        private static Dictionary<uint, string> Lookup = new Dictionary<uint, string>();

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
                return false;
            }
        }

        public static string GetHashString(uint hash)
        {
            if (Lookup.ContainsKey(hash))
                return Lookup[hash];

            return hash.ToString("X4");
        }
    }
}
