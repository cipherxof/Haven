using OpenTK.Mathematics;
using System;
using Avalonia3DControl.Core.Cameras;

namespace Avalonia3DControl.Core.Cameras
{
    /// <summary>
    /// 相机控制器，处理相机的交互操作和状态更新
    /// </summary>
    /// <remarks>
    /// CameraController负责管理3D场景中的相机行为，包括：
    /// - 鼠标交互处理：拖拽旋转、滚轮缩放
    /// - 相机状态管理：位置、旋转角度、缩放级别
    /// - 视图矩阵计算：根据相机参数生成变换矩阵
    /// - 相机重置：恢复到默认视角
    /// 
    /// 相机采用轨道控制模式，围绕目标点进行旋转和缩放。
    /// 支持的操作：
    /// - 左键拖拽：旋转视角
    /// - 滚轮：缩放距离
    /// - 中键拖拽：平移视角（可选）
    /// </remarks>
    public class CameraController
    {
        #region 常量定义
        private const float ZOOM_SMOOTHING = 0.25f;
        private const float ROTATION_LIMIT_OFFSET = 0.1f;
        #endregion

        #region 私有字段
        private float _rotationSensitivity = 0.01f;
        private float _translationSensitivity = 0.002f;
        private float _zoomSensitivity = 0.3f;
        private float _minZoom = 0.0001f;
        private float _maxZoom = 1000000.0f;
        private float _cameraDistance = 1.0f;
        private float _rotationX = 0.0f;
        private float _rotationY = 0.0f;
        private float _zoom = 1.0f;
        private float _targetZoom = 1.0f;
        private float _orthographicSize = 0.5f;
        private float _targetOrthographicSize = 0.5f;
        private Vector3 _cameraOffset = Vector3.Zero;
        private Scene3D _scene;
        #endregion

        #region 事件
        /// <summary>
        /// 相机状态改变时触发
        /// </summary>
        public event Action? CameraChanged;
        #endregion

        #region 构造函数
        public CameraController(Scene3D scene)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        }
        #endregion

        #region 公共属性
        /// <summary>
        /// 获取当前缩放值
        /// </summary>
        public float Zoom => _zoom;

        /// <summary>
        /// 获取当前X轴旋转角度
        /// </summary>
        public float RotationX => _rotationX;

        /// <summary>
        /// 获取当前Y轴旋转角度
        /// </summary>
        public float RotationY => _rotationY;

        /// <summary>
        /// 获取相机偏移
        /// </summary>
        public Vector3 CameraOffset => _cameraOffset;

        public void NudgeFreeCam(Vector3 delta)
        {
            _cameraOffset += delta;
            OnCameraChanged();
        }

        public void SetOverheadView()
        {
            _rotationX = MathHelper.PiOver2 - 0.01f;
            _rotationY = 0.0f;
            _scene.Camera.Mode = ProjectionMode.Perspective;
            _scene.Camera.ViewLock = ViewLockMode.None;
            OnCameraChanged();
        }

        public void SetSpeedScale(float scale)
        {
            if (scale <= 0)
            {
                return;
            }

            _translationSensitivity = 0.005f * scale;
        }

        public void SetZoomScale(float scale)
        {
            if (scale <= 0)
            {
                return;
            }

            _zoomSensitivity = 0.3f * scale;
        }

        public void SetZoomLimits(float minZoom, float maxZoom)
        {
            _minZoom = Math.Max(0.0001f, minZoom);
            _maxZoom = maxZoom <= 0 ? float.MaxValue : maxZoom;
            if (_minZoom > _maxZoom)
            {
                (_minZoom, _maxZoom) = (_maxZoom, _minZoom);
            }
        }

        public void FocusOnBounds(Vector3 center, float radius, float paddingFactor = 1.2f)
        {
            if (_scene?.Camera == null)
            {
                return;
            }

            if (radius <= 0.001f)
            {
                radius = 1.0f;
            }

            var fov = _scene.Camera.FieldOfView;
            var distance = (radius * paddingFactor) / (float)Math.Tan(fov * 0.5f);
            _targetZoom = distance / _cameraDistance;
            _targetZoom = Math.Max(_minZoom, Math.Min(_maxZoom, _targetZoom));
            _zoom = _targetZoom;
            _cameraOffset = center;
            _rotationX = 0.0f;
            _rotationY = 0.0f;
            OnCameraChanged();
        }
        #endregion

        #region 相机控制方法
        /// <summary>
        /// 处理旋转输入
        /// </summary>
        /// <param name="deltaX">X轴增量</param>
        /// <param name="deltaY">Y轴增量</param>
        public void HandleRotation(float deltaX, float deltaY)
        {
            if (_scene?.Camera?.ViewLock != ViewLockMode.None)
                return; // 视图锁定时不允许旋转

            _rotationY += deltaX * _rotationSensitivity;
            _rotationX -= deltaY * _rotationSensitivity;
            
            // 限制X轴旋转角度
            _rotationX = Math.Max(-MathHelper.PiOver2 + ROTATION_LIMIT_OFFSET, 
                                Math.Min(MathHelper.PiOver2 - ROTATION_LIMIT_OFFSET, _rotationX));
            
            OnCameraChanged();
        }

        /// <summary>
        /// 处理平移输入
        /// </summary>
        /// <param name="deltaX">X轴增量</param>
        /// <param name="deltaY">Y轴增量</param>
        public void HandleTranslation(float deltaX, float deltaY)
        {
            var right = Vector3.Cross(Vector3.UnitY, GetCameraDirection()).Normalized();
            var up = Vector3.Cross(GetCameraDirection(), right).Normalized();
            
            // 修正平移方向：鼠标向右拖拽，场景向左移动（相机向右移动）
            _cameraOffset -= right * deltaX * _translationSensitivity * _zoom;
            _cameraOffset += up * deltaY * _translationSensitivity * _zoom;
            
            OnCameraChanged();
        }

        /// <summary>
        /// 处理缩放输入
        /// </summary>
        /// <param name="delta">缩放增量</param>
        public void HandleZoom(float delta)
        {
            if (_scene?.Camera?.Mode == ProjectionMode.Perspective)
            {
                // 透视投影模式：使用对数缩放获得更平滑的体验
                var logZoom = (float)Math.Log(_targetZoom);
                logZoom += delta * _zoomSensitivity;
                
                // 转换回线性空间并应用限制
                _targetZoom = (float)Math.Exp(logZoom);
                _targetZoom = Math.Max(_minZoom, Math.Min(_maxZoom, _targetZoom));
            }
            else
            {
                // 正交投影模式：直接调整正交投影大小
                _targetOrthographicSize -= delta * _zoomSensitivity * 2.0f;
                _targetOrthographicSize = Math.Max(0.5f, Math.Min(20.0f, _targetOrthographicSize));
            }
            
            OnCameraChanged();
        }

        /// <summary>
        /// 重置相机到默认状态
        /// </summary>
        public void Reset()
        {
            _rotationX = 0.0f;
            _rotationY = 0.0f;
            _zoom = 1.0f;
            _targetZoom = 1.0f;
            _orthographicSize = 0.5f;
            _targetOrthographicSize = 0.5f;
            _cameraOffset = Vector3.Zero;
            
            OnCameraChanged();
        }
        
        /// <summary>
        /// 自动调整相机位置以适应模型
        /// </summary>
        /// <param name="model">要适应的模型</param>
        /// <param name="paddingFactor">边距因子，默认为1.2（即增加20%的边距）</param>
        public void FitToModel(Core.Models.Model3D model, float paddingFactor = 1.2f)
        {
            if (model == null || _scene?.Camera == null)
                return;
                
            var modelSize = model.GetSize();
            var modelCenter = model.GetCenter();
            
            // 如果模型尺寸为零，使用默认设置
            if (modelSize.Length < 0.001f)
            {
                Reset();
                return;
            }
            
            // 计算模型的最大尺寸
            float maxSize = Math.Max(Math.Max(modelSize.X, modelSize.Y), modelSize.Z);
            
            if (_scene.Camera.Mode == ProjectionMode.Perspective)
            {
                // 透视投影模式：根据视野角度计算合适的距离
                float fov = _scene.Camera.FieldOfView;
                float distance = (maxSize * paddingFactor) / (2.0f * (float)Math.Tan(fov * 0.5f));
                
                // 设置缩放值，使相机距离适应模型
                _targetZoom = distance / _cameraDistance;
                _zoom = _targetZoom;
            }
            else
            {
                // 正交投影模式：基于相机方向将模型包围盒投影到屏幕平面，计算所需视图高度
                var (min, max) = model.GetBoundingBox();
                var corners = new Vector3[]
                {
                    new Vector3(min.X, min.Y, min.Z),
                    new Vector3(min.X, min.Y, max.Z),
                    new Vector3(min.X, max.Y, min.Z),
                    new Vector3(min.X, max.Y, max.Z),
                    new Vector3(max.X, min.Y, min.Z),
                    new Vector3(max.X, min.Y, max.Z),
                    new Vector3(max.X, max.Y, min.Z),
                    new Vector3(max.X, max.Y, max.Z)
                };

                var dir = GetCameraDirection();
                var worldUp = Vector3.UnitY;
                var right = Vector3.Cross(dir, worldUp);
                if (right.Length < 1e-6f) right = Vector3.UnitX; else right.Normalize();
                var upOnScreen = Vector3.Cross(right, dir).Normalized();

                float minR = float.PositiveInfinity, maxR = float.NegativeInfinity;
                float minU = float.PositiveInfinity, maxU = float.NegativeInfinity;

                foreach (var c in corners)
                {
                    var v = c - modelCenter; // 使用模型中心作为目标点
                    float r = Vector3.Dot(v, right);
                    float u = Vector3.Dot(v, upOnScreen);
                    if (r < minR) minR = r; if (r > maxR) maxR = r;
                    if (u < minU) minU = u; if (u > maxU) maxU = u;
                }

                float width = maxR - minR;
                float height = maxU - minU;
                float aspect = _scene.Camera.AspectRatio > 0.0001f ? _scene.Camera.AspectRatio : 1.0f;

                // 对较小模型（例如单位立方体）给予稍大的边距，避免看起来偏大
                float baseSize = Math.Max(height, width / aspect);
                float effectivePadding = paddingFactor;
                if (baseSize <= 1.5f)
                    effectivePadding = Math.Max(effectivePadding, 1.3f);

                _targetOrthographicSize = baseSize * effectivePadding;
                _orthographicSize = _targetOrthographicSize;
            }
            
            // 设置相机偏移，使模型居中
            _cameraOffset = modelCenter;
            
            // 保持当前的旋转角度，只调整缩放和位置
            OnCameraChanged();
        }
        
        /// <summary>
        /// 自动调整相机位置以适应场景中的所有模型
        /// </summary>
        /// <param name="paddingFactor">边距因子，默认为1.2（即增加20%的边距）</param>
        public void FitToScene(float paddingFactor = 1.2f)
        {
            if (_scene?.Models == null || _scene.Models.Count == 0)
            {
                Reset();
                return;
            }
            
            // 计算所有模型的总边界框
            var overallMin = new Vector3(float.MaxValue);
            var overallMax = new Vector3(float.MinValue);
            bool hasValidModels = false;
            
            foreach (var model in _scene.Models)
            {
                if (model.Visible)
                {
                    var (min, max) = model.GetBoundingBox();
                    if (min != Vector3.Zero || max != Vector3.Zero)
                    {
                        overallMin = Vector3.ComponentMin(overallMin, min);
                        overallMax = Vector3.ComponentMax(overallMax, max);
                        hasValidModels = true;
                    }
                }
            }
            
            if (!hasValidModels)
            {
                Reset();
                return;
            }
            
            // 计算场景的中心和尺寸
            var sceneCenter = (overallMin + overallMax) * 0.5f;
            var sceneSize = overallMax - overallMin;
            float maxSize = Math.Max(Math.Max(sceneSize.X, sceneSize.Y), sceneSize.Z);
            
            if (_scene.Camera.Mode == ProjectionMode.Perspective)
            {
                // 透视投影模式
                float fov = _scene.Camera.FieldOfView;
                float distance = (maxSize * paddingFactor) / (2.0f * (float)Math.Tan(fov * 0.5f));
                
                _targetZoom = distance / _cameraDistance;
                _zoom = _targetZoom;
            }
            else
            {
                // 正交投影模式：基于相机方向将场景包围盒投影到屏幕平面，计算所需视图高度
                var corners = new Vector3[]
                {
                    new Vector3(overallMin.X, overallMin.Y, overallMin.Z),
                    new Vector3(overallMin.X, overallMin.Y, overallMax.Z),
                    new Vector3(overallMin.X, overallMax.Y, overallMin.Z),
                    new Vector3(overallMin.X, overallMax.Y, overallMax.Z),
                    new Vector3(overallMax.X, overallMin.Y, overallMin.Z),
                    new Vector3(overallMax.X, overallMin.Y, overallMax.Z),
                    new Vector3(overallMax.X, overallMax.Y, overallMin.Z),
                    new Vector3(overallMax.X, overallMax.Y, overallMax.Z)
                };

                var dir = GetCameraDirection();
                var worldUp = Vector3.UnitY;
                var right = Vector3.Cross(dir, worldUp);
                if (right.Length < 1e-6f) right = Vector3.UnitX; else right.Normalize();
                var upOnScreen = Vector3.Cross(right, dir).Normalized();

                float minR = float.PositiveInfinity, maxR = float.NegativeInfinity;
                float minU = float.PositiveInfinity, maxU = float.NegativeInfinity;

                foreach (var c in corners)
                {
                    var v = c - sceneCenter; // 使用场景中心作为目标点
                    float r = Vector3.Dot(v, right);
                    float u = Vector3.Dot(v, upOnScreen);
                    if (r < minR) minR = r; if (r > maxR) maxR = r;
                    if (u < minU) minU = u; if (u > maxU) maxU = u;
                }

                float width = maxR - minR;
                float height = maxU - minU;
                float aspect = _scene.Camera.AspectRatio > 0.0001f ? _scene.Camera.AspectRatio : 1.0f;

                // 对较小场景给予稍大的边距，避免看起来偏大
                float baseSize = Math.Max(height, width / aspect);
                float effectivePadding = paddingFactor;
                if (baseSize <= 1.5f)
                    effectivePadding = Math.Max(effectivePadding, 1.3f);

                _targetOrthographicSize = baseSize * effectivePadding;
                _orthographicSize = _targetOrthographicSize;
            }
            
            // 设置相机偏移，使场景居中
            _cameraOffset = sceneCenter;
            
            OnCameraChanged();
        }

        /// <summary>
        /// 更新相机状态
        /// </summary>
        /// <param name="aspectRatio">宽高比</param>
        /// <returns>是否需要继续渲染（用于平滑动画）</returns>
        public bool UpdateCamera(float aspectRatio)
        {
            if (_scene?.Camera == null) return false;
            
            bool needsContinuousRendering = false;
            
            // 检查是否处于视图锁定模式
            if (_scene.Camera.ViewLock != ViewLockMode.None)
            {
                // 视图锁定模式：只处理缩放，不改变相机位置和方向
                needsContinuousRendering = UpdateZoomInLockedMode();
            }
            else
            {
                // 自由视角模式：原有的相机控制逻辑
                needsContinuousRendering = UpdateCameraInFreeMode();
            }

            // 更新通用相机参数
            _scene.Camera.AspectRatio = aspectRatio;
            _scene.Camera.FieldOfView = MathHelper.DegreesToRadians(45.0f);
            var distance = _cameraDistance * _zoom;
            _scene.Camera.NearPlane = 10.0f;
            _scene.Camera.FarPlane = 10000000.0f;
            
            return needsContinuousRendering;
        }

        /// <summary>
        /// 切换到正交投影时，根据当前透视缩放匹配等效的正交视图高度，保持屏幕显示比例一致
        /// </summary>
        public void MatchScaleToOrthographic()
        {
            if (_scene?.Camera == null) return;
            // 透视下：视口高度 = 2 * tan(fov/2) * 距离
            float fov = _scene.Camera.FieldOfView;
            float distance = _cameraDistance * _zoom;
            float orthoSize = 2.0f * (float)Math.Tan(fov * 0.5f) * distance;
            _targetOrthographicSize = Math.Max(0.01f, orthoSize);
            _orthographicSize = _targetOrthographicSize;
            OnCameraChanged();
        }

        /// <summary>
        /// 切换到透视投影时，根据当前正交视图高度匹配等效的透视缩放，保持屏幕显示比例一致
        /// </summary>
        public void MatchScaleToPerspective()
        {
            if (_scene?.Camera == null) return;
            // 正交下：等效透视距离 = height / (2 * tan(fov/2))
            float fov = _scene.Camera.FieldOfView;
            float orthoSize = Math.Max(0.01f, _orthographicSize);
            float distance = orthoSize / (2.0f * (float)Math.Tan(fov * 0.5f));
            float zoom = distance / _cameraDistance;
            zoom = Math.Max(_minZoom, Math.Min(_maxZoom, zoom));
            _targetZoom = zoom;
            _zoom = _targetZoom;
            OnCameraChanged();
        }

        #endregion

        #region 私有方法
        private bool UpdateZoomInLockedMode()
        {
            bool needsContinuousRendering = false;
            
            if (_scene.Camera.Mode == ProjectionMode.Perspective)
            {
                // 透视投影模式：平滑插值到目标缩放值
                var zoomDifference = _targetZoom - _zoom;
                if (Math.Abs(zoomDifference) > 0.001f)
                {
                    _zoom += zoomDifference * ZOOM_SMOOTHING;
                    
                    if (Math.Abs(zoomDifference) < 0.01f)
                    {
                        _zoom = _targetZoom;
                    }
                    else
                    {
                        needsContinuousRendering = true;
                    }
                }
                
                // 在锁定视图中应用缩放到相机距离
                var basePosition = _scene.Camera.Position;
                var direction = Vector3.Normalize(basePosition - _scene.Camera.Target);
                _scene.Camera.Position = _scene.Camera.Target + direction * _cameraDistance * _zoom;
            }
            else
            {
                // 正交投影模式：平滑插值到目标正交投影大小
                var orthographicDifference = _targetOrthographicSize - _orthographicSize;
                if (Math.Abs(orthographicDifference) > 0.001f)
                {
                    _orthographicSize += orthographicDifference * ZOOM_SMOOTHING;
                    
                    if (Math.Abs(orthographicDifference) < 0.01f)
                    {
                        _orthographicSize = _targetOrthographicSize;
                    }
                    else
                    {
                        needsContinuousRendering = true;
                    }
                }
                
                _scene.Camera.OrthographicSize = _orthographicSize;
            }
            
            return needsContinuousRendering;
        }

        private bool UpdateCameraInFreeMode()
        {
            bool needsContinuousRendering = false;
            
            if (_scene.Camera.Mode == ProjectionMode.Perspective)
            {
                // 透视投影模式：平滑插值到目标缩放值
                var zoomDifference = _targetZoom - _zoom;
                if (Math.Abs(zoomDifference) > 0.001f)
                {
                    _zoom += zoomDifference * ZOOM_SMOOTHING;
                    
                    if (Math.Abs(zoomDifference) < 0.01f)
                    {
                        _zoom = _targetZoom;
                    }
                    else
                    {
                        needsContinuousRendering = true;
                    }
                }
                
                // 计算相机位置
                var cameraPosition = new Vector3(
                    (float)(Math.Sin(_rotationY) * Math.Cos(_rotationX) * _cameraDistance * _zoom),
                    (float)(Math.Sin(_rotationX) * _cameraDistance * _zoom),
                    (float)(Math.Cos(_rotationY) * Math.Cos(_rotationX) * _cameraDistance * _zoom)
                ) + _cameraOffset;

                _scene.Camera.Position = cameraPosition;
            }
            else
            {
                // 正交投影模式：平滑插值到目标正交投影大小
                var orthographicDifference = _targetOrthographicSize - _orthographicSize;
                if (Math.Abs(orthographicDifference) > 0.001f)
                {
                    _orthographicSize += orthographicDifference * ZOOM_SMOOTHING;
                    
                    if (Math.Abs(orthographicDifference) < 0.01f)
                    {
                        _orthographicSize = _targetOrthographicSize;
                    }
                    else
                    {
                        needsContinuousRendering = true;
                    }
                }
                
                // 正交投影模式下也支持旋转，但距离固定
                var cameraPosition = new Vector3(
                    (float)(Math.Sin(_rotationY) * Math.Cos(_rotationX) * _cameraDistance),
                    (float)(Math.Sin(_rotationX) * _cameraDistance),
                    (float)(Math.Cos(_rotationY) * Math.Cos(_rotationX) * _cameraDistance)
                ) + _cameraOffset;
                
                _scene.Camera.Position = cameraPosition;
                _scene.Camera.OrthographicSize = _orthographicSize;
            }
            
            // 更新Scene.Camera的通用参数
            _scene.Camera.Target = _cameraOffset;
            var forward = Vector3.Normalize(_scene.Camera.Target - _scene.Camera.Position);
            _scene.Camera.Up = ComputeStableUp(forward);
            
            return needsContinuousRendering;
        }

        private Vector3 GetCameraDirection()
        {
            return new Vector3(
                (float)(Math.Sin(_rotationY) * Math.Cos(_rotationX)),
                (float)Math.Sin(_rotationX),
                (float)(Math.Cos(_rotationY) * Math.Cos(_rotationX))
            ).Normalized();
        }

        private void OnCameraChanged()
        {
            CameraChanged?.Invoke();
        }

        private static Vector3 ComputeStableUp(Vector3 forward)
        {
            // Keep world-up stable; only switch to a fixed fallback when near the pole.
            var worldUp = Vector3.UnitY;
            var right = Vector3.Cross(worldUp, forward);
            if (right.LengthSquared < 1e-6f)
            {
                right = Vector3.Cross(Vector3.UnitZ, forward);
            }
            right.Normalize();
            return Vector3.Cross(forward, right).Normalized();
        }

        #endregion
    }
}
