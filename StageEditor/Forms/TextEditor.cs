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
    public partial class TextEditor : Form
    {
        private string Filename;
        private bool ReadOnly;

        public TextEditor(StageFile stageFile, bool readOnly)
        {
            InitializeComponent();

            Filename = stageFile.GetLocalPath();
            ReadOnly = readOnly;

            Text = $"TextEditor - {stageFile.Name}";
        }

        private void TextEditor_Load(object sender, EventArgs e)
        {
            if (!File.Exists(Filename))
            {
                MessageBox.Show("?");
                return;
            }

            textEditorText.Text = File.ReadAllText(Filename).Replace("\n", Environment.NewLine);
            textEditorText.Select(0, 0);
            btnSave.Enabled = !ReadOnly;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            File.WriteAllText(Filename, textEditorText.Text);
        }
    }
}
