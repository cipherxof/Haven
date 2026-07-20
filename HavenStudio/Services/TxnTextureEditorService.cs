using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HavenStudio.Extensions;
using HavenStudio.Formats.Dds;
using HavenStudio.Formats.Dld;
using HavenStudio.Formats.Dlz;
using HavenStudio.Formats.Txn;
using HavenStudio.Services.Workspace;
using HavenStudio.Utils;

namespace HavenStudio.Services;

public sealed class TxnTextureEditorService
{
    private readonly Endianness _endianness;
    private readonly WorkspacePath _txnWorkspacePath;
    private readonly IWorkspaceCatalog _workspace;
    private readonly TxnFile _txn;
    private readonly List<ContainerRef> _containers = new();
    private readonly List<TxnTextureEntry> _entries = new();

    public IReadOnlyList<TxnTextureEntry> Entries => _entries;
    public IReadOnlyList<ContainerRef> Containers => _containers;

    public TxnTextureEditorService(WorkspacePath txnPath, IWorkspaceCatalog workspace)
    {
        ArgumentNullException.ThrowIfNull(txnPath);
        ArgumentNullException.ThrowIfNull(workspace);
        _txnWorkspacePath = txnPath;
        _workspace = workspace;
        _endianness = workspace.Endianness;
        using var stream = workspace.OpenRead(txnPath);
        _txn = new TxnFile(stream, _endianness);

        LoadContainers();
        RebuildEntries();
    }

    public void ReplaceTexture(int entryIndex, string ddsPath)
    {
        if (entryIndex < 0 || entryIndex >= _entries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(entryIndex));
        }

        var dds = DdsFile.Read(ddsPath);
        var entry = _entries[entryIndex];

        if (!TryGetTxnFourCc(dds.FourCc, out var txnFourCc))
        {
            throw new InvalidDataException($"Unsupported DDS format '{dds.FourCc}'.");
        }

        entry.Image.Width = (ushort)dds.Width;
        entry.Image.Height = (ushort)dds.Height;
        entry.Image.FourCC = txnFourCc;
        entry.Info.Width = (ushort)dds.Width;
        entry.Info.Height = (ushort)dds.Height;

        var mainContainer = entry.MainContainer
            ?? _containers.FirstOrDefault()
            ?? throw new InvalidOperationException("No DLD/DLZ containers were found.");
        UpsertDldTexture(mainContainer, entry.Info.TriId, entry.Index, DldPriority.Main, dds.MainData, dds.MipMapCount);

        if (dds.MipData.Length > 0)
        {
            var mipContainer = entry.MipContainer ?? mainContainer;
            UpsertDldTexture(mipContainer, entry.Info.TriId, entry.Index, DldPriority.Mipmaps, dds.MipData, dds.MipMapCount);
        }
        else
        {
            RemoveDldTexture(entry.Info.TriId, entry.Index, DldPriority.Mipmaps);
        }

        RebuildEntries();
    }

    public void DeleteTexture(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= _entries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(entryIndex));
        }

        var entry = _entries[entryIndex];
        var removedInfo = _txn.ImageInfo[entryIndex];
        _txn.ImageInfo.RemoveAt(entryIndex);
        _txn.IndexLookup.Remove(removedInfo);

        RemoveDldTexture(entry.Info.TriId, entryIndex, DldPriority.Main);
        RemoveDldTexture(entry.Info.TriId, entryIndex, DldPriority.Mipmaps);
        DecrementTextureIndices(entry.Info.TriId, entryIndex);

        if (!_txn.ImageInfo.Any(info => _txn.GetIndex(info) == entry.ImageIndex))
        {
            _txn.Images.RemoveAt(entry.ImageIndex);
        }

        RebuildIndexLookup();
        RebuildEntries();
    }

    public void AddTexture(string ddsPath, uint texId, uint triId, string mainContainerPath, string? mipContainerPath)
    {
        var dds = DdsFile.Read(ddsPath);
        if (!TryGetTxnFourCc(dds.FourCc, out var txnFourCc))
        {
            throw new InvalidDataException($"Unsupported DDS format '{dds.FourCc}'.");
        }

        var mainContainer = ResolveContainer(mainContainerPath);
        var mipContainer = string.IsNullOrWhiteSpace(mipContainerPath) ? null : ResolveContainer(mipContainerPath);

        var image = new TxnImage((ushort)dds.Width, (ushort)dds.Height, txnFourCc, 1, 0, 0);
        _txn.Images.Add(image);

        var info = new TxnInfo(
            texId,
            triId,
            (ushort)dds.Width,
            (ushort)dds.Height,
            0,
            0,
            0,
            1.0f,
            1.0f,
            0.0f,
            0.0f);
        _txn.ImageInfo.Add(info);
        _txn.IndexLookup[info] = _txn.Images.Count - 1;

        int entryIndex = _txn.ImageInfo.Count - 1;
        UpsertDldTexture(mainContainer, triId, entryIndex, DldPriority.Main, dds.MainData, dds.MipMapCount);

        if (dds.MipData.Length > 0)
        {
            UpsertDldTexture(mipContainer ?? mainContainer, triId, entryIndex, DldPriority.Mipmaps, dds.MipData, dds.MipMapCount);
        }

        RebuildEntries();
    }

    /// <summary>
    /// Rebuilds the TXN image tables and their DLD/DLZ texture payloads from every
    /// <c>.dds</c> file in <paramref name="folder"/>. A file named with a hexadecimal
    /// hash uses that value as its texture id; any other name is hashed. The caller
    /// must still invoke <see cref="Save"/> to persist the result.
    /// </summary>
    public RestoreSummary RestoreFromFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Folder is required.", nameof(folder));
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException($"Folder '{folder}' was not found.");

        var ddsFiles = Directory
            .EnumerateFiles(folder, "*.dds", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ddsFiles.Count == 0)
        {
            throw new InvalidDataException("The selected folder contains no .dds textures.");
        }

        var container = SelectRestoreContainer();

        _txn.Images.Clear();
        _txn.ImageInfo.Clear();
        _txn.IndexLookup.Clear();

        var skipped = new List<string>();
        foreach (var ddsPath in ddsFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(ddsPath);
            var id = ResolveTextureId(baseName);

            DdsTextureData dds;
            try
            {
                dds = DdsFile.Read(ddsPath);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException)
            {
                skipped.Add($"{Path.GetFileName(ddsPath)}: {ex.Message}");
                continue;
            }

            if (!TryGetTxnFourCc(dds.FourCc, out var txnFourCc))
            {
                skipped.Add($"{Path.GetFileName(ddsPath)}: unsupported format '{dds.FourCc}'");
                continue;
            }

            var image = new TxnImage((ushort)dds.Width, (ushort)dds.Height, txnFourCc, 1, 0, 0);
            _txn.Images.Add(image);

            var info = new TxnInfo(
                id,
                id,
                (ushort)dds.Width,
                (ushort)dds.Height,
                0,
                0,
                0,
                1.0f,
                1.0f,
                0.0f,
                0.0f);
            _txn.ImageInfo.Add(info);
            int entryIndex = _txn.ImageInfo.Count - 1;
            _txn.IndexLookup[info] = _txn.Images.Count - 1;

            UpsertDldTexture(container, id, entryIndex, DldPriority.Main, dds.MainData, dds.MipMapCount);
            if (dds.MipData.Length > 0)
            {
                UpsertDldTexture(container, id, entryIndex, DldPriority.Mipmaps, dds.MipData, dds.MipMapCount);
            }
        }

        RebuildEntries();
        return new RestoreSummary(ddsFiles.Count, _txn.ImageInfo.Count, skipped);
    }

    private static uint ResolveTextureId(string baseName)
    {
        if (IsHexId(baseName, out var value))
        {
            return value;
        }

        return Utils.String.HashString(baseName);
    }

    private static bool IsHexId(string value, out uint hash)
    {
        hash = 0;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return false;
            }
        }

        return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hash);
    }

    private ContainerRef SelectRestoreContainer()
    {
        var sameDirectory = _containers
            .Where(container => container.WorkspacePath.IsSameDirectoryAs(_txnWorkspacePath))
            .ToList();
        var pool = sameDirectory.Count > 0 ? sameDirectory : _containers;
        var chosen = pool.FirstOrDefault(container => !container.IsDlz) ?? pool.FirstOrDefault();
        if (chosen != null)
        {
            return chosen;
        }

        if (_txnWorkspacePath.IsArchiveEntry)
        {
            throw new InvalidOperationException(
                "No DLD/DLZ container was found for this TXN. Add one to the archive before restoring.");
        }

        var directory = Path.GetDirectoryName(_txnWorkspacePath.PhysicalPath)
            ?? throw new InvalidOperationException("The TXN path has no directory.");
        var stem = Path.GetFileNameWithoutExtension(_txnWorkspacePath.PhysicalPath);
        var newContainerPath = WorkspacePath.Physical(Path.Combine(directory, $"{stem}.dld"));
        var newContainer = new ContainerRef(newContainerPath, isDlz: false, new DldFile()) { IsDirty = true };
        _containers.Add(newContainer);
        return newContainer;
    }

    public void Save()
    {
        SaveTxn();

        foreach (var container in _containers.Where(container => container.IsDirty))
        {
            byte[] payload;
            if (container.IsDlz)
            {
                payload = BuildDlzBytes(container);
            }
            else
            {
                payload = BuildDldBytes(container.Dld);
            }

            _workspace.Replace(container.WorkspacePath, payload);

            container.IsDirty = false;
        }
    }

    private void LoadContainers()
    {
        var snapshot = _workspace.Snapshot;
        if (snapshot is null)
        {
            return;
        }

        foreach (var dldFile in snapshot.WithExtension(".dld"))
        {
            using var stream = _workspace.OpenRead(dldFile.Path);
            _containers.Add(new ContainerRef(
                dldFile.Path,
                isDlz: false,
                new DldFile(stream, _endianness)));
        }

        foreach (var dlzFile in snapshot.WithExtension(".dlz"))
        {
            using var stream = _workspace.OpenRead(dlzFile.Path);
            var dlz = new DlzFile(stream, _endianness);
            using var unpacked = new MemoryStream();
            dlz.Unpack(unpacked);
            unpacked.Position = 0;
            var dld = new DldFile(unpacked, _endianness);
            _containers.Add(new ContainerRef(dlzFile.Path, isDlz: true, dld));
        }
    }

    private void RebuildEntries()
    {
        _entries.Clear();

        for (int i = 0; i < _txn.ImageInfo.Count; i++)
        {
            var info = _txn.ImageInfo[i];
            int imageIndex = _txn.GetIndex(info);
            if (_txn.Images.Count == 0)
            {
                continue;
            }

            if (imageIndex < 0 || imageIndex >= _txn.Images.Count)
            {
                imageIndex = Math.Clamp(i, 0, _txn.Images.Count - 1);
            }

            var image = _txn.Images[imageIndex];
            var main = FindTexture(info.TriId, i, DldPriority.Main);
            var mip = FindTexture(info.TriId, i, DldPriority.Mipmaps);

            _entries.Add(new TxnTextureEntry(
                i,
                info,
                image,
                imageIndex,
                ResolveTextureName(info.TexId),
                main.Texture,
                main.Container,
                mip.Texture,
                mip.Container));
        }
    }

    private string ResolveTextureName(uint hash)
    {
        if (DictionaryFile.TryGetLookupName(hash, out var name))
        {
            return name;
        }

        return hash.ToString("X8");
    }

    private ContainerRef ResolveContainer(string path)
    {
        return _containers.FirstOrDefault(c =>
            string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Container '{path}' is not loaded.");
    }

    private void RebuildIndexLookup()
    {
        _txn.IndexLookup.Clear();
        for (int i = 0; i < _txn.ImageInfo.Count; i++)
        {
            var info = _txn.ImageInfo[i];
            if (_txn.Images.Count == 0)
            {
                _txn.IndexLookup[info] = -1;
                continue;
            }

            int imageIndex = i < _txn.Images.Count ? i : _txn.Images.Count - 1;
            _txn.IndexLookup[info] = imageIndex;
        }
    }

    private (DldTexture? Texture, ContainerRef? Container) FindTexture(uint hashId, int entryNumber, DldPriority priority)
    {
        foreach (var container in _containers)
        {
            var texture = container.Dld.FindTexture(hashId, entryNumber, priority);
            if (texture != null)
            {
                return (texture, container);
            }
        }

        return (null, null);
    }

    private void UpsertDldTexture(ContainerRef container, uint hashId, int entryNumber, DldPriority priority, byte[] data, int mipMapCount)
    {
        var existing = container.Dld.FindTexture(hashId, entryNumber, priority);
        if (existing != null)
        {
            existing.Data = data;
            existing.DataSize = (uint)data.Length;
            existing.ParentDataSize = (uint)data.Length;
            existing.MipmapCount = (uint)Math.Max(mipMapCount, 1);
            existing.Alignment = 0x10;
            container.IsDirty = true;
            return;
        }

        var texture = new DldTexture(
            type: 0x91,
            priority,
            hashId,
            (uint)data.Length,
            (uint)data.Length,
            (uint)Math.Max(mipMapCount, 1),
            (uint)entryNumber,
            data);
        container.Dld.Textures.Add(texture);
        container.IsDirty = true;
    }

    private void RemoveDldTexture(uint hashId, int entryNumber, DldPriority priority)
    {
        foreach (var container in _containers)
        {
            var texture = container.Dld.FindTexture(hashId, entryNumber, priority);
            if (texture == null)
            {
                continue;
            }

            container.Dld.Textures.Remove(texture);
            container.IsDirty = true;
        }
    }

    private void DecrementTextureIndices(uint hashId, int removedIndex)
    {
        foreach (var container in _containers)
        {
            foreach (var texture in container.Dld.Textures)
            {
                if (texture.HashId == hashId && texture.EntryNumber > removedIndex)
                {
                    texture.EntryNumber -= 1;
                    container.IsDirty = true;
                }
            }
        }
    }

    private byte[] BuildDldBytes(DldFile dld)
    {
        using var memory = new MemoryStream();
        dld.Save(memory, _endianness);
        return memory.ToArray();
    }

    private byte[] BuildDlzBytes(ContainerRef container)
    {
        var dldBytes = BuildDldBytes(container.Dld);

        var chunks = BuildDlzChunks(dldBytes);
        var dlz = new DlzFile(chunks);
        using var memory = new MemoryStream();
        dlz.Save(memory, _endianness);
        return memory.ToArray();
    }

    private static List<DlzDataContainer> BuildDlzChunks(byte[] dldBytes)
    {
        var chunks = new List<DlzDataContainer>();
        int offset = 0;

        while (offset < dldBytes.Length)
        {
            int chunkSize = Math.Min(60000, dldBytes.Length - offset);
            byte[] chunkData;
            byte[] compressed;

            while (true)
            {
                chunkData = new byte[chunkSize];
                Buffer.BlockCopy(dldBytes, offset, chunkData, 0, chunkSize);
                compressed = Compression.DeflateBuffer(chunkData);

                if (compressed.Length <= ushort.MaxValue)
                {
                    break;
                }

                chunkSize /= 2;
                if (chunkSize <= 1024)
                {
                    throw new InvalidOperationException("Unable to pack DLZ data: compressed chunk too large.");
                }
            }

            chunks.Add(new DlzDataContainer(compressed.Length, chunkData.Length, compressed));
            offset += chunkSize;
        }

        return chunks;
    }

    private static bool TryGetTxnFourCc(string fourCc, out ushort value)
    {
        value = fourCc switch
        {
            "DXT1" => 9,
            "DXT3" => 10,
            "DXT5" => 11,
            _ => 0
        };

        return value != 0;
    }

    private void SaveTxn()
    {
        using var memory = new MemoryStream();
        _txn.Save(memory, _endianness);
        _workspace.Replace(_txnWorkspacePath, memory.ToArray());
    }

    public sealed record RestoreSummary(int Total, int Restored, IReadOnlyList<string> Skipped);

    public sealed class ContainerRef
    {
        public ContainerRef(WorkspacePath path, bool isDlz, DldFile dld)
        {
            WorkspacePath = path;
            Path = path.ToLegacyString();
            IsDlz = isDlz;
            Dld = dld;
            IsVirtual = path.IsArchiveEntry;
        }

        public WorkspacePath WorkspacePath { get; }
        public string Path { get; }
        public string Name => WorkspacePath.FileName;
        public bool IsDlz { get; }
        public bool IsVirtual { get; }
        public DldFile Dld { get; }
        public bool IsDirty { get; set; }
    }

    public sealed class TxnTextureEntry
    {
        public TxnTextureEntry(
            int index,
            TxnInfo info,
            TxnImage image,
            int imageIndex,
            string displayName,
            DldTexture? mainTexture,
            ContainerRef? mainContainer,
            DldTexture? mipTexture,
            ContainerRef? mipContainer)
        {
            Index = index;
            Info = info;
            Image = image;
            ImageIndex = imageIndex;
            DisplayName = displayName;
            MainTexture = mainTexture;
            MainContainer = mainContainer;
            MipTexture = mipTexture;
            MipContainer = mipContainer;
        }

        public int Index { get; }
        public TxnInfo Info { get; }
        public TxnImage Image { get; }
        public int ImageIndex { get; }
        public string DisplayName { get; }
        public DldTexture? MainTexture { get; }
        public ContainerRef? MainContainer { get; }
        public DldTexture? MipTexture { get; }
        public ContainerRef? MipContainer { get; }

        public string Resolution => $"{(Info.Width > 0 ? Info.Width : Image.Width)}x{(Info.Height > 0 ? Info.Height : Image.Height)}";
        public string Format => Image.FourCC switch
        {
            9 => "DXT1",
            10 => "DXT3",
            11 => "DXT5",
            _ => $"FC{Image.FourCC}"
        };
        public string HashText => $"{Info.TexId:X8} / {Info.TriId:X8}";
        public string MainContainerText => MainContainer?.Name ?? "(none)";
        public string MipContainerText => MipContainer?.Name ?? "(none)";
    }
}
