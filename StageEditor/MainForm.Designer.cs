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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            tabControl = new TabControl();
            tabPageFiles = new TabPage();
            treeViewFiles = new TreeView();
            imageListFileTypes = new ImageList(components);
            tabPageGeom = new TabPage();
            groupBox1 = new GroupBox();
            cbFlagsAll = new CheckBox();
            cbFlags1000000 = new CheckBox();
            cbFlagsWater = new CheckBox();
            cbFlagsRail = new CheckBox();
            cbFlagsStairs = new CheckBox();
            cbFlags800000 = new CheckBox();
            cbWireframe = new CheckBox();
            btnExportMesh = new Button();
            tbSpawnsFilter = new TextBox();
            treeViewGeom = new TreeView();
            glControl = new OpenTK.GLControl();
            tabPageLog = new TabPage();
            tbLog = new TextBox();
            toolStrip1 = new ToolStrip();
            btnLoadStage = new ToolStripDropDownButton();
            mGO2StageToolStripMenuItem = new ToolStripMenuItem();
            mGS4StageToolStripMenuItem = new ToolStripMenuItem();
            mGAStageToolStripMenuItem = new ToolStripMenuItem();
            btnSave = new ToolStripButton();
            toolStripDropDownButtonTools = new ToolStripDropDownButton();
            solidEyeToolStripMenuItem = new ToolStripMenuItem();
            encryptFileToolStripMenuItem = new ToolStripMenuItem();
            decryptFileToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem1 = new ToolStripMenuItem();
            mergeToolStripMenuItem = new ToolStripMenuItem();
            mergeReferencesToolStripMenuItem = new ToolStripMenuItem();
            mergeVLMToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem2 = new ToolStripMenuItem();
            stringHashUtilityToolStripMenuItem = new ToolStripMenuItem();
            labelStatus = new Label();
            timerRefresh = new System.Windows.Forms.Timer(components);
            labelCamPos = new Label();
            labelVersion = new Label();
            tabControl.SuspendLayout();
            tabPageFiles.SuspendLayout();
            tabPageGeom.SuspendLayout();
            groupBox1.SuspendLayout();
            tabPageLog.SuspendLayout();
            toolStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl
            // 
            tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl.Controls.Add(tabPageFiles);
            tabControl.Controls.Add(tabPageGeom);
            tabControl.Controls.Add(tabPageLog);
            tabControl.Location = new Point(0, 28);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(884, 510);
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.TabIndex = 0;
            // 
            // tabPageFiles
            // 
            tabPageFiles.Controls.Add(treeViewFiles);
            tabPageFiles.Location = new Point(4, 24);
            tabPageFiles.Name = "tabPageFiles";
            tabPageFiles.Padding = new Padding(3);
            tabPageFiles.Size = new Size(876, 482);
            tabPageFiles.TabIndex = 0;
            tabPageFiles.Text = "Main";
            tabPageFiles.UseVisualStyleBackColor = true;
            // 
            // treeViewFiles
            // 
            treeViewFiles.BorderStyle = BorderStyle.FixedSingle;
            treeViewFiles.Dock = DockStyle.Fill;
            treeViewFiles.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            treeViewFiles.Location = new Point(3, 3);
            treeViewFiles.Name = "treeViewFiles";
            treeViewFiles.Size = new Size(870, 476);
            treeViewFiles.StateImageList = imageListFileTypes;
            treeViewFiles.TabIndex = 0;
            treeViewFiles.MouseUp += treeViewFiles_MouseUp;
            // 
            // imageListFileTypes
            // 
            imageListFileTypes.ColorDepth = ColorDepth.Depth8Bit;
            imageListFileTypes.ImageStream = (ImageListStreamer)resources.GetObject("imageListFileTypes.ImageStream");
            imageListFileTypes.TransparentColor = Color.Transparent;
            imageListFileTypes.Images.SetKeyName(0, "File");
            imageListFileTypes.Images.SetKeyName(1, "Folder");
            imageListFileTypes.Images.SetKeyName(2, "Mesh");
            // 
            // tabPageGeom
            // 
            tabPageGeom.Controls.Add(groupBox1);
            tabPageGeom.Controls.Add(cbWireframe);
            tabPageGeom.Controls.Add(btnExportMesh);
            tabPageGeom.Controls.Add(tbSpawnsFilter);
            tabPageGeom.Controls.Add(treeViewGeom);
            tabPageGeom.Controls.Add(glControl);
            tabPageGeom.Location = new Point(4, 24);
            tabPageGeom.Name = "tabPageGeom";
            tabPageGeom.Size = new Size(876, 482);
            tabPageGeom.TabIndex = 1;
            tabPageGeom.Text = "Geom";
            tabPageGeom.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            groupBox1.BackColor = Color.Transparent;
            groupBox1.Controls.Add(cbFlagsAll);
            groupBox1.Controls.Add(cbFlags1000000);
            groupBox1.Controls.Add(cbFlagsWater);
            groupBox1.Controls.Add(cbFlagsRail);
            groupBox1.Controls.Add(cbFlagsStairs);
            groupBox1.Controls.Add(cbFlags800000);
            groupBox1.Location = new Point(8, 298);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(208, 181);
            groupBox1.TabIndex = 11;
            groupBox1.TabStop = false;
            groupBox1.Text = "Flags";
            // 
            // cbFlagsAll
            // 
            cbFlagsAll.AutoSize = true;
            cbFlagsAll.BackColor = Color.Transparent;
            cbFlagsAll.Location = new Point(6, 22);
            cbFlagsAll.Name = "cbFlagsAll";
            cbFlagsAll.Size = new Size(40, 19);
            cbFlagsAll.TabIndex = 6;
            cbFlagsAll.Text = "All";
            cbFlagsAll.UseVisualStyleBackColor = false;
            cbFlagsAll.CheckedChanged += cbFlags_CheckedChanged;
            // 
            // cbFlags1000000
            // 
            cbFlags1000000.AutoSize = true;
            cbFlags1000000.BackColor = Color.Transparent;
            cbFlags1000000.Checked = true;
            cbFlags1000000.CheckState = CheckState.Checked;
            cbFlags1000000.ForeColor = Color.Khaki;
            cbFlags1000000.Location = new Point(6, 147);
            cbFlags1000000.Name = "cbFlags1000000";
            cbFlags1000000.Size = new Size(80, 19);
            cbFlags1000000.TabIndex = 5;
            cbFlags1000000.Text = "0x1000000";
            cbFlags1000000.UseVisualStyleBackColor = false;
            cbFlags1000000.CheckedChanged += cbFlags_CheckedChanged;
            // 
            // cbFlagsWater
            // 
            cbFlagsWater.AutoSize = true;
            cbFlagsWater.BackColor = Color.Transparent;
            cbFlagsWater.Checked = true;
            cbFlagsWater.CheckState = CheckState.Checked;
            cbFlagsWater.ForeColor = Color.Teal;
            cbFlagsWater.Location = new Point(6, 122);
            cbFlagsWater.Name = "cbFlagsWater";
            cbFlagsWater.Size = new Size(57, 19);
            cbFlagsWater.TabIndex = 3;
            cbFlagsWater.Text = "Water";
            cbFlagsWater.UseVisualStyleBackColor = false;
            cbFlagsWater.CheckedChanged += cbFlags_CheckedChanged;
            // 
            // cbFlagsRail
            // 
            cbFlagsRail.AutoSize = true;
            cbFlagsRail.BackColor = Color.Transparent;
            cbFlagsRail.Checked = true;
            cbFlagsRail.CheckState = CheckState.Checked;
            cbFlagsRail.ForeColor = Color.Purple;
            cbFlagsRail.Location = new Point(6, 97);
            cbFlagsRail.Name = "cbFlagsRail";
            cbFlagsRail.Size = new Size(50, 19);
            cbFlagsRail.TabIndex = 2;
            cbFlagsRail.Text = "Rails";
            cbFlagsRail.UseVisualStyleBackColor = false;
            cbFlagsRail.CheckedChanged += cbFlags_CheckedChanged;
            // 
            // cbFlagsStairs
            // 
            cbFlagsStairs.AutoSize = true;
            cbFlagsStairs.BackColor = Color.Transparent;
            cbFlagsStairs.Checked = true;
            cbFlagsStairs.CheckState = CheckState.Checked;
            cbFlagsStairs.ForeColor = Color.FromArgb(0, 192, 0);
            cbFlagsStairs.Location = new Point(6, 72);
            cbFlagsStairs.Name = "cbFlagsStairs";
            cbFlagsStairs.Size = new Size(54, 19);
            cbFlagsStairs.TabIndex = 1;
            cbFlagsStairs.Text = "Stairs";
            cbFlagsStairs.UseVisualStyleBackColor = false;
            cbFlagsStairs.CheckedChanged += cbFlags_CheckedChanged;
            // 
            // cbFlags800000
            // 
            cbFlags800000.AutoSize = true;
            cbFlags800000.Checked = true;
            cbFlags800000.CheckState = CheckState.Checked;
            cbFlags800000.Location = new Point(6, 47);
            cbFlags800000.Name = "cbFlags800000";
            cbFlags800000.Size = new Size(74, 19);
            cbFlags800000.TabIndex = 0;
            cbFlags800000.Text = "0x800000";
            cbFlags800000.UseVisualStyleBackColor = true;
            cbFlags800000.CheckedChanged += cbFlags_CheckedChanged;
            // 
            // cbWireframe
            // 
            cbWireframe.AutoSize = true;
            cbWireframe.Location = new Point(8, 11);
            cbWireframe.Name = "cbWireframe";
            cbWireframe.Size = new Size(81, 19);
            cbWireframe.TabIndex = 10;
            cbWireframe.Text = "Wireframe";
            cbWireframe.ThreeState = true;
            cbWireframe.UseVisualStyleBackColor = true;
            cbWireframe.CheckStateChanged += cbWireframe_CheckStateChanged;
            // 
            // btnExportMesh
            // 
            btnExportMesh.Location = new Point(8, 34);
            btnExportMesh.Name = "btnExportMesh";
            btnExportMesh.Size = new Size(208, 23);
            btnExportMesh.TabIndex = 9;
            btnExportMesh.Text = "Export Model";
            btnExportMesh.UseVisualStyleBackColor = true;
            btnExportMesh.Click += btnExportMesh_Click;
            // 
            // tbSpawnsFilter
            // 
            tbSpawnsFilter.Location = new Point(8, 63);
            tbSpawnsFilter.Name = "tbSpawnsFilter";
            tbSpawnsFilter.PlaceholderText = "Search...";
            tbSpawnsFilter.Size = new Size(208, 23);
            tbSpawnsFilter.TabIndex = 8;
            tbSpawnsFilter.KeyDown += tbSpawnsFilter_KeyDown;
            // 
            // treeViewGeom
            // 
            treeViewGeom.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            treeViewGeom.Location = new Point(8, 92);
            treeViewGeom.Name = "treeViewGeom";
            treeViewGeom.Size = new Size(208, 200);
            treeViewGeom.TabIndex = 6;
            treeViewGeom.AfterCheck += treeViewGeom_AfterCheck;
            treeViewGeom.AfterSelect += treeViewGeom_AfterSelect;
            treeViewGeom.NodeMouseDoubleClick += treeViewGeom_NodeMouseDoubleClick;
            treeViewGeom.MouseUp += treeViewGeom_MouseUp;
            // 
            // glControl
            // 
            glControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            glControl.BackColor = Color.Black;
            glControl.Location = new Point(223, 11);
            glControl.Margin = new Padding(4, 3, 4, 3);
            glControl.Name = "glControl";
            glControl.Size = new Size(649, 468);
            glControl.TabIndex = 2;
            glControl.VSync = true;
            // 
            // tabPageLog
            // 
            tabPageLog.Controls.Add(tbLog);
            tabPageLog.Location = new Point(4, 24);
            tabPageLog.Name = "tabPageLog";
            tabPageLog.Size = new Size(876, 482);
            tabPageLog.TabIndex = 2;
            tabPageLog.Text = "Log";
            tabPageLog.UseVisualStyleBackColor = true;
            // 
            // tbLog
            // 
            tbLog.Dock = DockStyle.Fill;
            tbLog.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            tbLog.Location = new Point(0, 0);
            tbLog.Multiline = true;
            tbLog.Name = "tbLog";
            tbLog.ReadOnly = true;
            tbLog.ScrollBars = ScrollBars.Both;
            tbLog.Size = new Size(876, 482);
            tbLog.TabIndex = 0;
            // 
            // toolStrip1
            // 
            toolStrip1.Items.AddRange(new ToolStripItem[] { btnLoadStage, btnSave, toolStripDropDownButtonTools });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(884, 25);
            toolStrip1.TabIndex = 1;
            toolStrip1.Text = "toolStrip1";
            // 
            // btnLoadStage
            // 
            btnLoadStage.DropDownItems.AddRange(new ToolStripItem[] { mGO2StageToolStripMenuItem, mGS4StageToolStripMenuItem, mGAStageToolStripMenuItem });
            btnLoadStage.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            btnLoadStage.Image = (Image)resources.GetObject("btnLoadStage.Image");
            btnLoadStage.ImageTransparentColor = Color.Magenta;
            btnLoadStage.Name = "btnLoadStage";
            btnLoadStage.Size = new Size(62, 22);
            btnLoadStage.Text = "Load";
            // 
            // mGO2StageToolStripMenuItem
            // 
            mGO2StageToolStripMenuItem.Name = "mGO2StageToolStripMenuItem";
            mGO2StageToolStripMenuItem.Size = new Size(140, 22);
            mGO2StageToolStripMenuItem.Text = "MGO2 Stage";
            mGO2StageToolStripMenuItem.Click += mGO2StageToolStripMenuItem_Click;
            // 
            // mGS4StageToolStripMenuItem
            // 
            mGS4StageToolStripMenuItem.Name = "mGS4StageToolStripMenuItem";
            mGS4StageToolStripMenuItem.Size = new Size(140, 22);
            mGS4StageToolStripMenuItem.Text = "MGS4 Stage";
            mGS4StageToolStripMenuItem.Click += mGS4StageToolStripMenuItem_Click;
            // 
            // mGAStageToolStripMenuItem
            // 
            mGAStageToolStripMenuItem.Name = "mGAStageToolStripMenuItem";
            mGAStageToolStripMenuItem.Size = new Size(140, 22);
            mGAStageToolStripMenuItem.Text = "MGA Stage";
            mGAStageToolStripMenuItem.Click += mGAStageToolStripMenuItem_Click;
            // 
            // btnSave
            // 
            btnSave.Enabled = false;
            btnSave.Image = (Image)resources.GetObject("btnSave.Image");
            btnSave.ImageTransparentColor = Color.Magenta;
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(65, 22);
            btnSave.Text = "Save as";
            btnSave.Click += btnSave_Click;
            // 
            // toolStripDropDownButtonTools
            // 
            toolStripDropDownButtonTools.DropDownItems.AddRange(new ToolStripItem[] { solidEyeToolStripMenuItem, toolStripMenuItem1, stringHashUtilityToolStripMenuItem });
            toolStripDropDownButtonTools.Image = (Image)resources.GetObject("toolStripDropDownButtonTools.Image");
            toolStripDropDownButtonTools.ImageTransparentColor = Color.Magenta;
            toolStripDropDownButtonTools.Name = "toolStripDropDownButtonTools";
            toolStripDropDownButtonTools.Size = new Size(63, 22);
            toolStripDropDownButtonTools.Text = "Tools";
            // 
            // solidEyeToolStripMenuItem
            // 
            solidEyeToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { encryptFileToolStripMenuItem, decryptFileToolStripMenuItem });
            solidEyeToolStripMenuItem.Name = "solidEyeToolStripMenuItem";
            solidEyeToolStripMenuItem.Size = new Size(180, 22);
            solidEyeToolStripMenuItem.Text = "SolidEye";
            // 
            // encryptFileToolStripMenuItem
            // 
            encryptFileToolStripMenuItem.Name = "encryptFileToolStripMenuItem";
            encryptFileToolStripMenuItem.Size = new Size(136, 22);
            encryptFileToolStripMenuItem.Text = "Encrypt File";
            encryptFileToolStripMenuItem.Click += encryptFileToolStripMenuItem_Click;
            // 
            // decryptFileToolStripMenuItem
            // 
            decryptFileToolStripMenuItem.Name = "decryptFileToolStripMenuItem";
            decryptFileToolStripMenuItem.Size = new Size(136, 22);
            decryptFileToolStripMenuItem.Text = "Decrypt File";
            decryptFileToolStripMenuItem.Click += decryptFileToolStripMenuItem_Click;
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.DropDownItems.AddRange(new ToolStripItem[] { mergeToolStripMenuItem, mergeReferencesToolStripMenuItem, mergeVLMToolStripMenuItem, toolStripMenuItem2 });
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(180, 22);
            toolStripMenuItem1.Text = "Geom";
            // 
            // mergeToolStripMenuItem
            // 
            mergeToolStripMenuItem.Name = "mergeToolStripMenuItem";
            mergeToolStripMenuItem.Size = new Size(168, 22);
            mergeToolStripMenuItem.Text = "Merge";
            mergeToolStripMenuItem.Click += mergeToolStripMenuItem_Click;
            // 
            // mergeReferencesToolStripMenuItem
            // 
            mergeReferencesToolStripMenuItem.Name = "mergeReferencesToolStripMenuItem";
            mergeReferencesToolStripMenuItem.Size = new Size(168, 22);
            mergeReferencesToolStripMenuItem.Text = "Merge References";
            mergeReferencesToolStripMenuItem.Click += mergeReferencesToolStripMenuItem_Click;
            // 
            // mergeVLMToolStripMenuItem
            // 
            mergeVLMToolStripMenuItem.Name = "mergeVLMToolStripMenuItem";
            mergeVLMToolStripMenuItem.Size = new Size(168, 22);
            mergeVLMToolStripMenuItem.Text = "Merge VLM";
            mergeVLMToolStripMenuItem.Click += mergeVLMToolStripMenuItem_Click;
            // 
            // toolStripMenuItem2
            // 
            toolStripMenuItem2.Name = "toolStripMenuItem2";
            toolStripMenuItem2.Size = new Size(168, 22);
            toolStripMenuItem2.Text = "Endian Swap";
            toolStripMenuItem2.Click += toolStripMenuItem2_Click;
            // 
            // stringHashUtilityToolStripMenuItem
            // 
            stringHashUtilityToolStripMenuItem.Name = "stringHashUtilityToolStripMenuItem";
            stringHashUtilityToolStripMenuItem.Size = new Size(180, 22);
            stringHashUtilityToolStripMenuItem.Text = "String Hash";
            stringHashUtilityToolStripMenuItem.Click += stringHashUtilityToolStripMenuItem_Click;
            // 
            // labelStatus
            // 
            labelStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            labelStatus.AutoSize = true;
            labelStatus.Location = new Point(4, 541);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(39, 15);
            labelStatus.TabIndex = 2;
            labelStatus.Text = "Ready";
            // 
            // timerRefresh
            // 
            timerRefresh.Enabled = true;
            timerRefresh.Tick += timerRefresh_Tick;
            // 
            // labelCamPos
            // 
            labelCamPos.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            labelCamPos.AutoSize = true;
            labelCamPos.Location = new Point(224, 541);
            labelCamPos.Name = "labelCamPos";
            labelCamPos.RightToLeft = RightToLeft.No;
            labelCamPos.Size = new Size(0, 15);
            labelCamPos.TabIndex = 3;
            labelCamPos.TextAlign = ContentAlignment.BottomRight;
            // 
            // labelVersion
            // 
            labelVersion.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            labelVersion.AutoSize = true;
            labelVersion.Location = new Point(843, 541);
            labelVersion.Name = "labelVersion";
            labelVersion.Size = new Size(37, 15);
            labelVersion.TabIndex = 4;
            labelVersion.Text = "v0.0.7";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(884, 561);
            Controls.Add(labelVersion);
            Controls.Add(labelCamPos);
            Controls.Add(labelStatus);
            Controls.Add(toolStrip1);
            Controls.Add(tabControl);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(720, 500);
            Name = "MainForm";
            Text = "Haven";
            Load += MainForm_Load;
            tabControl.ResumeLayout(false);
            tabPageFiles.ResumeLayout(false);
            tabPageGeom.ResumeLayout(false);
            tabPageGeom.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            tabPageLog.ResumeLayout(false);
            tabPageLog.PerformLayout();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
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
        private TabPage tabPageLog;
        private TextBox tbLog;
    }
}