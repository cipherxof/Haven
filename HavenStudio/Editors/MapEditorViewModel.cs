using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia3DControl;
using Avalonia3DControl.Core.Models;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Rendering;
using HavenStudio.Services;
using HavenStudio.Formats.Geo;
using HavenStudio.Formats.Lit;
using HavenStudio.Editors.Lighting;
using HavenStudio.Services.Workspace;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Editors;

public abstract record MapEntity(string DisplayName);

public sealed record ProjectModelOption(uint Hash, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record GeoReferenceOption(uint Hash, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record PlacementEntity : MapEntity, INotifyPropertyChanged
{
    private readonly Func<PlacedModelReference, Vector3, string?> _updatePosition;
    private readonly Func<PlacedModelReference, uint, string?> _updateModelHash;
    private readonly Func<PlacedModelReference, uint?, string?> _updateCollisionReference;
    private string? _modelEditStatus;
    private string? _positionEditStatus;
    private string? _collisionReferenceEditStatus;

    public PlacementEntity(
        PlacedModelReference placement,
        IReadOnlyList<Model3D> models,
        string name,
        IReadOnlyList<ProjectModelOption> projectModels,
        IReadOnlyList<GeoReferenceOption> geoReferences,
        Func<PlacedModelReference, Vector3, string?> updatePosition,
        Func<PlacedModelReference, uint, string?> updateModelHash,
        Func<PlacedModelReference, uint?, string?> updateCollisionReference)
        : base(name)
    {
        Placement = placement;
        Models = models;
        ProjectModels = projectModels;
        GeoReferences = geoReferences;
        _updatePosition = updatePosition;
        _updateModelHash = updateModelHash;
        _updateCollisionReference = updateCollisionReference;
    }

    public PlacedModelReference Placement { get; }
    public IReadOnlyList<Model3D> Models { get; }
    public IReadOnlyList<ProjectModelOption> ProjectModels { get; }
    public IReadOnlyList<GeoReferenceOption> GeoReferences { get; }
    public bool CanDuplicate => Placement.Binding is { } binding &&
        (!binding.Site.IsNested ||
         binding.Site.Foreach != null && binding.ForeachRowIndex != null) &&
        (Placement.SourceEffect == null || binding.TransformSourceSite != null);
    public string DuplicateToolTip => CanDuplicate
        ? "Insert an identical placement immediately after this one in the GCX."
        : Placement.SourceEffect != null && Placement.Binding?.TransformSourceSite == null
            ? "This effect-bound placement does not have a writable GCX effect or property hash."
            : "This placement does not have a safely writable GCX command or foreach row.";
    public bool IsExpanded { get; set; }
    public string ModelHashText => $"0x{Placement.ModelHash:X8}";
    public bool CanEditModelHash => Placement.Binding?.ModelSite != null;
    public string ModelEditMessage => _modelEditStatus ??
        (CanEditModelHash
            ? string.Empty
            : "This placement does not contain a direct writable model hash.");
    public bool HasModelEditMessage => !string.IsNullOrWhiteSpace(ModelEditMessage);
    public ProjectModelOption? SelectedProjectModel
    {
        get => ProjectModels.FirstOrDefault(option => option.Hash == Placement.ModelHash);
        set
        {
            if (value == null || !CanEditModelHash || value.Hash == Placement.ModelHash)
            {
                return;
            }

            _modelEditStatus = _updateModelHash(Placement, value.Hash);
            OnPropertyChanged(nameof(SelectedProjectModel));
            OnPropertyChanged(nameof(ModelHashText));
            OnPropertyChanged(nameof(ModelEditMessage));
            OnPropertyChanged(nameof(HasModelEditMessage));
        }
    }
    public string PositionText => Placement.Position is { } value
        ? $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}"
        : "Not specified";
    public string RotationText => Placement.Rotation is { } value
        ? $"{RadiansToDegrees(value.X):0.###}, {RadiansToDegrees(value.Y):0.###}, {RadiansToDegrees(value.Z):0.###}°"
        : "Not specified";
    public string EffectText => Placement.EffectHash is { } hash ? $"0x{hash:X8}" : "None";
    public bool CanEditPosition => Placement.Binding?.Site.Editable == true || Placement.SourceEffect != null;
    public string PositionEditMessage => _positionEditStatus ??
        (Placement.Binding?.Site.Editable == true
            ? string.Empty
            : Placement.SourceEffect != null
                ? $"Writes to GEOM effect 0x{unchecked((uint)Placement.SourceEffect.Name):X8}."
                : Placement.Binding?.Site.ReadOnlyReason ??
                    "No direct writable position or GEOM effect was found.");
    public bool HasPositionEditMessage => !string.IsNullOrWhiteSpace(PositionEditMessage);
    public bool CanEditCollisionReference => Placement.Binding?.CollisionReferenceSite != null ||
        Placement.Binding?.Site.CollisionReferenceEditable == true;
    public string CollisionReferenceText => Placement.CollisionReferenceHash is { } hash
        ? $"0x{hash:X6}"
        : "None";
    public string CollisionReferenceEditMessage => _collisionReferenceEditStatus ??
        (CanEditCollisionReference
            ? string.Empty
            : Placement.Binding == null
                ? "No GCX source command was found for this placement."
                : Placement.Binding.Site.IsNested
                    ? "This nested collision reference has no writable foreach data cell."
                    : "This placement command cannot safely update a collision reference.");
    public bool HasCollisionReferenceEditMessage =>
        !string.IsNullOrWhiteSpace(CollisionReferenceEditMessage);
    public GeoReferenceOption? SelectedGeoReference
    {
        get
        {
            var hash = Placement.CollisionReferenceHash.GetValueOrDefault();
            return GeoReferences.FirstOrDefault(option => option.Hash == hash);
        }
        set
        {
            if (value == null || !CanEditCollisionReference)
            {
                return;
            }

            var hash = value.Hash == 0 ? null : (uint?)value.Hash;
            if (Placement.CollisionReferenceHash == hash)
            {
                return;
            }

            _collisionReferenceEditStatus = _updateCollisionReference(Placement, hash);
            NotifyCollisionReferenceChanged();
        }
    }
    public float PositionX
    {
        get => Placement.Position?.X ?? 0f;
        set => SetPosition(value, PositionY, PositionZ);
    }
    public float PositionY
    {
        get => Placement.Position?.Y ?? 0f;
        set => SetPosition(PositionX, value, PositionZ);
    }
    public float PositionZ
    {
        get => Placement.Position?.Z ?? 0f;
        set => SetPosition(PositionX, PositionY, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetPosition(float x, float y, float z)
    {
        TryUpdatePosition(new Vector3(x, y, z));
    }

    public bool TryUpdatePosition(Vector3 position)
    {
        if (!CanEditPosition)
        {
            return false;
        }
        if (Placement.Position == position)
        {
            return true;
        }

        _positionEditStatus = _updatePosition(Placement, position);
        if (_positionEditStatus == null)
        {
            RefreshPositionFromSource();
            return true;
        }

        NotifyPositionChanged();
        return false;
    }

    public void RefreshPositionFromSource()
    {
        if (Placement.Position is { } position)
        {
            foreach (var model in Models)
            {
                model.Position = position;
            }
        }
        NotifyPositionChanged();
    }

    private void NotifyPositionChanged()
    {
        OnPropertyChanged(nameof(PositionX));
        OnPropertyChanged(nameof(PositionY));
        OnPropertyChanged(nameof(PositionZ));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(PositionEditMessage));
        OnPropertyChanged(nameof(HasPositionEditMessage));
    }

    private void NotifyCollisionReferenceChanged()
    {
        OnPropertyChanged(nameof(SelectedGeoReference));
        OnPropertyChanged(nameof(CollisionReferenceText));
        OnPropertyChanged(nameof(CollisionReferenceEditMessage));
        OnPropertyChanged(nameof(HasCollisionReferenceEditMessage));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static float RadiansToDegrees(float value) => value * 180f / MathF.PI;
}

public sealed record BlockEntity(CollisionBlockViewModel Block)
    : MapEntity(Block.DisplayName);

public sealed record PrimEntity(
    CollisionPrimViewModel Prim,
    CollisionGeoPrimViewModel? GeoPrim)
    : MapEntity(GeoPrim?.DisplayName ?? Prim.DisplayName)
{
    public bool HasGeoPrim => GeoPrim != null;
}

public sealed record EffectEntity(CollisionEffectViewModel Effect)
    : MapEntity(Effect.DisplayName);

public sealed class MapOutlineGroup
{
    public MapOutlineGroup(string displayName)
    {
        DisplayName = displayName;
    }

    public string DisplayName { get; }
    public bool IsExpanded { get; set; }
    public ObservableCollection<object> Children { get; } = [];
}

public sealed class MapEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SceneHost _sceneHost;
    private readonly CollisionEditorViewModel _collisionEditor;
    private readonly GcxEditorViewModel _gcxEditor;
    private readonly MapOutlineGroup _placementsGroup = new("Placements");
    private readonly MapOutlineGroup _collisionGroup = new("Collision");
    private readonly MapOutlineGroup _effectsGroup = new("Effects");
    private readonly MapOutlineGroup _lightsGroup = new("Lights");
    private readonly Dictionary<PlacedModelReference, PlacementEntity> _placements =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<LitDocumentSession> _lightDocuments = [];
    private readonly List<LightEntity> _lightEntities = [];
    private readonly Dictionary<Model3D, LightEntity> _lightByModel = [];
    private readonly EditHistory _history = new();
    private readonly MapManipulationController _manipulationController;
    private MapEntity? _selectedEntity;
    private object? _selectedOutlineItem;
    private bool _syncingSelection;
    private bool _inspectorExpanded;
    private Vector3? _spawnPosition;
    private string _addObjectStatus = string.Empty;
    private string _manipulationStatus = string.Empty;
    private bool _gameLightingEnabled;
    private bool _gameFilterEnabled = false;
    private string _primaryLightSelectionReason = string.Empty;
    private CancellationTokenSource? _lightingBakeCancellation;
    private Task _lightingUpdateTask = Task.CompletedTask;
    private int _lightingBakeVersion;
    private bool _disposed;

    public MapEditorViewModel(
        SceneHost sceneHost,
        CollisionEditorViewModel collisionEditor,
        GcxEditorViewModel gcxEditor)
    {
        _sceneHost = sceneHost ?? throw new ArgumentNullException(nameof(sceneHost));
        _collisionEditor = collisionEditor ?? throw new ArgumentNullException(nameof(collisionEditor));
        _gcxEditor = gcxEditor ?? throw new ArgumentNullException(nameof(gcxEditor));
        _manipulationController = new MapManipulationController(_sceneHost);
        Outline = [_placementsGroup, _collisionGroup, _effectsGroup, _lightsGroup];
        _history.Changed += OnHistoryChanged;
        _sceneHost.LayerChanged += OnLayerChanged;
        _collisionEditor.SelectionChanged += OnCollisionSelectionChanged;
        _collisionEditor.PropertyChanged += OnCollisionEditorPropertyChanged;
        _gcxEditor.PropertyChanged += OnGcxEditorPropertyChanged;
        RefreshOutline();
        ApplyGameFog();
    }

    public ObservableCollection<MapOutlineGroup> Outline { get; }
    public MapEntity? SelectedEntity => _selectedEntity;
    public bool HasSelection => _selectedEntity != null;
    public bool InspectorExpanded
    {
        get => _inspectorExpanded;
        set
        {
            if (_inspectorExpanded == value)
            {
                return;
            }
            _inspectorExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(InspectorCollapsed));
        }
    }
    public bool InspectorCollapsed => !InspectorExpanded;
    public Vector3 SpawnPosition => _spawnPosition ?? _sceneHost.Scene.Camera.Target;
    public string SpawnPositionText =>
        $"Spawn: {SpawnPosition.X:0.##}, {SpawnPosition.Y:0.##}, {SpawnPosition.Z:0.##}";
    public string AddObjectStatus => _addObjectStatus;
    public bool HasAddObjectStatus => !string.IsNullOrWhiteSpace(_addObjectStatus);
    public string ManipulationStatus => _manipulationStatus;
    public bool HasManipulationStatus => !string.IsNullOrWhiteSpace(_manipulationStatus);
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;
    public string UndoToolTip => _history.UndoDescription is { } description
        ? $"Undo {description} (Ctrl+Z)"
        : "Undo (Ctrl+Z)";
    public string RedoToolTip => _history.RedoDescription is { } description
        ? $"Redo {description} (Ctrl+Y)"
        : "Redo (Ctrl+Y)";
    public bool GameLightingEnabled
    {
        get => _gameLightingEnabled;
        set
        {
            if (_gameLightingEnabled == value)
            {
                return;
            }
            _gameLightingEnabled = value;
            ApplyGameLighting();
            OnPropertyChanged();
        }
    }
    public bool GameFilterEnabled
    {
        get => _gameFilterEnabled;
        set
        {
            if (_gameFilterEnabled == value) return;
            _gameFilterEnabled = value;
            ApplyGameFilter();
            OnPropertyChanged();
            OnPropertyChanged(nameof(GameFilterSummary));
        }
    }
    public bool HasGameFilter => true;
    public string GameFilterSummary => _gcxEditor.ColorFilter is { } f
        ? $"GCX filter: mono {f.Mono:0.###}, scale {f.Scale.X:0.###}/{f.Scale.Y:0.###}/{f.Scale.Z:0.###}, bright {f.Brightness:0.###}, contrast {f.Contrast:0.###}"
        : "No GCX color filter detected";
    public bool HasGameFog => _gcxEditor.Fog?.IsConfigured(0) == true;
    public string GameFogSummary => _gcxEditor.Fog is { } fog && fog.TryGetViewport(0, out var state)
        ? $"GCX fog: near {state.Near:0.##}, far {state.Far:0.##}, " +
          $"RGB {state.Color.X:0.###}/{state.Color.Y:0.###}/{state.Color.Z:0.###}, " +
          $"limits {state.LimitMin:0.###}/{state.LimitMax:0.###}"
        : "No literal NewFogSet state was found for viewport 0; fog preview is disabled.";


    public bool FogEnabled
    {
        get => _sceneHost.FogEnabled;
        set
        {
            var enabled = value && HasGameFog;
            if (_sceneHost.FogEnabled == enabled)
            {
                if (value != enabled)
                {
                    OnPropertyChanged();
                }
                return;
            }
            _sceneHost.SetFogEnabled(enabled);
            OnPropertyChanged();
        }
    }

    public Task LightingUpdateTask => _lightingUpdateTask;

    public bool ShadowsEnabled
    {
        get => _sceneHost.ShadowsEnabled;
        set
        {
            if (_sceneHost.ShadowsEnabled == value)
            {
                return;
            }
            _sceneHost.SetShadowsEnabled(value);
            OnPropertyChanged();
        }
    }

    public bool GlareEnabled
    {
        get => _sceneHost.GlareEnabled;
        set
        {
            if (_sceneHost.GlareEnabled == value)
            {
                return;
            }
            _sceneHost.SetGlareEnabled(value);
            OnPropertyChanged();
        }
    }

    public object? SelectedOutlineItem
    {
        get => _selectedOutlineItem;
        set
        {
            if (ReferenceEquals(_selectedOutlineItem, value))
            {
                return;
            }
            _selectedOutlineItem = value;
            OnPropertyChanged();
            if (!_syncingSelection)
            {
                SelectOutlineItem(value);
            }
        }
    }

    public bool VisualModelsVisible
    {
        get => _sceneHost.StageModelsVisible;
        set
        {
            _sceneHost.SetStageModelsVisible(value);
            OnPropertyChanged();
        }
    }

    public bool PlacementsVisible
    {
        get => _sceneHost.PlacementsVisible;
        set
        {
            _sceneHost.SetPlacementsVisible(value);
            OnPropertyChanged();
        }
    }

    public bool CollisionVisible
    {
        get => _sceneHost.IsLayerVisible(SceneLayer.Collision);
        set => SetLayerVisible(SceneLayer.Collision, value);
    }

    public bool EffectsVisible
    {
        get => _sceneHost.IsLayerVisible(SceneLayer.Effects);
        set => SetLayerVisible(SceneLayer.Effects, value);
    }

    public bool GridVisible
    {
        get => _sceneHost.IsLayerVisible(SceneLayer.Grid);
        set => SetLayerVisible(SceneLayer.Grid, value);
    }

    public bool OverlayVisible
    {
        get => _sceneHost.IsLayerVisible(SceneLayer.Overlay);
        set => SetLayerVisible(SceneLayer.Overlay, value);
    }

    public bool LightsVisible
    {
        get => _sceneHost.IsLayerVisible(SceneLayer.Lights);
        set => SetLayerVisible(SceneLayer.Lights, value);
    }

    public IReadOnlyList<LitDocumentSession> LightDocuments => _lightDocuments;
    public LitDocumentSession? PrimaryLightDocument { get; private set; }
    public bool HasLights => _lightDocuments.Count > 0;
    public string LightSummary => _lightDocuments.Count == 0
        ? "No stage lights loaded"
        : $"{_lightDocuments.Count} light file(s), {_lightEntities.Count(entity => !entity.IsGlobal)} light record(s), " +
          $"active {PrimaryLightDocument?.DisplayName ?? "none"}" +
          (string.IsNullOrWhiteSpace(_primaryLightSelectionReason)
              ? string.Empty
              : $" ({_primaryLightSelectionReason})");
    public bool CanAddLightGroup => PrimaryLightDocument != null;
    public bool CanEditSelectedLightStructure => _selectedEntity is LightEntity { IsGlobal: false };
    public string LightBoundsWarning => _selectedEntity is LightEntity { IsOutsideGroupBounds: true }
        ? "This light is outside its group AABB and will be culled by the game."
        : string.Empty;
    public bool HasLightBoundsWarning => !string.IsNullOrEmpty(LightBoundsWarning);

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task DiscoverLightsAsync(
        IWorkspaceCatalog workspace,
        string? preferredStageStem = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var snapshot = workspace.Snapshot;
        if (snapshot == null)
        {
            ReplaceLightDocuments([]);
            return;
        }

        var normalizedStage = NormalizeStageStem(preferredStageStem);
        var paths = snapshot.WithExtension(".lt2")
            .Concat(snapshot.WithExtension(".lt3"))
            .OrderBy(file => LightDiscoveryRank(file.Name, normalizedStage))
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => file.Path)
            .ToList();

        var loaded = new List<LitDocumentSession>();
        var errors = new List<string>();
        await Task.Run(() =>
        {
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    loaded.Add(LitDocumentSession.Load(workspace, path));
                }
                catch (Exception exception) when (exception is InvalidDataException or IOException)
                {
                    errors.Add($"{path.FileName}: {exception.Message}");
                }
            }
        }, cancellationToken);

        var selection = StageLightResolver.Resolve(
            workspace,
            loaded,
            normalizedStage,
            cancellationToken);
        ReplaceLightDocuments(loaded, selection.Primary, selection.Reason);
        // Register any ".abc" ambient-cube files shipped in the stage cache.
        var abcStatus = Mgs4AmbientCubeLoader.RegisterFromWorkspace(workspace, snapshot);
        if (!string.IsNullOrEmpty(abcStatus))
        {
            SetManipulationStatus(abcStatus);
        }
        SetManipulationStatus(errors.Count == 0
            ? string.Empty
            : $"Some light files could not be loaded: {string.Join("; ", errors)}");
    }

    public async Task LoadLightsFromWorkspacePathAsync(
        WorkspacePath path,
        IWorkspaceCatalog workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(workspace);
        var session = await Task.Run(() => LitDocumentSession.Load(workspace, path), cancellationToken);
        var documents = _lightDocuments
            .Where(candidate => candidate.Path != path)
            .ToList();
        documents.Insert(0, session);
        ReplaceLightDocuments(documents, session, "selected manually");
        SelectLight(_lightEntities.First(entity => ReferenceEquals(entity.Session, session)));
    }

    public void ToggleInspector()
    {
        if (_selectedEntity != null)
        {
            InspectorExpanded = !InspectorExpanded;
        }
    }

    public void AddLightGroup()
    {
        var session = PrimaryLightDocument;
        if (session == null)
        {
            return;
        }
        var center = SpawnPosition;
        var group = new HavenStudio.Formats.Lit.LitGroup
        {
            Type = 1,
            BoundsMin = new Vector4(center - new Vector3(500), 0),
            BoundsMax = new Vector4(center + new Vector3(500), 0)
        };
        group.Lights.Add(CreateDefaultLight(group.Type, session, center));
        var index = session.Document.Groups.Count;
        SetSelectedEntity(null, null);
        _history.Execute(
            "add light group",
            () => ApplyLightStructureChange(session, () => session.Document.Groups.Insert(index, group)),
            () => ApplyLightStructureChange(session, () => session.Document.Groups.Remove(group)));
    }

    public void AddLightToSelectedGroup()
    {
        if (_selectedEntity is not LightEntity { Group: { } group } entity || group.Type == 64)
        {
            return;
        }
        var light = CreateDefaultLight(group.Type, entity.Session, entity.GetPosition() ?? SpawnPosition);
        var index = group.Lights.Count;
        SetSelectedEntity(null, null);
        _history.Execute(
            "add light",
            () => ApplyLightStructureChange(entity.Session, () => group.Lights.Insert(index, light)),
            () => ApplyLightStructureChange(entity.Session, () => group.Lights.Remove(light)));
    }

    public void DeleteSelectedLight()
    {
        if (_selectedEntity is not LightEntity { Group: { } group, Light: { } light } entity)
        {
            return;
        }
        var index = entity.RecordIndex!.Value;
        SetSelectedEntity(null, null);
        _history.Execute(
            "delete light",
            () => ApplyLightStructureChange(entity.Session, () => group.Lights.RemoveAt(index)),
            () => ApplyLightStructureChange(entity.Session, () => group.Lights.Insert(index, light)));
    }

    public void DeleteSelectedLightGroup()
    {
        if (_selectedEntity is not LightEntity { Group: { } group } entity)
        {
            return;
        }
        var index = entity.GroupIndex!.Value;
        SetSelectedEntity(null, null);
        _history.Execute(
            "delete light group",
            () => ApplyLightStructureChange(entity.Session, () => entity.Session.Document.Groups.RemoveAt(index)),
            () => ApplyLightStructureChange(entity.Session, () => entity.Session.Document.Groups.Insert(index, group)));
    }

    public void GrowSelectedLightBounds()
    {
        if (_selectedEntity is not LightEntity { Group: { } group } entity || entity.GetPosition() is not { } position)
        {
            return;
        }
        var margin = MathF.Max(100f, entity.Light switch
        {
            HavenStudio.Formats.Lit.LitPointLight point => point.ExtendedRange,
            HavenStudio.Formats.Lit.LitLineLight line => line.Range,
            HavenStudio.Formats.Lit.LitBlackPoint blackPoint => blackPoint.Range,
            _ => 100f
        });
        var beforeMin = group.BoundsMin;
        var beforeMax = group.BoundsMax;
        var afterMin = new Vector4(Vector3.ComponentMin(group.BoundsMin.Xyz, position - new Vector3(margin)), group.BoundsMin.W);
        var afterMax = new Vector4(Vector3.ComponentMax(group.BoundsMax.Xyz, position + new Vector3(margin)), group.BoundsMax.W);
        _history.Execute(
            "grow light group bounds",
            () => ApplyLightBounds(entity, afterMin, afterMax),
            () => ApplyLightBounds(entity, beforeMin, beforeMax));
    }

    public void SelectAt(Point point, OpenGL3DControl control)
    {
        var entity = PickEntityAt(point, control);
        if (entity == null)
        {
            return;
        }

        SelectEntity(entity);
    }

    public void PointerPressed(Point point, OpenGL3DControl control)
    {
        var entity = PickEntityAt(point, control);
        _manipulationController.PointerPressed(point, CreateManipulationTarget(entity));
    }

    public void PointerMoved(Point point, OpenGL3DControl control, bool heightOnly)
    {
        if (_manipulationController.TryUpdate(point, control, heightOnly, out var update))
        {
            ProcessDragUpdate(update);
            if (update.Target.Entity is not LightEntity)
            {
                ApplyPreviewLighting(update.Target.Models);
            }
        }
    }

    public void PointerReleased(Point point, OpenGL3DControl control, bool heightOnly)
    {
        if (_manipulationController.TryUpdate(point, control, heightOnly, out var update))
        {
            ProcessDragUpdate(update);
        }

        var completion = _manipulationController.PointerReleased();
        if (completion.IsClick)
        {
            SelectAt(point, control);
            SetSpawnPoint(point, control);
            return;
        }
        if (!completion.IsDrag || completion.Target == null)
        {
            return;
        }

        var entity = (MapEntity)completion.Target.Entity;
        if (!TryApplyEntityPosition(entity, completion.EndPosition, out var error))
        {
            _manipulationController.PreviewPosition(completion.Target, completion.StartPosition);
            _history.CancelCoalesced();
            SetManipulationStatus(error ?? "The selected entity could not be moved.");
            return;
        }

        var storedPosition = GetEntityPosition(entity) ?? completion.EndPosition;
        _manipulationController.PreviewPosition(completion.Target, storedPosition);
        if ((storedPosition - completion.StartPosition).LengthSquared < 0.000001f)
        {
            _history.CancelCoalesced();
            return;
        }

        _history.UpdateCoalesced(() => ApplyHistoryPosition(entity, storedPosition));
        _history.CommitCoalesced();
        SetManipulationStatus(string.Empty);
    }

    public void CancelManipulation()
    {
        _manipulationController.Cancel();
        _history.CancelCoalesced();
    }

    public void SetAxisConstraint(MapDragAxis axis, bool enabled)
    {
        _manipulationController.SetAxisConstraint(axis, enabled);
    }

    public void Undo()
    {
        RunHistoryOperation(_history.Undo);
    }

    public void Redo()
    {
        RunHistoryOperation(_history.Redo);
    }

    public void AddEffectAtCamera()
    {
        if (_collisionEditor.AddEffectAtCamera() is not { } change)
        {
            return;
        }

        _history.RecordApplied(
            "add effect",
            () => _collisionEditor.RemoveEffect(change, change.PreviousSelection),
            () => _collisionEditor.RestoreEffect(change, change.Effect));
    }

    public void DeleteSelectedEffect()
    {
        if (_collisionEditor.DeleteSelectedEffect() is not { } change)
        {
            return;
        }

        _history.RecordApplied(
            "delete effect",
            () => _collisionEditor.RestoreEffect(change, change.Effect),
            () => _collisionEditor.RemoveEffect(change, null));
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_gcxEditor.HasDocument && _gcxEditor.IsDirty)
        {
            await _gcxEditor.SaveSelectedScriptAsync(cancellationToken);
        }
        if (_collisionEditor.HasGeomLoaded && _collisionEditor.IsDirty)
        {
            await _collisionEditor.SaveAsync(cancellationToken);
        }
        foreach (var session in _lightDocuments.Where(session => session.IsDirty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(session.Save, cancellationToken);
        }
    }

    private MapEntity? PickEntityAt(Point point, OpenGL3DControl control)
    {
        var pickModels = _sceneHost.GetLayerModels(SceneLayer.VisualModels)
            .Where(model => model.Visible && _sceneHost.TryGetPlacement(model, out _))
            .Concat(_sceneHost.GetLayerModels(SceneLayer.Collision).Where(model => model.Visible))
            .Concat(_sceneHost.GetLayerModels(SceneLayer.Effects).Where(model => model.Visible))
            .Concat(_sceneHost.GetLayerModels(SceneLayer.Lights).Where(model => model.Visible))
            .ToList();

        if (!SelectionRaycaster.TryPickTriangle(point, control, pickModels, out var hit))
        {
            return null;
        }

        if (_sceneHost.TryGetPlacement(hit.Model, out var placement) &&
            _placements.TryGetValue(placement, out var placementEntity))
        {
            return placementEntity;
        }

        if (_lightByModel.TryGetValue(hit.Model, out var lightEntity))
        {
            return lightEntity;
        }

        if (_collisionEditor.TryResolveHit(hit, out var collisionSelection))
        {
            if (collisionSelection.Effect != null)
            {
                return new EffectEntity(collisionSelection.Effect);
            }
            if (collisionSelection.Prim != null)
            {
                return new PrimEntity(collisionSelection.Prim, collisionSelection.GeoPrim);
            }
            if (collisionSelection.Block != null)
            {
                return new BlockEntity(collisionSelection.Block);
            }
        }

        return null;
    }

    public void SetSpawnPoint(Point point, OpenGL3DControl control)
    {
        var surfaces = _sceneHost.GetLayerModels(SceneLayer.Collision)
            .Concat(_sceneHost.GetLayerModels(SceneLayer.Grid));
        if (SelectionRaycaster.TryPickPoint(point, control, surfaces, out var hitPoint))
        {
            _spawnPosition = hitPoint;
        }
        else if (SelectionRaycaster.TryGetPickRay(point, control, out var origin, out var direction) &&
            MathF.Abs(direction.Y) > 0.0001f &&
            -origin.Y / direction.Y >= 0)
        {
            _spawnPosition = origin + direction * (-origin.Y / direction.Y);
        }
        else
        {
            _spawnPosition = _sceneHost.Scene.Camera.Target;
        }
        OnPropertyChanged(nameof(SpawnPosition));
        OnPropertyChanged(nameof(SpawnPositionText));
    }

    public async Task AddObjectAsync(
        byte[] commandBytes,
        string targetProcedure,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var placement = await _gcxEditor.AddObjectAsync(commandBytes, targetProcedure, cancellationToken);
            if (placement == null)
            {
                _addObjectStatus = "The object command was inserted, but no model placement could be resolved.";
            }
            else if (_placements.TryGetValue(placement, out var entity))
            {
                _addObjectStatus = string.Empty;
                SelectPlacement(entity);
            }
            else
            {
                _addObjectStatus = "The model was inserted but its MDN could not be loaded from the workspace.";
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            _addObjectStatus = exception.Message;
        }
        OnPropertyChanged(nameof(AddObjectStatus));
        OnPropertyChanged(nameof(HasAddObjectStatus));
    }

    public async Task DuplicatePlacementAsync(
        PlacementEntity placement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);
        CollisionEffectDuplicate? effectDuplicate = null;
        try
        {
            if (!placement.CanDuplicate)
            {
                _addObjectStatus = placement.DuplicateToolTip;
            }
            else
            {
                if (placement.Placement.SourceEffect is { } sourceEffect)
                {
                    effectDuplicate = _collisionEditor.DuplicateEffectForPlacement(sourceEffect);
                }

                var duplicate = await _gcxEditor.DuplicatePlacementAsync(
                    placement.Placement,
                    effectDuplicate?.Hash,
                    cancellationToken);
                if (duplicate == null)
                {
                    _addObjectStatus = "The placement was duplicated, but the copy could not be resolved.";
                }
                else if (_placements.TryGetValue(duplicate, out var duplicateEntity))
                {
                    _addObjectStatus = string.Empty;
                    SelectPlacement(duplicateEntity);
                }
                else
                {
                    _addObjectStatus = "The placement was duplicated, but its MDN could not be loaded from the workspace.";
                }
            }
        }
        catch (Exception exception) when (
            exception is InvalidDataException or InvalidOperationException or OverflowException)
        {
            if (effectDuplicate != null)
            {
                _collisionEditor.RemoveEffect(
                    effectDuplicate.Change,
                    effectDuplicate.Change.PreviousSelection);
            }
            _addObjectStatus = exception.Message;
        }
        OnPropertyChanged(nameof(AddObjectStatus));
        OnPropertyChanged(nameof(HasAddObjectStatus));
    }

    public void ReportAddObjectStatus(string message)
    {
        _addObjectStatus = message ?? string.Empty;
        OnPropertyChanged(nameof(AddObjectStatus));
        OnPropertyChanged(nameof(HasAddObjectStatus));
    }

    public void FocusSelected()
    {
        switch (_selectedEntity)
        {
            case PlacementEntity placement:
                FocusOnModels(placement.Models);
                break;
            case BlockEntity block:
                _collisionEditor.FocusOnBlock(block.Block);
                break;
            case PrimEntity prim:
                _collisionEditor.FocusOnPrim(prim.Prim);
                break;
            case EffectEntity effect:
                _collisionEditor.FocusOnEffect(effect.Effect);
                break;
            case LightEntity light:
                FocusOnModels(light.Models);
                break;
        }
    }

    public void ClearSelection()
    {
        _collisionEditor.ClearSelection();
        SetSelectedEntity(null, null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _sceneHost.LayerChanged -= OnLayerChanged;
        _collisionEditor.SelectionChanged -= OnCollisionSelectionChanged;
        _collisionEditor.PropertyChanged -= OnCollisionEditorPropertyChanged;
        _gcxEditor.PropertyChanged -= OnGcxEditorPropertyChanged;
        _history.Changed -= OnHistoryChanged;
        foreach (var session in _lightDocuments)
        {
            session.Changed -= OnLightDocumentChanged;
        }
        CancelLightingBake();
        CancelManipulation();
    }

    private void ProcessDragUpdate(MapDragUpdate update)
    {
        var entity = (MapEntity)update.Target.Entity;
        if (update.Started)
        {
            SelectEntity(entity);
            var startPosition = update.Target.Position;
            _history.BeginCoalesced(
                $"move {entity.DisplayName}",
                () => ApplyHistoryPosition(entity, startPosition));
        }

        var position = update.Position;
        _history.UpdateCoalesced(() => ApplyHistoryPosition(entity, position));
    }

    private MapManipulationTarget? CreateManipulationTarget(MapEntity? entity)
    {
        switch (entity)
        {
            case PlacementEntity placement when
                placement.CanEditPosition && placement.Placement.Position is { } position:
            {
                var models = placement.Models.ToList();
                if (UsesEffectPosition(placement.Placement) &&
                    FindEffectViewModel(placement.Placement.SourceEffect!) is { } effect &&
                    _collisionEditor.TryGetEffectModel(effect, out var effectModel))
                {
                    models.Add(effectModel);
                }
                return new MapManipulationTarget(placement, position, models);
            }
            case EffectEntity effect:
            {
                var models = new List<Model3D>();
                if (_collisionEditor.TryGetEffectModel(effect.Effect, out var effectModel))
                {
                    models.Add(effectModel);
                }
                foreach (var (placement, placementEntity) in _placements)
                {
                    if (UsesEffectPosition(placement) &&
                        ReferenceEquals(placement.SourceEffect, effect.Effect.Effect))
                    {
                        models.AddRange(placementEntity.Models);
                    }
                }
                return models.Count == 0
                    ? null
                    : new MapManipulationTarget(
                        effect,
                        new Vector3(effect.Effect.X, effect.Effect.Y, effect.Effect.Z),
                        models);
            }
            case LightEntity light when light.CanEditPosition && light.GetPosition() is { } position:
                return new MapManipulationTarget(light, position, light.Models);
            default:
                return null;
        }
    }

    private bool TryApplyEntityPosition(MapEntity entity, Vector3 position, out string? error)
    {
        switch (entity)
        {
            case PlacementEntity placement:
                error = UpdatePlacementPositionCore(placement.Placement, position);
                placement.RefreshPositionFromSource();
                return error == null;
            case EffectEntity effect:
                if (!_collisionEditor.TrySetEffectPosition(effect.Effect.Effect, position))
                {
                    error = "The selected GEOM effect is no longer loaded.";
                    return false;
                }
                SynchronizePlacementsFromEffect(effect.Effect.Effect, position);
                error = null;
                return true;
            case LightEntity light:
                if (!light.SetPositionDirect(position))
                {
                    error = "The selected light does not have an editable position.";
                    return false;
                }
                light.Session.MarkDirty();
                error = null;
                return true;
            default:
                error = "Collision geometry cannot be translated in this phase.";
                return false;
        }
    }

    private void ApplyHistoryPosition(MapEntity entity, Vector3 position)
    {
        if (!TryApplyEntityPosition(entity, position, out var error))
        {
            throw new InvalidOperationException(error ?? "The map edit could not be applied.");
        }
        _sceneHost.InvalidateShadowTransforms();
    }

    private static Vector3? GetEntityPosition(MapEntity entity)
    {
        return entity switch
        {
            PlacementEntity placement => placement.Placement.Position,
            EffectEntity effect => new Vector3(effect.Effect.X, effect.Effect.Y, effect.Effect.Z),
            LightEntity light => light.GetPosition(),
            _ => null
        };
    }

    private void SelectEntity(MapEntity entity)
    {
        switch (entity)
        {
            case PlacementEntity placement:
                SelectPlacement(placement);
                break;
            case EffectEntity effect:
                _collisionEditor.SelectedEffect = effect.Effect;
                break;
            case LightEntity light:
                SelectLight(light);
                break;
            case PrimEntity prim:
                _collisionEditor.Select(new CollisionSceneSelection(
                    prim.Prim.ParentBlock,
                    prim.Prim,
                    prim.GeoPrim,
                    null));
                break;
            case BlockEntity block:
                _collisionEditor.SelectedBlock = block.Block;
                break;
        }
    }

    private CollisionEffectViewModel? FindEffectViewModel(GeoEffect effect)
    {
        return TreeTraversal.Flatten(_collisionEditor.Effects, effect => effect.Children)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.Effect, effect));
    }

    private static bool UsesEffectPosition(PlacedModelReference placement)
    {
        return placement.Binding?.Site.Editable != true && placement.SourceEffect != null;
    }

    private void SynchronizePlacementsFromEffect(GeoEffect effect, Vector3 position)
    {
        foreach (var (candidate, entity) in _placements)
        {
            if (!UsesEffectPosition(candidate) || !ReferenceEquals(candidate.SourceEffect, effect))
            {
                continue;
            }

            candidate.Position = position;
            entity.RefreshPositionFromSource();
        }
    }

    private void RunHistoryOperation(Func<bool> operation)
    {
        try
        {
            if (operation())
            {
                SetManipulationStatus(string.Empty);
            }
        }
        catch (InvalidOperationException exception)
        {
            SetManipulationStatus(exception.Message);
        }
    }

    private void SetManipulationStatus(string message)
    {
        _manipulationStatus = message ?? string.Empty;
        OnPropertyChanged(nameof(ManipulationStatus));
        OnPropertyChanged(nameof(HasManipulationStatus));
    }

    private void OnHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoToolTip));
        OnPropertyChanged(nameof(RedoToolTip));
    }

    private void SelectOutlineItem(object? item)
    {
        switch (item)
        {
            case PlacementEntity placement:
                SelectPlacement(placement);
                break;
            case CollisionBlockViewModel block:
                _collisionEditor.SelectedBlock = block;
                break;
            case CollisionPrimViewModel prim:
                _collisionEditor.SelectedPrim = prim;
                break;
            case CollisionGeoPrimViewModel geoPrim:
                _collisionEditor.SelectedGeoPrim = geoPrim;
                break;
            case CollisionEffectViewModel effect:
                _collisionEditor.SelectedEffect = effect;
                break;
            case LightEntity light:
                SelectLight(light);
                break;
            case LightFileOutline or LightGroupOutline:
                _collisionEditor.ClearSelection();
                SetSelectedEntity(null, item);
                break;
            case MapOutlineGroup group:
                _collisionEditor.ClearSelection();
                SetSelectedEntity(null, group);
                break;
            case null:
                ClearSelection();
                break;
        }
    }

    private void SelectPlacement(PlacementEntity placement)
    {
        _collisionEditor.ClearSelection();
        SetSelectedEntity(placement, placement);
    }

    private void SelectLight(LightEntity light)
    {
        _collisionEditor.ClearSelection();
        SetSelectedEntity(light, light);
    }

    private void OnCollisionSelectionChanged()
    {
        if (_collisionEditor.SelectedEffect is { } effect)
        {
            SetSelectedEntity(new EffectEntity(effect), effect);
            return;
        }
        if (_collisionEditor.SelectedPrim is { } prim)
        {
            var geoPrim = _collisionEditor.SelectedGeoPrim;
            SetSelectedEntity(new PrimEntity(prim, geoPrim), (object?)geoPrim ?? prim);
            return;
        }
        if (_collisionEditor.SelectedBlock is { } block)
        {
            SetSelectedEntity(new BlockEntity(block), block);
            return;
        }

        if (_selectedEntity is not PlacementEntity and not LightEntity)
        {
            SetSelectedEntity(null, null);
        }
    }

    private void SetSelectedEntity(MapEntity? entity, object? outlineItem)
    {
        var previousLight = _selectedEntity as LightEntity;
        _selectedEntity = entity;
        InspectorExpanded = entity != null;
        _syncingSelection = true;
        try
        {
            _selectedOutlineItem = outlineItem;
            OnPropertyChanged(nameof(SelectedOutlineItem));
        }
        finally
        {
            _syncingSelection = false;
        }
        OnPropertyChanged(nameof(SelectedEntity));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanEditSelectedLightStructure));
        OnPropertyChanged(nameof(LightBoundsWarning));
        OnPropertyChanged(nameof(HasLightBoundsWarning));
        if (!ReferenceEquals(previousLight, entity as LightEntity))
        {
            RebuildLightScene(entity as LightEntity);
        }
    }

    private void RefreshOutline()
    {
        RefreshPlacements();
        ReplaceChildren(_collisionGroup, _collisionEditor.Blocks.Cast<object>());
        ReplaceChildren(_effectsGroup, _collisionEditor.Effects.Cast<object>());
        RefreshLightOutline();
    }

    private void RefreshPlacements()
    {
        var previousSelection = _selectedEntity as PlacementEntity;
        var previousBinding = previousSelection?.Placement.Binding;
        var previousCommandOffset = previousBinding?.Site.CommandOffset;
        var previousModelValueOffset = previousBinding?.ModelSite?.ValueOffset;
        var previousForeachRow = previousBinding?.ForeachRowIndex;
        _placements.Clear();
        var modelsByPlacement = new Dictionary<PlacedModelReference, List<Model3D>>(ReferenceEqualityComparer.Instance);
        foreach (var model in _sceneHost.GetLayerModels(SceneLayer.VisualModels))
        {
            if (!_sceneHost.TryGetPlacement(model, out var placement))
            {
                continue;
            }
            if (!modelsByPlacement.TryGetValue(placement, out var models))
            {
                models = [];
                modelsByPlacement[placement] = models;
            }
            models.Add(model);
        }

        var projectModels = BuildProjectModelOptions(modelsByPlacement.Keys);
        var geoReferences = BuildGeoReferenceOptions(modelsByPlacement.Keys);

        var index = 1;
        foreach (var (placement, models) in modelsByPlacement)
        {
            var resolved = HavenStudio.Utils.DictionaryFile.GetHashString(placement.ModelHash);
            var name = string.IsNullOrWhiteSpace(resolved) || resolved.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? $"Placement {index} — 0x{placement.ModelHash:X8}"
                : $"Placement {index} — {resolved}";
            _placements[placement] = new PlacementEntity(
                placement,
                models,
                name,
                projectModels,
                geoReferences,
                UpdatePlacementPositionFromInspector,
                _gcxEditor.UpdatePlacementModelHash,
                _gcxEditor.UpdatePlacementCollisionReference);
            index++;
        }

        ReplaceChildren(_placementsGroup, _placements.Values.Cast<object>());
        if (previousSelection != null && _placements.TryGetValue(previousSelection.Placement, out var replacement))
        {
            SetSelectedEntity(replacement, replacement);
        }
        else if (previousSelection != null)
        {
            var reboundSelection = _placements.Values.FirstOrDefault(candidate =>
                previousBinding != null &&
                ReferenceEquals(candidate.Placement.Binding?.Script, previousBinding.Script) &&
                candidate.Placement.Binding?.Site.CommandOffset == previousCommandOffset &&
                candidate.Placement.Binding?.ModelSite?.ValueOffset == previousModelValueOffset &&
                candidate.Placement.Binding?.ForeachRowIndex == previousForeachRow);
            SetSelectedEntity(reboundSelection, reboundSelection);
        }
    }

    private IReadOnlyList<ProjectModelOption> BuildProjectModelOptions(
        IEnumerable<PlacedModelReference> placements)
    {
        var options = _gcxEditor.GetProjectModelPaths()
            .Select(pair => new ProjectModelOption(
                pair.Key,
                $"{Path.GetFileNameWithoutExtension(pair.Value.FileName)}  (0x{pair.Key:X6})"))
            .ToList();
        var knownHashes = options.Select(option => option.Hash).ToHashSet();
        foreach (var hash in placements.Select(placement => placement.ModelHash).Distinct())
        {
            if (hash != 0 && knownHashes.Add(hash))
            {
                options.Add(new ProjectModelOption(hash, $"Missing MDN  (0x{hash:X6})"));
            }
        }
        return options
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Hash)
            .ToArray();
    }

    private IReadOnlyList<GeoReferenceOption> BuildGeoReferenceOptions(
        IEnumerable<PlacedModelReference> placements)
    {
        var hashes = (_collisionEditor.GeomFile?.GeomRefs ?? [])
            .Select(reference => reference.Hash)
            .Concat(placements.Select(placement => placement.CollisionReferenceHash.GetValueOrDefault()))
            .Where(hash => hash != 0)
            .Distinct()
            .OrderBy(hash => ResolveGeoReferenceName(hash), StringComparer.OrdinalIgnoreCase)
            .ThenBy(hash => hash)
            .ToList();
        var options = new List<GeoReferenceOption>(hashes.Count + 1)
        {
            new(0, "None")
        };
        options.AddRange(hashes.Select(hash =>
            new GeoReferenceOption(hash, $"{ResolveGeoReferenceName(hash)}  (0x{hash:X6})")));
        return options;
    }

    private string? UpdatePlacementPositionFromInspector(
        PlacedModelReference placement,
        Vector3 position)
    {
        var before = placement.Position;
        var error = UpdatePlacementPositionCore(placement, position);
        if (error != null || before == null || placement.Position == null || before == placement.Position)
        {
            return error;
        }

        var after = placement.Position.Value;
        _sceneHost.InvalidateShadowTransforms();
        _history.RecordApplied(
            "move placement",
            () => ApplyPlacementHistoryPosition(placement, before.Value),
            () => ApplyPlacementHistoryPosition(placement, after));
        return null;
    }

    private string? UpdatePlacementPositionCore(PlacedModelReference placement, Vector3 position)
    {
        if (placement.Binding?.Site.Editable == true)
        {
            return _gcxEditor.UpdatePlacementPosition(placement, position);
        }

        if (placement.SourceEffect == null)
        {
            return "No direct writable position or GEOM effect was found.";
        }
        if (!_collisionEditor.TrySetEffectPosition(placement.SourceEffect, position))
        {
            return "The placement's GEOM effect is not loaded in the collision editor.";
        }

        SynchronizePlacementsFromEffect(placement.SourceEffect, position);
        return null;
    }

    private void ApplyPlacementHistoryPosition(PlacedModelReference placement, Vector3 position)
    {
        var error = UpdatePlacementPositionCore(placement, position);
        if (error != null)
        {
            throw new InvalidOperationException(error);
        }
        if (_placements.TryGetValue(placement, out var entity))
        {
            entity.RefreshPositionFromSource();
        }
        _sceneHost.InvalidateShadowTransforms();
    }

    private static string ResolveGeoReferenceName(uint hash)
    {
        var resolved = HavenStudio.Utils.DictionaryFile.GetHashString(hash);
        return string.IsNullOrWhiteSpace(resolved) ||
            string.Equals(resolved, hash.ToString("X4"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(resolved, hash.ToString("X6"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(resolved, hash.ToString("X8"), StringComparison.OrdinalIgnoreCase)
                ? "Unnamed collision mesh"
                : resolved;
    }

    private void ReplaceLightDocuments(
        IEnumerable<LitDocumentSession> documents,
        LitDocumentSession? resolvedPrimary = null,
        string? selectionReason = null)
    {
        if (_selectedEntity is LightEntity)
        {
            SetSelectedEntity(null, null);
        }
        foreach (var session in _lightDocuments)
        {
            session.Changed -= OnLightDocumentChanged;
        }

        _lightDocuments.Clear();
        _lightDocuments.AddRange(documents);
        foreach (var session in _lightDocuments)
        {
            session.Changed += OnLightDocumentChanged;
        }

        // Selection is resolved from direct name/hash evidence, model-family
        // correspondence, document structure, and only then file size. This avoids
        // both alphabetical selection and a fragile "largest file always wins" rule.
        PrimaryLightDocument = resolvedPrimary != null && _lightDocuments.Contains(resolvedPrimary)
            ? resolvedPrimary
            : _lightDocuments.FirstOrDefault(session => !session.IsSkyPass && !session.IsPreviewPass)
              ?? _lightDocuments.FirstOrDefault(session => !session.IsSkyPass)
              ?? _lightDocuments.FirstOrDefault();
        _primaryLightSelectionReason = selectionReason ?? string.Empty;
        CancelManipulation();
        _history.Clear();
        RefreshLightOutline();
        OnPropertyChanged(nameof(LightDocuments));
        OnPropertyChanged(nameof(PrimaryLightDocument));
        OnPropertyChanged(nameof(HasLights));
        OnPropertyChanged(nameof(LightSummary));
        OnPropertyChanged(nameof(CanAddLightGroup));
        ApplyGameLighting();
    }

    private void RefreshLightOutline()
    {
        _lightEntities.Clear();
        var files = new List<object>(_lightDocuments.Count);
        foreach (var session in _lightDocuments)
        {
            var fileNode = new LightFileOutline(session);
            var global = new LightEntity(session, null, null, "Global light", ApplyLightEdit);
            fileNode.Children.Add(global);
            _lightEntities.Add(global);

            for (var groupIndex = 0; groupIndex < session.Document.Groups.Count; groupIndex++)
            {
                var group = session.Document.Groups[groupIndex];
                var groupNode = new LightGroupOutline(groupIndex, group);
                for (var recordIndex = 0; recordIndex < group.Lights.Count; recordIndex++)
                {
                    var entity = new LightEntity(
                        session,
                        groupIndex,
                        recordIndex,
                        $"Light {recordIndex} — {LightTypeName(group.Type)}",
                        ApplyLightEdit);
                    groupNode.Children.Add(entity);
                    _lightEntities.Add(entity);
                }
                fileNode.Children.Add(groupNode);
            }
            files.Add(fileNode);
        }

        ReplaceChildren(_lightsGroup, files);
        RebuildLightScene(_selectedEntity as LightEntity);
        OnPropertyChanged(nameof(LightSummary));
    }

    private void RebuildLightScene(LightEntity? selected)
    {
        _lightByModel.Clear();
        var models = new List<Model3D>();
        foreach (var entity in _lightEntities)
        {
            var entityModels = LightSceneBuilder.BuildEntity(entity, ReferenceEquals(entity, selected));
            entity.Models = entityModels;
            foreach (var model in entityModels)
            {
                models.Add(model);
                _lightByModel[model] = entity;
            }
        }
        _sceneHost.ReplaceLayer(SceneLayer.Lights, models);
    }

    private void OnLightDocumentChanged()
    {
        RebuildLightScene(_selectedEntity as LightEntity);
        ApplyGameLighting();
        OnPropertyChanged(nameof(SelectedEntity));
        OnPropertyChanged(nameof(LightSummary));
        OnPropertyChanged(nameof(LightBoundsWarning));
        OnPropertyChanged(nameof(HasLightBoundsWarning));
    }

    private void ApplyLightEdit(LightEntity entity, string description, Action redo, Action undo)
    {
        _history.Execute(description, redo, undo);
        OnPropertyChanged(nameof(LightBoundsWarning));
        OnPropertyChanged(nameof(HasLightBoundsWarning));
    }

    private void ApplyLightStructureChange(LitDocumentSession session, Action mutation)
    {
        mutation();
        RefreshLightOutline();
        session.MarkDirty();
    }

    private void ApplyLightBounds(LightEntity entity, Vector4 min, Vector4 max)
    {
        if (entity.Group is not { } group)
        {
            return;
        }
        group.BoundsMin = min;
        group.BoundsMax = max;
        entity.Session.MarkDirty();
        entity.NotifyAllChanged();
    }

    private static HavenStudio.Formats.Lit.LitLight CreateDefaultLight(
        uint type,
        LitDocumentSession session,
        Vector3 position)
    {
        var document = session.Document;
        HavenStudio.Formats.Lit.LitLight light = type switch
        {
            1 => new HavenStudio.Formats.Lit.LitPointLight
            {
                Point = new Vector4(position, 0),
                Color = document.Color,
                Range = 500,
                ExtendedRange = 1000
            },
            2 => new HavenStudio.Formats.Lit.LitSpotLight
            {
                BoundsMin = new Vector4(position - new Vector3(500), 0),
                BoundsMax = new Vector4(position + new Vector3(500), 0),
                Point = new Vector4(position, 0),
                Direction = new Vector4(0, -1, 0, 0),
                Color = document.Color,
                Umbra = 0.9f,
                Penumbra = 0.7f
            },
            4 => new HavenStudio.Formats.Lit.LitLineLight
            {
                BoundsMin = new Vector4(position - new Vector3(500), 0),
                BoundsMax = new Vector4(position + new Vector3(500), 0),
                Point = new Vector4(position, 0),
                Direction = new Vector4(position + Vector3.UnitY * 500, 0),
                Color = document.Color,
                Range = 500
            },
            8 or 16 => new HavenStudio.Formats.Lit.LitBlackPoint
            {
                BoundsMin = new Vector4(position - new Vector3(500), 0),
                BoundsMax = new Vector4(position + new Vector3(500), 0),
                Point = new Vector4(position, 0),
                Range = 500
            },
            32 => new HavenStudio.Formats.Lit.LitParallelLight
            {
                BoundsMin = new Vector4(position - new Vector3(500), 0),
                BoundsMax = new Vector4(position + new Vector3(500), 0),
                Direction = document.Direction,
                Color = document.Color,
                Ambient = document.Ambient,
                Force = 1
            },
            _ => throw new InvalidOperationException($"Cannot create an editable light for group type {type}.")
        };
        if (document.Variant == HavenStudio.Formats.Lit.LitVariant.Prefixed)
        {
            light.VariantExtra = new byte[16];
        }
        return light;
    }

    private static string LightTypeName(uint type) => type switch
    {
        1 => "point",
        2 => "spot",
        4 => "line",
        8 or 16 => "black point",
        32 => "parallel",
        64 => "projection/raw",
        _ => $"unknown {type}"
    };

    private static int LightDiscoveryRank(string fileName, string normalizedStage)
    {
        var stem = StageLightResolver.NormalizeStem(Path.GetFileNameWithoutExtension(fileName));
        var matchesStage = normalizedStage.Length > 0 &&
            (stem.StartsWith(normalizedStage, StringComparison.OrdinalIgnoreCase) ||
             normalizedStage.StartsWith(stem, StringComparison.OrdinalIgnoreCase));
        var sky = fileName.Contains("sky", StringComparison.OrdinalIgnoreCase);
        var preview = fileName.Contains("preview", StringComparison.OrdinalIgnoreCase);
        return (matchesStage ? 0 : 2) + (sky ? 1 : 0) + (preview ? 100 : 0);
    }

    private static string NormalizeStageStem(string? stem) => StageLightResolver.NormalizeStem(stem);

    private static void ReplaceChildren(MapOutlineGroup group, IEnumerable<object> children)
    {
        group.Children.Clear();
        foreach (var child in children)
        {
            group.Children.Add(child);
        }
    }

    private void OnLayerChanged(SceneLayer layer)
    {
        if (layer == SceneLayer.VisualModels)
        {
            var placements = _sceneHost.GetPlacements();
            var placementSetChanged = placements.Count != _placements.Count ||
                placements.Any(placement => !_placements.ContainsKey(placement));
            if (placementSetChanged)
            {
                CancelManipulation();
                _history.Clear();
            }
            RefreshPlacements();
            ApplyGameLighting();
        }
        NotifyLayerVisibility(layer);
    }

    private void OnCollisionEditorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(CollisionEditorViewModel.BlockSummary))
        {
            ReplaceChildren(_collisionGroup, _collisionEditor.Blocks.Cast<object>());
            ReplaceChildren(_effectsGroup, _collisionEditor.Effects.Cast<object>());
            RefreshPlacements();
        }
        else if (eventArgs.PropertyName == nameof(CollisionEditorViewModel.GeomFile))
        {
            CancelManipulation();
            _history.Clear();
        }
    }

    private void OnGcxEditorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(GcxEditorViewModel.Fog))
        {
            ApplyGameFog();
            OnPropertyChanged(nameof(HasGameFog));
            OnPropertyChanged(nameof(GameFogSummary));
            return;
        }
        if (eventArgs.PropertyName == nameof(GcxEditorViewModel.ColorFilter))
        {
            ApplyGameFilter();
            OnPropertyChanged(nameof(HasGameFilter));
            OnPropertyChanged(nameof(GameFilterSummary));
            return;
        }
        if (eventArgs.PropertyName == nameof(GcxEditorViewModel.SystemLighting))
        {
            ApplyGameLighting();
            return;
        }
        if (eventArgs.PropertyName != nameof(GcxEditorViewModel.HasDocument))
        {
            return;
        }

        CancelManipulation();
        _history.Clear();
        ApplyGameLighting();
        ApplyGameFilter();
        ApplyGameFog();
        OnPropertyChanged(nameof(HasGameFog));
        OnPropertyChanged(nameof(GameFogSummary));
    }

    private void ApplyGameFog()
    {
        // Haven renders viewport 0. Never substitute the engine's black command-local
        // defaults when the GCX command could not be decoded: that was the cause of
        // entire stages becoming black at normal MGS4 world scales.
        if (_gcxEditor.Fog is { } fog && fog.TryGetViewport(0, out var state))
        {
            _sceneHost.SetFog(state);
            return;
        }

        if (_sceneHost.FogEnabled)
        {
            _sceneHost.SetFogEnabled(false);
            OnPropertyChanged(nameof(FogEnabled));
        }
    }

    private void ApplyGameFilter()
    {
        var settings = _gcxEditor.ColorFilter;
        if (settings == null)
        {
            // Useful viewport fallback when the stage's hashed GCX command has not
            // been decoded yet.  This never changes the GCX file.
            _sceneHost.SetColorFilter(new SceneColorFilterSettings(
                0.06f,
                new Vector3(1.10f, 1.04f, 0.78f),
                -0.015f,
                1.08f,
                Vector3.Zero,
                Vector3.One,
                0f));
        }
        else
        {
            _sceneHost.SetColorFilter(settings);
        }
        _sceneHost.SetColorFilterEnabled(_gameFilterEnabled);
    }

    private void SetLayerVisible(SceneLayer layer, bool visible)
    {
        _sceneHost.SetLayerVisible(layer, visible);
        NotifyLayerVisibility(layer);
    }

    private void NotifyLayerVisibility(SceneLayer layer)
    {
        if (layer == SceneLayer.VisualModels)
        {
            OnPropertyChanged(nameof(VisualModelsVisible));
            OnPropertyChanged(nameof(PlacementsVisible));
            return;
        }

        OnPropertyChanged(layer switch
        {
            SceneLayer.Collision => nameof(CollisionVisible),
            SceneLayer.Effects => nameof(EffectsVisible),
            SceneLayer.Lights => nameof(LightsVisible),
            SceneLayer.Grid => nameof(GridVisible),
            SceneLayer.Overlay => nameof(OverlayVisible),
            _ => null
        });
    }

    private void ApplyGameLighting()
    {
        CancelLightingBake();
        var primary = PrimaryLightDocument;
        var sceneLighting = _gcxEditor.SystemLighting;
        if (sceneLighting != null)
        {
            _sceneHost.SetShadowLightDirection(
                LightSampler.ToSurfaceLightDirection(sceneLighting.Direction));
        }
        else if (primary != null && primary.Document.Direction.Xyz.LengthSquared > 0.000001f)
        {
            _sceneHost.SetShadowLightDirection(
                LightSampler.ToSurfaceLightDirection(primary.Document.Direction.Xyz));
        }
        var samples = new Dictionary<PlacedModelReference, SampledLighting>(ReferenceEqualityComparer.Instance);
        var stageModels = new List<(Model3D Model, LightVertexBaker.SpatialBakeInput Input, uint ScopeHash)>();
        foreach (var model in _sceneHost.GetLayerModels(SceneLayer.VisualModels))
        {
            if (!_gameLightingEnabled || primary == null)
            {
                LightVertexBaker.Restore(model);
                continue;
            }

            if (_sceneHost.TryGetPlacement(model, out var placement))
            {
                if (!samples.TryGetValue(placement, out var lighting))
                {
                    // Stage geometry is BACKGROUND, not a character. Sampling it as
                    // Character bypassed the engine participation gate entirely
                    // (measured: 38/41/551 records accepted instead of 0/2/23) and
                    // pulled the character ambient floor instead of the stage one.
                    lighting = LightSampler.Sample(
                        primary.Document,
                        placement.Position ?? model.Position,
                        sceneLighting,
                        HavenStudio.Formats.Lit.LitLightingTarget.Background,
                        ResolveLightingScopeHash(model.SourceAssetName));
                    samples[placement] = lighting;
                }
                LightVertexBaker.Apply(model, lighting);
                continue;
            }

            if (LightVertexBaker.CaptureSpatialBake(model) is { } input)
            {
                stageModels.Add((model, input, ResolveLightingScopeHash(model.SourceAssetName)));
            }
        }

        if (!_gameLightingEnabled || primary == null || stageModels.Count == 0)
        {
            _lightingUpdateTask = Task.CompletedTask;
            _sceneHost.ViewportControl.RequestNextFrameRendering();
            return;
        }

        using var stream = new MemoryStream(primary.Document.ToArray(), writable: false);
        var lightingSnapshot = HavenStudio.Formats.Lit.LitFile.Read(stream);
        var cancellation = new CancellationTokenSource();
        _lightingBakeCancellation = cancellation;
        var version = ++_lightingBakeVersion;
        _lightingUpdateTask = BakeStageLightingAsync(
            stageModels,
            lightingSnapshot,
            sceneLighting,
            version,
            cancellation);
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }

    /// <summary>
    /// Live exposure multiplier, driven by the toolbar slider. Tunable in
    /// decimal without editing the GCX exposure command by hand.
    /// </summary>
    public void SetExposure(float exposureScale)
    {
        _sceneHost.ViewportControl.ExposureScale = exposureScale;
    }

    /// <summary>Live shadow coverage range from the toolbar slider (world units).</summary>
    public void SetShadowRange(float shadowRange)
    {
        _sceneHost.ViewportControl.ShadowRange = shadowRange;
    }

    /// <summary>Live display contrast from the toolbar slider (1.0 = neutral).</summary>
    public void SetContrast(float contrast)
    {
        _sceneHost.ViewportControl.Contrast = contrast;
    }

    private async Task BakeStageLightingAsync(
        IReadOnlyList<(Model3D Model, LightVertexBaker.SpatialBakeInput Input, uint ScopeHash)> stageModels,
        HavenStudio.Formats.Lit.LitFile lighting,
        SceneLightSettings? sceneLighting,
        int version,
        CancellationTokenSource cancellation)
    {
        try
        {
            var results = await Task.Run(() =>
            {
                var token = cancellation.Token;
                // Exact floating-point vertex positions are effectively unique and made
                // the old preview retain one SampledLighting object per vertex. Quantized
                // cells preserve smooth vertex interpolation while bounding RAM/CPU usage.
                var samples = new Dictionary<LightingSampleKey, SampledLighting>();
                var baked = new List<(Model3D Model, LightVertexBaker.BakedLighting Lighting)>(stageModels.Count);
                foreach (var (model, input, scopeHash) in stageModels)
                {
                    token.ThrowIfCancellationRequested();
                    var bakedLighting = LightVertexBaker.BakeSpatialLighting(
                        input.Positions,
                        input.Normals,
                        input.BaseColors,
                        input.VertexCount,
                        input.ModelMatrix,
                        position =>
                        {
                            var key = LightingSampleKey.FromWorld(position, scopeHash);
                            if (!samples.TryGetValue(key, out var sample))
                            {
                                sample = LightSampler.Sample(
                                    lighting,
                                    position,
                                    sceneLighting,
                                    HavenStudio.Formats.Lit.LitLightingTarget.Background,
                                    scopeHash);
                                samples[key] = sample;
                            }
                            return sample;
                        },
                        modulateBaseColor: true,
                        token);
                    baked.Add((model, bakedLighting));
                }
                return baked;
            }, cancellation.Token);

            if (cancellation.IsCancellationRequested ||
                version != _lightingBakeVersion ||
                !_gameLightingEnabled ||
                _disposed)
            {
                return;
            }

            foreach (var (model, bakedLighting) in results)
            {
                LightVertexBaker.ApplyBakedLighting(model, bakedLighting);
            }
            _sceneHost.ViewportControl.RequestNextFrameRendering();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!_disposed && version == _lightingBakeVersion)
            {
                SetManipulationStatus($"Lighting preview failed: {exception.Message}");
            }
        }
        finally
        {
            if (ReferenceEquals(_lightingBakeCancellation, cancellation))
            {
                _lightingBakeCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    private void CancelLightingBake()
    {
        _lightingBakeVersion++;
        _lightingBakeCancellation?.Cancel();
        _lightingBakeCancellation = null;
    }

    private void ApplyPreviewLighting(IEnumerable<Model3D> models)
    {
        if (!_gameLightingEnabled || PrimaryLightDocument == null)
        {
            return;
        }
        foreach (var model in models)
        {
            LightVertexBaker.Apply(
                model,
                LightSampler.Sample(
                    PrimaryLightDocument.Document,
                    model.Position,
                    _gcxEditor.SystemLighting,
                    HavenStudio.Formats.Lit.LitLightingTarget.Background,
                    ResolveLightingScopeHash(model.SourceAssetName)));
        }
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }

    private void FocusOnModels(IReadOnlyList<Model3D> models)
    {
        if (models.Count == 0)
        {
            return;
        }
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var model in models)
        {
            var bounds = model.GetBoundingBox();
            min = Vector3.ComponentMin(min, bounds.Min);
            max = Vector3.ComponentMax(max, bounds.Max);
        }
        var center = (min + max) * 0.5f;
        var size = max - min;
        var radius = MathF.Max(size.X, MathF.Max(size.Y, size.Z)) * 0.5f;
        _sceneHost.ViewportControl.FocusOnBounds(center, radius <= 0.001f ? 1.0f : radius, 1.5f);
    }

    private static uint ResolveLightingScopeHash(string? sourceAssetName)
    {
        var stem = Path.GetFileNameWithoutExtension(sourceAssetName ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        // Stage assets use a seven-character lighting owner prefix, for example
        // s01a13a_ground -> s01a13a. The LT3 type-64 metadata stores exactly the
        // Konami 24-bit hash of this owner.
        if (stem.Length >= 7 &&
            char.IsLetter(stem[0]) &&
            char.IsDigit(stem[1]) &&
            char.IsDigit(stem[2]) &&
            char.IsLetter(stem[3]) &&
            char.IsDigit(stem[4]) &&
            char.IsDigit(stem[5]) &&
            char.IsLetter(stem[6]))
        {
            return HavenStudio.Utils.String.HashString(stem[..7]);
        }

        return 0u;
    }

    private readonly record struct LightingSampleKey(int X, int Y, int Z, uint ScopeHash)
    {
        // 250 MGS4 world units is below the scale of the stage illumination volumes
        // while reducing a million unique vertex keys to a few thousand cells.
        private const float CellSize = 250.0f;

        public static LightingSampleKey FromWorld(Vector3 position, uint scopeHash) => new(
            Quantize(position.X),
            Quantize(position.Y),
            Quantize(position.Z),
            scopeHash);

        private static int Quantize(float value)
        {
            if (!float.IsFinite(value)) return 0;
            var cell = MathF.Floor(value / CellSize);
            if (cell <= int.MinValue) return int.MinValue;
            if (cell >= int.MaxValue) return int.MaxValue;
            return (int)cell;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName != null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
