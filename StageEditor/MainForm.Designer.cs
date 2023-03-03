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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cbFlagsAll = new System.Windows.Forms.CheckBox();
            this.cbFlags1000000 = new System.Windows.Forms.CheckBox();
            this.cbFlagsWater = new System.Windows.Forms.CheckBox();
            this.cbFlagsRail = new System.Windows.Forms.CheckBox();
            this.cbFlagsStairs = new System.Windows.Forms.CheckBox();
            this.cbFlags800000 = new System.Windows.Forms.CheckBox();
            this.cbWireframe = new System.Windows.Forms.CheckBox();
            this.btnExportMesh = new System.Windows.Forms.Button();
            this.tbSpawnsFilter = new System.Windows.Forms.TextBox();
            this.treeViewGeom = new System.Windows.Forms.TreeView();
            this.glControl = new OpenTK.GLControl();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.btnLoadStage = new System.Windows.Forms.ToolStripDropDownButton();
            this.mGO2StageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mGS4StageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mGAStageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.toolStripDropDownButtonTools = new System.Windows.Forms.ToolStripDropDownButton();
            this.solidEyeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.encryptFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.decryptFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.mergeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mergeVLMToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.stringHashUtilityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.labelStatus = new System.Windows.Forms.Label();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.labelCamPos = new System.Windows.Forms.Label();
            this.tabControl.SuspendLayout();
            this.tabPageFiles.SuspendLayout();
            this.tabPageGeom.SuspendLayout();
            this.groupBox1.SuspendLayout();
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
            this.tabPageGeom.Controls.Add(this.groupBox1);
            this.tabPageGeom.Controls.Add(this.cbWireframe);
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
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBox1.BackColor = System.Drawing.Color.Transparent;
            this.groupBox1.Controls.Add(this.cbFlagsAll);
            this.groupBox1.Controls.Add(this.cbFlags1000000);
            this.groupBox1.Controls.Add(this.cbFlagsWater);
            this.groupBox1.Controls.Add(this.cbFlagsRail);
            this.groupBox1.Controls.Add(this.cbFlagsStairs);
            this.groupBox1.Controls.Add(this.cbFlags800000);
            this.groupBox1.Location = new System.Drawing.Point(8, 298);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(208, 181);
            this.groupBox1.TabIndex = 11;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Flags";
            // 
            // cbFlagsAll
            // 
            this.cbFlagsAll.AutoSize = true;
            this.cbFlagsAll.BackColor = System.Drawing.Color.Transparent;
            this.cbFlagsAll.Location = new System.Drawing.Point(6, 22);
            this.cbFlagsAll.Name = "cbFlagsAll";
            this.cbFlagsAll.Size = new System.Drawing.Size(40, 19);
            this.cbFlagsAll.TabIndex = 6;
            this.cbFlagsAll.Text = "All";
            this.cbFlagsAll.UseVisualStyleBackColor = false;
            this.cbFlagsAll.CheckedChanged += new System.EventHandler(this.cbFlags_CheckedChanged);
            // 
            // cbFlags1000000
            // 
            this.cbFlags1000000.AutoSize = true;
            this.cbFlags1000000.BackColor = System.Drawing.Color.Transparent;
            this.cbFlags1000000.Checked = true;
            this.cbFlags1000000.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbFlags1000000.ForeColor = System.Drawing.Color.Khaki;
            this.cbFlags1000000.Location = new System.Drawing.Point(6, 147);
            this.cbFlags1000000.Name = "cbFlags1000000";
            this.cbFlags1000000.Size = new System.Drawing.Size(80, 19);
            this.cbFlags1000000.TabIndex = 5;
            this.cbFlags1000000.Text = "0x1000000";
            this.cbFlags1000000.UseVisualStyleBackColor = false;
            this.cbFlags1000000.CheckedChanged += new System.EventHandler(this.cbFlags_CheckedChanged);
            // 
            // cbFlagsWater
            // 
            this.cbFlagsWater.AutoSize = true;
            this.cbFlagsWater.BackColor = System.Drawing.Color.Transparent;
            this.cbFlagsWater.Checked = true;
            this.cbFlagsWater.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbFlagsWater.ForeColor = System.Drawing.Color.Teal;
            this.cbFlagsWater.Location = new System.Drawing.Point(6, 122);
            this.cbFlagsWater.Name = "cbFlagsWater";
            this.cbFlagsWater.Size = new System.Drawing.Size(57, 19);
            this.cbFlagsWater.TabIndex = 3;
            this.cbFlagsWater.Text = "Water";
            this.cbFlagsWater.UseVisualStyleBackColor = false;
            this.cbFlagsWater.CheckedChanged += new System.EventHandler(this.cbFlags_CheckedChanged);
            // 
            // cbFlagsRail
            // 
            this.cbFlagsRail.AutoSize = true;
            this.cbFlagsRail.BackColor = System.Drawing.Color.Transparent;
            this.cbFlagsRail.Checked = true;
            this.cbFlagsRail.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbFlagsRail.ForeColor = System.Drawing.Color.Purple;
            this.cbFlagsRail.Location = new System.Drawing.Point(6, 97);
            this.cbFlagsRail.Name = "cbFlagsRail";
            this.cbFlagsRail.Size = new System.Drawing.Size(50, 19);
            this.cbFlagsRail.TabIndex = 2;
            this.cbFlagsRail.Text = "Rails";
            this.cbFlagsRail.UseVisualStyleBackColor = false;
            this.cbFlagsRail.CheckedChanged += new System.EventHandler(this.cbFlags_CheckedChanged);
            // 
            // cbFlagsStairs
            // 
            this.cbFlagsStairs.AutoSize = true;
            this.cbFlagsStairs.BackColor = System.Drawing.Color.Transparent;
            this.cbFlagsStairs.Checked = true;
            this.cbFlagsStairs.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbFlagsStairs.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(192)))), ((int)(((byte)(0)))));
            this.cbFlagsStairs.Location = new System.Drawing.Point(6, 72);
            this.cbFlagsStairs.Name = "cbFlagsStairs";
            this.cbFlagsStairs.Size = new System.Drawing.Size(54, 19);
            this.cbFlagsStairs.TabIndex = 1;
            this.cbFlagsStairs.Text = "Stairs";
            this.cbFlagsStairs.UseVisualStyleBackColor = false;
            this.cbFlagsStairs.CheckedChanged += new System.EventHandler(this.cbFlags_CheckedChanged);
            // 
            // cbFlags800000
            // 
            this.cbFlags800000.AutoSize = true;
            this.cbFlags800000.Checked = true;
            this.cbFlags800000.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbFlags800000.Location = new System.Drawing.Point(6, 47);
            this.cbFlags800000.Name = "cbFlags800000";
            this.cbFlags800000.Size = new System.Drawing.Size(74, 19);
            this.cbFlags800000.TabIndex = 0;
            this.cbFlags800000.Text = "0x800000";
            this.cbFlags800000.UseVisualStyleBackColor = true;
            this.cbFlags800000.CheckedChanged += new System.EventHandler(this.cbFlags_CheckedChanged);
            // 
            // cbWireframe
            // 
            this.cbWireframe.AutoSize = true;
            this.cbWireframe.Location = new System.Drawing.Point(8, 11);
            this.cbWireframe.Name = "cbWireframe";
            this.cbWireframe.Size = new System.Drawing.Size(81, 19);
            this.cbWireframe.TabIndex = 10;
            this.cbWireframe.Text = "Wireframe";
            this.cbWireframe.ThreeState = true;
            this.cbWireframe.UseVisualStyleBackColor = true;
            this.cbWireframe.CheckStateChanged += new System.EventHandler(this.cbWireframe_CheckStateChanged);
            // 
            // btnExportMesh
            // 
            this.btnExportMesh.Location = new System.Drawing.Point(8, 34);
            this.btnExportMesh.Name = "btnExportMesh";
            this.btnExportMesh.Size = new System.Drawing.Size(208, 23);
            this.btnExportMesh.TabIndex = 9;
            this.btnExportMesh.Text = "Export Model";
            this.btnExportMesh.UseVisualStyleBackColor = true;
            this.btnExportMesh.Click += new System.EventHandler(this.btnExportMesh_Click);
            // 
            // tbSpawnsFilter
            // 
            this.tbSpawnsFilter.Location = new System.Drawing.Point(8, 63);
            this.tbSpawnsFilter.Name = "tbSpawnsFilter";
            this.tbSpawnsFilter.PlaceholderText = "Search...";
            this.tbSpawnsFilter.Size = new System.Drawing.Size(208, 23);
            this.tbSpawnsFilter.TabIndex = 8;
            this.tbSpawnsFilter.KeyDown += new System.Windows.Forms.KeyEventHandler(this.tbSpawnsFilter_KeyDown);
            // 
            // treeViewGeom
            // 
            this.treeViewGeom.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.treeViewGeom.Location = new System.Drawing.Point(8, 92);
            this.treeViewGeom.Name = "treeViewGeom";
            this.treeViewGeom.Size = new System.Drawing.Size(208, 200);
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
            this.btnLoadStage.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mGO2StageToolStripMenuItem,
            this.mGS4StageToolStripMenuItem,
            this.mGAStageToolStripMenuItem});
            this.btnLoadStage.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnLoadStage.Image = ((System.Drawing.Image)(resources.GetObject("btnLoadStage.Image")));
            this.btnLoadStage.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnLoadStage.Name = "btnLoadStage";
            this.btnLoadStage.Size = new System.Drawing.Size(62, 22);
            this.btnLoadStage.Text = "Load";
            // 
            // mGO2StageToolStripMenuItem
            // 
            this.mGO2StageToolStripMenuItem.Name = "mGO2StageToolStripMenuItem";
            this.mGO2StageToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
            this.mGO2StageToolStripMenuItem.Text = "MGO2 Stage";
            this.mGO2StageToolStripMenuItem.Click += new System.EventHandler(this.mGO2StageToolStripMenuItem_Click);
            // 
            // mGS4StageToolStripMenuItem
            // 
            this.mGS4StageToolStripMenuItem.Name = "mGS4StageToolStripMenuItem";
            this.mGS4StageToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
            this.mGS4StageToolStripMenuItem.Text = "MGS4 Stage";
            this.mGS4StageToolStripMenuItem.Click += new System.EventHandler(this.mGS4StageToolStripMenuItem_Click);
            // 
            // mGAStageToolStripMenuItem
            // 
            this.mGAStageToolStripMenuItem.Name = "mGAStageToolStripMenuItem";
            this.mGAStageToolStripMenuItem.Size = new System.Drawing.Size(140, 22);
            this.mGAStageToolStripMenuItem.Text = "MGA Stage";
            this.mGAStageToolStripMenuItem.Click += new System.EventHandler(this.mGAStageToolStripMenuItem_Click);
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
            this.solidEyeToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.solidEyeToolStripMenuItem.Text = "SolidEye";
            // 
            // encryptFileToolStripMenuItem
            // 
            this.encryptFileToolStripMenuItem.Name = "encryptFileToolStripMenuItem";
            this.encryptFileToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            this.encryptFileToolStripMenuItem.Text = "Encrypt File";
            this.encryptFileToolStripMenuItem.Click += new System.EventHandler(this.encryptFileToolStripMenuItem_Click);
            // 
            // decryptFileToolStripMenuItem
            // 
            this.decryptFileToolStripMenuItem.Name = "decryptFileToolStripMenuItem";
            this.decryptFileToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
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
            this.toolStripMenuItem1.Size = new System.Drawing.Size(135, 22);
            this.toolStripMenuItem1.Text = "Geom";
            // 
            // mergeToolStripMenuItem
            // 
            this.mergeToolStripMenuItem.Name = "mergeToolStripMenuItem";
            this.mergeToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.mergeToolStripMenuItem.Text = "Merge Groups";
            this.mergeToolStripMenuItem.Click += new System.EventHandler(this.mergeToolStripMenuItem_Click);
            // 
            // mergeVLMToolStripMenuItem
            // 
            this.mergeVLMToolStripMenuItem.Name = "mergeVLMToolStripMenuItem";
            this.mergeVLMToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.mergeVLMToolStripMenuItem.Text = "Merge VLM";
            this.mergeVLMToolStripMenuItem.Click += new System.EventHandler(this.mergeVLMToolStripMenuItem_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(149, 22);
            this.toolStripMenuItem2.Text = "Endian Swap";
            this.toolStripMenuItem2.Click += new System.EventHandler(this.toolStripMenuItem2_Click);
            // 
            // stringHashUtilityToolStripMenuItem
            // 
            this.stringHashUtilityToolStripMenuItem.Name = "stringHashUtilityToolStripMenuItem";
            this.stringHashUtilityToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
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
            this.Text = "Haven v0.0.6";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.tabControl.ResumeLayout(false);
            this.tabPageFiles.ResumeLayout(false);
            this.tabPageGeom.ResumeLayout(false);
            this.tabPageGeom.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private TabControl tabControl;
        private TabPage tabPageFiles;
        private ToolStrip toolStrip1;
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
        private ToolStripDropDownButton btnLoadStage;
        private ToolStripMenuItem mGO2StageToolStripMenuItem;
        private ToolStripMenuItem mGS4StageToolStripMenuItem;
        private ToolStripMenuItem mGAStageToolStripMenuItem;
        private CheckBox cbWireframe;
        private GroupBox groupBox1;
        private CheckBox cbFlagsStairs;
        private CheckBox cbFlags800000;
        private CheckBox cbFlagsRail;
        private CheckBox cbFlagsWater;
        private CheckBox cbFlags1000000;
        private CheckBox cbFlagsAll;
    }
}