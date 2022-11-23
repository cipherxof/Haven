using Haven.Parser;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Haven.Forms
{
    public partial class DciEditor : Form
    {
        public Stage Stage;
        public StageFile StageFile;
        public DciFile DciFile;
        public DciEntry? CurrentEntry;
        public Dictionary<TreeNode, DciEntry> DciEntries = new Dictionary<TreeNode, DciEntry>();
        public Dictionary<DataGridViewRow, DciAlias> DciAliases = new Dictionary<DataGridViewRow, DciAlias>();

        public DciEditor(StageFile stageFile, Stage stage)
        {
            InitializeComponent();

            Stage = stage;
            StageFile = stageFile;
            DciFile = new DciFile(stageFile.GetLocalPath());
            Text = $"DciEditor - {stageFile.Name}";
        }

        private void SetEntry(DciEntry entry)
        {
            dataGridDci.AllowUserToAddRows = false;
            dataGridDci.Rows.Clear();
            DciAliases = new Dictionary<DataGridViewRow, DciAlias>();
            CurrentEntry = null;

            foreach (var alias in DciFile.Aliases[entry])
            {
                int rowIndex = dataGridDci.Rows.Add();
                var row = dataGridDci.Rows[rowIndex];

                if (row == null)
                    continue;

                row.Cells["ColumnTxnIndex"].Value = alias.TxnIndex.ToString();
                row.Cells["ColumnDldEntry"].Value = alias.DldEntry.ToString();

                DciAliases[row] = alias;
            }

            CurrentEntry = entry;
        }

        private void DciEditor_Load(object sender, EventArgs e)
        {
            foreach (var entry in DciFile.Entries)
            {
                if (entry.Offset <= 1)
                    continue;

                var node = tvDciEntries.Nodes.Add(DictionaryFile.GetHashString(entry.Hash));
                DciEntries[node] = entry;
            }

            if (tvDciEntries.Nodes.Count > 0)
            {
                tvDciEntries.SelectedNode = tvDciEntries.Nodes[0];
                SetEntry(DciEntries[tvDciEntries.Nodes[0]]);
            }
        }

        private void tvDciEntries_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (!DciEntries.ContainsKey(e.Node))
                return;

            SetEntry(DciEntries[e.Node]);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dataGridDci.Rows)
            {
                if (!DciAliases.ContainsKey(row))
                    continue;

                var alias = DciAliases[row];

                short.TryParse(row.Cells["ColumnTxnIndex"].Value.ToString(), out alias.TxnIndex);
                short.TryParse(row.Cells["ColumnDldEntry"].Value.ToString(), out alias.DldEntry);


            }

            DciFile.Save(StageFile.GetLocalPath());
        }

        private void dataGridDci_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (CurrentEntry == null)
            {
                e.Cancel = true;
                return;
            }

            int i = 0;
            string rowValue = Convert.ToString(e.FormattedValue);

            if (!int.TryParse(rowValue, out i))
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = false;
        }

        private void dataGridDci_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            if (CurrentEntry == null)
                return;

            var row = dataGridDci.Rows[e.RowIndex];
            var alias = new DciAlias(0, 0);
            row.Cells["ColumnTxnIndex"].Value = 0;
            row.Cells["ColumnDldEntry"].Value = 0;
            DciAliases[row] = alias;
            DciFile.Aliases[CurrentEntry].Add(alias);
            CurrentEntry.AliasCount = (ushort)(dataGridDci.Rows.Count - 1);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            dataGridDci.Rows.Add();
        }
    }
}
