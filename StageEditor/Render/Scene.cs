using Haven.Properties;
using Haven.Render._3D;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Serilog;
using System.Text;

namespace Haven.Render
{
    public class Scene
    {
        public static Scene? CurrentScene { get; private set; }
        public Shader meshShader { get; private set; }

        public Drawable3D? SelectedDrawable;
        public IList<Drawable3D> Children { get; set; }
        public readonly Camera Camera;
        public event Action<Mesh?> MeshSelected;
        public event Action<List<Mesh>?> DragSelectDone;

        private GLControl glControl;
        private bool firstMove = true;
        private Vector2 lastPosition;
        private bool initialized = false;

        private int combinedVAO, combinedVBO, combinedNBO, combinedCBO, combinedEBO;
        private int[] drawCounts;
        private IntPtr[] drawOffsets;
        private int gridVAO, gridVBO;
        private int gridVertexCount;

        private bool isDragging = false;
        private bool isShiftDragging = false;
        private Point dragStart;
        private Point dragEnd;

        private Mesh? hoveredMesh = null;
        private ToolTip tooltip;
        private Point lastMousePos;
        private int mouseStillTimer = 0;
        private const int TOOLTIP_DELAY = 16; // ms

        private int floorHeightFBO, floorHeightTexture, floorHeightDepthBuffer;
        private const int FLOOR_HEIGHT_BUFFER_SIZE = 32;
        private float[] floorHeightPixels = new float[FLOOR_HEIGHT_BUFFER_SIZE * FLOOR_HEIGHT_BUFFER_SIZE];

        private int occlusionQueryFBO;
        private int occlusionQueryDepthTexture;
        private readonly Dictionary<Mesh, uint> occlusionQueries = new Dictionary<Mesh, uint>();
        private readonly Dictionary<Mesh, bool> occlusionResults = new Dictionary<Mesh, bool>();

        public bool GridEnabled { get; set; } = true;
        public int GridRows { get; set; } = 200;
        public int GridCols { get; set; } = 200;
        public float CellSize { get; set; } = 4000f;
        public Vector3 GridColor { get; set; } = new Vector3(0.5f, 0.5f, 0.5f);

        public Scene(GLControl control)
        {
            glControl = control;

            this.Camera = new Camera(new Vector3d(0, 0, 0), control.ClientSize.Width / control.ClientSize.Height);
            this.Children = new List<Drawable3D>();

            this.Camera.Position = new Vector3d(0, 10, 10);
            this.Camera.Yaw = -90;
            this.Camera.Pitch = -45;

            control.Paint += glControl_Paint;
            control.Resize += glControl_Resize;
            control.MouseWheel += glControl_MouseScroll;
            control.MouseUp += glControl_MouseUp;
            control.MouseMove += glControl_MouseMove;
            control.KeyPress += glControl_KeyPress;
            control.MouseDoubleClick += glControl_MouseDoubleClick;
            control.MouseDown += glControl_MouseDown;
            control.KeyDown += glControl_KeyDown;
            control.KeyUp += glControl_KeyUp;
            control.MouseLeave += glControl_MouseLeave;

            tooltip = new ToolTip();
            tooltip.ShowAlways = true;
            tooltip.UseAnimation = false;
            tooltip.UseFading = false;

            var hoverTimer = new System.Windows.Forms.Timer();
            hoverTimer.Interval = 16;
            hoverTimer.Tick += HoverTimer_Tick;
            hoverTimer.Start();

            CurrentScene = this;
        }

        private void Initialize()
        {
            initialized = true;

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.FrontFace(FrontFaceDirection.Ccw);
            GL.ClearColor(0.316f, 0.316f, 0.316f, 1.0f);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            var vert = Encoding.UTF8.GetString(Resources.ShaderVert);
            var frag = Encoding.UTF8.GetString(Resources.ShaderFrag);

            meshShader = new Shader(vert, frag);

            InitializeFloorHeightFramebuffer();
            InitializeOcclusionResources();
        }

        private void InitializeFloorHeightFramebuffer()
        {
            floorHeightFBO = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, floorHeightFBO);

            // Create depth texture
            floorHeightTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, floorHeightTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f,
                         FLOOR_HEIGHT_BUFFER_SIZE, FLOOR_HEIGHT_BUFFER_SIZE, 0,
                         PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                                   TextureTarget.Texture2D, floorHeightTexture, 0);

            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                Log.Error("Floor height framebuffer not complete!");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void InitializeOcclusionResources()
        {
            GL.GenFramebuffers(1, out occlusionQueryFBO);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, occlusionQueryFBO);

            GL.GenTextures(1, out occlusionQueryDepthTexture);
            GL.BindTexture(TextureTarget.Texture2D, occlusionQueryDepthTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32,
                         glControl.Width, glControl.Height, 0,
                         PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                                  TextureTarget.Texture2D, occlusionQueryDepthTexture, 0);

            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                Log.Error("Failed to create occlusion query framebuffer");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public void ResizeOcclusionResources()
        {
            if (occlusionQueryDepthTexture > 0)
            {
                GL.BindTexture(TextureTarget.Texture2D, occlusionQueryDepthTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32,
                             glControl.Width, glControl.Height, 0,
                             PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
        }

        private void CleanupOcclusionResources()
        {
            foreach (var query in occlusionQueries.Values)
            {
                GL.DeleteQuery(query);
            }
            occlusionQueries.Clear();

            if (occlusionQueryDepthTexture > 0)
            {
                GL.DeleteTexture(occlusionQueryDepthTexture);
                occlusionQueryDepthTexture = 0;
            }

            if (occlusionQueryFBO > 0)
            {
                GL.DeleteFramebuffer(occlusionQueryFBO);
                occlusionQueryFBO = 0;
            }
        }

        private void TestMeshVisibility(List<Mesh> meshesToTest)
        {
            foreach (var mesh in meshesToTest)
            {
                if (!occlusionQueries.ContainsKey(mesh))
                {
                    uint queryId;
                    GL.GenQueries(1, out queryId);
                    occlusionQueries[mesh] = queryId;
                }
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, occlusionQueryFBO);
            GL.Viewport(0, 0, glControl.Width, glControl.Height);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.ColorMask(false, false, false, false);
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);

            meshShader.Use();
            meshShader.SetMatrix4("view", Camera.GetViewMatrix().ToMatrix4());
            meshShader.SetMatrix4("projection", Camera.GetProjectionMatrix().ToMatrix4());
            foreach (var child in Children)
            {
                if (child is Mesh m && m.Visible && !meshesToTest.Contains(m))
                {
                    m.Draw();
                }
            }

            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(false);

            foreach (var mesh in meshesToTest)
            {
                if (mesh.Visible && mesh.DragSelectable)
                {
                    GL.BeginQuery(QueryTarget.SamplesPassed, occlusionQueries[mesh]);
                    mesh.Draw();
                    GL.EndQuery(QueryTarget.SamplesPassed);
                }
            }

            GL.ColorMask(true, true, true, true);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            occlusionResults.Clear();
            foreach (var mesh in meshesToTest)
            {
                if (mesh.Visible && mesh.DragSelectable)
                {
                    int pixelsVisible = 0;
                    GL.GetQueryObject(occlusionQueries[mesh], GetQueryObjectParam.QueryResult, out pixelsVisible);
                    occlusionResults[mesh] = pixelsVisible > 0;
                }
                else
                {
                    occlusionResults[mesh] = false;
                }
            }
        }

        public void Render()
        {
            if (!initialized) Initialize();
            glControl.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit); // Clear stencil buffer too
            meshShader.Use();
            meshShader.SetMatrix4("view", Camera.GetViewMatrix().ToMatrix4());
            meshShader.SetMatrix4("projection", Camera.GetProjectionMatrix().ToMatrix4());
            meshShader.SetVector3("viewPos", Camera.Position.ToVector3());
            meshShader.SetVector3("lightDir", Camera.Front.ToVector3());
            if (GridEnabled)
            {
                meshShader.SetFloat("ambientStrength", 1.0f);
                meshShader.SetVector3("lightColor", GridColor);
                meshShader.SetVector3("baseColor", GridColor);
                GL.BindVertexArray(gridVAO);
                GL.DrawArrays(PrimitiveType.Lines, 0, gridVertexCount);
                GL.BindVertexArray(0);
            }
            meshShader.SetFloat("ambientStrength", 0.1f);
            meshShader.SetVector3("lightColor", new Vector3(0.8f, 0.8f, 0.8f));
            meshShader.SetVector3("baseColor", new Vector3(0.6f, 0.7f, 0.85f));;

            foreach (var drawable in Children)
                if (drawable is Mesh m && m.Visible)
                    m.Draw();
            DrawSelectionRect();
            glControl.SwapBuffers();
        }

        public void UpdateGridProperties(Vector4 low, Vector4 high, int? rows = null, int? cols = null, float? cellSize = null, Vector3? color = null)
        {
            if (rows.HasValue) GridRows = rows.Value;
            if (cols.HasValue) GridCols = cols.Value;
            if (cellSize.HasValue) CellSize = cellSize.Value;
            if (color.HasValue) GridColor = color.Value;

            BuildGridMesh(low, high);
            Render();
        }

        public void SelectMesh(Mesh mesh)
        {
            ResetAllColors();

            if (SelectedDrawable != null)
            {
                var curSelected = SelectedDrawable as Mesh;

                if (curSelected == null)
                    return;

                curSelected.UseVertexColor = curSelected.ColorStatic != null;

                if (curSelected.ColorStatic != null)
                    curSelected?.SetColor((Color)curSelected.ColorStatic);
            }

            mesh.SetColor(Color.DarkRed);
            mesh.UseVertexColor = true;
            SelectedDrawable = mesh;

            MeshSelected?.Invoke(mesh);
            Render();
        }

        private Vector3d ProjectToScreen(Vector3d world)
        {
            var view = Camera.GetViewMatrix().ToMatrix4();
            var proj = Camera.GetProjectionMatrix().ToMatrix4();

            var v4 = new Vector4((float)world.X, (float)world.Y, (float)world.Z, 1f);
            v4 = Vector4.Transform(v4, view);
            v4 = Vector4.Transform(v4, proj);

            v4 /= v4.W;

            float x = (v4.X * 0.5f + 0.5f) * glControl.Width;
            float y = (1f - (v4.Y * 0.5f + 0.5f)) * glControl.Height;
            return new Vector3d(x, y, v4.Z);
        }

        private void ResetAllColors()
        {
            foreach (var d in Children)
            {
                if (d is Mesh m && m.Visible && m.ColorStatic != null)
                {
                    m.SetColor((Color)m.ColorStatic);
                }
            }
        }

        private void SelectMeshesInRectangle(Point p1, Point p2)
        {
            ResetAllColors();

            var minX = Math.Min(p1.X, p2.X);
            var maxX = Math.Max(p1.X, p2.X);
            var minY = Math.Min(p1.Y, p2.Y);
            var maxY = Math.Max(p1.Y, p2.Y);

            var potentialHits = new List<Mesh>();
            foreach (var d in Children)
            {
                if (d is Mesh m && m.Visible && m.DragSelectable)
                {
                    var world = m.Vertices
                               .Select(v => Vector3d.Transform(v, m.Transform.Value));

                    double sxMin = double.MaxValue, sxMax = double.MinValue;
                    double syMin = double.MaxValue, syMax = double.MinValue;

                    foreach (var w in world)
                    {
                        var win = ProjectToScreen(w);
                        sxMin = Math.Min(sxMin, win.X);
                        sxMax = Math.Max(sxMax, win.X);
                        syMin = Math.Min(syMin, win.Y);
                        syMax = Math.Max(syMax, win.Y);
                    }

                    if (!(sxMax < minX || sxMin > maxX || syMax < minY || syMin > maxY))
                    {
                        potentialHits.Add(m);
                    }
                }
            }

            TestMeshVisibility(potentialHits);

            var hits = new List<Mesh>();
            foreach (var mesh in potentialHits)
            {
                if (occlusionResults.TryGetValue(mesh, out bool isVisible) && isVisible)
                {
                    mesh.SetColor(Color.DarkRed);
                    hits.Add(mesh);
                }
            }

            DragSelectDone.Invoke(hits);
        }

        public static Ray ScreenPointToRay(Point screenPoint)
        {
            try
            {
                int[] viewport = new int[4];
                GL.GetInteger(GetPName.Viewport, viewport);

                var scene = CurrentScene;
                if (scene == null)
                    throw new InvalidOperationException("No current scene");

                var view = scene.Camera.GetViewMatrix();
                var projection = scene.Camera.GetProjectionMatrix();

                Vector3d near = Helpers.Unproject(screenPoint, 0.0, view, projection, viewport);
                Vector3d far = Helpers.Unproject(screenPoint, 1.0, view, projection, viewport);

                return new Ray(near, far);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create ray from screen point {Point}", screenPoint);
                return new Ray(Vector3d.Zero, Vector3d.UnitZ);
            }
        }

        public double GetNearestFloorHeight(Vector3d worldPos, double maxDrop = 10000)
        {
            var rayOrigin = new Vector3d(worldPos.X, worldPos.Y + 0.01, worldPos.Z);
            var down = new Vector3d(0, -1, 0);
            var ray = new Ray(rayOrigin, rayOrigin + down * maxDrop);

            double bestY = worldPos.Y - maxDrop;
            double bestDist = double.MaxValue;
            Mesh? bestMesh = null;

            foreach (var child in Children)
            {
                if (child is not Mesh m || !m.Visible || m.DragSelectable)
                    continue;

                var hit = m.HitTest(ray);
                if (hit == null)
                    continue;

                double dist = (hit.HitPoint - rayOrigin).Length;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestY = hit.HitPoint.Y;
                    bestMesh = m;
                }
            }

            return bestY;
        }

        public double GetNearestFloorHeightGPU(Vector3d worldPos, double maxDrop = 10000, double searchRadius = 100.0)
        {
            if (!initialized) return worldPos.Z;

            var originalViewport = new int[4];
            GL.GetInteger(GetPName.Viewport, originalViewport);

            var topDownPos = new Vector3d(worldPos.X, worldPos.Y + 1.0, worldPos.Z);
            var topDownTarget = new Vector3d(worldPos.X, worldPos.Y - maxDrop, worldPos.Z);
            var topDownUp = new Vector3d(0, 0, 1);

            var topDownView = Matrix4d.LookAt(topDownPos, topDownTarget, topDownUp);
            var topDownProjection = Matrix4d.CreateOrthographic(
                searchRadius * 2, searchRadius * 2, 1.0, maxDrop + 2.0);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, floorHeightFBO);
            GL.Viewport(0, 0, FLOOR_HEIGHT_BUFFER_SIZE, FLOOR_HEIGHT_BUFFER_SIZE);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            meshShader.Use();
            meshShader.SetMatrix4("view", topDownView.ToMatrix4());
            meshShader.SetMatrix4("projection", topDownProjection.ToMatrix4());

            // Render only floor meshes (non-DragSelectable)
            foreach (var child in Children)
            {
                if (child is Mesh m && m.Visible && !m.DragSelectable)
                {
                    m.Draw();
                }
            }

            GL.ReadPixels(0, 0, FLOOR_HEIGHT_BUFFER_SIZE, FLOOR_HEIGHT_BUFFER_SIZE,
                         PixelFormat.DepthComponent, PixelType.Float, floorHeightPixels);

            float closestDepth = 1.0f;
            int centerX = FLOOR_HEIGHT_BUFFER_SIZE / 2;
            int centerY = FLOOR_HEIGHT_BUFFER_SIZE / 2;
            int searchSize = 3;

            for (int y = Math.Max(0, centerY - searchSize); y <= Math.Min(FLOOR_HEIGHT_BUFFER_SIZE - 1, centerY + searchSize); y++)
            {
                for (int x = Math.Max(0, centerX - searchSize); x <= Math.Min(FLOOR_HEIGHT_BUFFER_SIZE - 1, centerX + searchSize); x++)
                {
                    float depth = floorHeightPixels[y * FLOOR_HEIGHT_BUFFER_SIZE + x];
                    if (depth < closestDepth && depth > 0.0f)
                    {
                        closestDepth = depth;
                    }
                }
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(originalViewport[0], originalViewport[1], originalViewport[2], originalViewport[3]);

            if (closestDepth < 1.0f)
            {
                double nearPlane = 1.0;
                double farPlane = maxDrop + 2.0;
                double linearDepth = nearPlane + closestDepth * (farPlane - nearPlane);
                return worldPos.Y + 1.0 - linearDepth;
            }

            return worldPos.Z;
        }

        private void UpdateHoveredMesh(Point mousePos)
        {
            Mesh? newHovered = null;
            double minDepth = double.MaxValue;

            if (!isDragging)
            {
                Ray worldRay = ScreenPointToRay(mousePos);

                foreach (var drawable in Children)
                {
                    if (drawable is Mesh m && m.Visible && m.DragSelectable)
                    {
                        var hitResult = m.HitTest(worldRay);
                        if (hitResult == null) continue;

                        double dist = (hitResult.HitPoint - Camera.Position).Length;

                        if (dist < minDepth)
                        {
                            minDepth = dist;
                            newHovered = m;
                        }
                    }
                }
            }

            if (hoveredMesh != newHovered)
            {
                if (hoveredMesh != null)
                    hoveredMesh.RestoreColor();

                hoveredMesh = newHovered;
                mouseStillTimer = 0;

                if (hoveredMesh == null)
                {
                    tooltip.Hide(glControl);
                }
                else
                {
                    //hoveredMesh.SetColor(Color.Yellow);
                }

                glControl.Invalidate();
            }
        }

        private void ShowTooltipForMesh(Mesh mesh, Point mousePos)
        {
            if (mesh == null) return;

            string tooltipText = mesh.ID;

            var screenPos = glControl.PointToScreen(new Point(mousePos.X + 10, mousePos.Y - 20));
            tooltip.Show(tooltipText, glControl, mousePos.X + 10, mousePos.Y - 20);
        }

        protected void Pick(Point mouseLocation)
        {
            try
            {
                double minDepth = double.MaxValue;
                var curSelected = SelectedDrawable;
                var newSelected = SelectedDrawable;

                Ray worldRay = ScreenPointToRay(mouseLocation);

                foreach (var drawable in this.Children)
                {
                    if (drawable is Mesh m && m.Visible)
                    {
                        var hitResult = m.HitTest(worldRay);
                        if (hitResult == null) continue;

                        double dist = (hitResult.HitPoint - Camera.Position).Length;

                        if (dist < minDepth)
                        {
                            minDepth = dist;
                            newSelected = hitResult.Drawable;
                        }
                    }
                }

                if (newSelected != curSelected && newSelected is Mesh newMesh)
                {
                    SelectMesh(newMesh);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during pick operation at {Location}", mouseLocation);
            }
        }

        private void DestroyGridMesh()
        {
            if (gridVBO != 0)
            {
                GL.DeleteBuffer(gridVBO);
                gridVBO = 0;
            }
            if (gridVAO != 0)
            {
                GL.DeleteVertexArray(gridVAO);
                gridVAO = 0;
            }
        }

        private void BuildGridMesh(Vector4 low, Vector4 high)
        {
            DestroyGridMesh();

            float minX = low.X, maxX = high.X;
            float minZ = low.Z, maxZ = high.Z;
            float width = maxX - minX;
            float depth = maxZ - minZ;

            int rows = (int)Math.Ceiling(depth / CellSize);
            int cols = (int)Math.Ceiling(width / CellSize);

            float y = low.Y;

            var verts = new List<float>((rows + cols + 2) * 6);
            for (int i = 0; i <= rows; i++)
            {
                float z = minZ + i * CellSize;
                verts.AddRange(new[] { minX, y, z, maxX, y, z });
            }
            for (int j = 0; j <= cols; j++)
            {
                float x = minX + j * CellSize;
                verts.AddRange(new[] { x, y, minZ, x, y, maxZ });
            }

            gridVertexCount = verts.Count / 3;

            gridVAO = GL.GenVertexArray();
            gridVBO = GL.GenBuffer();

            GL.BindVertexArray(gridVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, gridVBO);
            GL.BufferData(
                BufferTarget.ArrayBuffer,
                verts.Count * sizeof(float),
                verts.ToArray(),
                BufferUsageHint.StaticDraw
            );

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(
                0,
                3,
                VertexAttribPointerType.Float,
                false,
                3 * sizeof(float),
                0
            );

            GL.BindVertexArray(0);
        }

        private void DrawSelectionRect()
        {
            if (!isDragging) return;

            GL.UseProgram(0);

            GL.MatrixMode(OpenTK.Graphics.OpenGL.MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadIdentity();
            GL.Ortho(0, glControl.Width, glControl.Height, 0, -1, 1);

            GL.MatrixMode(OpenTK.Graphics.OpenGL.MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadIdentity();

            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            var x1 = dragStart.X;
            var y1 = dragStart.Y;
            var x2 = dragEnd.X;
            var y2 = dragEnd.Y;

            // Solid green outline
            GL.LineWidth(1.5f);
            GL.Color4(0.2f, 1f, 0.2f, 0.8f);
            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex2(x1, y1);
            GL.Vertex2(x2, y1);
            GL.Vertex2(x2, y2);
            GL.Vertex2(x1, y2);
            GL.End();

            GL.PopMatrix();
            GL.MatrixMode(OpenTK.Graphics.OpenGL.MatrixMode.Projection);
            GL.PopMatrix();
            GL.MatrixMode(OpenTK.Graphics.OpenGL.MatrixMode.Modelview);
            GL.Enable(EnableCap.DepthTest);

            meshShader.Use();
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            if (hoveredMesh != null && mouseStillTimer > TOOLTIP_DELAY)
            {
                ShowTooltipForMesh(hoveredMesh, lastMousePos);
                mouseStillTimer = int.MaxValue; // Prevent repeated calls
            }
            else if (hoveredMesh != null)
            {
                mouseStillTimer += 1;
            }
        }

        private void glControl_Paint(object? sender, PaintEventArgs e)
        {
            Render();
        }

        private void glControl_Resize(object? sender, EventArgs e)
        {
            GL.Viewport(0, 0, glControl.ClientRectangle.Width, glControl.ClientRectangle.Height);
            Camera.AspectRatio = glControl.Width / (float)glControl.Height;

            ResizeOcclusionResources();
        }

        private void glControl_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            Pick(e.Location);
            Render();
        }

        private void glControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (isDragging && (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && isShiftDragging)))
            {
                isDragging = false;
                isShiftDragging = false;
                SelectMeshesInRectangle(dragStart, dragEnd);
                glControl.Invalidate();
            }

            firstMove = true;
        }

        private void glControl_KeyPress(object? sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            const float cameraSpeed = 1.5f;

            if (e.KeyChar == 'w') Camera.Position += Camera.Front * cameraSpeed * 500;
            else if (e.KeyChar == 'a') Camera.Position -= Camera.Right * cameraSpeed * 500;
            else if (e.KeyChar == 's') Camera.Position -= Camera.Front * cameraSpeed * 500;
            else if (e.KeyChar == 'd') Camera.Position += Camera.Right * cameraSpeed * 500;

            Render();
        }

        private void glControl_MouseScroll(object? sender, MouseEventArgs e)
        {
            const float cameraSpeed = 20f;
            Camera.Position -= Camera.Front * cameraSpeed * (e.Delta * -1);
            Render();
        }

        private void glControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && isShiftDragging))
            {
                isDragging = true;
                dragStart = e.Location;
                dragEnd = e.Location;
                glControl.Invalidate();
            }
        }

        private void glControl_MouseMove(object? sender, MouseEventArgs e)
        {
            lastMousePos = e.Location;
            mouseStillTimer = 0; // Reset hover timer

            if (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && isShiftDragging))
            {
                dragEnd = e.Location;
                glControl.Invalidate();
                return;
            }

            if (Control.MouseButtons.HasFlag(MouseButtons.Left))
            {
                const float sensitivity = 0.2f;
                if (firstMove)
                {
                    lastPosition = new Vector2(e.X, e.Y);
                    firstMove = false;
                }
                else
                {
                    var deltaX = e.X - lastPosition.X;
                    var deltaY = e.Y - lastPosition.Y;
                    lastPosition = new Vector2(e.X, e.Y);

                    Camera.Yaw += deltaX * sensitivity;
                    Camera.Pitch -= deltaY * sensitivity;
                }

                Render();
            }
            else
            {
                firstMove = true;
                UpdateHoveredMesh(e.Location);
            }
        }

        private void glControl_MouseLeave(object? sender, EventArgs e)
        {
            if (hoveredMesh != null)
            {
                hoveredMesh.RestoreColor();
                hoveredMesh = null;
                tooltip.Hide(glControl);
                glControl.Invalidate();
            }
        }

        private void glControl_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Shift)
            {
                isShiftDragging = true;
            }
        }

        private void glControl_KeyUp(object? sender, KeyEventArgs e)
        {
            if (!e.Shift)
            {
                isShiftDragging = false;
            }
        }

        ~Scene()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (initialized)
            {
                DestroyGridMesh();

                if (floorHeightFBO != 0)
                {
                    GL.DeleteFramebuffer(floorHeightFBO);
                    floorHeightFBO = 0;
                }

                if (floorHeightTexture != 0)
                {
                    GL.DeleteTexture(floorHeightTexture);
                    floorHeightTexture = 0;
                }

                //meshShader?.Dispose();

                CleanupOcclusionResources();
            }

            tooltip?.Dispose();
        }
    }
}