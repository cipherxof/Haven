using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Editors;

public static class OctocamoMappingSave
{
    public static async Task<string> SaveAsync(IWorkspaceCatalog workspace, WorkspacePath path,
        OctocamoSurfaceCatalog catalog, CancellationToken cancellationToken = default)
    {
        var edited = catalog.GetMappingBytes();
        var expected = catalog.GetOriginalMappingBytes();
        var backup = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Always compare fresh bytes, even if an external tool preserved
            // the archive's size and modification timestamp.
            if (path.IsArchiveEntry && workspace is WorkspaceCatalog concrete)
                concrete.InvalidateArchive(path.PhysicalPath);
            if (!workspace.ReadAllBytes(path).AsSpan().SequenceEqual(expected))
                throw new IOException("The OctoCamo table changed on disk since loading. Reload the stage before saving; no changes were overwritten.");
            var originalFile = File.ReadAllBytes(path.PhysicalPath);
            var backupPath = $"{path.PhysicalPath}.octocamo-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bak";
            using (var backupFile = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                backupFile.Write(originalFile);
            cancellationToken.ThrowIfCancellationRequested();
            workspace.Replace(path, edited);
            if (!workspace.ReadAllBytes(path).AsSpan().SequenceEqual(edited))
                throw new IOException($"OctoCamo save read-back failed. Original file retained at {backupPath}.");
            return backupPath;
        }, cancellationToken);
        // If UI edits occur during I/O, retain them as dirty relative to this save.
        catalog.AcceptMappingSave(edited);
        return backup;
    }
}
