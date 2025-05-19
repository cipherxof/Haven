using Haven.Parser;
using Haven.Parser.Geom;
using Haven.Render;
using System.Globalization;

namespace Haven.Forms
{
    public partial class GeomEditor : Form
    {
        public readonly GeomFile File;
        public readonly GeoBlock Block;
        public readonly Mesh Mesh;
        public readonly Scene Scene;

        public GeomEditor(GeomFile file, GeoBlock block, Mesh mesh, Scene scene)
        {
            InitializeComponent();

            Block = block;
            File = file;
            Mesh = mesh;
            Scene = scene;

            PopulateDataGrid();
        }

        private void PopulateDataGrid()
        {
            var prims = File.BlockFaceData[Block];

            tbBlockAttribute.Text = Block.Attribute.ToString("X8");

            for (int i = 0; i < prims.Count; i++)
            {
                var prim = prims[i];

                int rowIndex = dataGridGeom.Rows.Add();
                var row = dataGridGeom.Rows[rowIndex];

                if (row == null)
                    continue;

                row.Cells["ColumnIndex"].Value = i.ToString();
                row.Cells["ColumnType"].Value = prim.GetPrimType().ToString();
                row.Cells["ColumnHash"].Value = DictionaryFile.GetHashString(prim.Name);
                row.Cells["ColumnHash"].ToolTipText = prim.Name.ToString("X4");
                row.Cells["ColumnAttributes"].Value = prim.Attribute.ToString("X8");
            }

            if (!File.BlockMaterialData.ContainsKey(Block))
                return;

            var mats = File.BlockMaterialData[Block];

            if (mats == null)
                return;

            for (int i = 0; i < mats.Materials.Count; i++)
            {
                if (mats.Materials[i] == 0)
                    break;

                int rowIndex = dataGridMats.Rows.Add();
                var row = dataGridMats.Rows[rowIndex];

                if (row == null)
                    continue;

                row.Cells["ColumnMat"].Value = DictionaryFile.GetHashString(mats.Materials[i]);
            }
        }

        private void dataGridDld_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex < 2)
            {
                e.Cancel = false;
                return;
            }

            ulong i = 0;
            string rowValue = Convert.ToString(e.FormattedValue);

            if (!ulong.TryParse(rowValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out i))
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = false;
        }

        private void btnDldSave_Click(object sender, EventArgs e)
        {
            ulong.TryParse(tbBlockAttribute.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out Block.Attribute);

            var prims = File.BlockFaceData[Block];

            for (int i = 0; i < dataGridGeom.Rows.Count; i++)
            {
                var row = dataGridGeom.Rows[i];

                if (row == null || row.Cells["ColumnIndex"] == null || row.Cells["ColumnIndex"].Value == null)
                    continue;

                int index = int.Parse(row.Cells["ColumnIndex"].Value.ToString());
                var prim = prims[index];

                ulong.TryParse(row.Cells["ColumnAttributes"].Value.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out prim.Attribute);
            }
        }

        private void dataGridDld_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e == null || sender == null)
                return;

            if (e.ColumnIndex != 3)
                return;

            var row = dataGridGeom.Rows[e.RowIndex];

            if (row == null)
                return;

            var value = row.Cells["ColumnIndex"].Value;

            if (value == null)
                return;

            var prims = File.BlockFaceData[Block];

            if (prims == null)
                return;

            int index = int.Parse(value.ToString());
            var prim = prims[index];

            if (prim == null)
                return;

            using (var flagEditor = new GeomFlagEditor(prim))
            {
                flagEditor.ShowDialog();

                row.Cells["ColumnAttributes"].Value = prim.Attribute.ToString("X8");
            }

        }

        private void DisplayPoly(HashSet<int> indices)
        {
            var prims = File.BlockFaceData[Block];
            for (int i = 0; i < prims.Count; i++)
            {
                var face = prims[i];
                if (face.GetPrimType() != Geom.Primitive.GEO_POLY || face.Poly == null)
                    continue;

                byte vis = indices.Contains(i) ? (byte)255 : (byte)0;

                foreach (var poly in face.Poly)
                {
                    var v = poly.Data;
                    int fa = v[0] + 1, fb = v[1] + 1, fc = v[2] + 1, fd = v[3] + 1;
                    Utils.FaceBitCalculation(v[4], ref fa, ref fb, ref fc, ref fd);

                    uint code = ((uint)Mesh.ColorCurrent.A << 24)
                              | ((uint)Mesh.ColorCurrent.B << 16)
                              | ((uint)Mesh.ColorCurrent.G << 8)
                              | ((uint)vis << 0);

                    Mesh.colors[fa - 1] = code;
                    Mesh.colors[fb - 1] = code;
                    Mesh.colors[fc - 1] = code;
                    Mesh.colors[fa - 1] = code;
                    Mesh.colors[fc - 1] = code;
                    Mesh.colors[fd - 1] = code;
                }
            }

            Mesh.UpdateColorBuffer();
            Scene.Render();
        }

        private void dataGridGeom_SelectionChanged(object sender, EventArgs e)
        {
            var rows = dataGridGeom.SelectedRows;

            if (rows.Count == 0)
                return;

            HashSet<int> indicies = new HashSet<int>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                if (row == null || row.Cells["ColumnIndex"] == null || row.Cells["ColumnIndex"].Value == null)
                    continue;

                var index = int.Parse(row.Cells["ColumnIndex"].Value.ToString());
                indicies.Add(index);
            }

            DisplayPoly(indicies);
        }

        private void GeomEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            var rows = dataGridGeom.Rows;
            HashSet<int> indicies = new HashSet<int>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                if (row == null)
                    continue;

                indicies.Add(i);
            }

            DisplayPoly(indicies);
        }

        private void dataGridGeom_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!cbEditRows.Checked)
                return;

            var row = dataGridGeom.Rows[e.RowIndex];

            if (row == null)
                return;

            var value = row.Cells["ColumnAttributes"].Value;

            if (value == null)
                return;

            foreach (DataGridViewRow? selectedRow in dataGridGeom.SelectedRows)
            {
                if (selectedRow == null) 
                    continue;

                selectedRow.Cells["ColumnAttributes"].Value = value;
            }
        }
    }
}
