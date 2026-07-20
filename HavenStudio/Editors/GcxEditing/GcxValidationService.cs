using System;
using System.Collections.Generic;
using HavenStudio.Formats.Gcx;

namespace HavenStudio.Editors.GcxEditing;

public sealed class GcxValidationService
{
    public IReadOnlyList<string> GetProcedureSizeErrors(Gcx? document)
    {
        var errors = new List<string>();
        if (document == null)
        {
            return errors;
        }

        ValidateScript("main", document.MainScript?.Bytes, errors);
        for (var index = 0; index < document.ScriptDefinitions.Count; index++)
        {
            ValidateScript($"proc{index + 1}", document.ScriptDefinitions[index].Script?.Bytes, errors);
        }

        return errors;
    }

    private static void ValidateScript(string name, byte[]? bytes, ICollection<string> errors)
    {
        if (bytes == null || bytes.Length == 0 || bytes[0] is not (0x8D or 0x8E))
        {
            return;
        }

        if (!TryGetDeclaredProcedureSize(bytes, out var declaredSize, out var headerSize))
        {
            errors.Add($"{name}: invalid proc header or too short to read size.");
            return;
        }

        var actualSize = Math.Max(0, bytes.Length - headerSize);
        if (declaredSize != actualSize)
        {
            errors.Add($"{name}: declared size {declaredSize} does not match actual size {actualSize}.");
        }
    }

    internal static bool TryGetDeclaredProcedureSize(byte[] bytes, out int declaredSize, out int headerSize)
    {
        declaredSize = 0;
        headerSize = 0;
        if (bytes.Length < 2)
        {
            return false;
        }

        if (bytes[0] == 0x8D)
        {
            declaredSize = bytes[1];
            headerSize = 2;
            return true;
        }

        if (bytes[0] != 0x8E || bytes.Length < 3)
        {
            return false;
        }

        declaredSize = bytes[1] | (bytes[2] << 8);
        headerSize = 3;
        return true;
    }
}
