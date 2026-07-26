using System;
using OpenTK.Graphics.OpenGL;

namespace Avalonia3DControl.Rendering.OpenGL
{
    /// <summary>
    /// MGS4 screen glare/bloom (ENGINE defaults, ELF debug 2739):
    /// scene fields +964 glare_threshold = 1.0 and +960 glare_alpha = 1.0
    /// (stores in DG_ResetViewportSystem @0xDD34C/DD354; per-stage override via
    /// GCX NewMGS3GlareControl). Only radiance ABOVE the threshold blooms, which
    /// is what makes the 2006 sunlit ground glow: lit floor computes ~1.4-1.9 in
    /// the HDR chain and bleeds past 1.0.
    ///
    /// Implementation: the scene is rendered into an RGBA16F FBO so values > 1
    /// survive; bright-pass max(0, c - threshold), separable gaussian blur at
    /// half resolution, then scene + glow * alpha composited to the previous
    /// framebuffer (Avalonia's control FBO - NOT assumed to be 0).
    ///
    /// Note on curve order: the material shaders emit pow(x, 1/2.2). pow is
    /// monotonic with pow(1)=1, so "linear radiance > 1.0" and "post-curve
    /// value > 1.0" select the SAME pixels; thresholding after the curve is
    /// equivalent to the engine's linear-domain threshold for detection, and
    /// the additive glow stays in display space consistently.
    /// </summary>
    public sealed class GlareRenderer : IDisposable
    {
        // Off by default: opt-in post-process, not yet confirmed on-screen.
        public bool Enabled { get; set; } = false;
        /// <summary>ENGINE default 1.0 (ELF +964).</summary>
        public float Threshold { get; set; } = 1.0f;
        /// <summary>ENGINE default 1.0 (ELF +960).</summary>
        public float Alpha { get; set; } = 1.0f;

        private int _width, _height;
        private int _sceneFbo, _sceneTex, _sceneDepth;
        private readonly int[] _pingFbo = new int[2];
        private readonly int[] _pingTex = new int[2];
        private int _quadVao, _quadVbo;
        private int _progBright, _progBlur, _progComposite;
        private int _previousFbo;
        private bool _active;
        private bool _failed;
        private bool _failureLogged;

        private const string QuadVertexSrc = @"#version 330 core
layout(location = 0) in vec2 aPos;
out vec2 vUv;
void main()
{
    vUv = aPos * 0.5 + 0.5;
    gl_Position = vec4(aPos, 0.0, 1.0);
}";

        private const string BrightFragmentSrc = @"#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uScene;
uniform float uThreshold;
void main()
{
    vec3 c = texture(uScene, vUv).rgb;
    FragColor = vec4(max(c - vec3(uThreshold), vec3(0.0)), 1.0);
}";

        private const string BlurFragmentSrc = @"#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uSource;
uniform vec2 uTexelStep; // (1/w, 0) for horizontal, (0, 1/h) for vertical
void main()
{
    // 9-tap gaussian, sigma ~= 2.6 in half-res texels
    float w0 = 0.1633; float w1 = 0.1531; float w2 = 0.1224;
    float w3 = 0.0918; float w4 = 0.0510;
    vec3 sum = texture(uSource, vUv).rgb * w0;
    sum += texture(uSource, vUv + uTexelStep * 1.0).rgb * w1;
    sum += texture(uSource, vUv - uTexelStep * 1.0).rgb * w1;
    sum += texture(uSource, vUv + uTexelStep * 2.0).rgb * w2;
    sum += texture(uSource, vUv - uTexelStep * 2.0).rgb * w2;
    sum += texture(uSource, vUv + uTexelStep * 3.0).rgb * w3;
    sum += texture(uSource, vUv - uTexelStep * 3.0).rgb * w3;
    sum += texture(uSource, vUv + uTexelStep * 4.0).rgb * w4;
    sum += texture(uSource, vUv - uTexelStep * 4.0).rgb * w4;
    FragColor = vec4(sum, 1.0);
}";

        private const string CompositeFragmentSrc = @"#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uScene;
uniform sampler2D uGlow;
uniform float uAlpha;
void main()
{
    vec3 scene = texture(uScene, vUv).rgb;
    vec3 glow = texture(uGlow, vUv).rgb;
    FragColor = vec4(scene + glow * uAlpha, 1.0);
}";

        /// <summary>
        /// Redirect subsequent rendering into the internal HDR framebuffer.
        /// Returns false (and leaves state untouched) when disabled or when the
        /// GPU resources could not be created; the caller then renders normally.
        /// </summary>
        public bool Begin(int width, int height)
        {
            if (!Enabled || _failed || width <= 0 || height <= 0)
            {
                return false;
            }

            _previousFbo = GL.GetInteger(GetPName.FramebufferBinding);

            if (!EnsureResources(width, height))
            {
                _failed = true;
                if (!_failureLogged)
                {
                    _failureLogged = true;
                    RenderLog.Glare("framebuffer setup FAILED -> glare self-disabled (GPU/driver refused RGBA16F FBO)");
                }
                return false;
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
            GL.Viewport(0, 0, _width, _height);
            _active = true;
            return true;
        }

        /// <summary>
        /// Bright-pass + blur the captured scene, then composite scene + glow
        /// into the framebuffer that was bound before Begin().
        /// </summary>
        public void End()
        {
            if (!_active)
            {
                return;
            }
            _active = false;

            bool blendWasEnabled = GL.IsEnabled(EnableCap.Blend);
            bool depthWasEnabled = GL.IsEnabled(EnableCap.DepthTest);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Blend);

            int halfW = Math.Max(1, _width / 2);
            int halfH = Math.Max(1, _height / 2);

            // 1) bright-pass into ping[0] (half res)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _pingFbo[0]);
            GL.Viewport(0, 0, halfW, halfH);
            GL.UseProgram(_progBright);
            BindTexture0(_progBright, "uScene", _sceneTex);
            GL.Uniform1(GL.GetUniformLocation(_progBright, "uThreshold"), Threshold);
            DrawQuad();

            // 2) horizontal blur ping[0] -> ping[1]
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _pingFbo[1]);
            GL.UseProgram(_progBlur);
            BindTexture0(_progBlur, "uSource", _pingTex[0]);
            GL.Uniform2(GL.GetUniformLocation(_progBlur, "uTexelStep"), 1.0f / halfW, 0.0f);
            DrawQuad();

            // 3) vertical blur ping[1] -> ping[0]
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _pingFbo[0]);
            BindTexture0(_progBlur, "uSource", _pingTex[1]);
            GL.Uniform2(GL.GetUniformLocation(_progBlur, "uTexelStep"), 0.0f, 1.0f / halfH);
            DrawQuad();

            // 4) composite scene + glow to the caller's framebuffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _previousFbo);
            GL.Viewport(0, 0, _width, _height);
            GL.UseProgram(_progComposite);
            BindTexture0(_progComposite, "uScene", _sceneTex);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _pingTex[0]);
            GL.Uniform1(GL.GetUniformLocation(_progComposite, "uGlow"), 1);
            GL.Uniform1(GL.GetUniformLocation(_progComposite, "uAlpha"), Alpha);
            DrawQuad();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.UseProgram(0);
            if (depthWasEnabled) GL.Enable(EnableCap.DepthTest);
            if (blendWasEnabled) GL.Enable(EnableCap.Blend);

            var err = GL.GetError();
            if (err != ErrorCode.NoError)
            {
                _failed = true;
                RenderLog.Glare($"composite raised GL error {err} -> glare disabled (press G to retry after fix)");
            }
        }

        private static void BindTexture0(int program, string name, int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.Uniform1(GL.GetUniformLocation(program, name), 0);
        }

        private void DrawQuad()
        {
            GL.BindVertexArray(_quadVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);
        }

        private bool EnsureResources(int width, int height)
        {
            if (_sceneFbo != 0 && width == _width && height == _height)
            {
                return true;
            }

            _width = width;
            _height = height;
            ReleaseFramebuffers();

            if (_quadVao == 0 && !CreateQuadAndPrograms())
            {
                return false;
            }

            // full-res HDR scene target with depth
            _sceneTex = CreateColorTexture(_width, _height);
            _sceneFbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _sceneTex, 0);
            _sceneDepth = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _sceneDepth);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer,
                RenderbufferStorage.DepthComponent24, _width, _height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _sceneDepth);
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _previousFbo);
                return false;
            }

            // half-res ping-pong for blur
            int halfW = Math.Max(1, _width / 2);
            int halfH = Math.Max(1, _height / 2);
            for (int i = 0; i < 2; i++)
            {
                _pingTex[i] = CreateColorTexture(halfW, halfH);
                _pingFbo[i] = GL.GenFramebuffer();
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _pingFbo[i]);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                    FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _pingTex[i], 0);
                if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                {
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, _previousFbo);
                    return false;
                }
            }

            // Self-test: FBO completeness can pass while float rendering is
            // broken (some drivers), which would black the whole viewport. Render
            // a known colour into the RGBA16F target and read it back; if it does
            // not survive, disable glare and let the scene render normally.
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
            GL.Viewport(0, 0, _width, _height);
            GL.ClearColor(0.5f, 0.25f, 0.125f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            float[] probe = new float[4];
            GL.ReadPixels(0, 0, 1, 1, PixelFormat.Rgba, PixelType.Float, probe);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _previousFbo);
            if (MathF.Abs(probe[0] - 0.5f) > 0.1f)
            {
                RenderLog.Glare($"self-test FAILED: RGBA16F readback=({probe[0]:F2},{probe[1]:F2},{probe[2]:F2}) expected ~0.50 -> glare disabled (driver float-FBO issue)");
                return false;
            }
            RenderLog.Glare($"ready: scene FBO {_width}x{_height} RGBA16F + depth24, self-test OK, prevFBO={_previousFbo}, threshold={Threshold:F2} alpha={Alpha:F2}");
            return true;
        }

        private static int CreateColorTexture(int width, int height)
        {
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f,
                width, height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return tex;
        }

        private bool CreateQuadAndPrograms()
        {
            float[] quad =
            {
                -1f, -1f,  1f, -1f,  1f,  1f,
                -1f, -1f,  1f,  1f, -1f,  1f
            };
            _quadVao = GL.GenVertexArray();
            _quadVbo = GL.GenBuffer();
            GL.BindVertexArray(_quadVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quad.Length * sizeof(float), quad, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.BindVertexArray(0);

            _progBright = CompileProgram(QuadVertexSrc, BrightFragmentSrc);
            _progBlur = CompileProgram(QuadVertexSrc, BlurFragmentSrc);
            _progComposite = CompileProgram(QuadVertexSrc, CompositeFragmentSrc);
            return _progBright != 0 && _progBlur != 0 && _progComposite != 0;
        }

        private static int CompileProgram(string vertexSrc, string fragmentSrc)
        {
            int vs = CompileShader(ShaderType.VertexShader, vertexSrc);
            int fs = CompileShader(ShaderType.FragmentShader, fragmentSrc);
            if (vs == 0 || fs == 0)
            {
                return 0;
            }
            int program = GL.CreateProgram();
            GL.AttachShader(program, vs);
            GL.AttachShader(program, fs);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int ok);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            if (ok == 0)
            {
                RenderLog.Glare("shader link error: " + GL.GetProgramInfoLog(program));
                GL.DeleteProgram(program);
                return 0;
            }
            return program;
        }

        private static int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0)
            {
                RenderLog.Glare(type + " compile error: " + GL.GetShaderInfoLog(shader));
                GL.DeleteShader(shader);
                return 0;
            }
            return shader;
        }

        private void ReleaseFramebuffers()
        {
            if (_sceneFbo != 0) { GL.DeleteFramebuffer(_sceneFbo); _sceneFbo = 0; }
            if (_sceneTex != 0) { GL.DeleteTexture(_sceneTex); _sceneTex = 0; }
            if (_sceneDepth != 0) { GL.DeleteRenderbuffer(_sceneDepth); _sceneDepth = 0; }
            for (int i = 0; i < 2; i++)
            {
                if (_pingFbo[i] != 0) { GL.DeleteFramebuffer(_pingFbo[i]); _pingFbo[i] = 0; }
                if (_pingTex[i] != 0) { GL.DeleteTexture(_pingTex[i]); _pingTex[i] = 0; }
            }
        }

        public void Dispose()
        {
            ReleaseFramebuffers();
            if (_quadVbo != 0) { GL.DeleteBuffer(_quadVbo); _quadVbo = 0; }
            if (_quadVao != 0) { GL.DeleteVertexArray(_quadVao); _quadVao = 0; }
            if (_progBright != 0) { GL.DeleteProgram(_progBright); _progBright = 0; }
            if (_progBlur != 0) { GL.DeleteProgram(_progBlur); _progBlur = 0; }
            if (_progComposite != 0) { GL.DeleteProgram(_progComposite); _progComposite = 0; }
        }
    }
}
