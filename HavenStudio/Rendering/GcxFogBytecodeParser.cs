using System;
using System.Collections.Generic;
using HavenStudio.Formats.Gcx;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

/// <summary>
/// Reads NewFogSet directly from GCX bytecode. This deliberately does not depend on
/// dictionary names or decompiler formatting, so scripts whose command names are
/// unresolved remain usable by the viewport preview.
///
/// The reader only accepts literal numeric arguments. Commands that calculate fog
/// values through expressions are left to <see cref="GcxFogParser"/> as a fallback.
/// Malformed or unsupported tokens are bounded by their GCX size fields and never
/// make the scanner walk outside a script.
/// </summary>
public static class GcxFogBytecodeParser
{
    private const uint NewFogSetHash = 0xDDE914;
    private const uint NearHash = 0x38A092;
    private const uint FarHash = 0x01A492;
    private const uint RgbHash = 0x01D542;
    private const uint ViewportHash = 0xAD95C5;
    private const uint LimitHash = 0xF6419A;
    private const uint BeforeNearHash = 0x323777;
    private const uint BeforeFarHash = 0x297149;
    private const uint BeforeRgbHash = 0x29A1F9;
    private const uint BeforeLimitHash = 0x291E3A;

    public static SceneFogSettings? Parse(Gcx? document)
    {
        if (document == null)
        {
            return null;
        }

        var accumulator = new FogAccumulator();
        var visited = new HashSet<GcxScript>(ReferenceEqualityComparer.Instance);

        // Script definitions are scanned first and the main script last. When a
        // stage contains more than one literal NewFogSet, the latest command seen
        // therefore behaves as the final viewport state rather than an arbitrary
        // dictionary/decompiler ordering.
        foreach (var definition in document.ScriptDefinitions)
        {
            Scan(definition.Script, visited, accumulator);
        }
        foreach (var definition in document.StringDefinitions)
        {
            Scan(definition.Script, visited, accumulator);
        }
        Scan(document.MainScript, visited, accumulator);

        return accumulator.ToSettings();
    }

    private static void Scan(
        GcxScript? script,
        ISet<GcxScript> visited,
        FogAccumulator accumulator)
    {
        if (script == null || script.Bytes.Length == 0 || !visited.Add(script))
        {
            return;
        }

        var reader = new Reader(script.Bytes, accumulator);
        reader.Scan();
    }

    private sealed class FogAccumulator
    {
        private readonly Mgs4FogState[] _states =
        [
            Mgs4FogState.Default,
            Mgs4FogState.Default,
            Mgs4FogState.Default
        ];
        private readonly bool[] _configured = new bool[3];

        public void Apply(FogCommand command)
        {
            var near = command.Scalar(NearHash, 0f);
            var far = command.Scalar(FarHash, 10000f);
            if (!float.IsFinite(near)) near = 0f;
            if (!float.IsFinite(far)) far = 10000f;
            if (far <= near + 0.0001f) far = near + 1f;

            var color = command.Rgb(RgbHash, new Vector4(0f, 0f, 0f, 1f));
            var viewport = command.Integer(ViewportHash, -1);
            var (limitMin, limitMax) = command.Limit(LimitHash, 0f, 1f);

            var beforeNear = command.Scalar(BeforeNearHash, near);
            var beforeFar = command.Scalar(BeforeFarHash, far);
            if (!float.IsFinite(beforeNear)) beforeNear = near;
            if (!float.IsFinite(beforeFar) || beforeFar <= beforeNear + 0.0001f)
            {
                beforeFar = MathF.Max(beforeNear + 1f, far);
            }

            var beforeColor = command.Rgb(BeforeRgbHash, color);
            var (beforeLimitMin, beforeLimitMax) = command.Limit(
                BeforeLimitHash,
                limitMin,
                limitMax);

            var state = new Mgs4FogState(
                near,
                far,
                color,
                limitMin,
                limitMax,
                beforeNear,
                beforeFar,
                beforeColor,
                beforeLimitMin,
                beforeLimitMax);

            if (viewport < 0)
            {
                for (var index = 0; index < 3; index++)
                {
                    _states[index] = state;
                    _configured[index] = true;
                }
            }
            else if (viewport <= 2)
            {
                _states[viewport] = state;
                _configured[viewport] = true;
            }
        }

        public SceneFogSettings? ToSettings()
        {
            if (!_configured[0] && !_configured[1] && !_configured[2])
            {
                return null;
            }

            return new SceneFogSettings(
                _states[0],
                _states[1],
                _states[2],
                _configured[0],
                _configured[1],
                _configured[2]);
        }
    }

    private sealed class FogCommand
    {
        private readonly Dictionary<uint, List<double>> _parameters = new();

        public void Add(uint hash, List<double> values)
        {
            // GCX command processing is sequential; the last occurrence wins.
            _parameters[hash] = values;
        }

        public float Scalar(uint hash, float fallback) =>
            TryGet(hash, 1, out var values) && IsFloat(values[0])
                ? (float)values[0]
                : fallback;

        public int Integer(uint hash, int fallback) =>
            TryGet(hash, 1, out var values) &&
            values[0] >= int.MinValue && values[0] <= int.MaxValue
                ? (int)Math.Truncate(values[0])
                : fallback;

        public Vector4 Rgb(uint hash, Vector4 fallback)
        {
            if (!TryGet(hash, 3, out var values) ||
                !IsFloat(values[0]) || !IsFloat(values[1]) || !IsFloat(values[2]))
            {
                return fallback;
            }

            return new Vector4(
                ToPackedByte((float)values[0]) / 255f,
                ToPackedByte((float)values[1]) / 255f,
                ToPackedByte((float)values[2]) / 255f,
                1f);
        }

        public (float Min, float Max) Limit(uint hash, float fallbackMin, float fallbackMax)
        {
            if (!TryGet(hash, 2, out var values) ||
                !IsFloat(values[0]) || !IsFloat(values[1]))
            {
                return (fallbackMin, fallbackMax);
            }

            var min = (float)values[0] / 1000f;
            var max = (float)values[1] / 1000f;
            if (!float.IsFinite(min) || !float.IsFinite(max))
            {
                return (fallbackMin, fallbackMax);
            }

            return (Math.Clamp(min, 0f, 1f), Math.Clamp(max, 0f, 1f));
        }

        private bool TryGet(uint hash, int minimumCount, out List<double> values)
        {
            if (_parameters.TryGetValue(hash, out var found) && found.Count >= minimumCount)
            {
                values = found;
                return true;
            }

            values = [];
            return false;
        }

        private static bool IsFloat(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) &&
            value >= -float.MaxValue && value <= float.MaxValue;

        private static float ToPackedByte(float component)
        {
            var scaled = MathF.Truncate(component * 255f / 1000f);
            return Math.Clamp(scaled, 0f, 255f);
        }
    }

    private readonly record struct TokenValue(double Number, uint Hash, TokenKind Kind)
    {
        public static TokenValue None => new(0, 0, TokenKind.None);
        public static TokenValue Numeric(double value) => new(value, 0, TokenKind.Number);
        public static TokenValue StringCode(uint hash) => new(0, hash, TokenKind.Hash);
    }

    private enum TokenKind
    {
        None,
        Number,
        Hash
    }

    private sealed class Reader
    {
        private readonly byte[] _bytes;
        private readonly FogAccumulator _accumulator;
        private int _position;
        private bool _invalid;

        public Reader(byte[] bytes, FogAccumulator accumulator)
        {
            _bytes = bytes;
            _accumulator = accumulator;
        }

        public void Scan()
        {
            while (!_invalid && _position < _bytes.Length)
            {
                var before = _position;
                ReadToken(_bytes.Length, null, null);
                if (_position <= before)
                {
                    _position = before + 1;
                }
            }
        }

        private TokenValue ReadToken(
            int limit,
            FogCommand? currentCommand,
            List<double>? numericValues)
        {
            if (_invalid || _position >= limit || _position >= _bytes.Length)
            {
                return TokenValue.None;
            }

            var tag = _bytes[_position] & 0xF0;
            if (tag is 0xC0 or 0xD0 or 0xE0 or 0xF0)
            {
                var value = (ReadByte() & 0x3F) - 1;
                numericValues?.Add(value);
                return TokenValue.Numeric(value);
            }

            if (tag == 0x90)
            {
                ReadByte();
                return TokenValue.None;
            }
            if (tag == 0x40)
            {
                var value = ReadByte() & 0x0F;
                if (value == 0x0F) ReadByte();
                return TokenValue.None;
            }
            if (tag is 0x10 or 0x20)
            {
                ReadUInt32();
                return TokenValue.None;
            }
            if (tag == 0x30)
            {
                SkipSizedContainer(limit);
                return TokenValue.None;
            }
            if (tag == 0x50)
            {
                ReadParameter(limit, currentCommand);
                return TokenValue.None;
            }
            if (tag == 0x60)
            {
                ReadCommand(limit);
                return TokenValue.None;
            }
            if (tag is 0x70 or 0x80)
            {
                ReadSizedContainer(limit, currentCommand);
                return TokenValue.None;
            }

            var type = ReadByte();
            TokenValue token;
            switch (type)
            {
                case 0x00:
                    token = TokenValue.None;
                    break;
                case 0x01:
                    token = TokenValue.Numeric(ReadInt16());
                    break;
                case 0x02:
                    token = TokenValue.Numeric(ReadSByte());
                    break;
                case 0x03:
                case 0x04:
                    token = TokenValue.Numeric(ReadByte());
                    break;
                case 0x06:
                    token = TokenValue.StringCode(ReadUInt24());
                    break;
                case 0x07:
                {
                    var length = ReadByte();
                    Skip(length);
                    token = TokenValue.None;
                    break;
                }
                case 0x08:
                    token = TokenValue.Numeric(ReadUInt16());
                    break;
                case 0x09:
                    token = TokenValue.Numeric(ReadInt32());
                    break;
                case 0x0A:
                    token = TokenValue.Numeric(ReadUInt32());
                    break;
                case 0x0D:
                    ReadUInt24();
                    ReadByte();
                    token = TokenValue.None;
                    break;
                case 0x0E:
                    ReadUInt16();
                    token = TokenValue.None;
                    break;
                default:
                    // Unknown primitive: stop this script instead of guessing its size.
                    _invalid = true;
                    token = TokenValue.None;
                    break;
            }

            if (token.Kind == TokenKind.Number)
            {
                numericValues?.Add(token.Number);
            }
            return token;
        }

        private void ReadCommand(int outerLimit)
        {
            var size = ReadSize();
            var end = ClampEnd(size, outerLimit);
            if (_invalid || end <= _position)
            {
                _position = Math.Max(_position, end);
                return;
            }

            ReadUInt24(); // command packet prefix / call-site hash
            var headerLength = ReadShortSize();
            var headerEnd = Math.Min(end, SafeAdd(_position, headerLength));
            uint commandHash = 0;
            while (!_invalid && _position < headerEnd)
            {
                var before = _position;
                var token = ReadToken(headerEnd, null, null);
                if (commandHash == 0 && token.Kind == TokenKind.Hash)
                {
                    commandHash = token.Hash;
                }
                if (_position <= before) _position = before + 1;
            }
            _position = headerEnd;

            var fogCommand = commandHash == NewFogSetHash ? new FogCommand() : null;
            while (!_invalid && _position < end)
            {
                var before = _position;
                ReadToken(end, fogCommand, null);
                if (_position <= before) _position = before + 1;
            }
            _position = end;

            if (fogCommand != null)
            {
                _accumulator.Apply(fogCommand);
            }
        }

        private void ReadParameter(int outerLimit, FogCommand? command)
        {
            var size = ReadSize();
            var end = ClampEnd(size, outerLimit);
            if (_invalid || end <= _position)
            {
                _position = Math.Max(_position, end);
                return;
            }

            ReadByte(); // one-letter parameter discriminator
            var hash = ReadUInt24();
            var values = new List<double>(3);
            while (!_invalid && _position < end)
            {
                var before = _position;
                ReadToken(end, command, values);
                if (_position <= before) _position = before + 1;
            }
            _position = end;
            command?.Add(hash, values);
        }

        private void ReadSizedContainer(int outerLimit, FogCommand? command)
        {
            var size = ReadSize();
            var end = ClampEnd(size, outerLimit);
            while (!_invalid && _position < end)
            {
                var before = _position;
                ReadToken(end, command, null);
                if (_position <= before) _position = before + 1;
            }
            _position = end;
        }

        private void SkipSizedContainer(int outerLimit)
        {
            var size = ReadSize();
            _position = ClampEnd(size, outerLimit);
        }

        private int ReadSize()
        {
            var value = ReadByte();
            var size = value & 0x0F;
            if (size == 0x0D) size = ReadByte();
            else if (size == 0x0E) size = ReadUInt16();
            return size;
        }

        private int ReadShortSize()
        {
            var value = ReadByte();
            if (value <= 0x7F)
            {
                return value;
            }
            return ((value << 8) | ReadByte()) & 0x7FFF;
        }

        private int ClampEnd(int payloadLength, int outerLimit)
        {
            if (payloadLength < 0)
            {
                _invalid = true;
                return _position;
            }
            return Math.Min(Math.Min(_bytes.Length, outerLimit), SafeAdd(_position, payloadLength));
        }

        private static int SafeAdd(int left, int right) =>
            right > int.MaxValue - left ? int.MaxValue : left + right;

        private byte ReadByte()
        {
            if (_position >= _bytes.Length)
            {
                _invalid = true;
                return 0;
            }
            return _bytes[_position++];
        }

        private sbyte ReadSByte() => unchecked((sbyte)ReadByte());

        private ushort ReadUInt16()
        {
            var b0 = ReadByte();
            var b1 = ReadByte();
            return (ushort)(b0 | (b1 << 8));
        }

        private short ReadInt16() => unchecked((short)ReadUInt16());

        private uint ReadUInt24()
        {
            var b0 = ReadByte();
            var b1 = ReadByte();
            var b2 = ReadByte();
            return (uint)(b0 | (b1 << 8) | (b2 << 16));
        }

        private uint ReadUInt32()
        {
            var b0 = ReadByte();
            var b1 = ReadByte();
            var b2 = ReadByte();
            var b3 = ReadByte();
            return (uint)(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
        }

        private int ReadInt32() => unchecked((int)ReadUInt32());

        private void Skip(int count)
        {
            if (count < 0 || count > _bytes.Length - _position)
            {
                _position = _bytes.Length;
                _invalid = true;
                return;
            }
            _position += count;
        }
    }
}
