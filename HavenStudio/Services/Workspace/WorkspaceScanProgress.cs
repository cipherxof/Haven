namespace HavenStudio.Services.Workspace;

public sealed record WorkspaceScanProgress(int PhysicalFilesScanned, int ArchiveEntriesIndexed, string CurrentPath);
