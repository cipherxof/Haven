using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Services.Workspace;
using Serilog;

namespace HavenStudio.Editors;

public sealed partial class MapEditorViewModel
{
    private static readonly ILogger OctocamoLog = Serilog.Log.ForContext<MapEditorViewModel>();
    private OctocamoSurfaceCatalog? _octocamoCatalog;
    private IWorkspaceCatalog? _octocamoWorkspace;
    private WorkspacePath? _octocamoTablePath;
    private string? _octocamoPatternSlotPath;
    private bool _octocamoViewEnabled;
    private bool _octocamoMusclePatternView;
    private (bool Models, bool Placements, bool Collision, bool Effects, bool Lights, bool Grid)?
        _preOctocamoVisibility;

    public bool HasOctocamoTable => _octocamoCatalog != null;
    public bool HasOctocamoPatternPreviews => _octocamoCatalog?.PatternAtlas != null;
    public bool CanRemapOctocamo => _octocamoCatalog?.HasPatternLibrary == true && _octocamoTablePath != null;
    public string OctocamoStatus => _octocamoCatalog == null
        ? "No stage OctoCamo table (.octt) found."
        : "Player-contact polygons only. Muscle texture shows the decoded pattern; otherwise the preview shows cloth colour. Magenta means missing mapping or preview." +
          (_octocamoCatalog.AmbiguousMaterialHashes.Count == 0 ? string.Empty :
              $" {_octocamoCatalog.AmbiguousMaterialHashes.Count} material(s) have conflicting duplicate OCTT rows; those materials are shown as unmapped and their rows are preserved.");

    public bool OctocamoViewEnabled
    {
        get => _octocamoViewEnabled;
        set
        {
            if (_octocamoViewEnabled == value || value && _octocamoCatalog == null) return;
            _octocamoViewEnabled = value;
            if (value)
            {
                _preOctocamoVisibility = (VisualModelsVisible, PlacementsVisible, CollisionVisible,
                    EffectsVisible, LightsVisible, GridVisible);
                VisualModelsVisible = false;
                PlacementsVisible = false;
                CollisionVisible = true;
                EffectsVisible = false;
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
                    CollisionVisible = old.Collision;
                    EffectsVisible = old.Effects;
                    LightsVisible = old.Lights;
                    GridVisible = old.Grid;
                }
                _preOctocamoVisibility = null;
            }
            OnPropertyChanged();
            OnCollisionSelectionChanged();
            RefreshOctocamoFocusWireframe();
        }
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
            RefreshOctocamoBoxLabels();
            OnPropertyChanged();
        }
    }

    public OctocamoRemapViewModel CreateOctocamoRemapper()
    {
        var catalog = _octocamoCatalog ?? throw new InvalidOperationException("Open a stage first.");
        if (!CanRemapOctocamo) throw new InvalidOperationException("Load camo previews from the online SLOT first.");
        var material = (_selectedEntity as PrimEntity)?.Octocamo?.SelectedMaterial?.MaterialHash;
        return new OctocamoRemapViewModel(catalog, material, () =>
        {
            if (!ReferenceEquals(catalog, _octocamoCatalog))
                throw new InvalidOperationException("The stage changed; reopen the remapping editor.");
            OctocamoMusclePatternView = true;
            if (!_octocamoViewEnabled) OctocamoViewEnabled = true;
            else _collisionEditor.SetOctocamoCatalog(catalog);
            RefreshOctocamoBoxLabels();
            OnCollisionSelectionChanged();
            SetManipulationStatus(catalog.IsMappingDirty
                ? "Stage-wide OctoCamo remap pending. Save Map writes the OCTT; regular textures are unchanged."
                : "Unsaved OctoCamo remaps reverted.");
        });
    }

    public async Task LoadOctocamoPatternSlotAsync(string path)
    {
        var catalog = _octocamoCatalog ?? throw new InvalidOperationException("Open a stage with an OCTT first.");
        var patterns = await Task.Run(() => OctocamoPatternLibrary.LoadAll(path));
        if (!ReferenceEquals(catalog, _octocamoCatalog)) return;
        if (patterns.Count == 0)
            throw new InvalidDataException("The SLOT contains no diffuse previews matching this stage's patterns.");
        catalog.SetPatternPreviews(patterns);
        _octocamoPatternSlotPath = path;
        OnPropertyChanged(nameof(HasOctocamoPatternPreviews));
        OnPropertyChanged(nameof(CanRemapOctocamo));
        OctocamoMusclePatternView = true;
        if (!_octocamoViewEnabled) OctocamoViewEnabled = true;
        else _collisionEditor.SetOctocamoCatalog(catalog);
        RefreshOctocamoBoxLabels();
        OnCollisionSelectionChanged();
    }

    public async Task DiscoverOctocamoAsync(IWorkspaceCatalog workspace,
        CancellationToken cancellationToken = default)
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
                OctocamoLog.Warning(exception, "Could not load OctoCamo table {Path}", candidate.Path);
            }
        }
        if (catalog != null && _octocamoPatternSlotPath != null)
        {
            try
            {
                var previews = await Task.Run(
                    () => OctocamoPatternLibrary.LoadAll(_octocamoPatternSlotPath), cancellationToken);
                catalog.SetPatternPreviews(previews);
                catalog.PreviewMusclePatterns = _octocamoMusclePatternView;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                OctocamoLog.Warning(exception, "Could not reload OctoCamo pattern previews");
            }
        }
        var previous = _octocamoCatalog;
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
        OnPropertyChanged(nameof(HasOctocamoTable));
        OnPropertyChanged(nameof(HasOctocamoPatternPreviews));
        OnPropertyChanged(nameof(CanRemapOctocamo));
        OnPropertyChanged(nameof(OctocamoStatus));
        OnCollisionSelectionChanged();
        ClearOctocamoFaceBox();
        previous?.Dispose();
    }

    private async Task SaveOctocamoMappingAsync(CancellationToken cancellationToken)
    {
        if (_octocamoCatalog is not { IsMappingDirty: true } catalog) return;
        if (_octocamoWorkspace == null || _octocamoTablePath == null)
            throw new InvalidOperationException("The OctoCamo table no longer has a writable source.");
        await OctocamoMappingSave.SaveAsync(_octocamoWorkspace, _octocamoTablePath,
            catalog, cancellationToken);
    }

    private void OnOctocamoEdited()
    {
        if (_octocamoViewEnabled) _collisionEditor.SetOctocamoCatalog(_octocamoCatalog);
        RefreshOctocamoBoxLabels();
        SetManipulationStatus("OctoCamo polygon edit pending. Save Map writes the edited GEOM face and shared aliases.");
    }
}
