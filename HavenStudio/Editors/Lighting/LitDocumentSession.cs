using System;
using System.IO;
using HavenStudio.Formats.Lit;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Editors.Lighting;

public sealed class LitDocumentSession
{
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

    /// <summary>
    /// Konami archives can contain a tiny generic MGS4_Preview.lt3 beside the real
    /// stage LT3. It is useful for asset preview, but must never drive stage lighting
    /// or the projected shadow direction.
    /// </summary>
    public bool IsPreviewPass => Path.FileName.Contains("preview", StringComparison.OrdinalIgnoreCase);

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
        IsDirty = true;
        Changed?.Invoke();
    }

    public void NotifyChanged() => Changed?.Invoke();

    public void Save()
    {
        var bytes = Document.ToArray();
        Workspace.Replace(Path, bytes);
        OriginalBytes = bytes;
        IsDirty = false;
        Changed?.Invoke();
    }
}
