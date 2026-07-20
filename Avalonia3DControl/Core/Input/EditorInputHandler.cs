using System;
using Avalonia;
using Avalonia.Input;
using OpenTK.Mathematics;
using Avalonia3DControl.Core.Cameras;

namespace Avalonia3DControl.Core.Input
{
    public class EditorInputHandler : IDisposable
    {
        private Vector2 _lastMousePosition;
        private bool _isRightMousePressed;
        private bool _isMiddleMousePressed;
        private readonly EditorCameraController _cameraController;

        public event Action? RenderRequested;
        public event Action? FocusRequested;

        public bool IsRightMouseDown => _isRightMousePressed;

        public EditorInputHandler(EditorCameraController cameraController)
        {
            _cameraController = cameraController ?? throw new ArgumentNullException(nameof(cameraController));
            _cameraController.CameraChanged += OnCameraChanged;
        }

        public void HandlePointerPressed(PointerPressedEventArgs e, double renderScaling)
        {
            var position = e.GetPosition(e.Source as Avalonia.Visual);
            _lastMousePosition = new Vector2(
                (float)(position.X * renderScaling),
                (float)(position.Y * renderScaling)
            );

            var point = e.GetCurrentPoint(e.Source as Avalonia.Visual);
            if (point.Properties.IsRightButtonPressed)
            {
                _isRightMousePressed = true;
                OnFocusRequested();
            }
            else if (point.Properties.IsMiddleButtonPressed)
            {
                _isMiddleMousePressed = true;
                OnFocusRequested();
            }

            e.Handled = true;
        }

        public void HandlePointerMoved(PointerEventArgs e, double renderScaling)
        {
            if (!_isRightMousePressed && !_isMiddleMousePressed)
                return;

            var position = e.GetPosition(e.Source as Avalonia.Visual);
            var currentMousePosition = new Vector2(
                (float)(position.X * renderScaling),
                (float)(position.Y * renderScaling)
            );
            var delta = currentMousePosition - _lastMousePosition;

            if (_isRightMousePressed)
            {
                _cameraController.HandleRotation(delta.X, delta.Y);
            }
            else if (_isMiddleMousePressed)
            {
                _cameraController.HandlePan(delta.X, delta.Y);
            }

            _lastMousePosition = currentMousePosition;
            e.Handled = true;
        }

        public void HandlePointerReleased(PointerReleasedEventArgs e)
        {
            _isRightMousePressed = false;
            _isMiddleMousePressed = false;
            e.Handled = true;
        }

        public void HandlePointerWheelChanged(PointerWheelEventArgs e)
        {
            var delta = (float)e.Delta.Y;
            _cameraController.HandleZoom(delta);
            e.Handled = true;
        }

        public void Reset()
        {
            _isRightMousePressed = false;
            _isMiddleMousePressed = false;
            _lastMousePosition = Vector2.Zero;
        }

        public bool IsMousePressed => _isRightMousePressed || _isMiddleMousePressed;

        private void OnCameraChanged()
        {
            RenderRequested?.Invoke();
        }

        private void OnFocusRequested()
        {
            FocusRequested?.Invoke();
        }

        public void Dispose()
        {
            if (_cameraController != null)
            {
                _cameraController.CameraChanged -= OnCameraChanged;
            }
        }
    }
}
