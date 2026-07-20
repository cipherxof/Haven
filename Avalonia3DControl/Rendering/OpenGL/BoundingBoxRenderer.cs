using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.UI;

namespace Avalonia3DControl.Rendering.OpenGL
{
    /// <summary>
    /// 包围盒渲染器，负责渲染模型的外接矩形包围盒和坐标轴刻度
    /// </summary>
    public class BoundingBoxRenderer
    {
        private uint _vao;
        private uint _vbo;
        private bool _isInitialized = false;
        // 仅控制"0"数字标签的绘制次数：
        // - 每帧在 RenderAxisTicks 开头重置为 false
        // - AddSingleAxisTicks 中遇到 value==0 且尚未绘制时，调用 AddTickLabel 后置为 true
        // 其它非 0 刻度的标签总是绘制（不受该标志限制）
        private bool _zeroLabelDrawn = false; // 每帧仅绘制一次"0"标签
        
        /// <summary>
        /// 轴标签渲染器
        /// </summary>
        private readonly AxisLabelRenderer _axisLabelRenderer = new AxisLabelRenderer();
        
        /// <summary>
        /// 是否显示包围盒
        /// </summary>
        public bool Visible { get; set; } = false;
        
        /// <summary>
        /// 是否显示坐标轴刻度
        /// </summary>
        public bool ShowAxisTicks { get; set; } = true;
        
        /// <summary>
        /// 包围盒线条颜色
        /// </summary>
        public Vector3 LineColor { get; set; } = new Vector3(1.0f, 1.0f, 0.0f); // 黄色
        
        /// <summary>
        /// 包围盒线条宽度
        /// </summary>
        public float LineWidth { get; set; } = 4.0f;
        
        /// <summary>
        /// 坐标轴刻度颜色
        /// </summary>
        public Vector3 TickColor { get; set; } = new Vector3(1.0f, 1.0f, 1.0f); // 白色，更明显
        
        /// <summary>
        /// 刻度线长度
        /// </summary>
        public float TickLength { get; set; } = 0.2f;
        /// <summary>
        /// 刻度字体缩放
        /// </summary>
        public float LabelScale { get; set; } = 3.0f;
        /// <summary>
        /// 标签线宽（用于数字字体线段）
        /// </summary>
        public float LabelLineWidth { get; set; } = 10.0f;
        
        /// <summary>
        /// 获取OpenGL支持的线宽范围信息
        /// </summary>
        /// <returns>返回支持的最小和最大线宽</returns>
        public (float Min, float Max) GetSupportedLineWidthRange()
        {
            float[] lineWidthRange = new float[2];
            GL.GetFloat(GetPName.AliasedLineWidthRange, lineWidthRange);
            return (lineWidthRange[0], lineWidthRange[1]);
        }
        
        /// <summary>
        /// 初始化渲染器
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            // 生成VAO和VBO
            GL.GenVertexArrays(1, out _vao);
            GL.GenBuffers(1, out _vbo);
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// 添加刻度数字标签
        /// </summary>
        private void AddTickLabel(List<float> labelVertices, Vector3 tickPos, Vector3 direction, Vector3 perpendicular, float value, Vector3 color, Matrix4 view, float sceneSize, float actualTickLength, float axisRadius)
        {
            // 智能格式化数值（支持非0刻度显示标签）
            string valueText = FormatTickValue(value);

            
            // 根据场景大小计算自适应的标签缩放，并叠加用户可调缩放因子 LabelScale
            float adaptiveLabelScale = Math.Max(sceneSize * 0.015f, 0.5f) * Math.Max(0.1f, LabelScale);
            
            // 规范化与稳定的垂直方向（与刻度线保持一致）
            Vector3 perp = perpendicular.LengthSquared > 1e-8f ? perpendicular.Normalized() : perpendicular;
        
            // 基于轴半径与刻度长度，先确定刻度末端，再将标签放在刻度末端外侧一些（从轴表面出发）
            Vector3 tickStartOnSurface = tickPos + perp * axisRadius; // 从轴表面开始
            Vector3 tickEnd = tickStartOnSurface + perp * actualTickLength; // 刻度末端（外侧）
        
            // 标签基准位置：刻度末端再沿同向外移一小段
            float labelMargin = actualTickLength * 0.15f; // 适度外移，避免与刻度线重叠
            Vector3 labelPos = tickEnd + perp * labelMargin;
        
            // 计算相机朝向向量（将文本线段转换为始终朝向相机的3D线段）
            var invView = view.Inverted();
            Vector3 camRight = invView.Column0.Xyz.Normalized(); // 相机右方向（世界坐标）
            Vector3 camUp = invView.Column1.Xyz.Normalized();    // 相机上方向（世界坐标）
            Vector3 camPos = invView.Column3.Xyz;                // 相机位置（世界坐标）
            
            // 将标签沿着朝向相机的方向轻微前移，避免与包围盒/刻度发生Z冲突
            var toCam = (camPos - labelPos).Normalized();
            labelPos += toCam * (actualTickLength * 0.10f);
            
            // 生成文本的局部2D线段（起点从0,0开始）
            var tempVerts = new List<float>();
            float charWidth = actualTickLength * 0.60f * adaptiveLabelScale;
            float charHeight = actualTickLength * 0.85f * adaptiveLabelScale;
            CharacterRenderer.AddTextLineSegments(tempVerts, valueText, 0.0f, 0.0f, charWidth, charHeight);
            
            for (int i = 0; i < tempVerts.Count; i += 4)
            {
                var x1 = tempVerts[i];
                var y1 = tempVerts[i + 1];
                var x2 = tempVerts[i + 2];
                var y2 = tempVerts[i + 3];
       
                Vector3 p1 = labelPos + camRight * x1 + camUp * y1;
                Vector3 p2 = labelPos + camRight * x2 + camUp * y2;
       
                labelVertices.AddRange(new float[] {
                    p1.X, p1.Y, p1.Z, color.X, color.Y, color.Z,
                    p2.X, p2.Y, p2.Z, color.X, color.Y, color.Z
                });
            }
        }
        
        /// <summary>
        /// 智能格式化刻度值
        /// </summary>
        private string FormatTickValue(float value)
        {
            // 只有原点才显示为 0：对极小值进行零钳制，避免把非零误显示为 0
            const float zeroEps = 1e-6f;
            float abs = MathF.Abs(value);
            if (abs < zeroEps)
                return "0";
            
            // 对接近整数（且绝对值较大）的值显示为整数，避免 2.00 这类
            float rounded = MathF.Round(value);
            if (abs >= 1.0f && MathF.Abs(value - rounded) < 1e-4f)
            {
                return rounded.ToString("0");
            }
            
            // 根据数值范围选择小数精度（避免把小数误显示为 0）
            if (abs >= 1000f)
                return value.ToString("F0");
            if (abs >= 100f)
                return value.ToString("F1");
            if (abs >= 10f)
                return value.ToString("F1");
            if (abs >= 1f)
                return value.ToString("F2");
            if (abs >= 0.1f)
                return value.ToString("F3");
            if (abs >= 0.01f)
                return value.ToString("F4");
            if (abs >= 0.001f)
                return value.ToString("F5");
            
            // 极小但非零，保留更多小数，避免显示为 0
            return value.ToString("F6");
        }
        
        /// <summary>
        /// 渲染包围盒和坐标轴刻度
        /// </summary>
        /// <param name="models">模型列表</param>
        /// <param name="shaderProgram">着色器程序</param>
        /// <param name="view">视图矩阵</param>
        /// <param name="projection">投影矩阵</param>
        public void Render(List<Model3D> models, int shaderProgram, Matrix4 view, Matrix4 projection)
        {
            if (!Visible || !_isInitialized || models.Count == 0)
                return;
            
            // 计算所有模型的总包围盒
            var (min, max) = CalculateSceneBoundingBox(models);
            
            // 计算坐标轴的起点：平移到包围盒的一角，而不是中心点
            var size = max - min;
            var center = (min + max) * 0.5f;
            // 将坐标轴起点平移到包围盒的最小角，而不是中心点
            var originCorner = min;
            
            // 调试输出
            Console.WriteLine($"Render方法中 - 原始包围盒 min: X={min.X}, Y={min.Y}, Z={min.Z}");
            Console.WriteLine($"Render方法中 - 原始包围盒 max: X={max.X}, Y={max.Y}, Z={max.Z}");
            Console.WriteLine($"Render方法中 - 计算的坐标轴起点: X={originCorner.X}, Y={originCorner.Y}, Z={originCorner.Z}");
            
            // 渲染包围盒（使用原始包围盒坐标）
            RenderBoundingBox(min, max, shaderProgram, view, projection);
            
            // 渲染坐标轴刻度（使用平移后的坐标轴起点）
            if (ShowAxisTicks)
            {
                RenderAxisTicks(min, max, originCorner, shaderProgram, view, projection);
            }
        }
        
        /// <summary>
        /// 计算场景中所有模型的总包围盒
        /// </summary>
        private (Vector3 Min, Vector3 Max) CalculateSceneBoundingBox(List<Model3D> models)
        {
            var sceneMin = new Vector3(float.MaxValue);
            var sceneMax = new Vector3(float.MinValue);
            
            foreach (var model in models)
            {
                var (modelMin, modelMax) = model.GetBoundingBox();
                sceneMin = Vector3.ComponentMin(sceneMin, modelMin);
                sceneMax = Vector3.ComponentMax(sceneMax, modelMax);
            }
            
            // 确保包围盒有一定的大小，避免模型过小或单点模型导致渲染问题
            var size = sceneMax - sceneMin;
            if (size.X < 0.001f) { sceneMax.X += 0.5f; sceneMin.X -= 0.5f; }
            if (size.Y < 0.001f) { sceneMax.Y += 0.5f; sceneMin.Y -= 0.5f; }
            if (size.Z < 0.001f) { sceneMax.Z += 0.5f; sceneMin.Z -= 0.5f; }
            
            return (sceneMin, sceneMax);
        }
        
        /// <summary>
        /// 渲染包围盒线框
        /// </summary>
        private void RenderBoundingBox(Vector3 min, Vector3 max, int shaderProgram, Matrix4 view, Matrix4 projection)
        {
            // 计算坐标轴的起点：X和Z方向平移尺寸的一半，Y方向保持不变
            var size = max - min;
            var originCorner = new Vector3(min.X + size.X * 0.5f, min.Y, min.Z + size.Z * 0.5f);
            
            // 调试输出
            Console.WriteLine($"RenderBoundingBox方法中 - 包围盒 min: X={min.X}, Y={min.Y}, Z={min.Z}");
            Console.WriteLine($"RenderBoundingBox方法中 - 包围盒 max: X={max.X}, Y={max.Y}, Z={max.Z}");
            Console.WriteLine($"RenderBoundingBox方法中 - 坐标轴起点: X={originCorner.X}, Y={originCorner.Y}, Z={originCorner.Z}");
            
            // 不再平移包围盒，保持原始位置
            // 创建包围盒的12条边的顶点数据，使用原始包围盒坐标
            var vertices = new float[]
            {
                // 底面4条边
                min.X, min.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                max.X, min.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                max.X, min.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                max.X, min.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                max.X, min.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                min.X, min.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                min.X, min.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                min.X, min.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                // 顶面4条边
                min.X, max.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                max.X, max.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                max.X, max.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                max.X, max.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                max.X, max.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                min.X, max.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                min.X, max.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                min.X, max.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                // 4条竖直边
                min.X, min.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                min.X, max.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                max.X, min.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                max.X, max.Y, min.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                max.X, min.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                max.X, max.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                
                min.X, min.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z,
                min.X, max.Y, max.Z, LineColor.X, LineColor.Y, LineColor.Z
            };
            
            RenderLines(vertices, shaderProgram, view, projection);
        }
        
        /// <summary>
        /// 渲染坐标轴刻度（从指定起点开始显示三条轴）
        /// </summary>
        /// <param name="originCorner">坐标轴的起点</param>
        private void RenderAxisTicks(Vector3 min, Vector3 max, Vector3 originCorner, int shaderProgram, Matrix4 view, Matrix4 projection)
        {
            var tickVertices = new List<float>();
            var labelVertices = new List<float>();
            _zeroLabelDrawn = false; // 新一帧开始，重置仅绘制一次标志
            
            // 查询OpenGL支持的线宽范围
            float[] lineWidthRange = new float[2];
            GL.GetFloat(GetPName.AliasedLineWidthRange, lineWidthRange);
            float maxSupportedLineWidth = lineWidthRange[1];
            
            // 计算场景大小用于自适应缩放
            var size = max - min;
            var center = (min + max) * 0.5f; // 模型中心

            // 调试输出包围盒信息
            Console.WriteLine($"包围盒 min: X={min.X}, Y={min.Y}, Z={min.Z}");
            Console.WriteLine($"包围盒 max: X={max.X}, Y={max.Y}, Z={max.Z}");
            Console.WriteLine($"包围盒 size: X={size.X}, Y={size.Y}, Z={size.Z}");
            Console.WriteLine($"传入的坐标轴起点: X={originCorner.X}, Y={originCorner.Y}, Z={originCorner.Z}");

            // 计算场景大小用于自适应缩放
            float sceneSize = size.Length; // 使用包围盒对角线长度作为场景大小

            // 创建实体轴的三角形顶点数据
            var solidAxisVertices = new List<float>();
            float axisRadius = Math.Max(sceneSize * 0.005f, 0.02f); // 轴的半径，基于场景大小
            
            // 使用传入的坐标轴起点，不再重新计算

            // 重置标志，确保0标签会被绘制
            _zeroLabelDrawn = false;
            
            // 不在此处添加0标签，让AddSingleAxisTicks方法处理0刻度标签
            // 这样可以确保0标签位置与实际0刻度位置一致

            // 计算坐标轴的长度（使用size而不是直接使用max-min）
            var halfSize = size * 0.5f;
            
            // X轴（沿 +X，从originCorner出发，长度为size.X）
            var xTickColor = new Vector3(1.0f, 0.0f, 0.0f);
            var xStart = originCorner;
            var xEnd = new Vector3(originCorner.X + size.X, originCorner.Y, originCorner.Z);
            float xLen = size.X;
            CreateSolidAxis(solidAxisVertices, xStart, xEnd, axisRadius, xTickColor);
            // 使用0到size.X作为刻度值范围，保持与平移前一致
            AddSingleAxisTicks(tickVertices, labelVertices, xStart, Vector3.UnitX, xLen, xTickColor, axisRadius, 0, size.X, view, sceneSize);
            
            // Y轴（沿 +Y，从originCorner出发，长度为size.Y）
            var yTickColor = new Vector3(0.0f, 1.0f, 0.0f);
            var yStart = originCorner;
            var yEnd = new Vector3(originCorner.X, originCorner.Y + size.Y, originCorner.Z); // 修改Y轴终点，使其基于originCorner
            float yLen = size.Y;
            CreateSolidAxis(solidAxisVertices, yStart, yEnd, axisRadius, yTickColor);
            // 使用0到size.Y作为刻度值范围，保持与平移前一致
            AddSingleAxisTicks(tickVertices, labelVertices, yStart, Vector3.UnitY, yLen, yTickColor, axisRadius, 0, size.Y, view, sceneSize);
        
            // Z轴（沿 +Z，从originCorner出发，长度为size.Z）
            var zTickColor = new Vector3(0.0f, 0.0f, 1.0f);
            var zStart = originCorner;
            var zEnd = new Vector3(originCorner.X, originCorner.Y, originCorner.Z + size.Z);
            float zLen = size.Z;
            CreateSolidAxis(solidAxisVertices, zStart, zEnd, axisRadius, zTickColor);
            // 使用0到size.Z作为刻度值范围，保持与平移前一致
            AddSingleAxisTicks(tickVertices, labelVertices, zStart, Vector3.UnitZ, zLen, zTickColor, axisRadius, 0, size.Z, view, sceneSize);
            
            // 调试输出坐标轴信息
            Console.WriteLine($"X轴: 起点({xStart.X}, {xStart.Y}, {xStart.Z}), 终点({xEnd.X}, {xEnd.Y}, {xEnd.Z}), 长度{xLen}");
            Console.WriteLine($"Y轴: 起点({yStart.X}, {yStart.Y}, {yStart.Z}), 终点({yEnd.X}, {yEnd.Y}, {yEnd.Z}), 长度{yLen}");
            Console.WriteLine($"Z轴: 起点({zStart.X}, {zStart.Y}, {zStart.Z}), 终点({zEnd.X}, {zEnd.Y}, {zEnd.Z}), 长度{zLen}");

            // 坐标轴交点标签已在方法开头处理

            // 在原点添加小圆球（放在 min 角上）
            // var originSphereVertices = new List<float>();
            // float sphereRadius = Math.Max(size.Length * 0.008f, 0.03f); // 球体半径，稍大于轴半径
            // var sphereColor = new Vector3(0.8f, 0.8f, 0.8f); // 灰白色
            // CreateOriginSphere(originSphereVertices, originCorner, sphereRadius, sphereColor);
            
            // 渲染实体轴（使用三角形）- 禁用深度测试确保始终可见
            if (solidAxisVertices.Count > 0)
            {
                GL.Disable(EnableCap.DepthTest);
                GL.DepthMask(false);
                RenderTriangles(solidAxisVertices.ToArray(), shaderProgram, view, projection);
                GL.DepthMask(true);
                GL.Enable(EnableCap.DepthTest);
            }
            
            // 渲染原点球体（已移除，避免与"0"概念混淆）
            // if (originSphereVertices.Count > 0)
            // {
            //     GL.Enable(EnableCap.PolygonOffsetFill);
            //     GL.PolygonOffset(1.25f, 1.0f);
            //     RenderTriangles(originSphereVertices.ToArray(), shaderProgram, view, projection);
            //     GL.Disable(EnableCap.PolygonOffsetFill);
            // }
            
            // 渲染轴标签（X、Y、Z）- 禁用深度测试确保始终可见
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);
            RenderAxisLabels(xEnd, yEnd, zEnd, axisRadius, shaderProgram, view, projection);
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
        
            // 渲染刻度线（使用更粗的线宽以提高可见性）- 禁用深度测试确保始终可见
            if (tickVertices.Count > 0)
            {
                GL.Disable(EnableCap.DepthTest);
                GL.DepthMask(false);
                float tickLineWidth = MathF.Max(LineWidth, 4.0f);
                RenderLines(tickVertices.ToArray(), shaderProgram, view, projection, tickLineWidth);
                GL.DepthMask(true);
                GL.Enable(EnableCap.DepthTest);
            }
        
            // 渲染"0"数字标签（使用更粗的线宽）——为了可见性，禁用深度测试并关闭深度写入
            if (labelVertices.Count > 0)
            {
                GL.Disable(EnableCap.DepthTest);
                GL.DepthMask(false);
                
                // 使用支持的最大线宽，但不超过设定的LabelLineWidth
                float effectiveLabelLineWidth = Math.Min(LabelLineWidth, maxSupportedLineWidth);
                
                // 如果支持的线宽太小（小于2），则使用几何方法绘制粗线条
                if (maxSupportedLineWidth < 2.0f && LabelLineWidth > 1.0f)
                {
                    RenderThickLinesAsQuads(labelVertices.ToArray(), shaderProgram, view, projection, LabelLineWidth);
                }
                else
                {
                    RenderLines(labelVertices.ToArray(), shaderProgram, view, projection, effectiveLabelLineWidth);
                }
                
                GL.DepthMask(true);
                GL.Enable(EnableCap.DepthTest);
            }
        }
        
        /// <summary>
        /// 计算合适的刻度值（减少刻度数量）
        /// </summary>
        private List<float> CalculateNiceTickValues(float min, float max, int targetCount)
        {
            var tickValues = new List<float>();
            

            
            if (Math.Abs(max - min) < 0.001f)
            {
                tickValues.Add(min);
                return tickValues;
            }
            
            // 减少目标刻度数量，使显示更简洁
            targetCount = 3; // 减少为3个刻度点
            
            // 计算合适的刻度间隔
            float range = max - min;
            float roughStep = range / (targetCount - 1);
            
            // 找到合适的步长（使用10的幂次）
            float magnitude = (float)Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
            float normalizedStep = roughStep / magnitude;
            
            float niceStep;
            if (normalizedStep <= 1.0f)
                niceStep = 1.0f * magnitude;
            else if (normalizedStep <= 2.0f)
                niceStep = 2.0f * magnitude;
            else if (normalizedStep <= 5.0f)
                niceStep = 5.0f * magnitude;
            else
                niceStep = 10.0f * magnitude;
            
            // 计算起始点（向下取整到最近的步长倍数）
            float niceMin = (float)(Math.Floor(min / niceStep) * niceStep);
            float niceMax = (float)(Math.Ceiling(max / niceStep) * niceStep);
            
            // 生成刻度值
            for (float tick = niceMin; tick <= niceMax + niceStep * 0.001f; tick += niceStep)
            {
                if (tick >= min - niceStep * 0.001f && tick <= max + niceStep * 0.001f)
                {
                    tickValues.Add((float)Math.Round(tick, 6)); // 避免浮点精度问题
                }
            }
            
            // 确保包含0点（如果在范围内）
            if (min < 0 && max > 0 && !tickValues.Any(v => Math.Abs(v) < 0.001f))
            {
                tickValues.Add(0.0f);
                tickValues = tickValues.OrderBy(v => v).ToList();
            }
            

            
            return tickValues;
        }
        
        /// <summary>
        /// 添加单个轴的刻度线和数字标签（只在一条边上显示）
        /// </summary>
        private void AddSingleAxisTicks(List<float> tickVertices, List<float> labelVertices, Vector3 origin, Vector3 direction, float length, Vector3 color, float axisRadius, float minValue, float maxValue, Matrix4 view, float sceneSize)
        {
            // 计算合适的刻度值
            var tickValues = CalculateNiceTickValues(minValue, maxValue, 5);
            
            // 如果已经绘制了0标签，则从刻度值列表中移除0
            if (_zeroLabelDrawn)
            {
                tickValues.RemoveAll(v => Math.Abs(v) < 0.001f);
            }

            // 根据轴长度自适应刻度线长度，避免在大模型下过短不可见
            float tickLen = MathF.Max(TickLength, 0.03f * MathF.Max(0.0001f, length));

            // 为每条轴选择一个稳定且与包围盒"外侧"一致的法向方向，避免随相机旋转而改变
            Vector3 perpendicularBase;
            if (direction == Vector3.UnitX)
                perpendicularBase = -Vector3.UnitZ;       // X 轴刻度朝向-Z方向（向前）
            else if (direction == Vector3.UnitY)
                perpendicularBase = -Vector3.UnitZ;       // Y 轴刻度也朝向-Z方向（向前）
            else // Z 轴
                perpendicularBase = -Vector3.UnitY;       // Z 轴刻度朝下

            float denom = MathF.Max(0.0f, maxValue - minValue);

            foreach (float value in tickValues)
            {
                // 计算刻度在轴上的位置
                float t = (denom > 1e-8f) ? length * (value - minValue) / denom : 0.0f;
                var tickPos = origin + direction * t;

                // 使用稳定的垂直方向
                var perpendicular = perpendicularBase; // 已是单位向量

                // 刻度线：从轴表面向外延伸（以轴半径为起点），确保视觉上“贴”在轴上
                var tickStart = tickPos + perpendicular * axisRadius; 
                var tickEnd = tickStart + perpendicular * tickLen;    // 向外延伸

                // 添加刻度线
                tickVertices.AddRange(new float[] {
                    tickStart.X, tickStart.Y, tickStart.Z, color.X, color.Y, color.Z,
                    tickEnd.X, tickEnd.Y, tickEnd.Z, color.X, color.Y, color.Z
                });

                // 特别标记0点：总是在原点位置显示0点标记和标签
                bool isZeroPoint = Math.Abs(value) < 0.001f;
                if (isZeroPoint)
                {
                    // 0点十字标记：使用与刻度正交的第二个方向
                    Vector3 perpendicular2 = Vector3.Cross(direction, perpendicular).Normalized();
                    var zeroMark1Start = tickPos - perpendicular2 * tickLen * 0.3f;
                    var zeroMark1End = tickPos + perpendicular2 * tickLen * 0.3f;
                
                    tickVertices.AddRange(new float[] {
                        zeroMark1Start.X, zeroMark1Start.Y, zeroMark1Start.Z, color.X, color.Y, color.Z,
                        zeroMark1End.X, zeroMark1End.Y, zeroMark1End.Z, color.X, color.Y, color.Z
                    });
                
                    if (!_zeroLabelDrawn)
                    {
                        // 0刻度使用白色显示
                        Vector3 whiteColor = new Vector3(1.0f, 1.0f, 1.0f);
                        AddTickLabel(labelVertices, tickPos, direction, perpendicular, value, whiteColor, view, sceneSize, tickLen, axisRadius);
                        _zeroLabelDrawn = true;
                    }
                }
                else
                {
                    // 为非 0 刻度添加数字标签，包括正值和负值
                    AddTickLabel(labelVertices, tickPos, direction, perpendicular, value, color, view, sceneSize, tickLen, axisRadius);
                }
            }
        }
        
        /// <summary>
        /// 渲染线条
        /// </summary>
        private void RenderLines(float[] vertices, int shaderProgram, Matrix4 view, Matrix4 projection)
        {
            // 保留原有签名，使用默认线宽渲染（用于包围盒和刻度线）
            RenderLines(vertices, shaderProgram, view, projection, LineWidth);
        }
        
        /// <summary>
        /// 使用四边形渲染粗线条（解决OpenGL线宽限制问题）
        /// </summary>
        private void RenderThickLinesAsQuads(float[] vertices, int shaderProgram, Matrix4 view, Matrix4 projection, float lineWidth)
        {
            if (vertices.Length < 12) return; // 至少需要两个顶点（每个顶点6个float：xyz + rgb）
            
            var quadVertices = new List<float>();
            
            // 将线段转换为四边形
            for (int i = 0; i < vertices.Length; i += 12) // 每条线段12个float（2个顶点 * 6个float）
            {
                if (i + 11 >= vertices.Length) break;
                
                // 提取线段的两个端点
                var p1 = new Vector3(vertices[i], vertices[i + 1], vertices[i + 2]);
                var color1 = new Vector3(vertices[i + 3], vertices[i + 4], vertices[i + 5]);
                var p2 = new Vector3(vertices[i + 6], vertices[i + 7], vertices[i + 8]);
                var color2 = new Vector3(vertices[i + 9], vertices[i + 10], vertices[i + 11]);
                
                // 计算线段方向和垂直方向
                var direction = (p2 - p1).Normalized();
                var perpendicular = Vector3.Cross(direction, (view.Inverted().Column2.Xyz)).Normalized();
                var halfWidth = perpendicular * (lineWidth * 0.001f); // 转换为世界坐标单位
                
                // 创建四边形的四个顶点
                var v1 = p1 - halfWidth;
                var v2 = p1 + halfWidth;
                var v3 = p2 + halfWidth;
                var v4 = p2 - halfWidth;
                
                // 添加两个三角形组成四边形
                // 第一个三角形: v1, v2, v3
                quadVertices.AddRange(new float[] { v1.X, v1.Y, v1.Z, color1.X, color1.Y, color1.Z });
                quadVertices.AddRange(new float[] { v2.X, v2.Y, v2.Z, color1.X, color1.Y, color1.Z });
                quadVertices.AddRange(new float[] { v3.X, v3.Y, v3.Z, color2.X, color2.Y, color2.Z });
                
                // 第二个三角形: v1, v3, v4
                quadVertices.AddRange(new float[] { v1.X, v1.Y, v1.Z, color1.X, color1.Y, color1.Z });
                quadVertices.AddRange(new float[] { v3.X, v3.Y, v3.Z, color2.X, color2.Y, color2.Z });
                quadVertices.AddRange(new float[] { v4.X, v4.Y, v4.Z, color2.X, color2.Y, color2.Z });
            }
            
            if (quadVertices.Count > 0)
            {
                RenderTriangles(quadVertices.ToArray(), shaderProgram, view, projection);
            }
        }
        
        /// <summary>
        /// 渲染三角形
        /// </summary>
        private void RenderTriangles(float[] vertices, int shaderProgram, Matrix4 view, Matrix4 projection)
        {
            GL.UseProgram(shaderProgram);
            
            // 设置与通用着色器一致的矩阵uniform
            SetMatrix(shaderProgram, "model", Matrix4.Identity);
            SetMatrix(shaderProgram, "view", view);
            SetMatrix(shaderProgram, "projection", projection);
            
            // 绑定VAO和VBO
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            
            // 上传顶点数据
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
            
            // 设置顶点属性
            // 位置属性 (location = 0)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            
            // 颜色属性 (location = 1)
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            
            // 渲染三角形
            GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Length / 6);
            
            // 清理
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }
        private void RenderLines(float[] vertices, int shaderProgram, Matrix4 view, Matrix4 projection, float lineWidth)
        {
            GL.UseProgram(shaderProgram);
            
            // 设置与通用着色器一致的矩阵uniform
            SetMatrix(shaderProgram, "model", Matrix4.Identity);
            SetMatrix(shaderProgram, "view", view);
            SetMatrix(shaderProgram, "projection", projection);
            
            // 绑定VAO和VBO
            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            
            // 上传顶点数据
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
            
            // 设置顶点属性
            // 位置属性 (location = 0)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            
            // 颜色属性 (location = 1)
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            
            // 设置线条宽度
            GL.LineWidth(lineWidth);
            
            // 渲染线条
            GL.DrawArrays(PrimitiveType.Lines, 0, vertices.Length / 6);
            
            // 清理
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }
        
        /// <summary>
        /// 创建实体轴的圆柱体几何体（带箭头）
        /// </summary>
        /// <param name="vertices">顶点列表</param>
        /// <param name="start">起始点</param>
        /// <param name="end">结束点</param>
        /// <param name="radius">半径</param>
        /// <param name="color">颜色</param>
        private void CreateSolidAxis(List<float> vertices, Vector3 start, Vector3 end, float radius, Vector3 color)
        {
            const int segments = 8; // 圆柱体的分段数
            var direction = (end - start).Normalized();
            var length = (end - start).Length;
            
            // 箭头参数
            float arrowLength = Math.Min(length * 0.15f, radius * 4.0f); // 箭头长度
            float arrowRadius = radius * 2.0f; // 箭头底部半径
            
            // 调整圆柱体长度，为箭头留出空间
            Vector3 cylinderEnd = end - direction * arrowLength;
            
            // 找到两个垂直于轴方向的向量
            Vector3 up = Math.Abs(direction.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitZ;
            Vector3 right = Vector3.Cross(direction, up).Normalized();
            up = Vector3.Cross(right, direction).Normalized();
            
            // 生成圆柱体的顶点
            var startCircle = new Vector3[segments];
            var cylinderEndCircle = new Vector3[segments];
            var arrowBaseCircle = new Vector3[segments];
            
            for (int i = 0; i < segments; i++)
            {
                float angle = 2.0f * MathF.PI * i / segments;
                Vector3 cylinderOffset = (right * MathF.Cos(angle) + up * MathF.Sin(angle)) * radius;
                Vector3 arrowOffset = (right * MathF.Cos(angle) + up * MathF.Sin(angle)) * arrowRadius;
                
                startCircle[i] = start + cylinderOffset;
                cylinderEndCircle[i] = cylinderEnd + cylinderOffset;
                arrowBaseCircle[i] = cylinderEnd + arrowOffset;
            }
            
            // 生成圆柱体侧面的三角形
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                
                // 第一个三角形
                AddTriangleVertex(vertices, startCircle[i], color);
                AddTriangleVertex(vertices, cylinderEndCircle[i], color);
                AddTriangleVertex(vertices, startCircle[next], color);
                
                // 第二个三角形
                AddTriangleVertex(vertices, startCircle[next], color);
                AddTriangleVertex(vertices, cylinderEndCircle[i], color);
                AddTriangleVertex(vertices, cylinderEndCircle[next], color);
            }
            
            // 生成起始端面
            Vector3 startCenter = start;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                AddTriangleVertex(vertices, startCenter, color);
                AddTriangleVertex(vertices, startCircle[next], color);
                AddTriangleVertex(vertices, startCircle[i], color);
            }
            
            // 生成圆柱体与箭头连接处的端面
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                AddTriangleVertex(vertices, cylinderEndCircle[i], color);
                AddTriangleVertex(vertices, cylinderEndCircle[next], color);
                AddTriangleVertex(vertices, arrowBaseCircle[i], color);
                
                AddTriangleVertex(vertices, cylinderEndCircle[next], color);
                AddTriangleVertex(vertices, arrowBaseCircle[next], color);
                AddTriangleVertex(vertices, arrowBaseCircle[i], color);
            }
            
            // 生成箭头圆锥体
            Vector3 arrowTip = end;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                
                // 箭头侧面三角形
                AddTriangleVertex(vertices, arrowBaseCircle[i], color);
                AddTriangleVertex(vertices, arrowTip, color);
                AddTriangleVertex(vertices, arrowBaseCircle[next], color);
            }
        }
        
        /// <summary>
        /// 创建原点球体几何体
        /// </summary>
        /// <param name="vertices">顶点列表</param>
        /// <param name="center">球心位置</param>
        /// <param name="radius">球体半径</param>
        /// <param name="color">颜色</param>
        private void CreateOriginSphere(List<float> vertices, Vector3 center, float radius, Vector3 color)
        {
            const int latitudeSegments = 8;  // 纬度分段数
            const int longitudeSegments = 12; // 经度分段数
            
            // 生成球体顶点
            var sphereVertices = new List<Vector3>();
            
            // 添加顶点（北极）
            sphereVertices.Add(center + Vector3.UnitY * radius);
            
            // 添加中间纬度圈的顶点
            for (int lat = 1; lat < latitudeSegments; lat++)
            {
                float theta = MathF.PI * lat / latitudeSegments; // 纬度角
                float sinTheta = MathF.Sin(theta);
                float cosTheta = MathF.Cos(theta);
                
                for (int lon = 0; lon < longitudeSegments; lon++)
                {
                    float phi = 2.0f * MathF.PI * lon / longitudeSegments; // 经度角
                    float x = sinTheta * MathF.Cos(phi);
                    float z = sinTheta * MathF.Sin(phi);
                    float y = cosTheta;
                    
                    sphereVertices.Add(center + new Vector3(x, y, z) * radius);
                }
            }
            
            // 添加底点（南极）
            sphereVertices.Add(center - Vector3.UnitY * radius);
            
            // 生成三角形面片
            // 顶部扇形（连接北极点）
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int next = (lon + 1) % longitudeSegments;
                AddTriangleVertex(vertices, sphereVertices[0], color); // 北极点
                AddTriangleVertex(vertices, sphereVertices[1 + next], color);
                AddTriangleVertex(vertices, sphereVertices[1 + lon], color);
            }
            
            // 中间的四边形条带（转换为三角形）
            for (int lat = 0; lat < latitudeSegments - 2; lat++)
            {
                for (int lon = 0; lon < longitudeSegments; lon++)
                {
                    int next = (lon + 1) % longitudeSegments;
                    int current = 1 + lat * longitudeSegments + lon;
                    int currentNext = 1 + lat * longitudeSegments + next;
                    int below = 1 + (lat + 1) * longitudeSegments + lon;
                    int belowNext = 1 + (lat + 1) * longitudeSegments + next;
                    
                    // 第一个三角形
                    AddTriangleVertex(vertices, sphereVertices[current], color);
                    AddTriangleVertex(vertices, sphereVertices[below], color);
                    AddTriangleVertex(vertices, sphereVertices[currentNext], color);
                    
                    // 第二个三角形
                    AddTriangleVertex(vertices, sphereVertices[currentNext], color);
                    AddTriangleVertex(vertices, sphereVertices[below], color);
                    AddTriangleVertex(vertices, sphereVertices[belowNext], color);
                }
            }
            
            // 底部扇形（连接南极点）
            int southPoleIndex = sphereVertices.Count - 1;
            int lastRingStart = 1 + (latitudeSegments - 2) * longitudeSegments;
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int next = (lon + 1) % longitudeSegments;
                AddTriangleVertex(vertices, sphereVertices[southPoleIndex], color); // 南极点
                AddTriangleVertex(vertices, sphereVertices[lastRingStart + lon], color);
                AddTriangleVertex(vertices, sphereVertices[lastRingStart + next], color);
            }
        }
        
        /// <summary>
        /// 添加三角形顶点到顶点列表
        /// </summary>
        private void AddTriangleVertex(List<float> vertices, Vector3 position, Vector3 color)
        {
            vertices.AddRange(new float[] {
                position.X, position.Y, position.Z,
                color.X, color.Y, color.Z
            });
        }
        
        /// <summary>
        /// 渲染轴标签（X、Y、Z）
        /// </summary>
        /// <param name="xEnd">X轴末端位置</param>
        /// <param name="yEnd">Y轴末端位置</param>
        /// <param name="zEnd">Z轴末端位置</param>
        /// <param name="axisRadius">轴半径</param>
        /// <param name="shaderProgram">着色器程序</param>
        /// <param name="view">视图矩阵</param>
        /// <param name="projection">投影矩阵</param>
        private void RenderAxisLabels(Vector3 xEnd, Vector3 yEnd, Vector3 zEnd, float axisRadius, int shaderProgram, Matrix4 view, Matrix4 projection)
        {
            // 计算场景大小用于自适应缩放
            float sceneDiagonal = Math.Max(Math.Max(xEnd.Length, yEnd.Length), zEnd.Length);
            
            // 根据场景大小计算自适应的标签大小和偏移
            float labelScale = Math.Max(sceneDiagonal * 0.08f, 0.2f); // 标签大小自适应（增大4倍）
            float labelOffset = Math.Max(axisRadius * 8.0f, labelScale * 2.0f); // 标签距离轴末端的偏移
            
            // 计算每个轴标签的位置
            Vector3[] labelPositions = {
                xEnd + Vector3.UnitX * labelOffset, // X标签位置
                yEnd + Vector3.UnitY * labelOffset, // Y标签位置
                zEnd + Vector3.UnitZ * labelOffset  // Z标签位置
            };
            
            Vector3[] labelColors = {
                new Vector3(1.0f, 0.0f, 0.0f), // X - 红色
                new Vector3(0.0f, 1.0f, 0.0f), // Y - 绿色
                new Vector3(0.0f, 0.0f, 1.0f)  // Z - 蓝色
            };
            
            // 渲染每个标签
            for (int i = 0; i < 3; i++)
            {
                Vector3 position = labelPositions[i];
                Vector3 color = labelColors[i];
                
                // 设置模型矩阵（移动到标签位置）
                Matrix4 labelModel = Matrix4.CreateTranslation(position);
                SetMatrix(shaderProgram, "model", labelModel);
                
                // 根据轴绘制对应的字母
                switch (i)
                {
                    case 0: // X
                        DrawFilledLetterX(color, labelScale);
                        break;
                    case 1: // Y
                        DrawFilledLetterY(color, labelScale);
                        break;
                    case 2: // Z
                        DrawFilledLetterZ(color, labelScale);
                        break;
                }
            }
        }
        
        /// <summary>
        /// 设置矩阵uniform变量
        /// </summary>
        private void SetMatrix(int shaderProgram, string name, Matrix4 matrix)
        {
            int location = GL.GetUniformLocation(shaderProgram, name);
            if (location != -1)
            {
                GL.UniformMatrix4(location, false, ref matrix);
            }
        }
        
        /// <summary>
        /// 绘制填充字母X（使用三角形）
        /// </summary>
        private void DrawFilledLetterX(Vector3 color, float labelScale)
        {
            float size = labelScale * 0.6f; // 基于标签缩放的字母大小
            float width = labelScale * 0.15f; // 基于标签缩放的笔画宽度
            
            // X字母由两个交叉的矩形组成，每个矩形用两个三角形绘制
            float[] vertices = {
                // 第一条对角线的矩形（左上到右下）
                // 三角形1
                -size-width, size, 0, color.X, color.Y, color.Z,
                -size+width, size, 0, color.X, color.Y, color.Z,
                size-width, -size, 0, color.X, color.Y, color.Z,
                // 三角形2
                -size+width, size, 0, color.X, color.Y, color.Z,
                size+width, -size, 0, color.X, color.Y, color.Z,
                size-width, -size, 0, color.X, color.Y, color.Z,
                
                // 第二条对角线的矩形（右上到左下）
                // 三角形3
                size-width, size, 0, color.X, color.Y, color.Z,
                size+width, size, 0, color.X, color.Y, color.Z,
                -size+width, -size, 0, color.X, color.Y, color.Z,
                // 三角形4
                size+width, size, 0, color.X, color.Y, color.Z,
                -size-width, -size, 0, color.X, color.Y, color.Z,
                -size+width, -size, 0, color.X, color.Y, color.Z
            };
            DrawTriangles(vertices, 12);
        }
        
        /// <summary>
        /// 绘制填充字母Y（使用三角形）
        /// </summary>
        private void DrawFilledLetterY(Vector3 color, float labelScale)
        {
            float size = labelScale * 0.6f; // 基于标签缩放的字母大小
            float width = labelScale * 0.15f; // 基于标签缩放的笔画宽度
            
            float[] vertices = {
                // 左上分支
                // 三角形1
                -size-width, size, 0, color.X, color.Y, color.Z,
                -size+width, size, 0, color.X, color.Y, color.Z,
                -width, width, 0, color.X, color.Y, color.Z,
                // 三角形2
                -size+width, size, 0, color.X, color.Y, color.Z,
                width, width, 0, color.X, color.Y, color.Z,
                -width, width, 0, color.X, color.Y, color.Z,
                
                // 右上分支
                // 三角形3
                size-width, size, 0, color.X, color.Y, color.Z,
                size+width, size, 0, color.X, color.Y, color.Z,
                width, width, 0, color.X, color.Y, color.Z,
                // 三角形4
                size+width, size, 0, color.X, color.Y, color.Z,
                -width, width, 0, color.X, color.Y, color.Z,
                width, width, 0, color.X, color.Y, color.Z,
                
                // 中心到下方的竖线
                // 三角形5
                -width, width, 0, color.X, color.Y, color.Z,
                width, width, 0, color.X, color.Y, color.Z,
                -width, -size, 0, color.X, color.Y, color.Z,
                // 三角形6
                width, width, 0, color.X, color.Y, color.Z,
                width, -size, 0, color.X, color.Y, color.Z,
                -width, -size, 0, color.X, color.Y, color.Z
            };
            DrawTriangles(vertices, 18);
        }
        
        /// <summary>
        /// 绘制填充字母Z（使用三角形）
        /// </summary>
        private void DrawFilledLetterZ(Vector3 color, float labelScale)
        {
            float size = labelScale * 0.9f; // 基于标签缩放的字母大小（Z稍大）
            float width = labelScale * 0.25f; // 基于标签缩放的笔画宽度（更粗）
            
            float[] vertices = {
                // 上横线
                // 三角形1
                -size, size, 0, color.X, color.Y, color.Z,
                size, size, 0, color.X, color.Y, color.Z,
                -size, size-width, 0, color.X, color.Y, color.Z,
                // 三角形2
                size, size, 0, color.X, color.Y, color.Z,
                size, size-width, 0, color.X, color.Y, color.Z,
                -size, size-width, 0, color.X, color.Y, color.Z,
                
                // 下横线
                // 三角形3
                -size, -size+width, 0, color.X, color.Y, color.Z,
                size, -size+width, 0, color.X, color.Y, color.Z,
                -size, -size, 0, color.X, color.Y, color.Z,
                // 三角形4
                size, -size+width, 0, color.X, color.Y, color.Z,
                size, -size, 0, color.X, color.Y, color.Z,
                -size, -size, 0, color.X, color.Y, color.Z,
                
                // 对角线（从右上到左下）
                // 三角形5
                size-width, size-width, 0, color.X, color.Y, color.Z,
                size, size-width, 0, color.X, color.Y, color.Z,
                -size, -size+width, 0, color.X, color.Y, color.Z,
                // 三角形6
                size, size-width, 0, color.X, color.Y, color.Z,
                -size+width, -size+width, 0, color.X, color.Y, color.Z,
                -size, -size+width, 0, color.X, color.Y, color.Z
            };
            DrawTriangles(vertices, 18);
        }
        
        /// <summary>
        /// 绘制三角形
        /// </summary>
        private void DrawTriangles(float[] vertices, int vertexCount)
        {
            // 创建临时VAO和VBO
            uint vao = (uint)GL.GenVertexArray();
            uint vbo = (uint)GL.GenBuffer();
            
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            
            // 设置顶点属性
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            
            // 绘制三角形
            GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
            
            // 清理
            GL.BindVertexArray(0);
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
        }
        
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            if (_isInitialized)
            {
                GL.DeleteVertexArrays(1, ref _vao);
                GL.DeleteBuffers(1, ref _vbo);
                _isInitialized = false;
            }
        }
    }
}