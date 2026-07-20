using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenStudio.Formats.Dld;
using HavenStudio.Formats.Dlz;
using HavenStudio.Formats.Mdn;
using HavenStudio.Formats.Txn;
using HavenStudio.Services.Workspace;
using Serilog;

namespace HavenStudio.Rendering;

public sealed class MdnTextureResolver
{
    private static readonly ILogger _log = Log.ForContext<MdnTextureResolver>();

    private readonly Dictionary<WorkspacePath, DldFile> _dldCache = new();
    private readonly Dictionary<WorkspacePath, TxnFile> _txnCache = new();
    private readonly Dictionary<uint, ResolvedTexture?> _textureCache = new();
    private IWorkspaceCatalog? _cachedWorkspace;
    private WorkspaceSnapshot? _cachedSnapshot;

    public bool TryResolve(Mdn mdn, IWorkspaceCatalog workspace, out ResolvedTexture texture)
    {
        texture = default;
        if (!TryResolveAll(mdn, workspace, out var textures))
        {
            return false;
        }

        if (TryGetDiffuseTextureHash(mdn, out var textureHash) && textures.TryGetValue(textureHash, out texture))
        {
            return true;
        }

        foreach (var value in textures.Values)
        {
            texture = value;
            return true;
        }

        return false;
    }

    public bool TryResolveAll(
        Mdn mdn,
        IWorkspaceCatalog workspace,
        out Dictionary<uint, ResolvedTexture> textures)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        textures = new Dictionary<uint, ResolvedTexture>();
        var snapshot = workspace.Snapshot;
        if (snapshot is null)
        {
            _log.Debug("[MDN] Texture resolve skipped: workspace has not been scanned.");
            return false;
        }

        ResetCacheForWorkspace(workspace, snapshot);

        if (mdn.Textures.Count == 0)
        {
            _log.Debug("[MDN] Texture resolve skipped: no textures in MDN.");
            return false;
        }

        var txnFiles = snapshot.WithExtension(".txn").ToList();

        if (txnFiles.Count == 0)
        {
            _log.Debug("[MDN] Texture resolve skipped: no .txn files under '{FolderPath}'.", workspace.RootPath);
            return false;
        }

        var dlds = LoadDlds(workspace, snapshot);
        if (dlds.Count == 0)
        {
            _log.Debug("[MDN] Texture resolve skipped: no .dld/.dlz files under '{FolderPath}'.", workspace.RootPath);
            return false;
        }

        foreach (var texture in mdn.Textures)
        {
            var textureHash = (uint)texture.NameHash;
            if (textures.ContainsKey(textureHash))
            {
                continue;
            }

            if (_textureCache.TryGetValue(textureHash, out var cachedTexture))
            {
                if (cachedTexture.HasValue)
                {
                    textures[textureHash] = cachedTexture.Value;
                }

                continue;
            }

            if (TryResolveFromTxn(workspace, txnFiles, dlds, textureHash, out var resolved))
            {
                textures[textureHash] = resolved;
                _textureCache[textureHash] = resolved;
            }
            else
            {
                _textureCache[textureHash] = null;
                _log.Debug("[MDN] Texture not resolved: 0x{TextureHash:X8}", textureHash);
            }
        }
        
        return textures.Count > 0;
    }

    private void ResetCacheForWorkspace(
        IWorkspaceCatalog workspace,
        WorkspaceSnapshot snapshot)
    {
        if (ReferenceEquals(_cachedWorkspace, workspace) &&
            ReferenceEquals(_cachedSnapshot, snapshot))
        {
            return;
        }

        _dldCache.Clear();
        _txnCache.Clear();
        _textureCache.Clear();
        _cachedWorkspace = workspace;
        _cachedSnapshot = snapshot;
    }

    private static bool TryGetDiffuseTextureHash(Mdn mdn, out uint textureHash)
    {
        textureHash = 0;
        if (mdn.Materials.Count > 0)
        {
            var diffuseIndex = mdn.Materials[0].DiffuseIndex;
            if (diffuseIndex >= 0 && diffuseIndex < mdn.Textures.Count)
            {
                textureHash = (uint)mdn.Textures[diffuseIndex].NameHash;
                return true;
            }
        }

        if (mdn.Textures.Count > 0)
        {
            textureHash = (uint)mdn.Textures[0].NameHash;
            return true;
        }

        return false;
    }
    
    private bool TryResolveFromTxn(
        IWorkspaceCatalog workspace,
        List<WorkspaceFile> txnFiles,
        List<DldFile> dlds,
        uint textureHash,
        out ResolvedTexture texture)
    {
        texture = default;
        foreach (var txnFile in txnFiles)
        {
            TxnFile txn;
            try
            {
                if (_txnCache.TryGetValue(txnFile.Path, out var value))
                {
                    txn = value;
                }
                else
                {
                    using var stream = workspace.OpenRead(txnFile.Path);
                    txn = new TxnFile(stream, workspace.Endianness);
                    _txnCache[txnFile.Path] = txn;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[MDN] Failed to read TXN '{VirtualPath}'", txnFile.Path);
                continue;
            }

            for (int textureIndex = 0; textureIndex < txn.ImageInfo.Count; textureIndex++)
            {
                var info = txn.ImageInfo[textureIndex];
                if (info.TexId != textureHash)
                {
                    continue;
                }

                var imageIndex = txn.GetIndex(info);
                if (imageIndex < 0 || imageIndex >= txn.Images.Count)
                {
                    continue;
                }

                var image = txn.Images[imageIndex];
                if (!TryFindTextureData(dlds, info.TriId, textureIndex, out var data))
                {
                    _log.Debug("[MDN] Texture data missing: tex=0x{TextureHash:X8} tri=0x{TriId:X8} texIdx={TexIndex} imgIdx={ImgIndex} (txn='{VirtualPath}').", textureHash, info.TriId, textureIndex, imageIndex, txnFile.Path);
                    continue;
                }

                ushort width = info.Width > 0 ? info.Width : image.Width;
                ushort height = info.Height > 0 ? info.Height : image.Height;

                LogTextureMeta(textureHash, info.TriId, textureIndex, imageIndex, width, height, info.ScaleU, info.ScaleV, info.OffsetU, info.OffsetV, info.Flag);
                if (!TryNormalizeTextureDimensions(
                        width,
                        height,
                        image.Width,
                        image.Height,
                        image.FourCC,
                        data,
                        out width,
                        out height,
                        out data))
                {
                    _log.Debug("[MDN] Texture sizing failed: tex=0x{TextureHash:X8} tri=0x{TriId:X8} idx={Index} fourCc={FourCC} data={DataLength}.", textureHash, info.TriId, textureIndex, image.FourCC, data.Length);
                    continue;
                }

                texture = new ResolvedTexture(width, height, image.FourCC, data);
                LogTextureSizing(textureHash, info.TriId, textureIndex, width, height, image.FourCC, data.Length);
                //Console.WriteLine($"[MDN] Texture resolved: tex=0x{textureHash:X8} tri=0x{info.TriId:X8} texIdx={textureIndex} imgIdx={imageIndex} (txn='{txnFile.VirtualPath}').");
                return true;
            }
        }

        return false;
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

                //Console.WriteLine($"Found {texture.Priority} with size {texture.DataSize}");
                data = texture.Data;
                //Console.WriteLine($"[MDN] Texture data selected: prio={texture.Priority} bytes={data.Length}.");
                return true;
            }
        }

        return false;
    }
    
    private List<DldFile> LoadDlds(IWorkspaceCatalog workspace, WorkspaceSnapshot snapshot)
    {
        foreach (var dldFile in snapshot.WithExtension(".dld"))
        {
            if (_dldCache.ContainsKey(dldFile.Path))
            {
                continue;
            }

            using var stream = workspace.OpenRead(dldFile.Path);
            _dldCache[dldFile.Path] = new DldFile(stream, workspace.Endianness);
        }

        foreach (var dlzFile in snapshot.WithExtension(".dlz"))
        {
            if (_dldCache.ContainsKey(dlzFile.Path))
            {
                continue;
            }

            using var input = workspace.OpenRead(dlzFile.Path);
            var dlz = new DlzFile(input, workspace.Endianness);
            using var unpacked = new MemoryStream();
            dlz.Unpack(unpacked);
            unpacked.Position = 0;
            _dldCache[dlzFile.Path] = new DldFile(unpacked, workspace.Endianness);
        }

        return _dldCache.Values.ToList();
    }

    private static void LogTextureMeta(uint texId, uint triId, int texIndex, int imgIndex, ushort width, ushort height, float scaleU, float scaleV, float offsetU, float offsetV, uint flag)
    {
        //Console.WriteLine($"[MDN] Texture meta: tex=0x{texId:X8} tri=0x{triId:X8} texIdx={texIndex} imgIdx={imgIndex} w={width} h={height} scale=({scaleU:0.####},{scaleV:0.####}) offset=({offsetU:0.####},{offsetV:0.####}) flag=0x{flag:X8}.");
    }

    private static void LogTextureSizing(uint texId, uint triId, int index, ushort width, ushort height, ushort fourCc, int dataLength)
    {
        int expected = GetExpectedDxtSize(width, height, fourCc);
        if (expected <= 0)
        {
            //Console.WriteLine($"[MDN] Texture size check: tex=0x{texId:X8} tri=0x{triId:X8} idx={index} fourCc={fourCc} w={width} h={height} data={dataLength} (unknown format).");
            return;
        }

        var status = expected == dataLength ? "OK" : "MISMATCH";
        //Console.WriteLine($"[MDN] Texture size check: tex=0x{texId:X8} tri=0x{triId:X8} idx={index} fourCc={fourCc} w={width} h={height} data={dataLength} expected={expected} => {status}.");
    }

    private static int GetExpectedDxtSize(int width, int height, ushort fourCc)
    {
        if (width <= 0 || height <= 0)
        {
            return -1;
        }

        int blockSize = fourCc switch
        {
            9 => 8,
            10 => 16,
            11 => 16,
            _ => -1
        };
        if (blockSize < 0)
        {
            return -1;
        }

        int blocksWide = (width + 3) / 4;
        int blocksHigh = (height + 3) / 4;
        return blocksWide * blocksHigh * blockSize;
    }

    private static bool TryNormalizeTextureDimensions(
        ushort primaryWidth,
        ushort primaryHeight,
        ushort secondaryWidth,
        ushort secondaryHeight,
        ushort fourCc,
        byte[] data,
        out ushort width,
        out ushort height,
        out byte[] normalizedData)
    {
        width = primaryWidth;
        height = primaryHeight;
        normalizedData = data;

        if (TryFitDimensionsByScaling(primaryWidth, primaryHeight, fourCc, data, out width, out height, out normalizedData))
        {
            return true;
        }

        if ((secondaryWidth != primaryWidth || secondaryHeight != primaryHeight)
            && TryFitDimensionsByScaling(secondaryWidth, secondaryHeight, fourCc, data, out width, out height, out normalizedData))
        {
            return true;
        }

        return false;
    }

    private static bool TryFitDimensionsByScaling(
        ushort seedWidth,
        ushort seedHeight,
        ushort fourCc,
        byte[] data,
        out ushort width,
        out ushort height,
        out byte[] normalizedData)
    {
        width = seedWidth;
        height = seedHeight;
        normalizedData = data;

        if (seedWidth == 0 || seedHeight == 0)
        {
            return false;
        }

        int bestW = 0;
        int bestH = 0;
        int bestExpected = -1;

        for (int step = -8; step <= 8; step++)
        {
            int w = ScalePow2(seedWidth, step);
            int h = ScalePow2(seedHeight, step);
            if (w <= 0 || h <= 0 || w > ushort.MaxValue || h > ushort.MaxValue)
            {
                continue;
            }

            int expected = GetExpectedDxtSize(w, h, fourCc);
            if (expected > 0)
            {
                if (data.Length == expected)
                {
                    width = (ushort)w;
                    height = (ushort)h;
                    normalizedData = data;
                    return true;
                }

                if (data.Length > expected)
                {
                    if (expected > bestExpected)
                    {
                        bestExpected = expected;
                        bestW = w;
                        bestH = h;
                    }
                }
            }
        }
        
        if (bestExpected > 0)
        {
            width = (ushort)bestW;
            height = (ushort)bestH;
            normalizedData = data.AsSpan(0, bestExpected).ToArray();
            return true;
        }

        return false;
    }

    private static int ScalePow2(int value, int step)
    {
        if (value <= 0)
        {
            return 0;
        }

        if (step >= 0)
        {
            long scaled = (long)value << step;
            return scaled > int.MaxValue ? 0 : (int)scaled;
        }

        int divisor = 1 << (-step);
        return Math.Max(1, value / divisor);
    }

}

public readonly record struct ResolvedTexture(ushort Width, ushort Height, ushort FourCC, byte[] Data);
