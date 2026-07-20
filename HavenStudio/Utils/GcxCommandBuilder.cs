using System;
using System.Collections.Generic;

namespace HavenStudio.Utils;

public enum GcxCommandType
{
    NewPutObject,
    NewCamera,
    NewSky,
    NewPutStageModelSet
}

public sealed class GcxCommandParameter
{
    public string Name { get; }
    public string Label { get; }
    public GcxParamType Type { get; }
    public object? DefaultValue { get; }

    public GcxCommandParameter(string name, string label, GcxParamType type, object? defaultValue = null)
    {
        Name = name;
        Label = label;
        Type = type;
        DefaultValue = defaultValue;
    }
}

public enum GcxParamType
{
    StrCode,    // 3-byte hash (uint, uses lower 24 bits)
    Int32,      // 4-byte signed integer
    Boolean     // Reserved for future optional toggles
}

public static class GcxCommandBuilder
{
    public static IReadOnlyList<GcxCommandParameter> GetParameters(GcxCommandType commandType)
    {
        return commandType switch
        {
            GcxCommandType.NewPutObject => new[]
            {
                new GcxCommandParameter("model", "Model Hash", GcxParamType.StrCode),
                new GcxCommandParameter("x", "Position X", GcxParamType.Int32),
                new GcxCommandParameter("z", "Position Z", GcxParamType.Int32),
                new GcxCommandParameter("y", "Position Y", GcxParamType.Int32),
                new GcxCommandParameter("collision", "Collision Ref", GcxParamType.StrCode),
                new GcxCommandParameter("pitch", "Rotation X", GcxParamType.Int32),
                new GcxCommandParameter("roll", "Rotation Y", GcxParamType.Int32),
                new GcxCommandParameter("yaw", "Rotation Z", GcxParamType.Int32),
                new GcxCommandParameter("light", "Light Hash", GcxParamType.StrCode),
                new GcxCommandParameter("flag", "Flag", GcxParamType.Int32),
                new GcxCommandParameter("eft", "Effect Hash", GcxParamType.StrCode),
            },
            GcxCommandType.NewCamera => new[]
            {
                new GcxCommandParameter("channel", "Channel", GcxParamType.Int32),
                new GcxCommandParameter("level", "Level", GcxParamType.Int32),
                new GcxCommandParameter("prio", "Priority", GcxParamType.Int32),
            },
            GcxCommandType.NewSky => new[]
            {
                new GcxCommandParameter("model", "Model Hash", GcxParamType.StrCode),
                new GcxCommandParameter("fog", "Fog", GcxParamType.Int32),
                new GcxCommandParameter("area_x", "Area X", GcxParamType.Int32),
                new GcxCommandParameter("area_z", "Area Z", GcxParamType.Int32),
                new GcxCommandParameter("area_y", "Area Y", GcxParamType.Int32),
                new GcxCommandParameter("rot_time", "Rotation Time", GcxParamType.Int32),
                new GcxCommandParameter("rot_way", "Rotation Way", GcxParamType.Int32),
            },
            GcxCommandType.NewPutStageModelSet => new[]
            {
                new GcxCommandParameter("model", "Model Hash", GcxParamType.StrCode),
                new GcxCommandParameter("flag", "Flag", GcxParamType.Int32),
                new GcxCommandParameter("light", "Light Hash", GcxParamType.StrCode),
                new GcxCommandParameter("coltolsc", "Col Tolerance", GcxParamType.Int32),
                new GcxCommandParameter("amb_scale_x", "Amb Scale X", GcxParamType.Int32),
                new GcxCommandParameter("amb_scale_y", "Amb Scale Y", GcxParamType.Int32),
                new GcxCommandParameter("amb_scale_z", "Amb Scale Z", GcxParamType.Int32),
            },
            _ => Array.Empty<GcxCommandParameter>()
        };
    }

    public static byte[] BuildCommand(GcxCommandType commandType, Dictionary<string, object> values)
    {
        return commandType switch
        {
            GcxCommandType.NewPutObject => BuildNewPutObject(values),
            GcxCommandType.NewCamera => BuildNewCamera(values),
            GcxCommandType.NewSky => BuildNewSky(values),
            GcxCommandType.NewPutStageModelSet => BuildNewPutStageModelSet(values),
            _ => Array.Empty<byte>()
        };
    }

    public static byte[] BuildNewPutObject(Dictionary<string, object> values)
    {
        var charaBytes = new List<byte>();

        bool hasModel = TryGetUInt(values, "model", out var modelHash);
        bool hasX = TryGetInt(values, "x", out var x);
        bool hasZ = TryGetInt(values, "z", out var z);
        bool hasY = TryGetInt(values, "y", out var y);
        bool hasCollision = TryGetUInt(values, "collision", out var collisionRef);
        bool hasPitch = TryGetInt(values, "pitch", out var pitch);
        bool hasRoll = TryGetInt(values, "roll", out var roll);
        bool hasYaw = TryGetInt(values, "yaw", out var yaw);
        bool hasLight = TryGetUInt(values, "light", out var lightHash);
        bool hasFlag = TryGetInt(values, "flag", out var flag);
        bool hasEft = TryGetUInt(values, "eft", out var eftHash);

        // chara
        charaBytes.AddRange(new byte[] { 0xA7, 0x92, 0x65, 0x08 });

        // NewPutObject [afb954]
        charaBytes.AddRange(new byte[] { 0x06, 0x16, 0xA5, 0x07, 0x06, 0x54, 0xB9, 0xAF });

        // -model
        if (hasModel)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x6D, 0x13, 0x1D, 0x09, 0x06 });
            charaBytes.AddRange(StrCodeBytes(modelHash));
        }

        // -pos (optional)
        if (hasX || hasZ || hasY)
        {
            charaBytes.AddRange(new byte[] { 0x5D, 0x13, 0x70 });
            charaBytes.AddRange(new byte[] { 0x53, 0xCE, 0x01 });
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(x));
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(z));
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(y));
        }

        // -ref (collision)
        if (hasCollision)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x72, 0x36, 0x9B, 0x84, 0x06 });
            charaBytes.AddRange(StrCodeBytes(collisionRef));
        }

        // -dir (optional)
        if (hasPitch || hasRoll || hasYaw)
        {
            charaBytes.AddRange(new byte[] { 0x5D, 0x13, 0x64 });
            charaBytes.AddRange(new byte[] { 0x92, 0x9D, 0x01 });
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(pitch));
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(roll));
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(yaw));
        }

        // -light
        if (hasLight)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x6C, 0x7A, 0x29, 0xF6, 0x06 });
            charaBytes.AddRange(StrCodeBytes(lightHash));
        }

        // -flag
        if (hasFlag)
        {
            charaBytes.AddRange(new byte[] { 0x59, 0x66, 0x87, 0xBC, 0x34, 0x09 });
            charaBytes.AddRange(NumberBytes(flag));
        }

        // -eft
        if (hasEft)
        {
            charaBytes.AddRange(new byte[] { 0x55, 0x65, 0x34, 0xA1, 0x01, 0x09 });
            charaBytes.AddRange(StrCodeBytes(eftHash));
        }

        // end
        charaBytes.Add(0x00);

        return WrapWithLengthPrefix(charaBytes);
    }

    private static byte[] BuildNewCamera(Dictionary<string, object> values)
    {
        var charaBytes = new List<byte>();

        bool hasChannel = TryGetInt(values, "channel", out var channel);
        bool hasLevel = TryGetInt(values, "level", out var level);
        bool hasPrio = TryGetInt(values, "prio", out var prio);

        // chara
        charaBytes.AddRange(new byte[] { 0xA7, 0x92, 0x65, 0x08 });

        // NewCamera [3d568c]
        charaBytes.AddRange(new byte[] { 0x06, 0xDE, 0x1A, 0x06, 0x06, 0x8C, 0x56, 0x3D });

        // -channel
        if (hasChannel)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x63, 0xA2, 0xDE, 0x48, 0x09 });
            charaBytes.AddRange(NumberBytes(channel));
        }

        // -level
        if (hasLevel)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x6C, 0x12, 0x65, 0xF4, 0x09 });
            charaBytes.AddRange(NumberBytes(level));
        }

        // -prio
        if (hasPrio)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x70, 0x8F, 0xD5, 0x39, 0x09 });
            charaBytes.AddRange(NumberBytes(prio));
        }

        // end
        charaBytes.Add(0x00);

        return WrapWithLengthPrefix(charaBytes);
    }

    private static byte[] BuildNewSky(Dictionary<string, object> values)
    {
        var charaBytes = new List<byte>();

        bool hasModel = TryGetUInt(values, "model", out var modelHash);
        bool hasFog = TryGetInt(values, "fog", out var fog);
        bool hasAreaX = TryGetInt(values, "area_x", out var areaX);
        bool hasAreaZ = TryGetInt(values, "area_z", out var areaZ);
        bool hasAreaY = TryGetInt(values, "area_y", out var areaY);
        bool hasRotTime = TryGetInt(values, "rot_time", out var rotTime);
        bool hasRotWay = TryGetInt(values, "rot_way", out var rotWay);

        // chara
        charaBytes.AddRange(new byte[] { 0xA7, 0x92, 0x65, 0x08 });

        // NewSky [ab40ab]
        charaBytes.AddRange(new byte[] { 0x06, 0x24, 0x2D, 0xCE, 0x06, 0xAB, 0x40, 0xAB });

        // -model
        if (hasModel)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x6D, 0x13, 0x1D, 0x09, 0x06 });
            charaBytes.AddRange(StrCodeBytes(modelHash));
        }

        // -fog
        if (hasFog)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x66, 0x47, 0xA6, 0x01, 0x09 });
            charaBytes.AddRange(NumberBytes(fog));
        }

        // -a (area) with 3 values
        if (hasAreaX || hasAreaZ || hasAreaY)
        {
            charaBytes.AddRange(new byte[] { 0x5D, 0x10, 0x61, 0x34, 0x26, 0x22, 0x09 });
            charaBytes.AddRange(NumberBytes(areaX));
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(areaZ));
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(areaY));
        }

        // -rot_time
        if (hasRotTime)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x72, 0xB3, 0x5E, 0x2F, 0x09 });
            charaBytes.AddRange(NumberBytes(rotTime));
        }

        // -rot_way
        if (hasRotWay)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x72, 0xFE, 0x85, 0x71, 0x09 });
            charaBytes.AddRange(NumberBytes(rotWay));
        }

        // end
        charaBytes.Add(0x00);

        return WrapWithLengthPrefix(charaBytes);
    }

    private static byte[] BuildNewPutStageModelSet(Dictionary<string, object> values)
    {
        var charaBytes = new List<byte>();

        bool hasModel = TryGetUInt(values, "model", out var modelHash);
        bool hasFlag = TryGetInt(values, "flag", out var flag);
        bool hasLight = TryGetUInt(values, "light", out var lightHash);
        bool hasColtolsc = TryGetInt(values, "coltolsc", out var coltolsc);
        bool hasAmbScaleX = TryGetInt(values, "amb_scale_x", out var ambScaleX);
        bool hasAmbScaleY = TryGetInt(values, "amb_scale_y", out var ambScaleY);
        bool hasAmbScaleZ = TryGetInt(values, "amb_scale_z", out var ambScaleZ);

        // chara
        charaBytes.AddRange(new byte[] { 0xA7, 0x92, 0x65, 0x08 });

        // NewPutStageModelSet [7E641F]
        charaBytes.AddRange(new byte[] { 0x06, 0x1F, 0x64, 0x7E });
        charaBytes.AddRange(new byte[] { 0x0D, 0x91, 0xEE, 0xAC, 0x01 });

        // -model
        if (hasModel)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x6D, 0x13, 0x1D, 0x09, 0x06 });
            charaBytes.AddRange(StrCodeBytes(modelHash));
        }

        // -flag
        if (hasFlag)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x66, 0x87, 0xBC, 0x34, 0x09 });
            charaBytes.AddRange(NumberBytes(flag));
        }

        // -light
        if (hasLight)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x6C, 0x7A, 0x29, 0xF6, 0x06 });
            charaBytes.AddRange(StrCodeBytes(lightHash));
        }

        // -coltolsc
        if (hasColtolsc)
        {
            charaBytes.AddRange(new byte[] { 0x58, 0x63, 0x5B, 0xBC, 0x5E, 0x09 });
            charaBytes.AddRange(NumberBytes(coltolsc));
        }

        // -amb_scale (3 values)
        if (hasAmbScaleX || hasAmbScaleY || hasAmbScaleZ)
        {
            charaBytes.AddRange(new byte[] { 0x5D, 0x10, 0x61, 0xF3, 0x82, 0x36, 0x09 });
            charaBytes.AddRange(NumberBytes(ambScaleX));
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(ambScaleY));
            charaBytes.Add(0x09);
            charaBytes.AddRange(NumberBytes(ambScaleZ));
        }

        // end
        charaBytes.Add(0x00);

        return WrapWithLengthPrefix(charaBytes);
    }

    private static byte[] WrapWithLengthPrefix(List<byte> charaBytes)
    {
        return WrapTaggedPayload(0x60, charaBytes.ToArray());
    }

    public static byte[] WrapTaggedPayload(byte tag, ReadOnlySpan<byte> payload)
    {
        if ((tag & 0x0F) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tag), "GCX block tag must contain only its high nibble.");
        }
        if (payload.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("GCX tagged payload exceeds the 16-bit size limit.");
        }

        var prefixLength = payload.Length <= 12 ? 1 : payload.Length <= byte.MaxValue ? 2 : 3;
        var result = new byte[prefixLength + payload.Length];
        if (prefixLength == 1)
        {
            result[0] = (byte)(tag | payload.Length);
        }
        else if (prefixLength == 2)
        {
            result[0] = (byte)(tag | 0x0D);
            result[1] = (byte)payload.Length;
        }
        else
        {
            result[0] = (byte)(tag | 0x0E);
            result[1] = (byte)(payload.Length & 0xFF);
            result[2] = (byte)((payload.Length >> 8) & 0xFF);
        }
        payload.CopyTo(result.AsSpan(prefixLength));
        return result;
    }

    public static byte[] Int32LiteralBytes(int value)
    {
        return
        [
            0x09,
            (byte)(value & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 24) & 0xFF)
        ];
    }

    private static byte[] StrCodeBytes(uint value)
    {
        return new byte[]
        {
            (byte)(value & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)((value >> 16) & 0xFF)
        };
    }

    private static byte[] NumberBytes(int value)
    {
        return new byte[]
        {
            (byte)(value & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 24) & 0xFF)
        };
    }

    private static bool TryGetUInt(Dictionary<string, object> values, string key, out uint value)
    {
        if (values.TryGetValue(key, out var obj))
        {
            switch (obj)
            {
                case uint u:
                    value = u;
                    return true;
                case int i:
                    value = (uint)i;
                    return true;
                case long l:
                    value = (uint)l;
                    return true;
                case string s when uint.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
                case string s when s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                                   uint.TryParse(s.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hex):
                    value = hex;
                    return true;
            }
        }

        value = 0u;
        return false;
    }

    private static bool TryGetInt(Dictionary<string, object> values, string key, out int value)
    {
        if (values.TryGetValue(key, out var obj))
        {
            switch (obj)
            {
                case int i:
                    value = i;
                    return true;
                case uint u:
                    value = (int)u;
                    return true;
                case long l:
                    value = (int)l;
                    return true;
                case string s when int.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
            }
        }

        value = 0;
        return false;
    }
}
