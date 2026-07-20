using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Avalonia3DControl.Core.Models;

namespace Avalonia3DControl.Core.Cameras
{
    public class EditorCameraController
    {
        private const float PITCH_LIMIT = MathHelper.PiOver2 - 0.001f;

        private float _rotationSensitivity = 0.003f;
        private float _panSensitivity = 1.0f;
        private float _flySensitivity = 500.0f;
        private float _zoomSensitivity = 2000.0f;

        private Vector3 _position = new Vector3(0f, 5000f, 5000f);
        private float _yaw = MathHelper.Pi;
        private float _pitch = -0.4f;

        private readonly Scene3D _scene;

        public event Action? CameraChanged;

        public EditorCameraController(Scene3D scene)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        }

        public Vector3 Position => _position;
        public float Yaw => _yaw;
        public float Pitch => _pitch;

        public void SetSpeedScale(float scale)
        {
            if (scale <= 0) return;
            _panSensitivity = 1.0f * scale;
            _flySensitivity = 500.0f * scale;
            _zoomSensitivity = 2000.0f * scale;
        }

        public void HandleRotation(float deltaX, float deltaY)
        {
            _yaw += deltaX * _rotationSensitivity;
            _pitch += deltaY * _rotationSensitivity;
            _pitch = Math.Clamp(_pitch, -PITCH_LIMIT, PITCH_LIMIT);
            OnCameraChanged();
        }

        public void HandlePan(float deltaX, float deltaY)
        {
            var right = GetRight();
            var up = GetUp();

            _position += right * deltaX * _panSensitivity;
            _position += up    * deltaY * _panSensitivity;

            OnCameraChanged();
        }

        public void HandleFly(Vector3 localDirection)
        {
            var forward = GetForward();
            var right = GetRight();
            var up = Vector3.UnitY;

            _position += forward * localDirection.Z * _flySensitivity;
            _position -= right   * localDirection.X * _flySensitivity;
            _position += up      * localDirection.Y * _flySensitivity;

            OnCameraChanged();
        }

        public void HandleZoom(float delta)
        {
            var forward = GetForward();
            _position += forward * delta * _zoomSensitivity;
            OnCameraChanged();
        }

        public void FocusOnBounds(Vector3 center, float radius, float paddingFactor = 1.5f)
        {
            if (_scene?.Camera == null) return;
            if (radius <= 0.001f) radius = 1.0f;

            var fov = _scene.Camera.FieldOfView;
            var distance = (radius * paddingFactor) / (float)Math.Tan(fov * 0.5f);

            var direction = GetForward();
            _position = center - direction * distance;
            OnCameraChanged();
        }

        public void SetPosition(Vector3 position)
        {
            _position = position;
            OnCameraChanged();
        }

        public void LookAt(Vector3 target)
        {
            var direction = Vector3.Normalize(target - _position);
            _pitch = (float)Math.Asin(direction.Y);
            _yaw = (float)Math.Atan2(direction.X, direction.Z);
            OnCameraChanged();
        }

        public bool UpdateCamera(float aspectRatio)
        {
            if (_scene?.Camera == null) return false;

            var forward = GetForward();
            var right = GetRight();
            var up = Vector3.Cross(forward, right).Normalized();
            var target = _position + forward;

            _scene.Camera.Position = _position;
            _scene.Camera.Target = target;
            _scene.Camera.Up = up;

            _scene.Camera.AspectRatio = aspectRatio;
            _scene.Camera.FieldOfView = MathHelper.DegreesToRadians(45.0f);
            _scene.Camera.NearPlane = 10.0f;
            _scene.Camera.FarPlane = 10000000.0f;
            _scene.Camera.Mode = ProjectionMode.Perspective;

            return false;
        }

        public void SetOverheadView()
        {
            // Set looking straight down
            _pitch = -PITCH_LIMIT;
            _yaw = 0f;

            // Compute scene bounds and position camera above the entire scene
            if (_scene?.Models != null && _scene.Models.Count > 0)
            {
                var overallMin = new Vector3(float.MaxValue);
                var overallMax = new Vector3(float.MinValue);
                bool hasVisibleModels = false;

                foreach (var model in _scene.Models)
                {
                    if (model.Visible)
                    {
                        var (min, max) = model.GetBoundingBox();
                        overallMin = Vector3.ComponentMin(overallMin, min);
                        overallMax = Vector3.ComponentMax(overallMax, max);
                        hasVisibleModels = true;
                    }
                }

                if (hasVisibleModels)
                {
                    var sceneCenter = (overallMin + overallMax) * 0.5f;
                    var sceneSize = overallMax - overallMin;
                    float maxHorizontal = Math.Max(sceneSize.X, sceneSize.Z);

                    // Compute distance needed to see the whole scene from above
                    float fov = _scene.Camera?.FieldOfView ?? MathHelper.DegreesToRadians(45.0f);
                    float distance = (maxHorizontal * 0.5f * 1.2f) / (float)Math.Tan(fov * 0.5f);

                    // Position camera above the scene center
                    _position = new Vector3(sceneCenter.X, overallMax.Y + distance, sceneCenter.Z);
                }
            }

            OnCameraChanged();
        }

        public void Reset()
        {
            _position = new Vector3(0f, 5000f, 5000f);
            _yaw = MathHelper.Pi;
            _pitch = -0.4f;
            OnCameraChanged();
        }

        private Vector3 GetForward()
        {
            return new Vector3(
                (float)(Math.Cos(_pitch) * Math.Sin(_yaw)),
                (float)Math.Sin(_pitch),
                (float)(Math.Cos(_pitch) * Math.Cos(_yaw))
            ).Normalized();
        }

        private Vector3 GetRight()
        {
            // Right vector is always horizontal (yaw-only), avoids gimbal lock
            return new Vector3(
                (float)Math.Cos(_yaw),
                0f,
                -(float)Math.Sin(_yaw)
            );
        }

        private Vector3 GetUp()
        {
            return Vector3.Cross(GetForward(), GetRight()).Normalized();
        }

        private void OnCameraChanged()
        {
            CameraChanged?.Invoke();
        }
    }
}
