namespace Haven
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageFiles = new System.Windows.Forms.TabPage();
            this.treeViewFiles = new System.Windows.Forms.TreeView();
            this.imageListFileTypes = new System.Windows.Forms.ImageList(this.components);
            this.tabPageGeom = new System.Windows.Forms.TabPage();
            this.btnExportMesh = new System.Windows.Forms.Button();
            this.tbSpawnsFilter = new System.Windows.Forms.TextBox();
            this.treeViewGeom = new System.Windows.Forms.TreeView();
            this.glControl = new OpenTK.GLControl();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnLoadStage = new System.Windows.Forms.ToolStripButton();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.toolStripDropDownButtonTools = new System.Windows.Forms.ToolStripDropDownButton();
            this.solidEyeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.encryptFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.decryptFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.mergeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.stringHashUtilityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.labelStatus = new System.Windows.Forms.Label();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.labelCamPos = new System.Windows.Forms.Label();
            this.mergeVLMToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControl.SuspendLayout();
            this.tabPageFiles.SuspendLayout();
            this.tabPageGeom.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabPageFiles);
            this.tabControl.Controls.Add(this.tabPageGeom);
            this.tabControl.Location = new System.Drawing.Point(0, 28);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(884, 510);
            this.tabControl.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabControl.TabIndex = 0;
            // 
            // tabPageFiles
            // 
            this.tabPageFiles.Controls.Add(this.treeViewFiles);
            this.tabPageFiles.Location = new System.Drawing.Point(4, 24);
            this.tabPageFiles.Name = "tabPageFiles";
            this.tabPageFiles.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageFiles.Size = new System.Drawing.Size(876, 482);
            this.tabPageFiles.TabIndex = 0;
            this.tabPageFiles.Text = "Main";
            this.tabPageFiles.UseVisualStyleBackColor = true;
            // 
            // treeViewFiles
            // 
            this.treeViewFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeViewFiles.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.treeViewFiles.Location = new System.Drawing.Point(0, 0);
            this.treeViewFiles.Name = "treeViewFiles";
            this.treeViewFiles.Size = new System.Drawing.Size(876, 482);
            this.treeViewFiles.StateImageList = this.imageListFileTypes;
            this.treeViewFiles.TabIndex = 0;
            this.treeViewFiles.MouseUp += new System.Windows.Forms.MouseEventHandler(this.treeViewFiles_MouseUp);
            // 
            // imageListFileTypes
            // 
            this.imageListFileTypes.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
            this.imageListFileTypes.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListFileTypes.ImageStream")));
            this.imageListFileTypes.TransparentColor = System.Drawing.Color.Transparent;
            this.imageListFileTypes.Images.SetKeyName(0, "File");
            this.imageListFileTypes.Images.SetKeyName(1, "Folder");
            this.imageListFileTypes.Images.SetKeyName(2, "Mesh");
            // 
            // tabPageGeom
            // 
            this.tabPageGeom.Controls.Add(this.btnExportMesh);
            this.tabPageGeom.Controls.Add(this.tbSpawnsFilter);
            this.tabPageGeom.Controls.Add(this.treeViewGeom);
            this.tabPageGeom.Controls.Add(this.glControl);
            this.tabPageGeom.Location = new System.Drawing.Point(4, 24);
            this.tabPageGeom.Name = "tabPageGeom";
            this.tabPageGeom.Size = new System.Drawing.Size(876, 482);
            this.tabPageGeom.TabIndex = 1;
            this.tabPageGeom.Text = "Geom";
            this.tabPageGeom.UseVisualStyleBackColor = true;
            // 
            // btnExportMesh
            // 
            this.btnExportMesh.Location = new System.Drawing.Point(8, 11);
            this.btnExportMesh.Name = "btnExportMesh";
            this.btnExportMesh.Size = new System.Drawing.Size(208, 23);
            this.btnExportMesh.TabIndex = 9;
            this.btnExportMesh.Text = "Export Model";
            this.btnExportMesh.UseVisualStyleBackColor = true;
            this.btnExportMesh.Click += new System.EventHandler(this.btnExportMesh_Click);
            // 
            // tbSpawnsFilter
            // 
            this.tbSpawnsFilter.Enabled = false;
            this.tbSpawnsFilter.Location = new System.Drawing.Point(8, 40);
            this.tbSpawnsFilter.Name = "tbSpawnsFilter";
            this.tbSpawnsFilter.PlaceholderText = "Search...";
            this.tbSpawnsFilter.Size = new System.Drawing.Size(208, 23);
            this.tbSpawnsFilter.TabIndex = 8;
            this.tbSpawnsFilter.TextChanged += new System.EventHandler(this.tbSpawnsFilter_TextChanged);
            // 
            // treeViewGeom
            // 
            this.treeViewGeom.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.treeViewGeom.Location = new System.Drawing.Point(8, 69);
            this.treeViewGeom.Name = "treeViewGeom";
            this.treeViewGeom.Size = new System.Drawing.Size(208, 410);
            this.treeViewGeom.TabIndex = 6;
            this.treeViewGeom.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.treeViewGeom_AfterCheck);
            this.treeViewGeom.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeViewGeom_AfterSelect);
            this.treeViewGeom.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeViewGeom_NodeMouseDoubleClick);
            this.treeViewGeom.MouseUp += new System.Windows.Forms.MouseEventHandler(this.treeViewGeom_MouseUp);
            // 
            // glControl
            // 
            this.glControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.glControl.BackColor = System.Drawing.Color.Black;
            this.glControl.Location = new System.Drawing.Point(223, 11);
            this.glControl.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.glControl.Name = "glControl";
            this.glControl.Size = new System.Drawing.Size(644, 468);
            this.glControl.TabIndex = 2;
            this.glControl.VSync = true;
            this.glControl.Load += new System.EventHandler(this.glControl_Load);
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnLoadStage,
            this.btnSave,
            this.toolStripDropDownButtonTools});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(884, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // btnLoadStage
            // 
            this.btnLoadStage.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnLoadStage.Image = ((System.Drawing.Image)(resources.GetObject("btnLoadStage.Image")));
            this.btnLoadStage.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnLoadStage.Name = "btnLoadStage";
            this.btnLoadStage.Size = new System.Drawing.Size(53, 22);
            this.btnLoadStage.Text = "Load";
            this.btnLoadStage.Click += new System.EventHandler(this.btnLoadStage_Click);
            // 
            // btnSave
            // 
            this.btnSave.Enabled = false;
            this.btnSave.Image = ((System.Drawing.Image)(resources.GetObject("btnSave.Image")));
            this.btnSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(65, 22);
            this.btnSave.Text = "Save as";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // toolStripDropDownButtonTools
            // 
            this.toolStripDropDownButtonTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.solidEyeToolStripMenuItem,
            this.toolStripMenuItem1,
            this.stringHashUtilityToolStripMenuItem});
            this.toolStripDropDownButtonTools.Image = ((System.Drawing.Image)(resources.GetObject("toolStripDropDownButtonTools.Image")));
            this.toolStripDropDownButtonTools.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownButtonTools.Name = "toolStripDropDownButtonTools";
            this.toolStripDropDownButtonTools.Size = new System.Drawing.Size(63, 22);
            this.toolStripDropDownButtonTools.Text = "Tools";
            // 
            // solidEyeToolStripMenuItem
            // 
            this.solidEyeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.encryptFileToolStripMenuItem,
            this.decryptFileToolStripMenuItem});
            this.solidEyeToolStripMenuItem.Name = "solidEyeToolStripMenuItem";
            this.solidEyeToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.solidEyeToolStripMenuItem.Text = "SolidEye";
            // 
            // encryptFileToolStripMenuItem
            // 
            this.encryptFileToolStripMenuItem.Name = "encryptFileToolStripMenuItem";
            this.encryptFileToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.encryptFileToolStripMenuItem.Text = "Encrypt File";
            this.encryptFileToolStripMenuItem.Click += new System.EventHandler(this.encryptFileToolStripMenuItem_Click);
            // 
            // decryptFileToolStripMenuItem
            // 
            this.decryptFileToolStripMenuItem.Name = "decryptFileToolStripMenuItem";
            this.decryptFileToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.decryptFileToolStripMenuItem.Text = "Decrypt File";
            this.decryptFileToolStripMenuItem.Click += new System.EventHandler(this.decryptFileToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mergeToolStripMenuItem,
            this.mergeVLMToolStripMenuItem,
            this.toolStripMenuItem2});
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(180, 22);
            this.toolStripMenuItem1.Text = "Geom";
            // 
            // mergeToolStripMenuItem
            // 
            this.mergeToolStripMenuItem.Name = "mergeToolStripMenuItem";
            this.mergeToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.mergeToolStripMenuItem.Text = "Merge Groups";
            this.mergeToolStripMenuItem.Click += new System.EventHandler(this.mergeToolStripMenuItem_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(180, 22);
            this.toolStripMenuItem2.Text = "Endian Swap";
            this.toolStripMenuItem2.Click += new System.EventHandler(this.toolStripMenuItem2_Click);
            // 
            // stringHashUtilityToolStripMenuItem
            // 
            this.stringHashUtilityToolStripMenuItem.Name = "stringHashUtilityToolStripMenuItem";
            this.stringHashUtilityToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.stringHashUtilityToolStripMenuItem.Text = "String Hash";
            this.stringHashUtilityToolStripMenuItem.Click += new System.EventHandler(this.stringHashUtilityToolStripMenuItem_Click);
            // 
            // labelStatus
            // 
            this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(4, 541);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(39, 15);
            this.labelStatus.TabIndex = 2;
            this.labelStatus.Text = "Ready";
            // 
            // timerRefresh
            // 
            this.timerRefresh.Enabled = true;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);
            // 
            // labelCamPos
            // 
            this.labelCamPos.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelCamPos.AutoSize = true;
            this.labelCamPos.Location = new System.Drawing.Point(224, 541);
            this.labelCamPos.Name = "labelCamPos";
            this.labelCamPos.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.labelCamPos.Size = new System.Drawing.Size(0, 15);
            this.labelCamPos.TabIndex = 3;
            this.labelCamPos.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // mergeVLMToolStripMenuItem
            // 
            this.mergeVLMToolStripMenuItem.Name = "mergeVLMToolStripMenuItem";
            this.mergeVLMToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.mergeVLMToolStripMenuItem.Text = "Merge VLM";
            this.mergeVLMToolStripMenuItem.Click += new System.EventHandler(this.mergeVLMToolStripMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 561);
            this.Controls.Add(this.labelCamPos);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.tabControl);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(720, 500);
            this.Name = "MainForm";
            this.Text = "Haven";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.tabControl.ResumeLayout(false);
            this.tabPageFiles.ResumeLayout(false);
            this.tabPageGeom.ResumeLayout(false);
            this.tabPageGeom.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private TabControl tabControl;
        private TabPage tabPageFiles;
        private ToolStrip toolStrip1;
        private ToolStripButton btnLoadStage;
        private TreeView treeViewFiles;
        private ToolStripButton btnSave;
        private ImageList imageListFileTypes;
        private Label labelStatus;
        private TabPage tabPageGeom;
        private OpenTK.GLControl glControl;
        private System.Windows.Forms.Timer timerRefresh;
        private TreeView treeViewGeom;
        private TextBox tbSpawnsFilter;
        private Label labelCamPos;
        private Button btnExportMesh;
        private ToolStripDropDownButton toolStripDropDownButtonTools;
        private ToolStripMenuItem solidEyeToolStripMenuItem;
        private ToolStripMenuItem encryptFileToolStripMenuItem;
        private ToolStripMenuItem decryptFileToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripMenuItem mergeToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem2;
        private ToolStripMenuItem stringHashUtilityToolStripMenuItem;
        private ToolStripMenuItem mergeVLMToolStripMenuItem;
    }
}