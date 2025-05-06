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
        public static readonly Dictionary<uint, string> ManualLookup = new();
        public static readonly Dictionary<uint, string> Lookup = new();
        public static int dictionaryUsed;

        public static bool Load(string dictionaryFilename, string manualFilename = "bin/manual-strings.txt")
        {
            try
            {
                if (File.Exists(dictionaryFilename))
                {
                    var lines = File.ReadAllLines(dictionaryFilename);

                    foreach (var line in lines)
                    {
                        var cleanedLine = line.Trim();
                        if (string.IsNullOrWhiteSpace(cleanedLine)) continue;

                        var hash = Utils.HashString(cleanedLine);
                        Lookup[hash] = cleanedLine;
                    }
                }

                if (File.Exists(manualFilename))
                {
                    var manualLines = File.ReadAllLines(manualFilename);
                    foreach (var line in manualLines)
                    {
                        var parts = line.Split(':');
                        if (parts.Length != 2) continue;

                        if (uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out uint hash))
                        {
                            ManualLookup[hash] = parts[1].Trim();
                        }
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Error("Failed to parse dictionary or manual file: {Message}", e.Message);
                return false;
            }
        }

        public static string GetHashString(uint hash)
        {
            if (Lookup.TryGetValue(hash, out var dictionaryValue))
            {
                dictionaryUsed = 1;
                return dictionaryValue;
            }
            if (ManualLookup.TryGetValue(hash, out var manualValue))
            {
                dictionaryUsed = 2;
                return manualValue;
            }

            return hash.ToString("X4");
        }
    }
}