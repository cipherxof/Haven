using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Extensions;
using HavenStudio.Services;

namespace HavenStudio.Services.Workspace;

public interface IWorkspaceCatalog
{
    string RootPath { get; }
    Endianness Endianness { get; }
    WorkspaceSnapshot? Snapshot { get; }

    Task<WorkspaceSnapshot> ScanAsync(
        CancellationToken cancellationToken = default,
        IProgress<WorkspaceScanProgress>? progress = null);

    Stream OpenRead(WorkspacePath path);
    byte[] ReadAllBytes(WorkspacePath path);
    void Replace(WorkspacePath path, ReadOnlySpan<byte> data);
    ArchiveDumpService.ExtractSummary ExtractArchive(WorkspacePath archivePath, string outputFolder);
}
