using System;
using OpenTK.Mathematics;
using Avalonia3DControl.Materials;
using Avalonia3DControl.Geometry.Factories;

namespace Avalonia3DControl.Core.Models
{
    /// <summary>
    /// Blend equations a model can request. Mirrors the MGS4/MGO2 MDN packet blend-mode
    /// index (low nibble of the packet flag).
    /// </summary>
    public enum ModelBlendMode
    {
        Alpha = 0,            // src*srcA + dst*(1-srcA)
        Additive = 1,         // src*srcA + dst
        ReverseSubtract = 2,  // dst - src*srcA
        Multiply = 3          // src*dst
    }

    /// <summary>
    /// 三维模型类
    /// </summary>
    public class Model3D
    {
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public Vector3 Color { get; set; }
        public float Alpha { get; set; }
        public float DepthBias { get; set; }
        public int TextureId { get; set; }
        public Material Material { get; set; }
        public RenderMode? RenderModeOverride { get; set; }
        public bool Visible { get; set; }
        public string Name { get; set; }

        /// <summary>Workspace MDN asset that produced this render packet, without extension.</summary>
        public string SourceAssetName { get; set; } = string.Empty;

        public int MaterialIndex { get; set; }

        // ---- Per-packet render state (decoded from the MDN packet flag; see
        // mdn-packet-flag-semantics). Defaults keep every non-MDN model rendering as before. ----

        /// <summary>Enable hardware blending for this model (MDN packet flag bit 0x10).</summary>
        public bool BlendEnabled { get; set; } = false;

        /// <summary>Blend equation/factors to use when <see cref="BlendEnabled"/> is set.</summary>
        public ModelBlendMode BlendMode { get; set; } = ModelBlendMode.Alpha;

        /// <summary>Write to the depth buffer. Blend layers (flag bit 0x200) clear this.</summary>
        public bool WriteDepth { get; set; } = true;

        /// <summary>Multiply fragment coverage by vertex alpha (terrain layer blend mask).</summary>
        public bool UseVertexAlpha { get; set; } = false;

        /// <summary>Discard fragments whose coverage falls below this (0 = no test; 0.5 = cutout).</summary>
        public float AlphaTestRef { get; set; } = 0.0f;

        /// <summary>Force output alpha to 1.0 (opaque and alpha-tested cutout packets).</summary>
        public bool ForceOpaqueAlpha { get; set; } = false;

        /// <summary>Whether this model contributes geometry to the directional shadow map.</summary>
        public bool CastsShadow { get; set; } = false;

        /// <summary>Whether the normal color pass applies the directional shadow map to this model.</summary>
        public bool ReceivesShadow { get; set; } = false;
        // 几何数据
        public float[] Vertices { get; set; } = Array.Empty<float>();
        public float[] Positions { get; set; } = Array.Empty<float>();
        public float[] Colors { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Per-vertex LT3 lighting after removing only the projected directional sun.
        /// This keeps the exact ambient, hemi and local-light RGB in shadow instead of
        /// approximating it with a single luminance weight. Stored as RGB triples.
        /// </summary>
        public float[] ShadowedColors { get; set; } = Array.Empty<float>();

        public float[] UVs { get; set; } = Array.Empty<float>();
        public uint[] Indices { get; set; } = Array.Empty<uint>();
        public int VertexCount { get; set; }
        public int IndexCount { get; set; }
        
        /// <summary>
        /// 顶点是否需要更新（用于渲染器优化）
        /// </summary>
        public bool VerticesNeedUpdate { get; set; } = false;

        /// <summary>
        /// 索引缓冲区是否需要更新。
        /// </summary>
        public bool IndicesNeedUpdate { get; set; } = false;
        
        public Model3D()
        {
            Position = Vector3.Zero;
            Rotation = Vector3.Zero;
            Scale = Vector3.One;
            Color = Vector3.One;
            Alpha = 1.0f;
            DepthBias = 0.0f;
            TextureId = 0;
            Material = new Material();
            RenderModeOverride = null;
            Visible = true;
            Name = "Model";
            MaterialIndex = -1;
            VerticesNeedUpdate = false;
            IndicesNeedUpdate = false;
        }
        
        /// <summary>
        /// 获取顶点数据
        /// </summary>
        /// <returns>顶点数据数组</returns>
        public virtual float[] GetVertexData()
        {
            return Vertices;
        }

        public virtual Matrix4 GetModelMatrix()
        {
            var translation = Matrix4.CreateTranslation(Position);
            var rotationX = Matrix4.CreateRotationX(Rotation.X);
            var rotationY = Matrix4.CreateRotationY(Rotation.Y);
            var rotationZ = Matrix4.CreateRotationZ(Rotation.Z);
            var scale = Matrix4.CreateScale(Scale);

            // OpenTK transforms positions as row vectors. Apply local scale/rotation first
            // and world translation last so rotation never rotates the object's world origin.
            return scale * rotationX * rotationY * rotationZ * translation;
        }
        
        /// <summary>
        /// 计算模型的边界框（在世界坐标系中）
        /// </summary>
        /// <returns>边界框的最小和最大坐标</returns>
        public (Vector3 Min, Vector3 Max) GetBoundingBox()
        {
            int vertexCount = VertexCount;
            if (vertexCount <= 0)
            {
                vertexCount = Positions.Length / 3;
                if (vertexCount <= 0)
                {
                    vertexCount = Vertices.Length / 6;
                }
            }

            if (vertexCount <= 0)
            {
                return (Vector3.Zero, Vector3.Zero);
            }
            
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            
            if (Positions.Length >= vertexCount * 3)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    int src = i * 3;
                    var vertex = new Vector3(Positions[src], Positions[src + 1], Positions[src + 2]);

                    var transformedVertex = Vector3.TransformPosition(vertex, GetModelMatrix());

                    min = Vector3.ComponentMin(min, transformedVertex);
                    max = Vector3.ComponentMax(max, transformedVertex);
                }

                return (min, max);
            }

            for (int i = 0; i < vertexCount; i++)
            {
                int src = i * 6;
                if (src + 2 >= Vertices.Length)
                {
                    break;
                }

                var vertex = new Vector3(Vertices[src], Vertices[src + 1], Vertices[src + 2]);

                var transformedVertex = Vector3.TransformPosition(vertex, GetModelMatrix());

                min = Vector3.ComponentMin(min, transformedVertex);
                max = Vector3.ComponentMax(max, transformedVertex);
            }
            
            return (min, max);
        }
        
        /// <summary>
        /// 获取模型的中心点（在世界坐标系中）
        /// </summary>
        /// <returns>模型中心点</returns>
        public Vector3 GetCenter()
        {
            var (min, max) = GetBoundingBox();
            return (min + max) * 0.5f;
        }
        
        /// <summary>
        /// 获取模型的尺寸
        /// </summary>
        /// <returns>模型在各轴上的尺寸</returns>
        public Vector3 GetSize()
        {
            var (min, max) = GetBoundingBox();
            return max - min;
        }
    }


}
