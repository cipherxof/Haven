using System;
using System.ComponentModel;
using System.Diagnostics;
using OpenTK;
using Haven.Parser;
using Haven.Render;
using Haven.Properties;
using static OpenTK.Graphics.OpenGL.GL;
using System.Xml.Linq;
using System.Windows.Forms;
using Haven.Forms;
using Haven.Parser.Geom;

namespace Haven
{
    public partial class MainForm : Form
    {
        public Stage? CurrentStage;
        public Scene? Scene;
        public GeomFile? Geom;
        public bool Decrypted = false;

        public List<Mesh> MeshGroups = new List<Mesh>();
        public List<Mesh> MeshObjects = new List<Mesh>();
        public List<Mesh> MeshProps = new List<Mesh>();

        public Dictionary<GeomProp, Mesh> GeomPropMeshLookup = new Dictionary<GeomProp, Mesh>();
        public Dictionary<TreeNode, StageFile> StageFileLookup = new Dictionary<TreeNode, StageFile>();
        public Dictionary<TreeNode, GeomProp> GeomPropLookup = new Dictionary<TreeNode, GeomProp>();

        public ContextMenuStrip ContextMenuFiles = new ContextMenuStrip();
        public ToolStripMenuItem MenuItemFilesOpen = new ToolStripMenuItem();
        public ToolStripMenuItem MenuItemFilesEdit = new ToolStripMenuItem();

        public ContextMenuStrip ContextMenuGeomProp = new ContextMenuStrip();
        public ToolStripMenuItem MenuItemGeomPropEdit = new ToolStripMenuItem();

        public ContextMenuStrip ContextMenuGeomMesh = new ContextMenuStrip();
        public ToolStripMenuItem MenuItemGeomMeshEdit = new ToolStripMenuItem();

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            tabPageGeom.Show();
            SetEnabled(false);
            DictionaryFile.Load("bin/dictionary.txt");
            SetupContextMenus();

            var dlz = new DlzFile(@"C:\Users\Me\Documents\GitHub\rpcs3\bin\dev_hdd0\game\NPMG00020\USRDIR\o\stage\n023a\cache.dlz.dec");
            dlz.Unpack(@"C:\Users\Me\Documents\GitHub\rpcs3\bin\dev_hdd0\game\NPMG00020\USRDIR\o\stage\n023a\cache.dld");
        }

        private void SetupContextMenus()
        {
            // files
            var menuItems = new List<ToolStripMenuItem>();

            MenuItemFilesEdit.Text = "Edit";
            menuItems.Add(MenuItemFilesEdit);

            MenuItemFilesOpen.Text = "Open in Explorer";
            menuItems.Add(MenuItemFilesOpen);

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

                new GeomEditor(Geom, block).ShowDialog();
            }
        }

        private void ContextMenuGeomProp_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            if (Scene == null || treeViewGeom.SelectedNode == null || CurrentStage == null)
                return;

            GeomProp? prop;

            if (!GeomPropLookup.TryGetValue(treeViewGeom.SelectedNode, out prop) || prop == null)
                return;

            Mesh? propMesh;

            if (!GeomPropMeshLookup.TryGetValue(prop, out propMesh) || propMesh == null)
                return;

            if (e.ClickedItem == MenuItemGeomPropEdit)
            {
                new PropEditor(Scene, propMesh, prop).ShowDialog();
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
                        new DciEditor(stageFile, CurrentStage).ShowDialog();
                        break;
                    case StageFile.FileType.DLZ:
                        new DldEditor(stageFile, CurrentStage).ShowDialog();
                        break;
                    case StageFile.FileType.TXN:
                        new TxnEditor(stageFile, CurrentStage).ShowDialog();
                        break;
                    case StageFile.FileType.CNF:
                    case StageFile.FileType.NNI:
                        new TextEditor(stageFile, false).ShowDialog();
                        break;
                    default:
                        MessageBox.Show("Unsupported file type", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                }
            }
        }

        private void glControl_Load(object sender, EventArgs e)
        {
            Scene = new Scene(glControl);
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
            MeshGroups = new List<Mesh>();
            MeshObjects = new List<Mesh>();
            MeshProps = new List<Mesh>();
            GeomPropMeshLookup = new Dictionary<GeomProp, Mesh>();
            CurrentStage = null;
            Geom = null;
            Scene?.Children.Clear();
            treeViewFiles.Nodes.Clear();
            treeViewGeom.Nodes.Clear();
            GeomMesh.BlockLookup.Clear();
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

                MeshGroups = await Task.Run(() => GeomMesh.GetGeomGroupMeshes(Geom));
                MeshObjects = await Task.Run(() => GeomMesh.GetGeomRefMeshes(Geom));

                await Task.Run(() =>
                {
                    MeshGroups.ForEach(group => Scene?.Children.Add(group));
                    MeshObjects.ForEach(obj =>
                    {
                        obj.Visible = false;
                        Scene?.Children.Add(obj);
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
                mesh.ColorStatic = Color.Green;
                mesh.SetColor(Color.Green, false);
                mesh.Visible = false;
                mesh.ID = id;
                MeshProps.Add(mesh);
                Scene?.Children.Add(mesh);
                GeomPropMeshLookup[prop] = mesh;
            }
        }

        private void PopulateGeomTreeView()
        {
            treeViewGeom.CheckBoxes = true;

            var nodeMeshes = treeViewGeom.Nodes[0];
            var nodeProps = treeViewGeom.Nodes[1];
            var nodeObjects = treeViewGeom.Nodes[2];

            nodeMeshes.Checked = true;
            nodeProps.Checked = false;
            nodeObjects.Checked = false;

            MeshGroups = MeshGroups.OrderBy(x => x.ID).ToList();

            foreach (var mesh in MeshGroups)
            {
                var node = nodeMeshes.Nodes.Add(mesh.ID);
                var block = GeomMesh.BlockLookup[mesh];

                foreach (var prim in Geom.BlockFaceData[block])
                {
                    if (prim.GetPrimType() == Parser.Geom.Geom.Primitive.GEO_FIELD)
                        continue;

                    //var nodePrim = node.Nodes.Add($"{prim.GetPrimType().ToString()} - {prim.Name.ToString("X4")}");
                }

                node.Checked = true;
            }

            MeshObjects = MeshObjects.OrderBy(x => x.ID).ToList();

            foreach (var mesh in MeshObjects)
            {
                var node = nodeObjects.Nodes.Add(mesh.ID);
                node.Checked = false;

                var block = GeomMesh.BlockLookup[mesh];

                foreach (var prim in Geom.BlockFaceData[block])
                {
                    if (prim.GetPrimType() == Parser.Geom.Geom.Primitive.GEO_FIELD)
                        continue;

                    //var nodePrim = node.Nodes.Add($"{prim.GetPrimType().ToString()} - {prim.Name.ToString("X4")}");
                }
            }

            if (Geom == null) 
                return;

            var propsList = Geom.GeomProps.OrderBy(x => DictionaryFile.GetHashString(x.Hash)).ToList();

            foreach (var prop in propsList)
            {
                Mesh? mesh;

                GeomPropMeshLookup.TryGetValue(prop, out mesh);

                if (mesh == null)
                    continue;

                var node = nodeProps.Nodes.Add(mesh.ID);
                node.Checked = false;

                GeomPropLookup[node] = prop;
            }
        }

        private async void btnLoadStage_Click(object sender, EventArgs e)
        {
            SetEnabled(false);

            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    Reset();
                    CurrentStage = new Stage(fbd.SelectedPath);

                    DialogResult dialogResult = MessageBox.Show("Do you want to decrypt these files?", "Notice", MessageBoxButtons.YesNo);

                    Decrypted = dialogResult == DialogResult.Yes;

                    if (dialogResult == DialogResult.Yes)
                    {
                        labelStatus.Text = "Decrypting...";
                        await CurrentStage.Decrypt();
                    }
                    else if (dialogResult == DialogResult.No)
                    {
                        labelStatus.Text = "Copying...";
                        await CurrentStage.Copy();
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

                    treeViewGeom.Nodes.Add("Meshes");
                    treeViewGeom.Nodes.Add("Props");
                    treeViewGeom.Nodes.Add("Objects");

                    if (CurrentStage.Geom != null)
                    {
                        labelStatus.Text = "Loading geom...";

                        await SetupGeom($"stage/{CurrentStage.Geom.Name}.dec");

                        PopulateGeomTreeView();
                    }

                    var centerStage = Mesh.FromID("PRP_STAGE_CENTER");

                    if (centerStage != null)
                    {
                        Scene.Camera.Position = new Vector3d(centerStage.TransformedCenter.X, centerStage.TransformedCenter.Y, centerStage.TransformedCenter.Z);
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
                    labelStatus.Text = "Packing...";
                    await CurrentStage.Pack();

                    labelStatus.Text = "Saving geom...";
                    Geom?.Save(CurrentStage.Geom.GetLocalPath());

                    if (Decrypted)
                    {
                        labelStatus.Text = "Encrypting...";
                        await CurrentStage.Encrypt(fbd.SelectedPath);
                    }
                    else
                    {
                        labelStatus.Text = "Copying...";
                        await CurrentStage.Encrypt(fbd.SelectedPath);
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

                if (Scene.SelectedDrawable != null)
                {
                    labelCamPos.Text += $" | {(Scene.SelectedDrawable as Mesh).ID}";
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

            if (parent.Text == "Objects" || parent.Text == "Meshes" || parent.Text == "Props")
            {
                var mesh = Mesh.FromID(e.Node.Text);

                if (mesh == null)
                    return;

                Scene.Camera.Position = new Vector3d(mesh.TransformedCenter.X, mesh.TransformedCenter.Y, mesh.TransformedCenter.Z);
                Scene.SelectMesh(mesh);

                Scene.Render();
            }
        }

        private void treeViewGeom_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Action == TreeViewAction.Unknown || e.Node == null)
                return;

            string id = e.Node.Text;

            if (id == "Meshes" || id == "Objects" || id == "Props")
            {
                treeViewGeom.Enabled = false;

                var list = id == "Meshes" ? MeshGroups : MeshObjects;

                if (id == "Props")
                    list = MeshProps;

                Parallel.ForEach(list, child => child.Visible = e.Node.Checked);

                for (int i = 0; i < e.Node.Nodes.Count; i++)
                    e.Node.Nodes[i].Checked = e.Node.Checked;

                treeViewGeom.SelectedNode = null;
                treeViewGeom.Enabled = true;

                Scene?.Render();

                return;
            }

            var mesh = Mesh.FromID(id);

            if (mesh == null)
                return;

            mesh.Visible = e.Node.Checked;

            Scene?.Render();
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

        private void tbSpawnsFilter_TextChanged(object sender, EventArgs e)
        {
            // todo
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
            catch(Exception exception)
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
                    var baseGeom = new GeomFile(baseFilePath);
                    baseGeom.Save(saveFileDialog.FileName);
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
                    else if (treeViewGeom.SelectedNode.Parent?.Text == "Meshes" || treeViewGeom.SelectedNode.Parent?.Text == "Objects")
                    {
                        ContextMenuGeomMesh.Show(treeViewGeom, e.Location);
                    }
                }
            }
        }

    }
}