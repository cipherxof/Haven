using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Geo;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Editors.GcxEditing;

public sealed record GcxPlacementWriteResult(byte[] Bytes, bool CommandResized);

public static class GcxPlacementWriter
{
    public static GcxPlacementWriteResult DuplicatePlacement(
        byte[] scriptBytes,
        GcxPlacementSite site,
        int? foreachRowIndex = null,
        GcxStringCodeSite? transformSourceSite = null,
        uint? replacementTransformHash = null)
    {
        ArgumentNullException.ThrowIfNull(scriptBytes);
        ArgumentNullException.ThrowIfNull(site);

        if (site.Foreach != null)
        {
            if (foreachRowIndex is not { } rowIndex)
            {
                throw new InvalidOperationException("The foreach placement row could not be identified.");
            }

            return DuplicateForeachRow(
                scriptBytes,
                site.Foreach,
                rowIndex,
                transformSourceSite,
                replacementTransformHash);
        }
        if (site.IsNested)
        {
            throw new InvalidOperationException(
                "This nested placement is not part of a writable foreach data table.");
        }

        ReadTaggedBlock(scriptBytes, site.CommandOffset, site.CommandLength, "command");
        var command = scriptBytes.AsSpan(site.CommandOffset, site.CommandLength).ToArray();
        PatchDuplicateTransform(
            command,
            site.CommandOffset,
            transformSourceSite,
            replacementTransformHash);
        var rewritten = ReplaceRange(
            scriptBytes,
            site.CommandOffset + site.CommandLength,
            0,
            command);
        UpdateProcedureSize(rewritten);
        return new GcxPlacementWriteResult(rewritten, CommandResized: true);
    }

    public static GcxPlacementWriteResult WriteModelHash(
        byte[] scriptBytes,
        GcxPlacementSite site,
        uint modelHash)
    {
        ArgumentNullException.ThrowIfNull(scriptBytes);
        ArgumentNullException.ThrowIfNull(site);
        if (!site.ModelHashEditable || site.Model == null)
        {
            throw new InvalidOperationException(
                "This placement does not contain a direct writable model hash.");
        }
        if (modelHash == 0)
        {
            throw new InvalidDataException("A placement model hash cannot be zero.");
        }

        return WriteModelHash(scriptBytes, site.Model, modelHash);
    }

    public static GcxPlacementWriteResult WriteModelHash(
        byte[] scriptBytes,
        GcxStringCodeSite? modelSite,
        uint modelHash)
    {
        ArgumentNullException.ThrowIfNull(scriptBytes);
        if (modelSite == null)
        {
            throw new InvalidOperationException(
                "This placement does not contain a direct writable model hash.");
        }
        if (modelHash == 0)
        {
            throw new InvalidDataException("A placement model hash cannot be zero.");
        }

        return PatchStringCode(scriptBytes, modelSite, modelHash, "model");
    }

    public static GcxPlacementWriteResult WritePosition(
        byte[] scriptBytes,
        GcxPlacementSite site,
        Vector3 worldPosition)
    {
        return WriteTransform(scriptBytes, site, worldPosition, rotationRadians: null);
    }

    public static GcxPlacementWriteResult WriteDirection(
        byte[] scriptBytes,
        GcxPlacementSite site,
        Vector3 rotationRadians)
    {
        return WriteTransform(scriptBytes, site, worldPosition: null, rotationRadians);
    }

    public static GcxPlacementWriteResult WriteTransform(
        byte[] scriptBytes,
        GcxPlacementSite site,
        Vector3? worldPosition,
        Vector3? rotationRadians)
    {
        ArgumentNullException.ThrowIfNull(scriptBytes);
        ArgumentNullException.ThrowIfNull(site);
        if (worldPosition == null && rotationRadians == null)
        {
            return new GcxPlacementWriteResult(scriptBytes.ToArray(), CommandResized: false);
        }
        if (worldPosition != null && !site.Editable)
        {
            throw new InvalidOperationException(site.ReadOnlyReason ?? "This placement is read-only.");
        }
        if (worldPosition != null && site.Position?.HasThreeLiteralComponents != true)
        {
            throw new InvalidDataException("GCX placement does not contain three position literals.");
        }
        if (rotationRadians != null &&
            (site.IsNested ||
             !GcxPlacementCommandCatalog.TryGet(site.CommandHash, out var definition) ||
             !definition.SupportsCommandReencoding))
        {
            throw new InvalidOperationException("This placement direction cannot be safely re-encoded.");
        }
        if (rotationRadians != null && site.Direction?.HasThreeLiteralComponents != true)
        {
            throw new InvalidDataException("GCX placement does not contain three direction literals.");
        }

        var requestedVectors = new List<(GcxVectorSite Site, IReadOnlyList<int> Values)>();
        if (worldPosition is { } position)
        {
            // GCX stores position vectors as X, Z, Y.
            requestedVectors.Add((site.Position!,
            [
                ToInt32(position.X, "X"),
                ToInt32(position.Z, "Z"),
                ToInt32(position.Y, "Y")
            ]));
        }
        if (rotationRadians is { } rotation)
        {
            requestedVectors.Add((site.Direction!,
            [
                GeoEffectChunkPatcher.EncodeAngle(rotation.X),
                GeoEffectChunkPatcher.EncodeAngle(rotation.Y),
                GeoEffectChunkPatcher.EncodeAngle(rotation.Z)
            ]));
        }

        foreach (var (vector, _) in requestedVectors)
        {
            ValidateVectorBounds(scriptBytes, vector);
        }
        if (requestedVectors.All(item => item.Site.Components
                .Zip(item.Values)
                .All(pair => Fits(pair.First, pair.Second))))
        {
            var patched = scriptBytes.ToArray();
            foreach (var (vector, values) in requestedVectors)
            {
                for (var index = 0; index < vector.Components.Count; index++)
                {
                    PatchLiteral(patched, vector.Components[index], values[index]);
                }
            }
            return new GcxPlacementWriteResult(patched, CommandResized: false);
        }

        return new GcxPlacementWriteResult(
            ReencodeCommand(scriptBytes, site, requestedVectors),
            CommandResized: true);
    }

    public static GcxPlacementWriteResult WriteEffectHash(
        byte[] scriptBytes,
        GcxPlacementSite site,
        uint effectHash)
    {
        ArgumentNullException.ThrowIfNull(scriptBytes);
        ArgumentNullException.ThrowIfNull(site);
        if (effectHash == 0)
        {
            throw new InvalidDataException("A placement effect hash cannot be zero.");
        }
        if (site.IsNested || site.Effect == null)
        {
            throw new InvalidOperationException(
                "This placement does not contain a direct writable effect hash.");
        }

        var command = ReadTaggedBlock(
            scriptBytes,
            site.CommandOffset,
            site.CommandLength,
            "command");
        var effect = site.Effect;
        ReadTaggedBlock(scriptBytes, effect.ParameterOffset, effect.ParameterLength, "effect parameter");
        EnsureRange(scriptBytes, effect.ValueOffset, 3, "effect hash");
        if (effect.ValueOffset <= effect.ParameterOffset ||
            effect.ValueOffset + 3 > command.PayloadOffset + command.PayloadLength ||
            scriptBytes[effect.ValueOffset - 1] is not (0x06 or 0x09))
        {
            throw new InvalidDataException("Recorded GCX effect no longer matches the script bytes.");
        }

        var patched = scriptBytes.ToArray();
        WriteUInt24(patched.AsSpan(effect.ValueOffset, 3), effectHash);
        return new GcxPlacementWriteResult(patched, CommandResized: false);
    }

    public static GcxPlacementWriteResult WriteCollisionReference(
        byte[] scriptBytes,
        GcxPlacementSite site,
        uint? collisionReferenceHash)
    {
        ArgumentNullException.ThrowIfNull(scriptBytes);
        ArgumentNullException.ThrowIfNull(site);
        if (!site.CollisionReferenceEditable)
        {
            throw new InvalidOperationException(
                site.IsNested
                    ? "Nested and foreach placements are read-only."
                    : "This placement command cannot safely update a collision reference.");
        }

        var hash = collisionReferenceHash.GetValueOrDefault();
        var reference = site.CollisionReference;
        if (site.IsNested && hash == 0)
        {
            throw new InvalidOperationException(
                "A nested collision reference can be changed, but it cannot be removed safely.");
        }
        if (reference != null && hash != 0)
        {
            return PatchStringCode(scriptBytes, reference, hash, "collision reference");
        }

        if (reference == null && hash == 0)
        {
            return new GcxPlacementWriteResult(scriptBytes.ToArray(), CommandResized: false);
        }

        var commandBlock = ReadTaggedBlock(scriptBytes, site.CommandOffset, site.CommandLength, "command");
        var commandPayload = scriptBytes.AsSpan(commandBlock.PayloadOffset, commandBlock.PayloadLength).ToArray();
        if (reference != null)
        {
            ReadTaggedBlock(
                scriptBytes,
                reference.ParameterOffset,
                reference.ParameterLength,
                "reference parameter");
            var relativeOffset = reference.ParameterOffset - commandBlock.PayloadOffset;
            commandPayload = ReplaceRange(
                commandPayload,
                relativeOffset,
                reference.ParameterLength,
                ReadOnlySpan<byte>.Empty);
        }
        else
        {
            if (commandPayload.Length == 0 || commandPayload[^1] != 0x00)
            {
                throw new InvalidDataException("GCX placement command is missing its terminating byte.");
            }
            commandPayload = ReplaceRange(
                commandPayload,
                commandPayload.Length - 1,
                0,
                BuildCollisionReferenceParameter(hash));
        }

        var commandBytes = GcxCommandBuilder.WrapTaggedPayload(
            (byte)(scriptBytes[site.CommandOffset] & 0xF0),
            commandPayload);
        var rewritten = ReplaceRange(
            scriptBytes,
            site.CommandOffset,
            site.CommandLength,
            commandBytes);
        if (!GcxScriptEditor.UpdateProcSize(rewritten))
        {
            throw new InvalidDataException("GCX placement belongs to a script without a procedure-size header.");
        }
        return new GcxPlacementWriteResult(rewritten, CommandResized: true);
    }

    public static GcxPlacementWriteResult WriteCollisionReference(
        byte[] scriptBytes,
        GcxStringCodeSite collisionReferenceSite,
        uint collisionReferenceHash)
    {
        ArgumentNullException.ThrowIfNull(scriptBytes);
        ArgumentNullException.ThrowIfNull(collisionReferenceSite);
        if (collisionReferenceHash == 0)
        {
            throw new InvalidOperationException(
                "A foreach collision reference can be changed, but it cannot be removed safely.");
        }
        return PatchStringCode(
            scriptBytes,
            collisionReferenceSite,
            collisionReferenceHash,
            "collision reference");
    }

    private static byte[] ReencodeCommand(
        byte[] scriptBytes,
        GcxPlacementSite site,
        IReadOnlyList<(GcxVectorSite Site, IReadOnlyList<int> Values)> requestedVectors)
    {
        var commandBlock = ReadTaggedBlock(scriptBytes, site.CommandOffset, site.CommandLength, "command");
        var commandPayload = scriptBytes.AsSpan(commandBlock.PayloadOffset, commandBlock.PayloadLength).ToArray();
        var requestedBySite = requestedVectors.ToDictionary(item => item.Site, item => item.Values);
        var vectors = new List<(GcxVectorSite Site, IReadOnlyList<int> Values)>();
        if (site.Position?.HasThreeLiteralComponents == true)
        {
            vectors.Add((
                site.Position,
                requestedBySite.GetValueOrDefault(site.Position) ??
                site.Position.Components.Select(component => component.Value).ToArray()));
        }
        if (site.Direction?.HasThreeLiteralComponents == true)
        {
            vectors.Add((
                site.Direction,
                requestedBySite.GetValueOrDefault(site.Direction) ??
                site.Direction.Components.Select(component => component.Value).ToArray()));
        }

        foreach (var (vector, values) in vectors.OrderByDescending(item => item.Site.ParameterOffset))
        {
            ValidateVectorBounds(scriptBytes, vector);
            var parameterBlock = ReadTaggedBlock(
                scriptBytes,
                vector.ParameterOffset,
                vector.ParameterLength,
                "parameter");
            var parameterPayload = scriptBytes
                .AsSpan(parameterBlock.PayloadOffset, parameterBlock.PayloadLength)
                .ToArray();
            for (var index = vector.Components.Count - 1; index >= 0; index--)
            {
                var literal = vector.Components[index];
                var relativeOffset = literal.Offset - parameterBlock.PayloadOffset;
                parameterPayload = ReplaceRange(
                    parameterPayload,
                    relativeOffset,
                    literal.Width,
                    GcxCommandBuilder.Int32LiteralBytes(values[index]));
            }

            var parameterBytes = GcxCommandBuilder.WrapTaggedPayload(
                (byte)(scriptBytes[vector.ParameterOffset] & 0xF0),
                parameterPayload);
            commandPayload = ReplaceRange(
                commandPayload,
                vector.ParameterOffset - commandBlock.PayloadOffset,
                vector.ParameterLength,
                parameterBytes);
        }

        var commandBytes = GcxCommandBuilder.WrapTaggedPayload(
            (byte)(scriptBytes[site.CommandOffset] & 0xF0),
            commandPayload);
        var rewritten = ReplaceRange(
            scriptBytes,
            site.CommandOffset,
            site.CommandLength,
            commandBytes);
        if (!GcxScriptEditor.UpdateProcSize(rewritten))
        {
            throw new InvalidDataException("GCX placement belongs to a script without a procedure-size header.");
        }
        return rewritten;
    }

    private static GcxPlacementWriteResult DuplicateForeachRow(
        byte[] scriptBytes,
        GcxForeachSite foreachSite,
        int rowIndex,
        GcxStringCodeSite? transformSourceSite,
        uint? replacementTransformHash)
    {
        if (rowIndex < 0 || rowIndex >= foreachSite.Rows.Count)
        {
            throw new InvalidOperationException("The selected foreach placement row is stale.");
        }

        var commandBlock = ReadTaggedBlock(
            scriptBytes,
            foreachSite.CommandOffset,
            foreachSite.CommandLength,
            "foreach command");
        var commandPayload = scriptBytes
            .AsSpan(commandBlock.PayloadOffset, commandBlock.PayloadLength)
            .ToArray();
        var replacements = new[]
        {
            new BlockReplacement(
                foreachSite.DataParameterOffset - commandBlock.PayloadOffset,
                foreachSite.DataParameterLength,
                DuplicateDataRow(
                    scriptBytes,
                    foreachSite,
                    rowIndex,
                    transformSourceSite,
                    replacementTransformHash)),
            new BlockReplacement(
                foreachSite.RepeatParameterOffset - commandBlock.PayloadOffset,
                foreachSite.RepeatParameterLength,
                IncrementRepeatParameter(scriptBytes, foreachSite))
        };
        foreach (var replacement in replacements.OrderByDescending(item => item.Offset))
        {
            commandPayload = ReplaceRange(
                commandPayload,
                replacement.Offset,
                replacement.Length,
                replacement.Bytes);
        }

        var commandBytes = GcxCommandBuilder.WrapTaggedPayload(
            (byte)(scriptBytes[foreachSite.CommandOffset] & 0xF0),
            commandPayload);
        var rewritten = ReplaceRange(
            scriptBytes,
            foreachSite.CommandOffset,
            foreachSite.CommandLength,
            commandBytes);
        UpdateProcedureSize(rewritten);
        return new GcxPlacementWriteResult(rewritten, CommandResized: true);
    }

    private static byte[] DuplicateDataRow(
        byte[] scriptBytes,
        GcxForeachSite foreachSite,
        int rowIndex,
        GcxStringCodeSite? transformSourceSite,
        uint? replacementTransformHash)
    {
        var parameterBlock = ReadTaggedBlock(
            scriptBytes,
            foreachSite.DataParameterOffset,
            foreachSite.DataParameterLength,
            "foreach data parameter");
        var parameterPayload = scriptBytes
            .AsSpan(parameterBlock.PayloadOffset, parameterBlock.PayloadLength)
            .ToArray();
        var row = foreachSite.Rows[rowIndex];
        EnsureRange(scriptBytes, row.Offset, row.Length, "foreach data row");
        var rowBytes = scriptBytes.AsSpan(row.Offset, row.Length).ToArray();
        PatchDuplicateTransform(
            rowBytes,
            row.Offset,
            transformSourceSite,
            replacementTransformHash);
        parameterPayload = ReplaceRange(
            parameterPayload,
            row.Offset + row.Length - parameterBlock.PayloadOffset,
            0,
            rowBytes);
        return GcxCommandBuilder.WrapTaggedPayload(
            (byte)(scriptBytes[foreachSite.DataParameterOffset] & 0xF0),
            parameterPayload);
    }

    private static void PatchDuplicateTransform(
        byte[] copiedBytes,
        int sourceOffset,
        GcxStringCodeSite? sourceSite,
        uint? replacementHash)
    {
        if (replacementHash == null)
        {
            return;
        }
        if (sourceSite == null)
        {
            throw new InvalidOperationException(
                "An effect-bound duplicate requires a writable GCX effect or property hash.");
        }
        var hash = replacementHash.Value;
        if (hash == 0 || hash > 0xFFFFFF)
        {
            throw new InvalidDataException("GCX string-code values must be non-zero 24-bit hashes.");
        }

        var relativeOffset = sourceSite.ValueOffset - sourceOffset;
        EnsureRange(copiedBytes, relativeOffset, 3, "duplicated transform hash");
        if (relativeOffset == 0 || copiedBytes[relativeOffset - 1] != 0x06)
        {
            throw new InvalidDataException(
                "Recorded GCX effect or property hash is outside the duplicated placement data.");
        }
        WriteUInt24(copiedBytes.AsSpan(relativeOffset, 3), hash);
    }

    private static byte[] IncrementRepeatParameter(
        byte[] scriptBytes,
        GcxForeachSite foreachSite)
    {
        var parameterBlock = ReadTaggedBlock(
            scriptBytes,
            foreachSite.RepeatParameterOffset,
            foreachSite.RepeatParameterLength,
            "foreach repeat parameter");
        var repeat = foreachSite.Repeat;
        EnsureRange(scriptBytes, repeat.Offset, repeat.Width, "foreach repeat literal");
        var parameterPayload = scriptBytes
            .AsSpan(parameterBlock.PayloadOffset, parameterBlock.PayloadLength)
            .ToArray();
        parameterPayload = ReplaceRange(
            parameterPayload,
            repeat.Offset - parameterBlock.PayloadOffset,
            repeat.Width,
            GcxCommandBuilder.Int32LiteralBytes(checked(repeat.Value + 1)));
        return GcxCommandBuilder.WrapTaggedPayload(
            (byte)(scriptBytes[foreachSite.RepeatParameterOffset] & 0xF0),
            parameterPayload);
    }

    private static void UpdateProcedureSize(byte[] bytes)
    {
        if (!GcxScriptEditor.UpdateProcSize(bytes))
        {
            throw new InvalidDataException("GCX placement belongs to a script without a procedure-size header.");
        }
    }

    private static void PatchLiteral(byte[] bytes, GcxLiteralSite literal, int value)
    {
        EnsureRange(bytes, literal.Offset, literal.Width, "literal");
        switch (literal.Encoding)
        {
            case GcxLiteralEncoding.PackedNumber:
                bytes[literal.Offset] = (byte)((bytes[literal.Offset] & 0xC0) | (value + 1));
                break;
            case GcxLiteralEncoding.Int16:
                if (bytes[literal.Offset] != 0x01)
                {
                    throw new InvalidDataException("Recorded GCX int16 literal no longer matches the script bytes.");
                }
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(literal.Offset + 1, 2), checked((short)value));
                break;
            case GcxLiteralEncoding.Int32:
                if (bytes[literal.Offset] != 0x09)
                {
                    throw new InvalidDataException("Recorded GCX int32 literal no longer matches the script bytes.");
                }
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(literal.Offset + 1, 4), value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(literal));
        }
    }

    private static byte[] BuildCollisionReferenceParameter(uint hash)
    {
        Span<byte> payload = stackalloc byte[8];
        payload[0] = (byte)'r';
        payload[1] = 0x36;
        payload[2] = 0x9B;
        payload[3] = 0x84;
        payload[4] = 0x06;
        WriteUInt24(payload[5..], hash);
        return GcxCommandBuilder.WrapTaggedPayload(0x50, payload);
    }

    private static GcxPlacementWriteResult PatchStringCode(
        byte[] scriptBytes,
        GcxStringCodeSite site,
        uint hash,
        string label)
    {
        ReadTaggedBlock(scriptBytes, site.ParameterOffset, site.ParameterLength, $"{label} parameter");
        EnsureRange(scriptBytes, site.ValueOffset, 3, $"{label} hash");
        if (site.ValueOffset <= site.ParameterOffset ||
            site.ValueOffset + 3 > site.ParameterOffset + site.ParameterLength ||
            scriptBytes[site.ValueOffset - 1] != 0x06)
        {
            throw new InvalidDataException($"Recorded GCX {label} no longer matches the script bytes.");
        }

        var patched = scriptBytes.ToArray();
        WriteUInt24(patched.AsSpan(site.ValueOffset, 3), hash);
        return new GcxPlacementWriteResult(patched, CommandResized: false);
    }

    private static void WriteUInt24(Span<byte> destination, uint value)
    {
        if (destination.Length < 3 || value > 0xFFFFFF)
        {
            throw new InvalidDataException("GCX string-code values must be 24-bit hashes.");
        }
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
    }

    private static bool Fits(GcxLiteralSite literal, int value)
    {
        return literal.Encoding switch
        {
            GcxLiteralEncoding.PackedNumber => value is >= -1 and <= 62,
            GcxLiteralEncoding.Int16 => value is >= short.MinValue and <= short.MaxValue,
            GcxLiteralEncoding.Int32 => true,
            _ => false
        };
    }

    private static int ToInt32(float value, string component)
    {
        if (!float.IsFinite(value) || value < int.MinValue || value > int.MaxValue)
        {
            throw new InvalidDataException($"GCX placement {component} must be a finite 32-bit value.");
        }
        return checked((int)MathF.Round(value));
    }

    private static void ValidateVectorBounds(byte[] bytes, GcxVectorSite vector)
    {
        EnsureRange(bytes, vector.ParameterOffset, vector.ParameterLength, "parameter");
        foreach (var literal in vector.Components)
        {
            EnsureRange(bytes, literal.Offset, literal.Width, "literal");
            if (literal.Offset < vector.ParameterPayloadOffset ||
                literal.Offset + literal.Width > vector.ParameterOffset + vector.ParameterLength)
            {
                throw new InvalidDataException("Recorded GCX literal is outside its parameter span.");
            }
        }
    }

    private static TaggedBlock ReadTaggedBlock(byte[] bytes, int offset, int recordedLength, string label)
    {
        EnsureRange(bytes, offset, recordedLength, label);
        var sizeCode = bytes[offset] & 0x0F;
        int prefixLength;
        int payloadLength;
        if (sizeCode == 0x0D)
        {
            EnsureRange(bytes, offset, 2, label);
            prefixLength = 2;
            payloadLength = bytes[offset + 1];
        }
        else if (sizeCode == 0x0E)
        {
            EnsureRange(bytes, offset, 3, label);
            prefixLength = 3;
            payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 1, 2));
        }
        else if (sizeCode <= 0x0C)
        {
            prefixLength = 1;
            payloadLength = sizeCode;
        }
        else
        {
            throw new InvalidDataException($"GCX {label} uses an unsupported size code.");
        }

        var actualLength = checked(prefixLength + payloadLength);
        if (actualLength != recordedLength)
        {
            throw new InvalidDataException(
                $"Recorded GCX {label} span is stale (recorded {recordedLength}, actual {actualLength}).");
        }
        EnsureRange(bytes, offset, actualLength, label);
        return new TaggedBlock(offset + prefixLength, payloadLength);
    }

    private static byte[] ReplaceRange(
        byte[] source,
        int offset,
        int length,
        ReadOnlySpan<byte> replacement)
    {
        EnsureRange(source, offset, length, "replacement");
        var result = new byte[checked(source.Length - length + replacement.Length)];
        source.AsSpan(0, offset).CopyTo(result);
        replacement.CopyTo(result.AsSpan(offset));
        source.AsSpan(offset + length).CopyTo(result.AsSpan(offset + replacement.Length));
        return result;
    }

    private static void EnsureRange(byte[] bytes, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException($"GCX {label} span is outside the script.");
        }
    }

    private readonly record struct TaggedBlock(int PayloadOffset, int PayloadLength);
    private readonly record struct BlockReplacement(int Offset, int Length, byte[] Bytes);
}
