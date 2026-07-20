using OpenTK.Graphics.OpenGL4;
using System;

namespace Avalonia3DControl.Rendering
{
    /// <summary>
    /// OpenGL渲染状态管理器，用于保存和恢复OpenGL状态
    /// </summary>
    public class RenderState : IDisposable
    {
        private int _currentProgram;
        private int _currentVAO;
        private bool _depthTestEnabled;
        private bool _blendEnabled;
        private int[] _viewport = new int[4];
        private bool _disposed = false;

        /// <summary>
        /// 保存当前OpenGL状态
        /// </summary>
        public void SaveState()
        {
            GL.GetInteger(GetPName.CurrentProgram, out _currentProgram);
            GL.GetInteger(GetPName.VertexArrayBinding, out _currentVAO);
            GL.GetInteger(GetPName.Viewport, _viewport);
            _depthTestEnabled = GL.IsEnabled(EnableCap.DepthTest);
            _blendEnabled = GL.IsEnabled(EnableCap.Blend);
        }

        /// <summary>
        /// 设置2D渲染状态
        /// </summary>
        public void Setup2DRenderState()
        {
            // 禁用深度测试，启用混合
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        /// <summary>
        /// 设置视口
        /// </summary>
        /// <param name="x">视口X坐标</param>
        /// <param name="y">视口Y坐标</param>
        /// <param name="width">视口宽度</param>
        /// <param name="height">视口高度</param>
        /// <returns>是否覆盖了原视口</returns>
        public bool SetViewport(int x, int y, int width, int height)
        {
            bool viewportOverridden = _viewport[0] != x || _viewport[1] != y || 
                                    _viewport[2] != width || _viewport[3] != height;
            if (viewportOverridden)
            {
                GL.Viewport(x, y, width, height);
            }
            return viewportOverridden;
        }

        /// <summary>
        /// 恢复OpenGL状态
        /// </summary>
        public void RestoreState()
        {
            // 恢复深度测试和混合状态
            if (_depthTestEnabled) 
                GL.Enable(EnableCap.DepthTest); 
            else 
                GL.Disable(EnableCap.DepthTest);
                
            if (!_blendEnabled) 
                GL.Disable(EnableCap.Blend);
                
            // 恢复着色器程序和VAO
            GL.UseProgram(_currentProgram);
            GL.BindVertexArray(_currentVAO);
        }

        /// <summary>
        /// 恢复视口
        /// </summary>
        public void RestoreViewport()
        {
            GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                RestoreState();
                _disposed = true;
            }
        }
    }
}