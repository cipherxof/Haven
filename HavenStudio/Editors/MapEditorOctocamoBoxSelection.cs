using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia3DControl;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Materials;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Editors;

public sealed partial class MapEditorViewModel
{
    private bool _boxSelectOctocamoFaces;
    private Point? _selectionBoxStart;
    private bool _selectionBoxVisible;
    private double _selectionBoxLeft;
    private double _selectionBoxTop;
    private double _selectionBoxWidth;
    private double _selectionBoxHeight;
    public bool SelectionBoxVisible => _selectionBoxVisible;
    public double SelectionBoxLeft => _selectionBoxLeft;
    public double SelectionBoxTop => _selectionBoxTop;
    public double SelectionBoxWidth => _selectionBoxWidth;
    public double SelectionBoxHeight => _selectionBoxHeight;
    private OctocamoFaceCandidate? _selectedOctocamoFace;
    private string _octocamoFaceFilter = string.Empty;
    private bool _changingOctocamoBatch;
    private readonly Dictionary<Model3D, Model3D> _octocamoWireframeModels = new();
    private readonly Model3D _octocamoFocusWireframe = new()
    {
        Name = "OctoCamo inspected face",
        Color = new Vector3(0.08f, 0.94f, 1f),
        RenderModeOverride = RenderMode.Line,
        RenderOnTop = true,
        RenderOnTopOrder = 1,
        WriteDepth = false
    };
    private Model3D? _focusedOctocamoSource;
    private CollisionGeoPrimViewModel? _focusedOctocamoPolygon;
    public ObservableCollection<OctocamoFaceCandidate> OctocamoBoxFaces { get; } = [];
    public string OctocamoFaceFilter
    {
        get => _octocamoFaceFilter;
        set
        {
            if (_octocamoFaceFilter == value) return;
            _octocamoFaceFilter = value ?? string.Empty;
            ClearOctocamoBatchSelection();
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilteredOctocamoBoxFaces));
            OnPropertyChanged(nameof(SelectedOctocamoFace));
        }
    }
    public IReadOnlyList<OctocamoFaceCandidate> FilteredOctocamoBoxFaces => OctocamoBoxFaces
        .Where(face => string.IsNullOrWhiteSpace(_octocamoFaceFilter) ||
            $"{face.AddressText} {face.MaterialText} {face.DetailsText}"
                .Contains(_octocamoFaceFilter.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
    public bool CanEditOctocamoBatch => OctocamoBoxFaces.Any(face => face.IsBatchSelected);
    public string OctocamoBatchSummary => $"{OctocamoBoxFaces.Count(face => face.IsBatchSelected)} checked for batch editing · {FilteredOctocamoBoxFaces.Count} match filter";

    public void SelectAllFilteredOctocamoFaces()
    {
        var filtered = FilteredOctocamoBoxFaces.ToHashSet();
        _changingOctocamoBatch = true;
        try { foreach (var face in OctocamoBoxFaces) face.IsBatchSelected = filtered.Contains(face); }
        finally { _changingOctocamoBatch = false; }
        NotifyOctocamoBatch();
    }

    public void ClearOctocamoBatchSelection()
    {
        _changingOctocamoBatch = true;
        try { foreach (var face in OctocamoBoxFaces) face.IsBatchSelected = false; }
        finally { _changingOctocamoBatch = false; }
        NotifyOctocamoBatch();
    }

    private void OnOctocamoFacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_changingOctocamoBatch && args.PropertyName == nameof(OctocamoFaceCandidate.IsBatchSelected))
            NotifyOctocamoBatch();
    }

    private void NotifyOctocamoBatch()
    {
        OnPropertyChanged(nameof(CanEditOctocamoBatch));
        OnPropertyChanged(nameof(OctocamoBatchSummary));
        RefreshOctocamoBatchWireframe();
    }

    private void RefreshOctocamoBatchWireframe()
    {
        var outlines = new List<Model3D>();
        if (_octocamoViewEnabled)
        {
            // A selected preview can be the closest box hit; resolve it to the
            // real block model so the complete polygon (not one triangle) is outlined.
            var groups = OctocamoBoxFaces.Where(face => face.IsBatchSelected)
                .GroupBy(face => face.Instance == null
                    ? _collisionEditor.GetBlockModel(face.Primitive.ParentBlock!) ?? face.Hit.Model
                    : face.Hit.Model);
            foreach (var group in groups)
            {
                var source = group.Key;
                if (!source.Visible) continue;
                var selected = group.Select(face => face.Polygon).ToHashSet();
                if (!_octocamoWireframeModels.TryGetValue(source, out var outline))
                {
                    outline = new Model3D
                    {
                        Name = "OctoCamo selected faces",
                        Color = new Vector3(1f, 0.78f, 0.08f),
                        RenderModeOverride = RenderMode.Line,
                        RenderOnTop = true,
                        WriteDepth = false
                    };
                    _octocamoWireframeModels[source] = outline;
                }
                if (UpdateOctocamoWireframe(outline, source, selected)) outlines.Add(outline);
            }
        }
        _sceneHost.ReplaceLayer(SceneLayer.OctocamoSelection, outlines);
    }

    private void RefreshOctocamoFocusWireframe()
    {
        var polygon = _collisionEditor.SelectedGeoPrim;
        if (!_octocamoViewEnabled || polygon == null || _collisionEditor.SelectedPrim?.ParentBlock is not { } block)
        {
            _focusedOctocamoSource = null;
            _focusedOctocamoPolygon = null;
            _sceneHost.ClearLayer(SceneLayer.OctocamoFocus);
            return;
        }

        if (!ReferenceEquals(_focusedOctocamoPolygon, polygon))
        {
            _focusedOctocamoSource = _collisionEditor.GetBlockModel(block);
            _focusedOctocamoPolygon = polygon;
        }
        var source = _focusedOctocamoSource ?? _collisionEditor.GetBlockModel(block);
        _sceneHost.ReplaceLayer(SceneLayer.OctocamoFocus,
            source != null && source.Visible &&
            UpdateOctocamoWireframe(_octocamoFocusWireframe, source, [polygon])
                ? [_octocamoFocusWireframe] : []);
    }

    private bool UpdateOctocamoWireframe(Model3D outline, Model3D source,
        HashSet<CollisionGeoPrimViewModel> selected)
    {
        var trianglePolygons = _collisionEditor.GetTrianglePolygons(source);
        var positions = new List<float>();
        var indices = new List<uint>();
        int count = source.Indices.Length / 3;
        for (int triangle = 0; triangle < count; triangle++)
        {
            CollisionGeoPrimViewModel? polygon = null;
            if (trianglePolygons != null && triangle < trianglePolygons.Count)
                polygon = trianglePolygons[triangle];
            if (polygon == null || !selected.Contains(polygon)) continue;
            int start = triangle * 3;
            if (source.Indices[start..(start + 3)].Any(index => index * 3 + 2 >= source.Positions.Length))
                continue;
            for (int vertex = 0; vertex < 3; vertex++)
            {
                int positionIndex = (int)source.Indices[start + vertex] * 3;
                indices.Add((uint)(positions.Count / 3));
                positions.Add(source.Positions[positionIndex]);
                positions.Add(source.Positions[positionIndex + 1]);
                positions.Add(source.Positions[positionIndex + 2]);
            }
        }
        if (indices.Count == 0) return false;
        outline.Positions = positions.ToArray();
        outline.VertexCount = positions.Count / 3;
        outline.Indices = indices.ToArray();
        outline.IndexCount = indices.Count;
        outline.Position = source.Position;
        outline.Rotation = source.Rotation;
        outline.Scale = source.Scale;
        outline.VerticesNeedUpdate = true;
        outline.IndicesNeedUpdate = true;
        return true;
    }

    public OctocamoBatchEditViewModel CreateOctocamoBatchEditor()
    {
        if (!_octocamoViewEnabled || _octocamoCatalog == null || _collisionEditor.GeomFile == null)
            throw new InvalidOperationException("Load a GEOM and enable OctoCamo view first.");
        var faces = OctocamoBoxFaces.Where(face => face.IsBatchSelected).ToArray();
        var aliases = _collisionEditor.GetPolygonAliases(faces.Select(face => face.SelectorOffset));
        var geometry = _collisionEditor.GeomFile;
        var catalog = _octocamoCatalog;
        return new OctocamoBatchEditViewModel(faces, aliases, catalog, (edits, muscle) =>
        {
            void Apply(bool forward)
            {
                if (!ReferenceEquals(geometry, _collisionEditor.GeomFile) || !ReferenceEquals(catalog, _octocamoCatalog))
                    throw new InvalidOperationException("The loaded GEOM has changed. Select the faces again.");
                var current = _collisionEditor.GetPolygonAliases(edits.Select(edit => edit.Offset));
                var byOffset = edits.ToDictionary(edit => edit.Offset);
                if (current.Select(poly => poly.ParentPrim.Prim.Offset + 0x26 + poly.ParentPrim.Children.IndexOf(poly) * 8)
                    .Distinct().Count() != edits.Count)
                    throw new InvalidOperationException("Some edited faces no longer exist. No changes were made.");
                // Validate every alias before touching any selector. A later manual
                // edit must not be silently overwritten by undo/redo of this batch.
                foreach (var poly in current)
                {
                    var offset = poly.ParentPrim.Prim.Offset + 0x26 + poly.ParentPrim.Children.IndexOf(poly) * 8;
                    var edit = byOffset[offset];
                    if (poly.Poly!.Attribute != (forward ? edit.Before : edit.After))
                        throw new InvalidOperationException($"Face 0x{offset:X} changed since this batch. No changes were made.");
                }
                _collisionEditor.SetPolygonAttributesWithAliases(edits, forward);
                OctocamoMusclePatternView = muscle;
                OnOctocamoEdited();
                OnCollisionSelectionChanged();
                SetManipulationStatus($"OctoCamo batch {(forward ? "applied" : "undone")}: {edits.Count} unique face selectors. Save Map updates the loaded GEOM and its aliases.");
            }
            _history.Execute($"change OctoCamo on {edits.Count} faces", () => Apply(true), () => Apply(false));
        });
    }
    public string OctocamoBoxSummary => OctocamoBoxFaces.Count == 0
        ? "Shift + left-drag a box, then choose a face below. Includes obscured faces at all depths."
        : $"{OctocamoBoxFaces.Count} faces intersect the box (all depths). Choose one to inspect/edit. Shared byte-offset aliases are grouped; independent overlaps remain separate.";
    public bool BoxSelectOctocamoFaces
    {
        get => _boxSelectOctocamoFaces;
        set
        {
            if (_boxSelectOctocamoFaces == value) return;
            _boxSelectOctocamoFaces = value;
            OnPropertyChanged();
        }
    }
    public OctocamoFaceCandidate? SelectedOctocamoFace
    {
        get => _selectedOctocamoFace;
        set
        {
            if (ReferenceEquals(value, _selectedOctocamoFace)) return;
            // A ListBox can send null while its ItemsSource is refreshed after
            // editing/filtering. Do not lose the user's exact face mid-edit.
            if (value == null && _selectedOctocamoFace != null &&
                OctocamoBoxFaces.Contains(_selectedOctocamoFace) &&
                ReferenceEquals(_selectedOctocamoFace.Polygon, _collisionEditor.SelectedGeoPrim)) return;
            if (value != null && (!OctocamoViewEnabled || !OctocamoBoxFaces.Contains(value))) return;
            _selectedOctocamoFace = value;
            OnPropertyChanged();
            if (value == null) return;
            _focusedOctocamoSource = value.Instance == null
                ? _collisionEditor.GetBlockModel(value.Primitive.ParentBlock!) ?? value.Hit.Model
                : value.Hit.Model;
            _focusedOctocamoPolygon = value.Polygon;
            SelectEntity(new PrimEntity(value.Primitive, value.Polygon));
            RefreshOctocamoFocusWireframe();
            SetManipulationStatus($"Selected face {value.AddressText}. {value.ScopeText}");
        }
    }

    public void ClearOctocamoFaceBox()
    {
        _selectedOctocamoFace = null;
        OnPropertyChanged(nameof(SelectedOctocamoFace));
        foreach (var face in OctocamoBoxFaces) face.PropertyChanged -= OnOctocamoFacePropertyChanged;
        OctocamoBoxFaces.Clear();
        NotifyOctocamoBatch();
        RefreshOctocamoFocusWireframe();
        OnPropertyChanged(nameof(OctocamoBoxSummary));
        OnPropertyChanged(nameof(FilteredOctocamoBoxFaces));
    }

    private void CompleteOctocamoBoxSelection(Rect rectangle, OpenGL3DControl control) =>
        SelectOctocamoFacesInBox(rectangle, control.Scene.Camera.GetViewMatrix(),
            control.Scene.Camera.GetProjectionMatrix(), control.Bounds.Width, control.Bounds.Height);

    public void SelectOctocamoFacesInBox(Rect rectangle, Matrix4 view, Matrix4 projection,
        double width, double height)
    {
        ClearOctocamoFaceBox();
        if (!_octocamoViewEnabled || _octocamoCatalog == null) return;
        var models = _sceneHost.GetLayerModels(SceneLayer.Collision);
        var faces = new Dictionary<(object? Instance, int Offset), OctocamoFaceCandidate>();
        // Do not query the depth buffer/closest ray hit: selection must expose the
        // independently editable polygons hidden behind the visible surface.
        foreach (var hit in SelectionBoxPicker.FindTriangles(rectangle, view, projection, width, height, models))
        {
            CollisionPrimViewModel? primitive;
            CollisionGeoPrimViewModel? polygon;
            object? instance = null;
            var owner = "Static collision";
            if (!_collisionEditor.TryResolveHit(hit, out var selection)) continue;
            primitive = selection.Prim;
            polygon = selection.GeoPrim;
            if (primitive == null || polygon?.Poly == null || primitive.ParentBlock == null ||
                !primitive.IsVisible || !polygon.IsVisible ||
                (primitive.Prim.Attribute & GeoCollisionAttributes.Player) == 0) continue;
            var candidate = new OctocamoFaceCandidate(primitive, polygon, hit, instance, owner, _octocamoCatalog);
            var key = (instance, candidate.SelectorOffset);
            if (!faces.TryGetValue(key, out var current) || hit.Distance < current.Hit.Distance)
                faces[key] = candidate;
        }
        ClearSelection();
        foreach (var face in faces.Values.OrderBy(face => face.Hit.Distance).ThenBy(face => face.SelectorOffset))
        {
            face.PropertyChanged += OnOctocamoFacePropertyChanged;
            OctocamoBoxFaces.Add(face);
        }
        NotifyOctocamoBatch();
        OnPropertyChanged(nameof(OctocamoBoxSummary));
        OnPropertyChanged(nameof(FilteredOctocamoBoxFaces));
        SetManipulationStatus(faces.Count == 0
            ? "No visible player-contact faces intersect this box. Check Collision visibility."
            : $"{faces.Count} OctoCamo faces found. Select an exact face from the left sidebar; no data has been changed.");
    }

    private void RefreshOctocamoBoxLabels()
    {
        foreach (var candidate in OctocamoBoxFaces) candidate.Refresh();
        if (!string.IsNullOrWhiteSpace(_octocamoFaceFilter))
            OnPropertyChanged(nameof(FilteredOctocamoBoxFaces));
        OnPropertyChanged(nameof(SelectedOctocamoFace));
        NotifyOctocamoBatch();
    }

    private void SynchronizeOctocamoBoxSelection()
    {
        if (_selectedOctocamoFace != null &&
            !ReferenceEquals(_selectedOctocamoFace.Polygon, _collisionEditor.SelectedGeoPrim))
        {
            _selectedOctocamoFace = null;
            OnPropertyChanged(nameof(SelectedOctocamoFace));
        }
    }

    internal static Rect NormalizeSelectionRectangle(Point first, Point second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        return new Rect(left, top, Math.Max(first.X, second.X) - left,
            Math.Max(first.Y, second.Y) - top);
    }

    private static Point ClampToViewport(Point point, OpenGL3DControl control) =>
        new(Math.Clamp(point.X, 0, control.Bounds.Width),
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
        if (!_selectionBoxVisible && _selectionBoxWidth == 0 && _selectionBoxHeight == 0) return;
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
}

