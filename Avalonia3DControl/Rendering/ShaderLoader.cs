using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Avalonia3DControl.Rendering
{
    /// <summary>
    /// 着色器文件加载器
    /// </summary>
    public static class ShaderLoader
    {
        /// <summary>
        /// 获取当前平台的着色器目录前缀
        /// </summary>
        private static string GetPlatformShaderPrefix()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "macOS";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }
            else
            {
                return "Windows"; // 默认使用Windows版本
            }
        }
        /// <summary>
        /// 从嵌入资源中加载着色器代码
        /// </summary>
        /// <param name="shaderPath">着色器文件路径（相对于Shaders目录）</param>
        /// <returns>着色器源代码</returns>
        public static string LoadShader(string shaderPath)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var platformPrefix = GetPlatformShaderPrefix();
                
                // 首先尝试加载平台特定的着色器
                var platformResourceName = $"Avalonia3DControl.Shaders.{platformPrefix}.{shaderPath.Replace('/', '.').Replace('\\', '.')}".Replace(".glsl", ".glsl");
                
                using var platformStream = assembly.GetManifestResourceStream(platformResourceName);
                if (platformStream != null)
                {
                    using var platformReader = new StreamReader(platformStream);
                    return platformReader.ReadToEnd();
                }
                
                // 如果平台特定版本不存在，回退到通用版本
                var resourceName = $"Avalonia3DControl.Shaders.{shaderPath.Replace('/', '.').Replace('\\', '.')}".Replace(".glsl", ".glsl");
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    throw new FileNotFoundException($"找不到着色器资源: {resourceName} 或 {platformResourceName}");
                }
                
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                throw new Exception($"加载着色器失败 '{shaderPath}': {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 加载GradientBar的顶点着色器
        /// </summary>
        public static string LoadGradientBarVertexShader() => LoadShader("GradientBar/vertex.glsl");
        
        /// <summary>
        /// 加载GradientBar的片段着色器
        /// </summary>
        public static string LoadGradientBarFragmentShader() => LoadShader("GradientBar/fragment.glsl");
        
        /// <summary>
        /// 加载GradientBar线条的顶点着色器
        /// </summary>
        public static string LoadGradientBarLineVertexShader() => LoadShader("GradientBar/line_vertex.glsl");
        
        /// <summary>
        /// 加载GradientBar线条的片段着色器
        /// </summary>
        public static string LoadGradientBarLineFragmentShader() => LoadShader("GradientBar/line_fragment.glsl");
        
        /// <summary>
        /// 加载渲染器的顶点着色器
        /// </summary>
        public static string LoadRendererVertexShader() => LoadShader("Renderer/vertex.glsl");
        
        /// <summary>
        /// 加载渲染器的片段着色器
        /// </summary>
        public static string LoadRendererFragmentShader() => LoadShader("Renderer/fragment.glsl");
        
        /// <summary>
        /// 加载纹理着色器的顶点着色器
        /// </summary>
        public static string LoadTextureVertexShader() => LoadShader("Renderer/texture_vertex.glsl");
        
        /// <summary>
        /// 加载纹理着色器的片段着色器
        /// </summary>
        public static string LoadTextureFragmentShader() => LoadShader("Renderer/texture_fragment.glsl");
        
        /// <summary>
        /// 加载材质着色器的顶点着色器
        /// </summary>
        public static string LoadMaterialVertexShader() => LoadShader("Renderer/material_vertex.glsl");
        
        /// <summary>
        /// 加载材质着色器的片段着色器
        /// </summary>
        public static string LoadMaterialFragmentShader() => LoadShader("Renderer/material_fragment.glsl");
    }
}