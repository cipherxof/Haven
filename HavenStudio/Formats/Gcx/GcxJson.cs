using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HavenStudio.Formats.Gcx;

public sealed class GcxJson
{
    public int Timestamp { get; set; }
    public int CryptoSeed { get; set; }
    public string? StringSectionPaddingBase64 { get; set; }

    public List<GcxScriptDefJson> ScriptDefinitions { get; set; } = new();
    public List<GcxStringDefJson> StringDefinitions { get; set; } = new();

    // Base64 of main script bytes
    public string MainScriptBase64 { get; set; } = "";
}

public sealed class GcxScriptDefJson
{
    public int Type { get; set; }
    public int? PhysicalOrder { get; set; }
    public string BytesBase64 { get; set; } = "";
}

public sealed class GcxStringDefJson
{
    public int Type { get; set; } // 0x80 => string, else => bytes/script

    // For Type == 0x80
    public string? Value { get; set; }

    // For Type != 0x80
    public string? BytesBase64 { get; set; }
}

public static class GcxJsonConverter
{
    public static GcxJson ToJsonModel(Gcx gcx)
    {
        if (gcx is null) throw new ArgumentNullException(nameof(gcx));

        var dto = new GcxJson
        {
            Timestamp = gcx.Timestamp,
            CryptoSeed = gcx.CryptoSeed,
            StringSectionPaddingBase64 = gcx.StringSectionPadding is null
                ? null
                : Convert.ToBase64String(gcx.StringSectionPadding),
            MainScriptBase64 = Convert.ToBase64String(gcx.MainScript?.Bytes ?? Array.Empty<byte>())
        };

        foreach (var def in gcx.ScriptDefinitions)
        {
            dto.ScriptDefinitions.Add(new GcxScriptDefJson
            {
                Type = def.Type,
                PhysicalOrder = def.PhysicalOrder,
                BytesBase64 = Convert.ToBase64String(def.Script?.Bytes ?? Array.Empty<byte>())
            });
        }

        foreach (var def in gcx.StringDefinitions)
        {
            if (def.Type == 0x80)
            {
                dto.StringDefinitions.Add(new GcxStringDefJson
                {
                    Type = def.Type,
                    Value = def.Value ?? string.Empty,
                    BytesBase64 = null
                });
            }
            else
            {
                dto.StringDefinitions.Add(new GcxStringDefJson
                {
                    Type = def.Type,
                    Value = null,
                    BytesBase64 = Convert.ToBase64String(def.Script?.Bytes ?? Array.Empty<byte>())
                });
            }
        }

        return dto;
    }

    public static Gcx FromJsonModel(GcxJson dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));

        var gcx = new Gcx
        {
            Timestamp = dto.Timestamp,
            CryptoSeed = dto.CryptoSeed,
            StringSectionPadding = dto.StringSectionPaddingBase64 is null
                ? null
                : DecodeBase64(dto.StringSectionPaddingBase64),
            MainScript = new GcxScript(DecodeBase64(dto.MainScriptBase64))
        };

        gcx.ScriptDefinitions.Clear();
        foreach (var sd in dto.ScriptDefinitions)
        {
            gcx.ScriptDefinitions.Add(new GcxScriptDefinition(sd.Type, offset: 0)
            {
                PhysicalOrder = sd.PhysicalOrder ?? -1,
                Script = new GcxScript(DecodeBase64(sd.BytesBase64))
            });
        }

        gcx.StringDefinitions.Clear();
        foreach (var st in dto.StringDefinitions)
        {
            var def = new GcxStringDefinition(st.Type, offset: 0);

            if (st.Type == 0x80)
            {
                def.Value = st.Value ?? string.Empty;
            }
            else
            {
                def.Script = new GcxScript(DecodeBase64(st.BytesBase64));
            }

            gcx.StringDefinitions.Add(def);
        }

        return gcx;
    }

    private static byte[] DecodeBase64(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Array.Empty<byte>();
        return Convert.FromBase64String(s);
    }
}

public static class GcxJsonIO
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(GcxJson dto) =>
        JsonSerializer.Serialize(dto, Options);

    public static GcxJson Deserialize(string json) =>
        JsonSerializer.Deserialize<GcxJson>(json, Options)
        ?? throw new InvalidDataException("Invalid GCX JSON.");

    public static void WriteToStream(Stream output, GcxJson dto)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        using var writer = new StreamWriter(output, leaveOpen: true);
        writer.Write(Serialize(dto));
        writer.Flush();
    }

    public static GcxJson ReadFromStream(Stream input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        using var reader = new StreamReader(input, leaveOpen: true);
        return Deserialize(reader.ReadToEnd());
    }
}
