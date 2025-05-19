using Haven.Parser;
using Haven.Render;
using OpenTK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Haven
{
    public partial class PropEditor : Form
    {
        private Mesh Mesh;
        private Scene Scene;
        private GeomProp Prop;
        private GeomProp PropOriginal;

        public static readonly Dictionary<GeomProp, GeomProp> GeomPropOriginal = new Dictionary<GeomProp, GeomProp>();

        public PropEditor(Scene scene, Mesh mesh, GeomProp prop)
        {
            Mesh = mesh;
            Scene = scene;
            Prop = prop;

            if (!GeomPropOriginal.TryGetValue(prop, out PropOriginal))
            {
                PropOriginal = new GeomProp();
                PropOriginal.X = prop.X;
                PropOriginal.Y = prop.Y;
                PropOriginal.Z = prop.Z;
                GeomPropOriginal[prop] = PropOriginal;
            }

            InitializeComponent();

        }

        private void SpawnEditor_Load(object sender, EventArgs e)
        {
            this.Text = $"Prop Editor - {Mesh.ID}";

            tbSpawnEditX.Text = Prop.X.ToString();
            tbSpawnEditZ.Text = Prop.Z.ToString();
            tbSpawnEditY.Text = Prop.Y.ToString();

            foreach (var value in Prop.Data)
            {
                tbExtraData.Text += value.ToString("X2") + " ";
            }

        }

        private void btnSpawnEditApply_Click(object sender, EventArgs e)
        {
            var x = double.Parse(tbSpawnEditX.Text);
            var y = double.Parse(tbSpawnEditY.Text);
            var z = double.Parse(tbSpawnEditZ.Text);

            Mesh.Transform.SetTranslation(x - PropOriginal.X, z - PropOriginal.Z, y - PropOriginal.Y);

            if (x == 0 && y == 0 && z == 0) // todo: fix this
                x = 0.001;

            Prop.X = (float)x;
            Prop.Y = (float)y;
            Prop.Z = (float)z;

            Scene.Render();
        }

        private void btnSpawnEditUseCam_Click(object sender, EventArgs e)
        {
            tbSpawnEditX.Text = ((float)Scene.Camera.Position.X).ToString();
            tbSpawnEditZ.Text = ((float)Scene.Camera.Position.Y).ToString();
            tbSpawnEditY.Text = ((float)Scene.Camera.Position.Z).ToString();
        }

        private void btnSnapGround_Click(object sender, EventArgs e)
        {
            var newZ = (float)Scene.CurrentScene.GetNearestFloorHeight(Mesh.Center);

            tbSpawnEditZ.Text = newZ.ToString();
        }
    }
}
