namespace Haven.Forms
{
    partial class GeomEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GeomEditor));
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnDldSave = new System.Windows.Forms.ToolStripButton();
            this.dataGridDld = new System.Windows.Forms.DataGridView();
            this.ColumnIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnHash = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnAttributes = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label1 = new System.Windows.Forms.Label();
            this.nupBlockAttribute = new System.Windows.Forms.NumericUpDown();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDld)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nupBlockAttribute)).BeginInit();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnDldSave});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(616, 25);
            this.toolStrip1.TabIndex = 3;
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
            // dataGridDld
            // 
            this.dataGridDld.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridDld.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridDld.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridDld.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnIndex,
            this.ColumnType,
            this.ColumnHash,
            this.ColumnAttributes});
            this.dataGridDld.Location = new System.Drawing.Point(12, 78);
            this.dataGridDld.MinimumSize = new System.Drawing.Size(590, 330);
            this.dataGridDld.Name = "dataGridDld";
            this.dataGridDld.RowTemplate.Height = 25;
            this.dataGridDld.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridDld.Size = new System.Drawing.Size(592, 334);
            this.dataGridDld.TabIndex = 4;
            this.dataGridDld.CellValidating += new System.Windows.Forms.DataGridViewCellValidatingEventHandler(this.dataGridDld_CellValidating);
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
            this.ColumnHash.ReadOnly = true;
            // 
            // ColumnAttributes
            // 
            this.ColumnAttributes.FillWeight = 70F;
            this.ColumnAttributes.HeaderText = "Attributes";
            this.ColumnAttributes.Name = "ColumnAttributes";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 31);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(86, 15);
            this.label1.TabIndex = 6;
            this.label1.Text = "Block Attribute";
            // 
            // nupBlockAttribute
            // 
            this.nupBlockAttribute.Hexadecimal = true;
            this.nupBlockAttribute.Location = new System.Drawing.Point(12, 49);
            this.nupBlockAttribute.Name = "nupBlockAttribute";
            this.nupBlockAttribute.Size = new System.Drawing.Size(120, 23);
            this.nupBlockAttribute.TabIndex = 7;
            // 
            // GeomEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(616, 424);
            this.Controls.Add(this.nupBlockAttribute);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dataGridDld);
            this.Controls.Add(this.toolStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "GeomEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "GeomEditor";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridDld)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nupBlockAttribute)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ToolStrip toolStrip1;
        private ToolStripButton btnDldSave;
        private DataGridView dataGridDld;
        private DataGridViewTextBoxColumn ColumnIndex;
        private DataGridViewTextBoxColumn ColumnType;
        private DataGridViewTextBoxColumn ColumnHash;
        private DataGridViewTextBoxColumn ColumnAttributes;
        private Label label1;
        private NumericUpDown nupBlockAttribute;
    }
}