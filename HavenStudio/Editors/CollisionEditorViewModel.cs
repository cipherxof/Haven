using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia3DControl;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;
using HavenStudio.Services.Workspace;
using HavenStudio.Utils;
using OpenTK.Mathematics;
using Serilog;

namespace HavenStudio.Editors;

public sealed record CollisionFlagFilterOption(ulong? Flag, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record CollisionEffectStructureChange(
    CollisionEffectViewModel Effect,
    CollisionEffectViewModel? Parent,
    int Index,
    CollisionEffectViewModel? PreviousSelection);

public sealed record CollisionEffectDuplicate(
    CollisionEffectStructureChange Change,
    uint Hash);

public sealed class CollisionEditorViewModel : INotifyPropertyChanged
{
    private static readonly ILogger Log = Serilog.Log.ForContext<CollisionEditorViewModel>();

    private readonly SceneHost _sceneHost;
    private readonly GeomDocumentSession _documentSession;
    private readonly CollisionSceneController _sceneController;
    private GeoEffectChunkLayout? _effectLayout;
    private CollisionBlockViewModel? _selectedBlock;
    private CollisionPrimViewModel? _selectedPrim;
    private CollisionGeoPrimViewModel? _selectedGeoPrim;
    private CollisionEffectViewModel? _selectedEffect;
    private object? _selectedTreeItem;
    private string _blockFilterText = string.Empty;
    private string _effectFilterText = string.Empty;
    private bool _showAllEffects;
    private bool _wireframeMode;
    private CollisionFlagFilterOption _selectedCollisionFlagFilter;

    public CollisionEditorViewModel(SceneHost sceneHost)
    {
        _sceneHost = sceneHost ?? throw new ArgumentNullException(nameof(sceneHost));
        _documentSession = new GeomDocumentSession();
        _sceneController = new CollisionSceneController(sceneHost);
        var flagFilters = new List<CollisionFlagFilterOption>
        {
            new(null, "All collision flags"),
            new(0, "No flags (0x0)")
        };
        flagFilters.AddRange(GeoCollisionAttributes.Definitions.Select(definition =>
            new CollisionFlagFilterOption(
                definition.Flag,
                $"{definition.DisplayName} (0x{definition.Flag:X})")));
        CollisionFlagFilters = flagFilters;
        _selectedCollisionFlagFilter = flagFilters[0];
    }

    public ObservableCollection<CollisionBlockViewModel> Blocks { get; } = [];
    public ObservableCollection<CollisionBlockViewModel> FilteredBlocks { get; } = [];
    public ObservableCollection<CollisionPrimViewModel> Prims { get; } = [];
    public ObservableCollection<CollisionEffectViewModel> Effects { get; } = [];
    public ObservableCollection<CollisionEffectViewModel> FilteredEffects { get; } = [];
    public IReadOnlyList<CollisionFlagFilterOption> CollisionFlagFilters { get; }

    public string GeomFileName => _documentSession.CurrentPath?.FileName ?? "No GEOM loaded";
    public string GeomPath => _documentSession.CurrentPath?.ToLegacyString() ?? string.Empty;
    public string BlockSummary => $"Blocks: {Blocks.Count} | Effects: {CountEffects(Effects)}";
    public bool HasGeomLoaded => _documentSession.HasDocument;
    public GeomFile? GeomFile => _documentSession.Document;
    public bool IsDirty => _documentSession.IsDirty;
    public bool HasSelectedBlock => _selectedBlock != null;
    public bool HasSelectedPrim => _selectedPrim != null;
    public bool HasSelectedGeoPrim => _selectedGeoPrim != null;
    public bool HasSelectedEffect => _selectedEffect != null;
    public bool CanAddEffect => _effectLayout != null;
    public bool CanDeleteEffect => _selectedEffect != null && _effectLayout != null;
    public bool ShowBlockEditor => _selectedEffect == null && _selectedPrim == null && _selectedBlock != null;
    public bool ShowPrimEditor => _selectedEffect == null && _selectedPrim != null;
    public bool ShowEffectEditor => _selectedEffect != null;
    public bool ShowGeoPrimEditor => _selectedGeoPrim != null;

    public object? SelectedTreeItem
    {
        get => _selectedTreeItem;
        set
        {
            if (ReferenceEquals(_selectedTreeItem, value))
            {
                return;
            }
            _selectedTreeItem = value;
            OnPropertyChanged();
        }
    }

    public string BlockFilterText
    {
        get => _blockFilterText;
        set
        {
            value ??= string.Empty;
            if (_blockFilterText == value)
            {
                return;
            }
            _blockFilterText = value;
            ApplyBlockFilter();
            OnPropertyChanged();
        }
    }

    public string EffectFilterText
    {
        get => _effectFilterText;
        set
        {
            value ??= string.Empty;
            if (_effectFilterText == value)
            {
                return;
            }
            _effectFilterText = value;
            ApplyEffectFilter();
            OnPropertyChanged();
        }
    }

    public bool ShowAllEffects
    {
        get => _showAllEffects;
        set
        {
            if (_showAllEffects == value)
            {
                return;
            }
            _showAllEffects = value;
            OnPropertyChanged();
            _sceneController.UpdateEffectVisibility();
        }
    }

    public bool WireframeMode
    {
        get => _wireframeMode;
        set
        {
            if (_wireframeMode == value)
            {
                return;
            }
            _wireframeMode = value;
            OnPropertyChanged();
            _sceneHost.SetLayerRenderMode(
                SceneLayer.Collision,
                value ? Avalonia3DControl.Materials.RenderMode.Line : null);
        }
    }

    public CollisionFlagFilterOption SelectedCollisionFlagFilter
    {
        get => _selectedCollisionFlagFilter;
        set
        {
            if (value == null || Equals(_selectedCollisionFlagFilter, value))
            {
                return;
            }

            _selectedCollisionFlagFilter = value;
            _sceneController.SetAttributeFilter(value.Flag);
            OnPropertyChanged();
        }
    }

    public CollisionBlockViewModel? SelectedBlock
    {
        get => _selectedBlock;
        set => ApplyCollisionSelection(value, null, null);
    }

    public CollisionPrimViewModel? SelectedPrim
    {
        get => _selectedPrim;
        set
        {
            var block = value?.ParentBlock ?? _selectedBlock;
            ApplyCollisionSelection(block, value, null);
        }
    }

    public CollisionGeoPrimViewModel? SelectedGeoPrim
    {
        get => _selectedGeoPrim;
        set
        {
            var prim = value?.ParentPrim ?? _selectedPrim;
            var block = prim?.ParentBlock ?? _selectedBlock;
            ApplyCollisionSelection(block, prim, value);
        }
    }

    public CollisionEffectViewModel? SelectedEffect
    {
        get => _selectedEffect;
        set
        {
            if (ReferenceEquals(_selectedEffect, value) && (value == null || _selectedBlock == null))
            {
                return;
            }

            _selectedEffect = value;
            if (value != null)
            {
                ClearCollisionSelectionFields();
                EnsureEffectSelectionVisible(value);
            }
            PublishSelection();
        }
    }

    public event Action? SelectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetWorkspace(IWorkspaceCatalog workspace)
    {
        _documentSession.SetWorkspace(workspace);
    }

    public Task LoadFromFilePathAsync(string? geomPath)
    {
        return LoadAsync(string.IsNullOrWhiteSpace(geomPath) ? null : WorkspacePath.Physical(geomPath));
    }

    public Task LoadFromWorkspacePathAsync(WorkspacePath? geomPath)
    {
        return LoadAsync(geomPath);
    }

    public void Save()
    {
        SaveAsync().GetAwaiter().GetResult();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        RebuildEffectChunk();
        if (await _documentSession.SaveAsync(cancellationToken))
        {
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    public void Clear()
    {
        _effectLayout = null;
        _documentSession.Unload();
        Blocks.Clear();
        FilteredBlocks.Clear();
        Prims.Clear();
        Effects.Clear();
        FilteredEffects.Clear();
        ClearSelectionFields();
        _sceneController.Clear();
        NotifyDocumentChanged();
        PublishSelection();
    }

    public void UpdateHover(Point point, OpenGL3DControl control)
    {
        // Hover selection remains disabled to keep camera movement smooth.
    }

    public void ClearHover() => _sceneController.ClearHover();

    public void SelectAt(Point point, OpenGL3DControl control)
    {
        if (_sceneController.TryPick(point, control, out var selection))
        {
            Select(selection);
        }
        else
        {
            ClearSelection();
        }
    }

    public bool TryResolveHit(SelectionHit hit, out CollisionSceneSelection selection)
    {
        return _sceneController.TryResolveHit(hit, out selection);
    }

    public void Select(CollisionSceneSelection selection)
    {
        if (selection.Effect != null)
        {
            SelectedEffect = selection.Effect;
            return;
        }
        ApplyCollisionSelection(selection.Block, selection.Prim, selection.GeoPrim);
    }

    public void ClearSelection()
    {
        if (_selectedBlock == null && _selectedPrim == null && _selectedGeoPrim == null && _selectedEffect == null)
        {
            return;
        }
        ClearSelectionFields();
        PublishSelection();
    }

    public void SetAllBlocksVisible(bool isVisible)
    {
        foreach (var block in Blocks)
        {
            block.IsVisible = isVisible;
        }
    }

    public void SetAllEffectsVisible(bool isVisible)
    {
        foreach (var effect in TreeTraversal.Flatten(Effects, effect => effect.Children))
        {
            effect.IsVisible = isVisible;
        }
        _sceneController.UpdateEffectVisibility();
    }

    public void FocusOnBlock(CollisionBlockViewModel block) => _sceneController.FocusOnBlock(block);

    public void FocusOnPrim(CollisionPrimViewModel prim)
    {
        if (prim.ParentBlock != null)
        {
            _sceneController.FocusOnBlock(prim.ParentBlock);
        }
    }

    public void FocusOnEffect(CollisionEffectViewModel effect) => _sceneController.FocusOnEffect(effect);

    public void SnapSelectedEffectToCamera()
    {
        if (_selectedEffect == null)
        {
            return;
        }
        var position = _sceneHost.Scene.Camera.Position;
        _selectedEffect.SetPosition(position.X, position.Y, position.Z);
    }

    public CollisionEffectStructureChange? AddEffectAtCamera()
    {
        var geometry = _documentSession.Document;
        if (geometry == null || _effectLayout == null)
        {
            return null;
        }

        var position = _sceneHost.Scene.Camera.Position;
        var effect = new GeoEffect
        {
            Name = 0,
            Index = 2,
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            W = 1f
        };
        var viewModel = BuildEffectViewModel(effect, null);
        var change = new CollisionEffectStructureChange(
            viewModel,
            null,
            Effects.Count,
            _selectedEffect);
        RestoreEffect(change, viewModel);
        return change;
    }

    public CollisionEffectStructureChange? DeleteSelectedEffect()
    {
        var selected = _selectedEffect;
        var geometry = _documentSession.Document;
        if (selected == null || geometry == null || _effectLayout == null)
        {
            return null;
        }

        var index = selected.Parent == null
            ? Effects.IndexOf(selected)
            : selected.Parent.Children.IndexOf(selected);
        if (index < 0)
        {
            throw new InvalidOperationException("The selected effect is no longer in the GEOM hierarchy.");
        }

        var change = new CollisionEffectStructureChange(selected, selected.Parent, index, selected);
        RemoveEffect(change, null);
        return change;
    }

    public CollisionEffectDuplicate DuplicateEffectForPlacement(GeoEffect source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_documentSession.Document == null)
        {
            throw new InvalidOperationException("No GEOM document is loaded.");
        }
        if (_effectLayout == null)
        {
            throw new InvalidOperationException("The loaded GEOM has no effect chunk.");
        }

        var sourceViewModel = TreeTraversal.Flatten(Effects, effect => effect.Children)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.Effect, source)) ??
            throw new InvalidOperationException("The placement's GEOM effect is no longer present.");
        var usedHashes = TreeTraversal.Flatten(Effects, effect => effect.Children)
            .Select(effect => unchecked((uint)effect.Effect.Name))
            .ToHashSet();
        var hash = unchecked((uint)source.Name);
        do
        {
            hash = (hash + 1) & 0xFFFFFF;
        }
        while (hash == 0 || usedHashes.Contains(hash));

        var clone = CloneEffect(source);
        clone.Name = unchecked((int)hash);
        var cloneViewModel = BuildEffectViewModel(clone, sourceViewModel.Parent);
        var siblings = sourceViewModel.Parent == null ? Effects : sourceViewModel.Parent.Children;
        var index = siblings.IndexOf(sourceViewModel);
        if (index < 0)
        {
            throw new InvalidOperationException("The placement's GEOM effect hierarchy is stale.");
        }

        var change = new CollisionEffectStructureChange(
            cloneViewModel,
            sourceViewModel.Parent,
            index + 1,
            _selectedEffect);
        RestoreEffect(change, _selectedEffect);
        return new CollisionEffectDuplicate(change, hash);

        GeoEffect CloneEffect(GeoEffect effect)
        {
            var result = new GeoEffect
            {
                Name = effect.Name,
                Index = effect.Index,
                X = effect.X,
                Y = effect.Y,
                Z = effect.Z,
                W = effect.W,
                RotationX = effect.RotationX,
                RotationY = effect.RotationY,
                RotationZ = effect.RotationZ
            };
            _effectLayout.CloneRecord(effect, result);
            foreach (var child in effect.Children)
            {
                result.Children.Add(CloneEffect(child));
            }
            return result;
        }
    }

    public void RestoreEffect(
        CollisionEffectStructureChange change,
        CollisionEffectViewModel? selectionAfter)
    {
        ArgumentNullException.ThrowIfNull(change);
        var geometry = _documentSession.Document ??
            throw new InvalidOperationException("No GEOM document is loaded.");
        if (_effectLayout == null)
        {
            throw new InvalidOperationException("The loaded GEOM has no effect chunk.");
        }

        var viewModels = change.Parent == null ? Effects : change.Parent.Children;
        var effects = change.Parent == null ? geometry.GeoEffects : change.Parent.Effect.Children;
        if (change.Index < 0 || change.Index > viewModels.Count || change.Index > effects.Count)
        {
            throw new InvalidOperationException("The effect can no longer be restored at its original position.");
        }
        if (viewModels.Contains(change.Effect) || effects.Contains(change.Effect.Effect))
        {
            throw new InvalidOperationException("The effect is already present in the GEOM hierarchy.");
        }

        ClearSelection();
        change.Effect.Parent = change.Parent;
        effects.Insert(change.Index, change.Effect.Effect);
        viewModels.Insert(change.Index, change.Effect);
        CommitEffectStructure();
        SelectedEffect = selectionAfter;
    }

    public void RemoveEffect(
        CollisionEffectStructureChange change,
        CollisionEffectViewModel? selectionAfter)
    {
        ArgumentNullException.ThrowIfNull(change);
        var geometry = _documentSession.Document ??
            throw new InvalidOperationException("No GEOM document is loaded.");
        if (_effectLayout == null)
        {
            throw new InvalidOperationException("The loaded GEOM has no effect chunk.");
        }

        var viewModels = change.Parent == null ? Effects : change.Parent.Children;
        var effects = change.Parent == null ? geometry.GeoEffects : change.Parent.Effect.Children;
        if (change.Index < 0 || change.Index >= viewModels.Count || change.Index >= effects.Count ||
            !ReferenceEquals(viewModels[change.Index], change.Effect) ||
            !ReferenceEquals(effects[change.Index], change.Effect.Effect))
        {
            throw new InvalidOperationException("The effect hierarchy changed and the edit cannot be replayed.");
        }

        ClearSelection();
        viewModels.RemoveAt(change.Index);
        effects.RemoveAt(change.Index);
        CommitEffectStructure();
        SelectedEffect = selectionAfter;
    }

    public bool TrySetEffectPosition(GeoEffect effect, Vector3 position)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var viewModel = TreeTraversal.Flatten(Effects, effect => effect.Children)
            .FirstOrDefault(candidate => ReferenceEquals(candidate.Effect, effect));
        if (viewModel == null)
        {
            return false;
        }

        viewModel.SetPosition(position.X, position.Y, position.Z);
        return true;
    }

    public bool TryGetEffectModel(CollisionEffectViewModel effect, out Avalonia3DControl.Core.Models.Model3D model)
    {
        return _sceneController.TryGetEffectModel(effect, out model!);
    }

    private async Task LoadAsync(WorkspacePath? path)
    {
        Clear();
        if (path == null)
        {
            return;
        }

        try
        {
            if (!await _documentSession.LoadAsync(path))
            {
                return;
            }
            BuildViewModels();
            var geometry = _documentSession.Document!;
            _effectLayout = geometry.GetChunkFromType(GeoChunkType.PROPS) == null
                ? null
                : GeoEffectChunkBuilder.Capture(
                    geometry.GeomChunk6,
                    geometry.GeoEffects,
                    geometry.Reader.Endianness);
            _sceneController.BuildSceneModels(_documentSession.Document!, Blocks, Effects);
            _documentSession.CloseDocumentStream();
            NotifyDocumentChanged();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "[Collision] Failed to load GEOM {GeomPath}", path);
            Clear();
            MessageDialog.Error("GEOM Load Error", $"Failed to load GEOM file:\n\n{path.FileName}\n\n{exception.Message}");
        }
    }

    private void BuildViewModels()
    {
        var geometry = _documentSession.Document;
        if (geometry == null)
        {
            return;
        }

        var index = 0;
        foreach (var block in geometry.GeomBlocks)
        {
            var prims = BuildPrimViewModels(geometry, block);
            var viewModel = new CollisionBlockViewModel(block, index++, prims, MarkDirty, OnBlockVisibilityChanged);
            foreach (var prim in prims)
            {
                prim.ParentBlock = viewModel;
            }
            Blocks.Add(viewModel);
        }

        foreach (var effect in geometry.GeoEffects)
        {
            Effects.Add(BuildEffectViewModel(effect, null));
        }
        ApplyBlockFilter();
        ApplyEffectFilter();
    }

    private List<CollisionPrimViewModel> BuildPrimViewModels(GeomFile geometry, GeoBlock block)
    {
        var result = new List<CollisionPrimViewModel>();
        if (!geometry.BlockFaceData.TryGetValue(block, out var faces))
        {
            return result;
        }
        for (var index = 0; index < faces.Count; index++)
        {
            CollisionPrimViewModel? viewModel = null;
            viewModel = new CollisionPrimViewModel(faces[index], index, () =>
            {
                MarkDirty();
                if (viewModel?.ParentBlock is { } parentBlock)
                {
                    _sceneController.RefreshBlockAppearance(parentBlock);
                }
            });
            result.Add(viewModel);
        }
        return result;
    }

    private CollisionEffectViewModel BuildEffectViewModel(GeoEffect effect, CollisionEffectViewModel? parent)
    {
        var viewModel = new CollisionEffectViewModel(effect, MarkDirty, OnEffectChanged) { Parent = parent };
        foreach (var child in effect.Children)
        {
            viewModel.Children.Add(BuildEffectViewModel(child, viewModel));
        }
        viewModel.ApplyFilter(string.Empty);
        return viewModel;
    }

    private void ApplyCollisionSelection(
        CollisionBlockViewModel? block,
        CollisionPrimViewModel? prim,
        CollisionGeoPrimViewModel? geoPrim)
    {
        if (ReferenceEquals(_selectedBlock, block) && ReferenceEquals(_selectedPrim, prim) &&
            ReferenceEquals(_selectedGeoPrim, geoPrim) && _selectedEffect == null)
        {
            return;
        }

        _selectedEffect = null;
        _selectedBlock = block;
        _selectedPrim = prim;
        _selectedGeoPrim = geoPrim;
        RebuildSelectedPrims();

        SelectedTreeItem = (object?)geoPrim ?? (object?)prim ?? block;
        PublishSelection();
    }

    private void PublishSelection()
    {
        _sceneController.SetSelection(_selectedBlock, _selectedPrim, _selectedGeoPrim, _selectedEffect);
        OnPropertyChanged(nameof(SelectedBlock));
        OnPropertyChanged(nameof(SelectedPrim));
        OnPropertyChanged(nameof(SelectedGeoPrim));
        OnPropertyChanged(nameof(SelectedEffect));
        OnPropertyChanged(nameof(HasSelectedBlock));
        OnPropertyChanged(nameof(HasSelectedPrim));
        OnPropertyChanged(nameof(HasSelectedGeoPrim));
        OnPropertyChanged(nameof(HasSelectedEffect));
        OnPropertyChanged(nameof(CanDeleteEffect));
        OnPropertyChanged(nameof(ShowBlockEditor));
        OnPropertyChanged(nameof(ShowPrimEditor));
        OnPropertyChanged(nameof(ShowGeoPrimEditor));
        OnPropertyChanged(nameof(ShowEffectEditor));
        SelectionChanged?.Invoke();
    }

    private void ClearSelectionFields()
    {
        ClearCollisionSelectionFields();
        _selectedEffect = null;
    }

    private void ClearCollisionSelectionFields()
    {
        _selectedBlock = null;
        _selectedPrim = null;
        _selectedGeoPrim = null;
        SelectedTreeItem = null;
        Prims.Clear();
    }

    private void RebuildSelectedPrims()
    {
        Prims.Clear();
        if (_selectedBlock == null)
        {
            return;
        }
        foreach (var prim in _selectedBlock.Prims)
        {
            Prims.Add(prim);
        }
    }

    private void EnsureEffectSelectionVisible(CollisionEffectViewModel selected)
    {
        var root = selected;
        while (root.Parent != null)
        {
            root = root.Parent;
        }
        if (!FilteredEffects.Contains(root))
        {
            FilteredEffects.Add(root);
        }
        var current = selected;
        while (current.Parent != null)
        {
            var parent = current.Parent;
            if (!parent.VisibleChildren.Contains(current))
            {
                parent.VisibleChildren.Add(current);
            }
            current = parent;
        }
    }

    private void ApplyBlockFilter()
    {
        FilteredBlocks.Clear();
        var filter = _blockFilterText.Trim();
        foreach (var block in Blocks)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                block.ApplyPrimFilter(string.Empty);
                FilteredBlocks.Add(block);
                continue;
            }
            var blockMatch = block.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase);
            if (blockMatch || block.ApplyPrimFilter(filter))
            {
                FilteredBlocks.Add(block);
            }
        }
    }

    private void ApplyEffectFilter()
    {
        FilteredEffects.Clear();
        var filter = _effectFilterText.Trim();
        foreach (var effect in Effects)
        {
            var include = effect.ApplyFilter(filter);
            if (_selectedEffect != null && !include && IsAncestorOrSelf(effect, _selectedEffect))
            {
                include = true;
            }
            if (include)
            {
                FilteredEffects.Add(effect);
            }
        }
    }

    private static bool IsAncestorOrSelf(CollisionEffectViewModel root, CollisionEffectViewModel target)
    {
        for (var current = target; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }
        }
        return false;
    }

    private void OnBlockVisibilityChanged(CollisionBlockViewModel block)
    {
        _sceneController.SetBlockVisible(block);
        if (!block.IsVisible && ReferenceEquals(_selectedBlock, block))
        {
            ClearSelection();
        }
    }

    private void OnEffectChanged(CollisionEffectViewModel effect)
    {
        _sceneController.UpdateEffect(effect);
    }

    private void CommitEffectStructure()
    {
        RebuildEffectChunk();
        MarkDirty();
        ApplyEffectFilter();
        _sceneController.RebuildEffectModels(Effects);
        OnPropertyChanged(nameof(BlockSummary));
        OnPropertyChanged(nameof(CanAddEffect));
        OnPropertyChanged(nameof(CanDeleteEffect));
    }

    private void RebuildEffectChunk()
    {
        var geometry = _documentSession.Document;
        if (geometry == null || _effectLayout == null)
        {
            return;
        }
        geometry.GeomChunk6 = _effectLayout.Rebuild(geometry.GeoEffects);
    }

    private void MarkDirty()
    {
        var wasDirty = _documentSession.IsDirty;
        _documentSession.MarkDirty();
        if (!wasDirty && _documentSession.IsDirty)
        {
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    private void NotifyDocumentChanged()
    {
        OnPropertyChanged(nameof(GeomFileName));
        OnPropertyChanged(nameof(GeomPath));
        OnPropertyChanged(nameof(BlockSummary));
        OnPropertyChanged(nameof(HasGeomLoaded));
        OnPropertyChanged(nameof(GeomFile));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanAddEffect));
        OnPropertyChanged(nameof(CanDeleteEffect));
    }

    private static int CountEffects(IEnumerable<CollisionEffectViewModel> effects)
    {
        return effects.Sum(effect => 1 + CountEffects(effect.Children));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
