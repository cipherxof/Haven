namespace Haven.Forms
{
    partial class DciEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DciEditor));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.btnAdd = new System.Windows.Forms.ToolStripButton();
            this.tvDciEntries = new System.Windows.Forms.TreeView();
            this.dataGridDci = new System.Windows.Forms.DataGridView();
            this.ColumnTxnIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnDldEntry = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDci)).BeginInit();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnSave,
            this.btnAdd});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(584, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnSave
            // 
            this.btnSave.Image = ((System.Drawing.Image)(resources.GetObject("btnSave.Image")));
            this.btnSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(51, 22);
            this.btnSave.Text = "Save";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnAdd
            // 
            this.btnAdd.Image = ((System.Drawing.Image)(resources.GetObject("btnAdd.Image")));
            this.btnAdd.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(49, 22);
            this.btnAdd.Text = "Add";
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // tvDciEntries
            // 
            this.tvDciEntries.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.tvDciEntries.Location = new System.Drawing.Point(12, 37);
            this.tvDciEntries.Name = "tvDciEntries";
            this.tvDciEntries.Size = new System.Drawing.Size(176, 412);
            this.tvDciEntries.TabIndex = 3;
            this.tvDciEntries.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.tvDciEntries_NodeMouseDoubleClick);
            // 
            // dataGridDci
            // 
            this.dataGridDci.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridDci.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridDci.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridDci.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnTxnIndex,
            this.ColumnDldEntry});
            this.dataGridDci.Location = new System.Drawing.Point(194, 37);
            this.dataGridDci.Name = "dataGridDci";
            this.dataGridDci.RowTemplate.Height = 25;
            this.dataGridDci.Size = new System.Drawing.Size(378, 412);
            this.dataGridDci.TabIndex = 4;
            this.dataGridDci.CellValidating += new System.Windows.Forms.DataGridViewCellValidatingEventHandler(this.dataGridDci_CellValidating);
            this.dataGridDci.RowsAdded += new System.Windows.Forms.DataGridViewRowsAddedEventHandler(this.dataGridDci_RowsAdded);
            // 
            // ColumnTxnIndex
            // 
            this.ColumnTxnIndex.HeaderText = "TXN Index";
            this.ColumnTxnIndex.Name = "ColumnTxnIndex";
            // 
            // ColumnDldEntry
            // 
            this.ColumnDldEntry.HeaderText = "DLD Entry";
            this.ColumnDldEntry.Name = "ColumnDldEntry";
            // 
            // DciEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 461);
            this.Controls.Add(this.dataGridDci);
            this.Controls.Add(this.tvDciEntries);
            this.Controls.Add(this.toolStrip1);
            this.MinimumSize = new System.Drawing.Size(600, 500);
            this.Name = "DciEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "DciEditor";
            this.Load += new System.EventHandler(this.DciEditor_Load);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDci)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ToolStrip toolStrip1;
        private ToolStripButton btnSave;
        private TreeView tvDciEntries;
        private DataGridView dataGridDci;
        private DataGridViewTextBoxColumn ColumnTxnIndex;
        private DataGridViewTextBoxColumn ColumnDldEntry;
        private ToolStripButton btnAdd;
    }
}