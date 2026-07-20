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
    private static readonly Vector3 SelectedEffectColor = new(1.0f, 0.35f, 0.35f);

    private readonly SceneHost _sceneHost;
    private readonly Dictionary<Model3D, CollisionBlockViewModel> _blockModelLookup = new();
    private readonly Dictionary<CollisionBlockViewModel, Model3D> _blockModels = new();
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
    private Model3D? _selectedEffectModel;
    private CollisionPrimViewModel? _selectedPrim;
    private CollisionGeoPrimViewModel? _selectedGeoPrim;
    private ulong? _attributeFilter;

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
            model.Visible = blockView.IsVisible;
            if (trianglePrimIndex.TryGetValue(model, out var primitiveMap) &&
                trianglePolyIndex.TryGetValue(model, out var polygonMap))
            {
                _unfilteredIndices[model] = model.Indices.ToArray();
                _unfilteredTrianglePrimLookup[model] = primitiveMap.ToArray();
                _unfilteredTrianglePolyLookup[model] = polygonMap.ToArray();
                BuildTriangleLookups(model, blockView, primitiveMap, polygonMap);
            }
        }

        if (_attributeFilter != null)
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

        _sceneHost.ReplaceLayer(SceneLayer.Collision, blockModels);
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

    public void SetSelection(
        CollisionBlockViewModel? block,
        CollisionPrimViewModel? prim,
        CollisionGeoPrimViewModel? geoPrim,
        CollisionEffectViewModel? effect)
    {
        var previousBlockModel = _selectedBlockModel;
        var previousEffectModel = _selectedEffectModel;
        _selectedBlockModel = block != null && _blockModels.TryGetValue(block, out var blockModel) ? blockModel : null;
        _selectedEffectModel = effect != null && _effectModels.TryGetValue(effect, out var effectModel) ? effectModel : null;
        _selectedPrim = prim;
        _selectedGeoPrim = geoPrim;

        if (previousBlockModel != null)
        {
            UpdateBlockColor(previousBlockModel);
        }

        UpdateBlockColor(_selectedBlockModel);
        UpdateEffectColor(previousEffectModel);
        UpdateEffectColor(_selectedEffectModel);
        ApplyPrimHighlight();
    }

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
            _sceneHost.SetModelVisible(SceneLayer.Collision, model, block.IsVisible);
        }
    }

    public void RefreshBlockAppearance(CollisionBlockViewModel block)
    {
        if (!_blockModels.TryGetValue(block, out var model))
        {
            return;
        }

        if (_attributeFilter != null)
        {
            ApplyAttributeFilter(model);
        }
        if (model == _selectedBlockModel && _selectedPrim != null)
        {
            ApplyPrimHighlight();
        }
        else if (_attributeFilter == null)
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
        _selectedEffectModel = null;
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
            _attributeFilter);

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

        var useHighlightColor = model == _hoveredBlockModel ||
            model == _selectedBlockModel && _selectedPrim == null;
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
            model.Color = DefaultBlockColor;
            model.Colors = GeomSceneBuilder.BuildCollisionVertexColors(
                model.Positions,
                model.Indices,
                primitiveIndices,
                primitiveAttributes);
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

        model.Color = model == _selectedEffectModel ? SelectedEffectColor : EffectColor;
        model.VerticesNeedUpdate = true;
        _sceneHost.ViewportControl.RequestNextFrameRendering();
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
            Alpha = 0.85f,
            MaterialIndex = -1,
            Position = new Vector3(effect.X, effect.Y, effect.Z),
            Rotation = new Vector3(effect.RotationX, effect.RotationY, effect.RotationZ),
            Scale = Vector3.One
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
            positions =
            [
                -0.5f, -0.5f, -0.5f, 0.5f, -0.5f, -0.5f,
                 0.5f,  0.5f, -0.5f, -0.5f, 0.5f, -0.5f,
                -0.5f, -0.5f,  0.5f, 0.5f, -0.5f, 0.5f,
                 0.5f,  0.5f,  0.5f, -0.5f, 0.5f, 0.5f
            ];
            indices =
            [
                0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4,
                0, 1, 5, 5, 4, 0, 2, 3, 7, 7, 6, 2,
                0, 3, 7, 7, 4, 0, 1, 2, 6, 6, 5, 1
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
        _blockModelLookup.Clear();
        _blockModels.Clear();
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
        _selectedEffectModel = null;
        _selectedPrim = null;
        _selectedGeoPrim = null;
    }

}
