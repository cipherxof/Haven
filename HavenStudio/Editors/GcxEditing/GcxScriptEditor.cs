using System;
using System.Collections.Generic;
using System.IO;
using AvaloniaHex.Document;
using HavenStudio.Formats.Gcx;

namespace HavenStudio.Editors.GcxEditing;

public sealed class GcxScriptEditor
{
    private readonly GcxDocumentSession _session;

    public GcxScriptEditor(GcxDocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public GcxScriptNode? SelectedScript { get; private set; }
    public IBinaryDocument? HexDocument { get; private set; }
    public bool HasSelectedScript => SelectedScript?.Script != null && !SelectedScript.IsAggregate;
    public byte[] SelectedBytes => SelectedScript?.Script?.Bytes ?? Array.Empty<byte>();

    public void Reset()
    {
        SelectedScript = null;
        HexDocument = null;
    }

    public void Select(GcxScriptNode? scriptNode)
    {
        SelectedScript = scriptNode;
        HexDocument = scriptNode?.Script == null || scriptNode.IsAggregate
            ? null
            : new DynamicBinaryDocument(scriptNode.Script.Bytes ?? Array.Empty<byte>());
    }

    public void CommitHexDocument()
    {
        if (!HasSelectedScript)
        {
            return;
        }

        var bytes = ExtractDocumentBytes(HexDocument);
        if (bytes.Length == 0 && HexDocument == null)
        {
            return;
        }

        SelectedScript!.Script!.Bytes = bytes;
        UpdateMainProcSize();
        _session.MarkDirty();
    }

    public GcxScriptNode? AddProcedure()
    {
        var document = _session.Document;
        if (document == null)
        {
            return null;
        }

        var newProcId = document.ScriptDefinitions.Count + 1;
        var script = new GcxScript([0x8E, 0x00, 0x00, 0x00]);
        UpdateProcSize(script.Bytes);
        document.ScriptDefinitions.Add(new GcxScriptDefinition(type: 0, offset: 0) { Script = script });
        UpdateMainProcSize();
        _session.MarkDirty();

        var node = new GcxScriptNode($"proc{newProcId}", script);
        Select(node);
        return node;
    }

    public bool UpdateSelectedProcedureSize()
    {
        if (!HasSelectedScript)
        {
            return false;
        }

        var bytes = ExtractDocumentBytes(HexDocument);
        if (bytes.Length == 0)
        {
            bytes = SelectedScript!.Script!.Bytes ?? Array.Empty<byte>();
        }

        if (!UpdateProcSize(bytes))
        {
            return false;
        }

        SelectedScript!.Script!.Bytes = bytes;
        HexDocument = new DynamicBinaryDocument(bytes);
        _session.MarkDirty();
        return true;
    }

    public bool InsertCommandBytes(ReadOnlySpan<byte> commandBytes, bool insertAtStart = false)
    {
        if (!HasSelectedScript || commandBytes.IsEmpty)
        {
            return false;
        }

        var currentBytes = ExtractDocumentBytes(HexDocument);
        if (currentBytes.Length == 0)
        {
            currentBytes = SelectedScript!.Script!.Bytes ?? Array.Empty<byte>();
        }

        if (currentBytes.Length == 0)
        {
            return false;
        }

        var insertIndex = insertAtStart
            ? GetScriptBodyStartIndex(currentBytes)
            : Math.Max(0, currentBytes.Length - 1);
        insertIndex = Math.Clamp(insertIndex, 0, currentBytes.Length);

        var finalBytes = new byte[currentBytes.Length + commandBytes.Length];
        currentBytes.AsSpan(0, insertIndex).CopyTo(finalBytes);
        commandBytes.CopyTo(finalBytes.AsSpan(insertIndex));
        currentBytes.AsSpan(insertIndex).CopyTo(finalBytes.AsSpan(insertIndex + commandBytes.Length));
        UpdateProcSize(finalBytes);

        SelectedScript!.Script!.Bytes = finalBytes;
        HexDocument = new DynamicBinaryDocument(finalBytes);
        _session.MarkDirty();
        return true;
    }

    public void ReplaceScriptBytes(GcxScript script, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(bytes);
        script.Bytes = bytes;
        if (ReferenceEquals(SelectedScript?.Script, script))
        {
            HexDocument = new DynamicBinaryDocument(bytes);
        }
    }

    public static byte[] ExtractDocumentBytes(IBinaryDocument? document)
    {
        return document switch
        {
            DynamicBinaryDocument dynamicDocument => dynamicDocument.ToArray(),
            MemoryBinaryDocument memoryDocument => memoryDocument.Memory.ToArray(),
            _ => Array.Empty<byte>()
        };
    }

    public static bool UpdateProcSize(byte[] bytes)
    {
        if (bytes.Length < 2)
        {
            return false;
        }

        if (bytes[0] == 0x8D)
        {
            var shortProcedureSize = bytes.Length - 2;
            if (shortProcedureSize > byte.MaxValue)
            {
                throw new InvalidDataException(
                    $"GCX 0x8D procedure body is {shortProcedureSize} bytes; the format limit is {byte.MaxValue} bytes.");
            }
            bytes[1] = (byte)shortProcedureSize;
            return true;
        }

        if (bytes[0] != 0x8E || bytes.Length < 3)
        {
            return false;
        }

        var size = bytes.Length - 3;
        if (size > ushort.MaxValue)
        {
            throw new InvalidDataException(
                $"GCX 0x8E procedure body is {size} bytes; the format limit is {ushort.MaxValue} bytes.");
        }
        bytes[1] = (byte)(size & 0xFF);
        bytes[2] = (byte)((size >> 8) & 0xFF);
        return true;
    }

    private void UpdateMainProcSize()
    {
        var mainBytes = _session.Document?.MainScript?.Bytes;
        if (mainBytes != null)
        {
            UpdateProcSize(mainBytes);
        }
    }

    private static int GetScriptBodyStartIndex(byte[] bytes)
    {
        if (bytes.Length < 2)
        {
            return 0;
        }

        return bytes[0] switch
        {
            0x8D => 2,
            0x8E => Math.Min(3, bytes.Length),
            _ => 0
        };
    }
}
