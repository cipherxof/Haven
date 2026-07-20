using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;

namespace Avalonia3DControl.Rendering
{
    /// <summary>
    /// 几何体渲染器，负责创建和渲染基本几何体
    /// </summary>
    public class GeometryRenderer : IDisposable
    {
        private int _quadVAO;
        private int _quadVBO;
        private int _quadEBO;
        private int _lineVAO;
        private int _lineVBO;
        private bool _disposed = false;

        /// <summary>
        /// 初始化几何体渲染器
        /// </summary>
        public void Initialize()
        {
            CreateQuadGeometry();
            CreateLineGeometry();
        }

        /// <summary>
        /// 创建四边形几何体
        /// </summary>
        private void CreateQuadGeometry()
        {
            // 创建VAO
            _quadVAO = GL.GenVertexArray();
            GL.BindVertexArray(_quadVAO);

            // 顶点数据（位置 + 纹理坐标）
            float[] vertices = {
                // 位置        纹理坐标
                -1.0f,  1.0f, 0.0f,  0.0f, 1.0f, // 左上
                -1.0f, -1.0f, 0.0f,  0.0f, 0.0f, // 左下
                 1.0f, -1.0f, 0.0f,  1.0f, 0.0f, // 右下
                 1.0f,  1.0f, 0.0f,  1.0f, 1.0f  // 右上
            };

            // 索引数据
            uint[] indices = {
                0, 1, 2,
                2, 3, 0
            };

            // 创建VBO
            _quadVBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // 创建EBO
            _quadEBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _quadEBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // 设置顶点属性
            // 位置属性
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // 纹理坐标属性
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }

        /// <summary>
        /// 创建线条几何体
        /// </summary>
        private void CreateLineGeometry()
        {
            _lineVAO = GL.GenVertexArray();
            _lineVBO = GL.GenBuffer();
            GL.BindVertexArray(_lineVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _lineVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, 1024 * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw); // 预分配
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// 渲染四边形
        /// </summary>
        /// <param name="vertices">顶点数据</param>
        public void RenderQuad(float[] vertices)
        {
            if (vertices.Length != 20) // 4个顶点 * 5个分量
                throw new ArgumentException("四边形顶点数据必须包含20个浮点数（4个顶点，每个5个分量）");

            uint[] indices = { 0, 1, 2, 2, 3, 0 };

            GL.BindVertexArray(_quadVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _quadEBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.DynamicDraw);

            GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
        }

        /// <summary>
        /// 渲染线条
        /// </summary>
        /// <param name="vertices">线条顶点数据</param>
        /// <param name="vertexCount">顶点数量</param>
        public void RenderLines(float[] vertices, int vertexCount)
        {
            if (vertices.Length < vertexCount * 2)
                throw new ArgumentException("顶点数据不足");

            GL.BindVertexArray(_lineVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _lineVBO);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertexCount * 2 * sizeof(float), vertices);
            GL.DrawArrays(PrimitiveType.Lines, 0, vertexCount);
        }

        /// <summary>
        /// 计算四边形顶点数据
        /// </summary>
        /// <param name="left">左边界</param>
        /// <param name="right">右边界</param>
        /// <param name="top">上边界</param>
        /// <param name="bottom">下边界</param>
        /// <returns>顶点数据数组</returns>
        public static float[] CreateQuadVertices(float left, float right, float top, float bottom)
        {
            return new float[]
            {
                left,  top,    0.0f,  0.0f, 1.0f, // 左上
                left,  bottom, 0.0f,  0.0f, 0.0f, // 左下
                right, bottom, 0.0f,  1.0f, 0.0f, // 右下
                right, top,    0.0f,  1.0f, 1.0f  // 右上
            };
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_quadVAO != 0)
                {
                    GL.DeleteVertexArray(_quadVAO);
                    GL.DeleteBuffer(_quadVBO);
                    GL.DeleteBuffer(_quadEBO);
                }

                if (_lineVAO != 0)
                {
                    GL.DeleteVertexArray(_lineVAO);
                    GL.DeleteBuffer(_lineVBO);
                }

                _disposed = true;
            }
        }
    }
}