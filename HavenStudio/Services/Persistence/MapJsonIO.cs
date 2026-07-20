using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HavenStudio.Services.Persistence;

public static class MapJsonIO
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string Serialize(MapDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, Options);
    }

    public static MapDocument Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("Map JSON is empty.");
        }
        return JsonSerializer.Deserialize<MapDocument>(json, Options)
            ?? throw new InvalidDataException("Invalid map JSON.");
    }

    public static void WriteToStream(Stream output, MapDocument document)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var writer = new StreamWriter(output, leaveOpen: true);
        writer.Write(Serialize(document));
        writer.Flush();
    }

    public static MapDocument ReadFromStream(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var reader = new StreamReader(input, leaveOpen: true);
        return Deserialize(reader.ReadToEnd());
    }
}
