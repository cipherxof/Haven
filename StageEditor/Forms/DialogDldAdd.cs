using Haven.Parser;
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
    public partial class DialogDldAdd : Form
    {
        public DldFile DldFile;

        public DialogDldAdd(DldFile file)
        {
            InitializeComponent();

            DldFile = file;
        }

        private void DialogDldAdd_Load(object sender, EventArgs e)
        {
            nupEntry.Maximum = decimal.MaxValue;
            nupHash.Maximum = decimal.MaxValue;
            nupMips.Maximum = decimal.MaxValue;
            nupType.Maximum = decimal.MaxValue;
            nupType.Value = 0x2001000;
        }

        private void btnLoadFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "DDS files (*.dds)|*.dds|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    tbFilePath.Text = openFileDialog.FileName;
                }
            }
        }

        private void btnDone_Click(object sender, EventArgs e)
        {
            var filename = tbFilePath.Text;

            if (!File.Exists(filename))
            {
                return;
            }

            var ext = Path.GetExtension(filename).ToLower();
            var data = File.ReadAllBytes(filename);

            if (ext == ".dds")
            {
                if (nupType.Value != 0x2001000)
                {
                    MessageBox.Show("Importing a DDS with mipmaps is unsupported, please import a binary file instead!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                data = data.Skip(0x80).ToArray();
            }

            DldTexture texture = new DldTexture(2, (DldPriority)nupType.Value, (uint)nupHash.Value, 0, (uint)data.Length, (uint)nupMips.Value, (uint)nupEntry.Value, data);
            DldFile.Textures.Add(texture);
            Close();
        }
    }
}
