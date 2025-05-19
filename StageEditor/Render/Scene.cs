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
        private bool isCtrlDragging = false;
        private Point dragStart;
        private Point dragEnd;

        public bool GridEnabled { get; set; } = true;
        public int GridRows { get; set; } = 200;
        public int GridCols { get; set; } = 200;
        public float CellSize { get; set; } = 1024f;
        public Vector3 GridColor { get; set; } = new Vector3(0.8f, 0.8f, 0.8f);


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
            var frag = Encoding.UTF8.GetString(Resources.ShaderFragment);

            meshShader = new Shader(vert, frag);
        }

        public void Render()
        {
            if (!initialized) Initialize();
            glControl.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
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
            meshShader.SetVector3("baseColor", new Vector3(0.6f, 0.7f, 0.85f));

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

        private void SelectMeshesInRectangle(Point p1, Point p2)
        {
            var minX = Math.Min(p1.X, p2.X);
            var maxX = Math.Max(p1.X, p2.X);
            var minY = Math.Min(p1.Y, p2.Y);
            var maxY = Math.Max(p1.Y, p2.Y);

            var hits = new List<Mesh>();
            foreach (var d in Children)
            {
                if (d is Mesh m && m.Visible && m.DragSelectable)
                {
                    var world = m.AABB.Vertices
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
                        hits.Add(m);
                }
            }

            if (SelectedDrawable is Mesh old) old.RestoreColor();

            if (hits.Count > 0)
            {
                var nearest = hits.OrderBy(m =>
                    (m.AABB.Center - Camera.Position).Length
                ).First();
                SelectMesh(nearest);
            }
            else
            {
                SelectedDrawable = null;
                MeshSelected?.Invoke(null);
                Render();
            }
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

            // Semi-transparent green fill
            GL.Color4(0.2f, 1f, 0.2f, 0.2f);
            GL.Begin(PrimitiveType.Quads);
            GL.Vertex2(x1, y1);
            GL.Vertex2(x2, y1);
            GL.Vertex2(x2, y2);
            GL.Vertex2(x1, y2);
            GL.End();

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


        private void glControl_Paint(object? sender, PaintEventArgs e)
        {
            Render();
        }

        private void glControl_Resize(object? sender, EventArgs e)
        {
            GL.Viewport(0, 0, glControl.ClientRectangle.Width, glControl.ClientRectangle.Height);
            Camera.AspectRatio = glControl.Width / (float)glControl.Height;
        }

        private void glControl_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            Pick(e.Location);
            Render();
        }

        private void glControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (isDragging && (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && isCtrlDragging)))
            { 
                isDragging = false;
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
            const float cameraSpeed = 2f;
            Camera.Position -= Camera.Front * cameraSpeed * (e.Delta * -1);
            Render();
        }

        private void glControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && isCtrlDragging))
            {
                isDragging = true;
                dragStart = e.Location;
                dragEnd = e.Location;
                glControl.Invalidate();
            }
        }

        private void glControl_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && isCtrlDragging))
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
            }
        }

        private void glControl_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                isCtrlDragging = true;
            }
        }
        private void glControl_KeyUp(object? sender, KeyEventArgs e)
        {
            if (!e.Control)
            {
                isCtrlDragging = false;
            }
        }
    }
}