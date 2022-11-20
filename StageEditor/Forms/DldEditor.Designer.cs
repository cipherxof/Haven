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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnDldDelete = new System.Windows.Forms.Button();
            this.btnDldSave = new System.Windows.Forms.Button();
            this.btnDldAdd = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDld)).BeginInit();
            this.groupBox1.SuspendLayout();
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
            this.dataGridDld.Location = new System.Drawing.Point(12, 67);
            this.dataGridDld.MinimumSize = new System.Drawing.Size(590, 330);
            this.dataGridDld.Name = "dataGridDld";
            this.dataGridDld.RowTemplate.Height = 25;
            this.dataGridDld.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridDld.Size = new System.Drawing.Size(590, 353);
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
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.btnDldDelete);
            this.groupBox1.Controls.Add(this.btnDldSave);
            this.groupBox1.Controls.Add(this.btnDldAdd);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(590, 49);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Tools";
            // 
            // btnDldDelete
            // 
            this.btnDldDelete.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnDldDelete.Image = ((System.Drawing.Image)(resources.GetObject("btnDldDelete.Image")));
            this.btnDldDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnDldDelete.Location = new System.Drawing.Point(287, 20);
            this.btnDldDelete.Name = "btnDldDelete";
            this.btnDldDelete.Size = new System.Drawing.Size(125, 23);
            this.btnDldDelete.TabIndex = 5;
            this.btnDldDelete.Text = "Delete Selected";
            this.btnDldDelete.UseVisualStyleBackColor = true;
            this.btnDldDelete.Click += new System.EventHandler(this.btnDldDelete_Click);
            // 
            // btnDldSave
            // 
            this.btnDldSave.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnDldSave.Image = ((System.Drawing.Image)(resources.GetObject("btnDldSave.Image")));
            this.btnDldSave.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnDldSave.Location = new System.Drawing.Point(504, 20);
            this.btnDldSave.Name = "btnDldSave";
            this.btnDldSave.Size = new System.Drawing.Size(80, 23);
            this.btnDldSave.TabIndex = 4;
            this.btnDldSave.Text = "Save";
            this.btnDldSave.UseVisualStyleBackColor = true;
            this.btnDldSave.Click += new System.EventHandler(this.btnTxnSave_Click);
            // 
            // btnDldAdd
            // 
            this.btnDldAdd.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnDldAdd.Image = ((System.Drawing.Image)(resources.GetObject("btnDldAdd.Image")));
            this.btnDldAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnDldAdd.Location = new System.Drawing.Point(418, 20);
            this.btnDldAdd.Name = "btnDldAdd";
            this.btnDldAdd.Size = new System.Drawing.Size(80, 23);
            this.btnDldAdd.TabIndex = 3;
            this.btnDldAdd.Text = "Add";
            this.btnDldAdd.UseVisualStyleBackColor = true;
            this.btnDldAdd.Click += new System.EventHandler(this.btnDldAdd_Click);
            // 
            // DldEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(614, 432);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.dataGridDld);
            this.MinimumSize = new System.Drawing.Size(630, 471);
            this.Name = "DldEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "DldEditor";
            this.Load += new System.EventHandler(this.DldEditor_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDld)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private DataGridView dataGridDld;
        private GroupBox groupBox1;
        private Button btnDldAdd;
        private Button btnDldSave;
        private DataGridViewButtonColumn ColumnExport;
        private DataGridViewTextBoxColumn ColumnIndex;
        private DataGridViewTextBoxColumn ColumnType;
        private DataGridViewTextBoxColumn ColumnHash;
        private DataGridViewTextBoxColumn ColumnEntry;
        private DataGridViewTextBoxColumn ColumnSize;
        private Button btnDldDelete;
    }
}