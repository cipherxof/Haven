using System;
using System.Collections.Generic;
using System.IO;
using HavenStudio.Formats.Dds;
using HavenStudio.Formats.Dld;
using HavenStudio.Formats.Dlz;
using HavenStudio.Formats.Txn;
using HavenStudio.Services.Workspace;
using HavenStudio.Utils;

namespace HavenStudio.Services;

public static class TxnTextureDumpService
{
    public sealed record DumpSummary(int Total, int Dumped, int Skipped);

    public static DumpSummary DumpAll(
        WorkspacePath txnPath,
        IWorkspaceCatalog workspace,
        string outputFolder)
    {
        ArgumentNullException.ThrowIfNull(txnPath);
        ArgumentNullException.ThrowIfNull(workspace);
        if (string.IsNullOrWhiteSpace(outputFolder)) throw new ArgumentException("Output folder is required.", nameof(outputFolder));

        Directory.CreateDirectory(outputFolder);

        using var stream = workspace.OpenRead(txnPath);
        var txn = new TxnFile(stream, workspace.Endianness);

        var dlds = LoadDlds(workspace);

        int dumped = 0;
        int skipped = 0;

        for (int textureIndex = 0; textureIndex < txn.ImageInfo.Count; textureIndex++)
        {
            var info = txn.ImageInfo[textureIndex];
            var imageIndex = txn.GetIndex(info);
            if (imageIndex < 0 || imageIndex >= txn.Images.Count)
            {
                skipped++;
                continue;
            }

            var image = txn.Images[imageIndex];
            if (!TryFindTextureData(dlds, info.TriId, textureIndex, out var data))
            {
                skipped++;
                continue;
            }

            ushort width = info.Width > 0 ? info.Width : image.Width;
            ushort height = info.Height > 0 ? info.Height : image.Height;
            if (width == 0 || height == 0)
            {
                skipped++;
                continue;
            }

            var format = GetDxtFormat(image.FourCC);
            if (format == null)
            {
                skipped++;
                continue;
            }

            int expected = GetExpectedDxtSize(width, height, image.FourCC);
            if (expected > 0 && data.Length > expected)
            {
                data = data.AsSpan(0, expected).ToArray();
            }

            var outputPath = Path.Combine(outputFolder, BuildOutputFileName(info));
            DdsFile.Create(outputPath, height, width, format, 1, data);
            dumped++;
        }

        return new DumpSummary(txn.ImageInfo.Count, dumped, skipped);
    }

    private static List<DldFile> LoadDlds(IWorkspaceCatalog workspace)
    {
        var dlds = new List<DldFile>();

        var snapshot = workspace.Snapshot;
        if (snapshot is null)
        {
            return dlds;
        }

        foreach (var dldFile in snapshot.WithExtension(".dld"))
        {
            using var stream = workspace.OpenRead(dldFile.Path);
            dlds.Add(new DldFile(stream, workspace.Endianness));
        }

        foreach (var dlzFile in snapshot.WithExtension(".dlz"))
        {
            using var input = workspace.OpenRead(dlzFile.Path);
            var dlz = new DlzFile(input, workspace.Endianness);
            using var unpacked = new MemoryStream();
            dlz.Unpack(unpacked);
            unpacked.Position = 0;
            dlds.Add(new DldFile(unpacked, workspace.Endianness));
        }

        return dlds;
    }

    private static bool TryFindTextureData(List<DldFile> dlds, uint objectId, int index, out byte[] data)
    {
        data = Array.Empty<byte>();

        for (byte priority = 0; priority <= 3; priority++)
        {
            foreach (var dld in dlds)
            {
                var texture = dld.FindTexture(objectId, index, (DldPriority)priority);
                if (texture == null)
                {
                    continue;
                }

                data = texture.Data;
                return true;
            }
        }

        return false;
    }

    private static int GetExpectedDxtSize(int width, int height, ushort fourCc)
    {
        if (width <= 0 || height <= 0)
        {
            return -1;
        }

        int blockSize = fourCc == 11 || fourCc == 10 ? 16 : (fourCc == 9 ? 8 : -1);
        if (blockSize < 0)
        {
            return -1;
        }

        int blocksWide = (width + 3) / 4;
        int blocksHigh = (height + 3) / 4;
        return blocksWide * blocksHigh * blockSize;
    }

    private static string? GetDxtFormat(ushort fourCc)
    {
        return fourCc switch
        {
            9 => "DXT1",
            10 => "DXT3",
            11 => "DXT5",
            _ => null
        };
    }

    private static string BuildOutputFileName(TxnInfo info)
    {
        var baseName = info.TexId.ToString("X8");
        if (DictionaryFile.TryGetLookupName(info.TexId, out var resolvedName))
        {
            baseName = SanitizeFileName(resolvedName);
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = info.TexId.ToString("X8");
        }

        return $"{baseName}.dds";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = value;
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        return sanitized.Trim();
    }
}
