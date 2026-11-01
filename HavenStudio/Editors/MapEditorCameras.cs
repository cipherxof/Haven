using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia3DControl.Core.Models;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Rendering;
using OpenTK.Mathematics;

namespace HavenStudio.Editors;

public sealed partial class MapEditorViewModel
{
    private readonly MapOutlineGroup _camerasGroup = new("Spectator cameras");
    private readonly Dictionary<Model3D,SpectatorCameraEntity> _cameraByModel = [];
    public bool CamerasVisible
    {
        get => _sceneHost.IsLayerVisible(SceneLayer.Cameras);
        set => SetLayerVisible(SceneLayer.Cameras,value);
    }
    private void RefreshCameras()
    {
        if (_selectedEntity is SpectatorCameraEntity) ClearSelection();
        _cameraByModel.Clear();
        foreach (var reference in _gcxEditor.GetSpectatorCameras())
        {
            var entity = new SpectatorCameraEntity(reference,EditCamera);
            _cameraByModel[entity.Marker] = entity;
        }
        ReplaceChildren(_camerasGroup,_cameraByModel.Values.Cast<object>());
        _sceneHost.ReplaceLayer(SceneLayer.Cameras,_cameraByModel.Keys.ToArray());
    }
    private void SelectCamera(SpectatorCameraEntity camera)
    {
        _collisionEditor.ClearSelection();
        SetSelectedEntity(camera,camera);
    }
    private void ApplyCameraBytes(SpectatorCameraEntity camera, byte[] bytes)
    {
        _gcxEditor.ApplyCameraScript(camera.Source,bytes);
        foreach (var entity in _cameraByModel.Values.Where(c => ReferenceEquals(c.Source.Script,camera.Source.Script)))
            entity.Refresh();
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }
    private void ApplyCameraTransform(SpectatorCameraEntity camera, Vector3 eye, Vector3 target) =>
        ApplyCameraBytes(camera,GcxCameraWriter.Write(camera.Source.Script.Bytes,camera.Source.TableIndex,camera.Source.RowIndex,eye,target));

    private string? EditCamera(SpectatorCameraEntity camera, Vector3 eye, Vector3 target)
    {
        try
        {
            var original = camera.Source.Script.Bytes.ToArray();
            var updated = GcxCameraWriter.Write(original,camera.Source.TableIndex,camera.Source.RowIndex,eye,target);
            if (!original.AsSpan().SequenceEqual(updated))
                _history.Execute($"edit {camera.DisplayName}", () => ApplyCameraBytes(camera,updated), () => ApplyCameraBytes(camera,original));
            return null;
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or OverflowException)
        { return e.Message; }
    }
    public string? SetSelectedCameraFromView()
    {
        if (_selectedEntity is not SpectatorCameraEntity camera) return "Select a spectator camera first.";
        var view = _sceneHost.Scene.Camera;
        var direction = view.Target-view.Position;
        if (direction.LengthSquared < .0001f) return "The current view has no usable direction.";
        // The editor uses a one-unit look-at vector, whereas GCX rounds to integers.
        // Retain a long target distance to avoid destroying the viewing angle.
        var target = view.Position + direction.Normalized()*MathF.Max(1000,(camera.Target-camera.Position).Length);
        return EditCamera(camera,view.Position,target);
    }
    public void ViewSelectedCamera()
    {
        if (_selectedEntity is not SpectatorCameraEntity camera) return;
        if ((camera.Target-camera.Position).LengthSquared < .0001f) return;
        _sceneHost.ViewportControl.EditorCamera?.SetPosition(camera.Position);
        _sceneHost.ViewportControl.EditorCamera?.LookAt(camera.Target);
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }
}
