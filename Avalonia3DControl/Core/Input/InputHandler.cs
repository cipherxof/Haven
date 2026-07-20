using Avalonia;
using Avalonia.Input;
using OpenTK.Mathematics;
using System;
using Avalonia3DControl.Core.Cameras;

namespace Avalonia3DControl.Core.Input
{
    /// <summary>
    /// 输入处理器，负责处理鼠标和键盘输入事件
    /// </summary>
    public class InputHandler
    {
        #region 私有字段
        private Vector2 _lastMousePosition;
        private bool _isMousePressed = false;
        private bool _isRightMousePressed = false;
        private CameraController _cameraController;
        #endregion

        #region 事件
        /// <summary>
        /// 需要重新渲染时触发
        /// </summary>
        public event Action? RenderRequested;

        /// <summary>
        /// 需要获取焦点时触发
        /// </summary>
        public event Action? FocusRequested;
        #endregion

        #region 构造函数
        public InputHandler(CameraController cameraController)
        {
            _cameraController = cameraController ?? throw new ArgumentNullException(nameof(cameraController));
            
            // 订阅相机控制器的变化事件
            _cameraController.CameraChanged += OnCameraChanged;
        }
        #endregion

        #region 鼠标事件处理
        /// <summary>
        /// 处理鼠标按下事件
        /// </summary>
        /// <param name="e">鼠标按下事件参数</param>
        /// <param name="renderScaling">渲染缩放比例</param>
        public void HandlePointerPressed(PointerPressedEventArgs e, double renderScaling)
        {
            var position = e.GetPosition(e.Source as Avalonia.Visual);
            
            // 考虑DPI缩放的鼠标坐标
            _lastMousePosition = new Vector2(
                (float)(position.X * renderScaling), 
                (float)(position.Y * renderScaling)
            );
            
            if (e.GetCurrentPoint(e.Source as Avalonia.Visual).Properties.IsLeftButtonPressed)
            {
                _isMousePressed = true;
                OnFocusRequested();
            }
            else if (e.GetCurrentPoint(e.Source as Avalonia.Visual).Properties.IsRightButtonPressed)
            {
                _isRightMousePressed = true;
                OnFocusRequested();
            }
            
            e.Handled = true;
        }

        /// <summary>
        /// 处理鼠标移动事件
        /// </summary>
        /// <param name="e">鼠标移动事件参数</param>
        /// <param name="renderScaling">渲染缩放比例</param>
        public void HandlePointerMoved(PointerEventArgs e, double renderScaling)
        {
            if (!_isMousePressed && !_isRightMousePressed)
                return;
            
            var position = e.GetPosition(e.Source as Avalonia.Visual);
            
            // 考虑DPI缩放的鼠标坐标
            var currentMousePosition = new Vector2(
                (float)(position.X * renderScaling), 
                (float)(position.Y * renderScaling)
            );
            var deltaPosition = currentMousePosition - _lastMousePosition;
            
            if (_isMousePressed)
            {
                // 左键拖拽：旋转
                _cameraController.HandleRotation(deltaPosition.X, deltaPosition.Y);
            }
            else if (_isRightMousePressed)
            {
                // 右键拖拽：平移
                _cameraController.HandleTranslation(deltaPosition.X, deltaPosition.Y);
            }
            
            _lastMousePosition = currentMousePosition;
            e.Handled = true;
        }

        /// <summary>
        /// 处理鼠标释放事件
        /// </summary>
        /// <param name="e">鼠标释放事件参数</param>
        public void HandlePointerReleased(PointerReleasedEventArgs e)
        {
            _isMousePressed = false;
            _isRightMousePressed = false;
            e.Handled = true;
        }

        /// <summary>
        /// 处理鼠标滚轮事件
        /// </summary>
        /// <param name="e">鼠标滚轮事件参数</param>
        public void HandlePointerWheelChanged(PointerWheelEventArgs e)
        {
            var delta = -(float)e.Delta.Y;
            _cameraController.HandleZoom(delta);
            e.Handled = true;
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 重置输入状态
        /// </summary>
        public void Reset()
        {
            _isMousePressed = false;
            _isRightMousePressed = false;
            _lastMousePosition = Vector2.Zero;
        }

        /// <summary>
        /// 获取当前是否有鼠标按下
        /// </summary>
        public bool IsMousePressed => _isMousePressed || _isRightMousePressed;
        #endregion

        #region 私有方法
        private void OnCameraChanged()
        {
            RenderRequested?.Invoke();
        }

        private void OnFocusRequested()
        {
            FocusRequested?.Invoke();
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_cameraController != null)
            {
                _cameraController.CameraChanged -= OnCameraChanged;
            }
        }
        #endregion
    }
}
