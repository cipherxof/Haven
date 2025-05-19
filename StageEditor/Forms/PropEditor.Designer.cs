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
            tbSpawnEditX = new TextBox();
            tbSpawnEditZ = new TextBox();
            tbSpawnEditY = new TextBox();
            labelSpawnEditX = new Label();
            labelSpawnEditY = new Label();
            labelSpawnEditZ = new Label();
            btnSpawnEditApply = new Button();
            btnSpawnEditUseCam = new Button();
            tbExtraData = new TextBox();
            label1 = new Label();
            btnSnapGround = new Button();
            SuspendLayout();
            // 
            // tbSpawnEditX
            // 
            tbSpawnEditX.Location = new Point(22, 79);
            tbSpawnEditX.Margin = new Padding(6);
            tbSpawnEditX.Name = "tbSpawnEditX";
            tbSpawnEditX.Size = new Size(182, 39);
            tbSpawnEditX.TabIndex = 0;
            // 
            // tbSpawnEditZ
            // 
            tbSpawnEditZ.Location = new Point(219, 79);
            tbSpawnEditZ.Margin = new Padding(6);
            tbSpawnEditZ.Name = "tbSpawnEditZ";
            tbSpawnEditZ.Size = new Size(182, 39);
            tbSpawnEditZ.TabIndex = 1;
            // 
            // tbSpawnEditY
            // 
            tbSpawnEditY.Location = new Point(416, 79);
            tbSpawnEditY.Margin = new Padding(6);
            tbSpawnEditY.Name = "tbSpawnEditY";
            tbSpawnEditY.Size = new Size(182, 39);
            tbSpawnEditY.TabIndex = 2;
            // 
            // labelSpawnEditX
            // 
            labelSpawnEditX.AutoSize = true;
            labelSpawnEditX.Location = new Point(22, 41);
            labelSpawnEditX.Margin = new Padding(6, 0, 6, 0);
            labelSpawnEditX.Name = "labelSpawnEditX";
            labelSpawnEditX.Size = new Size(28, 32);
            labelSpawnEditX.TabIndex = 3;
            labelSpawnEditX.Text = "X";
            // 
            // labelSpawnEditY
            // 
            labelSpawnEditY.AutoSize = true;
            labelSpawnEditY.Location = new Point(219, 41);
            labelSpawnEditY.Margin = new Padding(6, 0, 6, 0);
            labelSpawnEditY.Name = "labelSpawnEditY";
            labelSpawnEditY.Size = new Size(28, 32);
            labelSpawnEditY.TabIndex = 4;
            labelSpawnEditY.Text = "Z";
            // 
            // labelSpawnEditZ
            // 
            labelSpawnEditZ.AutoSize = true;
            labelSpawnEditZ.Location = new Point(416, 41);
            labelSpawnEditZ.Margin = new Padding(6, 0, 6, 0);
            labelSpawnEditZ.Name = "labelSpawnEditZ";
            labelSpawnEditZ.Size = new Size(27, 32);
            labelSpawnEditZ.TabIndex = 5;
            labelSpawnEditZ.Text = "Y";
            // 
            // btnSpawnEditApply
            // 
            btnSpawnEditApply.Location = new Point(22, 334);
            btnSpawnEditApply.Margin = new Padding(6);
            btnSpawnEditApply.Name = "btnSpawnEditApply";
            btnSpawnEditApply.Size = new Size(581, 49);
            btnSpawnEditApply.TabIndex = 6;
            btnSpawnEditApply.Text = "Apply";
            btnSpawnEditApply.UseVisualStyleBackColor = true;
            btnSpawnEditApply.Click += btnSpawnEditApply_Click;
            // 
            // btnSpawnEditUseCam
            // 
            btnSpawnEditUseCam.Location = new Point(22, 267);
            btnSpawnEditUseCam.Margin = new Padding(6);
            btnSpawnEditUseCam.Name = "btnSpawnEditUseCam";
            btnSpawnEditUseCam.Size = new Size(270, 46);
            btnSpawnEditUseCam.TabIndex = 7;
            btnSpawnEditUseCam.Text = "Use Camera View";
            btnSpawnEditUseCam.UseVisualStyleBackColor = true;
            btnSpawnEditUseCam.Click += btnSpawnEditUseCam_Click;
            // 
            // tbExtraData
            // 
            tbExtraData.Enabled = false;
            tbExtraData.Location = new Point(22, 183);
            tbExtraData.Margin = new Padding(6);
            tbExtraData.Name = "tbExtraData";
            tbExtraData.Size = new Size(570, 39);
            tbExtraData.TabIndex = 8;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(22, 145);
            label1.Margin = new Padding(6, 0, 6, 0);
            label1.Name = "label1";
            label1.Size = new Size(121, 32);
            label1.TabIndex = 9;
            label1.Text = "Extra Data";
            // 
            // btnSnapGround
            // 
            btnSnapGround.Location = new Point(322, 267);
            btnSnapGround.Name = "btnSnapGround";
            btnSnapGround.Size = new Size(270, 46);
            btnSnapGround.TabIndex = 10;
            btnSnapGround.Text = "Snap to Ground";
            btnSnapGround.UseVisualStyleBackColor = true;
            btnSnapGround.Click += btnSnapGround_Click;
            // 
            // PropEditor
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(618, 398);
            Controls.Add(btnSnapGround);
            Controls.Add(label1);
            Controls.Add(tbExtraData);
            Controls.Add(btnSpawnEditUseCam);
            Controls.Add(btnSpawnEditApply);
            Controls.Add(labelSpawnEditZ);
            Controls.Add(labelSpawnEditY);
            Controls.Add(labelSpawnEditX);
            Controls.Add(tbSpawnEditY);
            Controls.Add(tbSpawnEditZ);
            Controls.Add(tbSpawnEditX);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(6);
            MaximizeBox = false;
            Name = "PropEditor";
            StartPosition = FormStartPosition.CenterParent;
            Text = "SpawnEditor";
            Load += SpawnEditor_Load;
            ResumeLayout(false);
            PerformLayout();
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
        private TextBox tbExtraData;
        private Label label1;
        private Button btnSnapGround;
    }
}