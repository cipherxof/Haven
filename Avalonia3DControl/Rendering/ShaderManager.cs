using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using Avalonia3DControl.Materials;
using Avalonia3DControl.Core.ErrorHandling;

namespace Avalonia3DControl.Rendering
{
    /// <summary>
    /// 着色器管理器，统一管理所有着色器程序的创建、编译和销毁
    /// </summary>
    public class ShaderManager : IDisposable
    {
        private readonly Dictionary<string, int> _shaderPrograms = new Dictionary<string, int>();
        private bool _disposed = false;

        /// <summary>
        /// 初始化着色器管理器
        /// </summary>
        public void Initialize()
        {
            // 初始化默认着色器程序
            try
            {
                // 显式绑定属性位置，确保与VAO一致
                var attributeBindings = new Dictionary<int, string>
                {
                    { 0, "aPosition" },
                    { 1, "aColor" },
                    { 2, "aTexCoord" }
                };

                // 创建顶点着色器程序
                string vertexVertexSource = ShaderLoader.LoadRendererVertexShader();
                string vertexFragmentSource = ShaderLoader.LoadRendererFragmentShader();
                CreateShaderProgram("vertex", vertexVertexSource, vertexFragmentSource, attributeBindings);
                
                // 创建纹理着色器程序
                string textureVertexSource = ShaderLoader.LoadTextureVertexShader();
                string textureFragmentSource = ShaderLoader.LoadTextureFragmentShader();
                CreateShaderProgram("texture", textureVertexSource, textureFragmentSource, attributeBindings);
                
                // 创建材质着色器程序
                string materialVertexSource = ShaderLoader.LoadMaterialVertexShader();
                string materialFragmentSource = ShaderLoader.LoadMaterialFragmentShader();
                CreateShaderProgram("material", materialVertexSource, materialFragmentSource, attributeBindings);
            }
            catch (Exception ex)
            {
                // 如果着色器加载失败，记录错误但不抛出异常，避免程序崩溃
                ErrorHandler.HandleInitializationException(ex, "着色器初始化");
            }
        }

        /// <summary>
        /// 创建并编译着色器程序
        /// </summary>
        /// <param name="name">着色器程序名称</param>
        /// <param name="vertexSource">顶点着色器源码</param>
        /// <param name="fragmentSource">片段着色器源码</param>
        /// <param name="attributeBindings">属性绑定（可选）</param>
        /// <returns>着色器程序ID</returns>
        public int CreateShaderProgram(string name, string vertexSource, string fragmentSource, 
            Dictionary<int, string>? attributeBindings = null)
        {
            if (_shaderPrograms.ContainsKey(name))
            {
                throw new InvalidOperationException($"着色器程序 '{name}' 已存在");
            }

            try
            {
                // 编译着色器
                int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
                int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

                // 创建程序
                int program = GL.CreateProgram();
                GL.AttachShader(program, vertexShader);
                GL.AttachShader(program, fragmentShader);

                // 绑定属性位置（如果提供）
                if (attributeBindings != null)
                {
                    foreach (var binding in attributeBindings)
                    {
                        GL.BindAttribLocation(program, binding.Key, binding.Value);
                    }
                }

                // 链接程序
                GL.LinkProgram(program);

                // 检查链接状态
                GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
                if (success == 0)
                {
                    string infoLog = GL.GetProgramInfoLog(program);
                    GL.DeleteProgram(program);
                    throw new Exception($"着色器程序 '{name}' 链接失败: {infoLog}");
                }

                // 清理着色器对象
                GL.DeleteShader(vertexShader);
                GL.DeleteShader(fragmentShader);

                // 存储程序
                _shaderPrograms[name] = program;
                return program;
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleResourceException(ex, $"创建着色器程序 '{name}'");
                throw;
            }
        }

        /// <summary>
        /// 获取着色器程序ID
        /// </summary>
        /// <param name="name">着色器程序名称</param>
        /// <returns>着色器程序ID</returns>
        public int GetShaderProgram(string name)
        {
            if (_shaderPrograms.TryGetValue(name, out int program))
            {
                return program;
            }
            
            throw new ArgumentException($"着色器程序 '{name}' 不存在");
        }

        /// <summary>
        /// 根据着色模式获取着色器程序
        /// </summary>
        /// <param name="shadingMode">着色模式</param>
        /// <returns>着色器程序ID</returns>
        public int GetShaderProgram(ShadingMode shadingMode)
        {
            string name = shadingMode.ToString().ToLower();
            return GetShaderProgram(name);
        }

        /// <summary>
        /// 检查着色器程序是否存在
        /// </summary>
        /// <param name="name">着色器程序名称</param>
        /// <returns>是否存在</returns>
        public bool HasShaderProgram(string name)
        {
            return _shaderPrograms.ContainsKey(name);
        }

        /// <summary>
        /// 删除指定的着色器程序
        /// </summary>
        /// <param name="name">着色器程序名称</param>
        public void DeleteShaderProgram(string name)
        {
            if (_shaderPrograms.TryGetValue(name, out int program))
            {
                GL.DeleteProgram(program);
                _shaderPrograms.Remove(name);
            }
        }

        /// <summary>
        /// 编译单个着色器
        /// </summary>
        /// <param name="type">着色器类型</param>
        /// <param name="source">着色器源码</param>
        /// <returns>着色器ID</returns>
        private int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);
            
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                GL.DeleteShader(shader);
                throw new Exception($"{type} 着色器编译失败: {infoLog}");
            }
            
            return shader;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            foreach (var program in _shaderPrograms.Values)
            {
                GL.DeleteProgram(program);
            }
            _shaderPrograms.Clear();
        }

        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Cleanup();
                _disposed = true;
            }
        }
    }
}
