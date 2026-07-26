using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Utilities;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia3DControl.Core;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Core.Cameras;
using Avalonia3DControl.Core.Lighting;
using Avalonia3DControl.Core.Input;
using Avalonia3DControl.Materials;
using Avalonia3DControl.Rendering;
using Avalonia3DControl.Core.ErrorHandling;
using Avalonia3DControl.Rendering.OpenGL;
using Avalonia3DControl.Geometry.Factories;
using Avalonia3DControl.UI;

namespace Avalonia3DControl
{
    /// <summary>
    /// OpenGL 3D控件，提供3D场景渲染功能
    /// 支持模型加载、相机控制、光照、材质、动画等完整的3D渲染管线
    /// </summary>
    /// <remarks>
    /// 主要功能包括：
    /// - 3D模型渲染和显示
    /// - 相机视角控制（旋转、缩放、平移）
    /// - 多种光照模式（方向光、点光源）
    /// - 材质系统（漫反射、镜面反射、纹理）
    /// - 模态动画播放
    /// - 坐标轴显示
    /// - 渐变色条显示
    /// </remarks>
    public class OpenGL3DControl : OpenGlControlBase, ICustomHitTest
    {
        #region 私有字段
        // 核心组件
        private OpenGLRenderer? _renderer;
        private CameraController? _cameraController;
        private InputHandler? _inputHandler;
        private EditorCameraController? _editorCameraController;
        private EditorInputHandler? _editorInputHandler;
        private CameraMode _cameraMode = CameraMode.Orbital;
        private bool _gradientBarVisible = false;
        private float _cameraSpeedScale = 1.0f;

        // 渲染状态
        private ShadingMode _currentShadingMode = ShadingMode.Vertex;
        private RenderMode _currentRenderMode = RenderMode.Fill;
        private bool _isOpenGLInitialized = false;
        #endregion

        #region 公共属性
        /// <summary>
        /// 3D场景管理器
        /// </summary>
        public Scene3D Scene { get; private set; } = new Scene3D();

        public bool AllowHotkeys { get; set; }

        public bool IsOpenGlInitialized => _isOpenGLInitialized;

        public event Action? OpenGlInitialized;

        public CameraMode CameraMode
        {
            get => _cameraMode;
            set => _cameraMode = value;
        }

        public EditorCameraController? EditorCamera => _editorCameraController;
        
        /// <summary>
        /// Controls visibility of the gradient bar UI.
        /// </summary>
        public bool GradientBarVisible
        {
            get => _gradientBarVisible;
            set
            {
                _gradientBarVisible = value;
                _renderer?.SetGradientBarVisible(value);
            }
        }


        public bool FogEnabled
        {
            get => _renderer?.FogEnabled ?? _fogEnabled;
            set
            {
                _fogEnabled = value;
                if (_renderer != null) _renderer.FogEnabled = value;
                RequestNextFrameRendering();
            }
        }
        private bool _fogEnabled;
        private float _fogNear = 0.0f;
        private float _fogFar = 10000.0f;
        private Vector4 _fogColor = new(0.0f, 0.0f, 0.0f, 1.0f);
        private float _fogLimitMin = 0.0f;
        private float _fogLimitMax = 1.0f;

        public void SetFog(float near, float far, Vector4 color, float limitMin, float limitMax)
        {
            _fogNear = near;
            _fogFar = far;
            _fogColor = color;
            _fogLimitMin = limitMin;
            _fogLimitMax = limitMax;
            _renderer?.SetFog(near, far, color, limitMin, limitMax);
            RequestNextFrameRendering();
        }

        public bool ColorFilterEnabled
        {
            get => _renderer?.ColorFilterEnabled ?? false;
            set { if (_renderer != null) _renderer.ColorFilterEnabled = value; RequestNextFrameRendering(); }
        }

        public void SetColorFilter(float mono, Vector3 scale, float brightness, float contrast, Vector3 minimum, Vector3 maximum, float noise)
        {
            _renderer?.SetColorFilter(mono, scale, brightness, contrast, minimum, maximum, noise);
            RequestNextFrameRendering();
        }
        public bool ShadowsEnabled
        {
            get => _renderer?.ShadowsEnabled ?? _shadowsEnabled;
            set
            {
                _shadowsEnabled = value;
                if (_renderer != null)
                {
                    _renderer.ShadowsEnabled = value;
                }
                RequestNextFrameRendering();
            }
        }

        private bool _shadowsEnabled = false;

        public bool GlareEnabled
        {
            get => _renderer?.GlareEnabled ?? _glareEnabled;
            set
            {
                _glareEnabled = value;
                if (_renderer != null)
                {
                    _renderer.GlareEnabled = value;
                }
                RequestNextFrameRendering();
            }
        }

        private bool _glareEnabled = false;

        private float _exposureScale = 1.0f;
        private float _contrast = 1.0f;
        public float Contrast
        {
            get => _renderer?.Contrast ?? _contrast;
            set
            {
                _contrast = value;
                if (_renderer != null)
                {
                    _renderer.Contrast = value;
                }
                RequestNextFrameRendering();
            }
        }

        private float _shadowRange = 50000.0f;
        public float ShadowRange
        {
            get => _renderer?.ShadowRange ?? _shadowRange;
            set
            {
                _shadowRange = value;
                if (_renderer != null)
                {
                    _renderer.ShadowRange = value;
                }
                InvalidateShadowScene();
                RequestNextFrameRendering();
            }
        }
        public float ExposureScale
        {
            get => _renderer?.ExposureScale ?? _exposureScale;
            set
            {
                _exposureScale = value;
                if (_renderer != null)
                {
                    _renderer.ExposureScale = value;
                }
                RequestNextFrameRendering();
            }
        }

        public void InvalidateShadowScene()
        {
            _renderer?.InvalidateShadowScene();
            RequestNextFrameRendering();
        }

        public void InvalidateShadowTransforms()
        {
            _renderer?.InvalidateShadowTransforms();
            RequestNextFrameRendering();
        }

        public float CameraSpeedScale
        {
            get => _cameraSpeedScale;
            set
            {
                if (value <= 0)
                {
                    return;
                }

                _cameraSpeedScale = value;
                _cameraController?.SetSpeedScale(value);
                _editorCameraController?.SetSpeedScale(value);
            }
        }

        public void FocusOnBounds(Vector3 center, float radius, float paddingFactor = 1.2f)
        {
            if (_cameraMode == CameraMode.Editor)
                _editorCameraController?.FocusOnBounds(center, radius, paddingFactor);
            else
                _cameraController?.FocusOnBounds(center, radius, paddingFactor);
            RequestNextFrameRendering();
        }

        public void SetZoomLimits(float minZoom, float maxZoom)
        {
            _cameraController?.SetZoomLimits(minZoom, maxZoom);
        }

        public void SetZoomScale(float scale)
        {
            _cameraController?.SetZoomScale(scale);
        }

        public void ApplyTextureFromDds(Core.Models.Model3D model, int width, int height, ushort fourCc, byte[] data)
        {
            if (model == null || data == null || data.Length == 0)
            {
                Console.WriteLine("[Texture] Upload skipped: missing model or data.");
                return;
            }

            EnqueueGlAction(() =>
            {
                var textureId = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, textureId);

                // Select appropriate DXT format based on FourCC:
                // 9 = DXT1 (RGB, no alpha or 1-bit alpha)
                // 10 = DXT3 (RGBA, explicit alpha)
                // 11 = DXT5 (RGBA, interpolated alpha)
                InternalFormat internalFormat;
                if (fourCc == 11)
                {
                    internalFormat = (InternalFormat)All.CompressedRgbaS3tcDxt5Ext;
                }
                else if (fourCc == 10)
                {
                    internalFormat = (InternalFormat)All.CompressedRgbaS3tcDxt3Ext;
                }
                else
                {
                    internalFormat = (InternalFormat)All.CompressedRgbS3tcDxt1Ext;
                }

                GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, data.Length, data);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.BindTexture(TextureTarget.Texture2D, 0);

                model.TextureId = textureId;
                //Console.WriteLine($"[Texture] Uploaded id={textureId} size={width}x{height} fourCc={fourCc} data={data.Length}.");
                RequestNextFrameRendering();
            });
        }

        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _glActions = new();

        private void EnqueueGlAction(Action action)
        {
            _glActions.Enqueue(action);
            RequestNextFrameRendering();
        }
        #endregion
        
        #region 构造函数
        public OpenGL3DControl()
        {
            InitializeComponent();
            InitializeScene();
        }

        private void InitializeComponent()
        {
            Scene = new Scene3D();
            
            // 确保控件可以接收焦点和输入事件
            Focusable = true;
            IsHitTestVisible = true;
            
            // 设置控件布局属性，确保填充整个容器
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        }

        private void InitializeScene()
        {
            Scene.BackgroundColor = new Vector3(0.12f, 0.12f, 0.12f);
        }
        #endregion

        #region OpenGL初始化和清理
        protected override void OnOpenGlInit(GlInterface gl)
        {
            base.OnOpenGlInit(gl);
            
            try
            {
                // 初始化渲染器
                _renderer = new OpenGLRenderer
                {
                    ShadowsEnabled = _shadowsEnabled,
                    GlareEnabled = _glareEnabled,
                    FogEnabled = _fogEnabled
                };
                _renderer.Initialize(gl);
                _renderer.SetFog(_fogNear, _fogFar, _fogColor, _fogLimitMin, _fogLimitMax);
                _renderer.SetGradientBarVisible(_gradientBarVisible);
                
                // 初始化相机控制器 (based on camera mode)
                if (_cameraMode == CameraMode.Editor)
                {
                    _editorCameraController = new EditorCameraController(Scene);
                    _editorCameraController.SetSpeedScale(_cameraSpeedScale);

                    _editorInputHandler = new EditorInputHandler(_editorCameraController);
                    _editorInputHandler.RenderRequested += () => RequestNextFrameRendering();
                    _editorInputHandler.FocusRequested += () => Focus();
                }
                else
                {
                    _cameraController = new CameraController(Scene);
                    _cameraController.SetSpeedScale(_cameraSpeedScale);

                    _inputHandler = new InputHandler(_cameraController);
                    _inputHandler.RenderRequested += () => RequestNextFrameRendering();
                    _inputHandler.FocusRequested += () => Focus();
                }
                
                _isOpenGLInitialized = true;
                OpenGlInitialized?.Invoke();
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            _inputHandler?.Dispose();
            _editorInputHandler?.Dispose();
            _renderer?.Dispose();
            base.OnOpenGlDeinit(gl);
        }
        #endregion

        #region 渲染方法
        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (!_isOpenGLInitialized || _renderer == null)
            {
                return;
            }

            try
            {
                while (_glActions.TryDequeue(out var action))
                {
                    action();
                }

                // 设置视口，考虑DPI缩放
                var bounds = Bounds;
                var topLevel = TopLevel.GetTopLevel(this);
                var renderScaling = topLevel?.RenderScaling ?? 1.0;
                
                var pixelWidth = Math.Max(1, (int)(bounds.Width * renderScaling));
                var pixelHeight = Math.Max(1, (int)(bounds.Height * renderScaling));
                
                // 检查视口参数是否有效
                if (pixelWidth <= 0 || pixelHeight <= 0)
                {
                    return;
                }
                
                GL.Viewport(0, 0, pixelWidth, pixelHeight);

                // 更新相机参数
                UpdateCamera((float)bounds.Width / (float)bounds.Height);

                // 渲染场景（包含坐标轴和包围盒）
                var coordinateAxes = Scene.ShowCoordinateAxes ? Scene.CoordinateAxes.AxesModel : null;
                _renderer?.RenderSceneWithAxes(Scene.Camera, Scene.Models, Scene.Lights, Scene.BackgroundColor, _currentShadingMode, _currentRenderMode, coordinateAxes, Scene.MiniAxes, Scene.BoundingBoxRenderer, renderScaling);
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleRenderingException(ex, "OnOpenGlRender");
            }
        }



        private Matrix4 CreateProjectionMatrix(float aspectRatio)
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45.0f),
                aspectRatio,
                0.1f,
                100.0f
            );
        }

        private void UpdateCamera(float aspectRatio)
        {
            bool needsContinuousRendering;
            if (_cameraMode == CameraMode.Editor && _editorCameraController != null)
            {
                needsContinuousRendering = _editorCameraController.UpdateCamera(aspectRatio);
            }
            else if (_cameraController != null)
            {
                needsContinuousRendering = _cameraController.UpdateCamera(aspectRatio);
            }
            else
            {
                return;
            }

            if (needsContinuousRendering)
            {
                RequestNextFrameRendering();
            }
        }
        #endregion

        #region 鼠标事件处理
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var topLevel = TopLevel.GetTopLevel(this);
            var renderScaling = topLevel?.RenderScaling ?? 1.0;
            if (_cameraMode == CameraMode.Editor)
                _editorInputHandler?.HandlePointerPressed(e, renderScaling);
            else
                _inputHandler?.HandlePointerPressed(e, renderScaling);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            var topLevel = TopLevel.GetTopLevel(this);
            var renderScaling = topLevel?.RenderScaling ?? 1.0;
            if (_cameraMode == CameraMode.Editor)
                _editorInputHandler?.HandlePointerMoved(e, renderScaling);
            else
                _inputHandler?.HandlePointerMoved(e, renderScaling);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_cameraMode == CameraMode.Editor)
                _editorInputHandler?.HandlePointerReleased(e);
            else
                _inputHandler?.HandlePointerReleased(e);
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            if (_cameraMode == CameraMode.Editor)
                _editorInputHandler?.HandlePointerWheelChanged(e);
            else
                _inputHandler?.HandlePointerWheelChanged(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (AllowHotkeys)
            {
                if (_cameraMode == CameraMode.Editor)
                {
                    HandleEditorCamKey(e);
                }
                else
                {
                    HandleFreeCamKey(e);
                }

                if (e.Handled)
                {
                    return;
                }
            }

        }

        #endregion

        #region 公共方法
        public void SetShadingMode(ShadingMode mode)
        {
            _currentShadingMode = mode;
            RequestNextFrameRendering();
        }

        public void SetRenderMode(RenderMode mode)
        {
            _currentRenderMode = mode;
            RequestNextFrameRendering();
        }

        public void AddModel(Model3D model)
        {
            Scene.Models.Add(model);
            
            // 自动调整相机位置以适应新添加的模型
            _cameraController?.FitToScene();
            
            RequestNextFrameRendering();
        }

        public void RemoveModel(Model3D model)
        {
            Scene.Models.Remove(model);
            RequestNextFrameRendering();
        }

        public void ClearModels()
        {
            Scene.Models.Clear();
            RequestNextFrameRendering();
        }

        public void ResetCamera()
        {
            if (_cameraMode == CameraMode.Editor)
                _editorCameraController?.Reset();
            else
                _cameraController?.Reset();
            RequestNextFrameRendering();
        }
        
        /// <summary>
        /// 自动调整相机位置以适应场景中的所有模型
        /// </summary>
        public void FitCameraToScene()
        {
            _cameraController?.FitToScene();
            RequestNextFrameRendering();
        }

        public void ShowOverheadView()
        {
            if (Scene?.Camera == null)
            {
                return;
            }

            if (_cameraMode == CameraMode.Editor)
            {
                _editorCameraController?.SetOverheadView();
                RequestNextFrameRendering();
            }
            else
            {
                _cameraController?.SetOverheadView();
                FitCameraToScene();
                RequestNextFrameRendering();
            }
        }

        private void HandleFreeCamKey(KeyEventArgs e)
        {
            var camera = Scene.Camera;
            var forward = Vector3.Normalize(camera.Target - camera.Position);
            var right = Vector3.Normalize(Vector3.Cross(forward, camera.Up));
            var up = Vector3.Normalize(camera.Up);
            var speedScale = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1.0f : (1.0f / 3.0f);
            var baseStep = 1000.0f * speedScale;

            Vector3 delta = Vector3.Zero;
            switch (e.Key)
            {
                case Avalonia.Input.Key.W:
                case Avalonia.Input.Key.Up:
                    delta += forward * baseStep;
                    break;
                case Avalonia.Input.Key.S:
                case Avalonia.Input.Key.Down:
                    delta -= forward * baseStep;
                    break;
                case Avalonia.Input.Key.A:
                case Avalonia.Input.Key.Left:
                    delta -= right * baseStep;
                    break;
                case Avalonia.Input.Key.D:
                case Avalonia.Input.Key.Right:
                    delta += right * baseStep;
                    break;
                case Avalonia.Input.Key.Q:
                    delta -= up * baseStep;
                    break;
                case Avalonia.Input.Key.E:
                    delta += up * baseStep;
                    break;
                default:
                    return;
            }

            _cameraController?.NudgeFreeCam(delta);
            e.Handled = true;
        }

        private void HandleEditorCamKey(KeyEventArgs e)
        {
            if (_editorCameraController == null) return;

            Vector3 flyDirection = Vector3.Zero;
            switch (e.Key)
            {
                case Avalonia.Input.Key.W:
                case Avalonia.Input.Key.Up:
                    flyDirection.Z = 1f;
                    break;
                case Avalonia.Input.Key.S:
                case Avalonia.Input.Key.Down:
                    flyDirection.Z = -1f;
                    break;
                case Avalonia.Input.Key.A:
                case Avalonia.Input.Key.Left:
                    flyDirection.X = -1f;
                    break;
                case Avalonia.Input.Key.D:
                case Avalonia.Input.Key.Right:
                    flyDirection.X = 1f;
                    break;
                case Avalonia.Input.Key.E:
                    flyDirection.Y = 1f;
                    break;
                case Avalonia.Input.Key.Q:
                    flyDirection.Y = -1f;
                    break;
                default:
                    return;
            }

            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                flyDirection *= (1.0f / 3.0f);
            }
            _editorCameraController.HandleFly(flyDirection);
            e.Handled = true;
        }

        /// <summary>
        /// 自动调整相机位置以适应指定模型
        /// </summary>
        /// <param name="model">要适应的模型</param>
        public void FitCameraToModel(Model3D model)
        {
            _cameraController?.FitToModel(model);
            RequestNextFrameRendering();
        }

        /// <summary>
        /// 设置视图锁定模式
        /// </summary>
        /// <param name="lockMode">视图锁定模式</param>
        public void SetViewLock(ViewLockMode lockMode)
        {
            Scene.Camera.SetViewLock(lockMode);
            RequestNextFrameRendering();
        }

        /// <summary>
        /// 获取当前视图锁定模式
        /// </summary>
        /// <returns>当前视图锁定模式</returns>
        public ViewLockMode GetViewLockMode()
        {
            return Scene.Camera.ViewLock;
        }

        public void SetCoordinateAxesVisible(bool show)
        {
            Scene.SetCoordinateAxesVisible(show);
            RequestNextFrameRendering();
        }
        
        /// <summary>
        /// 设置包围盒可见性
        /// </summary>
        /// <param name="show">是否显示包围盒</param>
        public void SetBoundingBoxVisible(bool show)
        {
            Scene.SetBoundingBoxVisible(show);
            RequestNextFrameRendering();
        }
        
        /// <summary>
        /// 设置包围盒坐标刻度可见性
        /// </summary>
        /// <param name="show">是否显示坐标刻度</param>
        public void SetBoundingBoxTicksVisible(bool show)
        {
            Scene.SetBoundingBoxTicksVisible(show);
            RequestNextFrameRendering();
        }

        public void SetCurrentModel(string? modelType)
        {
            // 清空现有模型
            ClearModels();
            
            // 如果指定了模型类型，创建新模型
            if (!string.IsNullOrEmpty(modelType))
            {
                var model = GeometryFactory.CreateModel(modelType);
                if (model != null)
                {
                    Scene.Models.Add(model);
                    
                    // 自动调整相机位置以适应新模型
                    _cameraController?.FitToModel(model);
                    
                    RequestNextFrameRendering();
                }
            }
        }
        
        /// <summary>
        /// 切换到正交投影模式
        /// </summary>
        public void SwitchToOrthographic()
        {
            // 在切换投影模式前，先根据当前透视缩放匹配等效的正交大小
            _cameraController?.MatchScaleToOrthographic();
            Scene.Camera.SwitchToOrthographic();
            RequestNextFrameRendering();
        }
        
        /// <summary>
        /// 切换到透视投影模式
        /// </summary>
        public void SwitchToPerspective()
        {
            // 在切换投影模式前，先根据当前正交大小匹配等效的透视缩放
            _cameraController?.MatchScaleToPerspective();
            Scene.Camera.SwitchToPerspective();
            RequestNextFrameRendering();
        }
        
        /// <summary>
        /// 获取当前投影模式
        /// </summary>
        /// <returns>当前投影模式</returns>
        public ProjectionMode GetProjectionMode()
        {
            return Scene.Camera.Mode;
        }
        
        /// <summary>
        /// 设置梯度条可见性
        /// </summary>
        /// <param name="isVisible">是否可见</param>
        public void SetGradientBarVisible(bool isVisible)
        {
            _renderer?.SetGradientBarVisible(isVisible);
            RequestNextFrameRendering();
        }
        
        /// <summary>
        /// 设置梯度条位置
        /// </summary>
        /// <param name="position">梯度条位置</param>
        public void SetGradientBarPosition(GradientBarPosition position)
        {
            _renderer?.SetGradientBarPosition(position);
            RequestNextFrameRendering();
        }
        
        /// <summary>
        /// 设置梯度条颜色梯度类型
        /// </summary>
        public void SetGradientBarType(ColorGradientType gradientType)
        {
            _renderer?.SetGradientBarType(gradientType);
            RequestNextFrameRendering();
        }
        
        /// <summary>
        /// 设置梯度条是否使用归一化刻度（-1~1），否则显示实际最小最大值
        /// </summary>
        public void SetGradientBarUseNormalizedScale(bool useNormalized)
        {
            _renderer?.SetGradientBarUseNormalizedScale(useNormalized);
            RequestNextFrameRendering();
        }

        /// <summary>
        /// 设置是否显示梯度条刻度
        /// </summary>
        public void SetGradientBarShowTicks(bool show)
        {
            _renderer?.SetGradientBarShowTicks(show);
            RequestNextFrameRendering();
        }

        #endregion

        #region ICustomHitTest实现
        public bool HitTest(Point point)
        {
            // 检查点是否在控件边界内
            if (!Bounds.Contains(point))
                return false;

            // 检查控件是否可见和启用
            if (!IsVisible || !IsEnabled)
                return false;

            // 检查控件是否可以接收命中测试
            if (!IsHitTestVisible)
                return false;

            // 考虑DPI缩放的精确命中测试
            var topLevel = TopLevel.GetTopLevel(this);
            var renderScaling = topLevel?.RenderScaling ?? 1.0;

            // 转换为像素坐标进行更精确的测试
            var pixelPoint = new Point(
                point.X * renderScaling,
                point.Y * renderScaling
            );

            var pixelBounds = new Rect(
                0, 0,
                Bounds.Width * renderScaling,
                Bounds.Height * renderScaling
            );

            return pixelBounds.Contains(pixelPoint);
        }
        #endregion
    }
}
