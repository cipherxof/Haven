using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using Avalonia3DControl.Core;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Core.Lighting;
using Avalonia3DControl.Core.Cameras;
using Avalonia3DControl.Materials;
using Avalonia3DControl.Rendering;
using Avalonia3DControl.UI;

namespace Avalonia3DControl.Rendering.OpenGL
{
    /// <summary>
    /// 场景渲染器，负责管理复杂的场景渲染流程
    /// </summary>
    public class SceneRenderer
    {
        private readonly ShaderManager _shaderManager;
        private readonly ModelRenderer _modelRenderer;
        private readonly GradientBar? _gradientBar;
        private readonly AxisLabelRenderer _axisLabelRenderer;
        private RenderMode _currentRenderMode = RenderMode.Fill;

        public SceneRenderer(ShaderManager shaderManager, ModelRenderer modelRenderer, GradientBar? gradientBar)
        {
            _shaderManager = shaderManager ?? throw new ArgumentNullException(nameof(shaderManager));
            _modelRenderer = modelRenderer ?? throw new ArgumentNullException(nameof(modelRenderer));
            _gradientBar = gradientBar;
            _axisLabelRenderer = new AxisLabelRenderer();
        }

        /// <summary>
        /// 渲染完整场景（包含坐标轴和UI元素）
        /// </summary>
        public void RenderScene(Camera camera, List<Model3D> models, List<Light> lights, 
            Vector3 backgroundColor, ShadingMode shadingMode, RenderMode renderMode,
            Model3D? coordinateAxes = null, MiniAxes? miniAxes = null, BoundingBoxRenderer? boundingBoxRenderer = null, double dpiScale = 1.0)
        {
            // 更新动画
            
            // 准备渲染环境
            PrepareRenderEnvironment(backgroundColor, renderMode);
            
            // 获取主着色器程序
            int shaderProgram = _shaderManager.GetShaderProgram(shadingMode);
            if (shaderProgram == 0) return;
            
            GL.UseProgram(shaderProgram);
            
            // 设置相机矩阵
            SetupCameraMatrices(camera, shaderProgram);
            
            // 渲染坐标轴
            if (coordinateAxes != null && coordinateAxes.Visible)
            {
                RenderCoordinateAxes(coordinateAxes, camera, shaderProgram, renderMode);
            }
            
            // 渲染所有模型
            RenderModels(models, shaderProgram);
            
            // 渲染包围盒
            if (boundingBoxRenderer != null && boundingBoxRenderer.Visible)
            {
                RenderBoundingBox(boundingBoxRenderer, models, camera, shaderProgram);
            }
            
            // 渲染迷你坐标轴
            if (miniAxes != null && miniAxes.Visible && miniAxes.AxesModel != null)
            {
                RenderMiniAxes(miniAxes, camera, shaderProgram);
            }
            
            // 渲染UI元素（梯度条等）
            RenderUIElements(dpiScale);
            
            GL.UseProgram(0);
        }

        /// <summary>
        /// 准备渲染环境
        /// </summary>
        private void PrepareRenderEnvironment(Vector3 backgroundColor, RenderMode renderMode)
        {
            // 清除缓冲区
            GL.ClearColor(backgroundColor.X, backgroundColor.Y, backgroundColor.Z, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            // 启用混合以支持透明度
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            
            // 设置渲染模式
            _currentRenderMode = renderMode;
        }

        /// <summary>
        /// 设置相机矩阵
        /// </summary>
        private void SetupCameraMatrices(Camera camera, int shaderProgram)
        {
            var viewMatrix = camera.GetViewMatrix();
            var projectionMatrix = camera.GetProjectionMatrix();
            
            // Set view and projection matrices
            
            SetMatrix(shaderProgram, "view", viewMatrix);
            SetMatrix(shaderProgram, "projection", projectionMatrix);
        }

        /// <summary>
        /// 渲染坐标轴
        /// </summary>
        private void RenderCoordinateAxes(Model3D coordinateAxes, Camera camera, int mainShaderProgram, RenderMode renderMode)
        {
            // 坐标轴始终使用顶点着色器和填充模式渲染
            int axesShaderProgram = _shaderManager.GetShaderProgram(ShadingMode.Vertex);
            if (axesShaderProgram == 0) return;
            
            GL.UseProgram(axesShaderProgram);
            
            // 重新设置矩阵（因为切换了着色器）
            SetupCameraMatrices(camera, axesShaderProgram);
            
            // 保存当前渲染模式并强制使用填充模式
            var prevMode = _currentRenderMode;
            _currentRenderMode = RenderMode.Fill;
            
            // 禁用深度测试，确保坐标轴始终可见
            GL.Disable(EnableCap.DepthTest);
            
            _modelRenderer.RenderModel(coordinateAxes, axesShaderProgram, RenderMode.Fill);
            
            // 恢复深度测试
            GL.Enable(EnableCap.DepthTest);
            
            // 恢复渲染模式和着色器
            _currentRenderMode = prevMode;
            GL.UseProgram(mainShaderProgram);
        }

        /// <summary>
        /// 渲染所有模型
        /// </summary>
        private void RenderModels(List<Model3D> models, int shaderProgram)
        {
            // Draw opaque geometry first so translucent overlays remain visible regardless
            // of the order in which scene layers were loaded or replaced.
            RenderModels(models, shaderProgram, transparent: false);
            RenderModels(models, shaderProgram, transparent: true);
        }

        private void RenderModels(List<Model3D> models, int shaderProgram, bool transparent)
        {
            foreach (var model in models)
            {
                if (model.Visible && ModelRenderer.IsTransparent(model) == transparent)
                {
                    // 如果顶点需要更新，更新顶点缓冲区
                    if (model.VerticesNeedUpdate)
                    {
                        _modelRenderer.UpdateModelVertexBuffer(model);
                        model.VerticesNeedUpdate = false;
                    }
                    if (model.IndicesNeedUpdate)
                    {
                        _modelRenderer.UpdateModelIndexBuffer(model);
                        model.IndicesNeedUpdate = false;
                    }
                    
                    _modelRenderer.RenderModel(
                        model,
                        shaderProgram,
                        model.RenderModeOverride ?? _currentRenderMode);
                }
            }
        }

        /// <summary>
        /// 渲染迷你坐标轴
        /// </summary>
        private void RenderMiniAxes(MiniAxes miniAxes, Camera camera, int mainShaderProgram)
        {
            if (miniAxes.AxesModel == null) return;
            
            // 迷你坐标轴始终使用顶点着色器
            int miniAxesShaderProgram = _shaderManager.GetShaderProgram(ShadingMode.Vertex);
            if (miniAxesShaderProgram == 0) return;
            
            // 保存当前状态
            int[] originalViewport = new int[4];
            GL.GetInteger(GetPName.Viewport, originalViewport);
            GL.GetInteger(GetPName.CurrentProgram, out int prevProgram);
            var prevRenderMode = _currentRenderMode;
            
            try
            {
                // 设置迷你坐标轴视口
                SetupMiniAxesViewport(miniAxes, originalViewport);
                
                // 切换到顶点着色器
                GL.UseProgram(miniAxesShaderProgram);
                
                // 设置迷你坐标轴的矩阵
                SetupMiniAxesMatrices(camera, miniAxesShaderProgram);
                
                // 强制使用填充模式渲染
                _currentRenderMode = RenderMode.Fill;
                _modelRenderer.RenderModel(miniAxes.AxesModel, miniAxesShaderProgram, RenderMode.Fill);
                
                // 渲染轴标签
                RenderMiniAxesLabels(miniAxesShaderProgram, camera);
            }
            finally
            {
                // 恢复状态
                _currentRenderMode = prevRenderMode;
                GL.Viewport(originalViewport[0], originalViewport[1], originalViewport[2], originalViewport[3]);
                GL.UseProgram(mainShaderProgram);
            }
        }

        /// <summary>
        /// 设置迷你坐标轴视口
        /// </summary>
        private void SetupMiniAxesViewport(MiniAxes miniAxes, int[] originalViewport)
        {
            int screenWidth = originalViewport[2];
            int screenHeight = originalViewport[3];
            
            Vector2 screenPos = miniAxes.GetScreenPosition(screenWidth, screenHeight);
            
            int miniViewportSize = 150;
            int miniX = Math.Max(0, (int)(screenPos.X - miniViewportSize / 2));
            int miniY = Math.Max(0, (int)(screenHeight - screenPos.Y - miniViewportSize / 2));
            
            // 确保视口不超出屏幕边界
            miniX = Math.Min(miniX, screenWidth - miniViewportSize);
            miniY = Math.Min(miniY, screenHeight - miniViewportSize);
            
            GL.Viewport(miniX, miniY, miniViewportSize, miniViewportSize);
        }

        /// <summary>
        /// 设置迷你坐标轴矩阵
        /// </summary>
        private void SetupMiniAxesMatrices(Camera camera, int shaderProgram)
        {
            // 创建迷你坐标轴的投影矩阵（扩大视场避免截取）
            Matrix4 miniProjection = Matrix4.CreateOrthographic(5.0f, 5.0f, 0.1f, 10.0f);
            
            // 创建迷你坐标轴的视图矩阵（跟随主相机的旋转）
            Vector3 cameraDirection = Vector3.Normalize(camera.Target - camera.Position);
            Vector3 cameraUp = camera.Up;
            Vector3 miniCameraPos = -cameraDirection * 3.0f;
            Matrix4 miniView = Matrix4.LookAt(miniCameraPos, Vector3.Zero, cameraUp);
            
            SetMatrix(shaderProgram, "view", miniView);
            SetMatrix(shaderProgram, "projection", miniProjection);
        }

        /// <summary>
        /// 渲染坐标轴标签
        /// </summary>
        private void RenderAxesLabels(int shaderProgram, Matrix4 view, Matrix4 projection)
        {
            // 使用AxisLabelRenderer渲染主坐标轴标签
            _axisLabelRenderer.RenderAxisLabels(shaderProgram, view, projection);
        }

        /// <summary>
        /// 渲染迷你坐标轴标签
        /// </summary>
        private void RenderMiniAxesLabels(int shaderProgram, Camera camera)
        {
            // 禁用深度测试以确保标注可见
            GL.Disable(EnableCap.DepthTest);
            
            try
            {
                // 创建迷你坐标轴的矩阵用于标签渲染（扩大视场避免截取）
                Matrix4 miniProjection = Matrix4.CreateOrthographic(5.0f, 5.0f, 0.1f, 10.0f);
                Vector3 cameraDirection = Vector3.Normalize(camera.Target - camera.Position);
                Vector3 cameraUp = camera.Up;
                Vector3 miniCameraPos = -cameraDirection * 3.0f;
                Matrix4 miniView = Matrix4.LookAt(miniCameraPos, Vector3.Zero, cameraUp);
                
                // 使用AxisLabelRenderer渲染标签
                _axisLabelRenderer.RenderAxisLabels(shaderProgram, miniView, miniProjection);
            }
            finally
            {
                // 重新启用深度测试
                GL.Enable(EnableCap.DepthTest);
            }
        }
        
        /// <summary>
        /// 渲染包围盒
        /// </summary>
        private void RenderBoundingBox(BoundingBoxRenderer boundingBoxRenderer, List<Model3D> models, Camera camera, int shaderProgram)
        {
            if (models.Count == 0) return;
            
            // 初始化包围盒渲染器
            boundingBoxRenderer.Initialize();
            
            // 渲染包围盒
            var viewMatrix = camera.GetViewMatrix();
            var projectionMatrix = camera.GetProjectionMatrix();
            boundingBoxRenderer.Render(models, shaderProgram, viewMatrix, projectionMatrix);
        }

        /// <summary>
        /// 渲染UI元素
        /// </summary>
        private void RenderUIElements(double dpiScale)
        {
            if (_gradientBar != null)
            {
                // 获取当前视口尺寸
                int[] viewport = new int[4];
                GL.GetInteger(GetPName.Viewport, viewport);
                
                // 确保原点为(0,0)，避免被之前的小视口偏移影响
                if (viewport[0] != 0 || viewport[1] != 0)
                {
                    GL.Viewport(0, 0, viewport[2], viewport[3]);
                }
                
                _gradientBar.Render(viewport[2], viewport[3], dpiScale);
            }
        }

        /// <summary>
        /// 设置矩阵uniform变量
        /// </summary>
        private void SetMatrix(int shaderProgram, string name, Matrix4 matrix)
        {
            int location = GL.GetUniformLocation(shaderProgram, name);
            if (location >= 0)
            {
                GL.UniformMatrix4(location, false, ref matrix);
            }
        }
    }
}
