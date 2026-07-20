using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia3DControl;
using Avalonia3DControl.Core.Models;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public enum MapDragAxis
{
    None,
    X,
    Y,
    Z
}

public sealed class MapManipulationTarget
{
    public MapManipulationTarget(object entity, Vector3 position, IEnumerable<Model3D> models)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        Position = position;
        Models = models?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(models));
    }

    public object Entity { get; }
    public Vector3 Position { get; }
    public IReadOnlyList<Model3D> Models { get; }
}

public readonly record struct MapDragUpdate(
    MapManipulationTarget Target,
    Vector3 Position,
    bool Started);

public readonly record struct MapPointerCompletion(
    bool IsClick,
    MapManipulationTarget? Target,
    Vector3 StartPosition,
    Vector3 EndPosition)
{
    public bool IsDrag => Target != null && !IsClick;
}

public sealed class MapManipulationController
{
    public const double DragThreshold = 4.0;

    private readonly SceneHost _sceneHost;
    private Point _pressPoint;
    private MapManipulationTarget? _target;
    private Vector3 _lastPosition;
    private bool _pointerPressed;
    private bool _dragging;
    private bool _movedBeyondThreshold;
    private MapDragAxis _axisConstraint;

    public MapManipulationController(SceneHost sceneHost)
    {
        _sceneHost = sceneHost ?? throw new ArgumentNullException(nameof(sceneHost));
    }

    public bool IsPointerPressed => _pointerPressed;
    public bool IsDragging => _dragging;
    public MapDragAxis AxisConstraint => _axisConstraint;

    public void PointerPressed(Point point, MapManipulationTarget? target)
    {
        Cancel();
        _pressPoint = point;
        _target = target;
        _lastPosition = target?.Position ?? Vector3.Zero;
        _pointerPressed = true;
        _movedBeyondThreshold = false;
    }

    public bool TryUpdate(
        Point point,
        OpenGL3DControl control,
        bool heightOnly,
        out MapDragUpdate update)
    {
        update = default;
        if (!_pointerPressed)
        {
            return false;
        }

        var deltaX = point.X - _pressPoint.X;
        var deltaY = point.Y - _pressPoint.Y;
        var renderScaling = TopLevel.GetTopLevel(control)?.RenderScaling ?? 1.0;
        _movedBeyondThreshold |=
            (deltaX * deltaX + deltaY * deltaY) * renderScaling * renderScaling >=
            DragThreshold * DragThreshold;
        if (!_movedBeyondThreshold || _target == null)
        {
            return false;
        }

        if (!TryCalculatePosition(point, control, heightOnly, out var position))
        {
            return false;
        }

        var started = !_dragging;
        _dragging = true;
        _lastPosition = position;
        PreviewPosition(_target, position);
        update = new MapDragUpdate(_target, position, started);
        return true;
    }

    public MapPointerCompletion PointerReleased()
    {
        if (!_pointerPressed)
        {
            return default;
        }

        var result = _dragging && _target != null
            ? new MapPointerCompletion(false, _target, _target.Position, _lastPosition)
            : !_movedBeyondThreshold
                ? new MapPointerCompletion(true, _target, _target?.Position ?? Vector3.Zero, _lastPosition)
                : default;
        _pointerPressed = false;
        _dragging = false;
        _movedBeyondThreshold = false;
        _target = null;
        return result;
    }

    public void SetAxisConstraint(MapDragAxis axis, bool enabled)
    {
        if (axis == MapDragAxis.None)
        {
            _axisConstraint = MapDragAxis.None;
            return;
        }

        if (enabled)
        {
            _axisConstraint = axis;
        }
        else if (_axisConstraint == axis)
        {
            _axisConstraint = MapDragAxis.None;
        }
    }

    public void Cancel()
    {
        if (_dragging && _target != null)
        {
            PreviewPosition(_target, _target.Position);
        }

        _pointerPressed = false;
        _dragging = false;
        _movedBeyondThreshold = false;
        _target = null;
    }

    public void PreviewPosition(MapManipulationTarget target, Vector3 position)
    {
        foreach (var model in target.Models)
        {
            model.Position = position;
        }

        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }

    public static bool TryCalculatePlaneDrag(
        Vector3 initialPosition,
        Vector3 startRayOrigin,
        Vector3 startRayDirection,
        Vector3 currentRayOrigin,
        Vector3 currentRayDirection,
        Vector3 planeNormal,
        out Vector3 position)
    {
        position = initialPosition;
        if (!TryIntersectPlane(startRayOrigin, startRayDirection, initialPosition, planeNormal, out var start) ||
            !TryIntersectPlane(currentRayOrigin, currentRayDirection, initialPosition, planeNormal, out var current))
        {
            return false;
        }

        position = initialPosition + current - start;
        return IsFinite(position);
    }

    public static bool TryCalculateAxisDrag(
        Vector3 initialPosition,
        Vector3 startRayOrigin,
        Vector3 startRayDirection,
        Vector3 currentRayOrigin,
        Vector3 currentRayDirection,
        Vector3 axis,
        Vector3 fallbackPlaneNormal,
        out Vector3 position)
    {
        position = initialPosition;
        if (axis.LengthSquared < 0.000001f)
        {
            return false;
        }

        axis = Vector3.Normalize(axis);
        if (TryGetClosestAxisParameter(startRayOrigin, startRayDirection, initialPosition, axis, out var start) &&
            TryGetClosestAxisParameter(currentRayOrigin, currentRayDirection, initialPosition, axis, out var current))
        {
            position = initialPosition + axis * (current - start);
            return IsFinite(position);
        }

        if (!TryCalculatePlaneDrag(
                initialPosition,
                startRayOrigin,
                startRayDirection,
                currentRayOrigin,
                currentRayDirection,
                fallbackPlaneNormal,
                out var planePosition))
        {
            return false;
        }

        position = initialPosition + axis * Vector3.Dot(planePosition - initialPosition, axis);
        return IsFinite(position);
    }

    private bool TryCalculatePosition(
        Point point,
        OpenGL3DControl control,
        bool heightOnly,
        out Vector3 position)
    {
        position = _target!.Position;
        if (!SelectionRaycaster.TryGetPickRay(_pressPoint, control, out var startOrigin, out var startDirection) ||
            !SelectionRaycaster.TryGetPickRay(point, control, out var currentOrigin, out var currentDirection))
        {
            return false;
        }

        var camera = control.Scene.Camera;
        var cameraNormal = camera.Target - camera.Position;
        if (cameraNormal.LengthSquared < 0.000001f)
        {
            cameraNormal = -Vector3.UnitZ;
        }
        else
        {
            cameraNormal = Vector3.Normalize(cameraNormal);
        }

        var effectiveAxisConstraint = heightOnly ? MapDragAxis.Y : _axisConstraint;
        var axis = effectiveAxisConstraint switch
        {
            MapDragAxis.X => Vector3.UnitX,
            MapDragAxis.Y => Vector3.UnitY,
            MapDragAxis.Z => Vector3.UnitZ,
            _ => Vector3.Zero
        };
        if (effectiveAxisConstraint != MapDragAxis.None)
        {
            return TryCalculateAxisDrag(
                _target.Position,
                startOrigin,
                startDirection,
                currentOrigin,
                currentDirection,
                axis,
                cameraNormal,
                out position);
        }

        return TryCalculatePlaneDrag(
            _target.Position,
            startOrigin,
            startDirection,
            currentOrigin,
            currentDirection,
            Vector3.UnitY,
            out position);
    }

    private static bool TryIntersectPlane(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 planePoint,
        Vector3 planeNormal,
        out Vector3 intersection)
    {
        intersection = Vector3.Zero;
        var denominator = Vector3.Dot(rayDirection, planeNormal);
        if (MathF.Abs(denominator) < 0.000001f)
        {
            return false;
        }

        var distance = Vector3.Dot(planePoint - rayOrigin, planeNormal) / denominator;
        if (!float.IsFinite(distance))
        {
            return false;
        }

        intersection = rayOrigin + rayDirection * distance;
        return IsFinite(intersection);
    }

    private static bool TryGetClosestAxisParameter(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 axisOrigin,
        Vector3 axisDirection,
        out float parameter)
    {
        parameter = 0;
        var rayLengthSquared = rayDirection.LengthSquared;
        if (rayLengthSquared < 0.000001f)
        {
            return false;
        }

        rayDirection = Vector3.Normalize(rayDirection);
        var difference = rayOrigin - axisOrigin;
        var dot = Vector3.Dot(rayDirection, axisDirection);
        var denominator = 1.0f - dot * dot;
        if (MathF.Abs(denominator) < 0.000001f)
        {
            return false;
        }

        var rayProjection = Vector3.Dot(rayDirection, difference);
        var axisProjection = Vector3.Dot(axisDirection, difference);
        parameter = (axisProjection - dot * rayProjection) / denominator;
        return float.IsFinite(parameter);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}
