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
            this.dataGridGeom = new System.Windows.Forms.DataGridView();
            this.ColumnIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnHash = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnAttributes = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label1 = new System.Windows.Forms.Label();
            this.tbBlockAttribute = new System.Windows.Forms.TextBox();
            this.cbEditRows = new System.Windows.Forms.CheckBox();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridGeom)).BeginInit();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnDldSave});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(684, 25);
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
            // dataGridGeom
            // 
            this.dataGridGeom.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridGeom.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridGeom.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridGeom.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnIndex,
            this.ColumnType,
            this.ColumnHash,
            this.ColumnAttributes});
            this.dataGridGeom.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dataGridGeom.Location = new System.Drawing.Point(12, 78);
            this.dataGridGeom.MinimumSize = new System.Drawing.Size(590, 330);
            this.dataGridGeom.Name = "dataGridGeom";
            this.dataGridGeom.RowTemplate.Height = 25;
            this.dataGridGeom.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridGeom.Size = new System.Drawing.Size(660, 331);
            this.dataGridGeom.TabIndex = 4;
            this.dataGridGeom.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridDld_CellDoubleClick);
            this.dataGridGeom.CellValidating += new System.Windows.Forms.DataGridViewCellValidatingEventHandler(this.dataGridDld_CellValidating);
            this.dataGridGeom.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridGeom_CellValueChanged);
            this.dataGridGeom.SelectionChanged += new System.EventHandler(this.dataGridGeom_SelectionChanged);
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
            this.label1.Location = new System.Drawing.Point(12, 39);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(86, 15);
            this.label1.TabIndex = 6;
            this.label1.Text = "Block Attribute";
            // 
            // tbBlockAttribute
            // 
            this.tbBlockAttribute.Location = new System.Drawing.Point(104, 36);
            this.tbBlockAttribute.Name = "tbBlockAttribute";
            this.tbBlockAttribute.Size = new System.Drawing.Size(128, 23);
            this.tbBlockAttribute.TabIndex = 8;
            // 
            // cbEditRows
            // 
            this.cbEditRows.AutoSize = true;
            this.cbEditRows.Location = new System.Drawing.Point(548, 39);
            this.cbEditRows.Name = "cbEditRows";
            this.cbEditRows.Size = new System.Drawing.Size(124, 19);
            this.cbEditRows.TabIndex = 9;
            this.cbEditRows.Text = "Edit Selected Rows";
            this.cbEditRows.UseVisualStyleBackColor = true;
            // 
            // GeomEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(684, 421);
            this.Controls.Add(this.cbEditRows);
            this.Controls.Add(this.tbBlockAttribute);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dataGridGeom);
            this.Controls.Add(this.toolStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "GeomEditor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "GeomEditor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.GeomEditor_FormClosing);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridGeom)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ToolStrip toolStrip1;
        private ToolStripButton btnDldSave;
        private DataGridView dataGridGeom;
        private DataGridViewTextBoxColumn ColumnIndex;
        private DataGridViewTextBoxColumn ColumnType;
        private DataGridViewTextBoxColumn ColumnHash;
        private DataGridViewTextBoxColumn ColumnAttributes;
        private Label label1;
        private TextBox tbBlockAttribute;
        private CheckBox cbEditRows;
    }
}