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
            this.dataGridDld = new System.Windows.Forms.DataGridView();
            this.ColumnExport = new System.Windows.Forms.DataGridViewButtonColumn();
            this.ColumnIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnHash = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnEntry = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnDldSave = new System.Windows.Forms.ToolStripButton();
            this.btnDldAdd = new System.Windows.Forms.ToolStripButton();
            this.btnDldDelete = new System.Windows.Forms.ToolStripButton();
            this.btnReplace = new System.Windows.Forms.ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDld)).BeginInit();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dataGridDld
            // 
            this.dataGridDld.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridDld.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridDld.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridDld.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnExport,
            this.ColumnIndex,
            this.ColumnType,
            this.ColumnHash,
            this.ColumnEntry,
            this.ColumnSize});
            this.dataGridDld.Location = new System.Drawing.Point(12, 37);
            this.dataGridDld.MinimumSize = new System.Drawing.Size(590, 330);
            this.dataGridDld.Name = "dataGridDld";
            this.dataGridDld.RowTemplate.Height = 25;
            this.dataGridDld.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridDld.Size = new System.Drawing.Size(590, 383);
            this.dataGridDld.TabIndex = 0;
            // 
            // ColumnExport
            // 
            this.ColumnExport.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.ColumnExport.FillWeight = 70F;
            this.ColumnExport.HeaderText = "";
            this.ColumnExport.Name = "ColumnExport";
            this.ColumnExport.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnExport.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.ColumnExport.Width = 93;
            // 
            // ColumnIndex
            // 
            this.ColumnIndex.FillWeight = 50F;
            this.ColumnIndex.HeaderText = "Index";
            this.ColumnIndex.Name = "ColumnIndex";
            this.ColumnIndex.ReadOnly = true;
            // 
            // ColumnType
            // 
            this.ColumnType.FillWeight = 70F;
            this.ColumnType.HeaderText = "Type";
            this.ColumnType.Name = "ColumnType";
            this.ColumnType.ReadOnly = true;
            // 
            // ColumnHash
            // 
            this.ColumnHash.HeaderText = "Hash";
            this.ColumnHash.Name = "ColumnHash";
            // 
            // ColumnEntry
            // 
            this.ColumnEntry.FillWeight = 70F;
            this.ColumnEntry.HeaderText = "Entry";
            this.ColumnEntry.Name = "ColumnEntry";
            // 
            // ColumnSize
            // 
            this.ColumnSize.HeaderText = "Filesize";
            this.ColumnSize.Name = "ColumnSize";
            this.ColumnSize.ReadOnly = true;
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnDldSave,
            this.btnDldAdd,
            this.btnDldDelete,
            this.btnReplace});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(614, 25);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnDldSave
            // 
            this.btnDldSave.Image = ((System.Drawing.Image)(resources.GetObject("btnDldSave.Image")));
            this.btnDldSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDldSave.Name = "btnDldSave";
            this.btnDldSave.Size = new System.Drawing.Size(51, 22);
            this.btnDldSave.Text = "Save";
            this.btnDldSave.Click += new System.EventHandler(this.btnDldSave_Click);
            // 
            // btnDldAdd
            // 
            this.btnDldAdd.Image = ((System.Drawing.Image)(resources.GetObject("btnDldAdd.Image")));
            this.btnDldAdd.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDldAdd.Name = "btnDldAdd";
            this.btnDldAdd.Size = new System.Drawing.Size(63, 22);
            this.btnDldAdd.Text = "Import";
            this.btnDldAdd.Click += new System.EventHandler(this.btnDldAdd_Click);
            // 
            // btnDldDelete
            // 
            this.btnDldDelete.Image = ((System.Drawing.Image)(resources.GetObject("btnDldDelete.Image")));
            this.btnDldDelete.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnDldDelete.Name = "btnDldDelete";
            this.btnDldDelete.Size = new System.Drawing.Size(60, 22);
            this.btnDldDelete.Text = "Delete";
            this.btnDldDelete.Click += new System.EventHandler(this.btnDldDelete_Click);
            // 
            // btnReplace
            // 
            this.btnReplace.Image = ((System.Drawing.Image)(resources.GetObject("btnReplace.Image")));
            this.btnReplace.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnReplace.Name = "btnReplace";
            this.btnReplace.Size = new System.Drawing.Size(68, 22);
            this.btnReplace.Text = "Replace";
            this.btnReplace.Click += new System.EventHandler(this.btnReplace_Click);
            // 
            // DldEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(614, 432);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.dataGridDld);
            this.MinimumSize = new System.Drawing.Size(630, 471);
            this.Name = "DldEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "DldEditor";
            this.Load += new System.EventHandler(this.DldEditor_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDld)).EndInit();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DataGridView dataGridDld;
        private DataGridViewButtonColumn ColumnExport;
        private DataGridViewTextBoxColumn ColumnIndex;
        private DataGridViewTextBoxColumn ColumnType;
        private DataGridViewTextBoxColumn ColumnHash;
        private DataGridViewTextBoxColumn ColumnEntry;
        private DataGridViewTextBoxColumn ColumnSize;
        private ToolStrip toolStrip1;
        private ToolStripButton btnDldSave;
        private ToolStripButton btnDldAdd;
        private ToolStripButton btnDldDelete;
        private ToolStripButton btnReplace;
    }
}