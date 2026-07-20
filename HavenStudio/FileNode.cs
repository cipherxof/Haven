using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media;
using HavenStudio.Services.Workspace;

namespace HavenStudio;

public sealed class FileNode
{
    public FileNode(string name, WorkspacePath workspacePath, ObservableCollection<FileNode> children, bool isDirectory)
    {
        Name = name;
        WorkspacePath = workspacePath;
        FullPath = workspacePath.ToLegacyString();
        Children = children;
        IsDirectory = isDirectory;
        IsFile = !isDirectory;

        Extension = workspacePath.Extension;
        ArchivePath = workspacePath.IsArchiveEntry ? workspacePath.PhysicalPath : null;
        ArchiveExtension = GetArchiveExtension(ArchivePath);
        IsArchive = Extension is ".qar" or ".dar";
        IsArchiveEntry = ArchiveExtension is ".qar" or ".dar";
        IsTxn = Extension == ".txn";
        IsDlz = Extension == ".dlz";
        IsGcx = Extension == ".gcx";
        IsGeom = Extension == ".geom";
        CanCreateGeom = (IsDirectory && !IsArchive) || (IsGeom && !IsArchiveEntry);
        IsTextConfig = Extension is ".cnf" or ".nni";
        IsVirtual = workspacePath.IsArchiveEntry;
        HasContextMenu = IsArchive || IsArchiveEntry || IsTxn || IsDlz || IsGcx || IsGeom || CanCreateGeom;

        IconData = GetIconData(isDirectory, Extension);
        IconWidth = isDirectory ? 14 : 14;
        IconBrush = GetIconBrush(isDirectory, Extension);
        IconFill = GetIconFill(isDirectory, Extension);
        IconStrokeThickness = GetIconStrokeThickness(isDirectory, Extension);
    }

    public string Name { get; }
    public WorkspacePath WorkspacePath { get; }
    public string FullPath { get; }
    public ObservableCollection<FileNode> Children { get; }
    public bool IsDirectory { get; }
    public bool IsFile { get; }
    public string Extension { get; }
    public string? ArchivePath { get; }
    public string ArchiveExtension { get; }
    public bool IsArchive { get; }
    public bool IsArchiveEntry { get; }
    public bool IsTxn { get; }
    public bool IsDlz { get; }
    public bool IsGcx { get; }
    public bool IsGeom { get; }
    public bool CanCreateGeom { get; }
    public bool IsTextConfig { get; }
    public bool IsVirtual { get; }
    public bool HasContextMenu { get; }
    public string IconData { get; }
    public double IconWidth { get; }
    public IBrush IconBrush { get; }
    public IBrush IconFill { get; }
    public double IconStrokeThickness { get; }

    private static string GetArchiveExtension(string? archivePath)
    {
        return string.IsNullOrWhiteSpace(archivePath) ? string.Empty : Path.GetExtension(archivePath).ToLowerInvariant();
    }

    private static string GetIconData(bool isDirectory, string extension)
    {
        if (isDirectory)
        {
            return "M2,6 H7 L9,8 H18 V18 H2 Z";
        }

        return extension switch
        {
            ".mdn" => "M4,6 L10,2 L16,6 L10,10 Z M4,6 L4,14 L10,18 L10,10 M16,6 L16,14 L10,18",
            ".geom" => "M4,6 L10,2 L16,6 L10,10 Z M4,6 L4,14 L10,18 L10,10 M16,6 L16,14 L10,18",
            ".txn" => "M3,3 H17 V13 H3 Z M5,11 L8,8 L11,11 L14,7 L17,11 V13 H3 V11 Z",
            ".dds" => "M3,3 H17 V13 H3 Z M5,11 L8,8 L11,11 L14,7 L17,11 V13 H3 V11 Z",
            ".gcx" => "M3,2 H9 L13,6 V18 H3 Z M9,2 V6 H13 M5,9 H11 M5,12 H11 M5,15 H10",
            ".cnf" => "M3,2 H9 L13,6 V18 H3 Z M9,2 V6 H13 M5,8 H11 M5,11 H11 M5,14 H11",
            ".nni" => "M3,2 H9 L13,6 V18 H3 Z M9,2 V6 H13 M5,8 H11 M5,11 H11 M5,14 H11",
            ".dlz" => "M3,3 H17 V14 H3 Z M5,5 H15 V6 H5 Z M5,8 H15 V9 H5 Z M5,11 H11 V12 H5 Z",
            ".dld" => "M3,3 H17 V14 H3 Z M5,5 H15 V6 H5 Z M5,8 H15 V9 H5 Z M5,11 H11 V12 H5 Z",
            ".qar" => "M3,4 H17 V16 H3 Z M3,8 H17 M3,12 H17",
            ".dar" => "M3,4 H17 V16 H3 Z M3,8 H17 M3,12 H17",
            _ => "M3,2 H9 L13,6 V18 H3 Z M9,2 V6 H13"
        };
    }

    private static IBrush GetIconBrush(bool isDirectory, string extension)
    {
        if (isDirectory)
        {
            return new SolidColorBrush(Color.Parse("#9DA0A6"));
        }

        return extension switch
        {
            ".mdn" => new SolidColorBrush(Color.Parse("#9B6DFF")),
            ".geom" => new SolidColorBrush(Color.Parse("#4DA3FF")),
            ".txn" => new SolidColorBrush(Color.Parse("#39C46A")),
            ".dds" => new SolidColorBrush(Color.Parse("#39C46A")),
            ".gcx" => new SolidColorBrush(Color.Parse("#F4C542")),
            ".cnf" => new SolidColorBrush(Color.Parse("#6FB2FF")),
            ".nni" => new SolidColorBrush(Color.Parse("#6FB2FF")),
            ".dlz" => new SolidColorBrush(Color.Parse("#E05656")),
            ".dld" => new SolidColorBrush(Color.Parse("#E05656")),
            ".qar" => new SolidColorBrush(Color.Parse("#FF9E2C")),
            ".dar" => new SolidColorBrush(Color.Parse("#5B9BD5")),
            _ => new SolidColorBrush(Color.Parse("#D4D4D4"))
        };
    }

    private static IBrush GetIconFill(bool isDirectory, string extension)
    {
        if (isDirectory)
        {
            return new SolidColorBrush(Color.Parse("#9DA0A6"));
        }

        return extension switch
        {
            ".mdn" => Brushes.Transparent,
            ".geom" => Brushes.Transparent,
            ".txn" => new SolidColorBrush(Color.Parse("#39C46A")),
            ".dds" => new SolidColorBrush(Color.Parse("#39C46A")),
            ".gcx" => Brushes.Transparent,
            ".cnf" => Brushes.Transparent,
            ".nni" => Brushes.Transparent,
            ".dlz" => new SolidColorBrush(Color.Parse("#E05656")),
            ".dld" => new SolidColorBrush(Color.Parse("#E05656")),
            ".qar" => new SolidColorBrush(Color.Parse("#FF9E2C")),
            ".dar" => new SolidColorBrush(Color.Parse("#5B9BD5")),
            _ => new SolidColorBrush(Color.Parse("#D4D4D4"))
        };
    }

    private static double GetIconStrokeThickness(bool isDirectory, string extension)
    {
        if (isDirectory)
        {
            return 0;
        }

        return extension switch
        {
            ".mdn" => 1.4,
            ".geom" => 1.4,
            ".gcx" => 1.2,
            ".cnf" => 1.2,
            ".nni" => 1.2,
            _ => 0
        };
    }
}
