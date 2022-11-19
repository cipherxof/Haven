namespace Haven
{
    partial class StringHashEditor
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
            this.btnStringHashLookup = new System.Windows.Forms.Button();
            this.tbStringHashLookup = new System.Windows.Forms.TextBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btnStringHashCalc = new System.Windows.Forms.Button();
            this.tbStringHash = new System.Windows.Forms.TextBox();
            this.tbStringHashResult = new System.Windows.Forms.TextBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btnStringHashLookup);
            this.groupBox1.Controls.Add(this.tbStringHashLookup);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(200, 85);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Lookup";
            // 
            // btnStringHashLookup
            // 
            this.btnStringHashLookup.Location = new System.Drawing.Point(6, 51);
            this.btnStringHashLookup.Name = "btnStringHashLookup";
            this.btnStringHashLookup.Size = new System.Drawing.Size(188, 23);
            this.btnStringHashLookup.TabIndex = 1;
            this.btnStringHashLookup.Text = "Search for hash";
            this.btnStringHashLookup.UseVisualStyleBackColor = true;
            this.btnStringHashLookup.Click += new System.EventHandler(this.btnStringHashLookup_Click);
            // 
            // tbStringHashLookup
            // 
            this.tbStringHashLookup.Location = new System.Drawing.Point(6, 22);
            this.tbStringHashLookup.Name = "tbStringHashLookup";
            this.tbStringHashLookup.Size = new System.Drawing.Size(188, 23);
            this.tbStringHashLookup.TabIndex = 0;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.btnStringHashCalc);
            this.groupBox2.Controls.Add(this.tbStringHash);
            this.groupBox2.Location = new System.Drawing.Point(12, 103);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(200, 85);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Hash";
            // 
            // btnStringHashCalc
            // 
            this.btnStringHashCalc.Location = new System.Drawing.Point(6, 51);
            this.btnStringHashCalc.Name = "btnStringHashCalc";
            this.btnStringHashCalc.Size = new System.Drawing.Size(188, 23);
            this.btnStringHashCalc.TabIndex = 2;
            this.btnStringHashCalc.Text = "Calculate hash";
            this.btnStringHashCalc.UseVisualStyleBackColor = true;
            this.btnStringHashCalc.Click += new System.EventHandler(this.btnStringHashCalc_Click);
            // 
            // tbStringHash
            // 
            this.tbStringHash.Location = new System.Drawing.Point(6, 22);
            this.tbStringHash.Name = "tbStringHash";
            this.tbStringHash.Size = new System.Drawing.Size(188, 23);
            this.tbStringHash.TabIndex = 2;
            // 
            // tbStringHashResult
            // 
            this.tbStringHashResult.Location = new System.Drawing.Point(12, 203);
            this.tbStringHashResult.Name = "tbStringHashResult";
            this.tbStringHashResult.PlaceholderText = "Result...";
            this.tbStringHashResult.ReadOnly = true;
            this.tbStringHashResult.Size = new System.Drawing.Size(200, 23);
            this.tbStringHashResult.TabIndex = 2;
            this.tbStringHashResult.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // StringHashEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(230, 249);
            this.Controls.Add(this.tbStringHashResult);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MinimizeBox = false;
            this.Name = "StringHashEditor";
            this.Text = "StringHashEditor";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private GroupBox groupBox1;
        private Button btnStringHashLookup;
        private TextBox tbStringHashLookup;
        private GroupBox groupBox2;
        private Button btnStringHashCalc;
        private TextBox tbStringHash;
        private TextBox tbStringHashResult;
    }
}