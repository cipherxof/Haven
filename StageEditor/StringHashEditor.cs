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
    public partial class StringHashEditor : Form
    {
        public StringHashEditor()
        {
            InitializeComponent();
        }

        private void btnStringHashLookup_Click(object sender, EventArgs e)
        {
            if (tbStringHashLookup.Text.Replace(" ", "").Length == 0)
            {
                MessageBox.Show("Please enter a hash.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                uint hash = Convert.ToUInt32(tbStringHashLookup.Text.Replace("0x", ""), 16);
                tbStringHashResult.Text = DictionaryFile.GetHashString(hash);
            }
            catch(Exception exception)
            {
                MessageBox.Show(exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnStringHashCalc_Click(object sender, EventArgs e)
        {
            if (tbStringHash.Text.Replace(" ", "").Length == 0)
            {
                MessageBox.Show("Please enter a string.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            tbStringHashResult.Text = Utils.HashString(tbStringHash.Text).ToString("X4");
        }
    }
}
