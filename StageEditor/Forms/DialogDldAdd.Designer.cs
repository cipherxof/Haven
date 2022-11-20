namespace Haven
{
    partial class DialogDldAdd
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label7 = new System.Windows.Forms.Label();
            this.btnLoadFile = new System.Windows.Forms.Button();
            this.tbFilePath = new System.Windows.Forms.TextBox();
            this.nupEntry = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.nupMips = new System.Windows.Forms.NumericUpDown();
            this.label6 = new System.Windows.Forms.Label();
            this.nupHash = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.nupType = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.btnDone = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nupEntry)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nupMips)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nupHash)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nupType)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label7);
            this.groupBox1.Controls.Add(this.btnLoadFile);
            this.groupBox1.Controls.Add(this.tbFilePath);
            this.groupBox1.Controls.Add(this.nupEntry);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.nupMips);
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.nupHash);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.nupType);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(283, 219);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "File Information";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(6, 151);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(48, 15);
            this.label7.TabIndex = 14;
            this.label7.Text = "Filedata";
            // 
            // btnLoadFile
            // 
            this.btnLoadFile.Location = new System.Drawing.Point(197, 169);
            this.btnLoadFile.Name = "btnLoadFile";
            this.btnLoadFile.Size = new System.Drawing.Size(75, 23);
            this.btnLoadFile.TabIndex = 13;
            this.btnLoadFile.Text = "Load file...";
            this.btnLoadFile.UseVisualStyleBackColor = true;
            this.btnLoadFile.Click += new System.EventHandler(this.btnLoadFile_Click);
            // 
            // tbFilePath
            // 
            this.tbFilePath.Location = new System.Drawing.Point(6, 169);
            this.tbFilePath.Name = "tbFilePath";
            this.tbFilePath.Size = new System.Drawing.Size(185, 23);
            this.tbFilePath.TabIndex = 12;
            // 
            // nupEntry
            // 
            this.nupEntry.Hexadecimal = true;
            this.nupEntry.Location = new System.Drawing.Point(157, 99);
            this.nupEntry.Name = "nupEntry";
            this.nupEntry.Size = new System.Drawing.Size(120, 23);
            this.nupEntry.TabIndex = 11;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(157, 81);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(34, 15);
            this.label5.TabIndex = 10;
            this.label5.Text = "Entry";
            // 
            // nupMips
            // 
            this.nupMips.Location = new System.Drawing.Point(6, 99);
            this.nupMips.Name = "nupMips";
            this.nupMips.Size = new System.Drawing.Size(120, 23);
            this.nupMips.TabIndex = 9;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(6, 81);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(57, 15);
            this.label6.TabIndex = 8;
            this.label6.Text = "Mipmaps";
            // 
            // nupHash
            // 
            this.nupHash.Hexadecimal = true;
            this.nupHash.Location = new System.Drawing.Point(157, 46);
            this.nupHash.Name = "nupHash";
            this.nupHash.Size = new System.Drawing.Size(120, 23);
            this.nupHash.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(157, 28);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(34, 15);
            this.label2.TabIndex = 2;
            this.label2.Text = "Hash";
            // 
            // nupType
            // 
            this.nupType.Hexadecimal = true;
            this.nupType.Location = new System.Drawing.Point(6, 46);
            this.nupType.Maximum = new decimal(new int[] {
            33558528,
            0,
            0,
            0});
            this.nupType.Name = "nupType";
            this.nupType.Size = new System.Drawing.Size(120, 23);
            this.nupType.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 28);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(31, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Type";
            // 
            // btnDone
            // 
            this.btnDone.Location = new System.Drawing.Point(18, 247);
            this.btnDone.Name = "btnDone";
            this.btnDone.Size = new System.Drawing.Size(277, 36);
            this.btnDone.TabIndex = 15;
            this.btnDone.Text = "Done";
            this.btnDone.UseVisualStyleBackColor = true;
            this.btnDone.Click += new System.EventHandler(this.btnDone_Click);
            // 
            // DialogDldAdd
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(307, 295);
            this.Controls.Add(this.btnDone);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "DialogDldAdd";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import File";
            this.Load += new System.EventHandler(this.DialogDldAdd_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nupEntry)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nupMips)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nupHash)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nupType)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private GroupBox groupBox1;
        private Label label7;
        private Button btnLoadFile;
        private TextBox tbFilePath;
        private NumericUpDown nupEntry;
        private Label label5;
        private NumericUpDown nupMips;
        private Label label6;
        private NumericUpDown nupHash;
        private Label label2;
        private NumericUpDown nupType;
        private Label label1;
        private Button btnDone;
    }
}