namespace Haven
{
    partial class DldEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DldEditor));
            dataGridDld = new DataGridView();
            toolStrip1 = new ToolStrip();
            btnDldSave = new ToolStripButton();
            btnDldAdd = new ToolStripButton();
            btnDldDelete = new ToolStripButton();
            btnReplace = new ToolStripButton();
            ColumnExport = new DataGridViewButtonColumn();
            ColumnIndex = new DataGridViewTextBoxColumn();
            ColumnType = new DataGridViewTextBoxColumn();
            ColumnHash = new DataGridViewTextBoxColumn();
            ColumnEntry = new DataGridViewTextBoxColumn();
            ColumnSize = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)dataGridDld).BeginInit();
            toolStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // dataGridDld
            // 
            dataGridDld.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridDld.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridDld.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridDld.Columns.AddRange(new DataGridViewColumn[] { ColumnExport, ColumnIndex, ColumnType, ColumnHash, ColumnEntry, ColumnSize });
            dataGridDld.Location = new Point(12, 37);
            dataGridDld.MinimumSize = new Size(590, 330);
            dataGridDld.Name = "dataGridDld";
            dataGridDld.RowTemplate.Height = 25;
            dataGridDld.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridDld.Size = new Size(590, 383);
            dataGridDld.TabIndex = 0;
            dataGridDld.CellContentClick += dataGridDld_CellContentClick;
            // 
            // toolStrip1
            // 
            toolStrip1.Items.AddRange(new ToolStripItem[] { btnDldSave, btnDldAdd, btnDldDelete, btnReplace });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(614, 25);
            toolStrip1.TabIndex = 2;
            toolStrip1.Text = "toolStrip1";
            // 
            // btnDldSave
            // 
            btnDldSave.Image = (Image)resources.GetObject("btnDldSave.Image");
            btnDldSave.ImageTransparentColor = Color.Magenta;
            btnDldSave.Name = "btnDldSave";
            btnDldSave.Size = new Size(51, 22);
            btnDldSave.Text = "Save";
            btnDldSave.Click += btnDldSave_Click;
            // 
            // btnDldAdd
            // 
            btnDldAdd.Image = (Image)resources.GetObject("btnDldAdd.Image");
            btnDldAdd.ImageTransparentColor = Color.Magenta;
            btnDldAdd.Name = "btnDldAdd";
            btnDldAdd.Size = new Size(63, 22);
            btnDldAdd.Text = "Import";
            btnDldAdd.Click += btnDldAdd_Click;
            // 
            // btnDldDelete
            // 
            btnDldDelete.Image = (Image)resources.GetObject("btnDldDelete.Image");
            btnDldDelete.ImageTransparentColor = Color.Magenta;
            btnDldDelete.Name = "btnDldDelete";
            btnDldDelete.Size = new Size(60, 22);
            btnDldDelete.Text = "Delete";
            btnDldDelete.Click += btnDldDelete_Click;
            // 
            // btnReplace
            // 
            btnReplace.Image = (Image)resources.GetObject("btnReplace.Image");
            btnReplace.ImageTransparentColor = Color.Magenta;
            btnReplace.Name = "btnReplace";
            btnReplace.Size = new Size(68, 22);
            btnReplace.Text = "Replace";
            btnReplace.Click += btnReplace_Click;
            // 
            // ColumnExport
            // 
            ColumnExport.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            ColumnExport.FillWeight = 70F;
            ColumnExport.HeaderText = "";
            ColumnExport.Name = "ColumnExport";
            ColumnExport.Resizable = DataGridViewTriState.True;
            ColumnExport.SortMode = DataGridViewColumnSortMode.Automatic;
            ColumnExport.Width = 93;
            // 
            // ColumnIndex
            // 
            ColumnIndex.FillWeight = 50F;
            ColumnIndex.HeaderText = "Index";
            ColumnIndex.Name = "ColumnIndex";
            ColumnIndex.ReadOnly = true;
            // 
            // ColumnType
            // 
            ColumnType.FillWeight = 70F;
            ColumnType.HeaderText = "Priority";
            ColumnType.Name = "ColumnType";
            ColumnType.ReadOnly = true;
            // 
            // ColumnHash
            // 
            ColumnHash.HeaderText = "Hash";
            ColumnHash.Name = "ColumnHash";
            // 
            // ColumnEntry
            // 
            ColumnEntry.FillWeight = 70F;
            ColumnEntry.HeaderText = "Entry";
            ColumnEntry.Name = "ColumnEntry";
            // 
            // ColumnSize
            // 
            ColumnSize.HeaderText = "Filesize";
            ColumnSize.Name = "ColumnSize";
            ColumnSize.ReadOnly = true;
            // 
            // DldEditor
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(614, 432);
            Controls.Add(toolStrip1);
            Controls.Add(dataGridDld);
            MinimumSize = new Size(630, 471);
            Name = "DldEditor";
            StartPosition = FormStartPosition.CenterParent;
            Text = "DldEditor";
            FormClosed += DldEditor_FormClosed;
            Load += DldEditor_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridDld).EndInit();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView dataGridDld;
        private ToolStrip toolStrip1;
        private ToolStripButton btnDldSave;
        private ToolStripButton btnDldAdd;
        private ToolStripButton btnDldDelete;
        private ToolStripButton btnReplace;
        private DataGridViewButtonColumn ColumnExport;
        private DataGridViewTextBoxColumn ColumnIndex;
        private DataGridViewTextBoxColumn ColumnType;
        private DataGridViewTextBoxColumn ColumnHash;
        private DataGridViewTextBoxColumn ColumnEntry;
        private DataGridViewTextBoxColumn ColumnSize;
    }
}