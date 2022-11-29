using Haven.Parser;
using Haven.Parser.Geom;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Haven.Forms
{
    public partial class GeomEditor : Form
    {
        public readonly GeomFile File;
        public readonly GeoBlock Block;

        public GeomEditor(GeomFile file, GeoBlock block)
        {
            InitializeComponent();

            Block = block;
            File = file;

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
    }
}
