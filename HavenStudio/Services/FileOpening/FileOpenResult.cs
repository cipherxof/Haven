using System;

namespace HavenStudio.Services.FileOpening;

public enum FileOpenStatus
{
    Opened,
    Unsupported,
    Cancelled,
    Failed
}

public sealed record FileOpenResult(
    FileOpenStatus Status,
    string? Message = null,
    Exception? Exception = null)
{
    public static FileOpenResult Opened() => new(FileOpenStatus.Opened);
    public static FileOpenResult Unsupported(string message) => new(FileOpenStatus.Unsupported, message);
    public static FileOpenResult Cancelled() => new(FileOpenStatus.Cancelled);
    public static FileOpenResult Failed(string message, Exception exception) =>
        new(FileOpenStatus.Failed, message, exception);
}
