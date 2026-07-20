namespace HavenStudio.Services.Workspace;

public sealed record WorkspaceFile(
    WorkspacePath Path,
    string RelativePath,
    long Length)
{
    public string Name => Path.FileName;
    public string Extension => Path.Extension;
    public bool IsArchiveEntry => Path.IsArchiveEntry;
    public bool IsArchiveContainer => !Path.IsArchiveEntry && Extension is ".qar" or ".dar";
}
