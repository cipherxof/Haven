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
                uint hash = Convert.ToUInt32(tbStringHashLookup.Text.Replace("0x", "").Replace(" ", ""), 16);
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

            uint hash = Utils.HashString(tbStringHash.Text);
            string result = hash.ToString("X4");

            foreach (var kvp in DictionaryFile.Alias)
            {
                if (string.Equals(kvp.Value, tbStringHash.Text, StringComparison.OrdinalIgnoreCase))
                {
                    result += $" ({kvp.Key:X4})";
                    break;
                }
            }

            tbStringHashResult.Text = result;
        }
    }
}
