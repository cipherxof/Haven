﻿namespace Haven
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
            this.tabPageLog = new System.Windows.Forms.TabPage();
            this.tbLog = new System.Windows.Forms.TextBox();
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
            this.mergeReferencesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mergeVLMToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.stageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.generateTexturesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stringHashUtilityToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.labelStatus = new System.Windows.Forms.Label();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.labelCamPos = new System.Windows.Forms.Label();
            this.labelVersion = new System.Windows.Forms.Label();
            this.tabControl.SuspendLayout();
            this.tabPageFiles.SuspendLayout();
            this.tabPageGeom.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabPageLog.SuspendLayout();
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
            this.tabControl.Controls.Add(this.tabPageLog);
            this.tabControl.Location = new System.Drawing.Point(0, 47);
            this.tabControl.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(1263, 850);
            this.tabControl.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabControl.TabIndex = 2;
            // 
            // tabPageFiles
            // 
            this.tabPageFiles.Controls.Add(this.treeViewFiles);
            this.tabPageFiles.Location = new System.Drawing.Point(4, 34);
            this.tabPageFiles.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tabPageFiles.Name = "tabPageFiles";
            this.tabPageFiles.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tabPageFiles.Size = new System.Drawing.Size(1255, 812);
            this.tabPageFiles.TabIndex = 0;
            this.tabPageFiles.Text = "Main";
            this.tabPageFiles.UseVisualStyleBackColor = true;
            // 
            // treeViewFiles
            // 
            this.treeViewFiles.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.treeViewFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewFiles.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.treeViewFiles.Location = new System.Drawing.Point(4, 5);
            this.treeViewFiles.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.treeViewFiles.Name = "treeViewFiles";
            this.treeViewFiles.Size = new System.Drawing.Size(1247, 802);
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
            this.tabPageGeom.Location = new System.Drawing.Point(4, 34);
            this.tabPageGeom.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tabPageGeom.Name = "tabPageGeom";
            this.tabPageGeom.Size = new System.Drawing.Size(1255, 812);
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
            this.groupBox1.Location = new System.Drawing.Point(11, 497);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBox1.Size = new System.Drawing.Size(297, 302);
            this.groupBox1.TabIndex = 11;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Flags";
            // 
            // cbFlagsAll
            // 
            this.cbFlagsAll.AutoSize = true;
            this.cbFlagsAll.BackColor = System.Drawing.Color.Transparent;
            this.cbFlagsAll.Location = new System.Drawing.Point(9, 37);
            this.cbFlagsAll.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cbFlagsAll.Name = "cbFlagsAll";
            this.cbFlagsAll.Size = new System.Drawing.Size(58, 29);
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
            this.cbFlags1000000.Location = new System.Drawing.Point(9, 245);
            this.cbFlags1000000.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cbFlags1000000.Name = "cbFlags1000000";
            this.cbFlags1000000.Size = new System.Drawing.Size(126, 29);
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
            this.cbFlagsWater.Location = new System.Drawing.Point(9, 203);
            this.cbFlagsWater.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cbFlagsWater.Name = "cbFlagsWater";
            this.cbFlagsWater.Size = new System.Drawing.Size(84, 29);
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
            this.cbFlagsRail.Location = new System.Drawing.Point(9, 162);
            this.cbFlagsRail.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cbFlagsRail.Name = "cbFlagsRail";
            this.cbFlagsRail.Size = new System.Drawing.Size(74, 29);
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
            this.cbFlagsStairs.Location = new System.Drawing.Point(9, 120);
            this.cbFlagsStairs.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cbFlagsStairs.Name = "cbFlagsStairs";
            this.cbFlagsStairs.Size = new System.Drawing.Size(80, 29);
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
            this.cbFlags800000.Location = new System.Drawing.Point(9, 78);
            this.cbFlags800000.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cbFlags800000.Name = "cbFlags800000";
            this.cbFlags800000.Size = new System.Drawing.Size(116, 29);
            this.cbFlags800000.TabIndex = 0;
            this.cbFlags800000.Text = "0x800000";
            this.cbFlags800000.UseVisualStyleBackColor = true;
            this.cbFlags800000.CheckedChanged += new System.EventHandler(this.cbFlags_CheckedChanged);
            // 
            // cbWireframe
            // 
            this.cbWireframe.AutoSize = true;
            this.cbWireframe.Location = new System.Drawing.Point(11, 18);
            this.cbWireframe.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cbWireframe.Name = "cbWireframe";
            this.cbWireframe.Size = new System.Drawing.Size(120, 29);
            this.cbWireframe.TabIndex = 10;
            this.cbWireframe.Text = "Wireframe";
            this.cbWireframe.ThreeState = true;
            this.cbWireframe.UseVisualStyleBackColor = true;
            this.cbWireframe.CheckStateChanged += new System.EventHandler(this.cbWireframe_CheckStateChanged);
            // 
            // btnExportMesh
            // 
            this.btnExportMesh.Location = new System.Drawing.Point(11, 57);
            this.btnExportMesh.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnExportMesh.Name = "btnExportMesh";
            this.btnExportMesh.Size = new System.Drawing.Size(297, 38);
            this.btnExportMesh.TabIndex = 9;
            this.btnExportMesh.Text = "Export Model";
            this.btnExportMesh.UseVisualStyleBackColor = true;
            this.btnExportMesh.Click += new System.EventHandler(this.btnExportMesh_Click);
            // 
            // tbSpawnsFilter
            // 
            this.tbSpawnsFilter.Location = new System.Drawing.Point(11, 105);
            this.tbSpawnsFilter.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tbSpawnsFilter.Name = "tbSpawnsFilter";
            this.tbSpawnsFilter.PlaceholderText = "Search...";
            this.tbSpawnsFilter.Size = new System.Drawing.Size(295, 31);
            this.tbSpawnsFilter.TabIndex = 8;
            this.tbSpawnsFilter.KeyDown += new System.Windows.Forms.KeyEventHandler(this.tbSpawnsFilter_KeyDown);
            // 
            // treeViewGeom
            // 
            this.treeViewGeom.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.treeViewGeom.Location = new System.Drawing.Point(11, 153);
            this.treeViewGeom.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.treeViewGeom.Name = "treeViewGeom";
            this.treeViewGeom.Size = new System.Drawing.Size(295, 331);
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
            this.glControl.Location = new System.Drawing.Point(319, 18);
            this.glControl.Margin = new System.Windows.Forms.Padding(6, 5, 6, 5);
            this.glControl.Name = "glControl";
            this.glControl.Size = new System.Drawing.Size(927, 780);
            this.glControl.TabIndex = 2;
            this.glControl.VSync = true;
            // 
            // tabPageLog
            // 
            this.tabPageLog.Controls.Add(this.tbLog);
            this.tabPageLog.Location = new System.Drawing.Point(4, 34);
            this.tabPageLog.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tabPageLog.Name = "tabPageLog";
            this.tabPageLog.Size = new System.Drawing.Size(1255, 812);
            this.tabPageLog.TabIndex = 2;
            this.tabPageLog.Text = "Log";
            this.tabPageLog.UseVisualStyleBackColor = true;
            // 
            // tbLog
            // 
            this.tbLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbLog.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.tbLog.Location = new System.Drawing.Point(0, 0);
            this.tbLog.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tbLog.Multiline = true;
            this.tbLog.Name = "tbLog";
            this.tbLog.ReadOnly = true;
            this.tbLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbLog.Size = new System.Drawing.Size(1255, 812);
            this.tbLog.TabIndex = 0;
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnLoadStage,
            this.btnSave,
            this.toolStripDropDownButtonTools});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.toolStrip1.Size = new System.Drawing.Size(1263, 34);
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
            this.btnLoadStage.Size = new System.Drawing.Size(93, 29);
            this.btnLoadStage.Text = "Load";
            // 
            // mGO2StageToolStripMenuItem
            // 
            this.mGO2StageToolStripMenuItem.Name = "mGO2StageToolStripMenuItem";
            this.mGO2StageToolStripMenuItem.Size = new System.Drawing.Size(215, 34);
            this.mGO2StageToolStripMenuItem.Text = "MGO2 Stage";
            this.mGO2StageToolStripMenuItem.Click += new System.EventHandler(this.mGO2StageToolStripMenuItem_Click);
            // 
            // mGS4StageToolStripMenuItem
            // 
            this.mGS4StageToolStripMenuItem.Name = "mGS4StageToolStripMenuItem";
            this.mGS4StageToolStripMenuItem.Size = new System.Drawing.Size(215, 34);
            this.mGS4StageToolStripMenuItem.Text = "MGS4 Stage";
            this.mGS4StageToolStripMenuItem.Click += new System.EventHandler(this.mGS4StageToolStripMenuItem_Click);
            // 
            // mGAStageToolStripMenuItem
            // 
            this.mGAStageToolStripMenuItem.Name = "mGAStageToolStripMenuItem";
            this.mGAStageToolStripMenuItem.Size = new System.Drawing.Size(215, 34);
            this.mGAStageToolStripMenuItem.Text = "MGA Stage";
            this.mGAStageToolStripMenuItem.Click += new System.EventHandler(this.mGAStageToolStripMenuItem_Click);
            // 
            // btnSave
            // 
            this.btnSave.Enabled = false;
            this.btnSave.Image = ((System.Drawing.Image)(resources.GetObject("btnSave.Image")));
            this.btnSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(99, 29);
            this.btnSave.Text = "Save as";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // toolStripDropDownButtonTools
            // 
            this.toolStripDropDownButtonTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.solidEyeToolStripMenuItem,
            this.toolStripMenuItem1,
            this.stageToolStripMenuItem,
            this.stringHashUtilityToolStripMenuItem});
            this.toolStripDropDownButtonTools.Image = ((System.Drawing.Image)(resources.GetObject("toolStripDropDownButtonTools.Image")));
            this.toolStripDropDownButtonTools.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripDropDownButtonTools.Name = "toolStripDropDownButtonTools";
            this.toolStripDropDownButtonTools.Size = new System.Drawing.Size(95, 29);
            this.toolStripDropDownButtonTools.Text = "Tools";
            // 
            // solidEyeToolStripMenuItem
            // 
            this.solidEyeToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.encryptFileToolStripMenuItem,
            this.decryptFileToolStripMenuItem});
            this.solidEyeToolStripMenuItem.Name = "solidEyeToolStripMenuItem";
            this.solidEyeToolStripMenuItem.Size = new System.Drawing.Size(205, 34);
            this.solidEyeToolStripMenuItem.Text = "SolidEye";
            // 
            // encryptFileToolStripMenuItem
            // 
            this.encryptFileToolStripMenuItem.Name = "encryptFileToolStripMenuItem";
            this.encryptFileToolStripMenuItem.Size = new System.Drawing.Size(207, 34);
            this.encryptFileToolStripMenuItem.Text = "Encrypt File";
            this.encryptFileToolStripMenuItem.Click += new System.EventHandler(this.encryptFileToolStripMenuItem_Click);
            // 
            // decryptFileToolStripMenuItem
            // 
            this.decryptFileToolStripMenuItem.Name = "decryptFileToolStripMenuItem";
            this.decryptFileToolStripMenuItem.Size = new System.Drawing.Size(207, 34);
            this.decryptFileToolStripMenuItem.Text = "Decrypt File";
            this.decryptFileToolStripMenuItem.Click += new System.EventHandler(this.decryptFileToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mergeToolStripMenuItem,
            this.mergeReferencesToolStripMenuItem,
            this.mergeVLMToolStripMenuItem,
            this.toolStripMenuItem2});
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(205, 34);
            this.toolStripMenuItem1.Text = "Geom";
            // 
            // mergeToolStripMenuItem
            // 
            this.mergeToolStripMenuItem.Name = "mergeToolStripMenuItem";
            this.mergeToolStripMenuItem.Size = new System.Drawing.Size(254, 34);
            this.mergeToolStripMenuItem.Text = "Merge";
            this.mergeToolStripMenuItem.Click += new System.EventHandler(this.mergeToolStripMenuItem_Click);
            // 
            // mergeReferencesToolStripMenuItem
            // 
            this.mergeReferencesToolStripMenuItem.Name = "mergeReferencesToolStripMenuItem";
            this.mergeReferencesToolStripMenuItem.Size = new System.Drawing.Size(254, 34);
            this.mergeReferencesToolStripMenuItem.Text = "Merge References";
            this.mergeReferencesToolStripMenuItem.Click += new System.EventHandler(this.mergeReferencesToolStripMenuItem_Click);
            // 
            // mergeVLMToolStripMenuItem
            // 
            this.mergeVLMToolStripMenuItem.Name = "mergeVLMToolStripMenuItem";
            this.mergeVLMToolStripMenuItem.Size = new System.Drawing.Size(254, 34);
            this.mergeVLMToolStripMenuItem.Text = "Merge VLM";
            this.mergeVLMToolStripMenuItem.Click += new System.EventHandler(this.mergeVLMToolStripMenuItem_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(254, 34);
            this.toolStripMenuItem2.Text = "Endian Swap";
            this.toolStripMenuItem2.Click += new System.EventHandler(this.toolStripMenuItem2_Click);
            // 
            // stageToolStripMenuItem
            // 
            this.stageToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.generateTexturesToolStripMenuItem});
            this.stageToolStripMenuItem.Name = "stageToolStripMenuItem";
            this.stageToolStripMenuItem.Size = new System.Drawing.Size(205, 34);
            this.stageToolStripMenuItem.Text = "Stage";
            // 
            // generateTexturesToolStripMenuItem
            // 
            this.generateTexturesToolStripMenuItem.Name = "generateTexturesToolStripMenuItem";
            this.generateTexturesToolStripMenuItem.Size = new System.Drawing.Size(252, 34);
            this.generateTexturesToolStripMenuItem.Text = "Generate Textures";
            this.generateTexturesToolStripMenuItem.Click += new System.EventHandler(this.generateTexturesToolStripMenuItem_Click);
            // 
            // stringHashUtilityToolStripMenuItem
            // 
            this.stringHashUtilityToolStripMenuItem.Name = "stringHashUtilityToolStripMenuItem";
            this.stringHashUtilityToolStripMenuItem.Size = new System.Drawing.Size(205, 34);
            this.stringHashUtilityToolStripMenuItem.Text = "String Hash";
            this.stringHashUtilityToolStripMenuItem.Click += new System.EventHandler(this.stringHashUtilityToolStripMenuItem_Click);
            // 
            // labelStatus
            // 
            this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(6, 902);
            this.labelStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(60, 25);
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
            this.labelCamPos.Location = new System.Drawing.Point(320, 902);
            this.labelCamPos.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelCamPos.Name = "labelCamPos";
            this.labelCamPos.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.labelCamPos.Size = new System.Drawing.Size(0, 25);
            this.labelCamPos.TabIndex = 3;
            this.labelCamPos.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // labelVersion
            // 
            this.labelVersion.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(1204, 902);
            this.labelVersion.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(59, 25);
            this.labelVersion.TabIndex = 4;
            this.labelVersion.Text = "v0.0.7";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1263, 935);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.labelCamPos);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.tabControl);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MinimumSize = new System.Drawing.Size(1019, 796);
            this.Name = "MainForm";
            this.Text = "Haven";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.tabControl.ResumeLayout(false);
            this.tabPageFiles.ResumeLayout(false);
            this.tabPageGeom.ResumeLayout(false);
            this.tabPageGeom.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.tabPageLog.ResumeLayout(false);
            this.tabPageLog.PerformLayout();
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
        private Label labelVersion;
        private ToolStripMenuItem mergeReferencesToolStripMenuItem;
        private ToolStripMenuItem stageToolStripMenuItem;
        private ToolStripMenuItem generateTexturesToolStripMenuItem;
        private TabPage tabPageLog;
        private TextBox tbLog;
    }
}