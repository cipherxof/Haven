using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection.Metadata;
using Haven.Parser;

namespace Haven.Render
{
    public class Scene
    {
        public Drawable3D? SelectedDrawable;
        public IList<Drawable3D> Children { get; set; }
        public readonly Camera Camera;

        private GLControl glControl;
        private bool firstMove = true;
        private Vector2 lastPosition;
        private bool initialized = false;

        /// <summary>
        /// Initializes a new scene.
        /// </summary>
        /// <param name="control"></param>
        public Scene(GLControl control)
        {
            glControl = control;

            this.Camera = new Camera(new Vector3d(0,0,0), control.ClientSize.Width / control.ClientSize.Height);
            this.Children = new List<Drawable3D>();

            control.Paint += glControl_Paint;
            control.Resize += glControl_Resize;
            control.MouseWheel += glControl_MouseScroll;
            control.MouseUp += glControl_MouseUp;
            control.MouseMove += glControl_MouseMove;
            control.KeyPress += glControl_KeyPress;
            control.MouseDoubleClick += glControl_MouseDoubleClick;
        }

        /// <summary>
        /// Initialzes OpenGL data.
        /// </summary>
        private void Initialize()
        {
            initialized = true;

            GL.Enable(EnableCap.DepthTest);
            GL.ClearColor(Color.FromArgb(0x6A6A6A));

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            
            GL.Light(LightName.Light1, LightParameter.Ambient, new float[] { 0.1f, 0.1f, 0.1f, 0.0f });
            GL.Light(LightName.Light1, LightParameter.Diffuse, new float[] { 100f / 255.0f, 100f / 255.0f, 100f / 255.0f, 0.0f });
            GL.Light(LightName.Light1, LightParameter.Position, new float[] { 0.8f, 0.4f, 0.5f, 0.0f });
            GL.Enable(EnableCap.Light1);

            GL.Light(LightName.Light2, LightParameter.Ambient, new float[] { 0.2f, 0.2f, 0.2f, 0.0f });
            GL.Light(LightName.Light2, LightParameter.Diffuse, new float[] { 211.0f / 255.0f, 211.0f / 255.0f, 211.0f / 255.0f, 0.0f });
            GL.Light(LightName.Light2, LightParameter.Position, new float[] { 0.8f, -0.2f, 0.5f, 0.0f });
            GL.Enable(EnableCap.Light2);

            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.ColorMaterial);
            GL.FrontFace(FrontFaceDirection.Ccw);
            GL.ShadeModel(ShadingModel.Smooth);

            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.PointSmooth);
            //GL.Enable(EnableCap.PolygonSmooth);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        /// <summary>
        /// Renders the scene.
        /// </summary>
        public void Render()
        {
            if (!initialized)
            {
                Initialize();
            }

            glControl.MakeCurrent();

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.ColorMaterial);

            // Setup viewport and projection matrix
            Matrix4d perpective = Camera.GetProjectionMatrix();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perpective);

            // Setup camera
            Matrix4d modelView = Camera.GetViewMatrix();
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref modelView);

            // Draw meshes
            foreach (var child in Children)
            {
                if (child.Visible)
                {
                    child.Draw();
                }
            }

            glControl.SwapBuffers();
        }
        
        /// <summary>
        /// Sets the currently selected mesh.
        /// </summary>
        /// <param name="mesh"></param>
        public void SelectMesh(Mesh mesh)
        {
            if (SelectedDrawable != null)
            {
                var curSelected = SelectedDrawable as Mesh;
                curSelected?.SetColor(curSelected.ColorStatic);
            }

            mesh.SetColor(Color.DarkRed);
            SelectedDrawable = mesh;
        }

        /// <summary>
        /// Find the mesh that's under the mouse.
        /// </summary>
        /// <param name="mouseLocation"></param>
        protected void Pick(Point mouseLocation)
        {
            // If depth is 1, means do not do hit test (nothing under the cursor).
            //if (Helpers.GetDepth(mouseLocation) == 1)
            //    return;

            double minDepth = int.MaxValue;
            var curSelected = SelectedDrawable;
            var newSelected = SelectedDrawable;

            foreach (var drawable in this.Children)
            {
                Mesh m = drawable as Mesh;

                if (m != null && m.Visible)
                {
                    var res = m.HitTest(mouseLocation);

                    if (res == null)
                        continue;


                    // Select the point closest to the main camera.
                    double dist = Camera.GetDistanceTo(res.HitPoint);
                    if (dist < minDepth)
                    {
                        minDepth = dist;
                        newSelected = res.Drawable;
                    }
                }
            }

            if (newSelected != curSelected)
            {
                SelectMesh(newSelected as Mesh);
            }

            return;
        }

        private void glControl_Paint(object? sender, PaintEventArgs e)
        {
            Render();
        }

        private void glControl_Resize(object? sender, EventArgs e)
        {
            GL.Viewport(glControl.ClientRectangle.X, glControl.ClientRectangle.Y, glControl.ClientRectangle.Width, glControl.ClientRectangle.Height);

            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView((float)Math.PI / 4, glControl.Width / (float)glControl.Height, 1.0f, 64.0f);

            Camera.AspectRatio = glControl.Width / (float)glControl.Height;

            GL.MatrixMode(MatrixMode.Projection);

            GL.LoadMatrix(ref projection);
        }

        private void glControl_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            Pick(e.Location);
            Render();
        }

        private void glControl_MouseUp(object? sender, MouseEventArgs e)
        {
            firstMove = true;
        }

        private void glControl_KeyPress(object? sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            const float cameraSpeed = 1.5f;

            if (e.KeyChar == 'w') Camera.Position += Camera.Front * cameraSpeed * 500; // Forward
            else if (e.KeyChar == 'a') Camera.Position -= Camera.Right * cameraSpeed * 500; // Left
            else if (e.KeyChar == 's') Camera.Position -= Camera.Front * cameraSpeed * 500; // Backwards
            else if (e.KeyChar == 'd') Camera.Position += Camera.Right * cameraSpeed * 500; // Right

            Render();
        }

        private void glControl_MouseScroll(object? sender, MouseEventArgs e)
        {
            const float cameraSpeed = 2f;
            Camera.Position -= Camera.Front * cameraSpeed * (e.Delta * -1); // Backwards
            Render();
        }

        private void glControl_MouseMove(object? sender, MouseEventArgs mouse)
        {
            if (mouse.Button != MouseButtons.Left) return;

            const float sensitivity = 0.2f;

            if (firstMove)
            {
                lastPosition = new Vector2(mouse.X, mouse.Y);
                firstMove = false;
            }
            else
            {
                var deltaX = mouse.X - lastPosition.X;
                var deltaY = mouse.Y - lastPosition.Y;
                lastPosition = new Vector2(mouse.X, mouse.Y);

                Camera.Yaw += deltaX * sensitivity;
                Camera.Pitch -= deltaY * sensitivity;
            }

            Render();
        }

    }
}
