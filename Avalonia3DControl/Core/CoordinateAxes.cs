using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Geometry.Factories;
using Avalonia3DControl.Materials;

namespace Avalonia3DControl.Core
{
    /// <summary>
    /// 坐标轴组件，用于在3D场景中显示XYZ坐标轴
    /// 提供独立的坐标轴管理功能，包括显示控制、尺寸调整和渲染配置
    /// </summary>
    public class CoordinateAxes
    {
        private Model3D? _axesModel;
        
        /// <summary>
        /// 坐标轴是否可见
        /// </summary>
        public bool Visible { get; set; } = true;
        
        /// <summary>
        /// 坐标轴长度
        /// </summary>
        public float Length { get; set; } = 2.0f;
        
        /// <summary>
        /// 圆柱体半径
        /// </summary>
        public float CylinderRadius { get; set; } = 0.02f;
        
        /// <summary>
        /// 圆锥体半径（箭头）
        /// </summary>
        public float ConeRadius { get; set; } = 0.05f;
        
        /// <summary>
        /// 圆锥体高度（箭头）
        /// </summary>
        public float ConeHeight { get; set; } = 0.15f;
        
        /// <summary>
        /// 原点球体半径
        /// </summary>
        public float SphereRadius { get; set; } = 0.06f;
        
        /// <summary>
        /// 圆形分段数（影响圆滑度）
        /// </summary>
        public int Segments { get; set; } = 8;
        
        /// <summary>
        /// 坐标轴模型（只读）
        /// </summary>
        public Model3D? AxesModel => _axesModel;
        
        /// <summary>
        /// 标识这是坐标轴模型，需要特殊渲染处理
        /// </summary>
        public bool IsCoordinateAxes => true;
        
        public CoordinateAxes()
        {
            CreateAxesModel();
        }
        
        /// <summary>
        /// 创建坐标轴模型
        /// </summary>
        private void CreateAxesModel()
        {
            var vertices = new List<float>();
            var indices = new List<uint>();
            uint vertexIndex = 0;
            
            // 1. 创建原点球体（橙色）
            CreateSphere(vertices, indices, ref vertexIndex, Vector3.Zero, SphereRadius, 
                        new Vector3(1.0f, 0.5f, 0.0f), Segments); // 橙色
            
            // 2. 创建X轴（红色）
            CreateAxis(vertices, indices, ref vertexIndex, 
                      Vector3.UnitX, Length, CylinderRadius, ConeRadius, ConeHeight,
                      new Vector3(1.0f, 0.0f, 0.0f), Segments);
            
            // 3. 创建Y轴（绿色）
            CreateAxis(vertices, indices, ref vertexIndex, 
                      Vector3.UnitY, Length, CylinderRadius, ConeRadius, ConeHeight,
                      new Vector3(0.0f, 1.0f, 0.0f), Segments);
            
            // 4. 创建Z轴（蓝色）
            CreateAxis(vertices, indices, ref vertexIndex, 
                      Vector3.UnitZ, Length, CylinderRadius, ConeRadius, ConeHeight,
                      new Vector3(0.0f, 0.0f, 1.0f), Segments);
            
            _axesModel = new Model3D
            {
                Name = "CoordinateAxes",
                Vertices = vertices.ToArray(),
                Indices = indices.ToArray(),
                VertexCount = vertices.Count / 7, // 每个顶点7个float（位置3+颜色4）
                IndexCount = indices.Count,
                Material = Material.CreateMetal(new Vector3(0.8f, 0.8f, 0.8f)),
                Position = Vector3.Zero,
                Rotation = Vector3.Zero,
                Scale = Vector3.One,
                Visible = true
            };
        }
        
        /// <summary>
        /// 重新生成坐标轴模型（当参数改变时调用）
        /// </summary>
        public void RegenerateModel()
        {
            CreateAxesModel();
        }
        
        /// <summary>
        /// 获取模型矩阵（坐标轴始终返回单位矩阵，不受任何变换影响）
        /// </summary>
        /// <returns>单位矩阵</returns>
        public Matrix4 GetModelMatrix()
        {
            return Matrix4.Identity;
        }
        
        /// <summary>
        /// 创建单个坐标轴（圆柱+圆锥）
        /// </summary>
        private void CreateAxis(List<float> vertices, List<uint> indices, ref uint vertexIndex,
                               Vector3 direction, float length, float cylinderRadius, 
                               float coneRadius, float coneHeight, Vector3 color, int segments)
        {
            float cylinderLength = length - coneHeight;
            
            // 创建圆柱体
            CreateCylinder(vertices, indices, ref vertexIndex, Vector3.Zero, direction, 
                          cylinderLength, cylinderRadius, color, segments);
            
            // 创建圆锥体（箭头）
            Vector3 coneBase = direction * cylinderLength;
            CreateCone(vertices, indices, ref vertexIndex, coneBase, direction, 
                      coneHeight, coneRadius, color, segments);
        }
        
        /// <summary>
        /// 创建圆柱体
        /// </summary>
        private void CreateCylinder(List<float> vertices, List<uint> indices, ref uint vertexIndex,
                                   Vector3 start, Vector3 direction, float height, float radius, 
                                   Vector3 color, int segments)
        {
            Vector3 up = Math.Abs(Vector3.Dot(direction, Vector3.UnitY)) < 0.9f ? Vector3.UnitY : Vector3.UnitZ;
            Vector3 right = Vector3.Normalize(Vector3.Cross(direction, up));
            up = Vector3.Cross(right, direction);
            
            uint baseIndex = vertexIndex;
            
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2.0f * MathF.PI * i / segments;
                float x = MathF.Cos(angle) * radius;
                float y = MathF.Sin(angle) * radius;
                
                Vector3 offset = right * x + up * y;
                
                Vector3 bottomPos = start + offset;
                vertices.AddRange(new[] { bottomPos.X, bottomPos.Y, bottomPos.Z, color.X, color.Y, color.Z, 1.0f });
                
                Vector3 topPos = start + direction * height + offset;
                vertices.AddRange(new[] { topPos.X, topPos.Y, topPos.Z, color.X, color.Y, color.Z, 1.0f });
            }
            
            for (int i = 0; i < segments; i++)
            {
                uint bottom1 = baseIndex + (uint)(i * 2);
                uint top1 = bottom1 + 1;
                uint bottom2 = baseIndex + (uint)((i + 1) * 2);
                uint top2 = bottom2 + 1;
                
                indices.AddRange(new[] { bottom1, top1, bottom2 });
                indices.AddRange(new[] { top1, top2, bottom2 });
            }
            
            vertexIndex += (uint)((segments + 1) * 2);
        }
        
        /// <summary>
        /// 创建圆锥体
        /// </summary>
        private void CreateCone(List<float> vertices, List<uint> indices, ref uint vertexIndex,
                               Vector3 baseCenter, Vector3 direction, float height, float radius, 
                               Vector3 color, int segments)
        {
            Vector3 up = Math.Abs(Vector3.Dot(direction, Vector3.UnitY)) < 0.9f ? Vector3.UnitY : Vector3.UnitZ;
            Vector3 right = Vector3.Normalize(Vector3.Cross(direction, up));
            up = Vector3.Cross(right, direction);
            
            uint baseIndex = vertexIndex;
            
            Vector3 tip = baseCenter + direction * height;
            vertices.AddRange(new[] { tip.X, tip.Y, tip.Z, color.X, color.Y, color.Z, 1.0f });
            vertexIndex++;
            
            for (int i = 0; i <= segments; i++)
            {
                float angle = 2.0f * MathF.PI * i / segments;
                float x = MathF.Cos(angle) * radius;
                float y = MathF.Sin(angle) * radius;
                
                Vector3 offset = right * x + up * y;
                Vector3 pos = baseCenter + offset;
                vertices.AddRange(new[] { pos.X, pos.Y, pos.Z, color.X, color.Y, color.Z, 1.0f });
            }
            
            for (int i = 0; i < segments; i++)
            {
                indices.AddRange(new[] { baseIndex, baseIndex + 1 + (uint)i, baseIndex + 1 + (uint)((i + 1) % segments) });
            }
            
            vertexIndex += (uint)(segments + 1);
        }
        
        /// <summary>
        /// 创建球体
        /// </summary>
        private void CreateSphere(List<float> vertices, List<uint> indices, ref uint vertexIndex,
                                 Vector3 center, float radius, Vector3 color, int segments)
        {
            uint baseIndex = vertexIndex;
            
            // 简化的球体创建（使用较少的分段以提高性能）
            int rings = segments / 2;
            int sectors = segments;
            
            for (int r = 0; r <= rings; r++)
            {
                float phi = MathF.PI * r / rings;
                float y = MathF.Cos(phi);
                float ringRadius = MathF.Sin(phi);
                
                for (int s = 0; s <= sectors; s++)
                {
                    float theta = 2.0f * MathF.PI * s / sectors;
                    float x = ringRadius * MathF.Cos(theta);
                    float z = ringRadius * MathF.Sin(theta);
                    
                    Vector3 pos = center + new Vector3(x, y, z) * radius;
                    vertices.AddRange(new[] { pos.X, pos.Y, pos.Z, color.X, color.Y, color.Z, 1.0f });
                }
            }
            
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sectors; s++)
                {
                    uint current = baseIndex + (uint)(r * (sectors + 1) + s);
                    uint next = current + (uint)(sectors + 1);
                    
                    indices.AddRange(new[] { current, next, current + 1 });
                    indices.AddRange(new[] { current + 1, next, next + 1 });
                }
            }
            
            vertexIndex += (uint)((rings + 1) * (sectors + 1));
        }
    }
}