using OpenTK.Mathematics;

namespace Avalonia3DControl.Materials
{
    /// <summary>
    /// 着色模式枚举，定义3D对象的光照计算方式
    /// </summary>
    /// <remarks>
    /// 不同的着色模式会产生不同的视觉效果：
    /// - Flat: 平面着色，每个面使用统一颜色
    /// - Smooth: 平滑着色，在顶点间插值计算光照
    /// - Wireframe: 线框模式，只显示模型的边框
    /// </remarks>
    public enum ShadingMode
    {
        Vertex,         // 顶点显示
        Texture,        // 纹理模式
        Material        // 材质模式
    }

    /// <summary>
    /// 渲染模式枚举，定义3D对象的渲染方式
    /// </summary>
    /// <remarks>
    /// 渲染模式控制几何体的显示方式：
    /// - Solid: 实体渲染，显示完整的几何体表面
    /// - Wireframe: 线框渲染，只显示几何体的边线
    /// - Points: 点渲染，只显示顶点
    /// </remarks>
    public enum RenderMode
    {
        Point,          // 点模式
        Line,           // 线框模式
        Fill            // 填充模式
    }

    /// <summary>
    /// 材质类，定义3D对象的外观属性和光照响应
    /// </summary>
    /// <remarks>
    /// Material类封装了物体表面的视觉属性，包括：
    /// - 漫反射颜色：物体的基本颜色
    /// - 镜面反射颜色：高光颜色
    /// - 光泽度：控制高光的锐利程度
    /// - 环境光颜色：环境照明下的颜色
    /// 
    /// 提供了多种预设材质工厂方法：
    /// - CreatePlastic(): 塑料材质
    /// - CreateMetal(): 金属材质
    /// - CreateGlass(): 玻璃材质
    /// - CreateRubber(): 橡胶材质
    /// </remarks>
    public class Material
    {
        public Vector3 Ambient { get; set; }      // 环境光反射
        public Vector3 Diffuse { get; set; }      // 漫反射
        public Vector3 Specular { get; set; }     // 镜面反射
        public float Shininess { get; set; }      // 光泽度
        public Vector3 Emission { get; set; }     // 自发光
        public float Alpha { get; set; }          // 透明度 (0.0 = 完全透明, 1.0 = 完全不透明)
        public ShadingMode ShadingMode { get; set; } // 着色模式
        public RenderMode RenderMode { get; set; }   // 渲染模式

        public Material()
        {
            Ambient = new Vector3(0.2f, 0.2f, 0.2f);
            Diffuse = new Vector3(0.8f, 0.8f, 0.8f);
            Specular = new Vector3(1.0f, 1.0f, 1.0f);
            Shininess = 32.0f;
            Emission = Vector3.Zero;
            Alpha = 1.0f; // 默认完全不透明
            ShadingMode = ShadingMode.Vertex;
            RenderMode = RenderMode.Fill;
        }

        // 预定义材质
        public static Material CreatePlastic(Vector3 color, float alpha = 1.0f)
        {
            return new Material
            {
                Ambient = color * 0.2f,
                Diffuse = color * 0.8f,
                Specular = new Vector3(0.5f, 0.5f, 0.5f),
                Shininess = 32.0f,
                Alpha = alpha
            };
        }

        public static Material CreateMetal(Vector3 color, float alpha = 1.0f)
        {
            return new Material
            {
                Ambient = color * 0.1f,
                Diffuse = color * 0.3f,
                Specular = new Vector3(0.9f, 0.9f, 0.9f),
                Shininess = 128.0f,
                Alpha = alpha
            };
        }

        public static Material CreateGlass(Vector3 color, float alpha = 0.7f)
        {
            return new Material
            {
                Ambient = color * 0.05f,
                Diffuse = color * 0.2f,
                Specular = new Vector3(1.0f, 1.0f, 1.0f),
                Shininess = 256.0f,
                Alpha = alpha // 玻璃材质默认半透明
            };
        }
    }
}