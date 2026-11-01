using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Formats.Lit;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Editors.Lighting;

public sealed class LitDocumentSession
{
    private long _editVersion;
    private LitDocumentSession(
        IWorkspaceCatalog workspace,
        WorkspacePath path,
        LitFile document,
        byte[] originalBytes)
    {
        Workspace = workspace;
        Path = path;
        Document = document;
        OriginalBytes = originalBytes;
    }

    public IWorkspaceCatalog Workspace { get; }
    public WorkspacePath Path { get; }
    public LitFile Document { get; }
    public byte[] OriginalBytes { get; private set; }
    public bool IsDirty { get; private set; }
    public string DisplayName => Path.FileName;
    public bool IsSkyPass => Path.FileName.Contains("sky", StringComparison.OrdinalIgnoreCase);

    public event Action? Changed;

    public static LitDocumentSession Load(IWorkspaceCatalog workspace, WorkspacePath path)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(path);
        var bytes = workspace.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        var document = LitFile.Read(stream);
        return new LitDocumentSession(workspace, path, document, bytes);
    }

    public void MarkDirty()
    {
        _editVersion++;
        IsDirty = true;
        Changed?.Invoke();
    }

    public void NotifyChanged() => Changed?.Invoke();

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var version = _editVersion;
        var bytes = Document.ToArray();
        await Task.Run(() => Workspace.Replace(Path, bytes), cancellationToken);
        // Resume on the caller's UI context before notifying scene/UI subscribers.
        OriginalBytes = bytes;
        IsDirty = _editVersion != version;
        Changed?.Invoke();
    }

    public void Save()
    {
        var bytes = Document.ToArray();
        Workspace.Replace(Path, bytes);
        OriginalBytes = bytes;
        IsDirty = false;
        Changed?.Invoke();
    }
}
