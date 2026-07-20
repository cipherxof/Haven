using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using Avalonia3DControl.Core;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Core.Lighting;
using Avalonia3DControl.Core.Cameras;
using Avalonia3DControl.Core.ErrorHandling;
using Avalonia3DControl.Materials;
using Avalonia3DControl.UI;
using Avalonia3DControl.Rendering;

namespace Avalonia3DControl.Rendering.OpenGL
{
    /// <summary>
    /// OpenGL渲染器，负责所有OpenGL相关的渲染逻辑
    /// </summary>
    public class OpenGLRenderer : IDisposable
    {
        #region 私有字段
        private ModelRenderer? _modelRenderer;
        private ShaderManager _shaderManager;
        private SceneRenderer? _sceneRenderer;
        private int _defaultTexture = 0;
        private bool _isInitialized = false;
        private GradientBar? _gradientBar;
        private RenderMode _currentRenderMode = RenderMode.Fill;
        #endregion

        #region 构造函数
        public OpenGLRenderer()
        {
            _shaderManager = new ShaderManager();
            _gradientBar = new GradientBar();
        }
        #endregion

        #region 初始化方法
        /// <summary>
        /// 初始化OpenGL渲染器
        /// </summary>
        /// <param name="gl">Avalonia OpenGL接口</param>
        public void Initialize(Avalonia.OpenGL.GlInterface gl)
        {
            if (_isInitialized) return;
            
            try
            {
                // 1. 加载OpenGL绑定
                LoadOpenGLBindings(gl);
                
                // 2. 配置OpenGL状态
                ConfigureOpenGLState();
                
                // 3. 初始化资源
                InitializeResources();
                
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleInitializationException(ex, "OpenGL渲染器初始化");
            }
        }
        
        /// <summary>
        /// 加载OpenGL绑定
        /// </summary>
        /// <param name="gl">Avalonia OpenGL接口</param>
        private void LoadOpenGLBindings(Avalonia.OpenGL.GlInterface gl)
        {
            var context = new AvaloniaGLInterface(gl);
            OpenTK.Graphics.OpenGL4.GL.LoadBindings(context);
            
            // 验证OpenGL上下文
            var version = GL.GetString(StringName.Version);
            if (string.IsNullOrEmpty(version))
            {
                var error = new InvalidOperationException("OpenGL上下文不可用");
                ErrorHandler.HandleInitializationException(error, "OpenGL上下文验证");
            }
        }
        
        /// <summary>
        /// 初始化所有资源
        /// </summary>
        private void InitializeResources()
        {
            // 初始化着色器
            InitializeShaders();
            
            // 创建默认纹理
            CreateDefaultTexture();
            
            // 初始化模型渲染器
            _modelRenderer = new ModelRenderer(_defaultTexture);
            
            // 初始化场景渲染器
            if (_modelRenderer != null)
            {
                _sceneRenderer = new SceneRenderer(_shaderManager, _modelRenderer, _gradientBar);
            }
            
            // 初始化梯度条
            InitializeGradientBar();
        }
        
        /// <summary>
        /// 初始化梯度条
        /// </summary>
        private void InitializeGradientBar()
        {
            if (_gradientBar == null)
            {
                _gradientBar = new GradientBar();
            }
            
            _gradientBar.Initialize();
        }

        private void ConfigureOpenGLState()
        {
            // 启用深度测试
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.ClearColor(0.1f, 0.1f, 0.2f, 1.0f);
            
            // 暂时禁用背面剔除进行调试
            GL.Disable(EnableCap.CullFace);
            
            // 启用混合
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        /// <summary>
        /// 初始化着色器
        /// </summary>
        private void InitializeShaders()
        {
            _shaderManager.Initialize();
        }

        private void CreateDefaultTexture()
        {
            _defaultTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _defaultTexture);
            
            // 创建1x1白色纹理作为默认纹理
            byte[] whitePixel = { 255, 255, 255, 255 };
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, whitePixel);
            
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        #endregion

        #region 渲染方法
        /// <summary>
        /// 渲染场景
        /// </summary>
        /// <param name="camera">相机</param>
        /// <param name="models">模型列表</param>
        /// <param name="lights">光源列表</param>
        /// <param name="backgroundColor">背景色</param>
        /// <param name="shadingMode">着色模式</param>
        /// <param name="renderMode">渲染模式</param>
        public void RenderScene(Camera camera, List<Model3D> models, List<Light> lights, Vector3 backgroundColor, ShadingMode shadingMode, RenderMode renderMode)
        {
            if (!_isInitialized) return;
            
            // 清除缓冲区
            GL.ClearColor(backgroundColor.X, backgroundColor.Y, backgroundColor.Z, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            // 设置渲染模式
            SetRenderMode(renderMode);
            
            // 获取着色器程序
            int shaderProgram = _shaderManager.GetShaderProgram(shadingMode);
            if (shaderProgram == 0)
            {
                return; // 如果着色器不存在，跳过渲染
            }
            
            GL.UseProgram(shaderProgram);
            
            // 设置矩阵
            var viewMatrix = camera.GetViewMatrix();
            var projectionMatrix = camera.GetProjectionMatrix();
            
            // Set view and projection matrices
            SetMatrix(shaderProgram, "view", viewMatrix);
            SetMatrix(shaderProgram, "projection", projectionMatrix);
            
            // 渲染所有模型
            foreach (var model in models)
            {
                if (model.Visible)
                {
                    RenderModel(model, shaderProgram);
                }
            }
            
            GL.UseProgram(0);
        }
        
        /// <summary>
        /// 渲染场景（包含坐标轴）
        /// </summary>
        /// <param name="coordinateAxes">可选的坐标轴模型</param>
        /// <param name="miniAxes">可选的迷你坐标轴</param>
        /// <param name="boundingBoxRenderer">可选的包围盒渲染器</param>
        /// <param name="dpiScale">DPI缩放比例</param>
        public void RenderSceneWithAxes(Camera camera, List<Model3D> models, List<Light> lights, Vector3 backgroundColor, ShadingMode shadingMode, RenderMode renderMode, Model3D? coordinateAxes = null, MiniAxes? miniAxes = null, BoundingBoxRenderer? boundingBoxRenderer = null, double dpiScale = 1.0)
        {
            if (!_isInitialized || _sceneRenderer == null) 
            {
                return;
            }
            
            // 委托给SceneRenderer处理复杂的渲染流程
            _sceneRenderer.RenderScene(camera, models, lights, backgroundColor, shadingMode, renderMode, coordinateAxes, miniAxes, boundingBoxRenderer, dpiScale);
        }

        /// <summary>
        /// 渲染单个模型
        /// </summary>
        /// <param name="model">要渲染的模型</param>
        /// <param name="shaderProgram">着色器程序</param>
        private void RenderModel(Model3D model, int shaderProgram)
        {
            // 如果顶点需要更新，更新顶点缓冲区
            if (model.VerticesNeedUpdate)
            {
                _modelRenderer?.UpdateModelVertexBuffer(model);
                model.VerticesNeedUpdate = false;
            }
            
            // 使用ModelRenderer渲染模型
            _modelRenderer?.RenderModel(
                model,
                shaderProgram,
                model.RenderModeOverride ?? _currentRenderMode);
        }

        // CreateModelRenderData和UpdateModelVertexBuffer方法已移至ModelRenderer
        
        // UpdateModelVertexBuffer方法已移至ModelRenderer
        
        #endregion

        #region 辅助方法
        private void SetRenderMode(RenderMode renderMode)
        {
            // 确保内部渲染模式状态被更新（用于在 RenderModel 中切换绘制路径）
            _currentRenderMode = renderMode;
            
            // 检查OpenGL上下文是否可用
            try
            {
                var version = GL.GetString(StringName.Version);
                if (string.IsNullOrEmpty(version))
                {
                    System.Diagnostics.Debug.WriteLine("OpenGL context not available, skipping SetRenderMode");
                    return;
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine("OpenGL context not available, skipping SetRenderMode");
                return;
            }
            
            // 检查OpenGL版本和扩展支持
            if (!IsPolygonModeSupported())
            {
                System.Diagnostics.Debug.WriteLine("PolygonMode not supported in current OpenGL context, using fallback rendering");
                SetRenderModeFallback(renderMode);
                return;
            }

            try
            {
                // 清除之前的OpenGL错误
                GL.GetError();
                
                // 暂时禁用GL.PolygonMode调用以测试是否是崩溃原因
                 System.Diagnostics.Debug.WriteLine($"SetRenderMode called with {renderMode}, but GL.PolygonMode disabled for testing");
                 
                 switch (renderMode)
                 {
                     case RenderMode.Fill:
                         // GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill); // 临时禁用
                         try { GL.Disable(EnableCap.LineSmooth); } catch { }
                         break;
                     case RenderMode.Line:
                         // GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line); // 临时禁用
                         GL.LineWidth(5.0f); // 增加线条粗细以更好显示动画效果
                         // 启用线条平滑以获得更好的视觉效果
                         try
                         {
                             GL.Enable(EnableCap.LineSmooth);
                             GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
                         }
                         catch (Exception ex)
                         {
                             System.Diagnostics.Debug.WriteLine($"LineSmooth error: {ex.Message}");
                         }
                         break;
                     case RenderMode.Point:
                         // GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Point); // 临时禁用
                         GL.PointSize(8.0f); // 增加点的大小以更好显示动画效果
                         try { GL.Enable(EnableCap.ProgramPointSize); } catch { }
                         break;
                 }
                
                // 检查OpenGL错误
                ErrorCode error = GL.GetError();
                if (error != ErrorCode.NoError)
                {
                    System.Diagnostics.Debug.WriteLine($"OpenGL error after PolygonMode: {error}");
                    SetRenderModeFallback(renderMode);
                }
            }
            catch (Exception ex)
            {
                // GL.PolygonMode在OpenGL Core Profile中不被支持
                // 在Windows下可能使用Core Profile，而macOS使用兼容性配置文件
                System.Diagnostics.Debug.WriteLine($"Exception in SetRenderMode: {ex.Message}");
                SetRenderModeFallback(renderMode);
            }
        }
        
        private bool IsPolygonModeSupported()
        {
            try
            {
                // 检查OpenGL版本
                string version = GL.GetString(StringName.Version);
                System.Diagnostics.Debug.WriteLine($"OpenGL Version: {version}");
                
                // 在Core Profile中，PolygonMode通常不被支持
                // 这里我们可以通过检查版本或尝试获取函数指针来判断
                return true; // 先尝试，如果失败再降级
            }
            catch
            {
                return false;
            }
        }
        
        private void SetRenderModeFallback(RenderMode renderMode)
         {
             // 降级处理：只设置线条和点的属性，不使用PolygonMode
             _currentRenderMode = renderMode; // 同步内部渲染模式状态
             switch (renderMode)
             {
                 case RenderMode.Line:
                     GL.LineWidth(5.0f);
                     try
                     {
                         GL.Enable(EnableCap.LineSmooth);
                         GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
                     }
                     catch (Exception ex)
                     {
                         System.Diagnostics.Debug.WriteLine($"LineSmooth not supported: {ex.Message}");
                     }
                     break;
                 case RenderMode.Point:
                     GL.PointSize(8.0f);
                     try { GL.Enable(EnableCap.ProgramPointSize); } catch { }
                     break;
                 case RenderMode.Fill:
                     // Fill模式是默认的，不需要特殊处理
                     try { GL.Disable(EnableCap.LineSmooth); } catch { }
                     break;
             }
         }
         
         private void SetPolygonModeSafe(PolygonMode mode)
          {
              // 检查OpenGL上下文是否可用
              try
              {
                  var version = GL.GetString(StringName.Version);
                  if (string.IsNullOrEmpty(version))
                  {
                      System.Diagnostics.Debug.WriteLine("OpenGL context not available, skipping SetPolygonModeSafe");
                      return;
                  }
              }
              catch (Exception)
              {
                  System.Diagnostics.Debug.WriteLine("OpenGL context not available, skipping SetPolygonModeSafe");
                  return;
              }
              
              if (!IsPolygonModeSupported())
              {
                  System.Diagnostics.Debug.WriteLine($"PolygonMode.{mode} not supported, skipping");
                  return;
              }
             
             try
             {
                 // 清除之前的OpenGL错误
                  GL.GetError();
                  
                  // 临时禁用GL.PolygonMode调用以测试是否是崩溃原因
                  System.Diagnostics.Debug.WriteLine($"SetPolygonModeSafe called with {mode}, but GL.PolygonMode disabled for testing");
                  // GL.PolygonMode(TriangleFace.FrontAndBack, mode); // 临时禁用
                  
                  // 检查OpenGL错误
                  ErrorCode error = GL.GetError();
                  if (error != ErrorCode.NoError)
                  {
                      System.Diagnostics.Debug.WriteLine($"OpenGL error after PolygonMode.{mode}: {error}");
                  }
             }
             catch (Exception ex)
             {
                 System.Diagnostics.Debug.WriteLine($"Exception in SetPolygonModeSafe({mode}): {ex.Message}");
             }
         }

        private void SetMatrix(int shaderProgram, string name, Matrix4 matrix)
        {
            int location = GL.GetUniformLocation(shaderProgram, name);
            if (location != -1)
            {
                GL.UniformMatrix4(location, false, ref matrix);
            }
        }

        private void SetLighting(int shaderProgram, List<Light> lights, Vector3 viewPos)
        {
            // 设置观察位置
            int viewPosLocation = GL.GetUniformLocation(shaderProgram, "viewPos");
            if (viewPosLocation != -1)
            {
                GL.Uniform3(viewPosLocation, viewPos);
            }
            
            // 设置主光源（使用第一个方向光或点光源）
            if (lights.Count > 0)
            {
                var mainLight = lights[0];
                
                int lightColorLocation = GL.GetUniformLocation(shaderProgram, "lightColor");
                if (lightColorLocation != -1)
                {
                    GL.Uniform3(lightColorLocation, mainLight.Color * mainLight.Intensity);
                }
                
                if (mainLight is DirectionalLight dirLight)
                {
                    // 对于方向光，我们将其转换为一个远距离的点光源位置
                    // 这样可以兼容使用lightPos的着色器
                    Vector3 lightPos = viewPos - dirLight.Direction * 100.0f;
                    
                    int lightPosLocation = GL.GetUniformLocation(shaderProgram, "lightPos");
                    if (lightPosLocation != -1)
                    {
                        GL.Uniform3(lightPosLocation, lightPos);
                    }
                    
                    // 同时也设置lightDir，以兼容可能使用方向光的着色器
                    int lightDirLocation = GL.GetUniformLocation(shaderProgram, "lightDir");
                    if (lightDirLocation != -1)
                    {
                        GL.Uniform3(lightDirLocation, dirLight.Direction);
                    }
                }
                else if (mainLight is PointLight pointLight)
                {
                    int lightPosLocation = GL.GetUniformLocation(shaderProgram, "lightPos");
                    if (lightPosLocation != -1)
                    {
                        GL.Uniform3(lightPosLocation, pointLight.Position);
                    }
                }
            }
        }

        private void SetMaterial(int shaderProgram, Material material)
        {
            int ambientLocation = GL.GetUniformLocation(shaderProgram, "materialAmbient");
            if (ambientLocation != -1)
            {
                GL.Uniform3(ambientLocation, material.Ambient);
            }
            
            int diffuseLocation = GL.GetUniformLocation(shaderProgram, "materialDiffuse");
            if (diffuseLocation != -1)
            {
                GL.Uniform3(diffuseLocation, material.Diffuse);
            }
            
            int specularLocation = GL.GetUniformLocation(shaderProgram, "materialSpecular");
            if (specularLocation != -1)
            {
                GL.Uniform3(specularLocation, material.Specular);
            }
            
            int shininessLocation = GL.GetUniformLocation(shaderProgram, "materialShininess");
            if (shininessLocation != -1)
            {
                GL.Uniform1(shininessLocation, material.Shininess);
            }
        }
        #endregion

        #region 清理方法
        /// <summary>
        /// 清理OpenGL资源
        /// </summary>
        public void Cleanup()
        {
            // 清理模型渲染器
            _modelRenderer?.Cleanup();
            
            // 清理着色器程序
            _shaderManager?.Cleanup();
            
            // 清理纹理
            if (_defaultTexture != 0)
            {
                GL.DeleteTexture(_defaultTexture);
                _defaultTexture = 0;
            }
            
            _isInitialized = false;
        }
        #endregion

        
        #region 梯度条控制方法
        /// <summary>
        /// 设置梯度条可见性
        /// </summary>
        /// <param name="visible">是否可见</param>
        public void SetGradientBarVisible(bool visible)
        {
            if (_gradientBar != null)
            {
                _gradientBar.IsVisible = visible;
            }
        }
        
        /// <summary>
        /// 设置梯度条位置
        /// </summary>
        /// <param name="position">位置（左侧或右侧）</param>
        public void SetGradientBarPosition(GradientBarPosition position)
        {

            if (_gradientBar != null)
            {
                _gradientBar.Position = position;

            }
            else
            {

            }
        }
        
        /// <summary>
        /// 设置梯度条的颜色梯度类型
        /// </summary>
        /// <param name="gradientType">梯度类型</param>
        public void SetGradientBarType(ColorGradientType gradientType)
        {
            if (_gradientBar != null)
            {
                _gradientBar.GradientType = gradientType;
            }
        }
        
        /// <summary>
        /// 设置梯度条的数值范围
        /// </summary>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        public void SetGradientBarRange(float minValue, float maxValue)
        {
            if (_gradientBar != null)
            {
                _gradientBar.MinValue = minValue;
                _gradientBar.MaxValue = maxValue;
            }
        }
        
        public void SetGradientBarUseNormalizedScale(bool useNormalized)
        {
            if (_gradientBar != null)
            {
                _gradientBar.UseNormalizedScale = useNormalized;
            }
        }
        
        public void SetGradientBarShowTicks(bool show)
        {
            if (_gradientBar != null)
            {
                _gradientBar.ShowTicks = show;
            }
        }
        
        /// <summary>
        /// 获取梯度条是否可见
        /// </summary>
        /// <returns>是否可见</returns>
        public bool IsGradientBarVisible()
        {
            return _gradientBar?.IsVisible ?? false;
        }
        
        /// <summary>
        /// 获取梯度条位置
        /// </summary>
        /// <returns>梯度条位置</returns>
        public GradientBarPosition GetGradientBarPosition()
        {
            return _gradientBar?.Position ?? GradientBarPosition.Right;
        }
        #endregion
        
        #region IDisposable实现
        public void Dispose()
        {
            // 清理模型渲染器
            _modelRenderer?.Dispose();
            
            // 清理所有着色器程序
            _shaderManager?.Cleanup();
            
            // 清理纹理资源
            if (_defaultTexture != 0)
            {
                GL.DeleteTexture(_defaultTexture);
                _defaultTexture = 0;
            }
            
            _isInitialized = false;
        }
        #endregion
    }
}
