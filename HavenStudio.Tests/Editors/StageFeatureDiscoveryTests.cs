using HavenStudio.Editors;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Extensions;
using HavenStudio.Formats.Gcx;
using HavenStudio.Rendering;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Tests.Editors;

public sealed class StageFeatureDiscoveryTests
{
    [Fact]
    public async Task Opt_in_stage_loads_its_octocamo_template()
    {
        var root = Environment.GetEnvironmentVariable("HAVEN_STAGE_FEATURE_ROOT");
        if (string.IsNullOrWhiteSpace(root)) return;

        var workspace = new WorkspaceCatalog(root, Endianness.Big);
        var snapshot = await workspace.ScanAsync();
        var geomPath = StageManifestResolver.FindGeomPath(workspace, snapshot);
        Assert.NotNull(geomPath);

        var host = new SceneHost();
        var collision = new CollisionEditorViewModel(host);
        using var gcx = new GcxEditorViewModel(host);
        using var map = new MapEditorViewModel(host, collision, gcx);
        collision.SetWorkspace(workspace);
        await collision.LoadFromWorkspacePathAsync(geomPath);
        Assert.NotNull(collision.GeomFile);
        var stageDirectory = Path.GetDirectoryName(geomPath.PhysicalPath);
        var octtFiles = snapshot.WithExtension(".octt").Where(file =>
            string.Equals(Path.GetDirectoryName(file.Path.PhysicalPath), stageDirectory,
                StringComparison.OrdinalIgnoreCase) &&
            (!file.Path.IsArchiveEntry || Path.GetFileName(file.Path.PhysicalPath)
                .Equals("cache.dar", StringComparison.OrdinalIgnoreCase))).ToArray();
        if (octtFiles.Length > 0)
        {
            var octt = Assert.Single(octtFiles);
            var octl = Assert.Single(snapshot.WithExtension(".octl"), file =>
                file.Path.PhysicalPath.Equals(octt.Path.PhysicalPath, StringComparison.OrdinalIgnoreCase));
            using var decoded = new OctocamoSurfaceCatalog(collision.GeomFile!,
                workspace.ReadAllBytes(octt.Path), workspace.ReadAllBytes(octl.Path));
            Assert.True(decoded.MappingCount > 0);
        }
        await map.DiscoverOctocamoAsync(workspace);

        if (snapshot.WithExtension(".octt").Any())
            Assert.True(map.HasOctocamoTable, "A stage OCTT exists but OctoCamo was disabled.");
        collision.Clear();
    }

    [Fact]
    public async Task Opt_in_stage_scans_its_camera_template_without_full_scene_analysis()
    {
        var root = Environment.GetEnvironmentVariable("HAVEN_STAGE_FEATURE_ROOT");
        if (string.IsNullOrWhiteSpace(root)) return;

        var workspace = new WorkspaceCatalog(root, Endianness.Big);
        var snapshot = await workspace.ScanAsync();
        var geomPath = StageManifestResolver.FindGeomPath(workspace, snapshot);
        var gcxPath = StageManifestResolver.FindGcxPath(workspace, snapshot, geomPath);
        Assert.NotNull(gcxPath);
        using var stream = workspace.OpenRead(gcxPath);
        var document = GcxFile.Read(stream);
        var cameras = GcxCameraWriter.ScanDocument(document);
        Assert.NotEmpty(cameras);
        Assert.All(cameras, camera =>
        {
            var unchanged = GcxCameraWriter.Write(camera.Script.Bytes, camera.TableIndex,
                camera.RowIndex, camera.Position, camera.Target);
            Assert.Equal(camera.Script.Bytes, unchanged);
        });
        var first = cameras[0];
        var moved = new OpenTK.Mathematics.Vector3(first.Position.X + 1,
            first.Position.Y, first.Position.Z);
        var edited = GcxCameraWriter.Write(first.Script.Bytes, first.TableIndex,
            first.RowIndex, moved, first.Target);
        Assert.NotEqual(first.Script.Bytes, edited);
        Assert.Equal(moved, GcxCameraWriter.Vector(
            GcxCameraWriter.Scan(edited)[first.TableIndex], first.RowIndex * 7));
        Assert.Equal(first.Position, GcxCameraWriter.Vector(
            GcxCameraWriter.Scan(first.Script.Bytes)[first.TableIndex], first.RowIndex * 7));
    }
}
