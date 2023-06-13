namespace Haven.Forms
{
    partial class DldSelector
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
            dldLabelMain = new Label();
            dldLabelMips = new Label();
            dldSelectMain = new ComboBox();
            dldSelectMips = new ComboBox();
            dldBtnSubmit = new Button();
            SuspendLayout();
            // 
            // dldLabelMain
            // 
            dldLabelMain.AutoSize = true;
            dldLabelMain.Location = new Point(12, 9);
            dldLabelMain.Name = "dldLabelMain";
            dldLabelMain.Size = new Size(34, 15);
            dldLabelMain.TabIndex = 0;
            dldLabelMain.Text = "Main";
            // 
            // dldLabelMips
            // 
            dldLabelMips.AutoSize = true;
            dldLabelMips.Location = new Point(12, 66);
            dldLabelMips.Name = "dldLabelMips";
            dldLabelMips.Size = new Size(57, 15);
            dldLabelMips.TabIndex = 1;
            dldLabelMips.Text = "Mipmaps";
            // 
            // dldSelectMain
            // 
            dldSelectMain.DropDownStyle = ComboBoxStyle.DropDownList;
            dldSelectMain.FormattingEnabled = true;
            dldSelectMain.Location = new Point(12, 27);
            dldSelectMain.Name = "dldSelectMain";
            dldSelectMain.Size = new Size(220, 23);
            dldSelectMain.TabIndex = 2;
            dldSelectMain.SelectedIndexChanged += dldSelectMain_SelectedIndexChanged;
            // 
            // dldSelectMips
            // 
            dldSelectMips.DropDownStyle = ComboBoxStyle.DropDownList;
            dldSelectMips.FormattingEnabled = true;
            dldSelectMips.Location = new Point(12, 84);
            dldSelectMips.Name = "dldSelectMips";
            dldSelectMips.Size = new Size(220, 23);
            dldSelectMips.TabIndex = 3;
            dldSelectMips.SelectedIndexChanged += dldSelectMips_SelectedIndexChanged;
            // 
            // dldBtnSubmit
            // 
            dldBtnSubmit.Location = new Point(12, 140);
            dldBtnSubmit.Name = "dldBtnSubmit";
            dldBtnSubmit.Size = new Size(220, 30);
            dldBtnSubmit.TabIndex = 4;
            dldBtnSubmit.Text = "Submit";
            dldBtnSubmit.UseVisualStyleBackColor = true;
            dldBtnSubmit.Click += dldBtnSubmit_Click;
            // 
            // DldSelector
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(244, 182);
            Controls.Add(dldBtnSubmit);
            Controls.Add(dldSelectMips);
            Controls.Add(dldSelectMain);
            Controls.Add(dldLabelMips);
            Controls.Add(dldLabelMain);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Name = "DldSelector";
            StartPosition = FormStartPosition.CenterParent;
            Text = "DldSelector";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label dldLabelMain;
        private Label dldLabelMips;
        private ComboBox dldSelectMain;
        private ComboBox dldSelectMips;
        private Button dldBtnSubmit;
    }
}