using System;
using System.Collections.Generic;
using System.Threading;
using HavenStudio.Formats.Gcx;

namespace HavenStudio.Editors.GcxEditing;

public sealed class GcxDecompilationService
{
    public IReadOnlyDictionary<string, string> DecompileDocument(
        Gcx document,
        bool isMgs3,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        var scripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["main"] = Decompile(document.MainScript?.Bytes, "main", isMgs3)
        };

        for (var index = 0; index < document.ScriptDefinitions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = $"proc{index + 1}";
            scripts[name] = Decompile(document.ScriptDefinitions[index].Script?.Bytes, name, isMgs3);
        }

        return scripts;
    }

    public string Decompile(byte[]? bytes, string scriptName, bool isMgs3)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return "// Empty script.";
        }

        try
        {
            return GcxDecompiler.Decompile(bytes, scriptName, isMgs3);
        }
        catch (Exception exception)
        {
            return $"// Decompilation error: {exception.Message}";
        }
    }
}
