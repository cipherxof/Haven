using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Haven.Forms
{
    public partial class DldSelector : Form
    {
        public string FilenameMain = "";
        public string FilenameMips = "";

        public DldSelector(Stage stage)
        {
            InitializeComponent();

            var dlds = stage.Files.FindAll(f => f.Name.Contains(".dlz"));

            foreach (var dld in dlds)
            {
                var index = dldSelectMain.Items.Add(dld.Name);

                if (dld.Name.Contains("_d"))
                {
                    dldSelectMain.SelectedIndex = index;
                }
            }

            foreach (var dld in dlds)
            {
                var index = dldSelectMips.Items.Add(dld.Name);

                if (!dld.Name.Contains("_"))
                {
                    dldSelectMips.SelectedIndex = index;
                }
            }

            if (dldSelectMain.SelectedIndex == -1)
                dldSelectMain.SelectedIndex = 0;

            if (dldSelectMips.SelectedIndex == -1)
                dldSelectMips.SelectedIndex = 0;
        }

        private void dldSelectMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilenameMain = dldSelectMain.Text;
        }

        private void dldSelectMips_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilenameMips = dldSelectMips.Text;
        }

        private void dldBtnSubmit_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
