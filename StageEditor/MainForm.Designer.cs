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
            cbGrid = new CheckBox();
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
            tabControl.Location = new Point(0, 60);
            tabControl.Margin = new Padding(6);
            tabControl.Name = "tabControl";
            tabControl.SelectedIndex = 0;
            tabControl.Size = new Size(1642, 1088);
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.TabIndex = 0;
            // 
            // tabPageFiles
            // 
            tabPageFiles.Controls.Add(treeViewFiles);
            tabPageFiles.Location = new Point(8, 46);
            tabPageFiles.Margin = new Padding(6);
            tabPageFiles.Name = "tabPageFiles";
            tabPageFiles.Padding = new Padding(6);
            tabPageFiles.Size = new Size(1626, 1034);
            tabPageFiles.TabIndex = 0;
            tabPageFiles.Text = "Main";
            tabPageFiles.UseVisualStyleBackColor = true;
            // 
            // treeViewFiles
            // 
            treeViewFiles.BorderStyle = BorderStyle.FixedSingle;
            treeViewFiles.Dock = DockStyle.Fill;
            treeViewFiles.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            treeViewFiles.Location = new Point(6, 6);
            treeViewFiles.Margin = new Padding(6);
            treeViewFiles.Name = "treeViewFiles";
            treeViewFiles.Size = new Size(1614, 1022);
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
            tabPageGeom.Controls.Add(cbGrid);
            tabPageGeom.Controls.Add(cbWireframe);
            tabPageGeom.Controls.Add(btnExportMesh);
            tabPageGeom.Controls.Add(tbSpawnsFilter);
            tabPageGeom.Controls.Add(treeViewGeom);
            tabPageGeom.Controls.Add(glControl);
            tabPageGeom.Location = new Point(8, 46);
            tabPageGeom.Margin = new Padding(6);
            tabPageGeom.Name = "tabPageGeom";
            tabPageGeom.Size = new Size(1626, 1034);
            tabPageGeom.TabIndex = 1;
            tabPageGeom.Text = "Geom";
            tabPageGeom.UseVisualStyleBackColor = true;
            // 
            // cbGrid
            // 
            cbGrid.AutoSize = true;
            cbGrid.Checked = true;
            cbGrid.CheckState = CheckState.Checked;
            cbGrid.Location = new Point(184, 23);
            cbGrid.Margin = new Padding(6);
            cbGrid.Name = "cbGrid";
            cbGrid.Size = new Size(90, 36);
            cbGrid.TabIndex = 11;
            cbGrid.Text = "Grid";
            cbGrid.ThreeState = true;
            cbGrid.UseVisualStyleBackColor = true;
            cbGrid.CheckedChanged += cbGrid_CheckedChanged;
            // 
            // cbWireframe
            // 
            cbWireframe.AutoSize = true;
            cbWireframe.Location = new Point(15, 23);
            cbWireframe.Margin = new Padding(6);
            cbWireframe.Name = "cbWireframe";
            cbWireframe.Size = new Size(157, 36);
            cbWireframe.TabIndex = 10;
            cbWireframe.Text = "Wireframe";
            cbWireframe.ThreeState = true;
            cbWireframe.UseVisualStyleBackColor = true;
            cbWireframe.CheckStateChanged += cbWireframe_CheckStateChanged;
            // 
            // btnExportMesh
            // 
            btnExportMesh.Location = new Point(15, 73);
            btnExportMesh.Margin = new Padding(6);
            btnExportMesh.Name = "btnExportMesh";
            btnExportMesh.Size = new Size(386, 49);
            btnExportMesh.TabIndex = 9;
            btnExportMesh.Text = "Export Model";
            btnExportMesh.UseVisualStyleBackColor = true;
            btnExportMesh.Click += btnExportMesh_Click;
            // 
            // tbSpawnsFilter
            // 
            tbSpawnsFilter.Location = new Point(15, 134);
            tbSpawnsFilter.Margin = new Padding(6);
            tbSpawnsFilter.Name = "tbSpawnsFilter";
            tbSpawnsFilter.PlaceholderText = "Search...";
            tbSpawnsFilter.Size = new Size(383, 39);
            tbSpawnsFilter.TabIndex = 8;
            tbSpawnsFilter.KeyDown += tbSpawnsFilter_KeyDown;
            // 
            // treeViewGeom
            // 
            treeViewGeom.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            treeViewGeom.Location = new Point(15, 196);
            treeViewGeom.Margin = new Padding(6);
            treeViewGeom.Name = "treeViewGeom";
            treeViewGeom.Size = new Size(383, 825);
            treeViewGeom.TabIndex = 6;
            treeViewGeom.BeforeCheck += treeViewGeom_BeforeCheck;
            treeViewGeom.AfterCheck += treeViewGeom_AfterCheck;
            treeViewGeom.AfterSelect += treeViewGeom_AfterSelect;
            treeViewGeom.NodeMouseDoubleClick += treeViewGeom_NodeMouseDoubleClick;
            treeViewGeom.MouseUp += treeViewGeom_MouseUp;
            // 
            // glControl
            // 
            glControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            glControl.BackColor = Color.Black;
            glControl.Location = new Point(414, 23);
            glControl.Margin = new Padding(7, 6, 7, 6);
            glControl.Name = "glControl";
            glControl.Size = new Size(1205, 998);
            glControl.TabIndex = 2;
            glControl.VSync = true;
            // 
            // tabPageLog
            // 
            tabPageLog.Controls.Add(tbLog);
            tabPageLog.Location = new Point(8, 46);
            tabPageLog.Margin = new Padding(6);
            tabPageLog.Name = "tabPageLog";
            tabPageLog.Size = new Size(1626, 1034);
            tabPageLog.TabIndex = 2;
            tabPageLog.Text = "Log";
            tabPageLog.UseVisualStyleBackColor = true;
            // 
            // tbLog
            // 
            tbLog.Dock = DockStyle.Fill;
            tbLog.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            tbLog.Location = new Point(0, 0);
            tbLog.Margin = new Padding(6);
            tbLog.Multiline = true;
            tbLog.Name = "tbLog";
            tbLog.ReadOnly = true;
            tbLog.ScrollBars = ScrollBars.Both;
            tbLog.Size = new Size(1626, 1034);
            tbLog.TabIndex = 0;
            // 
            // toolStrip1
            // 
            toolStrip1.ImageScalingSize = new Size(32, 32);
            toolStrip1.Items.AddRange(new ToolStripItem[] { btnLoadStage, btnSave, toolStripDropDownButtonTools });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Padding = new Padding(0, 0, 4, 0);
            toolStrip1.Size = new Size(1642, 42);
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
            btnLoadStage.Size = new Size(119, 36);
            btnLoadStage.Text = "Load";
            // 
            // mGO2StageToolStripMenuItem
            // 
            mGO2StageToolStripMenuItem.Name = "mGO2StageToolStripMenuItem";
            mGO2StageToolStripMenuItem.Size = new Size(282, 44);
            mGO2StageToolStripMenuItem.Text = "MGO2 Stage";
            mGO2StageToolStripMenuItem.Click += mGO2StageToolStripMenuItem_Click;
            // 
            // mGS4StageToolStripMenuItem
            // 
            mGS4StageToolStripMenuItem.Name = "mGS4StageToolStripMenuItem";
            mGS4StageToolStripMenuItem.Size = new Size(282, 44);
            mGS4StageToolStripMenuItem.Text = "MGS4 Stage";
            mGS4StageToolStripMenuItem.Click += mGS4StageToolStripMenuItem_Click;
            // 
            // mGAStageToolStripMenuItem
            // 
            mGAStageToolStripMenuItem.Name = "mGAStageToolStripMenuItem";
            mGAStageToolStripMenuItem.Size = new Size(282, 44);
            mGAStageToolStripMenuItem.Text = "MGA Stage";
            mGAStageToolStripMenuItem.Click += mGAStageToolStripMenuItem_Click;
            // 
            // btnSave
            // 
            btnSave.Enabled = false;
            btnSave.Image = (Image)resources.GetObject("btnSave.Image");
            btnSave.ImageTransparentColor = Color.Magenta;
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(129, 36);
            btnSave.Text = "Save as";
            btnSave.Click += btnSave_Click;
            // 
            // toolStripDropDownButtonTools
            // 
            toolStripDropDownButtonTools.DropDownItems.AddRange(new ToolStripItem[] { solidEyeToolStripMenuItem, toolStripMenuItem1, stringHashUtilityToolStripMenuItem });
            toolStripDropDownButtonTools.Image = (Image)resources.GetObject("toolStripDropDownButtonTools.Image");
            toolStripDropDownButtonTools.ImageTransparentColor = Color.Magenta;
            toolStripDropDownButtonTools.Name = "toolStripDropDownButtonTools";
            toolStripDropDownButtonTools.Size = new Size(123, 36);
            toolStripDropDownButtonTools.Text = "Tools";
            // 
            // solidEyeToolStripMenuItem
            // 
            solidEyeToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { encryptFileToolStripMenuItem, decryptFileToolStripMenuItem });
            solidEyeToolStripMenuItem.Name = "solidEyeToolStripMenuItem";
            solidEyeToolStripMenuItem.Size = new Size(269, 44);
            solidEyeToolStripMenuItem.Text = "SolidEye";
            // 
            // encryptFileToolStripMenuItem
            // 
            encryptFileToolStripMenuItem.Name = "encryptFileToolStripMenuItem";
            encryptFileToolStripMenuItem.Size = new Size(274, 44);
            encryptFileToolStripMenuItem.Text = "Encrypt File";
            encryptFileToolStripMenuItem.Click += encryptFileToolStripMenuItem_Click;
            // 
            // decryptFileToolStripMenuItem
            // 
            decryptFileToolStripMenuItem.Name = "decryptFileToolStripMenuItem";
            decryptFileToolStripMenuItem.Size = new Size(274, 44);
            decryptFileToolStripMenuItem.Text = "Decrypt File";
            decryptFileToolStripMenuItem.Click += decryptFileToolStripMenuItem_Click;
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.DropDownItems.AddRange(new ToolStripItem[] { mergeToolStripMenuItem, mergeReferencesToolStripMenuItem, mergeVLMToolStripMenuItem, toolStripMenuItem2 });
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(269, 44);
            toolStripMenuItem1.Text = "Geom";
            // 
            // mergeToolStripMenuItem
            // 
            mergeToolStripMenuItem.Name = "mergeToolStripMenuItem";
            mergeToolStripMenuItem.Size = new Size(340, 44);
            mergeToolStripMenuItem.Text = "Merge";
            mergeToolStripMenuItem.Click += mergeToolStripMenuItem_Click;
            // 
            // mergeReferencesToolStripMenuItem
            // 
            mergeReferencesToolStripMenuItem.Name = "mergeReferencesToolStripMenuItem";
            mergeReferencesToolStripMenuItem.Size = new Size(340, 44);
            mergeReferencesToolStripMenuItem.Text = "Merge References";
            mergeReferencesToolStripMenuItem.Click += mergeReferencesToolStripMenuItem_Click;
            // 
            // mergeVLMToolStripMenuItem
            // 
            mergeVLMToolStripMenuItem.Name = "mergeVLMToolStripMenuItem";
            mergeVLMToolStripMenuItem.Size = new Size(340, 44);
            mergeVLMToolStripMenuItem.Text = "Merge VLM";
            mergeVLMToolStripMenuItem.Click += mergeVLMToolStripMenuItem_Click;
            // 
            // toolStripMenuItem2
            // 
            toolStripMenuItem2.Name = "toolStripMenuItem2";
            toolStripMenuItem2.Size = new Size(340, 44);
            toolStripMenuItem2.Text = "Endian Swap";
            toolStripMenuItem2.Click += toolStripMenuItem2_Click;
            // 
            // stringHashUtilityToolStripMenuItem
            // 
            stringHashUtilityToolStripMenuItem.Name = "stringHashUtilityToolStripMenuItem";
            stringHashUtilityToolStripMenuItem.Size = new Size(269, 44);
            stringHashUtilityToolStripMenuItem.Text = "String Hash";
            stringHashUtilityToolStripMenuItem.Click += stringHashUtilityToolStripMenuItem_Click;
            // 
            // labelStatus
            // 
            labelStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            labelStatus.AutoSize = true;
            labelStatus.Location = new Point(7, 1154);
            labelStatus.Margin = new Padding(6, 0, 6, 0);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(78, 32);
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
            labelCamPos.Location = new Point(416, 1154);
            labelCamPos.Margin = new Padding(6, 0, 6, 0);
            labelCamPos.Name = "labelCamPos";
            labelCamPos.RightToLeft = RightToLeft.No;
            labelCamPos.Size = new Size(0, 32);
            labelCamPos.TabIndex = 3;
            labelCamPos.TextAlign = ContentAlignment.BottomRight;
            // 
            // labelVersion
            // 
            labelVersion.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            labelVersion.AutoSize = true;
            labelVersion.Location = new Point(1566, 1154);
            labelVersion.Margin = new Padding(6, 0, 6, 0);
            labelVersion.Name = "labelVersion";
            labelVersion.Size = new Size(75, 32);
            labelVersion.TabIndex = 4;
            labelVersion.Text = "v0.1.0";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1642, 1197);
            Controls.Add(labelVersion);
            Controls.Add(labelCamPos);
            Controls.Add(labelStatus);
            Controls.Add(toolStrip1);
            Controls.Add(tabControl);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(6);
            MinimumSize = new Size(1315, 986);
            Name = "MainForm";
            Text = "Haven";
            Load += MainForm_Load;
            tabControl.ResumeLayout(false);
            tabPageFiles.ResumeLayout(false);
            tabPageGeom.ResumeLayout(false);
            tabPageGeom.PerformLayout();
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
        private Label labelVersion;
        private ToolStripMenuItem mergeReferencesToolStripMenuItem;
        private TabPage tabPageLog;
        private TextBox tbLog;
        private CheckBox cbGrid;
    }
}