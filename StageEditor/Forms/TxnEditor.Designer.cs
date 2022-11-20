namespace Haven
{
    partial class TxnEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TxnEditor));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnTxnDump = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.comboBoxDlz2 = new System.Windows.Forms.ComboBox();
            this.btnTxnSearch = new System.Windows.Forms.Button();
            this.btnTxnAdd = new System.Windows.Forms.Button();
            this.btnTxnSave = new System.Windows.Forms.Button();
            this.comboBoxDlz = new System.Windows.Forms.ComboBox();
            this.dataGridTxn = new System.Windows.Forms.DataGridView();
            this.ColumnExport = new System.Windows.Forms.DataGridViewButtonColumn();
            this.ColumnIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnMaterial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnObject = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnWidth = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnHeight = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnOffset = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnMipmaps = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnFlags = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridTxn)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.btnTxnDump);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.comboBoxDlz2);
            this.groupBox1.Controls.Add(this.btnTxnSearch);
            this.groupBox1.Controls.Add(this.btnTxnAdd);
            this.groupBox1.Controls.Add(this.btnTxnSave);
            this.groupBox1.Controls.Add(this.comboBoxDlz);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(848, 49);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Tools";
            // 
            // btnTxnDump
            // 
            this.btnTxnDump.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnTxnDump.Image = ((System.Drawing.Image)(resources.GetObject("btnTxnDump.Image")));
            this.btnTxnDump.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnTxnDump.Location = new System.Drawing.Point(592, 21);
            this.btnTxnDump.Name = "btnTxnDump";
            this.btnTxnDump.Size = new System.Drawing.Size(80, 23);
            this.btnTxnDump.TabIndex = 7;
            this.btnTxnDump.Text = "Dump";
            this.btnTxnDump.UseVisualStyleBackColor = true;
            this.btnTxnDump.Click += new System.EventHandler(this.btnTxnDump_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(202, 23);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(34, 15);
            this.label2.TabIndex = 6;
            this.label2.Text = "DLZ2";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 23);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(34, 15);
            this.label1.TabIndex = 5;
            this.label1.Text = "DLZ1";
            // 
            // comboBoxDlz2
            // 
            this.comboBoxDlz2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDlz2.FormattingEnabled = true;
            this.comboBoxDlz2.Location = new System.Drawing.Point(242, 21);
            this.comboBoxDlz2.Name = "comboBoxDlz2";
            this.comboBoxDlz2.Size = new System.Drawing.Size(150, 23);
            this.comboBoxDlz2.TabIndex = 4;
            // 
            // btnTxnSearch
            // 
            this.btnTxnSearch.Image = ((System.Drawing.Image)(resources.GetObject("btnTxnSearch.Image")));
            this.btnTxnSearch.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnTxnSearch.Location = new System.Drawing.Point(405, 21);
            this.btnTxnSearch.Name = "btnTxnSearch";
            this.btnTxnSearch.Size = new System.Drawing.Size(115, 23);
            this.btnTxnSearch.TabIndex = 3;
            this.btnTxnSearch.Text = "Find Selected";
            this.btnTxnSearch.UseVisualStyleBackColor = true;
            this.btnTxnSearch.Click += new System.EventHandler(this.btnTxnSearch_Click);
            // 
            // btnTxnAdd
            // 
            this.btnTxnAdd.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnTxnAdd.Enabled = false;
            this.btnTxnAdd.Image = ((System.Drawing.Image)(resources.GetObject("btnTxnAdd.Image")));
            this.btnTxnAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnTxnAdd.Location = new System.Drawing.Point(678, 21);
            this.btnTxnAdd.Name = "btnTxnAdd";
            this.btnTxnAdd.Size = new System.Drawing.Size(80, 23);
            this.btnTxnAdd.TabIndex = 2;
            this.btnTxnAdd.Text = "Add";
            this.btnTxnAdd.UseVisualStyleBackColor = true;
            this.btnTxnAdd.Click += new System.EventHandler(this.btnTxnAdd_Click);
            // 
            // btnTxnSave
            // 
            this.btnTxnSave.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnTxnSave.Image = ((System.Drawing.Image)(resources.GetObject("btnTxnSave.Image")));
            this.btnTxnSave.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnTxnSave.Location = new System.Drawing.Point(762, 21);
            this.btnTxnSave.Name = "btnTxnSave";
            this.btnTxnSave.Size = new System.Drawing.Size(80, 23);
            this.btnTxnSave.TabIndex = 1;
            this.btnTxnSave.Text = "Save";
            this.btnTxnSave.UseVisualStyleBackColor = true;
            this.btnTxnSave.Click += new System.EventHandler(this.btnTxnSave_Click);
            // 
            // comboBoxDlz
            // 
            this.comboBoxDlz.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDlz.FormattingEnabled = true;
            this.comboBoxDlz.Location = new System.Drawing.Point(46, 20);
            this.comboBoxDlz.Name = "comboBoxDlz";
            this.comboBoxDlz.Size = new System.Drawing.Size(150, 23);
            this.comboBoxDlz.TabIndex = 0;
            this.comboBoxDlz.SelectedIndexChanged += new System.EventHandler(this.comboBoxDlz_SelectedIndexChanged);
            // 
            // dataGridTxn
            // 
            this.dataGridTxn.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridTxn.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridTxn.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridTxn.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnExport,
            this.ColumnIndex,
            this.ColumnMaterial,
            this.ColumnObject,
            this.ColumnWidth,
            this.ColumnHeight,
            this.ColumnOffset,
            this.ColumnMipmaps,
            this.ColumnFlags});
            this.dataGridTxn.Location = new System.Drawing.Point(12, 67);
            this.dataGridTxn.Name = "dataGridTxn";
            this.dataGridTxn.RowTemplate.Height = 25;
            this.dataGridTxn.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridTxn.Size = new System.Drawing.Size(848, 439);
            this.dataGridTxn.TabIndex = 1;
            this.dataGridTxn.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridTxn_CellContentClick);
            // 
            // ColumnExport
            // 
            this.ColumnExport.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.ColumnExport.FillWeight = 75F;
            this.ColumnExport.HeaderText = "";
            this.ColumnExport.Name = "ColumnExport";
            this.ColumnExport.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.ColumnExport.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.ColumnExport.Text = "Export";
            this.ColumnExport.Width = 72;
            // 
            // ColumnIndex
            // 
            this.ColumnIndex.FillWeight = 50F;
            this.ColumnIndex.HeaderText = "Index";
            this.ColumnIndex.Name = "ColumnIndex";
            this.ColumnIndex.ReadOnly = true;
            // 
            // ColumnMaterial
            // 
            this.ColumnMaterial.FillWeight = 140.9036F;
            this.ColumnMaterial.HeaderText = "Material";
            this.ColumnMaterial.Name = "ColumnMaterial";
            this.ColumnMaterial.ReadOnly = true;
            // 
            // ColumnObject
            // 
            this.ColumnObject.FillWeight = 93.27415F;
            this.ColumnObject.HeaderText = "Object";
            this.ColumnObject.Name = "ColumnObject";
            this.ColumnObject.ReadOnly = true;
            // 
            // ColumnWidth
            // 
            this.ColumnWidth.FillWeight = 93.27415F;
            this.ColumnWidth.HeaderText = "Width";
            this.ColumnWidth.Name = "ColumnWidth";
            // 
            // ColumnHeight
            // 
            this.ColumnHeight.FillWeight = 93.27415F;
            this.ColumnHeight.HeaderText = "Height";
            this.ColumnHeight.Name = "ColumnHeight";
            // 
            // ColumnOffset
            // 
            this.ColumnOffset.HeaderText = "Offset";
            this.ColumnOffset.Name = "ColumnOffset";
            // 
            // ColumnMipmaps
            // 
            this.ColumnMipmaps.HeaderText = "Mips Offset";
            this.ColumnMipmaps.Name = "ColumnMipmaps";
            // 
            // ColumnFlags
            // 
            this.ColumnFlags.FillWeight = 93.27415F;
            this.ColumnFlags.HeaderText = "Flags";
            this.ColumnFlags.Name = "ColumnFlags";
            // 
            // TxnEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(872, 525);
            this.Controls.Add(this.dataGridTxn);
            this.Controls.Add(this.groupBox1);
            this.MinimumSize = new System.Drawing.Size(888, 564);
            this.Name = "TxnEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "TxnEditor";
            this.Load += new System.EventHandler(this.TxnEditor_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridTxn)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private GroupBox groupBox1;
        private ComboBox comboBoxDlz;
        private DataGridView dataGridTxn;
        private Button btnTxnAdd;
        private Button btnTxnSave;
        private Label label2;
        private Label label1;
        private ComboBox comboBoxDlz2;
        private Button btnTxnSearch;
        private Button btnTxnDump;
        private DataGridViewButtonColumn ColumnExport;
        private DataGridViewTextBoxColumn ColumnIndex;
        private DataGridViewTextBoxColumn ColumnMaterial;
        private DataGridViewTextBoxColumn ColumnObject;
        private DataGridViewTextBoxColumn ColumnWidth;
        private DataGridViewTextBoxColumn ColumnHeight;
        private DataGridViewTextBoxColumn ColumnOffset;
        private DataGridViewTextBoxColumn ColumnMipmaps;
        private DataGridViewTextBoxColumn ColumnFlags;
    }
}