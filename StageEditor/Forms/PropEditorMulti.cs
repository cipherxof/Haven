using Haven.Parser;

namespace Haven.Render
{
    public partial class PropEditorMulti : Form
    {
        private readonly List<GeomProp> _props = new();
        private readonly Dictionary<GeomProp, Mesh> _geomPropMeshLookup = new();

        public PropEditorMulti(List<GeomProp> props, Dictionary<GeomProp, Mesh> geomPropMeshLookup)
        {
            _props = props;
            _geomPropMeshLookup = geomPropMeshLookup;

            InitializeComponent();
        }

        private void PropEditorMulti_Load(object sender, EventArgs e)
        {
            lbPropSelectCount.Text = $"Selected Props: {_props.Count}";
        }

        private void btnSnapMulti_Click(object sender, EventArgs e)
        {
            foreach (var prop in _props)
            {
                _geomPropMeshLookup.TryGetValue(prop, out Mesh mesh);

                if (mesh == null)
                    continue;

                var newZ = (float)Scene.CurrentScene.GetNearestFloorHeight(mesh.Center);

                mesh.Transform.SetTranslation(0, newZ - prop.Z, 0);

                prop.Z = newZ;
            }

            Scene.CurrentScene.Render();
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            foreach (var prop in _props)
            {
                _geomPropMeshLookup.TryGetValue(prop, out Mesh mesh);

                if (mesh == null)
                    continue;

                var x = prop.X + double.Parse(tbSpawnEditX.Text);
                var y = prop.Y + double.Parse(tbSpawnEditY.Text);
                var z = prop.Z + double.Parse(tbSpawnEditZ.Text);

                mesh.Transform.SetTranslation(x - prop.X, z - prop.Z, y - prop.Y);

                if (x == 0 && y == 0 && z == 0)
                    x = 0.001;

                prop.X = (float)x;
                prop.Y = (float)y;
                prop.Z = (float)z;
            }

            Scene.CurrentScene.Render();

            this.Close();
        }
    }
}
