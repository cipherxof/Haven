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
    public partial class DialogTxnAdd : Form
    {
        public DialogTxnAdd()
        {
            InitializeComponent();
        }

        private void DialogTxnAdd_Load(object sender, EventArgs e)
        {
            nupOffset.Maximum = decimal.MaxValue;
            nupMipsOffset.Maximum = decimal.MaxValue;
            comboBoxCompression.SelectedIndex = 0;
        }
    }
}
