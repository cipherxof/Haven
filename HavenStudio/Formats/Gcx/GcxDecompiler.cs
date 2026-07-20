using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HavenStudio.Utils;

namespace HavenStudio.Formats.Gcx;

public static class GcxDecompiler
{
    public static string Decompile(
        byte[] bytes,
        string procName,
        bool isMgs3 = false,
        ICollection<GcxPlacementSite>? placementSites = null)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return string.Empty;
        }

        var decompiler = new GcxDecompilerCore(bytes, procName, isMgs3, placementSites);
        return decompiler.Decompile();
    }

    private sealed class GcxDecompilerCore
    {
        private readonly ReadOnlyMemory<byte> _buffer;
        private readonly StringBuilder _mainBuilder = new();
        private readonly IndentationManager _indentation = new();
        private readonly string _procName;
        private readonly bool _isMgs3;
        private readonly ICollection<GcxPlacementSite>? _placementSites;
        private readonly Stack<CommandSiteBuilder> _commandSites = new();
        private StringBuilder _activeBuilder;
        private ParameterSiteBuilder? _activeParameterSite;
        private uint? _capturedHeaderCommandHash;
        private bool _capturingCommandHeader;
        private byte _lastStrCodeLetter;
        private int _ptr;
        private bool _isInline;
        private bool _isFirstRun = true;
        private bool _hasError;

        public GcxDecompilerCore(
            byte[] bytes,
            string procName,
            bool isMgs3,
            ICollection<GcxPlacementSite>? placementSites)
        {
            _buffer = bytes;
            _procName = procName;
            _isMgs3 = isMgs3;
            _placementSites = placementSites;
            _activeBuilder = _mainBuilder;
        }

        public string Decompile()
        {
            Process();
            PrintNewLine();
            return _mainBuilder.ToString().TrimEnd();
        }

        private void PrintNewLine()
        {
            if (_isInline)
            {
                return;
            }

            _activeBuilder.Append('\n');
        }

        private void PrintProcSig()
        {
            if (_isFirstRun)
            {
                PrintProcName();
                _isFirstRun = false;
            }
        }

        private void PrintProcName()
        {
            _activeBuilder.Append(_procName);
            _activeBuilder.Append(' ');
        }

        private void OpenBrace()
        {
            _activeBuilder.Append('{');
            PrintNewLine();
            if (!_isInline)
            {
                _indentation.Indent();
            }
        }

        private void CloseBrace()
        {
            if (!_isInline)
            {
                _indentation.Unindent();
            }

            if (!_isInline)
            {
                _indentation.WriteIndent(_activeBuilder);
            }

            _activeBuilder.Append('}');
        }

        private void ReadLetter()
        {
            if (!TryReadByte(out var value))
            {
                return;
            }

            _activeBuilder.Append(value);
        }

        private void ReadCmdHeader()
        {
            int size = GetShortSize();
            int start = _ptr;

            _capturedHeaderCommandHash = null;
            _capturingCommandHeader = true;
            try
            {
                ProcessFor(start, size);
            }
            finally
            {
                _capturingCommandHeader = false;
            }
            _activeBuilder.Append(" \\");
            PrintNewLine();
        }

        private void CaptureValues(List<string> exprStack)
        {
            var previousBuilder = _activeBuilder;
            var capture = new StringBuilder();

            try
            {
                _activeBuilder = capture;
                Process();
            }
            finally
            {
                _activeBuilder = previousBuilder;
            }

            exprStack.Add(capture.ToString());
        }

        private void CalcExpr(List<string> exprStack)
        {
            if (!TryReadByte(out var opByte))
            {
                return;
            }

            byte op = (byte)(opByte & 0x1F);
            if (op == 0 || exprStack.Count < 2)
            {
                return;
            }

            string value1 = exprStack[^1];
            exprStack.RemoveAt(exprStack.Count - 1);
            string value2 = exprStack[^1];
            exprStack.RemoveAt(exprStack.Count - 1);

            var previousBuilder = _activeBuilder;
            var capture = new StringBuilder();

            try
            {
                _activeBuilder = capture;
                if (op == 0x16)
                {
                    _indentation.WriteIndent(_activeBuilder);
                }

                _activeBuilder.Append('(');
                ProcessExpr(op, value1, value2);
                _activeBuilder.Append(')');

                if (op == 0x16)
                {
                    _activeBuilder.Append('\n');
                }
            }
            finally
            {
                _activeBuilder = previousBuilder;
            }

            exprStack.Add(capture.ToString());
        }

        private void ReadUShort()
        {
            if (TryReadUInt16(out var value))
            {
                _activeBuilder.Append(value);
                RecordValueToken();
            }
        }

        private void ReadByte()
        {
            if (TryReadSByte(out var value))
            {
                _activeBuilder.Append(value);
                RecordValueToken();
            }
        }

        private void ReadUByte()
        {
            if (TryReadByte(out var value))
            {
                _activeBuilder.Append(value);
                RecordValueToken();
            }
        }

        private uint ReadStrCode(bool hasLetter = false)
        {
            byte letter = 0;
            if (hasLetter)
            {
                if (!TryReadByte(out letter))
                {
                    return 0;
                }
            }
            _lastStrCodeLetter = letter;

            var valueOffset = _ptr;
            if (!TryReadUInt24(out uint strcode))
            {
                return 0;
            }

            if (_capturingCommandHeader && !hasLetter && _capturedHeaderCommandHash == null)
            {
                _capturedHeaderCommandHash = strcode;
            }
            else if (!hasLetter && _activeParameterSite != null)
            {
                var value = new RecordedStringCode(valueOffset, strcode);
                _activeParameterSite.StringCodes.Add(value);
                _activeParameterSite.ValueTokens.Add(new RecordedValueToken(value));
            }

            if (hasLetter && TryGetKnownParameterName(letter, strcode, out var parameterName))
            {
                _activeBuilder.Append(parameterName);
                return strcode;
            }

            string hashName = DictionaryFile.GetHashString(strcode);
            if (!hashName.Equals(strcode.ToString("X4"), StringComparison.OrdinalIgnoreCase))
            {
                _activeBuilder.Append(hashName);
                return strcode;
            }

            string? commandName = CommandFile.GetCommandName(strcode);
            if (commandName != null)
            {
                _activeBuilder.Append(commandName);
                return strcode;
            }

            if (!hasLetter && GcxPlacementCommandCatalog.TryGet(strcode, out var placementCommand))
            {
                _activeBuilder.Append(placementCommand.Name);
                return strcode;
            }

            if (hasLetter)
            {
                if (letter >= 32 && letter <= 126)
                {
                    _activeBuilder.Append((char)letter);
                }
                else
                {
                    _activeBuilder.Append(letter);
                }
            }

            _activeBuilder.Append('[');
            _activeBuilder.Append(strcode.ToString("X6"));
            _activeBuilder.Append(']');
            return strcode;
        }

        private static bool TryGetKnownParameterName(byte letter, uint hash, out string name)
        {
            name = (letter, hash) switch
            {
                ((byte)'m', 0x091D13) => "model",
                ((byte)'p', 0x01CE53) => "pos",
                ((byte)'d', 0x019D92) => "dir",
                ((byte)'e', 0x01A134) => "eft",
                ((byte)'r', 0x849B36) => "ref",
                ((byte)'c', 0x31C62D) => "collision",
                _ => string.Empty
            };
            return name.Length != 0;
        }

        private void ReadString()
        {
            if (!TryReadByte(out var length))
            {
                return;
            }

            int actualLength = Math.Min(length, _buffer.Length - _ptr);
            if (actualLength <= 0)
            {
                _activeBuilder.Append("\"\"");
                return;
            }

            string str = Encoding.Latin1.GetString(_buffer.Span.Slice(_ptr, actualLength));
            _ptr += actualLength;
            if (str.Length > 0)
            {
                str = str.Substring(0, str.Length - 1);
            }

            _activeBuilder.Append('"');
            _activeBuilder.Append(str);
            _activeBuilder.Append('"');
            RecordValueToken();
        }

        private void ReadArray()
        {
            ReadStrCode();
            if (!TryReadByte(out var ap))
            {
                return;
            }

            _activeBuilder.Append('[');
            _activeBuilder.Append(ap);
            _activeBuilder.Append(']');
        }

        private void ReadShort()
        {
            int offset = _ptr - 1;
            if (TryReadInt16(out var value))
            {
                _activeBuilder.Append(value);
                RecordLiteral(offset, 3, GcxLiteralEncoding.Int16, value);
            }
        }

        private void ReadLong()
        {
            int offset = _ptr - 1;
            if (TryReadInt32(out var value))
            {
                _activeBuilder.Append(value);
                RecordLiteral(offset, 5, GcxLiteralEncoding.Int32, value);
            }
        }

        private void ReadULong()
        {
            if (TryReadUInt32(out var value))
            {
                _activeBuilder.Append(value);
                RecordValueToken();
            }
        }

        private void ReadStrRes()
        {
            _activeBuilder.Append("$strres:");
            if (TryReadUInt16(out var value))
            {
                _activeBuilder.Append(value);
                RecordValueToken();
            }
        }

        private void ReadEnd()
        {
        }

        private void ReadIf(int start, int size, bool isParam = false, bool isElse = false)
        {
            if (!isParam)
            {
                size = GetShortSize();
                start = _ptr;
            }

            if (!isElse)
            {
                _activeBuilder.Append(' ');
                ReadExpr();
            }

            _activeBuilder.Append(' ');
            OpenBrace();
            ProcessFor(start, size);
            CloseBrace();
        }

        private void ReadNum()
        {
            int offset = _ptr;
            if (!TryReadByte(out var value))
            {
                return;
            }

            int output = (value & 0x3F) - 1;
            _activeBuilder.Append(output);
            RecordLiteral(offset, 1, GcxLiteralEncoding.PackedNumber, output);
        }

        private void ReadLocal()
        {
            if (!TryReadByte(out var lclArg))
            {
                return;
            }

            lclArg = (byte)(lclArg & 0x0F);
            _activeBuilder.Append("$lclArg");
            _activeBuilder.Append(lclArg);
            RecordValueToken();
        }

        private void ReadProc()
        {
            int size = GetSize();
            int start = _ptr;

            if (!_isInline)
            {
                _indentation.WriteIndent(_activeBuilder);
            }

            _activeBuilder.Append("proc ");
            PrintProcSig();
            OpenBrace();
            ProcessFor(start, size);
            CloseBrace();
            PrintNewLine();
        }

        private void ReadEval()
        {
            int size = GetSize();
            int start = _ptr;

            if (!_isInline)
            {
                _indentation.WriteIndent(_activeBuilder);
            }

            _activeBuilder.Append("@proc");
            ReadShort();
            ProcessFor(start, size);
            PrintNewLine();
        }

        private void ReadCmd()
        {
            int commandOffset = _ptr;
            int size = GetSize();
            int start = _ptr;

            if (!_isInline)
            {
                _indentation.WriteIndent(_activeBuilder);
            }

            uint strcode = ReadStrCode();
            if (strcode == 0xD86)
            {
                ReadIf(start, size);
            }
            else
            {
                CommandSiteBuilder? commandSite = null;
                if (_placementSites != null && !_isMgs3)
                {
                    commandSite = new CommandSiteBuilder(
                        commandOffset,
                        Math.Max(0, Math.Min(_buffer.Length, start + size) - commandOffset),
                        isNested: _commandSites.Count > 0);
                    _commandSites.Push(commandSite);
                }

                try
                {
                    ReadCmdHeader();
                    if (commandSite != null)
                    {
                        commandSite.CommandHash = _capturedHeaderCommandHash;
                    }
                    _indentation.Indent();
                    ProcessFor(start, size);
                    _indentation.Unindent();
                }
                finally
                {
                    if (commandSite != null)
                    {
                        if (_commandSites.Count > 0 && ReferenceEquals(_commandSites.Peek(), commandSite))
                        {
                            _commandSites.Pop();
                        }
                        PublishPlacementSite(commandSite);
                    }
                }
            }

            PrintNewLine();
        }

        private void Mgs3Param()
        {
            int size = GetSize();
            int start = _ptr;

            ReadLetter();
            ProcessFor(start, size);
        }

        private void Mgs4Param()
        {
            int parameterOffset = _ptr;
            int size = GetSize();
            int start = _ptr;

            uint strcode = ReadStrCode(true);
            var previousParameterSite = _activeParameterSite;
            ParameterSiteBuilder? parameterSite = null;
            if (_commandSites.Count > 0)
            {
                parameterSite = new ParameterSiteBuilder(
                    _lastStrCodeLetter,
                    strcode,
                    parameterOffset,
                    Math.Max(0, Math.Min(_buffer.Length, start + size) - parameterOffset),
                    start);
                _commandSites.Peek().Parameters.Add(parameterSite);
                _activeParameterSite = parameterSite;
            }
            try
            {
                if (strcode == 0x69 && GetTag() == 0x30)
                {
                    ReadIf(start, size, isParam: true);
                }
                else if (strcode == 0x65)
                {
                    ReadIf(start, size, isParam: true, isElse: true);
                }
                else
                {
                    ProcessFor(start, size);
                }
            }
            finally
            {
                _activeParameterSite = previousParameterSite;
            }
        }

        private void ReadParam()
        {
            if (!_isInline)
            {
                _indentation.WriteIndent(_activeBuilder);
            }

            _activeBuilder.Append('-');
            if (_isMgs3)
            {
                Mgs3Param();
            }
            else
            {
                Mgs4Param();
            }

            if (_ptr < _buffer.Length && _buffer.Span[_ptr] != 0x00)
            {
                _activeBuilder.Append(" \\");
            }

            PrintNewLine();
        }

        private void ReadArgs()
        {
            if (!TryReadByte(out var argno))
            {
                return;
            }

            argno = (byte)(argno & 0x0F);
            if (argno == 0x0F)
            {
                if (!TryReadByte(out var next))
                {
                    return;
                }

                argno += next;
            }

            _activeBuilder.Append("$arg");
            _activeBuilder.Append(argno);
            if (_activeParameterSite != null)
            {
                _activeParameterSite.Arguments.Add(argno);
                _activeParameterSite.ValueTokens.Add(default);
            }
        }

        private void ReadExpr()
        {
            int size = GetSize();
            int start = _ptr;
            int length = start + size;
            var exprStack = new List<string>();

            _isInline = true;
            while (_ptr < length && _ptr < _buffer.Length)
            {
                byte type = _buffer.Span[_ptr];
                if ((type & 0xE0) != 0xA0)
                {
                    CaptureValues(exprStack);
                }
                else
                {
                    CalcExpr(exprStack);
                }
            }

            _isInline = false;
            if (exprStack.Count > 0)
            {
                _activeBuilder.Append(exprStack[0]);
            }
        }

        private void ReadVar()
        {
            if (!TryReadUInt32(out var raw))
            {
                return;
            }

            uint varcode = BinaryPrimitives.ReverseEndianness(raw);
            byte tag = (byte)((varcode & 0xF0000000) >> 24);
            uint region = varcode & 0xF00000;
            string bufp = "varbuf";
            if (region == 0x800000)
            {
                bufp = "linkvarbuf";
            }
            else if (region == 0x100000)
            {
                bufp = "localvarbuf";
            }

            uint offset = varcode & 0xFFFF;
            _activeBuilder.Append("$var:");
            _activeBuilder.Append(bufp);
            _activeBuilder.Append('[');
            _activeBuilder.Append(offset.ToString("X"));
            _activeBuilder.Append(']');

            if (tag == 0x20)
            {
                _activeBuilder.Append('[');
                Process();
                _activeBuilder.Append(']');
                _activeBuilder.Append('[');
                Process();
                _activeBuilder.Append(']');
            }
            RecordValueToken();
        }

        private void ProcessExpr(byte op, string value1, string value2)
        {
            switch (op)
            {
                case 0x00: break;
                case 0x01: _activeBuilder.Append("-"); _activeBuilder.Append(value1); break;
                case 0x02: _activeBuilder.Append("!"); _activeBuilder.Append(value1); break;
                case 0x03: _activeBuilder.Append("~"); _activeBuilder.Append(value1); break;
                case 0x04: _activeBuilder.Append(value2); _activeBuilder.Append(" + "); _activeBuilder.Append(value1); break;
                case 0x05: _activeBuilder.Append(value2); _activeBuilder.Append(" - "); _activeBuilder.Append(value1); break;
                case 0x06: _activeBuilder.Append(value2); _activeBuilder.Append(" * "); _activeBuilder.Append(value1); break;
                case 0x07: _activeBuilder.Append(value2); _activeBuilder.Append(" / "); _activeBuilder.Append(value1); break;
                case 0x08: _activeBuilder.Append(value2); _activeBuilder.Append(" % "); _activeBuilder.Append(value1); break;
                case 0x09: _activeBuilder.Append(value2); _activeBuilder.Append(" << "); _activeBuilder.Append(value1); break;
                case 0x0A: _activeBuilder.Append(value2); _activeBuilder.Append(" >> "); _activeBuilder.Append(value1); break;
                case 0x0B: _activeBuilder.Append(value2); _activeBuilder.Append(" == "); _activeBuilder.Append(value1); break;
                case 0x0C: _activeBuilder.Append(value2); _activeBuilder.Append(" != "); _activeBuilder.Append(value1); break;
                case 0x0D: _activeBuilder.Append(value2); _activeBuilder.Append(" < "); _activeBuilder.Append(value1); break;
                case 0x0E: _activeBuilder.Append(value2); _activeBuilder.Append(" <= "); _activeBuilder.Append(value1); break;
                case 0x0F: _activeBuilder.Append(value2); _activeBuilder.Append(" > "); _activeBuilder.Append(value1); break;
                case 0x10: _activeBuilder.Append(value2); _activeBuilder.Append(" >= "); _activeBuilder.Append(value1); break;
                case 0x11: _activeBuilder.Append(value2); _activeBuilder.Append(" | "); _activeBuilder.Append(value1); break;
                case 0x12: _activeBuilder.Append(value2); _activeBuilder.Append(" & "); _activeBuilder.Append(value1); break;
                case 0x13: _activeBuilder.Append(value2); _activeBuilder.Append(" ^ "); _activeBuilder.Append(value1); break;
                case 0x14: _activeBuilder.Append(value2); _activeBuilder.Append(" || "); _activeBuilder.Append(value1); break;
                case 0x15: _activeBuilder.Append(value2); _activeBuilder.Append(" && "); _activeBuilder.Append(value1); break;
                case 0x16: _activeBuilder.Append(value2); _activeBuilder.Append(" = "); _activeBuilder.Append(value1); break;
            }
        }

        private void ProcessType()
        {
            if (!TryReadByte(out var type))
            {
                return;
            }

            switch (type)
            {
                case 0x01: ReadShort(); break;
                case 0x02: ReadUByte(); break;
                case 0x03: ReadUByte(); break;
                case 0x04: ReadUByte(); break;
                case 0x06: ReadStrCode(); break;
                case 0x07: ReadString(); break;
                case 0x08: ReadUShort(); break;
                case 0x09: ReadLong(); break;
                case 0x0A: ReadULong(); break;
                case 0x0D: ReadArray(); break;
                case 0x0E: ReadStrRes(); break;
                case 0x00: ReadEnd(); break;
            }
        }

        private void ProcessTag(byte tag)
        {
            switch (tag)
            {
                case 0xF0:
                case 0xE0:
                case 0xD0:
                case 0xC0: ReadNum(); break;
                case 0x90: ReadLocal(); break;
                case 0x80: ReadProc(); break;
                case 0x70: ReadEval(); break;
                case 0x60: ReadCmd(); break;
                case 0x50: ReadParam(); break;
                case 0x40: ReadArgs(); break;
                case 0x30: ReadExpr(); break;
                case 0x20: ReadVar(); break;
                case 0x10: ReadVar(); break;
                default:
                    // Unknown tag - skip byte to prevent infinite loop
                    _ptr++;
                    _activeBuilder.Append($"[?{tag:X2}]");
                    break;
            }
        }

        private void ProcessSize(ref uint size)
        {
            switch (size)
            {
                case 0x0D:
                    if (TryReadByte(out var shortSize))
                    {
                        size = shortSize;
                    }
                    break;
                case 0x0E:
                    if (TryReadUInt16(out var longSize))
                    {
                        size = longSize;
                    }
                    break;
            }
        }

        private void ProcessFor(int start, int size)
        {
            int length = start + size;
            while (_ptr < length && _ptr < _buffer.Length)
            {
                _activeBuilder.Append(' ');
                var parameterSite = _activeParameterSite;
                var tokenIndex = parameterSite?.ValueTokens.Count ?? -1;
                var tokenOffset = _ptr;
                Process();
                if (parameterSite != null &&
                    ReferenceEquals(parameterSite, _activeParameterSite) &&
                    parameterSite.ValueTokens.Count == tokenIndex + 1)
                {
                    var token = parameterSite.ValueTokens[tokenIndex];
                    parameterSite.ValueTokens[tokenIndex] = token with
                    {
                        Offset = tokenOffset,
                        Length = _ptr - tokenOffset
                    };
                }
                if (_hasError)
                {
                    return;
                }
            }
        }

        private int GetShortSize()
        {
            if (!TryReadByte(out var size))
            {
                return 0;
            }

            int result = size & 0xFF;
            if (result > 0x7F)
            {
                if (!TryReadByte(out var size2))
                {
                    return result;
                }

                result = (result << 8) | size2;
                result &= 0x7FFF;
            }

            return result;
        }

        private int GetSize()
        {
            if (!TryReadByte(out var sizeByte))
            {
                return 0;
            }

            uint size = (uint)(sizeByte & 0x0F);
            ProcessSize(ref size);
            return (int)size;
        }

        private byte GetTag()
        {
            if (_ptr >= _buffer.Length)
            {
                return 0;
            }

            return (byte)(_buffer.Span[_ptr] & 0xF0);
        }

        private void Process()
        {
            byte tag = GetTag();
            if (tag != 0)
            {
                ProcessTag(tag);
                return;
            }

            ProcessType();
        }

        private bool TryReadByte(out byte value)
        {
            if (_ptr >= _buffer.Length)
            {
                value = 0;
                SetError();
                return false;
            }

            value = _buffer.Span[_ptr++];
            return true;
        }

        private bool TryReadSByte(out sbyte value)
        {
            if (!TryReadByte(out var raw))
            {
                value = 0;
                return false;
            }

            value = unchecked((sbyte)raw);
            return true;
        }

        private bool TryReadUInt16(out ushort value)
        {
            if (_ptr + 2 > _buffer.Length)
            {
                value = 0;
                SetError();
                return false;
            }

            value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Span.Slice(_ptr, 2));
            _ptr += 2;
            return true;
        }

        private bool TryReadInt16(out short value)
        {
            if (!TryReadUInt16(out var raw))
            {
                value = 0;
                return false;
            }

            value = unchecked((short)raw);
            return true;
        }

        private bool TryReadUInt24(out uint value)
        {
            if (_ptr + 3 > _buffer.Length)
            {
                value = 0;
                SetError();
                return false;
            }

            uint b0 = _buffer.Span[_ptr++];
            uint b1 = _buffer.Span[_ptr++];
            uint b2 = _buffer.Span[_ptr++];
            value = b0 | (b1 << 8) | (b2 << 16);
            return true;
        }

        private bool TryReadUInt32(out uint value)
        {
            if (_ptr + 4 > _buffer.Length)
            {
                value = 0;
                SetError();
                return false;
            }

            value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Span.Slice(_ptr, 4));
            _ptr += 4;
            return true;
        }

        private bool TryReadInt32(out int value)
        {
            if (!TryReadUInt32(out var raw))
            {
                value = 0;
                return false;
            }

            value = unchecked((int)raw);
            return true;
        }

        private void RecordLiteral(int offset, int width, GcxLiteralEncoding encoding, int value)
        {
            if (_activeParameterSite == null)
            {
                return;
            }
            _activeParameterSite.Literals.Add(new GcxLiteralSite(offset, width, encoding, value));
            _activeParameterSite.ValueTokens.Add(default);
        }

        private void RecordValueToken()
        {
            _activeParameterSite?.ValueTokens.Add(default);
        }

        private void PublishPlacementSite(CommandSiteBuilder builder)
        {
            if (_placementSites == null ||
                builder.CommandHash is not { } commandHash ||
                !GcxPlacementCommandCatalog.TryGet(commandHash, out var definition))
            {
                return;
            }

            var position = builder.GetVector((byte)'p', 0x01CE53);
            var direction = builder.GetVector((byte)'d', 0x019D92);
            var model = builder.GetStringCodeSite((byte)'m', 0x091D13);
            var modelHash = model?.Value;
            var modelArgument = builder.GetArgument((byte)'m', 0x091D13);
            var effect = builder.GetStringCodeSite((byte)'e', 0x01A134);
            var effectHash = effect?.Value ?? builder.GetStringCode((byte)'e', 0x01A134);
            var effectArgument = builder.GetArgument((byte)'e', 0x01A134);
            var propertyPositionParameterHash = HavenStudio.Utils.String.HashString("prop_pos");
            var propertyParameterHash = HavenStudio.Utils.String.HashString("prop");
            var propertyPosition = builder.GetStringCodeSite(propertyPositionParameterHash) ??
                builder.GetLastStringCodeSite(propertyParameterHash);
            var propertyArgument = builder.GetArgument(propertyPositionParameterHash) ??
                builder.GetLastArgument(propertyParameterHash);
            var collisionReference = builder.GetStringCodeSite((byte)'r', 0x849B36) ??
                builder.GetStringCodeSite((byte)'b', 0x30BDB7) ??
                (commandHash == 0x656B68
                    ? builder.GetStringCodeSite(
                        (byte)'c',
                        HavenStudio.Utils.String.HashString("collision"))
                    : null);
            var collisionArgument = builder.GetArgument((byte)'r', 0x849B36) ??
                builder.GetArgument((byte)'b', 0x30BDB7) ??
                (commandHash == 0x656B68
                    ? builder.GetArgument(
                        (byte)'c',
                        HavenStudio.Utils.String.HashString("collision"))
                    : null);
            var propertyPositionHash = propertyPosition?.Value;
            var hasEffectParameter = builder.HasParameter((byte)'e', 0x01A134);
            var transformArgument = hasEffectParameter ? effectArgument : propertyArgument;
            var foreachContext = GetForeachContext(
                modelArgument,
                transformArgument,
                collisionArgument);
            if (!definition.IsModelPlacement &&
                position == null &&
                direction == null &&
                !hasEffectParameter)
            {
                return;
            }

            bool hasLiteralPosition = position?.HasThreeLiteralComponents == true;
            bool editable = definition.IsModelPlacement &&
                definition.SupportsCommandReencoding &&
                !builder.IsNested &&
                hasLiteralPosition;
            string? readOnlyReason = editable
                ? null
                : !definition.IsModelPlacement
                    ? "This spatial command does not place a model."
                    : builder.IsNested
                        ? "Nested and foreach placements are read-only."
                        : !hasLiteralPosition
                            ? "The placement position is not three direct numeric literals."
                            : "This placement command cannot be safely re-encoded yet.";

            _placementSites.Add(new GcxPlacementSite
            {
                CommandHash = commandHash,
                CommandName = definition.Name,
                CommandOffset = builder.CommandOffset,
                CommandLength = builder.CommandLength,
                ModelHash = modelHash,
                EffectHash = effectHash,
                CollisionReferenceHash = collisionReference?.Value,
                PropertyPositionHash = propertyPositionHash,
                Position = position,
                Direction = direction,
                Model = model,
                Effect = effect,
                PropertyPosition = propertyPosition,
                ForeachModelSites = foreachContext.ModelSites,
                ForeachTransformSites = foreachContext.TransformSites,
                ForeachCollisionReferenceSites = foreachContext.CollisionReferenceSites,
                ForeachRowCount = foreachContext.RowCount,
                Foreach = foreachContext.Site,
                CollisionReference = collisionReference,
                IsNested = builder.IsNested,
                IsModelPlacement = definition.IsModelPlacement,
                Editable = editable,
                ModelHashEditable = definition.IsModelPlacement && model != null,
                CollisionReferenceEditable = definition.IsModelPlacement &&
                    (collisionReference != null ||
                     definition.SupportsCommandReencoding && !builder.IsNested),
                ReadOnlyReason = readOnlyReason
            });
        }

        private ForeachContext GetForeachContext(
            int? modelArgument,
            int? transformArgument,
            int? collisionReferenceArgument)
        {
            const int MinimumArgumentIndex = 1;
            var argumentIndex = modelArgument ?? transformArgument ?? collisionReferenceArgument ?? 0;

            var dataHash = HavenStudio.Utils.String.HashString("data");
            var argumentCountHash = HavenStudio.Utils.String.HashString("argc");
            var repeatHash = HavenStudio.Utils.String.HashString("repeat");
            foreach (var parent in _commandSites)
            {
                var data = parent.Parameters.FirstOrDefault(parameter => parameter.Hash == dataHash);
                var argumentCount = parent.Parameters
                    .FirstOrDefault(parameter => parameter.Hash == argumentCountHash)?
                    .Literals.FirstOrDefault()?.Value ?? 0;
                if (data == null || argumentCount <= 0 || argumentIndex > argumentCount)
                {
                    continue;
                }

                var rowCount = data.ValueTokens.Count / argumentCount;
                var modelSites = BuildDataSites(modelArgument);
                var transformSites = BuildDataSites(transformArgument);
                var collisionReferenceSites = BuildDataSites(collisionReferenceArgument);
                GcxStringCodeSite?[] BuildDataSites(int? requestedArgument)
                {
                    var sites = new GcxStringCodeSite?[rowCount];
                    var requestedIndex = requestedArgument.GetValueOrDefault();
                    for (var row = 0;
                         requestedIndex >= MinimumArgumentIndex && requestedIndex <= argumentCount && row < rowCount;
                         row++)
                    {
                        var token = data.ValueTokens[row * argumentCount + requestedIndex - 1];
                        if (token.StringCode is not { } value)
                        {
                            continue;
                        }
                        sites[row] = new GcxStringCodeSite(
                            data.ParameterOffset,
                            data.ParameterLength,
                            value.Offset,
                            value.Value);
                    }
                    return sites;
                }
                var repeatParameter = parent.Parameters.FirstOrDefault(parameter => parameter.Hash == repeatHash);
                var repeat = repeatParameter?.Literals.FirstOrDefault();
                GcxForeachSite? site = null;
                if (repeatParameter != null && repeat != null)
                {
                    var rows = new List<GcxForeachRowSite>(rowCount);
                    for (var row = 0; row < rowCount; row++)
                    {
                        var rowTokens = data.ValueTokens
                            .Skip(row * argumentCount)
                            .Take(argumentCount)
                            .ToArray();
                        if (rowTokens.Length != argumentCount ||
                            rowTokens.Any(token => token.Offset < 0 || token.Length <= 0))
                        {
                            rows.Clear();
                            break;
                        }

                        var rowOffset = rowTokens[0].Offset;
                        var rowEnd = rowTokens[^1].Offset + rowTokens[^1].Length;
                        rows.Add(new GcxForeachRowSite(rowOffset, rowEnd - rowOffset));
                    }

                    if (rows.Count == rowCount)
                    {
                        site = new GcxForeachSite
                        {
                            CommandOffset = parent.CommandOffset,
                            CommandLength = parent.CommandLength,
                            DataParameterOffset = data.ParameterOffset,
                            DataParameterLength = data.ParameterLength,
                            RepeatParameterOffset = repeatParameter.ParameterOffset,
                            RepeatParameterLength = repeatParameter.ParameterLength,
                            Repeat = repeat,
                            Rows = rows
                        };
                    }
                }
                return new ForeachContext(
                    rowCount,
                    modelSites,
                    transformSites,
                    collisionReferenceSites,
                    site);
            }
            return ForeachContext.Empty;
        }

        private sealed class CommandSiteBuilder
        {
            public CommandSiteBuilder(int commandOffset, int commandLength, bool isNested)
            {
                CommandOffset = commandOffset;
                CommandLength = commandLength;
                IsNested = isNested;
            }

            public int CommandOffset { get; }
            public int CommandLength { get; }
            public bool IsNested { get; }
            public uint? CommandHash { get; set; }
            public List<ParameterSiteBuilder> Parameters { get; } = [];

            public GcxVectorSite? GetVector(byte letter, uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Letter == letter && item.Hash == hash);
                return parameter == null
                    ? null
                    : new GcxVectorSite(
                        parameter.ParameterOffset,
                        parameter.ParameterLength,
                        parameter.ParameterPayloadOffset,
                        parameter.Literals);
            }

            public uint? GetStringCode(byte letter, uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Letter == letter && item.Hash == hash);
                return parameter == null || parameter.StringCodes.Count == 0
                    ? null
                    : parameter.StringCodes[0].Value;
            }

            public uint? GetStringCode(uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Hash == hash);
                return parameter == null || parameter.StringCodes.Count == 0
                    ? null
                    : parameter.StringCodes[0].Value;
            }

            public uint? GetLastStringCode(uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Hash == hash);
                return parameter == null || parameter.StringCodes.Count == 0
                    ? null
                    : parameter.StringCodes[^1].Value;
            }

            public GcxStringCodeSite? GetStringCodeSite(byte letter, uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Letter == letter && item.Hash == hash);
                if (parameter == null || parameter.StringCodes.Count == 0)
                {
                    return null;
                }

                var value = parameter.StringCodes[0];
                return new GcxStringCodeSite(
                    parameter.ParameterOffset,
                    parameter.ParameterLength,
                    value.Offset,
                    value.Value);
            }

            public GcxStringCodeSite? GetStringCodeSite(uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Hash == hash);
                return BuildStringCodeSite(parameter, useLast: false);
            }

            public GcxStringCodeSite? GetLastStringCodeSite(uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Hash == hash);
                return BuildStringCodeSite(parameter, useLast: true);
            }

            private static GcxStringCodeSite? BuildStringCodeSite(
                ParameterSiteBuilder? parameter,
                bool useLast)
            {
                if (parameter == null || parameter.StringCodes.Count == 0)
                {
                    return null;
                }

                var value = useLast ? parameter.StringCodes[^1] : parameter.StringCodes[0];
                return new GcxStringCodeSite(
                    parameter.ParameterOffset,
                    parameter.ParameterLength,
                    value.Offset,
                    value.Value);
            }

            public int? GetArgument(byte letter, uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Letter == letter && item.Hash == hash);
                return parameter?.Arguments.Count > 0 ? parameter.Arguments[0] : null;
            }

            public int? GetArgument(uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Hash == hash);
                return parameter?.Arguments.Count > 0 ? parameter.Arguments[0] : null;
            }

            public int? GetLastArgument(uint hash)
            {
                var parameter = Parameters.FirstOrDefault(item => item.Hash == hash);
                return parameter?.Arguments.Count > 0 ? parameter.Arguments[^1] : null;
            }

            public bool HasParameter(byte letter, uint hash)
            {
                return Parameters.Any(item => item.Letter == letter && item.Hash == hash);
            }
        }

        private sealed class ParameterSiteBuilder
        {
            public ParameterSiteBuilder(
                byte letter,
                uint hash,
                int parameterOffset,
                int parameterLength,
                int parameterPayloadOffset)
            {
                Letter = letter;
                Hash = hash;
                ParameterOffset = parameterOffset;
                ParameterLength = parameterLength;
                ParameterPayloadOffset = parameterPayloadOffset;
            }

            public byte Letter { get; }
            public uint Hash { get; }
            public int ParameterOffset { get; }
            public int ParameterLength { get; }
            public int ParameterPayloadOffset { get; }
            public List<GcxLiteralSite> Literals { get; } = [];
            public List<RecordedStringCode> StringCodes { get; } = [];
            public List<int> Arguments { get; } = [];
            public List<RecordedValueToken> ValueTokens { get; } = [];
        }

        private readonly record struct RecordedStringCode(int Offset, uint Value);
        private readonly record struct RecordedValueToken(
            RecordedStringCode? StringCode = null,
            int Offset = -1,
            int Length = 0);
        private sealed record ForeachContext(
            int RowCount,
            IReadOnlyList<GcxStringCodeSite?> ModelSites,
            IReadOnlyList<GcxStringCodeSite?> TransformSites,
            IReadOnlyList<GcxStringCodeSite?> CollisionReferenceSites,
            GcxForeachSite? Site)
        {
            public static ForeachContext Empty { get; } = new(0, [], [], [], null);
        }

        private void SetError()
        {
            if (_hasError)
            {
                return;
            }

            _hasError = true;
            _activeBuilder.Append(" /* EOF */");
            _ptr = _buffer.Length;
        }
    }

    private sealed class IndentationManager
    {
        private const string IndentToken = "    ";
        private int _level;

        public void Indent() => _level++;

        public void Unindent()
        {
            if (_level > 0)
            {
                _level--;
            }
        }

        public void WriteIndent(StringBuilder builder)
        {
            for (int i = 0; i < _level; i++)
            {
                builder.Append(IndentToken);
            }
        }
    }
}
