using Haven.Parser;

namespace Haven.Render
{
    public partial class PropEditorMulti : Form
    {
        private readonly List<GeomProp> _props = new();
        private readonly List<GeomProp> _propsOriginal = new();
        private readonly Dictionary<GeomProp, Mesh> _geomPropMeshLookup = new();

        public static readonly Dictionary<GeomProp, GeomProp> GeomPropOriginal = new Dictionary<GeomProp, GeomProp>();

        public PropEditorMulti(List<GeomProp> props, Dictionary<GeomProp, Mesh> geomPropMeshLookup)
        {
            _props = props;
            _geomPropMeshLookup = geomPropMeshLookup;

            InitializeComponent();
        }

        private void PropEditorMulti_Load(object sender, EventArgs e)
        {
            gbProps.Text = $"Selected Props: {_props.Count}";

            _props.Sort((a, b) => string.Compare(DictionaryFile.GetHashString(a.Hash), DictionaryFile.GetHashString(b.Hash), StringComparison.Ordinal));
            _props.ForEach(p => tbProps.Text += DictionaryFile.GetHashString(p.Hash) + Environment.NewLine);
        }

        private void btnSnapMulti_Click(object sender, EventArgs e)
        {
            foreach (var prop in _props)
            {
                _geomPropMeshLookup.TryGetValue(prop, out Mesh mesh);

                if (mesh == null)
                    continue;

                var newZ = (float)Scene.CurrentScene.GetNearestFloorHeightGPU(mesh.Center);

                if (!GeomPropOriginal.TryGetValue(prop, out var propOriginal))
                {
                    propOriginal = new GeomProp();
                    propOriginal.X = prop.X;
                    propOriginal.Y = prop.Y;
                    propOriginal.Z = prop.Z;
                    GeomPropOriginal[prop] = propOriginal;
                }

                mesh.Transform.SetTranslation(0, newZ - propOriginal.Z, 0);

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

                if (!GeomPropOriginal.TryGetValue(prop, out var propOriginal))
                {
                    propOriginal = new GeomProp();
                    propOriginal.X = prop.X;
                    propOriginal.Y = prop.Y;
                    propOriginal.Z = prop.Z;
                    GeomPropOriginal[prop] = propOriginal;
                }

                var x = prop.X + double.Parse(tbSpawnEditX.Text);
                var y = prop.Y + double.Parse(tbSpawnEditY.Text);
                var z = prop.Z + double.Parse(tbSpawnEditZ.Text);

                mesh.Transform.SetTranslation(x - propOriginal.X, z - propOriginal.Z, y - propOriginal.Y);

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
