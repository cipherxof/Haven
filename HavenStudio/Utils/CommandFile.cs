using System;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace HavenStudio.Utils;

public static class CommandFile
{
    private static readonly ILogger _log = Log.ForContext("SourceContext", "CommandFile");

    public static readonly Dictionary<uint, string> Commands = new();

    public static bool Load(string commandsFilename)
    {
        try
        {
            if (!File.Exists(commandsFilename))
            {
                return false;
            }

            var lines = File.ReadAllLines(commandsFilename);
            foreach (var line in lines)
            {
                var cleanedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(cleanedLine)) continue;

                var parts = cleanedLine.Split(" -> ", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;

                var hashStr = parts[0].Trim();
                if (hashStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    hashStr = hashStr.Substring(2);
                }

                if (uint.TryParse(hashStr, System.Globalization.NumberStyles.HexNumber, null, out uint hash))
                {
                    Commands[hash] = parts[1].Trim();
                }
            }

            return true;
        }
        catch (Exception e)
        {
            _log.Error(e, "Failed to parse commands file");
            return false;
        }
    }

    public static string? GetCommandName(uint hash)
    {
        if (Commands.TryGetValue(hash, out var commandName))
        {
            return commandName;
        }

        return null;
    }
}
