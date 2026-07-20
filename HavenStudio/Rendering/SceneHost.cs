using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia3DControl;
using Avalonia3DControl.Core;
using Avalonia3DControl.Core.Cameras;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Materials;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Formats.Mdn;
using HavenStudio.Services.Workspace;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public enum SceneLayer
{
    VisualModels,
    Collision,
    Effects,
    Lights,
    Grid,
    Overlay
}

public sealed class SceneHost
{
    private readonly Dictionary<SceneLayer, List<Model3D>> _layers =
        Enum.GetValues<SceneLayer>().ToDictionary(layer => layer, _ => new List<Model3D>());
    private readonly Dictionary<SceneLayer, bool> _layerVisibility =
        Enum.GetValues<SceneLayer>().ToDictionary(
            layer => layer,
            layer => layer is not SceneLayer.Collision and not SceneLayer.Lights);
    private readonly Dictionary<Model3D, bool> _modelVisibility = new();
    private readonly Dictionary<Model3D, RenderMode?> _modelRenderModes = new();
    private readonly Dictionary<SceneLayer, RenderMode?> _layerRenderModes =
        Enum.GetValues<SceneLayer>().ToDictionary(layer => layer, _ => (RenderMode?)null);
    private readonly Dictionary<Model3D, PlacedModelReference> _placementByModel = new();
    private bool _stageModelsVisible = true;
    private bool _placementsVisible = true;

    public OpenGL3DControl ViewportControl { get; } = new();
    public Scene3D Scene => ViewportControl.Scene;

    public event Action<SceneLayer>? LayerChanged;

    public SceneHost()
    {
        ViewportControl.CameraMode = CameraMode.Editor;
        ViewportControl.CameraSpeedScale = 10f;
    }

    public void AddModel(string modelType)
    {
        var model = Scene.CreateModel(modelType);
        if (model is null)
        {
            return;
        }

        var models = _layers[SceneLayer.VisualModels].ToList();
        models.Add(model);
        ReplaceLayer(SceneLayer.VisualModels, models);
    }

    public void ClearModels()
    {
        foreach (var layer in Enum.GetValues<SceneLayer>())
        {
            ClearLayer(layer);
        }
    }

    public void ReplaceLayer(SceneLayer layer, IEnumerable<Model3D> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        var replacement = models.Distinct().ToList();
        var current = _layers[layer];
        var desiredVisibility = replacement.ToDictionary(
            model => model,
            model => _modelVisibility.GetValueOrDefault(model, model.Visible));
        var desiredRenderModes = replacement.ToDictionary(
            model => model,
            model => _modelRenderModes.GetValueOrDefault(model, model.RenderModeOverride));
        var retainedPlacements = layer == SceneLayer.VisualModels
            ? replacement
                .Where(model => _placementByModel.ContainsKey(model))
                .ToDictionary(model => model, model => _placementByModel[model])
            : new Dictionary<Model3D, PlacedModelReference>();

        foreach (var model in current)
        {
            Scene.Models.Remove(model);
            _modelVisibility.Remove(model);
            _modelRenderModes.Remove(model);
            if (layer == SceneLayer.VisualModels)
            {
                _placementByModel.Remove(model);
            }
        }

        current.Clear();
        foreach (var model in replacement)
        {
            current.Add(model);
            _modelVisibility[model] = desiredVisibility[model];
            _modelRenderModes[model] = desiredRenderModes[model];
            if (layer == SceneLayer.VisualModels && retainedPlacements.TryGetValue(model, out var placement))
            {
                _placementByModel[model] = placement;
            }
            ApplyModelVisibility(layer, model);
            ApplyModelRenderMode(layer, model);
            if (!Scene.Models.Contains(model))
            {
                Scene.Models.Add(model);
            }
        }

        ViewportControl.RequestNextFrameRendering();
        LayerChanged?.Invoke(layer);
    }

    public void ClearLayer(SceneLayer layer)
    {
        ReplaceLayer(layer, Array.Empty<Model3D>());
    }

    public void SetLayerVisible(SceneLayer layer, bool visible)
    {
        if (_layerVisibility[layer] == visible)
        {
            return;
        }

        _layerVisibility[layer] = visible;
        foreach (var model in _layers[layer])
        {
            ApplyModelVisibility(layer, model);
            model.VerticesNeedUpdate = true;
        }

        ViewportControl.RequestNextFrameRendering();
        LayerChanged?.Invoke(layer);
    }

    public bool IsLayerVisible(SceneLayer layer) => _layerVisibility[layer];

    public void SetLayerRenderMode(SceneLayer layer, RenderMode? renderMode)
    {
        if (_layerRenderModes[layer] == renderMode)
        {
            return;
        }

        _layerRenderModes[layer] = renderMode;
        foreach (var model in _layers[layer])
        {
            ApplyModelRenderMode(layer, model);
        }
        ViewportControl.RequestNextFrameRendering();
    }

    public bool StageModelsVisible => _stageModelsVisible;

    public bool PlacementsVisible => _placementsVisible;

    public void SetStageModelsVisible(bool visible)
    {
        if (_stageModelsVisible == visible)
        {
            return;
        }

        _stageModelsVisible = visible;
        foreach (var model in _layers[SceneLayer.VisualModels].Where(model => !_placementByModel.ContainsKey(model)))
        {
            ApplyModelVisibility(SceneLayer.VisualModels, model);
        }
        ViewportControl.RequestNextFrameRendering();
    }

    public void SetPlacementsVisible(bool visible)
    {
        if (_placementsVisible == visible)
        {
            return;
        }

        _placementsVisible = visible;
        foreach (var model in _placementByModel.Keys)
        {
            ApplyModelVisibility(SceneLayer.VisualModels, model);
        }
        ViewportControl.RequestNextFrameRendering();
    }

    public IReadOnlyList<Model3D> GetLayerModels(SceneLayer layer) => _layers[layer];

    public void SetModelVisible(SceneLayer layer, Model3D model, bool visible)
    {
        if (!_layers[layer].Contains(model))
        {
            return;
        }

        _modelVisibility[model] = visible;
        ApplyModelVisibility(layer, model);
        model.VerticesNeedUpdate = true;
        ViewportControl.RequestNextFrameRendering();
    }

    public bool TryGetPlacement(Model3D model, out PlacedModelReference placement)
    {
        return _placementByModel.TryGetValue(model, out placement!);
    }

    public IReadOnlyList<PlacedModelReference> GetPlacements()
    {
        return _placementByModel.Values.Distinct().ToArray();
    }

    public void LoadMdn(Mdn mdn, IWorkspaceCatalog workspace)
    {
        var models = MdnSceneRenderer.BuildModels(mdn);
        ReplaceLayer(SceneLayer.VisualModels, models);
        FocusCameraOnMdn(mdn);
        MdnSceneRenderer.ApplyTextures(ViewportControl, mdn, models, MdnSceneRenderer.ResolveTextures(mdn, workspace));
        ViewportControl.RequestNextFrameRendering();
    }

    public void AppendMdn(Mdn mdn, IWorkspaceCatalog workspace)
    {
        var appended = MdnSceneRenderer.BuildModels(mdn);
        var models = _layers[SceneLayer.VisualModels].Concat(appended).ToList();
        ReplaceLayer(SceneLayer.VisualModels, models);
        MdnSceneRenderer.ApplyTextures(ViewportControl, mdn, appended, MdnSceneRenderer.ResolveTextures(mdn, workspace));
        ViewportControl.RequestNextFrameRendering();
    }

    public void ReplaceModels(IReadOnlyList<MdnSceneBatch> batches)
    {
        ArgumentNullException.ThrowIfNull(batches);
        var models = batches.SelectMany(batch => batch.Models).ToList();
        ReplaceLayer(SceneLayer.VisualModels, models);
        _placementByModel.Clear();

        foreach (var batch in batches)
        {
            if (batch.Placement != null)
            {
                foreach (var model in batch.Models)
                {
                    _placementByModel[model] = batch.Placement;
                }
            }

            MdnSceneRenderer.ApplyTextures(ViewportControl, batch.Document, batch.Models, batch.Textures);
        }

        foreach (var model in models)
        {
            ApplyModelVisibility(SceneLayer.VisualModels, model);
        }

        LayerChanged?.Invoke(SceneLayer.VisualModels);
        ViewportControl.RequestNextFrameRendering();
    }

    public void ReplacePlacementModels(
        IEnumerable<PlacedModelReference> placements,
        IReadOnlyList<MdnSceneBatch> batches)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(batches);
        var targets = placements.ToHashSet();
        if (targets.Count == 0)
        {
            return;
        }
        if (batches.Any(batch => batch.Placement == null || !targets.Contains(batch.Placement)))
        {
            throw new ArgumentException(
                "Targeted placement batches must belong to one of the replaced placements.",
                nameof(batches));
        }

        var models = _layers[SceneLayer.VisualModels];
        for (var index = models.Count - 1; index >= 0; index--)
        {
            var model = models[index];
            if (!_placementByModel.TryGetValue(model, out var placement) || !targets.Contains(placement))
            {
                continue;
            }

            models.RemoveAt(index);
            Scene.Models.Remove(model);
            _modelVisibility.Remove(model);
            _modelRenderModes.Remove(model);
            _placementByModel.Remove(model);
        }

        foreach (var batch in batches)
        {
            foreach (var model in batch.Models)
            {
                if (models.Contains(model))
                {
                    continue;
                }

                models.Add(model);
                _modelVisibility[model] = model.Visible;
                _modelRenderModes[model] = model.RenderModeOverride;
                Scene.Models.Add(model);
                _placementByModel[model] = batch.Placement!;
                ApplyModelVisibility(SceneLayer.VisualModels, model);
                ApplyModelRenderMode(SceneLayer.VisualModels, model);
            }

            MdnSceneRenderer.ApplyTextures(ViewportControl, batch.Document, batch.Models, batch.Textures);
        }

        ViewportControl.RequestNextFrameRendering();
        LayerChanged?.Invoke(SceneLayer.VisualModels);
    }

    private void ApplyModelVisibility(SceneLayer layer, Model3D model)
    {
        var categoryVisible = true;
        if (layer == SceneLayer.VisualModels)
        {
            categoryVisible = _placementByModel.ContainsKey(model)
                ? _placementsVisible
                : _stageModelsVisible;
        }

        model.Visible = _layerVisibility[layer] &&
            _modelVisibility.GetValueOrDefault(model, true) &&
            categoryVisible;
    }

    private void ApplyModelRenderMode(SceneLayer layer, Model3D model)
    {
        model.RenderModeOverride = _layerRenderModes[layer] ??
            _modelRenderModes.GetValueOrDefault(model);
    }

    private void FocusCameraOnMdn(Mdn mdn)
    {
        var bounds = mdn.Bounds;
        if (bounds is null)
        {
            return;
        }

        var min = new Vector3(bounds.MinX, bounds.MinY, bounds.MinZ);
        var max = new Vector3(bounds.MaxX, bounds.MaxY, bounds.MaxZ);
        var center = (min + max) * 0.5f;
        var size = max - min;
        var radius = Math.Max(size.X, Math.Max(size.Y, size.Z)) * 0.5f;
        if (radius <= 0.001f)
        {
            radius = 1.0f;
        }

        ViewportControl.FocusOnBounds(center, radius, 1.5f);
    }
}
