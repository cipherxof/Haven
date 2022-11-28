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

            nupBlockAttribute.Maximum = decimal.MaxValue;
            PopulateDataGrid();
        }

        private void PopulateDataGrid()
        {
            var prims = File.BlockFaceData[Block];

            nupBlockAttribute.Value = Block.Attribute;

            for (int i = 0; i < prims.Count; i++)
            {
                var prim = prims[i];

                int rowIndex = dataGridDld.Rows.Add();
                var row = dataGridDld.Rows[rowIndex];

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
            Block.Attribute = (ulong)nupBlockAttribute.Value;

            var prims = File.BlockFaceData[Block];

            for (int i = 0; i < dataGridDld.Rows.Count; i++)
            {
                var row = dataGridDld.Rows[i];

                if (row == null || row.Cells["ColumnIndex"] == null || row.Cells["ColumnIndex"].Value == null)
                    continue;

                int index = int.Parse(row.Cells["ColumnIndex"].Value.ToString());
                var prim = prims[index];

                ulong.TryParse(row.Cells["ColumnAttributes"].Value.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out prim.Attribute);
            }
        }
    }
}
