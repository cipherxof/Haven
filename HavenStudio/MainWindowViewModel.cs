using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using HavenStudio.Editors;
using HavenStudio.Rendering;
using HavenStudio.Services;
using HavenStudio.Services.Workspace;
using Serilog;

namespace HavenStudio;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly ILogger _log = Log.ForContext<MainWindowViewModel>();

    public SceneHost SceneHost { get; } = new();
    public Avalonia3DControl.OpenGL3DControl ViewportControl => SceneHost.ViewportControl;
    public ObservableCollection<FileNode> RootItems { get; } = new();
    public GcxEditorViewModel GcxEditor { get; }
    public CollisionEditorViewModel CollisionEditor { get; }
    public MapEditorViewModel MapEditor { get; }
    public MinimapViewModel Minimap { get; } = new();
    public IWorkspaceCatalog? Workspace => _workspaceSession.Catalog;
    public WorkspaceSnapshot? WorkspaceSnapshot => Workspace?.Snapshot ?? _workspaceSession.Snapshot;

    private int _selectedTabIndex;
    private string _rootFolderName = string.Empty;
    private string? _rootFolderPath;
    private readonly WorkspaceSession _workspaceSession = new();
    private long _workspaceLoadGeneration;
    private bool _isWorkspaceScanning;
    private WorkspaceScanProgress? _workspaceScanProgress;
    private DispatcherTimer? _statusTimer;
    private bool _disposed;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (_selectedTabIndex == value)
            {
                return;
            }

            _selectedTabIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMapSelected));
            OnPropertyChanged(nameof(IsScriptingSelected));
        }
    }

    public bool IsMapSelected => SelectedTabIndex == 0;
    public bool IsScriptingSelected => SelectedTabIndex == 1;

    public string StatusLeftText => BuildStatusLeftText();
    public string RootFolderName => _rootFolderName;
    public string? RootFolderPath => _rootFolderPath;
    public bool IsWorkspaceScanning => _isWorkspaceScanning;
    public WorkspaceScanProgress? WorkspaceScanProgress => _workspaceScanProgress;
    public MainWindowViewModel()
    {
        GcxEditor = new GcxEditorViewModel(SceneHost);
        CollisionEditor = new CollisionEditorViewModel(SceneHost);
        MapEditor = new MapEditorViewModel(SceneHost, CollisionEditor, GcxEditor);
        StartStatusTimer();
    }

    private void StartStatusTimer()
    {
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };

        _statusTimer.Tick += OnStatusTimerTick;
        _statusTimer.Start();
    }

    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(StatusLeftText));
    }

    private string BuildStatusLeftText()
    {
        const string baseText = "";
        if (!IsMapSelected)
        {
            return baseText;
        }

        var pos = SceneHost.Scene.Camera.Position;
        return $"{baseText}{pos.X:0.00}, {pos.Y:0.00}, {pos.Z:0.00}";
    }

    public void AddModel(string modelType)
    {
        SceneHost.AddModel(modelType);
    }

    public void ClearModels()
    {
        SceneHost.ClearModels();
    }

    public void RefreshWorkspaceTree()
    {
        var snapshot = Workspace?.Snapshot;
        if (snapshot == null)
        {
            return;
        }

        var children = BuildNodes(snapshot);
        RootItems.Clear();
        foreach (var child in children)
        {
            RootItems.Add(child);
        }
        OnPropertyChanged(nameof(WorkspaceSnapshot));
    }

    public async Task LoadFromFolderAsync(string folderPath)
    {
        var rootDirectory = new DirectoryInfo(folderPath);
        var loadGeneration = Interlocked.Increment(ref _workspaceLoadGeneration);
        _isWorkspaceScanning = true;
        _workspaceScanProgress = null;
        OnPropertyChanged(nameof(IsWorkspaceScanning));
        OnPropertyChanged(nameof(WorkspaceScanProgress));

        var catalog = new WorkspaceCatalog(
            folderPath,
            Extensions.EndianBinaryReader.DefaultEndianness);
        var progress = new Progress<WorkspaceScanProgress>(value =>
        {
            if (loadGeneration != Volatile.Read(ref _workspaceLoadGeneration))
            {
                return;
            }

            _workspaceScanProgress = value;
            OnPropertyChanged(nameof(WorkspaceScanProgress));
        });

        bool published;
        try
        {
            published = await _workspaceSession.OpenAsync(catalog, progress: progress);
        }
        finally
        {
            if (loadGeneration == Volatile.Read(ref _workspaceLoadGeneration))
            {
                _isWorkspaceScanning = false;
                OnPropertyChanged(nameof(IsWorkspaceScanning));
            }
        }

        if (!published || loadGeneration != Volatile.Read(ref _workspaceLoadGeneration))
        {
            return;
        }

        var snapshot = _workspaceSession.Snapshot!;
        var children = BuildNodes(snapshot);

        RootItems.Clear();
        foreach (var child in children)
        {
            RootItems.Add(child);
        }

        _rootFolderName = rootDirectory.Name;
        _rootFolderPath = folderPath;
        OnPropertyChanged(nameof(RootFolderName));

        GcxEditor.SetWorkspace(catalog, snapshot);
        CollisionEditor.SetWorkspace(catalog);
        OnPropertyChanged(nameof(Workspace));
        OnPropertyChanged(nameof(WorkspaceSnapshot));

        // Load geom first so placed objects can resolve positions from effects
        var geomPath = snapshot.WithExtension(".geom").FirstOrDefault()?.Path;
        _log.Debug("Loading GEOM from: {GeomPath}", geomPath?.ToString() ?? "(not found)");
        await CollisionEditor.LoadFromWorkspacePathAsync(geomPath);
        _log.Debug("GEOM loaded: {HasGeom}", CollisionEditor.GeomFile != null);

        if (loadGeneration != Volatile.Read(ref _workspaceLoadGeneration))
        {
            return;
        }

        GcxEditor.SetGeomFile(CollisionEditor.GeomFile);

        var gcxPath = snapshot.WithExtension(".gcx").FirstOrDefault()?.Path;
        await GcxEditor.LoadFromWorkspacePathAsync(gcxPath);

        if (loadGeneration != Volatile.Read(ref _workspaceLoadGeneration))
        {
            return;
        }

        var stageStem = Path.GetFileNameWithoutExtension(geomPath?.FileName ?? gcxPath?.FileName);
        await MapEditor.DiscoverLightsAsync(catalog, stageStem);

        _ = LoadMinimapAsync(catalog, loadGeneration);
    }

    private async Task LoadMinimapAsync(IWorkspaceCatalog catalog, long loadGeneration)
    {
        Minimap.SetLoading();
        try
        {
            var textures = await Task.Run(() => MinimapService.Load(catalog));
            if (loadGeneration != Volatile.Read(ref _workspaceLoadGeneration))
            {
                return;
            }

            if (textures.Count == 0)
            {
                Minimap.SetEmpty();
                return;
            }

            var levels = new List<MinimapViewModel.MinimapLevel>(textures.Count);
            foreach (var texture in textures)
            {
                levels.Add(new MinimapViewModel.MinimapLevel(
                    texture.Label,
                    CreateBitmap(texture),
                    texture.Projection));
            }

            Minimap.SetLevels(levels);
        }
        catch (Exception exception)
        {
            _log.Warning(exception, "Failed to load minimap");
            Minimap.SetEmpty();
        }
    }

    private static WriteableBitmap CreateBitmap(MinimapService.MinimapTexture texture)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(texture.Width, texture.Height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);

        using (var framebuffer = bitmap.Lock())
        {
            Marshal.Copy(texture.Rgba, 0, framebuffer.Address, texture.Rgba.Length);
        }

        return bitmap;
    }

    public async Task LoadGcxFromFilePathAsync(string gcxPath)
    {
        // Ensure geom is up to date before loading GCX
        GcxEditor.SetGeomFile(CollisionEditor.GeomFile);
        await GcxEditor.LoadFromFilePathAsync(gcxPath);
    }

    public async Task LoadGcxFromWorkspacePathAsync(WorkspacePath gcxPath)
    {
        GcxEditor.SetGeomFile(CollisionEditor.GeomFile);
        await GcxEditor.LoadFromWorkspacePathAsync(gcxPath);
    }

    public async Task LoadGeomFromFilePathAsync(string geomPath)
    {
        await CollisionEditor.LoadFromFilePathAsync(geomPath);
        GcxEditor.SetGeomFile(CollisionEditor.GeomFile);
    }

    public async Task LoadGeomFromWorkspacePathAsync(WorkspacePath geomPath)
    {
        await CollisionEditor.LoadFromWorkspacePathAsync(geomPath);
        GcxEditor.SetGeomFile(CollisionEditor.GeomFile);
    }

    public Task LoadLightsFromWorkspacePathAsync(
        WorkspacePath path,
        IWorkspaceCatalog workspace,
        CancellationToken cancellationToken = default)
    {
        return MapEditor.LoadLightsFromWorkspacePathAsync(path, workspace, cancellationToken);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static ObservableCollection<FileNode> BuildNodes(WorkspaceSnapshot snapshot)
    {
        return BuildDirectoryNodes(snapshot, string.Empty);
    }

    private static ObservableCollection<FileNode> BuildDirectoryNodes(
        WorkspaceSnapshot snapshot,
        string relativeDirectory)
    {
        var children = new ObservableCollection<FileNode>();

        var subDirectories = snapshot.Directories
            .Where(directory => RelativeParent(directory).Equals(relativeDirectory, StringComparison.OrdinalIgnoreCase))
            .OrderBy(directory => Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase);
        foreach (var subDirectory in subDirectories)
        {
            var physicalPath = Path.Combine(snapshot.RootPath, subDirectory);
            children.Add(new FileNode(
                Path.GetFileName(subDirectory),
                WorkspacePath.Physical(physicalPath),
                BuildDirectoryNodes(snapshot, subDirectory),
                true));
        }

        var physicalFiles = snapshot.Files
            .Where(file => !file.IsArchiveEntry &&
                RelativeParent(file.RelativePath).Equals(relativeDirectory, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var file in physicalFiles)
        {
            if (file.IsArchiveContainer)
            {
                var archiveChildren = new ObservableCollection<FileNode>(
                    snapshot.InArchive(file.Path.PhysicalPath)
                        .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(entry => new FileNode(
                            entry.Name,
                            entry.Path,
                            new ObservableCollection<FileNode>(),
                            false)));
                children.Add(new FileNode(file.Name, file.Path, archiveChildren, true));
                continue;
            }

            children.Add(new FileNode(
                file.Name,
                file.Path,
                new ObservableCollection<FileNode>(),
                false));
        }

        return children;
    }

    private static string RelativeParent(string relativePath)
    {
        return Path.GetDirectoryName(relativePath) ?? string.Empty;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_statusTimer != null)
        {
            _statusTimer.Stop();
            _statusTimer.Tick -= OnStatusTimerTick;
            _statusTimer = null;
        }

        GcxEditor.Dispose();
        MapEditor.Dispose();
        Minimap.Dispose();
        _workspaceSession.Dispose();
    }
}
