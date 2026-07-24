using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaHex.Document;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;
using HavenStudio.Services.Workspace;
using Serilog;

namespace HavenStudio.Editors;

public sealed class GcxEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<GcxEditorViewModel>();

    private readonly SceneHost _sceneHost;
    private readonly GcxDocumentSession _documentSession;
    private readonly GcxScriptEditor _scriptEditor;
    private readonly GcxModelReferenceScanner _modelReferenceScanner;
    private readonly ProjectModelLoader _projectModelLoader;
    private readonly GcxSearchService _searchService;
    private readonly GcxValidationService _validationService;
    private readonly GcxDecompilationService _decompilationService;

    private IWorkspaceCatalog? _workspace;
    private WorkspaceSnapshot? _workspaceSnapshot;
    private GeomFile? _geometry;
    private CancellationTokenSource? _operationCancellation;
    private string _gcxFileName = "No GCX loaded";
    private string _gcxTimestamp = "-";
    private string _gcxCryptoSeed = "-";
    private string _gcxScriptCount = "-";
    private string _gcxMainSize = "-";
    private string _selectedScriptInfo = "Selected: None";
    private string _decompilationText = "No script selected.";
    private bool _isBusy;
    private bool _isProgressIndeterminate;
    private double _progressValue;
    private string _progressText = string.Empty;
    private bool _disposed;
    private Dictionary<string, string> _decompiledScripts = new(StringComparer.OrdinalIgnoreCase);
    private GcxModelReferences _projectReferences = GcxModelReferences.Empty;

    public GcxEditorViewModel(SceneHost sceneHost)
    {
        _sceneHost = sceneHost ?? throw new ArgumentNullException(nameof(sceneHost));
        _documentSession = new GcxDocumentSession();
        _scriptEditor = new GcxScriptEditor(_documentSession);
        _modelReferenceScanner = new GcxModelReferenceScanner();
        _projectModelLoader = new ProjectModelLoader();
        _searchService = new GcxSearchService();
        _validationService = new GcxValidationService();
        _decompilationService = new GcxDecompilationService();
    }

    public ObservableCollection<GcxScriptNode> ScriptItems { get; } = [];

    public string GcxFileName => _gcxFileName;
    public string GcxTimestamp => _gcxTimestamp;
    public string GcxCryptoSeed => _gcxCryptoSeed;
    public string GcxScriptCount => _gcxScriptCount;
    public string GcxMainSize => _gcxMainSize;
    public string SelectedScriptInfo => _selectedScriptInfo;
    public string DecompilationText => _decompilationText;
    public IBinaryDocument? HexDocument => _scriptEditor.HexDocument;
    public bool HasSelectedScript => _scriptEditor.HasSelectedScript;
    public bool IsDirty => _documentSession.IsDirty;
    public bool IsBusy => _isBusy;
    public bool IsProgressIndeterminate => _isProgressIndeterminate;
    public double ProgressValue => _progressValue;
    public string ProgressText => _progressText;
    public bool HasDocument => _documentSession.HasDocument;
    public SceneLightSettings? SystemLighting => GcxSystemLightParser.Parse(_decompiledScripts.Values);
    public SceneColorFilterSettings? ColorFilter =>
        ParseColorFilterFromGcxBytes() ??
        GcxColorFilterParser.Parse(_decompiledScripts.Values);
    public SceneFogSettings? Fog =>
        GcxFogBytecodeParser.Parse(_documentSession.Document) ??
        GcxFogParser.Parse(_decompiledScripts.Values);
    public IReadOnlyList<string> ProcedureNames => ScriptItems
        .Where(node => !node.IsAggregate && node.Script != null)
        .Select(node => node.Name)
        .ToArray();

    public string? DefaultPlacementProcedureName
    {
        get
        {
            foreach (var node in ScriptItems.Where(node => !node.IsAggregate && node.Script != null))
            {
                var sites = new List<GcxPlacementSite>();
                GcxDecompiler.Decompile(node.Script!.Bytes, node.Name, SettingsStore.Current.IsMgs3, sites);
                if (sites.Any(site => site.IsModelPlacement))
                {
                    return node.Name;
                }
            }
            return ProcedureNames.FirstOrDefault();
        }
    }

    public IReadOnlyDictionary<uint, WorkspacePath> GetProjectModelPaths()
    {
        return _workspaceSnapshot == null
            ? new Dictionary<uint, WorkspacePath>()
            : ProjectModelLoader.BuildPathLookup(_workspaceSnapshot);
    }

    public void SetWorkspace(IWorkspaceCatalog workspace, WorkspaceSnapshot snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(snapshot);
        CancelPendingModelScan();
        _workspace = workspace;
        _workspaceSnapshot = snapshot;
        _documentSession.SetWorkspace(workspace);
    }

    public void SetGeomFile(GeomFile? geometry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _geometry = geometry;
        if (_documentSession.Document != null)
        {
            _ = RefreshProjectModelsSafelyAsync();
        }
    }

    public Task LoadFromFilePathAsync(string? gcxPath)
    {
        return LoadAsync(string.IsNullOrWhiteSpace(gcxPath)
            ? null
            : WorkspacePath.ParseLegacy(gcxPath));
    }

    public Task LoadFromWorkspacePathAsync(WorkspacePath? gcxPath)
    {
        return LoadAsync(gcxPath);
    }

    public void SelectScript(GcxScriptNode? scriptNode)
    {
        _scriptEditor.Select(scriptNode);
        PublishSelection();
    }

    public async Task SaveSelectedScriptAsync(CancellationToken cancellationToken = default)
    {
        if (!_scriptEditor.HasSelectedScript || !_documentSession.HasDocument)
        {
            return;
        }

        _scriptEditor.CommitHexDocument();
        await RefreshDecompilationAsync(_scriptEditor.SelectedScript, cancellationToken);
        PublishSelection();
        await _documentSession.SaveAsync(cancellationToken);
        UpdateMetadata();
        OnPropertyChanged(nameof(IsDirty));
    }

    public async Task AddProcAsync(CancellationToken cancellationToken = default)
    {
        var node = _scriptEditor.AddProcedure();
        if (node == null)
        {
            return;
        }

        await RefreshDecompilationAsync(node, cancellationToken);
        ScriptItems.Add(node);
        PublishSelection();
        UpdateMetadata();
        OnPropertyChanged(nameof(ScriptItems));
        OnPropertyChanged(nameof(IsDirty));
    }

    public async Task UpdateSelectedProcSizeAsync(CancellationToken cancellationToken = default)
    {
        if (!_scriptEditor.UpdateSelectedProcedureSize())
        {
            return;
        }

        await RefreshDecompilationAsync(_scriptEditor.SelectedScript, cancellationToken);
        PublishSelection();
        OnPropertyChanged(nameof(IsDirty));
    }

    public async Task InsertCommandBytesAsync(
        byte[] commandBytes,
        bool insertAtStart = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandBytes);
        if (!_scriptEditor.InsertCommandBytes(commandBytes, insertAtStart))
        {
            return;
        }

        await RefreshDecompilationAsync(_scriptEditor.SelectedScript, cancellationToken);
        PublishSelection();
        OnPropertyChanged(nameof(IsDirty));
    }

    public string? UpdatePlacementPosition(PlacedModelReference placement, OpenTK.Mathematics.Vector3 position)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var binding = placement.Binding;
        if (binding == null)
        {
            return "This placement has no writable GCX source site.";
        }

        try
        {
            var storedPosition = new OpenTK.Mathematics.Vector3(
                MathF.Round(position.X),
                MathF.Round(position.Y),
                MathF.Round(position.Z));
            var previousSite = binding.Site;
            var result = GcxPlacementWriter.WritePosition(binding.Script.Bytes, previousSite, storedPosition);
            var refreshedSites = new List<GcxPlacementSite>();
            var text = GcxDecompiler.Decompile(result.Bytes, binding.ScriptName, SettingsStore.Current.IsMgs3, refreshedSites);
            var refreshedSite = refreshedSites
                .Where(site => site.CommandHash == previousSite.CommandHash &&
                    site.ModelHash == previousSite.ModelHash)
                .OrderBy(site => Math.Abs(site.CommandOffset - previousSite.CommandOffset))
                .FirstOrDefault();
            if (refreshedSite == null)
            {
                throw new InvalidDataException("The rewritten placement could not be found in the GCX script.");
            }

            _scriptEditor.ReplaceScriptBytes(binding.Script, result.Bytes);
            binding.Site = refreshedSite;
            placement.Position = storedPosition;
            _decompiledScripts[binding.ScriptName] = text;
            _documentSession.MarkDirty();
            UpdateMetadata();
            PublishSelection();
            OnPropertyChanged(nameof(IsDirty));
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or OverflowException)
        {
            return exception.Message;
        }
    }

    public string? UpdatePlacementModelHash(PlacedModelReference placement, uint modelHash)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var binding = placement.Binding;
        if (binding == null)
        {
            return "This placement has no writable GCX source site.";
        }

        try
        {
            var previousSite = binding.Site;
            var previousModelSite = binding.ModelSite;
            var affectedPlacements = _projectReferences.PlacedModels
                .Where(candidate => SharesModelSite(candidate, binding.Script, previousModelSite))
                .Distinct()
                .ToList();
            if (!affectedPlacements.Contains(placement))
            {
                affectedPlacements.Add(placement);
            }
            var result = GcxPlacementWriter.WriteModelHash(
                binding.Script.Bytes,
                previousModelSite,
                modelHash);
            var refreshedSites = new List<GcxPlacementSite>();
            var text = GcxDecompiler.Decompile(result.Bytes, binding.ScriptName, SettingsStore.Current.IsMgs3, refreshedSites);
            var refreshedSite = refreshedSites
                .Where(site => site.CommandHash == previousSite.CommandHash)
                .OrderBy(site => Math.Abs(site.CommandOffset - previousSite.CommandOffset))
                .FirstOrDefault();
            if (refreshedSite == null)
            {
                throw new InvalidDataException("The rewritten placement could not be found in the GCX script.");
            }

            _scriptEditor.ReplaceScriptBytes(binding.Script, result.Bytes);
            foreach (var affected in affectedPlacements)
            {
                var affectedBinding = affected.Binding;
                if (affectedBinding == null)
                {
                    continue;
                }

                var oldModelSite = affectedBinding.ModelSite;
                affectedBinding.Site = refreshedSite;
                affectedBinding.ModelSite = refreshedSite.Model ??
                    refreshedSite.ForeachModelSites.FirstOrDefault(site =>
                        site?.ValueOffset == oldModelSite?.ValueOffset) ??
                    (oldModelSite is { } site
                        ? site with { Value = modelHash }
                        : null);
                affected.ModelHash = modelHash;
            }
            _decompiledScripts[binding.ScriptName] = text;
            _documentSession.MarkDirty();
            UpdateMetadata();
            PublishSelection();
            OnPropertyChanged(nameof(IsDirty));
            _ = RefreshPlacementModelsSafelyAsync(affectedPlacements);
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or OverflowException)
        {
            return exception.Message;
        }
    }

    public string? UpdatePlacementCollisionReference(
        PlacedModelReference placement,
        uint? collisionReferenceHash)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var binding = placement.Binding;
        if (binding == null)
        {
            return "This placement has no writable GCX source site.";
        }

        try
        {
            var previousSite = binding.Site;
            var collisionSite = binding.CollisionReferenceSite;
            var isForeachDataSite = collisionSite != null &&
                previousSite.CollisionReference?.ValueOffset != collisionSite.ValueOffset;
            if (isForeachDataSite)
            {
                if (collisionReferenceHash is not { } nestedHash || nestedHash == 0)
                {
                    throw new InvalidOperationException(
                        "A foreach collision reference can be changed, but it cannot be removed safely.");
                }

                var affectedRows = _projectReferences.PlacedModels
                    .Where(candidate => SharesCollisionReferenceSite(
                        candidate,
                        binding.Script,
                        collisionSite))
                    .Distinct()
                    .ToList();
                if (!affectedRows.Contains(placement))
                {
                    affectedRows.Add(placement);
                }
                var nestedResult = GcxPlacementWriter.WriteCollisionReference(
                    binding.Script.Bytes,
                    collisionSite!,
                    nestedHash);
                var nestedText = GcxDecompiler.Decompile(
                    nestedResult.Bytes,
                    binding.ScriptName,
                    SettingsStore.Current.IsMgs3);
                _scriptEditor.ReplaceScriptBytes(binding.Script, nestedResult.Bytes);
                foreach (var affected in affectedRows)
                {
                    affected.CollisionReferenceHash = nestedHash;
                    if (affected.Binding?.CollisionReferenceSite is { } affectedSite)
                    {
                        affected.Binding.CollisionReferenceSite = affectedSite with { Value = nestedHash };
                    }
                }
                _decompiledScripts[binding.ScriptName] = nestedText;
                _documentSession.MarkDirty();
                UpdateMetadata();
                PublishSelection();
                OnPropertyChanged(nameof(IsDirty));
                return null;
            }

            var affectedPlacements = _projectReferences.PlacedModels
                .Where(candidate => SharesCollisionReferenceSite(
                    candidate,
                    binding.Script,
                    previousSite.CollisionReference))
                .Distinct()
                .ToList();
            if (!affectedPlacements.Contains(placement))
            {
                affectedPlacements.Add(placement);
            }
            var result = GcxPlacementWriter.WriteCollisionReference(
                binding.Script.Bytes,
                previousSite,
                collisionReferenceHash);
            var refreshedSites = new List<GcxPlacementSite>();
            var text = GcxDecompiler.Decompile(result.Bytes, binding.ScriptName, SettingsStore.Current.IsMgs3, refreshedSites);
            var refreshedSite = refreshedSites
                .Where(site => site.CommandHash == previousSite.CommandHash &&
                    site.ModelHash == previousSite.ModelHash)
                .OrderBy(site => Math.Abs(site.CommandOffset - previousSite.CommandOffset))
                .FirstOrDefault();
            if (refreshedSite == null)
            {
                throw new InvalidDataException("The rewritten placement could not be found in the GCX script.");
            }

            _scriptEditor.ReplaceScriptBytes(binding.Script, result.Bytes);
            foreach (var affected in affectedPlacements)
            {
                if (affected.Binding != null)
                {
                    affected.Binding.Site = refreshedSite;
                    affected.Binding.CollisionReferenceSite = refreshedSite.CollisionReference;
                    affected.Binding.TransformSourceSite = refreshedSite.Effect ??
                        refreshedSite.PropertyPosition;
                }
                affected.CollisionReferenceHash = collisionReferenceHash is > 0
                    ? collisionReferenceHash
                    : null;
            }
            _decompiledScripts[binding.ScriptName] = text;
            _documentSession.MarkDirty();
            UpdateMetadata();
            PublishSelection();
            OnPropertyChanged(nameof(IsDirty));
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or OverflowException)
        {
            return exception.Message;
        }
    }

    public async Task<PlacedModelReference?> AddObjectAsync(
        byte[] commandBytes,
        string targetProcedure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandBytes);
        if (commandBytes.Length == 0)
        {
            return null;
        }
        var document = _documentSession.Document;
        var target = ScriptItems.FirstOrDefault(node =>
            !node.IsAggregate &&
            node.Script != null &&
            node.Name.Equals(targetProcedure, StringComparison.OrdinalIgnoreCase));
        if (document == null || target?.Script == null)
        {
            return null;
        }

        BeginProgress("Adding map object...", indeterminate: true);
        try
        {
            _scriptEditor.Select(target);
            if (!_scriptEditor.InsertCommandBytes(commandBytes))
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var isMgs3 = SettingsStore.Current.IsMgs3;
            var analysis = await Task.Run(() =>
            {
                var decompiled = _decompilationService.DecompileDocument(document, isMgs3, cancellationToken);
                var references = _modelReferenceScanner.Scan(
                    document,
                    _geometry,
                    isMgs3,
                    cancellationToken);
                return new DocumentAnalysis(decompiled, references);
            }, cancellationToken);
            _decompiledScripts = new Dictionary<string, string>(
                analysis.DecompiledScripts,
                StringComparer.OrdinalIgnoreCase);
            PublishSelection();
            UpdateMetadata();
            OnPropertyChanged(nameof(IsDirty));
            await LoadProjectModelsAsync(analysis.References, cancellationToken);

            return analysis.References.PlacedModels
                .Where(placement => ReferenceEquals(placement.Binding?.Script, target.Script))
                .OrderByDescending(placement => placement.Binding?.Site.CommandOffset ?? -1)
                .FirstOrDefault();
        }
        finally
        {
            EndProgress();
        }
    }

    public async Task<PlacedModelReference?> DuplicatePlacementAsync(
        PlacedModelReference placement,
        uint? replacementTransformHash = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var binding = placement.Binding ?? throw new InvalidOperationException(
            "This placement has no writable GCX source site.");
        var document = _documentSession.Document;
        var target = ScriptItems.FirstOrDefault(node =>
            !node.IsAggregate && ReferenceEquals(node.Script, binding.Script));
        if (document == null || target?.Script == null)
        {
            return null;
        }

        BeginProgress("Duplicating map placement...", indeterminate: true);
        try
        {
            var sourceSite = binding.Site;
            var sourceForeachRow = binding.ForeachRowIndex;
            var result = GcxPlacementWriter.DuplicatePlacement(
                binding.Script.Bytes,
                sourceSite,
                sourceForeachRow,
                binding.TransformSourceSite,
                replacementTransformHash);
            _scriptEditor.Select(target);
            _scriptEditor.ReplaceScriptBytes(binding.Script, result.Bytes);
            _documentSession.MarkDirty();

            cancellationToken.ThrowIfCancellationRequested();
            var isMgs3 = SettingsStore.Current.IsMgs3;
            var analysis = await Task.Run(() =>
            {
                var decompiled = _decompilationService.DecompileDocument(document, isMgs3, cancellationToken);
                var references = _modelReferenceScanner.Scan(
                    document,
                    _geometry,
                    isMgs3,
                    cancellationToken);
                return new DocumentAnalysis(decompiled, references);
            }, cancellationToken);
            _decompiledScripts = new Dictionary<string, string>(
                analysis.DecompiledScripts,
                StringComparer.OrdinalIgnoreCase);
            PublishSelection();
            UpdateMetadata();
            OnPropertyChanged(nameof(IsDirty));
            await LoadProjectModelsAsync(analysis.References, cancellationToken);

            var candidates = analysis.References.PlacedModels.Where(candidate =>
                candidate.Binding is { } candidateBinding &&
                ReferenceEquals(candidateBinding.Script, target.Script) &&
                candidateBinding.Site.CommandHash == sourceSite.CommandHash &&
                candidate.ModelHash == placement.ModelHash);
            if (sourceForeachRow is { } rowIndex && sourceSite.Foreach is { } sourceForeach)
            {
                return candidates.FirstOrDefault(candidate =>
                    candidate.Binding!.ForeachRowIndex == rowIndex + 1 &&
                    candidate.Binding.Site.Foreach?.CommandOffset == sourceForeach.CommandOffset);
            }

            var duplicateOffset = sourceSite.CommandOffset + sourceSite.CommandLength;
            return candidates.FirstOrDefault(candidate =>
                candidate.Binding!.Site.CommandOffset == duplicateOffset);
        }
        finally
        {
            EndProgress();
        }
    }

    public bool TryFindNextInDecompilation(
        string query,
        out int matchIndex,
        out int matchLength,
        out GcxScriptNode? matchedNode)
    {
        var match = _searchService.FindNext(query, ScriptItems, GetSearchText);
        if (match == null)
        {
            matchIndex = -1;
            matchLength = 0;
            matchedNode = null;
            return false;
        }

        SelectScript(match.Node);
        matchIndex = match.Index;
        matchLength = match.Length;
        matchedNode = match.Node;
        return true;
    }

    public IReadOnlyList<string> GetProcSizeErrors()
    {
        return _validationService.GetProcedureSizeErrors(_documentSession.Document);
    }

    public void CancelPendingModelScan()
    {
        _operationCancellation?.Cancel();
        _projectModelLoader.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var activeOperation = _operationCancellation;
        _operationCancellation = null;
        activeOperation?.Cancel();
        _projectModelLoader.Cancel();
        _projectModelLoader.Dispose();
        _documentSession.Unload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Reads the color filter straight from the loaded GCX bytecode (data-driven,
    // by parameter name hash), like the fog parser above. Falls back to the
    // decompiled-text parser when no literal command is present.
    private SceneColorFilterSettings? ParseColorFilterFromGcxBytes()
    {
        var document = _documentSession.Document;
        if (document == null)
        {
            return null;
        }

        var scripts = new List<byte[]?>();
        foreach (var definition in document.ScriptDefinitions)
        {
            scripts.Add(definition.Script?.Bytes);
        }
        foreach (var definition in document.StringDefinitions)
        {
            scripts.Add(definition.Script?.Bytes);
        }
        scripts.Add(document.MainScript?.Bytes);
        return GcxColorFilterParser.ParseGcxScripts(scripts);
    }

    private async Task LoadAsync(WorkspacePath? path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var cancellation = BeginOperation();
        ResetEditorState();
        BeginProgress("Loading GCX...", indeterminate: true);

        try
        {
            if (!await _documentSession.LoadAsync(path, cancellation.Token))
            {
                ResetMetadata();
                return;
            }

            cancellation.Token.ThrowIfCancellationRequested();
            var analysis = await AnalyzeDocumentAsync(
                forceDecompilation: true,
                cancellationToken: cancellation.Token);
            _decompiledScripts = new Dictionary<string, string>(analysis.DecompiledScripts, StringComparer.OrdinalIgnoreCase);
            PublishLoadedDocument();
            var references = analysis.References;
            await LoadProjectModelsAsync(references, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_operationCancellation, cancellation))
            {
                _operationCancellation = null;
                EndProgress();
            }

            cancellation.Dispose();
        }
    }

    private async Task RefreshProjectModelsSafelyAsync()
    {
        var cancellation = BeginOperation();
        try
        {
            var analysis = await AnalyzeDocumentAsync(
                forceDecompilation: false,
                cancellationToken: cancellation.Token);
            _decompiledScripts = new Dictionary<string, string>(analysis.DecompiledScripts, StringComparer.OrdinalIgnoreCase);
            var references = analysis.References;
            await LoadProjectModelsAsync(references, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to refresh GCX project models after GEOM changed");
        }
        finally
        {
            if (ReferenceEquals(_operationCancellation, cancellation))
            {
                _operationCancellation = null;
                EndProgress();
            }

            cancellation.Dispose();
        }
    }

    private Task<DocumentAnalysis> AnalyzeDocumentAsync(
        bool forceDecompilation,
        CancellationToken cancellationToken)
    {
        var document = _documentSession.Document;
        if (document == null)
        {
            return Task.FromResult(new DocumentAnalysis(
                new Dictionary<string, string>(),
                GcxModelReferences.Empty));
        }

        var geometry = _geometry;
        var isMgs3 = SettingsStore.Current.IsMgs3;
        var cachedDecompilation = forceDecompilation || _decompiledScripts.Count == 0
            ? null
            : new Dictionary<string, string>(_decompiledScripts, StringComparer.OrdinalIgnoreCase);
        BeginProgress("Scanning GCX model references...", indeterminate: true);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decompiled = cachedDecompilation ?? _decompilationService.DecompileDocument(
                document,
                isMgs3,
                cancellationToken);
            var references = _modelReferenceScanner.Scan(
                document,
                geometry,
                isMgs3,
                cancellationToken);
            return new DocumentAnalysis(decompiled, references);
        }, cancellationToken);
    }

    private async Task LoadProjectModelsAsync(
        GcxModelReferences references,
        CancellationToken cancellationToken)
    {
        _projectReferences = references;
        if (!SettingsStore.Current.LoadSceneFromGcx ||
            _workspace == null ||
            _workspaceSnapshot == null ||
            !references.RequiredModelHashes().Any())
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _sceneHost.ClearLayer(SceneLayer.VisualModels));
            return;
        }

        BeginProgress("Scanning project...", indeterminate: false);
        var progress = new Progress<ProjectModelLoadProgress>(value =>
            UpdateProgress(value.Value, value.Text));
        await _projectModelLoader.LoadAsync(
            references,
            _workspace,
            _workspaceSnapshot,
            PublishSceneAsync,
            progress,
            cancellationToken);
    }

    private async Task RefreshPlacementModelsSafelyAsync(
        IReadOnlyList<PlacedModelReference> placements)
    {
        var cancellation = BeginOperation();
        try
        {
            if (!SettingsStore.Current.LoadSceneFromGcx ||
                _workspace == null ||
                _workspaceSnapshot == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _sceneHost.ReplacePlacementModels(placements, []));
                return;
            }

            BeginProgress(
                placements.Count == 1
                    ? "Reloading placed model..."
                    : $"Reloading {placements.Count} placed models...",
                indeterminate: false);
            var references = new GcxModelReferences(
                new HashSet<uint>(),
                placements);
            var progress = new Progress<ProjectModelLoadProgress>(value =>
                UpdateProgress(value.Value, value.Text));
            await _projectModelLoader.LoadAsync(
                references,
                _workspace,
                _workspaceSnapshot,
                async (batches, token) =>
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        _sceneHost.ReplacePlacementModels(placements, batches);
                    });
                },
                progress,
                cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to reload edited GCX placement models");
        }
        finally
        {
            if (ReferenceEquals(_operationCancellation, cancellation))
            {
                _operationCancellation = null;
                EndProgress();
            }

            cancellation.Dispose();
        }
    }

    private async Task PublishSceneAsync(
        IReadOnlyList<MdnSceneBatch> batches,
        CancellationToken cancellationToken)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sceneHost.ReplaceModels(batches);
        });
    }

    private CancellationTokenSource BeginOperation()
    {
        _operationCancellation?.Cancel();
        _projectModelLoader.Cancel();
        _operationCancellation = new CancellationTokenSource();
        return _operationCancellation;
    }

    private void ResetEditorState()
    {
        ScriptItems.Clear();
        _scriptEditor.Reset();
        _searchService.Reset();
        _decompiledScripts.Clear();
        _projectReferences = GcxModelReferences.Empty;
        ResetMetadata();
        OnPropertyChanged(nameof(ScriptItems));
        OnPropertyChanged(nameof(HexDocument));
        OnPropertyChanged(nameof(HasSelectedScript));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(ProcedureNames));
        OnPropertyChanged(nameof(DefaultPlacementProcedureName));
    }

    private static bool SharesModelSite(
        PlacedModelReference placement,
        GcxScript script,
        GcxStringCodeSite? modelSite)
    {
        return modelSite != null &&
            ReferenceEquals(placement.Binding?.Script, script) &&
            placement.Binding.ModelSite?.ValueOffset == modelSite.ValueOffset;
    }

    private static bool SharesCollisionReferenceSite(
        PlacedModelReference placement,
        GcxScript script,
        GcxStringCodeSite? collisionReference)
    {
        return collisionReference != null &&
            ReferenceEquals(placement.Binding?.Script, script) &&
            placement.Binding.CollisionReferenceSite?.ValueOffset == collisionReference.ValueOffset;
    }

    private void PublishLoadedDocument()
    {
        var document = _documentSession.Document;
        if (document == null)
        {
            return;
        }

        ScriptItems.Clear();
        ScriptItems.Add(GcxScriptNode.CreateAggregate("entire script"));
        ScriptItems.Add(new GcxScriptNode("main", document.MainScript));
        for (var index = 0; index < document.ScriptDefinitions.Count; index++)
        {
            var definition = document.ScriptDefinitions[index];
            definition.Script ??= new GcxScript(Array.Empty<byte>());
            ScriptItems.Add(new GcxScriptNode($"proc{index + 1}", definition.Script));
        }

        SelectScript(ScriptItems.FirstOrDefault());
        UpdateMetadata();
        OnPropertyChanged(nameof(ScriptItems));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(ProcedureNames));
        OnPropertyChanged(nameof(DefaultPlacementProcedureName));
        OnPropertyChanged(nameof(SystemLighting));
        OnPropertyChanged(nameof(ColorFilter));
        OnPropertyChanged(nameof(Fog));
    }

    private void PublishSelection()
    {
        var selected = _scriptEditor.SelectedScript;
        if (selected == null)
        {
            _selectedScriptInfo = "Selected: None";
            _decompilationText = "No script selected.";
        }
        else if (selected.IsAggregate)
        {
            _selectedScriptInfo = "Selected: Entire Script";
            _decompilationText = BuildEntireScriptDecompilation();
        }
        else if (selected.Script == null)
        {
            _selectedScriptInfo = "Selected: None";
            _decompilationText = "No script selected.";
        }
        else
        {
            var bytes = selected.Script.Bytes ?? Array.Empty<byte>();
            _selectedScriptInfo = $"Selected: {selected.Name} ({bytes.Length} bytes)";
            _decompilationText = _decompiledScripts.GetValueOrDefault(selected.Name, "// Decompilation unavailable.");
        }

        OnPropertyChanged(nameof(SelectedScriptInfo));
        OnPropertyChanged(nameof(DecompilationText));
        OnPropertyChanged(nameof(HexDocument));
        OnPropertyChanged(nameof(HasSelectedScript));
    }

    private string BuildEntireScriptDecompilation()
    {
        if (_documentSession.Document == null)
        {
            return "No script selected.";
        }

        var builder = new StringBuilder();
        AppendScript(builder, "main");
        for (var index = 0; index < _documentSession.Document.ScriptDefinitions.Count; index++)
        {
            AppendScript(builder, $"proc{index + 1}");
        }

        return builder.ToString().TrimEnd();
    }

    private void AppendScript(StringBuilder builder, string name)
    {
        builder.AppendLine(_decompiledScripts.GetValueOrDefault(name, "// Decompilation unavailable."));
        builder.AppendLine();
    }

    private string GetSearchText(GcxScriptNode node)
    {
        var text = _decompiledScripts.GetValueOrDefault(node.Name, string.Empty);
        return text.StartsWith("// Decompilation error:", StringComparison.Ordinal)
            ? string.Empty
            : text;
    }

    private async Task RefreshDecompilationAsync(
        GcxScriptNode? node,
        CancellationToken cancellationToken)
    {
        if (node?.Script == null || node.IsAggregate)
        {
            return;
        }

        var bytes = (node.Script.Bytes ?? Array.Empty<byte>()).ToArray();
        var name = node.Name;
        var isMgs3 = SettingsStore.Current.IsMgs3;
        var text = await Task.Run(
            () => _decompilationService.Decompile(bytes, name, isMgs3),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (node.Script.Bytes.AsSpan().SequenceEqual(bytes))
        {
            _decompiledScripts[name] = text;
            OnPropertyChanged(nameof(SystemLighting));
            OnPropertyChanged(nameof(ColorFilter));
            OnPropertyChanged(nameof(Fog));
        }
    }

    private void UpdateMetadata()
    {
        var document = _documentSession.Document;
        if (document == null)
        {
            ResetMetadata();
            return;
        }

        _gcxFileName = _documentSession.CurrentPath?.FileName ?? "No GCX loaded";
        _gcxTimestamp = document.Timestamp.ToString();
        _gcxCryptoSeed = document.CryptoSeed.ToString();
        _gcxScriptCount = document.ScriptDefinitions.Count.ToString();
        _gcxMainSize = (document.MainScript?.Bytes?.Length ?? 0).ToString();
        NotifyMetadataChanged();
    }

    private void ResetMetadata()
    {
        _gcxFileName = "No GCX loaded";
        _gcxTimestamp = "-";
        _gcxCryptoSeed = "-";
        _gcxScriptCount = "-";
        _gcxMainSize = "-";
        _selectedScriptInfo = "Selected: None";
        _decompilationText = "No script selected.";
        NotifyMetadataChanged();
        OnPropertyChanged(nameof(SelectedScriptInfo));
        OnPropertyChanged(nameof(DecompilationText));
    }

    private void NotifyMetadataChanged()
    {
        OnPropertyChanged(nameof(GcxFileName));
        OnPropertyChanged(nameof(GcxTimestamp));
        OnPropertyChanged(nameof(GcxCryptoSeed));
        OnPropertyChanged(nameof(GcxScriptCount));
        OnPropertyChanged(nameof(GcxMainSize));
    }

    private void BeginProgress(string text, bool indeterminate)
    {
        _isBusy = true;
        _isProgressIndeterminate = indeterminate;
        _progressValue = 0;
        _progressText = text;
        NotifyProgressChanged();
    }

    private void UpdateProgress(double value, string text)
    {
        _progressValue = Math.Clamp(value, 0, 1);
        if (!string.IsNullOrWhiteSpace(text))
        {
            _progressText = text;
        }

        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressText));
    }

    private void EndProgress()
    {
        _isBusy = false;
        _isProgressIndeterminate = false;
        _progressValue = 0;
        _progressText = string.Empty;
        NotifyProgressChanged();
    }

    private void NotifyProgressChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsProgressIndeterminate));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record DocumentAnalysis(
        IReadOnlyDictionary<string, string> DecompiledScripts,
        GcxModelReferences References);
}
