using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Materials;
using Avalonia3DControl.Core;

namespace Avalonia3DControl.Rendering.OpenGL
{
    /// <summary>
    /// 模型渲染器，负责管理模型的VAO/VBO和渲染逻辑
    /// </summary>
    public class ModelRenderer : IDisposable
    {
        #region 私有字段
        private Dictionary<Model3D, ModelRenderData> _modelRenderData;
        private int _defaultTexture;
        private readonly HashSet<Model3D> _loggedMissingTexture = new();
        #endregion

        #region 内部类
        private class ModelRenderData
        {
            public int VAO { get; set; }
            public int VBO { get; set; }
            public int EBO { get; set; }
            public int LineEBO { get; set; }
            public int LineIndexCount { get; set; }
        }

        private readonly record struct VertexLayout(
            int Stride,
            int UvOffset,
            int ShadowColorOffset);
        #endregion

        #region 构造函数
        public ModelRenderer(int defaultTexture)
        {
            _modelRenderData = new Dictionary<Model3D, ModelRenderData>();
            _defaultTexture = defaultTexture;
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 渲染模型
        /// </summary>
        /// <param name="model">要渲染的模型</param>
        /// <param name="shaderProgram">着色器程序</param>
        /// <param name="renderMode">渲染模式</param>
        public void RenderModel(Model3D model, int shaderProgram, RenderMode renderMode)
        {
            if (model == null || !model.Visible) 
            {
                return;
            }

            // 确保模型有渲染数据
            if (!_modelRenderData.ContainsKey(model))
            {
                CreateModelRenderData(model);
            }

            var renderData = _modelRenderData[model];

            // 设置模型矩阵
            Matrix4 modelMatrix = model.GetModelMatrix();
            SetMatrix(shaderProgram, "model", modelMatrix);

            // 设置材质属性
            SetMaterialProperties(shaderProgram, model);

            var shadowedLightingLocation = GL.GetUniformLocation(shaderProgram, "uHasShadowedLighting");
            if (shadowedLightingLocation != -1)
            {
                var hasShadowedLighting = model.VertexCount > 0 &&
                    model.ShadowedColors.Length >= model.VertexCount * 3;
                GL.Uniform1(shadowedLightingLocation, hasShadowedLighting ? 1 : 0);
            }
            
            // 设置材质透明度
            var alphaLocation = GL.GetUniformLocation(shaderProgram, "materialAlpha");
            if (alphaLocation != -1)
            {
                // 坐标轴相关模型始终保持完全不透明，不受UI透明度控制影响
                bool isAxesModel2 = model.Name == "MiniAxes" || model.Name == "CoordinateAxes";
                float alpha = isAxesModel2 ? 1.0f : GetEffectiveAlpha(model);
                GL.Uniform1(alphaLocation, alpha);
            }
            
            // 设置点模式相关的uniform变量
            int pointModeLocation = GL.GetUniformLocation(shaderProgram, "uPointMode");
            if (pointModeLocation != -1)
            {
                GL.Uniform1(pointModeLocation, renderMode == RenderMode.Point ? 1 : 0);
            }
            
            int pointSizeLocation = GL.GetUniformLocation(shaderProgram, "uPointSize");
            if (pointSizeLocation != -1)
            {
                GL.Uniform1(pointSizeLocation, 5.0f);
            }

            // 绑定VAO
            GL.BindVertexArray(renderData.VAO);

            int hasTextureLocation = GL.GetUniformLocation(shaderProgram, "hasTexture");
            if (hasTextureLocation != -1)
            {
                GL.Uniform1(hasTextureLocation, model.TextureId > 0 ? 1 : 0);
            }

            if (model.TextureId <= 0 && !_loggedMissingTexture.Contains(model))
            {
                _loggedMissingTexture.Add(model);
                Console.WriteLine($"[Render] Model '{model.Name}' has no texture id; using default texture.");
            }

            // 绑定纹理采样器到纹理单元0（如果着色器需要）
            int textureLocation = GL.GetUniformLocation(shaderProgram, "texture0");
            if (textureLocation != -1)
            {
                GL.Uniform1(textureLocation, 0);
            }

            // Per-packet coverage/alpha-test state (decoded from the MDN packet flag).
            SetBool(shaderProgram, "uUseVertexAlpha", model.UseVertexAlpha);
            SetFloat(shaderProgram, "uAlphaTestRef", model.AlphaTestRef);
            SetBool(shaderProgram, "uForceOpaqueAlpha", model.ForceOpaqueAlpha);
            SetBool(shaderProgram, "uReceiveShadow", model.ReceivesShadow);

            // 处理纹理
            HandleTexture(model);

            // Check if model has transparency and disable depth writing for transparent objects
            bool isAxesModel = model.Name == "MiniAxes" || model.Name == "CoordinateAxes";
            float modelAlpha = isAxesModel ? 1.0f : GetEffectiveAlpha(model);
            bool uiTransparent = modelAlpha < 0.99f;

            // Depth writes off for blend layers that request it (flag bit 0x200) or when the UI
            // has made the model translucent; the coordinate axes always write.
            bool depthMaskOff = !isAxesModel && (!model.WriteDepth || uiTransparent);
            if (depthMaskOff)
            {
                GL.DepthMask(false);
            }

            // Blend layers: composite in the requested mode.
            bool customBlend = model.BlendEnabled && !isAxesModel;
            if (customBlend)
            {
                ApplyBlendMode(model.BlendMode);
            }

            // Blend layers are frequently coplanar with the opaque surface beneath them; use
            // Lequal plus a small polygon offset (the editor analog of the engine's
            // raise_projection depth bias) so the later-drawn layer wins the depth tie.
            bool hasDepthBias = MathF.Abs(model.DepthBias) > 0.0001f;
            bool layerBias = customBlend && !model.WriteDepth;
            bool relaxDepthFunc = hasDepthBias || layerBias;
            bool offsetFilledPolygons = (hasDepthBias || layerBias) && renderMode == RenderMode.Fill;
            if (relaxDepthFunc)
            {
                GL.DepthFunc(DepthFunction.Lequal);
            }
            if (offsetFilledPolygons)
            {
                float bias = hasDepthBias ? model.DepthBias : -1.0f;
                GL.Enable(EnableCap.PolygonOffsetFill);
                GL.PolygonOffset(bias, bias);
            }

            // 根据渲染模式绘制
            DrawModel(model, renderData, renderMode);

            if (offsetFilledPolygons)
            {
                GL.Disable(EnableCap.PolygonOffsetFill);
            }
            if (relaxDepthFunc)
            {
                GL.DepthFunc(DepthFunction.Less);
            }

            if (customBlend)
            {
                // Restore the default alpha-over blend the rest of the scene expects.
                GL.BlendEquation(BlendEquationMode.FuncAdd);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            }

            if (depthMaskOff)
            {
                GL.DepthMask(true); // Re-enable depth writing
            }

            // 解绑VAO
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Minimal depth/alpha-test submission used by the Haven shadow pass.
        /// It intentionally skips material, blending and lighting uniforms to keep
        /// camera-dependent shadow updates cheap on large stages.
        /// </summary>
        public void RenderShadowModel(Model3D model, int shaderProgram)
        {
            if (model == null || !model.Visible || model.Indices.Length < 3)
            {
                return;
            }

            if (!_modelRenderData.TryGetValue(model, out var renderData))
            {
                CreateModelRenderData(model);
                renderData = _modelRenderData[model];
            }

            var modelMatrix = model.GetModelMatrix();
            SetMatrix(shaderProgram, "model", modelMatrix);
            SetBool(shaderProgram, "hasTexture", model.TextureId > 0);
            SetBool(shaderProgram, "uUseVertexAlpha", model.UseVertexAlpha);
            SetFloat(shaderProgram, "uAlphaTestRef", model.AlphaTestRef);

            var textureLocation = GL.GetUniformLocation(shaderProgram, "texture0");
            if (textureLocation >= 0)
            {
                GL.Uniform1(textureLocation, 0);
            }

            GL.BindVertexArray(renderData.VAO);
            HandleTexture(model);
            DrawModel(model, renderData, RenderMode.Fill);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// 更新模型顶点缓冲区
        /// </summary>
        /// <param name="model">要更新的模型</param>
        public void UpdateModelVertexBuffer(Model3D model)
        {
            if (!_modelRenderData.TryGetValue(model, out var renderData))
            {
                CreateModelRenderData(model);
                return;
            }

            // 检查缓冲区是否有效
            if (!GL.IsBuffer(renderData.VBO))
            {
                CreateModelRenderData(model);
                return;
            }

            try
            {
                GL.BindVertexArray(renderData.VAO);
                GL.BindBuffer(BufferTarget.ArrayBuffer, renderData.VBO);
                var vertices = BuildVertexData(model, out var layout);
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
                SetupVertexAttributes(layout);
                GL.BindVertexArray(0);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新顶点缓冲区时出错: {ex.Message}");
                // 重新创建渲染数据
                CreateModelRenderData(model);
            }
        }

        /// <summary>
        /// Updates triangle and wireframe index buffers without rebuilding vertex data.
        /// </summary>
        public void UpdateModelIndexBuffer(Model3D model)
        {
            if (!_modelRenderData.TryGetValue(model, out var renderData))
            {
                CreateModelRenderData(model);
                return;
            }

            GL.BindVertexArray(renderData.VAO);
            if (renderData.EBO == 0)
            {
                renderData.EBO = GL.GenBuffer();
            }

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, renderData.EBO);
            if (model.Indices.Length > 0)
            {
                GL.BufferData(
                    BufferTarget.ElementArrayBuffer,
                    model.Indices.Length * sizeof(uint),
                    model.Indices,
                    BufferUsageHint.DynamicDraw);
            }
            else
            {
                GL.BufferData(
                    BufferTarget.ElementArrayBuffer,
                    0,
                    IntPtr.Zero,
                    BufferUsageHint.DynamicDraw);
            }

            if (renderData.LineEBO != 0)
            {
                GL.DeleteBuffer(renderData.LineEBO);
                renderData.LineEBO = 0;
                renderData.LineIndexCount = 0;
            }
            CreateLineIndices(model, renderData);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            foreach (var renderData in _modelRenderData.Values)
            {
                GL.DeleteVertexArray(renderData.VAO);
                GL.DeleteBuffer(renderData.VBO);
                GL.DeleteBuffer(renderData.EBO);
                if (renderData.LineEBO != 0) GL.DeleteBuffer(renderData.LineEBO);
            }
            _modelRenderData.Clear();
        }
        #endregion

        #region 私有方法
        internal static bool IsTransparent(Model3D model)
        {
            // Blend packets (terrain/decal layers) must draw in the second pass, after opaque
            // geometry, so they composite over it in file/packet order.
            return model.BlendEnabled || GetEffectiveAlpha(model) < 0.99f;
        }

        private static float GetEffectiveAlpha(Model3D model)
        {
            return Math.Clamp(model.Alpha * (model.Material?.Alpha ?? 1.0f), 0.0f, 1.0f);
        }

        /// <summary>
        /// 创建模型渲染数据
        /// </summary>
        /// <param name="model">模型</param>
        private void CreateModelRenderData(Model3D model)
        {
            // 如果已存在，先清理
            if (_modelRenderData.TryGetValue(model, out var existingData))
            {
                GL.DeleteVertexArray(existingData.VAO);
                GL.DeleteBuffer(existingData.VBO);
                GL.DeleteBuffer(existingData.EBO);
                if (existingData.LineEBO != 0) GL.DeleteBuffer(existingData.LineEBO);
            }

            var renderData = new ModelRenderData();

            // 创建VAO
            renderData.VAO = GL.GenVertexArray();
            GL.BindVertexArray(renderData.VAO);

            // 创建VBO
            renderData.VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, renderData.VBO);
            var vertices = BuildVertexData(model, out var layout);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // 设置顶点属性
            SetupVertexAttributes(layout);

            // 创建EBO（用于三角形）
            if (model.Indices != null && model.Indices.Length > 0)
            {
                renderData.EBO = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, renderData.EBO);
                GL.BufferData(BufferTarget.ElementArrayBuffer, model.Indices.Length * sizeof(uint), model.Indices, BufferUsageHint.StaticDraw);
            }

            // 创建线框索引缓冲区
            CreateLineIndices(model, renderData);

            GL.BindVertexArray(0);
            _modelRenderData[model] = renderData;
        }

        /// <summary>
        /// 设置顶点属性
        /// </summary>
        private void SetupVertexAttributes(VertexLayout layout)
        {
            int strideBytes = layout.Stride * sizeof(float);

            // Position: location 0, RGB color + coverage alpha: location 1.
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, strideBytes, 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, strideBytes, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            if (layout.UvOffset >= 0)
            {
                GL.VertexAttribPointer(
                    2,
                    2,
                    VertexAttribPointerType.Float,
                    false,
                    strideBytes,
                    layout.UvOffset * sizeof(float));
                GL.EnableVertexAttribArray(2);
            }
            else
            {
                GL.DisableVertexAttribArray(2);
                GL.VertexAttrib2(2, 0.0f, 0.0f);
            }

            // RGB lighting with the projected sun removed. This is the exact LT3
            // endpoint used when the building shadow map occludes the sun.
            if (layout.ShadowColorOffset >= 0)
            {
                GL.VertexAttribPointer(
                    3,
                    3,
                    VertexAttribPointerType.Float,
                    false,
                    strideBytes,
                    layout.ShadowColorOffset * sizeof(float));
                GL.EnableVertexAttribArray(3);
            }
            else
            {
                GL.DisableVertexAttribArray(3);
                GL.VertexAttrib3(3, 0.0f, 0.0f, 0.0f);
            }
        }

        private float[] BuildVertexData(Model3D model, out VertexLayout layout)
        {
            if (model.Vertices != null && model.Vertices.Length > 0 && model.Positions.Length == 0)
            {
                var interleavedStride = 7;
                if (model.VertexCount > 0)
                {
                    int candidateStride = model.Vertices.Length / model.VertexCount;
                    if (candidateStride >= 9)
                    {
                        interleavedStride = 9; // pos(3) + color(4) + uv(2)
                    }
                    else if (candidateStride >= 7)
                    {
                        interleavedStride = 7; // pos(3) + color(4)
                    }
                }

                layout = new VertexLayout(
                    interleavedStride,
                    interleavedStride >= 9 ? 7 : -1,
                    -1);
                return model.Vertices;
            }

            int vertexCount = model.VertexCount > 0 ? model.VertexCount : model.Positions.Length / 3;
            if (vertexCount <= 0)
            {
                layout = new VertexLayout(7, -1, -1);
                return Array.Empty<float>();
            }

            bool hasRgbaColors = model.Colors.Length >= vertexCount * 4;
            bool hasRgbColors = !hasRgbaColors && model.Colors.Length >= vertexCount * 3;
            bool hasUvs = model.UVs.Length >= vertexCount * 2;
            bool hasShadowedColors = model.ShadowedColors.Length >= vertexCount * 3;

            // Separate-array stage models reserve RGB for the LT3 result with the
            // projected sun removed. The layout remains stable when Game lighting is
            // toggled; models without baked LT3 data are flagged by a uniform.
            var stride = hasUvs ? 12 : 10;
            var uvOffset = hasUvs ? 7 : -1;
            var shadowOffset = hasUvs ? 9 : 7;
            layout = new VertexLayout(stride, uvOffset, shadowOffset);
            var data = new float[vertexCount * stride];

            var fallbackColor = model.Color;
            for (int i = 0; i < vertexCount; i++)
            {
                int dst = i * stride;
                int posSrc = i * 3;

                if (model.Positions.Length >= posSrc + 3)
                {
                    data[dst] = model.Positions[posSrc];
                    data[dst + 1] = model.Positions[posSrc + 1];
                    data[dst + 2] = model.Positions[posSrc + 2];
                }

                if (hasRgbaColors)
                {
                    int colorSrc = i * 4;
                    data[dst + 3] = model.Colors[colorSrc];
                    data[dst + 4] = model.Colors[colorSrc + 1];
                    data[dst + 5] = model.Colors[colorSrc + 2];
                    data[dst + 6] = model.Colors[colorSrc + 3];
                }
                else if (hasRgbColors)
                {
                    int colorSrc = i * 3;
                    data[dst + 3] = model.Colors[colorSrc];
                    data[dst + 4] = model.Colors[colorSrc + 1];
                    data[dst + 5] = model.Colors[colorSrc + 2];
                    data[dst + 6] = 1.0f;
                }
                else
                {
                    data[dst + 3] = fallbackColor.X;
                    data[dst + 4] = fallbackColor.Y;
                    data[dst + 5] = fallbackColor.Z;
                    data[dst + 6] = 1.0f;
                }

                if (hasUvs)
                {
                    int uvSrc = i * 2;
                    data[dst + uvOffset] = model.UVs[uvSrc];
                    data[dst + uvOffset + 1] = model.UVs[uvSrc + 1];
                }

                var shadowSource = i * 3;
                if (hasShadowedColors)
                {
                    data[dst + shadowOffset] = MathF.Max(0.0f, model.ShadowedColors[shadowSource]);
                    data[dst + shadowOffset + 1] = MathF.Max(0.0f, model.ShadowedColors[shadowSource + 1]);
                    data[dst + shadowOffset + 2] = MathF.Max(0.0f, model.ShadowedColors[shadowSource + 2]);
                }
            }

            return data;
        }

        /// <summary>
        /// 创建线框索引
        /// </summary>
        /// <param name="model">模型</param>
        /// <param name="renderData">渲染数据</param>
        private void CreateLineIndices(Model3D model, ModelRenderData renderData)
        {
            if (model.Indices == null || model.Indices.Length == 0) return;

            var lineIndices = new List<uint>();
            for (int i = 0; i < model.Indices.Length; i += 3)
            {
                if (i + 2 < model.Indices.Length)
                {
                    uint i0 = model.Indices[i];
                    uint i1 = model.Indices[i + 1];
                    uint i2 = model.Indices[i + 2];

                    // 添加三角形的三条边
                    lineIndices.Add(i0); lineIndices.Add(i1);
                    lineIndices.Add(i1); lineIndices.Add(i2);
                    lineIndices.Add(i2); lineIndices.Add(i0);
                }
            }

            if (lineIndices.Count > 0)
            {
                renderData.LineEBO = GL.GenBuffer();
                renderData.LineIndexCount = lineIndices.Count;
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, renderData.LineEBO);
                GL.BufferData(BufferTarget.ElementArrayBuffer, lineIndices.Count * sizeof(uint), lineIndices.ToArray(), BufferUsageHint.StaticDraw);
            }
        }

        /// <summary>
        /// 设置材质属性
        /// </summary>
        /// <param name="shaderProgram">着色器程序</param>
        /// <param name="model">模型</param>
        private void SetMaterialProperties(int shaderProgram, Model3D model)
        {
            // 设置透明度
            int alphaLocation = GL.GetUniformLocation(shaderProgram, "alpha");
            if (alphaLocation != -1)
            {
                GL.Uniform1(alphaLocation, model.Alpha);
            }

            // 设置材质属性
            if (model.Material != null)
            {
                SetMaterial(shaderProgram, model.Material);
            }
        }

        /// <summary>
        /// 设置材质
        /// </summary>
        /// <param name="shaderProgram">着色器程序</param>
        /// <param name="material">材质</param>
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

            int alphaLocation = GL.GetUniformLocation(shaderProgram, "materialAlpha");
            if (alphaLocation != -1)
            {
                GL.Uniform1(alphaLocation, material.Alpha);
            }
        }

        /// <summary>
        /// 处理纹理
        /// </summary>
        /// <param name="model">模型</param>
        private void HandleTexture(Model3D model)
        {
            if (model.TextureId > 0)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, model.TextureId);
            }
            else
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _defaultTexture);
            }
        }

        /// <summary>
        /// 绘制模型
        /// </summary>
        /// <param name="model">模型</param>
        /// <param name="renderData">渲染数据</param>
        /// <param name="renderMode">渲染模式</param>
        private void DrawModel(Model3D model, ModelRenderData renderData, RenderMode renderMode)
        {
            switch (renderMode)
            {
                case RenderMode.Line:
                    if (renderData.LineEBO != 0)
                    {
                        GL.BindBuffer(BufferTarget.ElementArrayBuffer, renderData.LineEBO);
                        GL.DrawElements(PrimitiveType.Lines, renderData.LineIndexCount, DrawElementsType.UnsignedInt, 0);
                    }
                    break;

                case RenderMode.Point:
                    if (model.Indices != null && model.Indices.Length > 0)
                    {
                        GL.BindBuffer(BufferTarget.ElementArrayBuffer, renderData.EBO);
                        GL.DrawElements(PrimitiveType.Points, model.Indices.Length, DrawElementsType.UnsignedInt, 0);
                    }
                    break;

                case RenderMode.Fill:
                default:
                    if (model.Indices != null && model.Indices.Length > 0)
                    {
                        GL.BindBuffer(BufferTarget.ElementArrayBuffer, renderData.EBO);
                        GL.DrawElements(PrimitiveType.Triangles, model.Indices.Length, DrawElementsType.UnsignedInt, 0);
                    }
                    break;
            }
        }

        /// <summary>
        /// 设置矩阵
        /// </summary>
        /// <param name="shaderProgram">着色器程序</param>
        /// <param name="name">uniform名称</param>
        /// <param name="matrix">矩阵</param>
        private void SetMatrix(int shaderProgram, string name, Matrix4 matrix)
        {
            int location = GL.GetUniformLocation(shaderProgram, name);
            if (location != -1)
            {
                GL.UniformMatrix4(location, false, ref matrix);
            }
        }

        private static void SetBool(int shaderProgram, string name, bool value)
        {
            int location = GL.GetUniformLocation(shaderProgram, name);
            if (location != -1)
            {
                GL.Uniform1(location, value ? 1 : 0);
            }
        }

        private static void SetFloat(int shaderProgram, string name, float value)
        {
            int location = GL.GetUniformLocation(shaderProgram, name);
            if (location != -1)
            {
                GL.Uniform1(location, value);
            }
        }

        /// <summary>
        /// Maps an MDN packet blend-mode index to GL blend state.
        /// </summary>
        private static void ApplyBlendMode(ModelBlendMode mode)
        {
            switch (mode)
            {
                case ModelBlendMode.Additive:
                    GL.BlendEquation(BlendEquationMode.FuncAdd);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                    break;
                case ModelBlendMode.ReverseSubtract:
                    GL.BlendEquation(BlendEquationMode.FuncReverseSubtract);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                    break;
                case ModelBlendMode.Multiply:
                    GL.BlendEquation(BlendEquationMode.FuncAdd);
                    GL.BlendFunc(BlendingFactor.DstColor, BlendingFactor.Zero);
                    break;
                case ModelBlendMode.Alpha:
                default:
                    GL.BlendEquation(BlendEquationMode.FuncAdd);
                    GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    break;
            }
        }
        #endregion

        #region IDisposable实现
        public void Dispose()
        {
            Cleanup();
        }
        #endregion
    }
}
