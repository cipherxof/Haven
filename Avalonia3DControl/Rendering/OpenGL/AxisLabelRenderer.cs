using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;

namespace Avalonia3DControl.Rendering.OpenGL
{
    /// <summary>
    /// 轴标签渲染器，负责渲染坐标轴标签
    /// </summary>
    public class AxisLabelRenderer
    {
        /// <summary>
        /// 渲染轴标签
        /// </summary>
        /// <param name="shaderProgram">着色器程序</param>
        /// <param name="view">视图矩阵</param>
        /// <param name="projection">投影矩阵</param>
        public void RenderAxisLabels(int shaderProgram, Matrix4 view, Matrix4 projection)
        {
            // 设置线条渲染模式，增加线宽使标注更清晰
            GL.LineWidth(8.0f);
            
            // 标注位置（放在各轴顶端并略微前移，避免与轴重叠）
            float labelSize = 0.6f; // 与字母绘制大小保持一致
            float offsetAlong = labelSize + 0.02f; // 沿轴方向的额外偏移，确保完全在轴尖之外
            float labelBase = 1.0f; // 轴的顶端（迷你坐标轴模型长度为1）
            Vector3[] labelPositions = {
                new Vector3(labelBase + offsetAlong, 0, 0), // X标注位置：轴尖之外
                new Vector3(0, labelBase + offsetAlong, 0), // Y标注位置：轴尖之外
                new Vector3(0, 0, labelBase + offsetAlong)  // Z标注位置：轴尖之外
            };
            
            Vector3[] labelColors = {
                new Vector3(1.0f, 0.0f, 0.0f), // X - 红色
                new Vector3(0.0f, 1.0f, 0.0f), // Y - 绿色
                new Vector3(0.0f, 0.0f, 1.0f)  // Z - 蓝色
            };
            
            // 渲染每个标注
            for (int i = 0; i < 3; i++)
            {
                Vector3 position = labelPositions[i];
                Vector3 color = labelColors[i];
                
                // 设置模型矩阵（移动到标注位置）
                Matrix4 labelModel = Matrix4.CreateTranslation(position);
                SetMatrix(shaderProgram, "model", labelModel);
                
                // 根据轴绘制对应的字母（使用填充三角形）
                switch (i)
                {
                    case 0: // X
                        DrawFilledLetterX(color);
                        break;
                    case 1: // Y
                        DrawFilledLetterY(color);
                        break;
                    case 2: // Z
                        DrawFilledLetterZ(color);
                        break;
                }
            }
            
            GL.LineWidth(1.0f); // 恢复默认线宽
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
        private void DrawFilledLetterX(Vector3 color)
        {
           float size = 0.2f;  // 字母大小
             float width = 0.06f; // 笔画宽度
            
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
        private void DrawFilledLetterY(Vector3 color)
        {
            float size = 0.2f;  // 字母大小
            float width = 0.06f; // 笔画宽度
            
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
        private void DrawFilledLetterZ(Vector3 color)
        {
            float size = 0.2f;  // 字母大小
             float width = 0.06f; // 笔画宽度（更粗）
            
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
    }
}