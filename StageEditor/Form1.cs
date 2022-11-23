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

namespace Haven
{
    public partial class MainForm : Form
    {
        public Stage? CurrentStage;
        public Scene Scene;
        public GeomFile? Geom;
        public List<Mesh> MeshGroups = new List<Mesh>();
        public List<Mesh> MeshObjects = new List<Mesh>();
        public List<Mesh> MeshProps = new List<Mesh>();
        public List<ContextMenuStrip> ContextMenuStrips = new List<ContextMenuStrip>();
        public Dictionary<GeomProp, Mesh> GeomPropMeshLookup = new Dictionary<GeomProp, Mesh>();
        public Dictionary<TreeNode, StageFile> StageFileLookup = new Dictionary<TreeNode, StageFile>();

        public bool Decrypted = false;
        public ContextMenuStrip ContextMenuFiles = new ContextMenuStrip();
        public ToolStripMenuItem MenuItemOpen = new ToolStripMenuItem();
        public ToolStripMenuItem MenuItemEdit = new ToolStripMenuItem();

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            tabPageGeom.Show();
            SetEnabled(false);
            DictionaryFile.Load("bin/dictionary.txt");

            var menuItems = new List<ToolStripMenuItem>();

            MenuItemEdit.Text = "Edit";
            menuItems.Add(MenuItemEdit);

            MenuItemOpen.Text = "Open in Explorer";
            menuItems.Add(MenuItemOpen);

            ContextMenuFiles.ItemClicked += ContextMenuFiles_ItemClicked;
            ContextMenuFiles.Items.AddRange(menuItems.ToArray());
        }

        private void ContextMenuFiles_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            if (treeViewFiles.SelectedNode == null || CurrentStage == null)
                return;

            StageFile stageFile = StageFileLookup[treeViewFiles.SelectedNode];

            if (stageFile == null)
                return;

            if (e.ClickedItem == MenuItemOpen)
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
            else if (e.ClickedItem == MenuItemEdit) 
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
            Scene.Children.Clear();
            treeViewFiles.Nodes.Clear();
            treeViewGeom.Nodes.Clear();
            Mesh.ResetID();

            foreach (var mesh in Mesh.MeshList)
            {
                mesh.Delete();
            }

            Mesh.MeshList.Clear();

            foreach (var item in ContextMenuStrips)
            {
                item.ItemClicked -= PropNode_ItemClicked;
                item.Items.Clear();
                item.Dispose();
            }

            ContextMenuData.Clear();
            ContextMenuStrips = new List<ContextMenuStrip>();

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

                MeshGroups = await Task.Run(() => Mesh.GetGeomGroupMeshes(Geom));
                MeshObjects = await Task.Run(() => Mesh.GetGeomRefMeshes(Geom));

                await Task.Run(() =>
                {
                    MeshGroups.ForEach(group => Scene.Children.Add(group));
                    MeshObjects.ForEach(obj =>
                    {
                        obj.Visible = false;
                        Scene.Children.Add(obj);
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
                Scene.Children.Add(mesh);
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
                node.Checked = true;
            }

            MeshObjects = MeshObjects.OrderBy(x => x.ID).ToList();

            foreach (var mesh in MeshObjects)
            {
                var node = nodeObjects.Nodes.Add(mesh.ID);
                node.Checked = false;
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

                var docMenu = new ContextMenuStrip();
                ToolStripMenuItem editLabel = new ToolStripMenuItem();
                editLabel.Text = "Edit";
                docMenu.ItemClicked += PropNode_ItemClicked;
                
                docMenu.Items.AddRange(new ToolStripMenuItem[] { editLabel });
                node.ContextMenuStrip = docMenu;
                ContextMenuStrips.Add(docMenu);

                new ContextMenuData(docMenu, node, mesh, prop);
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

        private void PropNode_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
        {
            var data = ContextMenuData.FromObject(sender);

            if (sender == null || data == null)
            {
                return;
            }

            var node = data.Node;
            var mesh = data.Mesh;
            var prop = data.Prop;
            var propOriginal = data.PropOriginal;

            if (mesh == null || prop == null || propOriginal == null)
            {
                return;
            }

            new PropEditor(Scene, mesh, (GeomProp)prop, (GeomProp)propOriginal).ShowDialog();

            mesh = Mesh.FromID(node.Text);

            if (mesh == null)
            {
                return;
            }

            MeshProps.Add(mesh);
            Scene.Children.Add(mesh);
            Scene.Render();
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

                Scene.Render();

                return;
            }

            var mesh = Mesh.FromID(id);

            if (mesh == null)
                return;

            mesh.Visible = e.Node.Checked;

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

        private void treeViewFiles_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            //MessageBox.Show("test");
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
                    MenuItemEdit.Enabled = canEdit.Contains(Path.GetExtension(filename));

                    ContextMenuFiles.Show(treeViewFiles, e.Location);
                }
            }
        }
    }
}