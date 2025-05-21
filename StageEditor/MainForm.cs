using System;
using System.ComponentModel;
using System.Diagnostics;
using OpenTK;
using Haven.Parser;
using Haven.Render;
using Haven.Properties;
using Haven.Forms;
using Haven.Parser.Geom;
using Joveler.Compression.ZLib;
using OpenTK.Graphics.OpenGL;
using System.IO;
using Serilog.Events;
using Serilog;

namespace Haven
{
    public partial class MainForm : Form
    {
        public Stage? CurrentStage;
        public GeomFile? Geom;

        public Scene Scene;

        public List<Mesh> MeshGroups = new List<Mesh>();
        public List<Mesh> MeshRefs = new List<Mesh>();
        public List<Mesh> MeshProps = new List<Mesh>();
        public List<Mesh> MeshBoundaries = new List<Mesh>();

        public Dictionary<GeomProp, Mesh> GeomPropMeshLookup = new Dictionary<GeomProp, Mesh>();
        public Dictionary<Mesh, GeomProp> MeshGeomPropLookup = new Dictionary<Mesh, GeomProp>();
        public Dictionary<TreeNode, StageFile> StageFileLookup = new Dictionary<TreeNode, StageFile>();
        public Dictionary<TreeNode, GeomProp> GeomPropLookup = new Dictionary<TreeNode, GeomProp>();
        public Dictionary<string, TreeNode> TreeNodeLookup = new();

        public ContextMenuStrip ContextMenuFiles = new ContextMenuStrip();
        public ToolStripMenuItem MenuItemFilesOpen = new ToolStripMenuItem();
        public ToolStripMenuItem MenuItemFilesEdit = new ToolStripMenuItem();
        public ToolStripMenuItem MenuItemFilesRebuild = new ToolStripMenuItem();

        public ContextMenuStrip ContextMenuGeomProp = new ContextMenuStrip();
        public ToolStripMenuItem MenuItemGeomPropEdit = new ToolStripMenuItem();

        public ContextMenuStrip ContextMenuGeomMesh = new ContextMenuStrip();
        public ToolStripMenuItem MenuItemGeomMeshEdit = new ToolStripMenuItem();

        public TreeNode TreeNodeGeomMeshes;
        public TreeNode TreeNodeGeomRefs;
        public TreeNode TreeNodeGeomProps;
        public TreeNode TreeNodeGeomBoundaries;

        public static string LoggerTemplate = "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}] {Message:lj}{NewLine}{Exception}";
        public static CustomLoggerSink LoggerSink = new CustomLoggerSink(null, LoggerTemplate);

        private bool _suspendAfterCheck = false;

        public MainForm()
        {
            InitializeComponent();

            TreeNodeGeomMeshes = treeViewGeom.Nodes.Add("Meshes");
            TreeNodeGeomProps = treeViewGeom.Nodes.Add("Props");
            TreeNodeGeomRefs = treeViewGeom.Nodes.Add("References");
            TreeNodeGeomBoundaries = treeViewGeom.Nodes.Add("Boundaries");

            Scene = new Scene(glControl);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Verbose)
                .WriteTo.File("log.txt", outputTemplate: LoggerTemplate)
                .WriteTo.Sink(LoggerSink)
                .CreateLogger();

            LoggerSink.NewLogHandler += LoggerSink_NewLogHandler;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            tabPageGeom.Show();
            SetEnabled(false);
            DictionaryFile.Load("bin/dictionary.txt", "bin/dictionary-aliases.txt");
            ZLibInit.GlobalInit(Path.GetFullPath("zlibwapi.dll"));
            SetupContextMenus();

            Scene.MeshSelected += mesh =>
            {
                if (mesh != null && TreeNodeLookup.TryGetValue(mesh.ID, out var node))
                {
                    treeViewGeom.SelectedNode = node;
                    treeViewGeom.Focus();
                }
            };

            Scene.DragSelectDone += Scene_DragSelectDone;

            Log.Information("Initialized");
        }

        private void Scene_DragSelectDone(List<Mesh>? obj)
        {
            if (obj == null || obj.Count == 0)
                return;

            if (obj.Count == 1)
            {
                MeshGeomPropLookup.TryGetValue(obj[0], out var prop);

                if (prop == null)
                {
                    return;
                }

                using (var propEditor = new PropEditor(Scene, obj[0], prop))
                {
                    propEditor.ShowDialog();
                }

                return;
            }

            var props = new List<GeomProp>();

            foreach (var mesh in obj)
            {
                MeshGeomPropLookup.TryGetValue(mesh, out var prop);

                if (prop != null)
                {
                    props.Add(prop);
                }
            }

            glControl.Invalidate();

            using (var propEditor = new PropEditorMulti(props, GeomPropMeshLookup))
            {
                propEditor.ShowDialog();
                Scene.ClearSelection();
            }
        }

        private void SetupContextMenus()
        {
            // files
            var menuItems = new List<ToolStripMenuItem>();

            MenuItemFilesEdit.Text = "Edit";
            menuItems.Add(MenuItemFilesEdit);

            MenuItemFilesOpen.Text = "Open in Explorer";
            menuItems.Add(MenuItemFilesOpen);

            MenuItemFilesRebuild.Text = "Repack Textures";
            menuItems.Add(MenuItemFilesRebuild);

            ContextMenuFiles.ItemClicked += ContextMenuFiles_ItemClicked;
            ContextMenuFiles.Items.AddRange(menuItems.ToArray());

            // geom prop
            menuItems = new List<ToolStripMenuItem>();

            MenuItemGeomPropEdit.Text = "Edit";
            menuItems.Add(MenuItemGeomPropEdit);

            ContextMenuGeomProp.ItemClicked += ContextMenuGeomProp_ItemClicked;
            ContextMenuGeomProp.Items.AddRange(menuItems.ToArray());

            // geom mesh
            menuItems = new List<ToolStripMenuItem>();

            MenuItemGeomMeshEdit.Text = "Edit";
            menuItems.Add(MenuItemGeomMeshEdit);

            ContextMenuGeomMesh.ItemClicked += ContextMenuGeomMesh_ItemClicked;
            ContextMenuGeomMesh.Items.AddRange(menuItems.ToArray());
        }

        private void ContextMenuGeomMesh_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            if (Geom == null || treeViewGeom.SelectedNode == null || CurrentStage == null)
                return;

            Mesh? mesh = Mesh.MeshList.Find(m => m.ID == treeViewGeom.SelectedNode.Text);

            if (mesh == null)
                return;

            if (e.ClickedItem == MenuItemGeomMeshEdit)
            {
                GeoBlock? block;

                if (!GeomMesh.BlockLookup.TryGetValue(mesh, out block) || block == null)
                    return;

                using (var geomEditor = new GeomEditor(Geom, block, mesh, Scene))
                {
                    geomEditor.ShowDialog();
                }
            }
        }

        private void ContextMenuGeomProp_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            if (treeViewGeom.SelectedNode == null || CurrentStage == null)
                return;

            GeomProp? prop;

            if (!GeomPropLookup.TryGetValue(treeViewGeom.SelectedNode, out prop) || prop == null)
                return;

            Mesh? propMesh;

            if (!GeomPropMeshLookup.TryGetValue(prop, out propMesh) || propMesh == null)
                return;

            if (e.ClickedItem == MenuItemGeomPropEdit)
            {
                using (var propEditor = new PropEditor(Scene, propMesh, prop))
                {
                    propEditor.ShowDialog();
                }
            }
        }

        private void ContextMenuFiles_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            if (treeViewFiles.SelectedNode == null || CurrentStage == null)
                return;

            StageFile stageFile = StageFileLookup[treeViewFiles.SelectedNode];

            if (stageFile == null)
                return;

            if (e.ClickedItem == MenuItemFilesOpen)
            {
                switch (stageFile.Type)
                {
                    case StageFile.FileType.QAR:
                    case StageFile.FileType.DAR:
                        Utils.ExplorerOpenDirectory(stageFile.GetUnpackedDir());
                        break;
                    default:
                        Utils.ExplorerSelectFile(stageFile.GetLocalPath());
                        break;
                }
            }
            else if (e.ClickedItem == MenuItemFilesEdit)
            {
                switch (stageFile.Type)
                {
                    case StageFile.FileType.DCI:
                        using (var dciEditor = new DciEditor(stageFile, CurrentStage))
                        {
                            dciEditor.ShowDialog();
                        }
                        break;
                    case StageFile.FileType.DLZ:
                        using (var dldEditor = new DldEditor(stageFile, CurrentStage))
                        {
                            dldEditor.ShowDialog();
                        }
                        break;
                    case StageFile.FileType.TXN:
                        using (var txnEditor = new TxnEditor(stageFile, CurrentStage))
                        {
                            txnEditor.ShowDialog();
                        }
                        break;
                    case StageFile.FileType.CNF:
                    case StageFile.FileType.NNI:
                        using (var txtEditor = new TextEditor(stageFile, false))
                        {
                            txtEditor.ShowDialog();
                        }
                        break;
                    default:
                        MessageBox.Show("Unsupported file type", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                }
            }
            else if (e.ClickedItem == MenuItemFilesRebuild)
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    DldFile? cache = null;
                    DldFile? cacheMips = null;

                    using (var dldSelector = new DldSelector(CurrentStage))
                    {
                        dldSelector.ShowDialog();

                        if (dldSelector.FilenameMain == "" || dldSelector.FilenameMips == "")
                        {
                            MessageBox.Show("You must select a DLZ.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        cache = new DldFile($"stage\\_dlz\\{dldSelector.FilenameMain.Replace(".dlz", ".dld")}");
                        cacheMips = new DldFile($"stage\\_dlz\\{dldSelector.FilenameMips.Replace(".dlz", ".dld")}");
                    }

                    DialogResult result = fbd.ShowDialog();

                    if (result != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        return;
                    }

                    Utils.RebuildTXN(fbd.SelectedPath, cache, cacheMips, stageFile.GetLocalPath());
                }
            }
        }

        private void SetEnabled(bool flag)
        {
            if (flag)
            {
                tabControl.Enabled = true;
                btnSave.Enabled = true;
                labelStatus.Text = "Ready";
            }
            else
            {
                tabControl.Enabled = false;
                btnSave.Enabled = false;
            }
        }

        private void Reset()
        {
            if (Geom != null)
            {
                Geom.CloseStream();
                Geom.Clear();
            }

            MeshGroups.Clear();
            MeshRefs.Clear();
            MeshProps.Clear();
            GeomPropMeshLookup.Clear();
            MeshGeomPropLookup.Clear();
            CurrentStage = null;
            Geom = null;
            Scene.Children.Clear();
            treeViewFiles.Nodes.Clear();
            treeViewGeom.Nodes.Clear();
            GeomMesh.BlockLookup.Clear();
            GeomMesh.MeshLookup.Clear();
            PropEditor.GeomPropOriginal.Clear();
            Mesh.ResetID();

            foreach (var mesh in Mesh.MeshList)
            {
                mesh.Delete();
            }

            Mesh.MeshList.Clear();

            if (Directory.Exists("stage"))
            {
                Directory.Delete("stage", true);
            }

            Directory.CreateDirectory("stage");
        }

        private async Task SetupGeom(string path)
        {
            try
            {
                var copyPath = $"{path}.edit";

                if (File.Exists(copyPath))
                    File.Delete(copyPath);

                File.Copy(path, copyPath);

                await Task.Run(() => Geom = new GeomFile(copyPath));

                if (Geom != null)
                {
                    MeshGroups = await Task.Run(() => GeomMesh.GetGeomGroupMeshes(Geom));
                    MeshRefs = await Task.Run(() => GeomMesh.GetGeomRefMeshes(Geom));
                    MeshBoundaries = await Task.Run(() => GeomMesh.GetGeomBoundaryMeshes(Geom));

                    var low = new Vector4();
                    var high = new Vector4();

                    Geom.GetWorldBoundary(ref low, ref high);
                    Scene.UpdateGridProperties(low, high);
                }

                await Task.Run(() =>
                {
                    MeshGroups.ForEach(mesh => Scene.Children.Add(mesh));

                    MeshRefs.ForEach(mesh =>
                    {
                        mesh.Visible = false;
                        Scene.Children.Add(mesh);
                    });

                    MeshBoundaries.ForEach(mesh =>
                    {
                        mesh.Visible = false;
                        Scene.Children.Add(mesh);
                    });
                });

                await Task.Run(() => GenerateGeomPropMeshes());
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Geom Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetPropModel(string id)
        {
            if (id.Contains("PRP_"))
            {
                if (id.Contains("_GOAL"))
                {
                    return Resources.ModelTerminal;
                }
                else if (id.Contains("_TGT_00"))
                {
                    return Resources.ModelGako;
                }
                else if (id.Contains("_TGT_01"))
                {
                    return Resources.ModelKerotan;
                }
                else if (id.Contains("_CBOX"))
                {
                    return Resources.ModelCBOX;
                }
                else if (id.Contains("_DOLL_"))
                {
                    return Resources.ModelDoll;
                }
            }

            return Resources.ModelCube;
        }

        private void GenerateGeomPropMeshes()
        {
            if (Geom == null)
                return;

            foreach (var prop in Geom.GeomProps)
            {
                if (prop.X == 0 && prop.Y == 0 && prop.Z == 0 && prop.W == 0)
                    continue;

                string id = DictionaryFile.GetHashString(prop.Hash);
                Mesh mesh = Mesh.LoadFromPLYBuffer(GetPropModel(id), new Vector3d(prop.X, prop.Z, prop.Y));
                var color = Color.FromArgb(255, 100, 140, 100);

                mesh.ColorStatic = color;
                mesh.SetColor(color);
                mesh.UseVertexColor = true;
                mesh.Visible = false;
                mesh.ID = id;
                mesh.DragSelectable = true;
                MeshProps.Add(mesh);
                Scene.Children.Add(mesh);
                GeomPropMeshLookup[prop] = mesh;
                MeshGeomPropLookup[mesh] = prop;
            }
        }

        private void PopulateGeomTreeView(string text)
        {
            if (Geom == null) return;

            _suspendAfterCheck = true;
            treeViewGeom.Enabled = false;
            treeViewGeom.BeginUpdate();

            text = text.ToLower();
            GeomPropLookup.Clear();
            TreeNodeLookup.Clear();

            // 1) MeshGroups
            TreeNodeGeomMeshes.Nodes.Clear();
            foreach (var mesh in MeshGroups.OrderBy(m => m.ID))
            {
                if (!mesh.ID.ToLower().Contains(text)) continue;
                var tn = TreeNodeGeomMeshes.Nodes.Add(mesh.ID);
                tn.Checked = true;
                tn.Tag = mesh;
                TreeNodeLookup[mesh.ID] = tn;
            }

            // 2) MeshRefs
            TreeNodeGeomRefs.Nodes.Clear();
            foreach (var mesh in MeshRefs.OrderBy(m => m.ID))
            {
                if (!mesh.ID.ToLower().Contains(text)) continue;
                var tn = TreeNodeGeomRefs.Nodes.Add(mesh.ID);
                tn.Checked = false;
                tn.Tag = mesh;
                TreeNodeLookup[mesh.ID] = tn;
            }

            // 3) MeshBoundaries
            TreeNodeGeomBoundaries.Nodes.Clear();
            foreach (var mesh in MeshBoundaries.OrderBy(m => m.ID))
            {
                if (!mesh.ID.ToLower().Contains(text)) continue;
                var tn = TreeNodeGeomBoundaries.Nodes.Add(mesh.ID);
                tn.Checked = false;
                tn.Tag = mesh;
                TreeNodeLookup[mesh.ID] = tn;
            }

            // 4) GeomProps
            TreeNodeGeomProps.Nodes.Clear();
            foreach (var prop in Geom.GeomProps
                                     .OrderBy(p => DictionaryFile.GetHashString(p.Hash)))
            {
                if (!GeomPropMeshLookup.TryGetValue(prop, out var mesh) ||
                    mesh == null ||
                    !mesh.ID.ToLower().Contains(text))
                    continue;

                var tn = TreeNodeGeomProps.Nodes.Add(mesh.ID);
                tn.Checked = false;
                tn.Tag = mesh;
                TreeNodeLookup[mesh.ID] = tn;
                GeomPropLookup[tn] = prop;
            }

            treeViewGeom.EndUpdate();
            treeViewGeom.Enabled = true;
            _suspendAfterCheck = false;
        }

        private async void PromptStageLoad(Stage.GameType game)
        {
            SetEnabled(false);

            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    Reset();
                    CurrentStage = new Stage(fbd.SelectedPath, game);

                    if (game == Stage.GameType.MGO2)
                    {
                        BinaryWriterEx.DefaultBigEndian = true;
                        BinaryReaderEx.DefaultBigEndian = true;
                        labelStatus.Text = "Decrypting...";
                        await CurrentStage.Decrypt();
                    }
                    else
                    {
                        BinaryWriterEx.DefaultBigEndian = game == Stage.GameType.MGS4;
                        BinaryReaderEx.DefaultBigEndian = game == Stage.GameType.MGS4;
                        labelStatus.Text = "Copying...";
                        CurrentStage.Copy("stage");
                    }

                    labelStatus.Text = "Unpacking...";
                    await CurrentStage.Unpack();

                    foreach (var file in CurrentStage.Files)
                    {
                        string ext = Path.GetExtension(file.Name);

                        if (ext == ".dec" || ext == ".enc")
                            continue;

                        FileInfo fi = new FileInfo(file.Name);
                        TreeNode? tds = null;

                        if (file.Archive != null)
                        {
                            var parent = treeViewFiles.Nodes.Find(file.Archive.Name, false);

                            if (parent.Length > 0)
                            {
                                tds = parent[0].Nodes.Add(fi.Name);
                                tds.Name = fi.Name;
                                StageFileLookup[tds] = file;
                            }
                        }
                        else
                        {
                            tds = treeViewFiles.Nodes.Add(fi.Name);
                            tds.Name = fi.Name;
                            StageFileLookup[tds] = file;
                        }

                        if (tds != null)
                        {
                            tds.Tag = fi.FullName;
                            tds.StateImageIndex = 0;

                            if (ext == ".qar" || ext == ".dar")
                                tds.StateImageIndex = 1;
                        }
                    }

                    TreeNodeGeomMeshes = treeViewGeom.Nodes.Add("Meshes");
                    TreeNodeGeomProps = treeViewGeom.Nodes.Add("Props");
                    TreeNodeGeomRefs = treeViewGeom.Nodes.Add("References");
                    TreeNodeGeomBoundaries = treeViewGeom.Nodes.Add("Boundaries");

                    treeViewGeom.CheckBoxes = true;
                    TreeNodeGeomMeshes.Checked = true;

                    if (CurrentStage.Geom != null)
                    {
                        labelStatus.Text = "Loading geom...";

                        await SetupGeom($"stage/{CurrentStage.Geom.Name}.dec");

                        PopulateGeomTreeView("");
                    }

                    var centerStage = Mesh.FromID("PRP_STAGE_CENTER");

                    if (centerStage != null && Scene != null)
                    {
                        Scene.Camera.Position = centerStage.Center;
                    }

                    SetEnabled(true);
                }
                else if (CurrentStage != null)
                {
                    SetEnabled(true);
                }
            }
        }

        private async void btnSave_Click(object sender, EventArgs e)
        {
            if (CurrentStage == null) return;

            SetEnabled(false);

            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    CurrentStage.Key = Directory.GetParent(fbd.SelectedPath)?.Name + "/" + new DirectoryInfo(fbd.SelectedPath).Name;

                    labelStatus.Text = "Packing...";
                    await CurrentStage.Pack();

                    if (Geom != null && CurrentStage.Geom != null)
                    {
                        labelStatus.Text = "Saving geom...";
                        Geom.Save(CurrentStage.Geom.GetLocalPath());
                    }

                    if (CurrentStage.Game == Stage.GameType.MGO2)
                    {
                        labelStatus.Text = "Encrypting...";
                        await CurrentStage.Encrypt(fbd.SelectedPath);
                    }
                    else
                    {
                        labelStatus.Text = "Copying...";
                        CurrentStage.CopyOut(fbd.SelectedPath);
                    }

                    var files = Directory.GetFiles(fbd.SelectedPath);

                    foreach (var file in files)
                    {
                        string newName = file.Replace(".dec", "").Replace(".enc", "");

                        if (newName != file && File.Exists(newName))
                            File.Delete(newName);

                        File.Move(file, newName);
                    }
                }
            }

            SetEnabled(true);
        }

        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            if (Scene != null && tabControl.SelectedTab == tabPageGeom)
            {
                labelCamPos.Text = $"{(int)Scene.Camera.Position.X}, {(int)Scene.Camera.Position.Y}, {(int)Scene.Camera.Position.Z}";

                if (Scene.SelectedDrawable != null && glControl.Focused)
                {
                    var meshId = (Scene.SelectedDrawable as Mesh)?.ID;

                    labelCamPos.Text += $" | {meshId}";
                }
            }
            else
            {
                labelCamPos.Text = "";
            }
        }

        private void treeViewGeom_AfterSelect(object sender, TreeViewEventArgs e)
        {
        }

        private void treeViewGeom_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var parent = e.Node.Parent;

            if (parent == null)
                return;

            if (parent == TreeNodeGeomMeshes || parent == TreeNodeGeomRefs || parent == TreeNodeGeomProps)
            {
                var mesh = Mesh.FromID(e.Node.Text);

                if (mesh == null)
                    return;

                if (parent == TreeNodeGeomProps)
                {
                    Scene.Camera.Position = mesh.AABB.TransformedCenter;
                }
                else
                {
                    Scene.Camera.Position = mesh.Vertices[0];
                }

                Scene.SelectMesh(mesh);
                Scene.Render();
            }
        }

        private void treeViewGeom_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {
            if (_suspendAfterCheck) return;

            if (e.Node == TreeNodeGeomMeshes ||
                e.Node == TreeNodeGeomRefs ||
                e.Node == TreeNodeGeomProps ||
                e.Node == TreeNodeGeomBoundaries)
            {
                e.Cancel = true;
                bool newState = !e.Node.Checked;

                _suspendAfterCheck = true;


                e.Node.Checked = newState;

                foreach (TreeNode child in e.Node.Nodes)
                {
                    child.Checked = newState;
                    if (child.Tag is Mesh m)
                        m.Visible = newState;
                }

                _suspendAfterCheck = false;

                treeViewGeom.SelectedNode = null;
                Scene.Render();
            }
        }


        private void treeViewGeom_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_suspendAfterCheck || e.Action == TreeViewAction.Unknown || e.Node == null)
                return;

            if (e.Node.Tag is Mesh mesh)
            {
                mesh.Visible = e.Node.Checked;
                Scene.Render();
            }
        }

        private void tbSpawnsFilter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                PopulateGeomTreeView(tbSpawnsFilter.Text);
            }
        }

        private void cbWireframe_CheckStateChanged(object sender, EventArgs e)
        {
            GL.Disable(EnableCap.PolygonSmooth);

            switch (cbWireframe.CheckState)
            {
                case CheckState.Unchecked:
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                    break;
                case CheckState.Checked:
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                    break;
                case CheckState.Indeterminate:
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                    GL.Enable(EnableCap.PolygonSmooth);
                    break;
                default:
                    break;
            }

            Scene.Render();
        }

        private void btnExportMesh_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog1 = new SaveFileDialog())
            {
                saveFileDialog1.Filter = "PLY files (*.ply)|*.ply";
                saveFileDialog1.RestoreDirectory = true;

                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    List<Mesh> meshes = new List<Mesh>();

                    foreach (var child in Scene.Children)
                    {
                        if (!child.Visible)
                            continue;

                        meshes.Add(child as Mesh);
                    }

                    Mesh mesh = Mesh.CombineMeshes(meshes);
                    mesh.SaveMesh(saveFileDialog1.FileName);
                }
            }
        }

        private async void encryptFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var path = openFileDialog.FileName;
                    await Utils.EncryptFileAsync(path, Utils.GetPathKey(path));
                }
            }
        }

        private async void decryptFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var path = openFileDialog.FileName;
                    await Utils.DecryptFileAsync(path, Utils.GetPathKey(path));
                }
            }
        }

        private void mergeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var baseFilePath = string.Empty;
            var mergeFilePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "geom files (*.geom)|*.geom|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    baseFilePath = openFileDialog.FileName;
                }
            }

            if (baseFilePath == string.Empty)
                return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "geom files (*.geom)|*.geom|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    mergeFilePath = openFileDialog.FileName;
                }
            }

            try
            {
                var baseGeom = new GeomFile(baseFilePath);
                var mergeGeom = new GeomFile(mergeFilePath);
                baseGeom.Merge(mergeGeom);
                mergeGeom.CloseStream();

                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "geom files (*.geom)|*.geom|All files (*.*)|*.*";
                    saveFileDialog.FilterIndex = 1;
                    saveFileDialog.RestoreDirectory = true;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        baseGeom.Save(saveFileDialog.FileName);
                    }
                }

                baseGeom.CloseStream();
            }
            catch (Exception exception)
            {
                // leaks if failed
                MessageBox.Show(exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void mergeVLMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var baseFilePath = string.Empty;
            var mergeFilePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "vlm files (*.vlm)|*.vlm|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    baseFilePath = openFileDialog.FileName;
                }
            }

            if (baseFilePath == string.Empty)
                return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "vlm files (*.vlm)|*.vlm|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    mergeFilePath = openFileDialog.FileName;
                }
            }

            try
            {
                var vlmBase = new VlmFile(baseFilePath);
                var vlmMerge = new VlmFile(mergeFilePath);
                vlmBase.Merge(vlmMerge);

                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "vlm files (*.vlm)|*.vlm|All files (*.*)|*.*";
                    saveFileDialog.FilterIndex = 1;
                    saveFileDialog.RestoreDirectory = true;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        vlmBase.Save(saveFileDialog.FileName);
                    }
                }
            }
            catch (Exception exception)
            {
                // leaks if failed
                MessageBox.Show(exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            var baseFilePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "geom files (*.geom)|*.geom|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    baseFilePath = openFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "geom files (*.geom)|*.geom|All files (*.*)|*.*";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.RestoreDirectory = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var baseGeom = new GeomFile(baseFilePath, false);
                    baseGeom.Save(saveFileDialog.FileName, true);
                    baseGeom.CloseStream();
                }
            }
        }

        private void stringHashUtilityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new StringHashEditor().ShowDialog();
        }

        private void treeViewFiles_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Select the clicked node
                treeViewFiles.SelectedNode = treeViewFiles.GetNodeAt(e.X, e.Y);

                if (treeViewFiles.SelectedNode != null)
                {
                    string[] canEdit = new string[] { ".nni", ".cnf", ".txn", ".dlz", ".dci" };
                    string filename = treeViewFiles.SelectedNode.Text;
                    MenuItemFilesEdit.Enabled = canEdit.Contains(Path.GetExtension(filename));

                    ContextMenuFiles.Show(treeViewFiles, e.Location);

                    MenuItemFilesRebuild.Visible = filename.Contains(".txn");
                }
            }
        }

        private void treeViewGeom_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Select the clicked node
                treeViewGeom.SelectedNode = treeViewGeom.GetNodeAt(e.X, e.Y);

                if (treeViewGeom.SelectedNode != null)
                {
                    if (treeViewGeom.SelectedNode.Parent?.Text == "Props")
                    {
                        ContextMenuGeomProp.Show(treeViewGeom, e.Location);
                    }
                    else if (treeViewGeom.SelectedNode.Parent?.Text == "Meshes" || treeViewGeom.SelectedNode.Parent?.Text == "References")
                    {
                        ContextMenuGeomMesh.Show(treeViewGeom, e.Location);
                    }
                }
            }
        }

        private void mGO2StageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PromptStageLoad(Stage.GameType.MGO2);
        }

        private void mGS4StageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PromptStageLoad(Stage.GameType.MGS4);
        }

        private void mGAStageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PromptStageLoad(Stage.GameType.MGA);
        }

        private void mergeReferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var baseFilePath = string.Empty;
            var mergeFilePath = string.Empty;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "geom files (*.geom)|*.geom|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    baseFilePath = openFileDialog.FileName;
                }
            }

            if (baseFilePath == string.Empty)
                return;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "geom files (*.geom)|*.geom|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    mergeFilePath = openFileDialog.FileName;
                }
            }

            try
            {
                var baseGeom = new GeomFile(baseFilePath);
                var mergeGeom = new GeomFile(mergeFilePath);
                //baseGeom.CopySingleRef(mergeGeom, 0x874c5);
                baseGeom.MergeReferences(mergeGeom);
                mergeGeom.CloseStream();

                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "geom files (*.geom)|*.geom|All files (*.*)|*.*";
                    saveFileDialog.FilterIndex = 1;
                    saveFileDialog.RestoreDirectory = true;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        baseGeom.Save(saveFileDialog.FileName);
                    }
                }

                baseGeom.CloseStream();
            }
            catch (Exception exception)
            {
                // leaks if failed
                MessageBox.Show(exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void AppendLog(string value)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(AppendLog), new object[] { value });
                return;
            }
            tbLog.Text += value;
            tbLog.SelectionStart = tbLog.TextLength;
            tbLog.ScrollToCaret();
        }

        private void LoggerSink_NewLogHandler(object? sender, EventArgs e)
        {
            var log = ((LogEventArgs)e).Log;
            using var writer = new StringWriter();
            LoggerSink.Formatter.Format(log, writer);
            var message = writer.ToString();
            AppendLog(message);
        }

        private void cbGrid_CheckedChanged(object sender, EventArgs e)
        {
            if (Scene.CurrentScene != null)
            {
                Scene.CurrentScene.GridEnabled = cbGrid.Checked;
                Scene.CurrentScene.Render();
            }
        }

    }
}