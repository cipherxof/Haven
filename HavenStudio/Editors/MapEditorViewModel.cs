using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia3DControl;
using Avalonia3DControl.Core.Models;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Editors.Vegetation;
using HavenStudio.Rendering;
using HavenStudio.Services;
using HavenStudio.Formats.Geo;
using HavenStudio.Formats.Mdn;
using HavenStudio.Editors.Lighting;
using HavenStudio.Services.Workspace;
using HavenStudio.Utils;
using OpenTK.Mathematics;
using Serilog;

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
    private readonly Func<PlacedModelReference, Vector3, string?> _updateRotation;
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
        Func<PlacedModelReference, Vector3, string?> updateRotation,
        Func<PlacedModelReference, uint, string?> updateModelHash,
        Func<PlacedModelReference, uint?, string?> updateCollisionReference)
        : base(name)
    {
        Placement = placement;
        Models = models;
        ProjectModels = projectModels;
        GeoReferences = geoReferences;
        _updatePosition = updatePosition;
        _updateRotation = updateRotation;
        _updateModelHash = updateModelHash;
        _updateCollisionReference = updateCollisionReference;
    }

    public PlacedModelReference Placement { get; }
    public IReadOnlyList<Model3D> Models { get; }
    public IReadOnlyList<ProjectModelOption> ProjectModels { get; }
    public IReadOnlyList<GeoReferenceOption> GeoReferences { get; }
    public bool CanDuplicate => Placement.Binding is { } binding &&
        (binding.Site.Foreach != null && binding.ForeachRowIndex != null ||
         binding.Site.Foreach == null &&
         (!binding.Site.IsNested ||
          binding.Site.CommandHash == 0x07A516 && binding.Site.EnclosingBlocks.Count > 0)) &&
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
    public bool CanEditRotation => Placement.SourceEffect != null;
    public float RotationX
    {
        get => RadiansToDegrees(Placement.Rotation?.X ?? 0f);
        set => SetRotationDegrees(value, RotationY, RotationZ);
    }
    public float RotationY
    {
        get => RadiansToDegrees(Placement.Rotation?.Y ?? 0f);
        set => SetRotationDegrees(RotationX, value, RotationZ);
    }
    public float RotationZ
    {
        get => RadiansToDegrees(Placement.Rotation?.Z ?? 0f);
        set => SetRotationDegrees(RotationX, RotationY, value);
    }
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
                    ? "This nested placement does not have a safely resizable NewPutObject command."
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

    private void SetRotationDegrees(float x, float y, float z)
    {
        TryUpdateRotationDegrees(new Vector3(x, y, z), out _);
    }

    public bool TryUpdateRotationDegrees(Vector3 degrees, out string? error)
    {
        if (!CanEditRotation)
        {
            error = "This placement has no writable rotation source.";
            return false;
        }
        var rotation = new Vector3(
            DegreesToRadians(degrees.X),
            DegreesToRadians(degrees.Y),
            DegreesToRadians(degrees.Z));
        if (Placement.Rotation == rotation)
        {
            error = null;
            return true;
        }
        error = _updateRotation(Placement, rotation);
        if (error == null)
        {
            RefreshRotationFromSource();
            return true;
        }
        RefreshRotationFromSource();
        return false;
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

    public void RefreshRotationFromSource()
    {
        if (Placement.Rotation is { } rotation)
        {
            foreach (var model in Models)
            {
                model.Rotation = rotation;
            }
        }
        OnPropertyChanged(nameof(RotationX));
        OnPropertyChanged(nameof(RotationY));
        OnPropertyChanged(nameof(RotationZ));
        OnPropertyChanged(nameof(RotationText));
        OnPropertyChanged(nameof(CanEditRotation));
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
    private static float DegreesToRadians(float value) => value * MathF.PI / 180f;
}

public sealed record BlockEntity(CollisionBlockViewModel Block)
    : MapEntity(Block.DisplayName);

public sealed record PrimEntity(
    CollisionPrimViewModel Prim,
    CollisionGeoPrimViewModel? GeoPrim)
    : MapEntity(GeoPrim?.DisplayName ?? Prim.DisplayName)
{
    public bool HasGeoPrim => GeoPrim != null;
    public OctocamoSelection? Octocamo { get; init; }
    public bool HasOctocamo => Octocamo != null;
}

public sealed record EffectEntity(CollisionEffectViewModel Effect)
    : MapEntity(Effect.DisplayName);

public sealed record EffectSelectionEntity : MapEntity, INotifyPropertyChanged
{
    private readonly Func<float, string?> _updateRotationY;
    private string? _rotationEditStatus;

    public EffectSelectionEntity(
        IReadOnlyList<CollisionEffectViewModel> effects,
        Func<float, string?> updateRotationY)
        : base($"{effects.Count} effects selected")
    {
        Effects = effects;
        _updateRotationY = updateRotationY;
    }

    public IReadOnlyList<CollisionEffectViewModel> Effects { get; }
    public int Count => Effects.Count;

    public string RotationYText
    {
        get
        {
            if (Effects.Count == 0 || HasMixedRotationY)
            {
                return string.Empty;
            }

            return RadiansToDegrees(Effects[0].RotationY)
                .ToString("0.###", CultureInfo.CurrentCulture);
        }
    }

    public bool HasMixedRotationY
    {
        get
        {
            if (Effects.Count <= 1)
            {
                return false;
            }

            var first = Effects[0].RotationY;
            return Effects.Skip(1).Any(effect => MathF.Abs(effect.RotationY - first) > 0.0001f);
        }
    }

    public string RotationYHint => HasMixedRotationY
        ? "Mixed current values. Entering a value applies it to every selected effect."
        : "Applies an absolute Y rotation to every selected effect.";

    public string RotationEditStatus => _rotationEditStatus ?? string.Empty;
    public bool HasRotationEditStatus => !string.IsNullOrWhiteSpace(_rotationEditStatus);

    public bool TryUpdateRotationYDegrees(float degrees, out string? error)
    {
        error = _updateRotationY(degrees);
        _rotationEditStatus = error;
        RefreshRotationState();
        return error == null;
    }

    public void RefreshRotationState()
    {
        OnPropertyChanged(nameof(RotationYText));
        OnPropertyChanged(nameof(HasMixedRotationY));
        OnPropertyChanged(nameof(RotationYHint));
        OnPropertyChanged(nameof(RotationEditStatus));
        OnPropertyChanged(nameof(HasRotationEditStatus));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static float RadiansToDegrees(float value) => value * 180f / MathF.PI;
}

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

public sealed partial class MapEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<MapEditorViewModel>();
    private readonly SceneHost _sceneHost;
    private readonly CollisionEditorViewModel _collisionEditor;
    private readonly GcxEditorViewModel _gcxEditor;
    private readonly MapOutlineGroup _placementsGroup = new("Placements");
    private readonly MapOutlineGroup _vegetationGroup = new("Vegetation");
    private readonly MapOutlineGroup _collisionGroup = new("Collision");
    private readonly MapOutlineGroup _effectsGroup = new("Effects");
    private readonly MapOutlineGroup _gameModeEffectsGroup = new("Game mode locations");
    private readonly MapOutlineGroup _lightsGroup = new("Lights");
    private static readonly HashSet<uint> GameModeEffectSectionHashes =
    [
        HavenStudio.Utils.String.HashString("ITEM"),
        HavenStudio.Utils.String.HashString("START"),
        HavenStudio.Utils.String.HashString("RULE"),
        HavenStudio.Utils.String.HashString("SYSTEM")
    ];
    private readonly Dictionary<PlacedModelReference, PlacementEntity> _placements =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<PlacedModelReference, List<Model3D>> _placementCollisionModels =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Model3D, (CollisionBlockViewModel Block, int[] Prims, int[] Polys)>
        _placementCollisionLookup = new();
    private readonly Dictionary<Model3D, (CollisionBlockViewModel Block, uint[] Indices, int[] Prims, int[] Polys)>
        _placementCollisionSources = new();
    private OctocamoSurfaceCatalog? _octocamoCatalog;
    private IWorkspaceCatalog? _octocamoWorkspace;
    private WorkspacePath? _octocamoTablePath;
    private bool _octocamoViewEnabled;
    private bool _octocamoMusclePatternView;
    private string? _octocamoPatternSlotPath;
    private (bool Models, bool Placements, bool Vegetation, bool Collision,
        bool PlacementCollision, bool Effects, bool Cameras, bool Sdm, bool Lights,
        bool Grid)? _preOctocamoVisibility;
    private readonly List<VegetationDocumentSession> _vegetationDocuments = [];
    private readonly List<VegetationGroupEntity> _vegetationEntities = [];
    private readonly Dictionary<Model3D, MapEntity> _vegetationByModel = [];
    private readonly List<LitDocumentSession> _lightDocuments = [];
    private readonly List<LightEntity> _lightEntities = [];
    private readonly Dictionary<Model3D, LightEntity> _lightByModel = [];
    private readonly EditHistory _history = new();
    private readonly MapManipulationController _manipulationController;
    private readonly HashSet<CollisionEffectViewModel> _selectedEffects =
        new(ReferenceEqualityComparer.Instance);
    private MapEntity? _selectedEntity;
    private object? _selectedOutlineItem;
    private bool _syncingSelection;
    private bool _inspectorExpanded;
    private Vector3? _spawnPosition;
    private string _addObjectStatus = string.Empty;
    private string _manipulationStatus = string.Empty;
    private string _mapSaveStatus = string.Empty;
    private bool _gameLightingEnabled;
    private CancellationTokenSource? _lightingBakeCancellation;
    private Task _lightingUpdateTask = Task.CompletedTask;
    private int _lightingBakeVersion;
    private Point? _selectionBoxStart;
    private bool _selectionBoxVisible;
    private double _selectionBoxLeft;
    private double _selectionBoxTop;
    private double _selectionBoxWidth;
    private double _selectionBoxHeight;
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
        Outline = [_placementsGroup, _collisionGroup, _effectsGroup, _lightsGroup, _vegetationGroup, _camerasGroup, _sdmAreasGroup];
        _history.Changed += OnHistoryChanged;
        _sceneHost.LayerChanged += OnLayerChanged;
        _collisionEditor.SelectionChanged += OnCollisionSelectionChanged;
        _collisionEditor.PropertyChanged += OnCollisionEditorPropertyChanged;
        _gcxEditor.PropertyChanged += OnGcxEditorPropertyChanged;
        RefreshOutline();
    }

    public ObservableCollection<MapOutlineGroup> Outline { get; }
    public MapEntity? SelectedEntity => _selectedEntity;
    public bool HasSelection => _selectedEntity != null;
    public bool SelectionBoxVisible => _selectionBoxVisible;
    public double SelectionBoxLeft => _selectionBoxLeft;
    public double SelectionBoxTop => _selectionBoxTop;
    public double SelectionBoxWidth => _selectionBoxWidth;
    public double SelectionBoxHeight => _selectionBoxHeight;
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
    public string MapSaveStatus => _mapSaveStatus;
    public bool HasMapSaveStatus => !string.IsNullOrWhiteSpace(_mapSaveStatus);
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;
    public bool HasOctocamoTable => _octocamoCatalog != null;
    public bool HasOctocamoPatternPreviews => _octocamoCatalog?.PatternAtlas != null;
    public bool CanRemapOctocamo => _octocamoCatalog?.HasPatternLibrary == true && _octocamoTablePath != null;
    public string? LastOctocamoSaveReport { get; private set; }

    public OctocamoRemapViewModel CreateOctocamoRemapper()
    {
        var catalog = _octocamoCatalog ?? throw new InvalidOperationException("Open a stage first.");
        if (!CanRemapOctocamo) throw new InvalidOperationException("Load camo previews from the online SLOT first.");
        var material = (_selectedEntity as PrimEntity)?.Octocamo?.SelectedMaterial?.MaterialHash;
        return new OctocamoRemapViewModel(catalog, material, () =>
        {
            if (!ReferenceEquals(catalog, _octocamoCatalog)) throw new InvalidOperationException("The stage changed; reopen the remapping editor.");
            OctocamoMusclePatternView = true;
            if (!_octocamoViewEnabled) OctocamoViewEnabled = true;
            if (_octocamoViewEnabled) _collisionEditor.SetOctocamoCatalog(catalog);
            RefreshPlacementOctocamoColours();
            RefreshOctocamoBoxLabels();
            OnCollisionSelectionChanged();
            SetMapSaveStatus(catalog.IsMappingDirty
                ? "Stage-wide OctoCamo remap pending. Save Map writes the OCTT in cache.dar; physical materials and regular textures are unchanged."
                : "Unsaved OctoCamo remaps reverted.");
        });
    }
    public bool OctocamoMusclePatternView
    {
        get => _octocamoMusclePatternView;
        set
        {
            if (_octocamoMusclePatternView == value || value && !HasOctocamoPatternPreviews) return;
            _octocamoMusclePatternView = value;
            if (_octocamoCatalog != null) _octocamoCatalog.PreviewMusclePatterns = value;
            if (_octocamoViewEnabled) _collisionEditor.SetOctocamoCatalog(_octocamoCatalog);
            RefreshPlacementOctocamoColours();
            OnPropertyChanged();
        }
    }

    public async Task LoadOctocamoPatternSlotAsync(string path)
    {
        var catalog = _octocamoCatalog ?? throw new InvalidOperationException("Open a stage with an OCTT first.");
        var patterns = await Task.Run(() => OctocamoPatternLibrary.LoadAll(path));
        if (!ReferenceEquals(catalog, _octocamoCatalog)) return;
        if (patterns.Count == 0) throw new InvalidDataException("The SLOT contains no diffuse previews matching this stage's patterns.");
        catalog.SetPatternPreviews(patterns);
        _octocamoPatternSlotPath = path;
        OnPropertyChanged(nameof(HasOctocamoPatternPreviews));
        OnPropertyChanged(nameof(CanRemapOctocamo));
        OctocamoMusclePatternView = true;
        if (!_octocamoViewEnabled) OctocamoViewEnabled = true;
        else _collisionEditor.SetOctocamoCatalog(catalog);
        RefreshPlacementOctocamoColours();
        OnCollisionSelectionChanged();
        SetMapSaveStatus($"Loaded {patterns.Count} real muscle-pattern previews. Muscle texture view shows the raw diffuse projected onto contact polygons; cloth colour and in-game lighting remain separate.");
    }
    public string OctocamoStatus => _octocamoCatalog == null
        ? "No stage OctoCamo table (.octt) found."
        : "Player-contact polygons only. Enable Muscle texture for decoded diffuse patterns, or disable it for cloth RGB. Magenta = missing mapping or preview." +
          (_octocamoCatalog.AmbiguousMaterialHashes.Count == 0 ? string.Empty :
              $" {_octocamoCatalog.AmbiguousMaterialHashes.Count} material(s) have conflicting duplicate OCTT rows and are shown unmapped; those rows are preserved on save.");
    public bool OctocamoViewEnabled
    {
        get => _octocamoViewEnabled;
        set
        {
            if (_octocamoViewEnabled == value || value && _octocamoCatalog == null) return;
            _octocamoViewEnabled = value;
            if (value)
            {
                _preOctocamoVisibility = (VisualModelsVisible, PlacementsVisible, VegetationVisible,
                    CollisionVisible, PlacementCollisionVisible, EffectsVisible, CamerasVisible,
                    SdmAreaVisible, LightsVisible, GridVisible);
                VisualModelsVisible = false;
                PlacementsVisible = false;
                VegetationVisible = false;
                CollisionVisible = true;
                PlacementCollisionVisible = true;
                EffectsVisible = false;
                CamerasVisible = false;
                SdmAreaVisible = false;
                LightsVisible = false;
                GridVisible = false;
                _collisionEditor.SetOctocamoCatalog(_octocamoCatalog);
            }
            else
            {
                BoxSelectOctocamoFaces = false;
                ClearOctocamoFaceBox();
                ResetSelectionBox();
                _collisionEditor.SetOctocamoCatalog(null);
                if (_preOctocamoVisibility is { } old)
                {
                    VisualModelsVisible = old.Models;
                    PlacementsVisible = old.Placements;
                    VegetationVisible = old.Vegetation;
                    CollisionVisible = old.Collision;
                    PlacementCollisionVisible = old.PlacementCollision;
                    EffectsVisible = old.Effects;
                    CamerasVisible = old.Cameras;
                    SdmAreaVisible = old.Sdm;
                    LightsVisible = old.Lights;
                    GridVisible = old.Grid;
                }
                _preOctocamoVisibility = null;
            }
            RefreshPlacementOctocamoColours();
            OnPropertyChanged();
            OnCollisionSelectionChanged();
        }
    }
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
    public Task LightingUpdateTask => _lightingUpdateTask;

    public async Task DiscoverOctocamoAsync(IWorkspaceCatalog workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var geometry = _collisionEditor.GeomFile;
        var directory = string.IsNullOrWhiteSpace(_collisionEditor.GeomPath)
            ? null
            : Path.GetDirectoryName(WorkspacePath.ParseLegacy(_collisionEditor.GeomPath).PhysicalPath);
        var candidate = workspace.Snapshot?.WithExtension(".octt")
            .Where(file => string.Equals(Path.GetDirectoryName(file.Path.PhysicalPath),
                directory, StringComparison.OrdinalIgnoreCase) &&
                (!file.Path.IsArchiveEntry || Path.GetFileName(file.Path.PhysicalPath)
                    .Equals("cache.dar", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(file => file.Path.IsArchiveEntry)
            .FirstOrDefault();
        OctocamoSurfaceCatalog? catalog = null;
        if (geometry != null && candidate != null)
        {
            try
            {
                var data = await Task.Run(() => workspace.ReadAllBytes(candidate.Path), cancellationToken);
                var registry = workspace.Snapshot?.WithExtension(".octl")
                    .FirstOrDefault(file => file.Path.IsArchiveEntry == candidate.Path.IsArchiveEntry &&
                        (file.Path.IsArchiveEntry
                            ? file.Path.PhysicalPath.Equals(candidate.Path.PhysicalPath,
                                StringComparison.OrdinalIgnoreCase)
                            : string.Equals(Path.GetDirectoryName(file.Path.PhysicalPath), directory,
                                StringComparison.OrdinalIgnoreCase)));
                var registryData = registry == null ? null : await Task.Run(
                    () => workspace.ReadAllBytes(registry.Path), cancellationToken);
                catalog = new OctocamoSurfaceCatalog(geometry, data, registryData);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Log.Warning(exception, "Could not load OctoCamo table {Path}", candidate.Path);
            }
        }
        if (catalog != null && _octocamoPatternSlotPath != null)
        {
            try
            {
                var previews = await Task.Run(() => OctocamoPatternLibrary.LoadAll(_octocamoPatternSlotPath), cancellationToken);
                catalog.SetPatternPreviews(previews);
                catalog.PreviewMusclePatterns = _octocamoMusclePatternView;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            { Log.Warning(exception, "Could not reload OctoCamo pattern previews"); }
        }
        var previousCatalog = _octocamoCatalog;
        _octocamoCatalog = catalog;
        _octocamoWorkspace = catalog == null ? null : workspace;
        _octocamoTablePath = catalog == null ? null : candidate?.Path;
        if (catalog?.PatternAtlas == null)
        {
            _octocamoMusclePatternView = false;
            OnPropertyChanged(nameof(OctocamoMusclePatternView));
        }
        if (_octocamoViewEnabled)
        {
            if (catalog == null) OctocamoViewEnabled = false;
            else _collisionEditor.SetOctocamoCatalog(catalog);
        }
        RefreshPlacementOctocamoColours();
        OnPropertyChanged(nameof(HasOctocamoTable));
        OnPropertyChanged(nameof(HasOctocamoPatternPreviews));
        OnPropertyChanged(nameof(CanRemapOctocamo));
        OnPropertyChanged(nameof(OctocamoStatus));
        OnCollisionSelectionChanged();
        previousCatalog?.Dispose();
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
        : $"{_lightDocuments.Count} light file(s), {_lightEntities.Count(entity => !entity.IsGlobal)} light record(s)";
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

        ReplaceLightDocuments(loaded);
        SetManipulationStatus(errors.Count == 0
            ? string.Empty
            : $"Some light files could not be loaded: {string.Join("; ", errors)}");
    }

    public bool PlacementCollisionVisible
    {
        get => _sceneHost.IsLayerVisible(SceneLayer.PlacementCollision);
        set => SetLayerVisible(SceneLayer.PlacementCollision, value);
    }

    public async Task DiscoverVegetationAsync(
        IWorkspaceCatalog workspace,
        IReadOnlyList<GcxVegetationReference> references,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(references);
        var snapshot = workspace.Snapshot;
        Log.Debug(
            "Discovering vegetation from {VegetationReferenceCount} GCX reference(s); workspace snapshot available={HasSnapshot}",
            references.Count,
            snapshot != null);
        if (snapshot == null || references.Count == 0)
        {
            ReplaceVegetationDocuments([]);
            Log.Warning(
                "Vegetation discovery skipped: snapshot available={HasSnapshot}, references={VegetationReferenceCount}",
                snapshot != null,
                references.Count);
            return;
        }

        var modelPaths = ProjectModelLoader.BuildPathLookup(snapshot);
        var pdlPaths = snapshot.WithExtension(".pdl")
            .Select(file => (Hash: HavenStudio.Utils.String.HashString(Path.GetFileNameWithoutExtension(file.Name)), file.Path))
            .GroupBy(item => item.Hash)
            .ToDictionary(group => group.Key, group => group.First().Path);
        var errors = new List<string>();
        var prepared = await Task.Run(() =>
        {
            var result = new List<PreparedVegetation>();
            foreach (var reference in references.Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!pdlPaths.TryGetValue(reference.PdlHash, out var pdlPath))
                {
                    errors.Add($"PDL 0x{reference.PdlHash:X6} was not found");
                    continue;
                }
                if (!modelPaths.TryGetValue(reference.ModelHash, out var modelPath))
                {
                    errors.Add($"MDN 0x{reference.ModelHash:X6} for {pdlPath.FileName} was not found");
                    continue;
                }

                try
                {
                    var session = VegetationDocumentSession.Load(workspace, pdlPath, reference.ModelHash);
                    using var modelStream = workspace.OpenRead(modelPath);
                    var mdn = MdnFile.Read(modelStream);
                    var textures = MdnSceneRenderer.ResolveTextures(mdn, workspace);
                    var groups = new List<IReadOnlyList<VegetationPreviewInstance>>(session.Document.Groups.Count);
                    foreach (var group in session.Document.Groups)
                    {
                        var previews = new List<VegetationPreviewInstance>(group.Instances.Count);
                        for (var index = 0; index < group.Instances.Count; index++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var instance = group.Instances[index];
                            var previewScale = CalculateVegetationPreviewScale(
                                reference, group.Index, index);
                            var models = MdnSceneRenderer.BuildModels(
                                mdn,
                                $"_pdl_g{group.Index + 1}_{index + 1}",
                                instance.Position);
                            foreach (var model in models)
                                model.Scale = Vector3.One * previewScale;
                            previews.Add(new VegetationPreviewInstance(instance, previewScale, models));
                        }
                        groups.Add(previews);
                    }
                    result.Add(new PreparedVegetation(
                        session,
                        mdn,
                        Path.GetFileNameWithoutExtension(modelPath.FileName),
                        textures,
                        groups));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    errors.Add($"{pdlPath.FileName}: {exception.Message}");
                    Log.Error(
                        exception,
                        "Failed to prepare vegetation PDL {PdlPath} with model 0x{ModelHash:X6}",
                        pdlPath.ToString(),
                        reference.ModelHash);
                }
            }
            return result;
        }, cancellationToken);

        _vegetationDocuments.Clear();
        _vegetationEntities.Clear();
        _vegetationByModel.Clear();
        var allModels = new List<Model3D>();
        foreach (var item in prepared)
        {
            _vegetationDocuments.Add(item.Session);
            for (var groupIndex = 0; groupIndex < item.Session.Document.Groups.Count; groupIndex++)
            {
                var entity = new VegetationGroupEntity(
                    item.Session,
                    item.Session.Document.Groups[groupIndex],
                    item.ModelName,
                    item.Groups[groupIndex],
                    UpdateVegetationPositionFromInspector,
                    UpdateVegetationInstanceFromInspector);
                _vegetationEntities.Add(entity);
                foreach (var child in entity.Children)
                {
                    foreach (var model in child.Models)
                    {
                        allModels.Add(model);
                        _vegetationByModel[model] = child;
                    }
                }
            }
        }
        _sceneHost.ReplaceLayer(SceneLayer.Vegetation, allModels);
        foreach (var item in prepared)
        {
            MdnSceneRenderer.ApplyTextures(
                _sceneHost.ViewportControl,
                item.Document,
                item.Groups.SelectMany(group => group).SelectMany(preview => preview.Models),
                item.Textures);
        }
        ReplaceChildren(_vegetationGroup, _vegetationEntities.Cast<object>());
        OnPropertyChanged(nameof(VegetationSummary));
        OnPropertyChanged(nameof(VegetationVisible));
        SetManipulationStatus(errors.Count == 0
            ? string.Empty
            : $"Some vegetation layers could not be loaded: {string.Join("; ", errors)}");
        Log.Information(
            "Loaded {VegetationDocumentCount} vegetation document(s), {VegetationInstanceCount} instance(s), {VegetationErrorCount} error(s)",
            _vegetationDocuments.Count,
            _vegetationEntities.Sum(entity => entity.InstanceCount),
            errors.Count);
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
        ReplaceLightDocuments(documents);
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

        if (_octocamoViewEnabled && entity is PrimEntity { GeoPrim: not null } prim)
        {
            _focusedOctocamoPolygon = prim.GeoPrim;
            _focusedOctocamoSource = _lastOctocamoPickModel ??
                (prim.Prim.ParentBlock == null ? null : _collisionEditor.GetBlockModel(prim.Prim.ParentBlock));
            _selectedOctocamoFace = OctocamoBoxFaces.FirstOrDefault(face =>
                ReferenceEquals(face.Polygon, prim.GeoPrim) &&
                ReferenceEquals(face.Instance == null
                    ? _collisionEditor.GetBlockModel(face.Primitive.ParentBlock!) ?? face.Hit.Model
                    : face.Hit.Model, _focusedOctocamoSource));
            OnPropertyChanged(nameof(SelectedOctocamoFace));
        }
        SelectEntity(entity);
        if (_octocamoViewEnabled) RefreshOctocamoFocusWireframe();
    }

    public void PointerPressed(Point point, OpenGL3DControl control, bool boxSelectionModifier)
    {
        if (boxSelectionModifier || _octocamoViewEnabled && BoxSelectOctocamoFaces)
        {
            _manipulationController.Cancel();
            _selectionBoxStart = ClampToViewport(point, control);
            UpdateSelectionBox(_selectionBoxStart.Value, _selectionBoxStart.Value, visible: false);
            return;
        }

        var entity = PickEntityAt(point, control);
        if (entity is EffectEntity effect &&
            _selectedEntity is EffectSelectionEntity selection &&
            selection.Effects.Contains(effect.Effect))
        {
            entity = selection;
        }

        var target = CreateManipulationTarget(entity);
        if (target == null)
        {
            ResetSelectionBox();
            _manipulationController.PointerPressed(point, null);
            return;
        }

        ResetSelectionBox();
        _manipulationController.PointerPressed(point, target);
    }

    public void PointerMoved(Point point, OpenGL3DControl control, bool heightOnly)
    {
        if (_selectionBoxStart is { } selectionStart)
        {
            var current = ClampToViewport(point, control);
            var deltaX = current.X - selectionStart.X;
            var deltaY = current.Y - selectionStart.Y;
            var renderScaling = TopLevel.GetTopLevel(control)?.RenderScaling ?? 1.0;
            var visible = _selectionBoxVisible ||
                (deltaX * deltaX + deltaY * deltaY) * renderScaling * renderScaling >=
                MapManipulationController.DragThreshold * MapManipulationController.DragThreshold;
            UpdateSelectionBox(selectionStart, current, visible);
            return;
        }

        if (_manipulationController.TryUpdate(point, control, heightOnly, out var update))
        {
            ProcessDragUpdate(update);
            if (update.Target.Entity is not LightEntity and not SpectatorCameraEntity)
            {
                ApplyPreviewLighting(update.Target.Models);
            }
        }
    }

    public void PointerReleased(Point point, OpenGL3DControl control, bool heightOnly)
    {
        if (_selectionBoxStart is { } selectionStart)
        {
            var wasSelecting = _selectionBoxVisible;
            var current = ClampToViewport(point, control);
            var selectionRect = NormalizeSelectionRectangle(selectionStart, current);
            ResetSelectionBox();
            if (wasSelecting)
            {
                if (_octocamoViewEnabled) CompleteOctocamoBoxSelection(selectionRect, control);
                else CompleteEffectBoxSelection(selectionRect, control);
            }
            else
            {
                SetSpawnPoint(point, control);
            }
            return;
        }

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

        if (entity is SpectatorCameraEntity camera)
        {
            var finalBytes = camera.Source.Script.Bytes.ToArray();
            _history.UpdateCoalesced(() => ApplyCameraBytes(camera,finalBytes));
        }
        else _history.UpdateCoalesced(() => ApplyHistoryPosition(entity, storedPosition));
        _history.CommitCoalesced();
        SetManipulationStatus(string.Empty);
    }

    public void CancelManipulation()
    {
        ResetSelectionBox();
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

    public string? SnapSelectedToFloor()
    {
        if (CreateManipulationTarget(SelectedEntity) is not { } target ||
            target.Entity is not MapEntity entity)
        {
            return "The selected object does not have an editable position.";
        }

        var before = target.Position;
        var rayOrigin = before + Vector3.UnitY * 0.01f;
        var floorModels = _sceneHost.GetLayerModels(SceneLayer.Collision);
        if (!SelectionRaycaster.TryPickPoint(
                rayOrigin,
                -Vector3.UnitY,
                floorModels,
                out var floorPoint))
        {
            return "No GEOM collision floor was found directly below the selected object.";
        }

        var after = new Vector3(before.X, floorPoint.Y, before.Z);
        if (MathF.Abs(after.Y - before.Y) < 0.0001f)
        {
            return null;
        }
        if (!TryApplyEntityPosition(entity, after, out var error))
        {
            return error ?? "The selected object could not be moved.";
        }

        _history.RecordApplied(
            $"snap {entity.DisplayName} to floor",
            () => ApplyHistoryPosition(entity, before),
            () => ApplyHistoryPosition(entity, after));
        return null;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        LastOctocamoSaveReport = null;
        string? mappingSave = null;
        if (_octocamoCatalog is { IsMappingDirty: true } catalog)
        {
            if (_octocamoWorkspace == null || _octocamoTablePath == null)
                throw new InvalidOperationException("The OctoCamo table no longer has a writable source.");
            var backup = await OctocamoMappingSave.SaveAsync(_octocamoWorkspace, _octocamoTablePath, catalog, cancellationToken);
            mappingSave = $"Saved OctoCamo table: {_octocamoTablePath.PhysicalPath}. Backup: {backup}. Re-encrypt this file for the game; existing .enc copies are not refreshed.";
        }
        var savedGeomPath = _collisionEditor.HasGeomLoaded && _collisionEditor.IsDirty
            ? _collisionEditor.GeomPath : null;
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
            await session.SaveAsync(cancellationToken);
        }
        foreach (var session in _vegetationDocuments.Where(session => session.IsDirty))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(session.Save, cancellationToken);
        }
        SetMapSaveStatus(mappingSave != null
            ? mappingSave + (savedGeomPath == null ? string.Empty : $" Also saved GEOM: {savedGeomPath}; re-encrypt it too.")
            : savedGeomPath == null
            ? "Save Map completed."
            : $"Saved plaintext GEOM: {savedGeomPath}. Re-encrypt this saved file before installing it in-game; Save Map does not refresh an existing .enc copy.");
        if (mappingSave != null || _octocamoViewEnabled && savedGeomPath != null)
            LastOctocamoSaveReport = "Saved and verified by exact file read-back.\n\n" +
                (mappingSave == null ? string.Empty : mappingSave + "\n\n") +
                (savedGeomPath == null ? string.Empty : $"Plaintext GEOM: {savedGeomPath}\nBackup: {_collisionEditor.LastSaveBackupPath}\n\n") +
                "Existing .enc files and installed game files were NOT updated. Re-encrypt the saved files using this stage's key, then install the new encrypted output after launcher verification.\n\n" +
                "Polygon dropdowns edit only the selected face (and serialized aliases). Muscle-pattern and cloth-colour previews are separate; the preview switches to the channel you edit.";
    }

    private MapEntity? PickEntityAt(Point point, OpenGL3DControl control)
    {
        _lastOctocamoPickModel = null;
        var pickModels = _octocamoViewEnabled
            ? _sceneHost.GetLayerModels(SceneLayer.Collision).Where(model => model.Visible)
                .Concat(_sceneHost.GetLayerModels(SceneLayer.PlacementCollision).Where(model => model.Visible)).ToList()
            : _sceneHost.GetLayerModels(SceneLayer.VisualModels)
            .Where(model => model.Visible && _sceneHost.TryGetPlacement(model, out _))
            .Concat(_sceneHost.GetLayerModels(SceneLayer.Vegetation).Where(model => model.Visible))
            .Concat(_sceneHost.GetLayerModels(SceneLayer.Collision).Where(model => model.Visible))
            .Concat(_sceneHost.GetLayerModels(SceneLayer.Effects).Where(model => model.Visible))
            .Concat(_sceneHost.GetLayerModels(SceneLayer.Cameras).Where(model => model.Visible))
            .Concat(_sceneHost.GetLayerModels(SceneLayer.SdmArea).Where(model => model.Visible))
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

        if (_cameraByModel.TryGetValue(hit.Model, out var cameraEntity)) return cameraEntity;

        if (_sdmAreaByModel.TryGetValue(hit.Model, out var sdmAreaEntity)) return sdmAreaEntity;

        if (_lightByModel.TryGetValue(hit.Model, out var lightEntity))
        {
            return lightEntity;
        }

        if (_octocamoViewEnabled && _placementCollisionLookup.TryGetValue(hit.Model, out var placed) &&
            hit.TriangleIndex >= 0 && hit.TriangleIndex < placed.Prims.Length)
        {
            var primIndex = placed.Prims[hit.TriangleIndex];
            var polyIndex = placed.Polys[hit.TriangleIndex];
            if (primIndex >= 0 && primIndex < placed.Block.Prims.Count)
            {
                var prim = placed.Block.Prims[primIndex];
                var polygon = polyIndex >= 0 && polyIndex < prim.Children.Count ? prim.Children[polyIndex] : null;
                if (polygon != null) _lastOctocamoPickModel = hit.Model;
                return new PrimEntity(prim, polygon);
            }
        }

        if (_vegetationByModel.TryGetValue(hit.Model, out var vegetationEntity))
        {
            return vegetationEntity;
        }

        if (_collisionEditor.TryResolveHit(hit, out var collisionSelection))
        {
            if (collisionSelection.Effect != null)
            {
                return new EffectEntity(collisionSelection.Effect);
            }
            if (collisionSelection.Prim != null)
            {
                if (_octocamoViewEnabled && collisionSelection.GeoPrim != null &&
                    collisionSelection.Prim.ParentBlock != null)
                    _lastOctocamoPickModel = _collisionEditor.GetBlockModel(collisionSelection.Prim.ParentBlock);
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

    private void CompleteEffectBoxSelection(Rect selectionRect, OpenGL3DControl control)
    {
        if (!EffectsVisible)
        {
            ClearSelection();
            SetManipulationStatus("Enable Effects before box-selecting spawn points.");
            return;
        }

        var effects = TreeTraversal.Flatten(_collisionEditor.Effects, effect => effect.Children)
            .Where(effect => effect.IsVisible && CanMoveEffect(effect))
            .Where(effect =>
                SelectionRaycaster.TryProjectToScreen(
                    new Vector3(effect.X, effect.Y, effect.Z),
                    control,
                    out var screenPosition) &&
                selectionRect.Contains(screenPosition))
            .ToArray();

        if (effects.Length == 0)
        {
            ClearSelection();
            SetManipulationStatus("No movable effects were inside the selection box.");
            return;
        }

        if (effects.Length == 1)
        {
            _collisionEditor.SelectedEffect = effects[0];
            SetManipulationStatus("1 effect selected.");
            return;
        }

        _collisionEditor.ClearSelection();
        SetSelectedEffects(effects);
        SetSelectedEntity(
            new EffectSelectionEntity(
                effects,
                degrees => UpdateEffectSelectionRotationY(effects, degrees)),
            null);
        SetManipulationStatus(
            $"{effects.Length} effects selected. Drag any highlighted effect to move the group.");
    }

    private static bool CanMoveEffect(CollisionEffectViewModel effect) =>
        GeoEffectLayout.GetPositionSlot(effect.Effect.Index) != 0;

    internal static Rect NormalizeSelectionRectangle(Point first, Point second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        return new Rect(
            left,
            top,
            Math.Max(first.X, second.X) - left,
            Math.Max(first.Y, second.Y) - top);
    }

    private static Point ClampToViewport(Point point, OpenGL3DControl control) =>
        new(
            Math.Clamp(point.X, 0, control.Bounds.Width),
            Math.Clamp(point.Y, 0, control.Bounds.Height));

    private void UpdateSelectionBox(Point first, Point second, bool visible)
    {
        var rectangle = NormalizeSelectionRectangle(first, second);
        _selectionBoxLeft = rectangle.X;
        _selectionBoxTop = rectangle.Y;
        _selectionBoxWidth = rectangle.Width;
        _selectionBoxHeight = rectangle.Height;
        _selectionBoxVisible = visible;
        OnPropertyChanged(nameof(SelectionBoxLeft));
        OnPropertyChanged(nameof(SelectionBoxTop));
        OnPropertyChanged(nameof(SelectionBoxWidth));
        OnPropertyChanged(nameof(SelectionBoxHeight));
        OnPropertyChanged(nameof(SelectionBoxVisible));
    }

    private void ResetSelectionBox()
    {
        _selectionBoxStart = null;
        if (!_selectionBoxVisible && _selectionBoxWidth == 0 && _selectionBoxHeight == 0)
        {
            return;
        }

        _selectionBoxVisible = false;
        _selectionBoxLeft = 0;
        _selectionBoxTop = 0;
        _selectionBoxWidth = 0;
        _selectionBoxHeight = 0;
        OnPropertyChanged(nameof(SelectionBoxVisible));
        OnPropertyChanged(nameof(SelectionBoxLeft));
        OnPropertyChanged(nameof(SelectionBoxTop));
        OnPropertyChanged(nameof(SelectionBoxWidth));
        OnPropertyChanged(nameof(SelectionBoxHeight));
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

                PlacedModelReference? duplicate;
                var propertyParent = effectDuplicate?.Change.Parent?.Effect;
                var isExpandedPropertyPlacement = propertyParent != null &&
                    placement.Placement.PropertyPositionHash ==
                        unchecked((uint)propertyParent.Name);
                if (isExpandedPropertyPlacement)
                {
                    // NewTestTree-style property commands expand every child marker under
                    // one parent effect. Adding the child already creates the placement;
                    // duplicating the foreach row as well creates two references to the same
                    // property group and can make the game reject the GCX.
                    await _gcxEditor.RefreshProjectModelsAsync(cancellationToken);
                    var duplicateEffect = effectDuplicate!.Change.Effect.Effect;
                    duplicate = _sceneHost.GetPlacements().FirstOrDefault(candidate =>
                        ReferenceEquals(candidate.SourceEffect, duplicateEffect));
                }
                else
                {
                    duplicate = await _gcxEditor.DuplicatePlacementAsync(
                        placement.Placement,
                        effectDuplicate?.Hash,
                        cancellationToken);
                }
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
            case SdmAreaEntity area:
                FocusOnModels(area.Models);
                break;
            case SpectatorCameraEntity camera:
                FocusOnModels([camera.Marker]);
                break;
            case PlacementEntity placement:
                FocusOnModels(placement.Models);
                break;
            case VegetationGroupEntity vegetation:
                FocusOnModels(vegetation.Models);
                break;
            case VegetationInstanceEntity vegetation:
                FocusOnModels(vegetation.Models);
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
            case EffectSelectionEntity selection:
                FocusOnModels(selection.Effects.SelectMany(GetEffectManipulationModels).Distinct().ToArray());
                break;
            case LightEntity light:
                FocusOnModels(light.Models);
                break;
        }
    }

    public void ClearSelection()
    {
        _collisionEditor.ClearSelection();
        SetSelectedEffects([]);
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
        if (entity is SpectatorCameraEntity camera)
        {
            if (update.Started)
            {
                SelectCamera(camera);
                var original = camera.Source.Script.Bytes.ToArray();
                _history.BeginCoalesced($"move {camera.DisplayName}", () => ApplyCameraBytes(camera, original));
            }
            var cameraPosition = update.Position;
            var cameraTarget = camera.Target + cameraPosition-camera.Position;
            _history.UpdateCoalesced(() => ApplyCameraTransform(camera, cameraPosition, cameraTarget));
            return;
        }
        if (update.Started)
        {
            SelectEntity(entity);
            var startPosition = update.Target.Position;
            _history.BeginCoalesced(
                entity is EffectSelectionEntity selection
                    ? $"move {selection.Count} effects"
                    : $"move {entity.DisplayName}",
                () => ApplyHistoryPosition(entity, startPosition));
        }

        var position = update.Position;
        _history.UpdateCoalesced(() => ApplyHistoryPosition(entity, position));
    }

    private MapManipulationTarget? CreateManipulationTarget(MapEntity? entity)
    {
        switch (entity)
        {
            case SpectatorCameraEntity camera:
                return new MapManipulationTarget(camera, camera.Position, [camera.Marker]);
            case PlacementEntity placement when
                placement.CanEditPosition && placement.Placement.Position is { } position:
            {
                var models = placement.Models.ToList();
                if (_placementCollisionModels.TryGetValue(placement.Placement, out var collisionModels))
                {
                    models.AddRange(collisionModels);
                }
                if (UsesEffectPosition(placement.Placement) &&
                    FindEffectViewModel(placement.Placement.SourceEffect!) is { } effect &&
                    _collisionEditor.TryGetEffectModel(effect, out var effectModel))
                {
                    models.Add(effectModel);
                }
                return new MapManipulationTarget(placement, position, models);
            }
            case VegetationGroupEntity vegetation:
                return new MapManipulationTarget(vegetation, vegetation.Position, vegetation.Models);
            case VegetationInstanceEntity vegetation:
                return new MapManipulationTarget(vegetation, vegetation.Position, vegetation.Models);
            case EffectEntity effect when CanMoveEffect(effect.Effect):
            {
                var models = GetEffectManipulationModels(effect.Effect).ToArray();
                return models.Length == 0
                    ? null
                    : new MapManipulationTarget(
                        effect,
                        new Vector3(effect.Effect.X, effect.Effect.Y, effect.Effect.Z),
                        models);
            }
            case EffectSelectionEntity selection when selection.Count > 0:
            {
                var models = selection.Effects
                    .SelectMany(GetEffectManipulationModels)
                    .Distinct()
                    .ToArray();
                return models.Length == 0
                    ? null
                    : new MapManipulationTarget(
                        selection,
                        GetEffectSelectionCenter(selection),
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
            case SpectatorCameraEntity camera:
                try { ApplyCameraTransform(camera,position,camera.Target+position-camera.Position); error=null; return true; }
                catch (Exception e) when (e is InvalidDataException or InvalidOperationException or OverflowException)
                { error=e.Message; return false; }
            case PlacementEntity placement:
                error = UpdatePlacementPositionCore(placement.Placement, position);
                placement.RefreshPositionFromSource();
                UpdatePlacementCollisionTransform(placement.Placement);
                return error == null;
            case VegetationGroupEntity vegetation:
                error = UpdateVegetationPositionCore(vegetation, position);
                vegetation.RefreshFromSource();
                return error == null;
            case VegetationInstanceEntity vegetation:
                error = UpdateVegetationInstanceCore(vegetation, position);
                vegetation.RefreshFromSource();
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
            case EffectSelectionEntity selection:
            {
                var delta = position - GetEffectSelectionCenter(selection);
                foreach (var effect in selection.Effects)
                {
                    var effectPosition = new Vector3(effect.X, effect.Y, effect.Z) + delta;
                    if (!_collisionEditor.TrySetEffectPosition(effect.Effect, effectPosition))
                    {
                        error = "One of the selected GEOM effects is no longer loaded.";
                        return false;
                    }
                    SynchronizePlacementsFromEffect(effect.Effect, effectPosition);
                }
                error = null;
                return true;
            }
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
    }

    private static Vector3? GetEntityPosition(MapEntity entity)
    {
        return entity switch
        {
            PlacementEntity placement => placement.Placement.Position,
            VegetationGroupEntity vegetation => vegetation.Position,
            VegetationInstanceEntity vegetation => vegetation.Position,
            EffectEntity effect => new Vector3(effect.Effect.X, effect.Effect.Y, effect.Effect.Z),
            EffectSelectionEntity selection => GetEffectSelectionCenter(selection),
            LightEntity light => light.GetPosition(),
            SpectatorCameraEntity camera => camera.Position,
            _ => null
        };
    }

    private void SelectEntity(MapEntity entity)
    {
        switch (entity)
        {
            case SpectatorCameraEntity camera:
                SelectCamera(camera);
                break;
            case SdmAreaEntity area:
                SelectSdmArea(area);
                break;
            case PlacementEntity placement:
                SelectPlacement(placement);
                break;
            case VegetationGroupEntity vegetation:
                SelectVegetation(vegetation);
                break;
            case VegetationInstanceEntity vegetation:
                SelectVegetation(vegetation);
                break;
            case EffectEntity effect:
                _collisionEditor.SelectedEffect = effect.Effect;
                break;
            case EffectSelectionEntity selection:
                SetSelectedEffects(selection.Effects);
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
            UpdatePlacementCollisionTransform(candidate);
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
            case VegetationGroupEntity vegetation:
                SelectVegetation(vegetation);
                break;
            case VegetationInstanceEntity vegetation:
                SelectVegetation(vegetation);
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
            case SpectatorCameraEntity camera:
                SelectCamera(camera);
                break;
            case SdmAreaEntity area:
                SelectSdmArea(area);
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

    private IEnumerable<Model3D> GetEffectManipulationModels(CollisionEffectViewModel effect)
    {
        if (_collisionEditor.TryGetEffectModel(effect, out var effectModel))
        {
            yield return effectModel;
        }
        foreach (var (placement, placementEntity) in _placements)
        {
            if (!UsesEffectPosition(placement) ||
                !ReferenceEquals(placement.SourceEffect, effect.Effect))
            {
                continue;
            }

            foreach (var model in placementEntity.Models)
            {
                yield return model;
            }
            if (_placementCollisionModels.TryGetValue(placement, out var collisionModels))
            {
                foreach (var model in collisionModels)
                {
                    yield return model;
                }
            }
        }
    }

    private static Vector3 GetEffectSelectionCenter(EffectSelectionEntity selection)
    {
        var total = Vector3.Zero;
        foreach (var effect in selection.Effects)
        {
            total += new Vector3(effect.X, effect.Y, effect.Z);
        }
        return total / selection.Count;
    }

    private void SelectVegetation(VegetationGroupEntity vegetation)
    {
        _collisionEditor.ClearSelection();
        SetSelectedEntity(vegetation, vegetation);
    }

    private void SelectVegetation(VegetationInstanceEntity vegetation)
    {
        _collisionEditor.ClearSelection();
        SetSelectedEntity(vegetation, vegetation);
    }

    private void SelectLight(LightEntity light)
    {
        _collisionEditor.ClearSelection();
        SetSelectedEntity(light, light);
    }

    private void OnCollisionSelectionChanged()
    {
        SynchronizeOctocamoBoxSelection();
        RefreshOctocamoFocusWireframe();
        if (_collisionEditor.SelectedEffect is { } effect)
        {
            SetSelectedEffects([effect]);
            SetSelectedEntity(new EffectEntity(effect), effect);
            return;
        }
        SetSelectedEffects([]);
        if (_collisionEditor.SelectedPrim is { } prim)
        {
            var geoPrim = _collisionEditor.SelectedGeoPrim;
            var octocamo = _octocamoViewEnabled && geoPrim?.Poly != null &&
                prim.ParentBlock is { } primBlock && _octocamoCatalog != null
                ? new OctocamoSelection(geoPrim, primBlock.Block, _octocamoCatalog,
                    OnOctocamoEdited, _collisionEditor.SetPolygonAttributeWithAliases,
                    muscle => OctocamoMusclePatternView = muscle)
                : null;
            SetSelectedEntity(new PrimEntity(prim, geoPrim) { Octocamo = octocamo },
                (object?)geoPrim ?? prim);
            return;
        }
        if (_collisionEditor.SelectedBlock is { } block)
        {
            SetSelectedEntity(new BlockEntity(block), block);
            return;
        }

        if (_selectedEntity is not PlacementEntity and not VegetationGroupEntity and
            not VegetationInstanceEntity and not LightEntity and not SpectatorCameraEntity and
            not SdmAreaEntity)
        {
            SetSelectedEntity(null, null);
        }
    }

    private void SetSelectedEntity(MapEntity? entity, object? outlineItem)
    {
        if (entity is not EffectEntity and not EffectSelectionEntity && _selectedEffects.Count > 0)
        {
            SetSelectedEffects([]);
        }
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
        ReplaceChildren(_vegetationGroup, _vegetationEntities.Cast<object>());
        ReplaceChildren(_collisionGroup, _collisionEditor.Blocks.Cast<object>());
        RefreshEffectOutline();
        RefreshSdmAreas();
        RefreshLightOutline();
    }

    private void RefreshEffectOutline()
    {
        var promoted = TreeTraversal.Flatten(
                _collisionEditor.Effects,
                effect => effect.Children)
            .Where(effect => effect.Parent != null &&
                GameModeEffectSectionHashes.Contains(unchecked((uint)effect.Effect.Name)))
            .OrderBy(effect => Array.IndexOf(
                ["ITEM", "START", "RULE", "SYSTEM"],
                effect.DisplayName))
            .Cast<object>()
            .ToArray();

        ReplaceChildren(_gameModeEffectsGroup, promoted);
        var roots = _collisionEditor.Effects.Cast<object>();
        ReplaceChildren(
            _effectsGroup,
            promoted.Length == 0
                ? roots
                : new object[] { _gameModeEffectsGroup }.Concat(roots));
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
                UpdatePlacementRotationFromInspector,
                _gcxEditor.UpdatePlacementModelHash,
                UpdatePlacementCollisionReferenceFromInspector);
            index++;
        }

        ReplaceChildren(_placementsGroup, _placements.Values.Cast<object>());
        RefreshPlacementCollisionModels();
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
            new GeoReferenceOption(hash, $"{ResolveGeoReferenceLabel(hash)}  (0x{hash:X6})")));
        return options;
    }

    private void RefreshPlacementCollisionModels()
    {
        ClearOctocamoFaceBox();
        _placementCollisionModels.Clear();
        _placementCollisionLookup.Clear();
        _placementCollisionSources.Clear();
        if (_collisionEditor.GeomFile is not { } geometry)
        {
            _sceneHost.ClearLayer(SceneLayer.PlacementCollision);
            return;
        }

        var references = geometry.GeomRefs
            .GroupBy(reference => reference.Hash)
            .ToDictionary(group => group.Key, group => group.First());
        var blockViews = _collisionEditor.Blocks.ToDictionary(view => view.Block);
        var models = new List<Model3D>();
        foreach (var placement in _placements.Keys)
        {
            if (placement.CollisionReferenceHash is not { } hash ||
                !references.TryGetValue(hash, out var reference))
                continue;

            var blockIndex = 0;
            foreach (var block in geometry.GeomRefBlocks[reference])
            {
                if (!geometry.BlockVertexData.TryGetValue(block, out var vertices) ||
                    !geometry.BlockFaceData.TryGetValue(block, out var faces) ||
                    !GeomMeshDecoder.TryDecodeBlock(vertices, faces, out var mesh))
                {
                    blockIndex++;
                    continue;
                }

                var primitiveAttributes = faces.Select(face => face.Attribute).ToArray();
                var model = new Model3D
                {
                    Name = $"PlacementCollision_{hash:X6}_{blockIndex}",
                    Positions = mesh.Positions,
                    Colors = GeomSceneBuilder.BuildCollisionVertexColors(
                        mesh.Positions, mesh.Indices, mesh.PrimitiveIndices, primitiveAttributes),
                    Indices = mesh.Indices,
                    VertexCount = mesh.VertexCount,
                    IndexCount = mesh.Indices.Length,
                    Position = placement.Position ?? Vector3.Zero,
                    Rotation = placement.Rotation ?? Vector3.Zero,
                    Scale = Vector3.One,
                    Color = GeomSceneBuilder.GetCollisionAttributeColor(block.Attribute),
                    Alpha = 0.55f,
                    BlendEnabled = true,
                    WriteDepth = false,
                    ForceOpaqueAlpha = false,
                    DepthBias = -2f,
                    MaterialIndex = -1
                };
                models.Add(model);
                if (blockViews.TryGetValue(block, out var blockView))
                    _placementCollisionSources[model] = (blockView, mesh.Indices, mesh.PrimitiveIndices, mesh.PolygonIndices);
                if (!_placementCollisionModels.TryGetValue(placement, out var placementModels))
                {
                    placementModels = [];
                    _placementCollisionModels[placement] = placementModels;
                }
                placementModels.Add(model);
                blockIndex++;
            }
        }
        _sceneHost.ReplaceLayer(SceneLayer.PlacementCollision, models);
        RefreshPlacementOctocamoColours();
    }

    private void RefreshPlacementOctocamoColours()
    {
        foreach (var (model, source) in _placementCollisionSources)
        {
            var faces = source.Block.Prims.Select(prim => prim.Prim).ToArray();
            var attributes = faces.Select(face => face.Attribute).ToArray();
            var filtered = GeomSceneBuilder.FilterCollisionTriangles(source.Indices, source.Prims,
                source.Polys, attributes, _octocamoViewEnabled ? GeoCollisionAttributes.Player : null);
            model.Indices = filtered.Indices;
            model.IndexCount = filtered.Indices.Length;
            model.IndicesNeedUpdate = true;
            _placementCollisionLookup[model] = (source.Block, filtered.PrimitiveIndices, filtered.PolygonIndices);
            if (_octocamoViewEnabled && _octocamoCatalog?.PreviewMusclePatterns == true && _octocamoCatalog.PatternAtlas != null)
            {
                model.Colors = new float[model.Positions.Length / 3 * 4];
                Array.Fill(model.Colors, 1f);
                model.UVs = GeomSceneBuilder.BuildOctocamoTextureUvs(model.Positions, model.Indices,
                    filtered.PrimitiveIndices, filtered.PolygonIndices, faces, source.Block.Block, _octocamoCatalog);
            }
            else model.Colors = _octocamoViewEnabled && _octocamoCatalog != null
                ? GeomSceneBuilder.BuildOctocamoVertexColors(model.Positions, model.Indices,
                    filtered.PrimitiveIndices, filtered.PolygonIndices, faces, source.Block.Block, _octocamoCatalog)
                : GeomSceneBuilder.BuildCollisionVertexColors(model.Positions, model.Indices,
                    filtered.PrimitiveIndices, attributes);
            model.Alpha = _octocamoViewEnabled ? 1f : 0.55f;
            model.Color = _octocamoViewEnabled
                ? Vector3.One
                : GeomSceneBuilder.GetCollisionAttributeColor(source.Block.Block.Attribute);
            model.BlendEnabled = !_octocamoViewEnabled;
            model.WriteDepth = _octocamoViewEnabled;
            model.ForceOpaqueAlpha = _octocamoViewEnabled;
            model.VerticesNeedUpdate = true;
        }
        var atlas = _octocamoViewEnabled && _octocamoCatalog?.PreviewMusclePatterns == true ? _octocamoCatalog.PatternAtlas : null;
        _sceneHost.ViewportControl.ApplySharedPreviewTexture("octocamo", _placementCollisionSources.Keys.ToArray(),
            atlas?.Width ?? 0, atlas?.Height ?? 0, atlas?.Rgba);
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }

    private void OnOctocamoEdited()
    {
        if (_octocamoViewEnabled) _collisionEditor.SetOctocamoCatalog(_octocamoCatalog);
        RefreshPlacementOctocamoColours();
        RefreshOctocamoBoxLabels();
        SetMapSaveStatus($"OctoCamo polygon edit pending ({(_octocamoMusclePatternView ? "muscle texture" : "cloth RGB")} preview). Save Map updates {_collisionEditor.GeomPath} and its shared aliases, not an existing .enc copy. Only the selected polygon is edited.");
    }

    private void SetMapSaveStatus(string message)
    {
        _mapSaveStatus = message;
        OnPropertyChanged(nameof(MapSaveStatus));
        OnPropertyChanged(nameof(HasMapSaveStatus));
    }

    private string? UpdatePlacementCollisionReferenceFromInspector(
        PlacedModelReference placement,
        uint? collisionReferenceHash)
    {
        var error = _gcxEditor.UpdatePlacementCollisionReference(placement, collisionReferenceHash);
        if (error == null)
        {
            RefreshPlacementCollisionModels();
        }
        return error;
    }

    private void UpdatePlacementCollisionTransform(PlacedModelReference placement)
    {
        if (!_placementCollisionModels.TryGetValue(placement, out var models))
            return;
        var position = placement.Position ?? Vector3.Zero;
        var rotation = placement.Rotation ?? Vector3.Zero;
        foreach (var model in models)
        {
            model.Position = position;
            model.Rotation = rotation;
            model.VerticesNeedUpdate = true;
        }
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }

    private string ResolveGeoReferenceLabel(uint hash)
    {
        var name = ResolveGeoReferenceName(hash);
        if (name != "Unnamed collision mesh" || _collisionEditor.GeomFile is not { } geometry)
            return name;
        var references = geometry.GeomRefs.Where(reference => reference.Hash == hash).ToArray();
        if (references.Length == 0)
            return name;
        var points = new List<float>();
        var triangleCount = 0;
        foreach (var reference in references)
        {
            foreach (var block in geometry.GeomRefBlocks[reference])
            {
                if (geometry.BlockVertexData.TryGetValue(block, out var vertices) &&
                    geometry.BlockFaceData.TryGetValue(block, out var faces) &&
                    GeomMeshDecoder.TryDecodeBlock(vertices, faces, out var mesh))
                {
                    points.AddRange(mesh.Positions);
                    triangleCount += mesh.TriangleCount;
                }
            }
        }
        if (points.Count == 0)
            return name;
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        for (var offset = 0; offset < points.Count; offset += 3)
        {
            var point = new Vector3(points[offset], points[offset + 1], points[offset + 2]);
            min = Vector3.ComponentMin(min, point);
            max = Vector3.ComponentMax(max, point);
        }
        var size = max - min;
        var radialMatch = MathF.Abs(size.X - size.Z) <= MathF.Max(size.X, size.Z) * 0.2f;
        var shape = triangleCount <= 12
            ? "Box"
            : radialMatch && triangleCount is >= 24 and <= 256
                ? "Cylinder"
                : "Mesh";
        return $"{shape} {size.X:0}×{size.Y:0}×{size.Z:0}";
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
        UpdatePlacementCollisionTransform(placement);
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

    private string? UpdateVegetationPositionFromInspector(
        VegetationGroupEntity vegetation,
        Vector3 position)
    {
        var before = vegetation.Position;
        var error = UpdateVegetationPositionCore(vegetation, position);
        if (error != null || before == vegetation.Position)
            return error;
        var after = vegetation.Position;
        _history.RecordApplied(
            "move vegetation group",
            () => ApplyVegetationHistoryPosition(vegetation, before),
            () => ApplyVegetationHistoryPosition(vegetation, after));
        return null;
    }

    private static string? UpdateVegetationPositionCore(
        VegetationGroupEntity vegetation,
        Vector3 position)
    {
        try
        {
            vegetation.Session.MoveGroupTo(vegetation.Group, position);
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            return exception.Message;
        }
    }

    private void SetSelectedEffects(IEnumerable<CollisionEffectViewModel> effects)
    {
        _selectedEffects.Clear();
        foreach (var effect in effects)
        {
            _selectedEffects.Add(effect);
        }
        _collisionEditor.HighlightEffects(_selectedEffects);
        UpdateRaceGoalRelationshipHighlight();
    }

    private void UpdateRaceGoalRelationshipHighlight()
    {
        if (_selectedEffects.Count != 1 ||
            _selectedEffects.Single() is not { } selected ||
            !IsRaceNode(selected))
        {
            _collisionEditor.HighlightRaceEffects([], [], []);
            return;
        }

        var raceGoals = TreeTraversal.Flatten(
                _collisionEditor.Effects,
                effect => effect.Children)
            .Where(IsRaceNode)
            .ToArray();
        var links = _gcxEditor.GetRaceGoalLinks(
            raceGoals.Select(effect => unchecked((uint)effect.Effect.Name)));
        var selectedHash = unchecked((uint)selected.Effect.Name);
        if (!links.TryGetValue(selectedHash, out var blockedHashes))
        {
            _collisionEditor.HighlightRaceEffects([], [], []);
            return;
        }
        // A52C7F's misleadingly named -link list is an exclusion list. Several
        // retail rows even include their own base index, which cannot represent
        // a valid next destination. Goals absent from the row are selectable.
        var available = raceGoals.Where(effect =>
            !ReferenceEquals(effect, selected) &&
            !blockedHashes.Contains(unchecked((uint)effect.Effect.Name))).ToArray();
        var unavailable = raceGoals.Where(effect =>
            !ReferenceEquals(effect, selected) &&
            blockedHashes.Contains(unchecked((uint)effect.Effect.Name)));
        _collisionEditor.HighlightRaceEffects([selected], available, unavailable);
    }

    private static bool IsRaceNode(CollisionEffectViewModel effect) =>
        effect.DisplayName.StartsWith("PRP_RACE_BASE_", StringComparison.OrdinalIgnoreCase) ||
        effect.DisplayName.StartsWith("PRP_RACE_HOME_", StringComparison.OrdinalIgnoreCase);

    private static void ApplyVegetationHistoryPosition(
        VegetationGroupEntity vegetation,
        Vector3 position)
    {
        var error = UpdateVegetationPositionCore(vegetation, position);
        if (error != null)
            throw new InvalidOperationException(error);
        vegetation.RefreshFromSource();
    }

    private string? UpdateVegetationInstanceFromInspector(
        VegetationInstanceEntity vegetation,
        Vector3 position)
    {
        var beforePosition = vegetation.Position;
        var error = UpdateVegetationInstanceCore(vegetation, position);
        if (error != null || beforePosition == vegetation.Position)
            return error;
        var afterPosition = vegetation.Position;
        _history.RecordApplied(
            "move vegetation instance",
            () => ApplyVegetationInstanceHistory(vegetation, beforePosition),
            () => ApplyVegetationInstanceHistory(vegetation, afterPosition));
        return null;
    }

    private static string? UpdateVegetationInstanceCore(
        VegetationInstanceEntity vegetation,
        Vector3 position)
    {
        try
        {
            vegetation.Parent.Session.UpdateInstancePosition(vegetation.Parent.Group, vegetation.Instance, position);
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException)
        {
            return exception.Message;
        }
    }

    private static void ApplyVegetationInstanceHistory(
        VegetationInstanceEntity vegetation,
        Vector3 position)
    {
        var error = UpdateVegetationInstanceCore(vegetation, position);
        if (error != null)
            throw new InvalidOperationException(error);
        vegetation.RefreshFromSource();
    }

    private static float CalculateVegetationPreviewScale(
        GcxVegetationReference reference,
        int groupIndex,
        int instanceIndex)
    {
        var minimum = MathF.Min(reference.MinimumScale, reference.MaximumScale);
        var maximum = MathF.Max(reference.MinimumScale, reference.MaximumScale);
        if (!float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum <= 0f)
            return 1f;
        uint seed = reference.PdlHash ^ ((uint)(groupIndex + 1) * 0x9E3779B9u) ^ (uint)(instanceIndex + 1);
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        var fraction = (seed & 0x00FFFFFFu) / 16777215f;
        return minimum + (maximum - minimum) * fraction;
    }

    public bool VegetationVisible
    {
        get => _sceneHost.IsLayerVisible(SceneLayer.Vegetation);
        set => SetLayerVisible(SceneLayer.Vegetation, value);
    }

    public string VegetationSummary => _vegetationDocuments.Count == 0
        ? "No GCX-linked PDL vegetation loaded"
        : $"{_vegetationDocuments.Count} PDL file(s), {_vegetationEntities.Sum(entity => entity.InstanceCount)} instance(s)";

    private string? UpdatePlacementRotationFromInspector(
        PlacedModelReference placement,
        Vector3 rotation)
    {
        var before = placement.Rotation;
        var error = UpdatePlacementRotationCore(placement, rotation);
        if (error != null || before == null || placement.Rotation == null || before == placement.Rotation)
        {
            return error;
        }

        var after = placement.Rotation.Value;
        UpdatePlacementCollisionTransform(placement);
        _history.RecordApplied(
            "rotate placement",
            () => ApplyPlacementHistoryRotation(placement, before.Value),
            () => ApplyPlacementHistoryRotation(placement, after));
        return null;
    }

    private string? UpdateEffectSelectionRotationY(
        IReadOnlyList<CollisionEffectViewModel> effects,
        float degrees)
    {
        if (!float.IsFinite(degrees))
        {
            return "Rotation Y must be a finite number.";
        }
        if (effects.Count == 0)
        {
            return "No effects are selected.";
        }
        if (effects.Any(effect => !_collisionEditor.CanSetEffectRotation(effect.Effect)))
        {
            return "One of the selected GEOM effects cannot store rotation data.";
        }

        var before = effects
            .Select(effect => new Vector3(effect.RotationX, effect.RotationY, effect.RotationZ))
            .ToArray();
        var rotationY = degrees * MathF.PI / 180f;
        var after = before
            .Select(rotation => new Vector3(rotation.X, rotationY, rotation.Z))
            .ToArray();
        if (before.SequenceEqual(after))
        {
            return null;
        }

        ApplyEffectSelectionRotations(effects, after);
        _history.RecordApplied(
            $"rotate {effects.Count} effects",
            () => ApplyEffectSelectionRotations(effects, before),
            () => ApplyEffectSelectionRotations(effects, after));
        return null;
    }

    private void ApplyEffectSelectionRotations(
        IReadOnlyList<CollisionEffectViewModel> effects,
        IReadOnlyList<Vector3> rotations)
    {
        if (effects.Count != rotations.Count)
        {
            throw new InvalidOperationException("Effect rotation history is inconsistent.");
        }

        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            var rotation = rotations[index];
            if (!_collisionEditor.TrySetEffectRotation(effect.Effect, rotation))
            {
                throw new InvalidOperationException(
                    "One of the selected GEOM effects could not be rotated.");
            }
            SynchronizePlacementsFromEffectRotation(effect.Effect, rotation);
        }

        if (_selectedEntity is EffectSelectionEntity selection &&
            selection.Effects.SequenceEqual(effects))
        {
            selection.RefreshRotationState();
        }
    }

    private string? UpdatePlacementRotationCore(PlacedModelReference placement, Vector3 rotation)
    {
        if (placement.SourceEffect is { } effect)
        {
            if (!_collisionEditor.TrySetEffectRotation(effect, rotation))
            {
                return "The placement's GEOM effect could not be promoted to a rotatable record.";
            }
            SynchronizePlacementsFromEffectRotation(effect, rotation);
            return null;
        }

        return "This placement does not have a GEOM effect transform.";
    }

    private void SynchronizePlacementsFromEffectRotation(GeoEffect effect, Vector3 rotation)
    {
        foreach (var (candidate, entity) in _placements)
        {
            if (!ReferenceEquals(candidate.SourceEffect, effect))
            {
                continue;
            }
            candidate.Rotation = rotation;
            entity.RefreshRotationFromSource();
            UpdatePlacementCollisionTransform(candidate);
        }
    }

    private void ApplyPlacementHistoryRotation(PlacedModelReference placement, Vector3 rotation)
    {
        var error = UpdatePlacementRotationCore(placement, rotation);
        if (error != null)
        {
            throw new InvalidOperationException(error);
        }
        if (_placements.TryGetValue(placement, out var entity))
        {
            entity.RefreshRotationFromSource();
        }
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
        UpdatePlacementCollisionTransform(placement);
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

    private void ReplaceVegetationDocuments(IEnumerable<PreparedVegetation> _)
    {
        _vegetationDocuments.Clear();
        _vegetationEntities.Clear();
        _vegetationByModel.Clear();
        _sceneHost.ClearLayer(SceneLayer.Vegetation);
        ReplaceChildren(_vegetationGroup, []);
        OnPropertyChanged(nameof(VegetationSummary));
        OnPropertyChanged(nameof(VegetationVisible));
    }

    private void ReplaceLightDocuments(IEnumerable<LitDocumentSession> documents)
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

        PrimaryLightDocument = _lightDocuments.FirstOrDefault(session => !session.IsSkyPass)
            ?? _lightDocuments.FirstOrDefault();
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
        var stem = NormalizeStageStem(Path.GetFileNameWithoutExtension(fileName));
        var matchesStage = normalizedStage.Length > 0 &&
            (stem.StartsWith(normalizedStage, StringComparison.OrdinalIgnoreCase) ||
             normalizedStage.StartsWith(stem, StringComparison.OrdinalIgnoreCase));
        var sky = fileName.Contains("sky", StringComparison.OrdinalIgnoreCase);
        return (matchesStage ? 0 : 2) + (sky ? 1 : 0);
    }

    private static string NormalizeStageStem(string? stem)
    {
        var normalized = (stem ?? string.Empty).Trim().ToLowerInvariant();
        string[] suffixes = ["_sky_d", "_sky", "_d"];
        foreach (var suffix in suffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                return normalized[..^suffix.Length];
            }
        }
        return normalized;
    }

    private sealed record PreparedVegetation(
        VegetationDocumentSession Session,
        Mdn Document,
        string ModelName,
        IReadOnlyDictionary<uint, ResolvedTexture> Textures,
        IReadOnlyList<IReadOnlyList<VegetationPreviewInstance>> Groups);

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
            RefreshEffectOutline();
            RefreshSdmAreas();
            RefreshPlacements();
        }
        else if (eventArgs.PropertyName == nameof(CollisionEditorViewModel.GeomFile))
        {
            CancelManipulation();
            _history.Clear();
            SetMapSaveStatus(string.Empty);
            if (_octocamoViewEnabled) OctocamoViewEnabled = false;
            // A newly opened GEOM starts with the SDM boundary overlay hidden,
            // even if it was enabled while inspecting the previous stage.
            SdmAreaVisible = false;
            _octocamoCatalog?.Dispose();
            _octocamoCatalog = null;
            _octocamoWorkspace = null;
            _octocamoTablePath = null;
            OnPropertyChanged(nameof(HasOctocamoTable));
            OnPropertyChanged(nameof(HasOctocamoPatternPreviews));
            OnPropertyChanged(nameof(CanRemapOctocamo));
            OnPropertyChanged(nameof(OctocamoStatus));
        }
    }

    private void OnGcxEditorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
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
        RefreshCameras();
        RefreshSdmAreas();
        ApplyGameLighting();
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
            SceneLayer.Vegetation => nameof(VegetationVisible),
            SceneLayer.Collision => nameof(CollisionVisible),
            SceneLayer.PlacementCollision => nameof(PlacementCollisionVisible),
            SceneLayer.Effects => nameof(EffectsVisible),
            SceneLayer.Cameras => nameof(CamerasVisible),
            SceneLayer.SdmArea => nameof(SdmAreaVisible),
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
        var samples = new Dictionary<PlacedModelReference, SampledLighting>(ReferenceEqualityComparer.Instance);
        var stageModels = new List<(Model3D Model, LightVertexBaker.SpatialBakeInput Input)>();
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
                    lighting = LightSampler.Sample(
                        primary.Document,
                        placement.Position ?? model.Position,
                        sceneLighting);
                    samples[placement] = lighting;
                }
                LightVertexBaker.Apply(model, lighting);
                continue;
            }

            if (LightVertexBaker.CaptureSpatialBake(model) is { } input)
            {
                stageModels.Add((model, input));
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

    private async Task BakeStageLightingAsync(
        IReadOnlyList<(Model3D Model, LightVertexBaker.SpatialBakeInput Input)> stageModels,
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
                var samples = new Dictionary<Vector3, SampledLighting>();
                var baked = new List<(Model3D Model, float[] Colors)>(stageModels.Count);
                foreach (var (model, input) in stageModels)
                {
                    token.ThrowIfCancellationRequested();
                    var colors = LightVertexBaker.BakeSpatialColors(
                        input.Positions,
                        input.Normals,
                        input.BaseColors,
                        input.VertexCount,
                        input.ModelMatrix,
                        position =>
                        {
                            if (!samples.TryGetValue(position, out var sample))
                            {
                                sample = LightSampler.Sample(lighting, position, sceneLighting);
                                samples[position] = sample;
                            }
                            return sample;
                        },
                        modulateBaseColor: true,
                        token);
                    baked.Add((model, colors));
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

            foreach (var (model, colors) in results)
            {
                LightVertexBaker.ApplyBakedColors(model, colors);
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
                    _gcxEditor.SystemLighting));
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName != null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
