using System;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace HavenStudio.Utils;

public static class DictionaryFile
{
    private static readonly ILogger _log = Log.ForContext("SourceContext", "DictionaryFile");

    public static readonly Dictionary<uint, string> Alias = new();
    public static readonly Dictionary<uint, string> Lookup = new();

    public static bool Load(string dictionaryFilename, string aliasFilename)
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

                    var hash = Utils.String.HashString(cleanedLine);
                    Lookup[hash] = cleanedLine;
                }
            }

            if (File.Exists(aliasFilename))
            {
                var aliasLines = File.ReadAllLines(aliasFilename);
                foreach (var line in aliasLines)
                {
                    var parts = line.Split(':');
                    if (parts.Length != 2) continue;

                    if (uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out uint hash))
                    {
                        Alias[hash] = parts[1].Trim();
                    }
                }
            }

            return true;
        }
        catch (Exception e)
        {
            _log.Error(e, "Failed to parse dictionary or alias file");
            return false;
        }
    }

    public static string GetHashString(uint hash)
    {
        if (Lookup.TryGetValue(hash, out var dictionaryValue))
        {

            return dictionaryValue;
        }

        if (Alias.TryGetValue(hash, out var aliasValue))
        {
            return $"{aliasValue} (A)";
        }

        return hash.ToString("X4");
    }

    public static bool TryGetLookupName(uint hash, out string name)
    {
        if (Lookup.TryGetValue(hash, out var dictionaryValue))
        {
            name = dictionaryValue;
            return true;
        }

        if (Alias.TryGetValue(hash, out var aliasValue))
        {
            name = aliasValue;
            return true;
        }

        name = string.Empty;
        return false;
    }
}
