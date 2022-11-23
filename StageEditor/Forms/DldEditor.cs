using Haven.Parser;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Haven
{
    public partial class DldEditor : Form
    {
        public DldFile DldFile;
        public Stage Stage;
        public StageFile StageFile;
        public string Path;
        public Dictionary<DataGridViewRow, DldTexture> RowTexture = new Dictionary<DataGridViewRow, DldTexture>();

        public DldEditor(StageFile stageFile, Stage stage)
        {
            InitializeComponent();

            var dldPath = stageFile.Name.Replace(".dlz", ".dld");
            Path = $"stage\\_dlz\\{dldPath}";

            StageFile = stageFile;
            DldFile = new DldFile(Path);
            Stage = stage;

            Text = $"DldEditor - {StageFile.Name}";
        }

        private void PopulateDataGrid()
        {
            for (int i = 0; i < DldFile.Textures.Count; i++)
            {
                var texture = DldFile.Textures[i];

                int rowIndex = dataGridDld.Rows.Add();
                var row = dataGridDld.Rows[rowIndex];

                if (row == null)
                    continue;

                row.Cells["ColumnIndex"].Value = i.ToString();
                row.Cells["ColumnType"].Value = texture.Type.ToString("X4");
                row.Cells["ColumnHash"].Value = DictionaryFile.GetHashString(texture.HashId);
                row.Cells["ColumnHash"].ToolTipText = texture.HashId.ToString("X4");
                row.Cells["ColumnSize"].Value = texture.DataSize;
                row.Cells["ColumnEntry"].Value = texture.EntryNumber;
                row.Cells["ColumnExport"].Value = "Export";

                RowTexture[row] = texture;
            }
        }

        private void DldEditor_Load(object sender, EventArgs e)
        {
            dataGridDld.AllowUserToAddRows = false;
            PopulateDataGrid();
        }

        private void btnDldAdd_Click(object sender, EventArgs e)
        {
            new DialogDldAdd(DldFile).ShowDialog();
            DldFile.Save(Path);
            DldFile = new DldFile(Path);
            dataGridDld.Rows.Clear();
            PopulateDataGrid();
        }

        private void btnDldDelete_Click(object sender, EventArgs e)
        {
            var row = dataGridDld.SelectedRows[0];

            if (row == null || !RowTexture.ContainsKey(row))
                return;

            DldFile.Textures.Remove(RowTexture[row]);
            DldFile.Save(Path);
            DldFile = new DldFile(Path);
            dataGridDld.Rows.Clear();
            PopulateDataGrid();
        }

        private void btnDldSave_Click(object sender, EventArgs e)
        {
            var textures = new DldTexture[dataGridDld.RowCount];

            foreach (DataGridViewRow row in dataGridDld.Rows)
            {
                if (row.Cells["ColumnIndex"] == null || row.Cells["ColumnIndex"].Value == null || !RowTexture.ContainsKey(row))
                    continue;

                var indexString = row.Cells["ColumnIndex"].Value.ToString();

                if (indexString == null)
                    continue;

                int index = int.Parse(indexString);

                var texture = RowTexture[row];

                uint hash;
                uint entry;
                uint.TryParse(row.Cells["ColumnEntry"].Value.ToString(), out entry);
                uint.TryParse(row.Cells["ColumnHash"].Value.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hash);

                texture.HashId = hash == 0 ? texture.HashId : hash;
                texture.EntryNumber = entry;

                textures[index] = texture;
            }

            var dld = new DldFile();
            dld.Textures = textures.ToList();
            dld.Save(Path);
        }

        private void btnReplace_Click(object sender, EventArgs e)
        {
            var row = dataGridDld.SelectedRows[0];

            if (row == null || !RowTexture.ContainsKey(row))
                return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "DDS files (*.dds)|*.dds|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                var texture = RowTexture[row];
                var ext = System.IO.Path.GetExtension(openFileDialog.FileName);
                var data = File.ReadAllBytes(openFileDialog.FileName);

                if (ext == ".dds")
                {
                    if (texture.Type != 0x2001000)
                    {
                        MessageBox.Show("Importing a DDS with mipmaps is unsupported, please import a binary file instead!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    data = data.Skip(0x80).ToArray();
                }

                texture.DataSize = (uint)data.Length;
                texture.Data = data;
                dataGridDld.Rows.Clear();
                PopulateDataGrid();
            }
        }
    }
}
