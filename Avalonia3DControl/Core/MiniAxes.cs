using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Geometry.Factories;
using Avalonia3DControl.Materials;

namespace Avalonia3DControl.Core
{
    /// <summary>
    /// 迷你坐标轴位置枚举
    /// </summary>
    public enum MiniAxesPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// 迷你坐标轴组件，用于在屏幕角落显示小型XYZ指示器
    /// </summary>
    public class MiniAxes
    {
        public bool Visible { get; set; } = true;
        public MiniAxesPosition Position { get; set; } = MiniAxesPosition.BottomLeft;
        public Model3D? AxesModel { get; private set; }
        public float Size { get; set; } = 1.2f; // 迷你坐标轴的大小（更短避免截断）
        
        /// <summary>
        /// 屏幕空间中的位置偏移（像素）
        /// </summary>
        public Vector2 ScreenOffset { get; set; } = new Vector2(100, 100);
        
        public MiniAxes()
        {
            CreateAxesModel();
        }
        
        /// <summary>
        /// 创建迷你坐标轴模型
        /// </summary>
        private void CreateAxesModel()
        {
            var vertices = new List<float>();
            var indices = new List<uint>();
            uint vertexIndex = 0;
            
            float axisLength = Size;
            float cylinderRadius = 0.05f;  // 增加圆柱体半径，让坐标轴更粗
            float coneRadius = 0.1f;      // 增加圆锥体半径，让箭头更粗
            float coneHeight = 0.08f;
            int segments = 6; // 简化分段数以提高性能
            
            // 创建X轴（红色）
            CreateMiniAxis(vertices, indices, ref vertexIndex, 
                          Vector3.UnitX, axisLength, cylinderRadius, coneRadius, coneHeight,
                          new Vector3(1.0f, 0.0f, 0.0f), segments);
            
            // 创建Y轴（绿色）
            CreateMiniAxis(vertices, indices, ref vertexIndex, 
                          Vector3.UnitY, axisLength, cylinderRadius, coneRadius, coneHeight,
                          new Vector3(0.0f, 1.0f, 0.0f), segments);
            
            // 创建Z轴（蓝色）
            CreateMiniAxis(vertices, indices, ref vertexIndex, 
                          Vector3.UnitZ, axisLength, cylinderRadius, coneRadius, coneHeight,
                          new Vector3(0.0f, 0.0f, 1.0f), segments);
            
            AxesModel = new Model3D
            {
                Name = "MiniAxes",
                Vertices = vertices.ToArray(),
                Indices = indices.ToArray(),
                VertexCount = vertices.Count / 7,
                IndexCount = indices.Count,
                Material = Material.CreateMetal(new Vector3(0.8f, 0.8f, 0.8f))
            };
        }
        
        /// <summary>
        /// 创建单个迷你坐标轴
        /// </summary>
        private void CreateMiniAxis(List<float> vertices, List<uint> indices, ref uint vertexIndex,
                                   Vector3 direction, float length, float cylinderRadius, 
                                   float coneRadius, float coneHeight, Vector3 color, int segments)
        {
            float cylinderLength = length - coneHeight;
            
            // 创建圆柱体
            CreateMiniCylinder(vertices, indices, ref vertexIndex, Vector3.Zero, direction, 
                              cylinderLength, cylinderRadius, color, segments);
            
            // 创建圆锥体（箭头）
            Vector3 coneBase = direction * cylinderLength;
            CreateMiniCone(vertices, indices, ref vertexIndex, coneBase, direction, 
                          coneHeight, coneRadius, color, segments);
        }
        
        /// <summary>
        /// 创建迷你圆柱体
        /// </summary>
        private void CreateMiniCylinder(List<float> vertices, List<uint> indices, ref uint vertexIndex,
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
        /// 创建迷你圆锥体
        /// </summary>
        private void CreateMiniCone(List<float> vertices, List<uint> indices, ref uint vertexIndex,
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
        /// 获取屏幕空间中的位置
        /// </summary>
        /// <param name="screenWidth">屏幕宽度</param>
        /// <param name="screenHeight">屏幕高度</param>
        /// <returns>屏幕坐标</returns>
        public Vector2 GetScreenPosition(int screenWidth, int screenHeight)
        {
            return Position switch
            {
                MiniAxesPosition.TopLeft => new Vector2(ScreenOffset.X, ScreenOffset.Y),
                MiniAxesPosition.TopRight => new Vector2(screenWidth - ScreenOffset.X, ScreenOffset.Y),
                MiniAxesPosition.BottomLeft => new Vector2(ScreenOffset.X, screenHeight - ScreenOffset.Y),
                MiniAxesPosition.BottomRight => new Vector2(screenWidth - ScreenOffset.X, screenHeight - ScreenOffset.Y),
                _ => new Vector2(screenWidth - ScreenOffset.X, screenHeight - ScreenOffset.Y)
            };
        }
    }
}