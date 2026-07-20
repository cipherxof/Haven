using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using Avalonia3DControl.Rendering;
using Avalonia3DControl.Core.ErrorHandling;

namespace Avalonia3DControl.UI
{
    /// <summary>
    /// 梯度条位置枚举
    /// </summary>
    public enum GradientBarPosition
    {
        Left,
        Right
    }

    /// <summary>
    /// 梯度条类，用于在OpenGL窗体中显示颜色梯度条
    /// </summary>
    public class GradientBar : IDisposable
    {
        #region 私有字段
        private ShaderManager? _shaderManager;
        private GeometryRenderer? _geometryRenderer;
        private bool _isInitialized = false;
        private bool _disposed = false;
        
        // 渲染状态缓存
        private float _lastLeft, _lastRight, _lastTop, _lastBottom;
        
        // 着色器程序名称常量
        private const string GRADIENT_SHADER = "GradientBar";
        private const string LINE_SHADER = "GradientBarLine";
        
        // 线条渲染资源
        private bool _lineResourcesCreated = false;

        #endregion

        #region 公共属性
        /// <summary>
        /// 梯度条配置
        /// </summary>
        public GradientBarConfiguration Configuration { get; private set; } = new GradientBarConfiguration();

        // 为了保持向后兼容性，提供属性访问器
        /// <summary>
        /// 是否显示梯度条
        /// </summary>
        public bool IsVisible 
        { 
            get => Configuration.IsVisible; 
            set => Configuration.IsVisible = value; 
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        /// <param name="newConfiguration">新配置</param>
        public void UpdateConfiguration(GradientBarConfiguration newConfiguration)
        {
            if (newConfiguration?.IsValid() == true)
            {
                Configuration = newConfiguration.Clone();
            }
        }

        /// <summary>
        /// 获取当前配置的副本
        /// </summary>
        /// <returns>配置副本</returns>
        public GradientBarConfiguration GetConfiguration()
        {
            return Configuration.Clone();
        }

        /// <summary>
        /// 梯度条位置（左侧或右侧）
        /// </summary>
        public GradientBarPosition Position 
        { 
            get => Configuration.Position; 
            set => Configuration.Position = value; 
        }

        /// <summary>
        /// 梯度条宽度（NDC坐标系）
        /// </summary>
        public float Width 
        { 
            get => Configuration.Width; 
            set => Configuration.Width = value; 
        }

        /// <summary>
        /// 梯度条高度（相对于窗口高度的比例）
        /// </summary>
        public float Height 
        { 
            get => Configuration.Height; 
            set => Configuration.Height = value; 
        }

        /// <summary>
        /// 梯度条距离边缘的偏移（相对于窗口宽度的比例）
        /// </summary>
        public float EdgeOffset 
        { 
            get => Configuration.EdgeOffset; 
            set => Configuration.EdgeOffset = value; 
        }

        /// <summary>
        /// 当前颜色梯度类型
        /// </summary>
        public ColorGradientType GradientType 
        { 
            get => Configuration.GradientType; 
            set => Configuration.GradientType = value; 
        }

        /// <summary>
        /// 最小值
        /// </summary>
        public float MinValue 
        { 
            get => Configuration.MinValue; 
            set => Configuration.MinValue = value; 
        }

        /// <summary>
        /// 最大值
        /// </summary>
        public float MaxValue 
        { 
            get => Configuration.MaxValue; 
            set => Configuration.MaxValue = value; 
        }

        /// <summary>
        /// 是否使用归一化刻度（-1~1）；false 则显示实际 Min~Max
        /// </summary>
        public bool UseNormalizedScale 
        { 
            get => Configuration.UseNormalizedScale; 
            set => Configuration.UseNormalizedScale = value; 
        }

        /// <summary>
        /// 是否显示刻度
        /// </summary>
        public bool ShowTicks 
        { 
            get => Configuration.ShowTicks; 
            set => Configuration.ShowTicks = value; 
        }

        /// <summary>
        /// 刻度数量（包含两端），建议为奇数，如5或7
        /// </summary>
        public int TickCount 
        { 
            get => Configuration.TickCount; 
            set => Configuration.TickCount = value; 
        }
        #endregion

        #region 初始化和清理
        /// <summary>
        /// 初始化梯度条
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                // 检查OpenGL上下文是否可用
                try
                {
                    var version = GL.GetString(StringName.Version);
                    if (string.IsNullOrEmpty(version))
                    {
                        _isInitialized = false;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ErrorHandler.HandleRenderingException(ex, "获取OpenGL版本");
                    _isInitialized = false;
                    return;
                }
                
                // 初始化着色器管理器和几何渲染器
                _shaderManager = new ShaderManager();
                _geometryRenderer = new GeometryRenderer();
                
                CreateShaders();
                _geometryRenderer.Initialize();
                if (!_lineResourcesCreated)
                {
                    CreateLineResources();
                }
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleInitializationException(ex, "GradientBar初始化");
                _isInitialized = false;
            }
        }

        private void CreateLineResources()
        {
            // 创建线条着色器（如果还未创建）
            if (_shaderManager != null && !_shaderManager.HasShaderProgram(LINE_SHADER))
            {
                string lineVertexSource = ShaderLoader.LoadGradientBarLineVertexShader();
                string lineFragmentSource = ShaderLoader.LoadGradientBarLineFragmentShader();
                
                _shaderManager.CreateShaderProgram(LINE_SHADER, lineVertexSource, lineFragmentSource);
            }

            _lineResourcesCreated = true;
        }
        #endregion

        #region 着色器与几何体
        private void CreateShaders()
        {
            // 创建梯度条着色器
            string gradientVertexSource = ShaderLoader.LoadGradientBarVertexShader();
            string gradientFragmentSource = ShaderLoader.LoadGradientBarFragmentShader();
            
            var gradientBindings = new Dictionary<int, string>
            {
                { 0, "aPosition" },
                { 1, "aTexCoord" }
            };
            
            _shaderManager?.CreateShaderProgram(GRADIENT_SHADER, gradientVertexSource, 
                gradientFragmentSource, gradientBindings);
        }

        /// <summary>
        /// 设置梯度着色器的uniform变量
        /// </summary>
        /// <param name="shaderProgram">着色器程序ID</param>
        private void SetGradientUniforms(int shaderProgram)
        {
            int gradientTypeLocation = GL.GetUniformLocation(shaderProgram, "gradientType");
            int isSymmetricLocation = GL.GetUniformLocation(shaderProgram, "isSymmetric");
            int minValueLocation = GL.GetUniformLocation(shaderProgram, "minValue");
            int maxValueLocation = GL.GetUniformLocation(shaderProgram, "maxValue");

            GL.Uniform1(gradientTypeLocation, (int)GradientType.BaseType);
            GL.Uniform1(isSymmetricLocation, GradientType.IsSymmetric ? 1 : 0);
            GL.Uniform1(minValueLocation, MinValue);
            GL.Uniform1(maxValueLocation, MaxValue);
        }



        /// <summary>
        /// 创建几何体
        /// </summary>

        #endregion

        #region 渲染方法
        /// <summary>
        /// 渲染梯度条
        /// </summary>
        /// <param name="viewportWidth">视口宽度</param>
        /// <param name="viewportHeight">视口高度</param>
        /// <param name="dpiScale">DPI缩放比例</param>
        public void Render(int viewportWidth, int viewportHeight, double dpiScale = 1.0)
        {
            if (!_isInitialized)
            {
                try { Initialize(); } catch (Exception ex) { ErrorHandler.HandleRenderingException(ex, "渲染时GradientBar初始化"); return; }
                if (!_isInitialized) return;
            }
            
            if (!IsVisible || _geometryRenderer == null)
            {
                return;
            }
            
            using var renderState = new RenderState();
            renderState.SaveState();
            renderState.Setup2DRenderState();

            try
            {
                // 记录并临时覆盖视口，保证梯度条按全屏视口渲染
                bool viewportOverridden = renderState.SetViewport(0, 0, viewportWidth, viewportHeight);

                // 使用梯度条着色器并设置uniform变量
                int gradientProgram = _shaderManager?.GetShaderProgram(GRADIENT_SHADER) ?? 0;
                GL.UseProgram(gradientProgram);
                SetGradientUniforms(gradientProgram);
                
                // 计算位置（每帧计算，便于刻度布局）
                // Width、Height、EdgeOffset都是NDC坐标系的值，不需要DPI缩放
                float barWidth = Width;
                float barHeight = Height; 
                float offsetX = EdgeOffset;

                float left, right;
                if (Position == GradientBarPosition.Left)
                {
                    left = -1.0f + offsetX;
                    right = left + barWidth;
                }
                else
                {
                    right = 1.0f - offsetX;
                    left = right - barWidth;
                }
                // 垂直方向改为居中显示
                float centerY = 0.0f;
                float halfHeight = barHeight * 0.5f;
                float top = centerY + halfHeight;
                float bottom = centerY - halfHeight;

                // 创建并渲染四边形
                var vertices = GeometryRenderer.CreateQuadVertices(left, right, top, bottom);
                _geometryRenderer.RenderQuad(vertices);

                // 记录位置用于刻度线/文字
                _lastLeft = left;
                _lastRight = right;
                _lastTop = top;
                _lastBottom = bottom;

                // 绘制刻度线/标签
                if (ShowTicks)
                {
                    RenderTicksAndLabels();
                }

                // 恢复视口
                if (viewportOverridden)
                {
                    renderState.RestoreViewport();
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleRenderingException(ex, "GradientBar渲染");
            }
        }

        private void RenderTicksAndLabels()
        {
            if (!_lineResourcesCreated) return;

            // 计算刻度参数
            float barWidth = _lastRight - _lastLeft;
            float barHeight = _lastTop - _lastBottom;
            float tickLength = barWidth * 0.5f;
            float labelPadding = barWidth * 0.25f;
            float textHeight = MathF.Min(barHeight * 0.035f, 0.035f);
            float textWidth = textHeight * 0.55f; // 单字符宽度

            int n = Math.Max(2, TickCount);

            // 准备线段数据
            var lineVerts = new List<float>(1024);

            // 刻度线颜色（白色）
            Vector3 tickColor = new Vector3(1f, 1f, 1f);

            // 计算刻度位置与标签
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / (float)(n - 1); // 0..1 顶->底或底->顶？我们定义顶端为1
                float y = Lerp(_lastBottom, _lastTop, t);

                // 画刻度线（根据位置选择内侧）
                float x1, x2;
                if (Position == GradientBarPosition.Right)
                {
                    x1 = _lastLeft - 0.002f; // 稍微离开条一点
                    x2 = x1 - tickLength;
                }
                else
                {
                    x1 = _lastRight + 0.002f;
                    x2 = x1 + tickLength;
                }
                // 添加线段
                lineVerts.Add(x1); lineVerts.Add(y);
                lineVerts.Add(x2); lineVerts.Add(y);

                // 计算标签文本
                string label = FormatTickLabel(1.0f - t); // 顶部为1，底部为-1 或 Min/Max

                // 文字绘制起点
                float textStartX;
                if (Position == GradientBarPosition.Right)
                {
                    textStartX = x2 - labelPadding - label.Length * textWidth;
                }
                else
                {
                    textStartX = x2 + labelPadding;
                }
                float textBaselineY = y - textHeight * 0.5f; // 垂直居中

                // 将字符线段添加到lineVerts
                CharacterRenderer.AddTextLineSegments(lineVerts, label, textStartX, textBaselineY, textWidth, textHeight);
            }

            // 提交并绘制
            if (lineVerts.Count > 0)
            {
                int lineProgram = _shaderManager?.GetShaderProgram(LINE_SHADER) ?? 0;
                GL.UseProgram(lineProgram);
                int colorLoc = GL.GetUniformLocation(lineProgram, "uColor");
                GL.Uniform3(colorLoc, new Vector3(1f,1f,1f));

                _geometryRenderer?.RenderLines(lineVerts.ToArray(), lineVerts.Count / 2);
            }
        }

        private string FormatTickLabel(float normalizedTopToBottom)
        {
            // normalizedTopToBottom: 顶部1 -> 底部0
            float v;
            if (UseNormalizedScale)
            {
                // 映射到 [-1, 1]
                v = 1f - normalizedTopToBottom * 2f;
                // 为了整洁，常用值保留一位小数
                if (MathF.Abs(v) < 1e-4f) v = 0f;
                return v.ToString("0.0");
            }
            else
            {
                // 映射到 [Max, Min]
                v = Lerp(MaxValue, MinValue, normalizedTopToBottom);
                return v.ToString("0.00");
            }
        }

        // 字符渲染现在由CharacterRenderer类处理
        #endregion

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        #region IDisposable实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_isInitialized)
                {
                    // 清理着色器管理器
                    _shaderManager?.Dispose();
                    _shaderManager = null;
                    // 清理几何渲染器
                    _geometryRenderer?.Dispose();
                    _geometryRenderer = null;
                }
                _disposed = true;
            }
        }

        ~GradientBar()
        {
            Dispose(false);
        }
        #endregion
    }
}
