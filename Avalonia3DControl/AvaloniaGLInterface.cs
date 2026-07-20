using Avalonia.OpenGL;
using OpenTK;
using System;

namespace Avalonia3DControl
{
    /// <summary>
    /// Avalonia OpenGL接口到OpenTK的桥接类
    /// </summary>
    public class AvaloniaGLInterface : IBindingsContext
    {
        private readonly GlInterface _gl;

        public AvaloniaGLInterface(GlInterface gl)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        }

        public IntPtr GetProcAddress(string procName)
        {
            return _gl.GetProcAddress(procName);
        }
    }
}