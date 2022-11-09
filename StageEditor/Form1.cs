using System;
using System.ComponentModel;
using System.Diagnostics;
using OpenTK;
using Haven.Parser;
using Haven.Render;
using Haven.Properties;
using static OpenTK.Graphics.OpenGL.GL;
using System.Windows.Forms;
using OpenTK.Input;
using System.IO;
using System.Reflection.Metadata;
using System.Security.Cryptography;

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

        public MainForm()
        {
            InitializeComponent();
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
            var hashCounter = new Dictionary<string, int>();

            foreach (var prop in Geom.GeomProps)
            {
                if (prop.X == 0 && prop.Y == 0 && prop.Z == 0 && prop.W == 0)
                    continue;

                string id = DictionaryFile.GetHashString(prop.Hash);
                Mesh mesh = Mesh.LoadFromPLYBuffer(GetPropModel(id), new Vector3d(prop.X, prop.Z, prop.Y));
                mesh.ColorStatic = Color.Green;
                mesh.SetColor(Color.Green, false);
                mesh.Visible = false;

                if (Mesh.FromID(id) != null)
                {
                    if (!hashCounter.ContainsKey(id))
                        hashCounter[id] = 0;

                    hashCounter[id]++;

                    id = $"{id} ({hashCounter[id]})";
                }

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

                    foreach (var file in CurrentStage.Files)
                    {
                        string ext = Path.GetExtension(file.Name);

                        if (ext == ".dec" || ext == ".enc")
                            continue;

                        FileInfo fi = new FileInfo(file.Name);
                        TreeNode tds = treeViewFiles.Nodes.Add(fi.Name);
                        tds.Tag = fi.FullName;
                        tds.StateImageIndex = 0;

                        if (ext == ".qar" || ext == ".dar")
                            tds.StateImageIndex = 1;
                    }

                    treeViewGeom.Nodes.Add("Meshes");
                    treeViewGeom.Nodes.Add("Props");
                    treeViewGeom.Nodes.Add("Objects");

                    DialogResult dialogResult = MessageBox.Show("Do you want to decrypt these files?", "Notice", MessageBoxButtons.YesNo);

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

        private void treeViewFiles_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            string filename = e.Node.Text;
            string ext = Path.GetExtension(filename);
            var fileType = StageFile.GetTypeFromExt(ext);

            switch (fileType)
            {
                case StageFile.FileType.GEOM:
                    tabControl.SelectedTab = tabPageGeom;
                    break;
                case StageFile.FileType.CNF:
                case StageFile.FileType.NNI:
                    var textEditor = new TextEditor(filename, false);
                    textEditor.ShowDialog();
                    break;
                case StageFile.FileType.QAR:
                case StageFile.FileType.DAR:
                    try
                    {
                        Process.Start("explorer.exe", $"\"{Directory.GetCurrentDirectory()}\\stage\\_{filename}\\{ext.Replace(".", "")}\"");
                    }
                    catch (Win32Exception win32Exception)
                    {
                        MessageBox.Show(win32Exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    break;
                default:
                    MessageBox.Show("Unsupported file type", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
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

                    labelStatus.Text = "Encrypting...";
                    await CurrentStage.Encrypt(fbd.SelectedPath);

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

        private void glControl_Load(object sender, EventArgs e)
        {
            Scene = new Scene(glControl);
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            tabPageGeom.Show();
            SetEnabled(false);
            DictionaryFile.Load("bin/dictionary.txt");
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
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

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
                    var dir = Path.GetDirectoryName(path);
                    var key = Directory.GetParent(dir).Name + "/" + new DirectoryInfo(dir).Name;
                    await Utils.EncryptFileAsync(path, key);
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
                    var dir = Path.GetDirectoryName(path);
                    var key = Directory.GetParent(dir).Name + "/" + new DirectoryInfo(dir).Name;
                    await Utils.DecryptFileAsync(path, key);
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

    }
}