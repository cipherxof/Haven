using Haven.Parser;
using Haven.Parser.Geom;
using Haven.Render;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Haven
{
    public partial class TxnEditor : Form
    {
        public TxnFile Txn;
        public Stage Stage;
        public string Path;
        public List<DldFile> TextureData = new List<DldFile>(); // todo: don't re-parse on each load
        public DldFile? CurrentDld;
        public DldFile? CurrentDld2;
        //public Dictionary<string, DciFile> DciFiles = new Dictionary<string, DciFile>();
        public List<DciFile> DciFilesList = new List<DciFile>();

        public TxnEditor(StageFile stageFile, Stage stage)
        {
            InitializeComponent();

            Path = stageFile.GetLocalPath();
            Txn = new TxnFile(Path);
            Stage = stage;

            Text = $"TxnEditor - {stageFile.Name}";
        }

        private int GetIndexDldEntry(int txnIndex)
        {
            var index = Txn.ImageInfo[txnIndex];

            foreach (var dci in DciFilesList)
            {
                foreach (var entry in dci.Entries)
                {
                    if (entry.Offset <= 1)
                        continue;

                    if (entry.Hash == index.TriId)
                    {
                        foreach (var alias in dci.Aliases[entry])
                        {
                            if (alias.TxnIndex == txnIndex)
                            {
                                return alias.DldEntry;
                            }
                        }
                    }
                }
            }

            return txnIndex;
        }

        private void AddRowTxnIndex(int txnIndex)
        {
            int rowIndex = dataGridTxn.Rows.Add();
            var row = dataGridTxn.Rows[rowIndex];
            var index1 = Txn.Images[txnIndex];
            var index2 = Txn.ImageInfo[txnIndex];

            row.Cells["ColumnIndex"].Value = txnIndex;
            row.Cells["ColumnMaterial"].Value = DictionaryFile.GetHashString(index2.TexId);
            row.Cells["ColumnMaterial"].ToolTipText = index2.TexId.ToString("X4");
            row.Cells["ColumnObject"].Value = DictionaryFile.GetHashString(index2.TriId);
            row.Cells["ColumnObject"].ToolTipText = index2.TriId.ToString("X4");
            row.Cells["ColumnWidth"].Value = index2.Width;
            row.Cells["ColumnHeight"].Value = index2.Height;
            row.Cells["ColumnFlags"].Value = index1.Flag;
            row.Cells["ColumnOffset"].Value = index1.Offset.ToString("X4");
            row.Cells["ColumnMipmaps"].Value = index1.OffsetMips.ToString("X4");
            row.Cells["ColumnExport"].Value = "Export";
        }

        private void TxnEditor_Load(object sender, EventArgs e)
        {
            dataGridTxn.AllowUserToAddRows = false;

            foreach (var file in Stage.Files)
            {
                if (file.Type == StageFile.FileType.DLZ)
                {
                    var dldPath = $"{file.GetUnpackedDir()}\\{file.Name.Replace(".dlz", ".dld").Replace(".dec", "")}";

                    TextureData.Add(new DldFile(dldPath));
                    comboBoxDlz.Items.Add(file.Name); comboBoxDlz2.Items.Add(file.Name);
                    comboBoxDlz.SelectedIndex = 0;
                }
                else if (file.Type == StageFile.FileType.DCI)
                {
                    DciFilesList.Add(new DciFile(file.GetLocalPath()));
                }
            }


            if (TextureData.Count > 0)
                CurrentDld = TextureData[0];

            for (int i = 0; i < Txn.Images.Count; i++)
            {
                AddRowTxnIndex(i);
            }
        }

        private void btnTxnSave_Click(object sender, EventArgs e)
        {
            /*foreach (DataGridViewRow row in dataGridTxn.Rows)
            {
                int txnIndex;

                if (row.Cells["ColumnIndex"].Value == null || !int.TryParse(row.Cells["ColumnIndex"].Value.ToString(), out txnIndex))
                    continue;

                if (txnIndex >= Txn.Indicies.Count)
                    continue;

                if (!uint.TryParse(row.Cells["ColumnMaterial"].Value.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out Txn.Indicies2[txnIndex].MaterialId))
                {
                    var matString = row.Cells["ColumnMaterial"].Value.ToString();
                    if (matString != null)
                    {
                        Txn.Indicies2[txnIndex].MaterialId = Utils.HashString(matString);
                    }
                }

                if (!uint.TryParse(row.Cells["ColumnObject"].Value.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out Txn.Indicies2[txnIndex].ObjectId))
                {
                    var matString = row.Cells["ColumnObject"].Value.ToString();
                    if (matString != null)
                    {
                        Txn.Indicies2[txnIndex].ObjectId = Utils.HashString(matString);
                    }
                }

                ushort.TryParse(row.Cells["ColumnWidth"].Value.ToString(), out Txn.Indicies[txnIndex].Width);
                ushort.TryParse(row.Cells["ColumnHeight"].Value.ToString(), out Txn.Indicies[txnIndex].Height);
                ushort.TryParse(row.Cells["ColumnWidth"].Value.ToString(), out Txn.Indicies2[txnIndex].Width);
                ushort.TryParse(row.Cells["ColumnHeight"].Value.ToString(), out Txn.Indicies2[txnIndex].Height);
                ushort.TryParse(row.Cells["ColumnFlags"].Value.ToString(), out Txn.Indicies[txnIndex].Flag);
                uint.TryParse(row.Cells["ColumnOffset"].ToolTipText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out Txn.Indicies[txnIndex].Offset);
                uint.TryParse(row.Cells["ColumnMipmaps"].ToolTipText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out Txn.Indicies[txnIndex].MipMapOffset);
            }*/

            Txn.Save(Path);
        }

        private void dataGridTxn_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            var senderGrid = (DataGridView)sender;

            if (senderGrid.Columns[e.ColumnIndex] is DataGridViewButtonColumn && e.RowIndex >= 0)
            {
                if (CurrentDld == null)
                {
                    MessageBox.Show($"Please select the DLZ to search for the texture in.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int txnIndex = (int)senderGrid.Rows[e.RowIndex].Cells["ColumnIndex"].Value;
                var txnIndexUpdated = GetIndexDldEntry(txnIndex);

                var index2List = Txn.GetIndex2List(txnIndex);

                if (index2List.Count == 0)
                {
                    MessageBox.Show($"....", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else if (index2List.Count > 1) 
                {
                    MessageBox.Show($"Multiple entires were found for this texture", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                var objectId = index2List[0].TriId;
                var texture = CurrentDld.FindTexture(objectId, txnIndexUpdated, DldPriority.Main);

                if (texture == null)
                {
                    texture = CurrentDld.FindTexture(objectId, txnIndexUpdated, DldPriority.Mipmaps);
                    if (texture == null)
                    {
                        MessageBox.Show($"Unable to locate this texture in {comboBoxDlz.Text}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                using (SaveFileDialog saveFileDialog1 = new SaveFileDialog())
                {
                    string filename = DictionaryFile.GetHashString(Txn.ImageInfo[txnIndex].TexId);
                    saveFileDialog1.FileName = $"{filename}.dds";
                    saveFileDialog1.Filter = "DDS files (*.dds)|*.dds";
                    saveFileDialog1.RestoreDirectory = true;

                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        Txn.CreateDdsFromIndex(saveFileDialog1.FileName, txnIndex, texture, CurrentDld2?.FindTexture(objectId, txnIndexUpdated, DldPriority.Mipmaps));
                    }
                }
            }
        }

        private void comboBoxDlz_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxDlz.SelectedIndex == -1)
            {
                CurrentDld = null;
                return;
            }
            else
            {
                CurrentDld = TextureData[comboBoxDlz.SelectedIndex];
            }

            if (comboBoxDlz2.SelectedIndex == -1)
            {
                CurrentDld2 = null;
                return;
            }
            else
            {
                CurrentDld2 = TextureData[comboBoxDlz2.SelectedIndex];
            }
        }

        private void btnTxnSearch_Click(object sender, EventArgs e)
        {
            if (dataGridTxn.SelectedRows.Count == 0)
                return;

            var row = dataGridTxn.SelectedRows[0];

            if (row == null)
                return;

            int txnIndex = (int)row.Cells["ColumnIndex"].Value;

            comboBoxDlz.SelectedIndex = -1;
            comboBoxDlz2.SelectedIndex = -1;

            for (int i = 0; i < TextureData.Count; i++)
            {
                var dld = TextureData[i];

                var objectId = Txn.ImageInfo[txnIndex].TriId;
                var updatedIndex = GetIndexDldEntry(txnIndex);

                var texture = dld.FindTexture(objectId, updatedIndex, DldPriority.Main);
                var textureMips = dld.FindTexture(objectId, updatedIndex, DldPriority.Mipmaps);

                if (texture != null)
                {
                    CurrentDld = dld;
                    comboBoxDlz.SelectedIndex = i;
                }

                if (textureMips != null)
                {
                    CurrentDld2 = dld;
                    comboBoxDlz2.SelectedIndex = i;
                }
            }

            if (CurrentDld == null && CurrentDld2 != null)
            {
                comboBoxDlz.SelectedIndex = comboBoxDlz2.SelectedIndex;
                comboBoxDlz2.SelectedIndex = -1;
            }

        }

        private void btnTxnDump_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    var dir = fbd.SelectedPath;

                    for (int txnIndex = 0; txnIndex < Txn.Images.Count; txnIndex++)
                    {
                        DldTexture? texture = null;
                        DldTexture? textureMips = null;
                        var objectId = Txn.ImageInfo[txnIndex].TriId;
                        var txnIndexUpdated = GetIndexDldEntry(txnIndex);

                        for (int i = TextureData.Count - 1; i >= 0; i--)
                        {
                            var dld = TextureData[i];

                            if (texture == null)
                                texture = dld.FindTexture(objectId, txnIndexUpdated, DldPriority.Main);

                            if (textureMips == null)
                                textureMips = dld.FindTexture(objectId, txnIndexUpdated, DldPriority.Mipmaps);
                        }

                        string filename = DictionaryFile.GetHashString(Txn.ImageInfo[txnIndex].TexId);
                        string fullDir = $"{dir}\\{System.IO.Path.GetFileNameWithoutExtension(Txn.Path)}";

                        if (!Directory.Exists(fullDir))
                            Directory.CreateDirectory(fullDir);

                        var txnInfo = Txn.GetIndex2List(txnIndex);
                        var hasMips = txnInfo[0].Flag != 0xF0;

                        if (texture == null && textureMips != null)
                        {
                            Txn.CreateDdsFromIndex($"{fullDir}\\{filename}.dds", txnIndex, textureMips, null);
                            continue;
                        }

                        Txn.CreateDdsFromIndex($"{fullDir}\\{filename}.dds", txnIndex, texture, textureMips);
                    }
                }
            }
        }

        private void btnTxnAdd_Click(object sender, EventArgs e)
        {
            if (dataGridTxn.SelectedRows.Count == 0)
                return;

            var row = dataGridTxn.SelectedRows[0];
            var txnIndex = (int)row.Cells["ColumnIndex"].Value;

            var index = Txn.Images[txnIndex];
            var index2 = Txn.ImageInfo[txnIndex];

            var newIndex = new TxnImage(index.Width, index.Height, index.FourCC, index.Flag, index.Offset, index.OffsetMips);
            var newIndex2 = new TxnInfo(index2.TexId, index2.TriId, index2.Width, index2.Height, index2.OffsetX, index2.OffsetY, index2.TxnImageOffset, index2.ScaleU, index2.ScaleV, index2.OffsetU, index2.OffsetV);

            Txn.Images.Add(newIndex);
            Txn.ImageInfo.Add(newIndex2);

            AddRowTxnIndex(Txn.Images.Count-1);
        }

        private void btnTxnDelete_Click(object sender, EventArgs e)
        {
            if (dataGridTxn.SelectedRows.Count == 0)
                return;

            var row = dataGridTxn.SelectedRows[0];

            if (row == null)
                return;

            var txnIndex = (int)row.Cells["ColumnIndex"].Value;
            var index1 = Txn.Images[txnIndex];
            var index2 = Txn.ImageInfo[txnIndex];

            var safeobjs = new List<uint> { 0x9A9116, 0x9A53B7, 0x4AAF74, 0x4ADF14, 0x6D60FF };

            if (safeobjs.Contains(index2.TexId)) 
                return;

            bool[] deleted = new bool[] { false, false };

            for (int i = 0; i < TextureData.Count; i++)
            {
                var dld = TextureData[i];
                var updatedIndex = GetIndexDldEntry(txnIndex);
                var objectId = Txn.ImageInfo[txnIndex].TriId;

                if (!deleted[0])
                {
                    var texture = dld.FindTexture(objectId, updatedIndex, DldPriority.Main);

                    if (texture != null)
                    {
                        if (dld.RemoveTexture(texture))
                        {
                            deleted[0] = true;
                            dld.Save(dld.Filename);
                        }
                    }
                }

                if (!deleted[1])
                {
                    var textureMips = dld.FindTexture(objectId, updatedIndex, DldPriority.Mipmaps);

                    if (textureMips != null)
                    {
                        if (dld.RemoveTexture(textureMips))
                        {
                            deleted[1] = true;
                            dld.Save(dld.Filename);
                        }
                    }
                }
            }

            /*if (!deleted[0] && !deleted[1])
            {
                MessageBox.Show("FAILED DELETE");
                return;
            }

            if (!deleted[0] || !deleted[1])
            {
                MessageBox.Show("FAILED DELETE 2");
            }*/

            for (int i = txnIndex + 1; i < Txn.Images.Count; i++)
            {
                Txn.Images[i].Offset -= Txn.Images[txnIndex].OffsetMips - Txn.Images[txnIndex].Offset;
                Txn.Images[i].OffsetMips -= Txn.Images[txnIndex].OffsetMips - Txn.Images[txnIndex].Offset;
            }

            Txn.Images.Remove(index1);
            Txn.ImageInfo.Remove(index2);

            dataGridTxn.Rows.Clear();

            for (int i = 0; i < Txn.Images.Count; i++)
            {
                AddRowTxnIndex(i);
            }

            dataGridTxn.Sort(dataGridTxn.Columns[5], ListSortDirection.Descending);
        }
    }
}
