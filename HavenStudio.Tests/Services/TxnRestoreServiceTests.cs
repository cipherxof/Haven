using HavenStudio.Extensions;
using HavenStudio.Formats.Dds;
using HavenStudio.Formats.Dld;
using HavenStudio.Formats.Txn;
using HavenStudio.Services;
using HavenStudio.Services.Workspace;
using HavenStudio.Tests.TestSupport;

namespace HavenStudio.Tests.Services;

public sealed class TxnRestoreServiceTests
{
    [Fact]
    public async Task RestoreFromFolder_uses_hex_ids_directly_and_hashes_named_textures()
    {
        using var temp = new TempDirectory();
        var root = temp.GetPath("workspace");
        Directory.CreateDirectory(root);
        WriteEmptyTxn(Path.Combine(root, "stage.txn"));
        WriteEmptyDld(Path.Combine(root, "stage.dld"));

        var ddsFolder = temp.GetPath("textures");
        WriteDds(Path.Combine(ddsFolder, "0000abcd.dds"));
        WriteDds(Path.Combine(ddsFolder, "grass.dds"));

        var catalog = new WorkspaceCatalog(root, Endianness.Big);
        await catalog.ScanAsync();
        var txnPath = WorkspacePath.Physical(Path.Combine(root, "stage.txn"));

        var service = new TxnTextureEditorService(txnPath, catalog);
        var summary = service.RestoreFromFolder(ddsFolder);
        service.Save();

        Assert.Equal(2, summary.Total);
        Assert.Equal(2, summary.Restored);
        Assert.Empty(summary.Skipped);

        var grassHash = HavenStudio.Utils.String.HashString("grass");
        using var txnStream = new MemoryStream(File.ReadAllBytes(Path.Combine(root, "stage.txn")));
        var reloadedTxn = new TxnFile(txnStream, Endianness.Big);
        Assert.Equal(2, reloadedTxn.ImageInfo.Count);
        Assert.Equal(0xABCDu, reloadedTxn.ImageInfo[0].TexId);
        Assert.Equal(0xABCDu, reloadedTxn.ImageInfo[0].TriId);
        Assert.Equal(grassHash, reloadedTxn.ImageInfo[1].TexId);

        using var dldStream = new MemoryStream(File.ReadAllBytes(Path.Combine(root, "stage.dld")));
        var reloadedDld = new DldFile(dldStream, Endianness.Big);
        Assert.Contains(reloadedDld.Textures, t => t.HashId == 0xABCDu && t.Priority == 0 && t.EntryNumber == 0);
        Assert.Contains(reloadedDld.Textures, t => t.HashId == grassHash && t.Priority == 0 && t.EntryNumber == 1);
    }

    [Fact]
    public async Task RestoreFromFolder_creates_a_dld_beside_a_physical_txn_when_none_exists()
    {
        using var temp = new TempDirectory();
        var root = temp.GetPath("workspace");
        Directory.CreateDirectory(root);
        WriteEmptyTxn(Path.Combine(root, "stage.txn"));

        var ddsFolder = temp.GetPath("textures");
        WriteDds(Path.Combine(ddsFolder, "cafe.dds"));

        var catalog = new WorkspaceCatalog(root, Endianness.Big);
        await catalog.ScanAsync();
        var txnPath = WorkspacePath.Physical(Path.Combine(root, "stage.txn"));

        var service = new TxnTextureEditorService(txnPath, catalog);
        service.RestoreFromFolder(ddsFolder);
        service.Save();

        var createdDld = Path.Combine(root, "stage.dld");
        Assert.True(File.Exists(createdDld));
        using var dldStream = new MemoryStream(File.ReadAllBytes(createdDld));
        var reloadedDld = new DldFile(dldStream, Endianness.Big);
        Assert.Contains(reloadedDld.Textures, t => t.HashId == 0xCAFEu);
    }

    [Fact]
    public async Task RestoreFromFolder_rejects_a_folder_without_textures()
    {
        using var temp = new TempDirectory();
        var root = temp.GetPath("workspace");
        Directory.CreateDirectory(root);
        WriteEmptyTxn(Path.Combine(root, "stage.txn"));
        WriteEmptyDld(Path.Combine(root, "stage.dld"));
        var emptyFolder = temp.GetPath("empty");
        Directory.CreateDirectory(emptyFolder);

        var catalog = new WorkspaceCatalog(root, Endianness.Big);
        await catalog.ScanAsync();
        var service = new TxnTextureEditorService(
            WorkspacePath.Physical(Path.Combine(root, "stage.txn")),
            catalog);

        Assert.Throws<InvalidDataException>(() => service.RestoreFromFolder(emptyFolder));
    }

    private static void WriteEmptyTxn(string path)
    {
        var txn = new TxnFile();
        using var stream = File.Create(path);
        txn.Save(stream, Endianness.Big);
    }

    private static void WriteEmptyDld(string path)
    {
        var dld = new DldFile();
        using var stream = File.Create(path);
        dld.Save(stream, Endianness.Big);
    }

    private static void WriteDds(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        DdsFile.Create(path, height: 4, width: 4, fourCc: "DXT1", mipMapCount: 1, data: new byte[8]);
    }
}
