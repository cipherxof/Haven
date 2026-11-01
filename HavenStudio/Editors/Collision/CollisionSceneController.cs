using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia3DControl;
using Avalonia3DControl.Core.Models;
using HavenStudio.Formats.Geo;
using HavenStudio.Rendering;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Editors;

public readonly record struct CollisionSceneSelection(
    CollisionBlockViewModel? Block,
    CollisionPrimViewModel? Prim,
    CollisionGeoPrimViewModel? GeoPrim,
    CollisionEffectViewModel? Effect);

public sealed class CollisionSceneController
{
    private const float EffectSizeMultiplier = 500.0f;

    private static readonly Vector3 DefaultBlockColor = new(0.65f, 0.65f, 0.65f);
    private static readonly Vector3 HoverBlockColor = new(0.25f, 0.95f, 0.35f);
    private static readonly Vector3 SelectedBlockColor = new(0.05f, 0.85f, 0.20f);
    private static readonly Vector3 PrimHighlightColor = new(0.05f, 0.95f, 0.20f);
    private static readonly Vector3 EffectColor = new(0.95f, 0.15f, 0.15f);
    private static readonly Vector3 SelectedEffectColor = new(1.0f, 0.55f, 0.05f);
    private static readonly Vector3 RelatedEffectColor = new(0.10f, 1.0f, 0.20f);

    private readonly SceneHost _sceneHost;
    private readonly Dictionary<Model3D, CollisionBlockViewModel> _blockModelLookup = new();
    private readonly Dictionary<CollisionBlockViewModel, Model3D> _blockModels = new();
    private readonly HashSet<Model3D> _referenceBlockModels = [];
    private readonly Dictionary<Model3D, CollisionEffectViewModel> _effectModelLookup = new();
    private readonly Dictionary<CollisionEffectViewModel, Model3D> _effectModels = new();
    private readonly Dictionary<Model3D, int[]> _trianglePrimLookup = new();
    private readonly Dictionary<CollisionPrimViewModel, int[]> _primTriangleLookup = new();
    private readonly Dictionary<CollisionGeoPrimViewModel, int[]> _geoPrimTriangleLookup = new();
    private readonly Dictionary<Model3D, CollisionGeoPrimViewModel?[]> _triangleGeoPrimLookup = new();
    private readonly Dictionary<Model3D, uint[]> _unfilteredIndices = new();
    private readonly Dictionary<Model3D, int[]> _unfilteredTrianglePrimLookup = new();
    private readonly Dictionary<Model3D, int[]> _unfilteredTrianglePolyLookup = new();

    private Model3D? _hoveredBlockModel;
    private Model3D? _selectedBlockModel;
    private readonly HashSet<Model3D> _selectedEffectModels = [];
    private readonly HashSet<Model3D> _selectedRaceEffectModels = [];
    private readonly HashSet<Model3D> _relatedEffectModels = [];
    private readonly HashSet<Model3D> _unavailableEffectModels = [];
    private CollisionPrimViewModel? _selectedPrim;
    private CollisionGeoPrimViewModel? _selectedGeoPrim;
    private ulong? _attributeFilter;
    private OctocamoSurfaceCatalog? _octocamoCatalog;
    private Avalonia3DControl.Materials.ShadingMode? _preOctocamoShading;
    private readonly Model3D _selectedOctocamoPreview = new()
    {
        Name = "SelectedOctocamoFacePreview",
        Visible = false,
        Alpha = 1f,
        ForceOpaqueAlpha = true,
        WriteDepth = false,
        DepthBias = -2f,
        RenderAfterTransparent = true
    };

    public CollisionSceneController(SceneHost sceneHost)
    {
        _sceneHost = sceneHost ?? throw new ArgumentNullException(nameof(sceneHost));
    }

    public void BuildSceneModels(
        GeomFile geom,
        IReadOnlyList<CollisionBlockViewModel> blocks,
        IEnumerable<CollisionEffectViewModel> effects)
    {
        ArgumentNullException.ThrowIfNull(geom);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(effects);

        ClearLookups();
        _sceneHost.ViewportControl.SetRenderMode(Avalonia3DControl.Materials.RenderMode.Fill);

        var blockModels = GeomSceneBuilder.BuildBlockModels(
            geom,
            out var modelToBlock,
            out _,
            out var trianglePrimIndex,
            out var trianglePolyIndex);
        var staticBlocks = geom.GeomGroupBlocks.Values.SelectMany(items => items).ToHashSet();

        foreach (var model in blockModels)
        {
            model.Color = DefaultBlockColor;
            if (!modelToBlock.TryGetValue(model, out var block))
            {
                continue;
            }

            var blockView = blocks.FirstOrDefault(candidate => ReferenceEquals(candidate.Block, block));
            if (blockView == null)
            {
                continue;
            }

            _blockModelLookup[model] = blockView;
            _blockModels[blockView] = model;
            if (!staticBlocks.Contains(block)) _referenceBlockModels.Add(model);
            model.Visible = blockView.IsVisible;
            if (_octocamoCatalog != null && _referenceBlockModels.Contains(model)) model.Visible = false;
            if (trianglePrimIndex.TryGetValue(model, out var primitiveMap) &&
                trianglePolyIndex.TryGetValue(model, out var polygonMap))
            {
                _unfilteredIndices[model] = model.Indices.ToArray();
                _unfilteredTrianglePrimLookup[model] = primitiveMap.ToArray();
                _unfilteredTrianglePolyLookup[model] = polygonMap.ToArray();
                BuildTriangleLookups(model, blockView, primitiveMap, polygonMap);
            }
        }

        if (_attributeFilter != null || _octocamoCatalog != null)
        {
            foreach (var model in blockModels)
            {
                ApplyAttributeFilter(model);
            }
        }

        var effectModels = new List<Model3D>();
        foreach (var effectView in TreeTraversal.Flatten(effects, effect => effect.Children))
        {
            var model = CreateEffectMarker(effectView);
            effectModels.Add(model);
            _effectModelLookup[model] = effectView;
            _effectModels[effectView] = model;
        }

        _sceneHost.SetModelVisible(SceneLayer.Collision, _selectedOctocamoPreview, false);
        _selectedOctocamoPreview.Visible = false;
        _sceneHost.ReplaceLayer(SceneLayer.Collision, blockModels.Append(_selectedOctocamoPreview));
        if (_octocamoCatalog != null) SetOctocamoCatalog(_octocamoCatalog);
        _sceneHost.ReplaceLayer(SceneLayer.Effects, effectModels);
        var grid = GeomSceneBuilder.BuildGridModel(geom);
        _sceneHost.ReplaceLayer(SceneLayer.Grid, grid == null ? Array.Empty<Model3D>() : new[] { grid });
        UpdateEffectVisibility();
        FocusCamera(blockModels);
    }

    public void Clear()
    {
        ClearLookups();
        _sceneHost.ClearLayer(SceneLayer.Collision);
        _sceneHost.ClearLayer(SceneLayer.Effects);
        _sceneHost.ClearLayer(SceneLayer.Grid);
    }

    public bool TryPick(Point point, OpenGL3DControl control, out CollisionSceneSelection selection)
    {
        var models = _blockModelLookup.Keys.Concat(_effectModelLookup.Keys).Where(model => model.Visible);
        if (!SelectionRaycaster.TryPickTriangle(point, control, models, out var hit))
        {
            selection = default;
            return false;
        }

        return TryResolveHit(hit, out selection);
    }

    public bool TryResolveHit(SelectionHit hit, out CollisionSceneSelection selection)
    {
        if (ReferenceEquals(hit.Model, _selectedOctocamoPreview) &&
            _selectedBlockModel != null && _selectedPrim != null && _selectedGeoPrim != null &&
            hit.TriangleIndex >= 0 && hit.TriangleIndex < _selectedOctocamoPreview.IndexCount / 3 &&
            _selectedOctocamoPreview.Visible)
        {
            selection = new CollisionSceneSelection(_blockModelLookup[_selectedBlockModel],
                _selectedPrim, _selectedGeoPrim, null);
            return true;
        }
        if (_effectModelLookup.TryGetValue(hit.Model, out var effect))
        {
            selection = new CollisionSceneSelection(null, null, null, effect);
            return true;
        }

        if (!_blockModelLookup.TryGetValue(hit.Model, out var block))
        {
            selection = default;
            return false;
        }

        CollisionPrimViewModel? prim = null;
        CollisionGeoPrimViewModel? geoPrim = null;
        if (_trianglePrimLookup.TryGetValue(hit.Model, out var primMap) &&
            hit.TriangleIndex >= 0 && hit.TriangleIndex < primMap.Length)
        {
            var primIndex = primMap[hit.TriangleIndex];
            if (primIndex >= 0 && primIndex < block.Prims.Count)
            {
                prim = block.Prims[primIndex];
            }
        }

        if (_triangleGeoPrimLookup.TryGetValue(hit.Model, out var geoMap) &&
            hit.TriangleIndex >= 0 && hit.TriangleIndex < geoMap.Length)
        {
            geoPrim = geoMap[hit.TriangleIndex];
        }

        selection = new CollisionSceneSelection(block, prim, geoPrim, null);
        return true;
    }

    public IReadOnlyList<CollisionGeoPrimViewModel?>? GetTrianglePolygons(Model3D model) =>
        _triangleGeoPrimLookup.GetValueOrDefault(model);

    public Model3D? GetBlockModel(CollisionBlockViewModel block) =>
        _blockModels.GetValueOrDefault(block);

    public void SetSelection(
        CollisionBlockViewModel? block,
        CollisionPrimViewModel? prim,
        CollisionGeoPrimViewModel? geoPrim,
        CollisionEffectViewModel? effect)
    {
        var previousBlockModel = _selectedBlockModel;
        _selectedBlockModel = block != null && _blockModels.TryGetValue(block, out var blockModel) ? blockModel : null;
        _selectedPrim = prim;
        _selectedGeoPrim = geoPrim;

        if (previousBlockModel != null)
        {
            UpdateBlockColor(previousBlockModel);
        }

        UpdateBlockColor(_selectedBlockModel);
        SetEffectSelection(effect == null ? [] : [effect]);
        ApplyPrimHighlight();
    }

    public void SetOctocamoCatalog(OctocamoSurfaceCatalog? catalog)
    {
        _octocamoCatalog = catalog;
        foreach (var model in _blockModelLookup.Keys)
        {
            if (_referenceBlockModels.Contains(model))
                _sceneHost.SetModelVisible(SceneLayer.Collision, model,
                    catalog == null && _blockModelLookup[model].IsVisible);
            ApplyAttributeFilter(model);
        }
        var atlas = catalog?.PreviewMusclePatterns == true ? catalog.PatternAtlas : null;
        if (atlas != null)
        {
            _preOctocamoShading ??= _sceneHost.ViewportControl.CurrentShadingMode;
            _sceneHost.ViewportControl.SetShadingMode(Avalonia3DControl.Materials.ShadingMode.Texture);
        }
        else if (catalog == null && _preOctocamoShading is { } previous)
        {
            _sceneHost.ViewportControl.SetShadingMode(previous);
            _preOctocamoShading = null;
        }
        _sceneHost.ViewportControl.ApplySharedPreviewTexture("octocamo", _blockModelLookup.Keys.Append(_selectedOctocamoPreview).ToArray(),
            atlas?.Width ?? 0, atlas?.Height ?? 0, atlas?.Rgba);
        ApplyPrimHighlight();
    }

    public void SetEffectSelection(IEnumerable<CollisionEffectViewModel> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var previousModels = _selectedEffectModels.ToArray();
        _selectedEffectModels.Clear();
        foreach (var effect in effects)
        {
            if (_effectModels.TryGetValue(effect, out var model))
            {
                _selectedEffectModels.Add(model);
            }
        }

        foreach (var model in previousModels.Concat(_selectedEffectModels).Distinct())
        {
            if (_effectModelLookup.TryGetValue(model, out var effect))
            {
                if (IsEffectBeacon(model))
                {
                    ApplyVerticalBeaconShape(model);
                }
                else
                {
                    model.Rotation = new Vector3(effect.RotationX, effect.RotationY, effect.RotationZ);
                    model.Scale = Vector3.One;
                    ApplyEffectShape(effect, model, GetEffectSize(effect));
                }
            }
            UpdateEffectColor(model);
        }
    }

    public void SetRelatedEffectHighlight(IEnumerable<CollisionEffectViewModel> effects)
    {
        SetRaceEffectHighlights([], effects, []);
    }

    public void SetRaceEffectHighlights(
        IEnumerable<CollisionEffectViewModel> selectedRaceEffects,
        IEnumerable<CollisionEffectViewModel> availableEffects,
        IEnumerable<CollisionEffectViewModel> unavailableEffects)
    {
        ArgumentNullException.ThrowIfNull(selectedRaceEffects);
        ArgumentNullException.ThrowIfNull(availableEffects);
        ArgumentNullException.ThrowIfNull(unavailableEffects);
        var previousModels = _selectedRaceEffectModels
            .Concat(_relatedEffectModels)
            .Concat(_unavailableEffectModels)
            .ToArray();
        _selectedRaceEffectModels.Clear();
        _relatedEffectModels.Clear();
        _unavailableEffectModels.Clear();
        foreach (var effect in selectedRaceEffects)
        {
            if (_effectModels.TryGetValue(effect, out var model))
            {
                _selectedRaceEffectModels.Add(model);
            }
        }
        foreach (var effect in availableEffects)
        {
            if (_effectModels.TryGetValue(effect, out var model))
            {
                _relatedEffectModels.Add(model);
            }
        }
        foreach (var effect in unavailableEffects)
        {
            if (_effectModels.TryGetValue(effect, out var model))
            {
                _unavailableEffectModels.Add(model);
            }
        }

        foreach (var model in previousModels
                     .Concat(_selectedRaceEffectModels)
                     .Concat(_relatedEffectModels)
                     .Concat(_unavailableEffectModels)
                     .Distinct())
        {
            if (_effectModelLookup.TryGetValue(model, out var effect))
            {
                if (IsEffectBeacon(model))
                {
                    ApplyVerticalBeaconShape(model);
                }
                else
                {
                    model.Rotation = new Vector3(effect.RotationX, effect.RotationY, effect.RotationZ);
                    model.Scale = Vector3.One;
                    ApplyEffectShape(effect, model, GetEffectSize(effect));
                }
            }
            UpdateEffectColor(model);
        }
    }

    private bool IsEffectBeacon(Model3D model) =>
        _selectedRaceEffectModels.Contains(model) ||
        _relatedEffectModels.Contains(model) ||
        _unavailableEffectModels.Contains(model);

    public void ClearHover()
    {
        if (_hoveredBlockModel == null)
        {
            return;
        }

        var previous = _hoveredBlockModel;
        _hoveredBlockModel = null;
        UpdateBlockColor(previous);
    }

    public void SetBlockVisible(CollisionBlockViewModel block)
    {
        if (_blockModels.TryGetValue(block, out var model))
        {
            _sceneHost.SetModelVisible(SceneLayer.Collision, model,
                block.IsVisible && (_octocamoCatalog == null || !_referenceBlockModels.Contains(model)));
        }
        if (_selectedBlockModel != null && _blockModels.GetValueOrDefault(block) == _selectedBlockModel)
            ApplyPrimHighlight();
    }

    public void RefreshBlockAppearance(CollisionBlockViewModel block)
    {
        if (!_blockModels.TryGetValue(block, out var model))
        {
            return;
        }

        if (_attributeFilter != null || _octocamoCatalog != null)
        {
            ApplyAttributeFilter(model);
        }
        if (model == _selectedBlockModel && _selectedPrim != null)
        {
            ApplyPrimHighlight();
        }
        else if (_attributeFilter == null && _octocamoCatalog == null)
        {
            UpdateBlockColor(model);
        }
    }

    public void SetAttributeFilter(ulong? requiredFlag)
    {
        if (_attributeFilter == requiredFlag)
        {
            return;
        }

        _attributeFilter = requiredFlag;
        foreach (var model in _blockModelLookup.Keys)
        {
            ApplyAttributeFilter(model);
        }
        ApplyPrimHighlight();
    }

    public void UpdateEffect(CollisionEffectViewModel effect)
    {
        if (_effectModels.TryGetValue(effect, out var model))
        {
            ApplyEffectShape(effect, model, GetEffectSize(effect));
            model.Position = new Vector3(effect.X, effect.Y, effect.Z);
            model.Rotation = new Vector3(effect.RotationX, effect.RotationY, effect.RotationZ);
            model.Scale = Vector3.One;
            _sceneHost.ViewportControl.RequestNextFrameRendering();
        }

        UpdateEffectVisibility();
    }

    public void RebuildEffectModels(IEnumerable<CollisionEffectViewModel> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        _effectModelLookup.Clear();
        _effectModels.Clear();
        _selectedEffectModels.Clear();
        _selectedRaceEffectModels.Clear();
        _relatedEffectModels.Clear();
        _unavailableEffectModels.Clear();
        var models = new List<Model3D>();
        foreach (var effect in TreeTraversal.Flatten(effects, effect => effect.Children))
        {
            var model = CreateEffectMarker(effect);
            models.Add(model);
            _effectModelLookup[model] = effect;
            _effectModels[effect] = model;
        }
        _sceneHost.ReplaceLayer(SceneLayer.Effects, models);
        UpdateEffectVisibility();
    }

    public void UpdateEffectVisibility()
    {
        foreach (var (viewModel, model) in _effectModels)
        {
            _sceneHost.SetModelVisible(SceneLayer.Effects, model, viewModel.IsVisible);
        }
    }

    public void FocusOnBlock(CollisionBlockViewModel block)
    {
        if (_blockModels.TryGetValue(block, out var model))
        {
            FocusOnModel(model);
        }
    }

    public void FocusOnEffect(CollisionEffectViewModel effect)
    {
        if (_effectModels.TryGetValue(effect, out var model))
        {
            FocusOnModel(model);
        }
    }

    public bool TryGetEffectModel(CollisionEffectViewModel effect, out Model3D model)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return _effectModels.TryGetValue(effect, out model!);
    }

    private void BuildTriangleLookups(
        Model3D model,
        CollisionBlockViewModel block,
        int[] primIndexMap,
        int[] polyIndexMap)
    {
        _trianglePrimLookup[model] = primIndexMap;
        var primTriangles = new Dictionary<CollisionPrimViewModel, List<int>>();
        var geoTriangles = new Dictionary<CollisionGeoPrimViewModel, List<int>>();
        var triangleGeo = new CollisionGeoPrimViewModel?[primIndexMap.Length];

        for (var triangle = 0; triangle < primIndexMap.Length; triangle++)
        {
            var primIndex = primIndexMap[triangle];
            if (primIndex < 0 || primIndex >= block.Prims.Count)
            {
                continue;
            }

            var prim = block.Prims[primIndex];
            if (!primTriangles.TryGetValue(prim, out var triangles))
            {
                triangles = new List<int>();
                primTriangles[prim] = triangles;
            }
            triangles.Add(triangle);

            if (triangle >= polyIndexMap.Length)
            {
                continue;
            }

            var polyIndex = polyIndexMap[triangle];
            if (polyIndex < 0 || polyIndex >= prim.Children.Count)
            {
                continue;
            }

            var geoPrim = prim.Children[polyIndex];
            triangleGeo[triangle] = geoPrim;
            if (!geoTriangles.TryGetValue(geoPrim, out var geoList))
            {
                geoList = new List<int>();
                geoTriangles[geoPrim] = geoList;
            }
            geoList.Add(triangle);
        }

        foreach (var (prim, triangles) in primTriangles)
        {
            _primTriangleLookup[prim] = triangles.ToArray();
        }
        foreach (var (geoPrim, triangles) in geoTriangles)
        {
            _geoPrimTriangleLookup[geoPrim] = triangles.ToArray();
        }
        _triangleGeoPrimLookup[model] = triangleGeo;
    }

    private void ApplyAttributeFilter(Model3D model)
    {
        if (!_blockModelLookup.TryGetValue(model, out var block) ||
            !_unfilteredIndices.TryGetValue(model, out var indices) ||
            !_unfilteredTrianglePrimLookup.TryGetValue(model, out var primitiveMap) ||
            !_unfilteredTrianglePolyLookup.TryGetValue(model, out var polygonMap))
        {
            return;
        }

        var primitiveAttributes = block.Prims.Select(prim => prim.Prim.Attribute).ToArray();
        var filtered = GeomSceneBuilder.FilterCollisionTriangles(
            indices,
            primitiveMap,
            polygonMap,
            primitiveAttributes,
            _octocamoCatalog != null ? GeoCollisionAttributes.Player : _attributeFilter);

        model.Indices = filtered.Indices;
        model.IndexCount = filtered.Indices.Length;
        model.IndicesNeedUpdate = true;
        RemoveTriangleLookups(model, block);
        BuildTriangleLookups(model, block, filtered.PrimitiveIndices, filtered.PolygonIndices);
        UpdateBlockColor(model);
    }

    private void RemoveTriangleLookups(Model3D model, CollisionBlockViewModel block)
    {
        _trianglePrimLookup.Remove(model);
        _triangleGeoPrimLookup.Remove(model);
        foreach (var primitive in block.Prims)
        {
            _primTriangleLookup.Remove(primitive);
            foreach (var polygon in primitive.Children)
            {
                _geoPrimTriangleLookup.Remove(polygon);
            }
        }
    }

    private void ApplyPrimHighlight()
    {
        _sceneHost.SetModelVisible(SceneLayer.Collision, _selectedOctocamoPreview, false);
        _selectedOctocamoPreview.Indices = [];
        _selectedOctocamoPreview.IndexCount = 0;
        _selectedOctocamoPreview.IndicesNeedUpdate = true;
        if (_selectedBlockModel == null)
        {
            return;
        }

        if (_selectedPrim == null || !_primTriangleLookup.TryGetValue(_selectedPrim, out var triangles))
        {
            UpdateBlockColor(_selectedBlockModel);
            return;
        }

        if (_selectedGeoPrim != null && _geoPrimTriangleLookup.TryGetValue(_selectedGeoPrim, out var geoTriangles))
        {
            triangles = geoTriangles;
        }

        var model = _selectedBlockModel;
        if (model.Positions.Length == 0 || model.Indices.Length == 0)
        {
            return;
        }

        UpdateBlockColor(model);
        // In OctoCamo view the selected face is the colour being edited. A solid
        // selection tint would conceal every cloth-colour change until deselection.
        if (_octocamoCatalog != null)
        {
            // GEOM can contain independent, coplanar contact polygons in multiple
            // spatial blocks. Later triangles otherwise hide a correctly updated
            // face. Preview ONLY the selected polygon above depth ties, using its
            // real UVs/cloth colours, never a selection tint or a neighbour edit.
            if (_selectedGeoPrim != null && _geoPrimTriangleLookup.TryGetValue(_selectedGeoPrim, out var selectedTriangles) &&
                _blockModelLookup[model].IsVisible &&
                !_referenceBlockModels.Contains(model))
            {
                _selectedOctocamoPreview.Positions = model.Positions;
                _selectedOctocamoPreview.Colors = model.Colors;
                _selectedOctocamoPreview.UVs = model.UVs;
                _selectedOctocamoPreview.Position = model.Position;
                _selectedOctocamoPreview.Rotation = model.Rotation;
                _selectedOctocamoPreview.Scale = model.Scale;
                _selectedOctocamoPreview.VertexCount = model.VertexCount;
                _selectedOctocamoPreview.Indices = selectedTriangles
                    .Where(triangle => triangle >= 0 && triangle * 3 + 2 < model.Indices.Length)
                    .SelectMany(triangle => model.Indices.Skip(triangle * 3).Take(3)).ToArray();
                _selectedOctocamoPreview.IndexCount = _selectedOctocamoPreview.Indices.Length;
                _selectedOctocamoPreview.VerticesNeedUpdate = true;
                _sceneHost.SetModelVisible(SceneLayer.Collision, _selectedOctocamoPreview,
                    _selectedOctocamoPreview.IndexCount > 0);
            }
            return;
        }
        var colors = model.Colors.ToArray();
        foreach (var triangle in triangles)
        {
            var start = triangle * 3;
            if (start + 2 >= model.Indices.Length)
            {
                continue;
            }

            ApplyVertexHighlight(colors, (int)model.Indices[start], PrimHighlightColor);
            ApplyVertexHighlight(colors, (int)model.Indices[start + 1], PrimHighlightColor);
            ApplyVertexHighlight(colors, (int)model.Indices[start + 2], PrimHighlightColor);
        }

        model.Colors = colors;
        model.Alpha = GeomSceneBuilder.CollisionMeshAlpha;
        model.VerticesNeedUpdate = true;
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }

    private void UpdateBlockColor(Model3D? model)
    {
        if (model == null)
        {
            return;
        }

        var useHighlightColor = _octocamoCatalog == null &&
            (model == _hoveredBlockModel || model == _selectedBlockModel && _selectedPrim == null);
        model.Alpha = GeomSceneBuilder.CollisionMeshAlpha;

        if (useHighlightColor)
        {
            var color = model == _hoveredBlockModel ? HoverBlockColor : SelectedBlockColor;
            model.Color = color;
            model.Colors = GeomSceneBuilder.BuildVertexColors(model.Positions, model.Indices, color);
        }
        else if (_blockModelLookup.TryGetValue(model, out var block) &&
                 _trianglePrimLookup.TryGetValue(model, out var primitiveIndices))
        {
            var primitiveAttributes = block.Prims.Select(prim => prim.Prim.Attribute).ToArray();
            model.Color = _octocamoCatalog == null ? DefaultBlockColor : Vector3.One;
            if (_octocamoCatalog != null && _triangleGeoPrimLookup.TryGetValue(model, out var polygonMap))
            {
                var polygonIndices = BuildPolygonIndices(block, primitiveIndices, polygonMap);
                var faces = block.Prims.Select(prim => prim.Prim).ToArray();
                if (_octocamoCatalog.PreviewMusclePatterns && _octocamoCatalog.PatternAtlas != null)
                {
                    model.Colors = new float[model.Positions.Length / 3 * 4];
                    Array.Fill(model.Colors, 1f);
                    model.UVs = GeomSceneBuilder.BuildOctocamoTextureUvs(model.Positions, model.Indices,
                        primitiveIndices, polygonIndices, faces, block.Block, _octocamoCatalog);
                }
                else model.Colors = GeomSceneBuilder.BuildOctocamoVertexColors(model.Positions, model.Indices,
                    primitiveIndices, polygonIndices, faces, block.Block, _octocamoCatalog);
            }
            else model.Colors = GeomSceneBuilder.BuildCollisionVertexColors(
                model.Positions, model.Indices, primitiveIndices, primitiveAttributes);
        }

        model.VerticesNeedUpdate = true;
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }

    private void UpdateEffectColor(Model3D? model)
    {
        if (model == null)
        {
            return;
        }

        var isSelected = _selectedEffectModels.Contains(model);
        model.Color = isSelected
            ? SelectedEffectColor
            : _relatedEffectModels.Contains(model)
                ? RelatedEffectColor
                : EffectColor;
        model.Alpha = 1.0f;
        model.RenderAfterTransparent = true;
        model.VerticesNeedUpdate = true;
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }

    private static int[] BuildPolygonIndices(
        CollisionBlockViewModel block, int[] primitiveIndices,
        CollisionGeoPrimViewModel?[] polygons)
    {
        var result = new int[primitiveIndices.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var primIndex = primitiveIndices[i];
            result[i] = primIndex >= 0 && primIndex < block.Prims.Count && i < polygons.Length
                ? block.Prims[primIndex].Children.IndexOf(polygons[i]!) : -1;
        }
        return result;
    }

    private static void ApplyVertexHighlight(float[] colors, int vertexIndex, Vector3 color)
    {
        var destination = vertexIndex * 4;
        if (destination + 3 >= colors.Length)
        {
            return;
        }

        colors[destination] = color.X;
        colors[destination + 1] = color.Y;
        colors[destination + 2] = color.Z;
        colors[destination + 3] = 1.0f;
    }

    private static Model3D CreateEffectMarker(CollisionEffectViewModel effect)
    {
        var model = new Model3D
        {
            Name = $"Effect_{effect.IndexText}",
            Color = EffectColor,
            Alpha = 1.0f,
            MaterialIndex = -1,
            Position = new Vector3(effect.X, effect.Y, effect.Z),
            Rotation = new Vector3(effect.RotationX, effect.RotationY, effect.RotationZ),
            Scale = Vector3.One,
            RenderAfterTransparent = true
        };
        ApplyEffectShape(effect, model, GetEffectSize(effect));
        return model;
    }

    private static void ApplyEffectShape(CollisionEffectViewModel effect, Model3D model, float size)
    {
        float[] positions;
        uint[] indices;
        if (effect.RenderAsFlag)
        {
            positions =
            [
                0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.8f, 0.75f, 0.0f,
                0.0f, 0.5f, 0.0f,
                0.8f, 0.25f, 0.0f
            ];
            indices = [0, 1, 2, 0, 2, 3, 0, 3, 4];
        }
        else
        {
            // The cube's arrowhead points along local +Z. Because the complete
            // marker uses the effect transform, it turns with spawn points and
            // other directional effects without changing their stored data.
            positions =
            [
                -0.5f, -0.5f, -0.5f, 0.5f, -0.5f, -0.5f,
                 0.5f,  0.5f, -0.5f, -0.5f, 0.5f, -0.5f,
                -0.5f, -0.5f,  0.5f, 0.5f, -0.5f, 0.5f,
                 0.5f,  0.5f,  0.5f, -0.5f, 0.5f, 0.5f,

                // Triangular direction wedge on the +Z face (bottom, then top).
                -0.28f, -0.35f, 0.5f, 0.28f, -0.35f, 0.5f, 0.0f, -0.35f, 0.92f,
                -0.28f,  0.35f, 0.5f, 0.28f,  0.35f, 0.5f, 0.0f,  0.35f, 0.92f
            ];
            indices =
            [
                0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4,
                0, 1, 5, 5, 4, 0, 2, 3, 7, 7, 6, 2,
                0, 3, 7, 7, 4, 0, 1, 2, 6, 6, 5, 1,

                8, 10, 9, 11, 12, 13,
                8, 9, 12, 12, 11, 8,
                9, 10, 13, 13, 12, 9,
                10, 8, 11, 11, 13, 10
            ];
        }

        for (var index = 0; index < positions.Length; index++)
        {
            positions[index] *= size;
        }
        model.Positions = positions;
        model.Indices = indices;
        model.VertexCount = positions.Length / 3;
        model.IndexCount = indices.Length;
        model.VerticesNeedUpdate = true;
        model.IndicesNeedUpdate = true;
    }

    private static void ApplyVerticalBeaconShape(Model3D model)
    {
        const float halfWidth = 125.0f;
        const float height = 20000.0f;
        model.Positions =
        [
            -halfWidth, 0, -halfWidth,  halfWidth, 0, -halfWidth,
             halfWidth, height, -halfWidth, -halfWidth, height, -halfWidth,
            -halfWidth, 0,  halfWidth,  halfWidth, 0,  halfWidth,
             halfWidth, height,  halfWidth, -halfWidth, height,  halfWidth
        ];
        model.Indices =
        [
            0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4,
            0, 1, 5, 5, 4, 0, 2, 3, 7, 7, 6, 2,
            0, 3, 7, 7, 4, 0, 1, 2, 6, 6, 5, 1
        ];
        model.VertexCount = 8;
        model.IndexCount = model.Indices.Length;
        model.Rotation = Vector3.Zero;
        model.Scale = Vector3.One;
        model.VerticesNeedUpdate = true;
        model.IndicesNeedUpdate = true;
    }

    private static float GetEffectSize(CollisionEffectViewModel effect)
    {
        var scale = MathF.Abs(effect.W);
        scale = scale <= 0.001f ? 1.0f : scale;
        return MathF.Max(scale, 1.0f) * EffectSizeMultiplier;
    }

    private void FocusCamera(IReadOnlyCollection<Model3D> models)
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
        FocusOnBounds(min, max);
    }

    private void FocusOnModel(Model3D model)
    {
        var bounds = model.GetBoundingBox();
        FocusOnBounds(bounds.Min, bounds.Max);
    }

    private void FocusOnBounds(Vector3 min, Vector3 max)
    {
        var center = (min + max) * 0.5f;
        var size = max - min;
        var radius = MathF.Max(size.X, MathF.Max(size.Y, size.Z)) * 0.5f;
        _sceneHost.ViewportControl.FocusOnBounds(center, radius <= 0.001f ? 1.0f : radius, 1.5f);
    }

    private void ClearLookups()
    {
        _sceneHost.SetModelVisible(SceneLayer.Collision, _selectedOctocamoPreview, false);
        _selectedOctocamoPreview.Visible = false;
        _selectedOctocamoPreview.Positions = [];
        _selectedOctocamoPreview.Colors = [];
        _selectedOctocamoPreview.UVs = [];
        _selectedOctocamoPreview.Indices = [];
        _selectedOctocamoPreview.VertexCount = 0;
        _selectedOctocamoPreview.IndexCount = 0;
        _blockModelLookup.Clear();
        _blockModels.Clear();
        _referenceBlockModels.Clear();
        _effectModelLookup.Clear();
        _effectModels.Clear();
        _trianglePrimLookup.Clear();
        _primTriangleLookup.Clear();
        _geoPrimTriangleLookup.Clear();
        _triangleGeoPrimLookup.Clear();
        _unfilteredIndices.Clear();
        _unfilteredTrianglePrimLookup.Clear();
        _unfilteredTrianglePolyLookup.Clear();
        _hoveredBlockModel = null;
        _selectedBlockModel = null;
        _selectedEffectModels.Clear();
        _selectedRaceEffectModels.Clear();
        _relatedEffectModels.Clear();
        _unavailableEffectModels.Clear();
        _selectedPrim = null;
        _selectedGeoPrim = null;
    }

}
