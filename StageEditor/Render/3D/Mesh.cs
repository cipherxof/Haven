﻿using Haven.Render._3D;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Serilog;

namespace Haven.Render
{
    /// <summary>
    /// Represents a mesh built using vertices and triangle indices that can be
    /// rendered in OpenGL, manipulated in C# and be the subject of CSG operations
    /// in Carve
    /// </summary>
    public class Mesh : Drawable3D
    {
        private static int idGen = -1;
        private static Dictionary<string, Mesh> IDLookup = new Dictionary<string, Mesh>();
        public static List<Mesh> MeshList = new List<Mesh>();

        private bool Initialized = false;
        private Vector3d[] vertices;
        private Vector3d[] normals;
        public uint[] colors;
        public bool UseVertexColor = false;
        public Color? ColorStatic = null;
        public Color ColorCurrent;
        public bool HasColor { get; private set; }
        private int[] triangleIndices;

        public int VAO;
        private int VBO_vertices;
        private int VBO_normals;
        private int VBO_colors;
        private int EBO;
        private int numElements;

        private string id;

        public Vector3d[] Vertices { get { return this.vertices; } }
        public uint[] Colors { get { return this.colors; } }
        public int[] TriangleIndices { get { return this.triangleIndices; } }
        public Vector3d[] Normals => normals;

        /// <summary>
        /// An arbitrary ID string associated with this mesh
        /// </summary>
        public string ID 
        {
            get
            {
                return id;
            }
            set
            {
                id = FindNextID(value);
                IDLookup[id] = this;
            }
        }

        private string FindNextID(string key)
        {
            if (!IDLookup.ContainsKey(key))
            {
                return key;
            }

            for (int i = 1; i < 0xFF; i++)
            {
                var newId = $"{key} ({i})";

                if (!IDLookup.ContainsKey(newId))
                {
                    return newId;
                }
            }

            return key;
        }
        /// <summary>
        /// Gets the collection of all the triangles of this Mesh.
        /// </summary>
        public TriangleCollection Triangles { get; protected set; }

        /// <summary>
        /// Calculates the array of vertices of this mesh after applying
        /// the transforms applied to this mesh.
        /// </summary>
        /// <returns>The transformed vertices of this mesh</returns>
        public Vector3d[] GetTransformedVertices()
        {
            Vector3d[] result = new Vector3d[this.vertices.Length];
            Matrix4d transform = Transform.Value;

            Parallel.For(0, this.vertices.Length, i =>
            {
                result[i] = Vector3d.Transform(this.vertices[i], transform);
            });

            return result;
        }

        /// <summary>
        /// Initializes a new mesh.
        /// </summary>
        /// <param name="vertices">The vertex coordinates of this mesh</param>
        /// <param name="triangleIndices">The face triangle indices of this mesh</param>
        /// <param name="colors">Vertex colors of this mesh</param>
        public Mesh(Vector3d[] vertices, int[] triangleIndices, uint[]? colors = null)
        {
            MeshList.Add(this);

            this.vertices = vertices;
            this.triangleIndices = triangleIndices;
            this.Triangles = new TriangleCollection(vertices, triangleIndices);
            this.normals = new Vector3d[vertices.Length];
            this.numElements = triangleIndices.Length;

            for (int i = 0; i < vertices.Length; i++)
                normals[i] = Vector3d.Zero;

            for (int i = 0; i < triangleIndices.Length; i += 3)
            {
                var a = triangleIndices[i];
                var b = triangleIndices[i + 1];
                var c = triangleIndices[i + 2];

                if (a < vertices.Length && b < vertices.Length && c < vertices.Length)
                {
                    var e1 = vertices[b] - vertices[a];
                    var e2 = vertices[c] - vertices[a];
                    var normal = Vector3d.Cross(e1, e2).Normalized();

                    if (normal.LengthSquared > 1e-10)
                    {
                        normal.Normalize();
                        normals[a] += normal;
                        normals[b] += normal;
                        normals[c] += normal;
                    }
                }
            }

            for (int i = 0; i < normals.Length; i++)
            {
                if (normals[i].LengthSquared > 1e-10)
                    normals[i] = normals[i].Normalized();
                else
                    normals[i] = new Vector3d(0, 1, 0);
            }

            if (colors != null)
            {
                this.colors = colors;
                this.HasColor = true;
            }
            else
            {
                this.HasColor = false;
                this.colors = new uint[vertices.Length];
                Color color = Color.Gray;
                uint colorCode = (uint)color.A << 24 | (uint)color.B << 16 | (uint)color.G << 8 | (uint)color.R;

                for (int i = 0; i < this.colors.Length; i++)
                    this.colors[i] = colorCode;

                ColorStatic = color;
            }

            CalculateCenter();
            this.Attachments = new Drawable3DCollection(this);
            ComputeAABB();

            Mesh.idGen++;
            this.ID = "Mesh-" + idGen.ToString();
        }

        /// <summary>
        /// Computes the axis-aligned bounding box of this mesh.
        /// </summary>
        protected void ComputeAABB()
        {
            this.AABB = new AABB(this, Helpers.GetMinVector3d(this.vertices), Helpers.GetMaxVector3d(this.vertices));
        }

        /// <summary>
        /// Calculates the center point of this mesh
        /// </summary>
        protected override void CalculateCenter()
        {
            this.center = new Vector3d(0);

            foreach (var vertex in this.vertices)
                center += vertex;

            center /= this.vertices.Count();
        }

        /// <summary>
        /// Registers the handles VBO of this mesh with OpenGL and initializes the data.
        /// </summary>
        private void Init()
        {
            if (Initialized)
                return;

            GL.GenVertexArrays(1, out VAO);
            GL.BindVertexArray(VAO);

            float[] vertexData = new float[vertices.Length * 3];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertexData[i * 3] = (float)vertices[i].X;
                vertexData[i * 3 + 1] = (float)vertices[i].Y;
                vertexData[i * 3 + 2] = (float)vertices[i].Z;
            }

            float[] normalData = new float[normals.Length * 3];
            for (int i = 0; i < normals.Length; i++)
            {
                normalData[i * 3] = (float)normals[i].X;
                normalData[i * 3 + 1] = (float)normals[i].Y;
                normalData[i * 3 + 2] = (float)normals[i].Z;
            }

            // Vertices
            GL.GenBuffers(1, out VBO_vertices);
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO_vertices);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexData.Length * sizeof(float), vertexData, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Normals
            GL.GenBuffers(1, out VBO_normals);
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO_normals);
            GL.BufferData(BufferTarget.ArrayBuffer, normalData.Length * sizeof(float), normalData, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);

            // Colors
            GL.GenBuffers(1, out VBO_colors);
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO_colors);
            GL.BufferData(BufferTarget.ArrayBuffer, colors.Length * sizeof(uint), colors, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, sizeof(uint), 0);
            GL.EnableVertexAttribArray(2);

            // Element buffer
            GL.GenBuffers(1, out EBO);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, triangleIndices.Length * sizeof(int), triangleIndices, BufferUsageHint.StaticDraw);


            GL.BindVertexArray(0);
            Initialized = true;
        }

        /// <summary>
        /// Clears the ID lookup table.
        /// </summary>
        public static void ResetID()
        {
            idGen = 0;
            IDLookup.Clear();
        }

        public void UpdateColorBuffer()
        {
            if (!Initialized)
                return;

            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO_colors);
            GL.BufferData(BufferTarget.ArrayBuffer, colors.Length * sizeof(uint), colors, BufferUsageHint.StaticDraw);
        }

        /// <summary>
        /// Sets the mesh color.
        /// </summary>
        /// <param name="color"></param>
        /// <param name="updateBuffer"></param>

        public void SetColor(Color color)
        {
            uint colorCode = (uint)color.A << 24 | (uint)color.B << 16 | (uint)color.G << 8 | (uint)color.R;

            for (int i = 0; i < colors.Length; i++)
                colors[i] = colorCode;

            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO_colors);
            GL.BufferData(BufferTarget.ArrayBuffer, colors.Length * sizeof(uint), colors, BufferUsageHint.StaticDraw);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newColors"></param>
        public void SetColorArray(uint[] newColors)
        {
            for (int i = 0; i < this.colors.Length && i < newColors.Length; i++)
                this.colors[i] = newColors[i];
        }


        /// <summary>
        /// Draws the mesh using OpenGL. The method must be called in a drawing context (after setting
        /// the view properties and performing clearing)
        /// </summary>
        public override void Draw()
        {
            if (!Initialized)
                Init();

            // Get the current shader from Scene and set matrices
            var scene = Scene.CurrentScene;
            if (scene?.meshShader != null)
            {
                Matrix4 model = Transform.Value.ToMatrix4();
                scene.meshShader.SetMatrix4("model", model);
                scene.meshShader.SetInt("useVertexColor", UseVertexColor ? 1 : 0);

                // Calculate normal matrix (transpose of inverse of model matrix)
                Matrix3 normalMatrix = Matrix3.Transpose(Matrix3.Invert(new Matrix3(model)));
                scene.meshShader.SetMatrix3("normalMatrix", normalMatrix);
            }

            GL.BindVertexArray(VAO);
            GL.DrawElements(PrimitiveType.Triangles, numElements, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            foreach (var attachment in this.Attachments)
                attachment.Draw();

            //if (ShowAABB || Gizmos.ShowAABB)
            //    this.AABB.Draw();
        }

        public void Delete()
        {
            this.Visible = false;
            this.vertices = new Vector3d[0];
            this.triangleIndices = new int[0];
            this.normals = new Vector3d[0];
            this.colors = new uint[0];

            if (this.Triangles != null)
            {
                this.Triangles.Clear();
            }
        }

        /// <summary>
        /// Performs a ray casting hit test using the specified ray. This function does not
        /// take the ray into its own space! Thus make sure the ray is specified in the space of this
        /// object (with proper transforms applied to it).
        /// </summary>
        /// <param name="ray">The ray to perform hit test for</param>
        /// <returns>The result of the hit test if anything occurred. null otherwise.</returns>
        public override HitTestResult HitTest(Ray ray)
        {
            // First determine if the ray hits the AABB
            //if (this.AABB.HitTest(ray) == null)
            //    return null;

            System.Collections.Concurrent.ConcurrentBag<MeshHitTestResult> hits = new System.Collections.Concurrent.ConcurrentBag<MeshHitTestResult>();

            Parallel.ForEach(Triangles, item =>
            {
                Vector3d? intersection = item.IntersectionWith(ray);

                if (intersection != null)
                {
                    hits.Add(new MeshHitTestResult(this, Transform.Transform(intersection.Value), item));
                }
            });

            double minDist = int.MaxValue;
            Vector3d bestHit = Vector3d.Zero;
            Triangle triHit = null;
            Vector3d cameraPoistion = Camera.MainCamera.Position;

            foreach (var item in hits)
            {
                if (item == null)
                    continue;

                double dist = (cameraPoistion - item.HitPoint).LengthSquared;
                if (dist < minDist)
                {
                    minDist = dist;
                    bestHit = Vector3d.Transform(item.HitPoint, Transform.Value.Inverted());
                    triHit = item.TriangleHit;
                }
            }

            if (triHit == null)
                return null;

            return new MeshHitTestResult(this, bestHit, triHit);
        }

        /// <summary>
        /// Saves this mesh into a PLY format
        /// </summary>
        /// <param name="path">The path to the file to save this mesh to</param>
        public void SaveMesh(string path)
        {
            var vertices = GetTransformedVertices();
            var faces = this.triangleIndices;

            StreamWriter file = new StreamWriter(path);

            file.WriteLine("ply");
            file.WriteLine("format ascii 1.0");
            file.WriteLine("element vertex " + vertices.Length);
            file.WriteLine("property float x");
            file.WriteLine("property float y");
            file.WriteLine("property float z");
            file.WriteLine("element face " + faces.Length / 3);
            file.WriteLine("property list uchar int vertex_indices");
            file.WriteLine("end_header");

            foreach (var v in vertices)
                file.WriteLine(v.X + " " + v.Y + " " + v.Z);

            for (int i = 0; i < faces.Length; i += 3)
                file.WriteLine("3 " + faces.ElementAt(i) + " " + faces.ElementAt(i + 1) + " " + faces.ElementAt(i + 2));

            file.Close();
        }

        /// <summary>
        /// Find a mesh from its ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Mesh? FromID(string id)
        {
            if (!IDLookup.ContainsKey(id))
                return null;

            return IDLookup[id];
        }

        /// <summary>
        /// Combine multiple meshes into a single mesh
        /// </summary>
        /// <param name="meshes"></param>
        /// <returns></returns>
        public static Mesh CombineMeshes(List<Mesh> meshes)
        {
            List<Vector3d> verts = new List<Vector3d>();
            List<int> faces = new List<int>();
            int baseIndex = 0;

            foreach (var mesh in meshes)
            {
                foreach (var vert in mesh.vertices)
                    verts.Add(vert);

                foreach (var face in mesh.triangleIndices)
                    faces.Add(baseIndex + face);

                baseIndex += mesh.vertices.Length;
            }

            return new Mesh(verts.ToArray(), faces.ToArray());
        }

        /// <summary>
        /// Creates a mesh by parsing a PLY buffer.
        /// </summary>
        /// <param name="path">The contents of the PLY mesh file</param>
        /// <returns>A mesh corresponding to the information in the PLY file</returns>
        public static Mesh LoadFromPLYBuffer(String buffer, Vector3d offset)
        {
            //MeshObject result = new MeshObject();
            int numVertices = 0;
            int numFaces = 0;
            bool containsColor = false;
            Dictionary<string, int> colorMapping = new Dictionary<string, int>();
            int colorIndex = 0;

            // open scene.ply and convert its contents to an array of strings
            StringReader sr = new StringReader(buffer);

            // determine the number of vertices and faces and determine whether or not geometry will be colored
            string line = sr.ReadLine();

            while (!(line = sr.ReadLine()).Contains("end_header"))
            {
                string[] parsedLine = line.Split(null);

                if (line.Contains("element vertex"))
                {
                    numVertices = int.Parse(line.Split(' ')[2]);
                }

                if (line.Contains("element face"))
                {
                    numFaces = int.Parse(line.Split(' ')[2]);
                }

                if (line.StartsWith("property"))
                {
                    if (line.Contains("list"))
                        continue;

                    if (!containsColor && line.Contains("red") || line.Contains("green") || line.Contains("blue") || line.Contains("alpha"))
                        containsColor = true;

                    if (line.Contains("red"))
                        colorMapping["red"] = colorIndex++;
                    else if (line.Contains("green"))
                        colorMapping["green"] = colorIndex++;
                    else if (line.Contains("blue"))
                        colorMapping["blue"] = colorIndex++;
                    else if (line.Contains("alpha"))
                        colorMapping["alpha"] = colorIndex++;
                }
            }

            Vector3d[] vertices = new Vector3d[numVertices];
            uint[] colors = new uint[numVertices];
            int[] faces = new int[numFaces * 3];

            string[] lines = new string[numVertices];

            for (int i = 0; i < numVertices; i++)
                lines[i] = sr.ReadLine();

            Parallel.For(0, numVertices, i => {
                string thisLine = lines[i];

                string[] parsedLine = thisLine.Split(null);

                double x = double.Parse(parsedLine[0]) + offset.X;
                double y = double.Parse(parsedLine[1]) + offset.Y;
                double z = double.Parse(parsedLine[2]) + offset.Z;

                Color color = Color.Blue;

                if (containsColor)
                {
                    int a = colorMapping.ContainsKey("alpha") ? int.Parse(parsedLine[3 + colorMapping["alpha"]]) : 255;
                    int r = colorMapping.ContainsKey("red") ? int.Parse(parsedLine[3 + colorMapping["red"]]) : 128;
                    int g = colorMapping.ContainsKey("green") ? int.Parse(parsedLine[3 + colorMapping["green"]]) : 128;
                    int b = colorMapping.ContainsKey("blue") ? int.Parse(parsedLine[3 + colorMapping["blue"]]) : 128;

                    color = Color.FromArgb(a, r, g, b);
                }

                vertices[i] = new Vector3d(x, y, z);
                colors[i] = (uint)color.A << 24 | (uint)color.B << 16 | (uint)color.G << 8 | (uint)color.R;
            });

            lines = new string[numFaces];

            for (int i = 0; i < numFaces; i++)
                lines[i] = sr.ReadLine();

            Parallel.For(0, numFaces, i =>
            {
                string thisLine = lines[i];

                string[] parsedLine = thisLine.Split(null);

                int verticesPerFace = int.Parse(parsedLine[0]);

                // triangle
                if (verticesPerFace == 3)
                {
                    faces[3 * i] = int.Parse(parsedLine[1]);
                    faces[3 * i + 1] = int.Parse(parsedLine[2]);
                    faces[3 * i + 2] = int.Parse(parsedLine[3]);
                }
            });

            return new Mesh(vertices, faces, containsColor ? colors : null);
        }


        /// <summary>
        /// Creates a mesh by parsing a PLY file
        /// </summary>
        /// <param name="path">The path to the PLY mesh file</param>
        /// <returns>A mesh corresponding to the information in the PLY file</returns>
        public static Mesh LoadFromPLYFile(String path)
        {
            return LoadFromPLYBuffer(File.ReadAllText(path), new Vector3d(0,0,0));
        }
        
        public override string ToString()
        {
            return this.ID;
        }
    }

}