namespace Haven
{
    partial class PropEditor
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
            this.tbSpawnEditX = new System.Windows.Forms.TextBox();
            this.tbSpawnEditZ = new System.Windows.Forms.TextBox();
            this.tbSpawnEditY = new System.Windows.Forms.TextBox();
            this.labelSpawnEditX = new System.Windows.Forms.Label();
            this.labelSpawnEditY = new System.Windows.Forms.Label();
            this.labelSpawnEditZ = new System.Windows.Forms.Label();
            this.btnSpawnEditApply = new System.Windows.Forms.Button();
            this.btnSpawnEditUseCam = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // tbSpawnEditX
            // 
            this.tbSpawnEditX.Location = new System.Drawing.Point(12, 37);
            this.tbSpawnEditX.Name = "tbSpawnEditX";
            this.tbSpawnEditX.Size = new System.Drawing.Size(100, 23);
            this.tbSpawnEditX.TabIndex = 0;
            // 
            // tbSpawnEditZ
            // 
            this.tbSpawnEditZ.Location = new System.Drawing.Point(118, 37);
            this.tbSpawnEditZ.Name = "tbSpawnEditZ";
            this.tbSpawnEditZ.Size = new System.Drawing.Size(100, 23);
            this.tbSpawnEditZ.TabIndex = 1;
            // 
            // tbSpawnEditY
            // 
            this.tbSpawnEditY.Location = new System.Drawing.Point(224, 37);
            this.tbSpawnEditY.Name = "tbSpawnEditY";
            this.tbSpawnEditY.Size = new System.Drawing.Size(100, 23);
            this.tbSpawnEditY.TabIndex = 2;
            // 
            // labelSpawnEditX
            // 
            this.labelSpawnEditX.AutoSize = true;
            this.labelSpawnEditX.Location = new System.Drawing.Point(12, 19);
            this.labelSpawnEditX.Name = "labelSpawnEditX";
            this.labelSpawnEditX.Size = new System.Drawing.Size(14, 15);
            this.labelSpawnEditX.TabIndex = 3;
            this.labelSpawnEditX.Text = "X";
            // 
            // labelSpawnEditY
            // 
            this.labelSpawnEditY.AutoSize = true;
            this.labelSpawnEditY.Location = new System.Drawing.Point(118, 19);
            this.labelSpawnEditY.Name = "labelSpawnEditY";
            this.labelSpawnEditY.Size = new System.Drawing.Size(14, 15);
            this.labelSpawnEditY.TabIndex = 4;
            this.labelSpawnEditY.Text = "Z";
            // 
            // labelSpawnEditZ
            // 
            this.labelSpawnEditZ.AutoSize = true;
            this.labelSpawnEditZ.Location = new System.Drawing.Point(224, 19);
            this.labelSpawnEditZ.Name = "labelSpawnEditZ";
            this.labelSpawnEditZ.Size = new System.Drawing.Size(14, 15);
            this.labelSpawnEditZ.TabIndex = 5;
            this.labelSpawnEditZ.Text = "Y";
            // 
            // btnSpawnEditApply
            // 
            this.btnSpawnEditApply.Location = new System.Drawing.Point(249, 125);
            this.btnSpawnEditApply.Name = "btnSpawnEditApply";
            this.btnSpawnEditApply.Size = new System.Drawing.Size(75, 23);
            this.btnSpawnEditApply.TabIndex = 6;
            this.btnSpawnEditApply.Text = "Apply";
            this.btnSpawnEditApply.UseVisualStyleBackColor = true;
            this.btnSpawnEditApply.Click += new System.EventHandler(this.btnSpawnEditApply_Click);
            // 
            // btnSpawnEditUseCam
            // 
            this.btnSpawnEditUseCam.Location = new System.Drawing.Point(108, 66);
            this.btnSpawnEditUseCam.Name = "btnSpawnEditUseCam";
            this.btnSpawnEditUseCam.Size = new System.Drawing.Size(121, 23);
            this.btnSpawnEditUseCam.TabIndex = 7;
            this.btnSpawnEditUseCam.Text = "Use Camera View";
            this.btnSpawnEditUseCam.UseVisualStyleBackColor = true;
            this.btnSpawnEditUseCam.Click += new System.EventHandler(this.btnSpawnEditUseCam_Click);
            // 
            // SpawnEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(333, 160);
            this.Controls.Add(this.btnSpawnEditUseCam);
            this.Controls.Add(this.btnSpawnEditApply);
            this.Controls.Add(this.labelSpawnEditZ);
            this.Controls.Add(this.labelSpawnEditY);
            this.Controls.Add(this.labelSpawnEditX);
            this.Controls.Add(this.tbSpawnEditY);
            this.Controls.Add(this.tbSpawnEditZ);
            this.Controls.Add(this.tbSpawnEditX);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "SpawnEditor";
            this.Text = "SpawnEditor";
            this.Load += new System.EventHandler(this.SpawnEditor_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private TextBox tbSpawnEditX;
        private TextBox tbSpawnEditZ;
        private TextBox tbSpawnEditY;
        private Label labelSpawnEditX;
        private Label labelSpawnEditY;
        private Label labelSpawnEditZ;
        private Button btnSpawnEditApply;
        private Button btnSpawnEditUseCam;
    }
}