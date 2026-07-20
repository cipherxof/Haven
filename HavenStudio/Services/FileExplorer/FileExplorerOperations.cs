using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using HavenStudio.Formats.Dlz;
using HavenStudio.Formats.Gcx;
using HavenStudio.Formats.Geo;
using HavenStudio.Services.FileOpening;
using HavenStudio.Services.Persistence;
using HavenStudio.Services.Workspace;
using HavenStudio.Utils;
using Serilog;

namespace HavenStudio.Services.FileExplorer;

public sealed class FileExplorerOperations
{
    private static readonly ILogger Log = Serilog.Log.ForContext<FileExplorerOperations>();

    private readonly Window _owner;
    private readonly MainWindowViewModel _viewModel;
    private readonly FileOpenCoordinator _fileOpenCoordinator;
    private readonly CancellationToken _cancellationToken;

    public FileExplorerOperations(
        Window owner,
        MainWindowViewModel viewModel,
        FileOpenCoordinator fileOpenCoordinator,
        CancellationToken cancellationToken)
    {
        _owner = owner;
        _viewModel = viewModel;
        _fileOpenCoordinator = fileOpenCoordinator;
        _cancellationToken = cancellationToken;
    }

    public async Task OpenAsync(WorkspacePath path)
    {
        var result = await _fileOpenCoordinator.OpenAsync(path, _viewModel.Workspace, _cancellationToken);
        FileOpenResultPresenter.Present(result);
    }

    public Task ExtractArchiveAsync(FileNode fileNode) =>
        ExecuteAsync("Dump Archive", () => ExtractArchiveCoreAsync(fileNode));

    public Task ReplaceArchiveEntryAsync(FileNode fileNode) =>
        ExecuteAsync("Replace File", () => ReplaceArchiveEntryCoreAsync(fileNode));

    public Task DumpTxnTexturesAsync(FileNode fileNode) =>
        ExecuteAsync("Dump TXN Textures", () => DumpTxnTexturesCoreAsync(fileNode));

    public Task DumpDlzAsync(FileNode fileNode) =>
        ExecuteAsync("Dump DLZ", () => DumpDlzCoreAsync(fileNode));

    public Task DumpGcxJsonAsync(FileNode fileNode) =>
        ExecuteAsync("Dump GCX", () => DumpGcxJsonCoreAsync(fileNode));

    public Task RestoreGcxJsonAsync(FileNode fileNode) =>
        ExecuteAsync("Restore GCX", () => RestoreGcxJsonCoreAsync(fileNode));

    public Task DumpMapJsonAsync(FileNode fileNode) =>
        ExecuteAsync("Dump Map JSON", () => DumpMapJsonCoreAsync(fileNode));

    public Task RestoreMapJsonAsync(FileNode fileNode) =>
        ExecuteAsync("Restore Map JSON", () => RestoreMapJsonCoreAsync(fileNode));

    public Task ExportGeomGltfAsync(FileNode fileNode) =>
        ExecuteAsync("Export GEOM glTF", () => ExportGeomGltfCoreAsync(fileNode));

    public Task ImportGeomGltfPositionsAsync(FileNode fileNode) =>
        ExecuteAsync("Import GEOM glTF Positions", () => ImportGeomGltfPositionsCoreAsync(fileNode));

    public Task ImportGeomGltfTopologyAsync(FileNode fileNode) =>
        ExecuteAsync("Import GEOM glTF Mesh", () => ImportGeomGltfTopologyCoreAsync(fileNode));

    public Task CreateGeomFromGltfAsync() =>
        ExecuteAsync("Create GEOM from glTF", CreateGeomFromGltfCoreAsync);

    public Task TransportGeomEffectsAsync(FileNode fileNode) =>
        ExecuteAsync("Transport GEOM Effects", () => TransportGeomEffectsCoreAsync(fileNode));

    public Task RestoreArchiveFromFolderAsync(FileNode fileNode) =>
        ExecuteAsync("Restore Archive from Folder", () => RestoreArchiveFromFolderCoreAsync(fileNode));

    public Task RestoreTxnFromFolderAsync(FileNode fileNode) =>
        ExecuteAsync("Restore TXN from Folder", () => RestoreTxnFromFolderCoreAsync(fileNode));

    private async Task ExtractArchiveCoreAsync(FileNode fileNode)
    {
        var outputFolder = await PickOutputFolderAsync("Select Output Folder for Dumped Files");
        if (outputFolder == null)
        {
            return;
        }

        try
        {
            var workspace = RequireWorkspace();
            var summary = workspace.ExtractArchive(fileNode.WorkspacePath, outputFolder);
            Log.Information(
                "[ArchiveDump] Extracted {ExtractedCount} files from '{ArchiveName}' to '{OutputFolder}'.",
                summary.ExtractedCount,
                summary.ArchiveName,
                summary.OutputFolder);
        }
        catch (Exception exception)
        {
            PresentFailure("Dump Archive", exception);
        }
    }

    private async Task ReplaceArchiveEntryCoreAsync(FileNode fileNode)
    {
        if (!fileNode.IsArchiveEntry)
        {
            return;
        }

        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Select Replacement for '{fileNode.WorkspacePath.ArchiveEntryName}'",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (_cancellationToken.IsCancellationRequested || file == null || !File.Exists(file.Path.LocalPath))
        {
            return;
        }

        try
        {
            var replacementPath = file.Path.LocalPath;
            var data = await File.ReadAllBytesAsync(replacementPath, _cancellationToken);
            RequireWorkspace().Replace(fileNode.WorkspacePath, data);
            Log.Information(
                "[Archive] Replaced entry '{EntryName}' in '{ArchivePath}' with '{ReplacementPath}' ({Size} bytes).",
                fileNode.Name,
                fileNode.ArchivePath,
                replacementPath,
                data.Length);
            MessageDialog.Info("Replace File", $"Replaced '{fileNode.Name}' in {Path.GetFileName(fileNode.ArchivePath)}.");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Replace File", exception);
        }
    }

    private async Task DumpTxnTexturesCoreAsync(FileNode fileNode)
    {
        var outputFolder = await PickOutputFolderAsync("Select Output Folder for TXN Textures");
        if (outputFolder == null)
        {
            return;
        }

        try
        {
            var summary = TxnTextureDumpService.DumpAll(fileNode.WorkspacePath, RequireWorkspace(), outputFolder);
            Log.Information(
                "[TxnDump] Dumped {Dumped}/{Total} textures to '{OutputFolder}' (skipped {Skipped}).",
                summary.Dumped,
                summary.Total,
                outputFolder,
                summary.Skipped);
        }
        catch (Exception exception)
        {
            PresentFailure("Dump TXN Textures", exception);
        }
    }

    private async Task DumpDlzCoreAsync(FileNode fileNode)
    {
        var suggestedName = $"{Path.GetFileNameWithoutExtension(fileNode.WorkspacePath.FileName)}.dld";
        var target = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save DLD",
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType("DLD") { Patterns = ["*.dld"] }
            ]
        });

        if (_cancellationToken.IsCancellationRequested || target == null)
        {
            return;
        }

        try
        {
            var workspace = RequireWorkspace();
            using var source = workspace.OpenRead(fileNode.WorkspacePath);
            var dlz = new DlzFile(source, workspace.Endianness);
            dlz.Unpack(target.Path.LocalPath);
            Log.Information("[DLZ] Unpacked '{SourcePath}' to '{TargetPath}'.", fileNode.FullPath, target.Path.LocalPath);
        }
        catch (Exception exception)
        {
            PresentFailure("Dump DLZ", exception);
        }
    }

    private async Task DumpGcxJsonCoreAsync(FileNode fileNode)
    {
        var suggestedName = $"{Path.GetFileNameWithoutExtension(fileNode.WorkspacePath.FileName)}.json";
        var target = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save GCX JSON",
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType("JSON") { Patterns = ["*.json"] }
            ]
        });

        if (_cancellationToken.IsCancellationRequested || target == null)
        {
            return;
        }

        try
        {
            using var source = RequireWorkspace().OpenRead(fileNode.WorkspacePath);
            var json = GcxFile.ToJson(source);
            await File.WriteAllTextAsync(target.Path.LocalPath, json, _cancellationToken);
            Log.Information("[GCX] JSON written to '{TargetPath}'.", target.Path.LocalPath);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Dump GCX", exception);
        }
    }

    private async Task RestoreGcxJsonCoreAsync(FileNode fileNode)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select GCX JSON",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON") { Patterns = ["*.json"] }
            ]
        });

        var file = files.FirstOrDefault();
        if (_cancellationToken.IsCancellationRequested || file == null)
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(file.Path.LocalPath, _cancellationToken);
            var gcx = GcxFile.FromJson(json);
            using var stream = new MemoryStream();
            GcxFile.Write(stream, gcx);
            RequireWorkspace().Replace(fileNode.WorkspacePath, stream.ToArray());
            Log.Information("[GCX] Restored '{TargetPath}' from '{SourcePath}'.", fileNode.FullPath, file.Path.LocalPath);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Restore GCX", exception);
        }
    }

    private async Task DumpMapJsonCoreAsync(FileNode fileNode)
    {
        var suggestedName = $"{Path.GetFileNameWithoutExtension(fileNode.WorkspacePath.FileName)}.mapdoc.json";
        var target = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Map JSON",
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType("Map Document") { Patterns = ["*.mapdoc.json"] }
            ]
        });

        if (_cancellationToken.IsCancellationRequested || target == null)
        {
            return;
        }

        try
        {
            var workspace = RequireWorkspace();
            var geomPath = FindPairedGeomPath(workspace, fileNode.WorkspacePath);
            var sources = new MapDocumentSources
            {
                Gcx = ToWorkspaceRelativeSource(workspace, fileNode.WorkspacePath),
                Geom = ToWorkspaceRelativeSource(workspace, geomPath)
            };
            var document = MapDocumentBuilder.Build(
                workspace.ReadAllBytes(fileNode.WorkspacePath),
                workspace.ReadAllBytes(geomPath),
                sources,
                workspace.Endianness,
                SettingsStore.Current.IsMgs3);
            await File.WriteAllTextAsync(
                target.Path.LocalPath,
                MapJsonIO.Serialize(document),
                _cancellationToken);
            Log.Information(
                "[MapDoc] Map JSON for '{GcxPath}' and '{GeomPath}' written to '{TargetPath}'.",
                fileNode.FullPath,
                geomPath,
                target.Path.LocalPath);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Dump Map JSON", exception);
        }
    }

    private async Task RestoreMapJsonCoreAsync(FileNode fileNode)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Map JSON",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Map Document") { Patterns = ["*.mapdoc.json", "*.json"] }
            ]
        });

        var file = files.FirstOrDefault();
        if (_cancellationToken.IsCancellationRequested || file == null)
        {
            return;
        }

        try
        {
            var workspace = RequireWorkspace();
            var json = await File.ReadAllTextAsync(file.Path.LocalPath, _cancellationToken);
            var document = MapJsonIO.Deserialize(json);
            var gcxPath = ResolveWorkspaceSource(workspace, document.Sources.Gcx);
            var geomPath = ResolveWorkspaceSource(workspace, document.Sources.Geom);
            if (!SameWorkspacePath(gcxPath, fileNode.WorkspacePath))
            {
                throw new InvalidDataException(
                    $"This map document targets '{document.Sources.Gcx}'. Restore it from that GCX file's context menu.");
            }

            var result = MapDocumentApplier.Apply(document, workspace.Endianness, SettingsStore.Current.IsMgs3);
            _cancellationToken.ThrowIfCancellationRequested();
            workspace.Replace(geomPath, result.GeomBytes);
            workspace.Replace(gcxPath, result.GcxBytes);

            await _viewModel.LoadGeomFromWorkspacePathAsync(geomPath);
            await _viewModel.LoadGcxFromWorkspacePathAsync(gcxPath);
            Log.Information(
                "[MapDoc] Restored '{GcxPath}' and '{GeomPath}' from '{SourcePath}'.",
                gcxPath,
                geomPath,
                file.Path.LocalPath);
            MessageDialog.Info("Restore Map JSON", "Restored the GCX and GEOM map files.");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Restore Map JSON", exception);
        }
    }

    private async Task ExportGeomGltfCoreAsync(FileNode fileNode)
    {
        var suggestedName = $"{Path.GetFileNameWithoutExtension(fileNode.WorkspacePath.FileName)}.gltf";
        var target = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export GEOM Collision as glTF",
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType("glTF 2.0") { Patterns = ["*.gltf"] }
            ]
        });

        if (_cancellationToken.IsCancellationRequested || target == null)
        {
            return;
        }

        GeomFile? geometry = null;
        try
        {
            var workspace = RequireWorkspace();
            geometry = new GeomFile(workspace.OpenRead(fileNode.WorkspacePath), workspace.Endianness);
            await using var output = new FileStream(
                target.Path.LocalPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true);
            var summary = GeomMeshExchange.ExportGltf(geometry, output);
            await output.FlushAsync(_cancellationToken);
            Log.Information(
                "[GEOM] Exported {PrimitiveCount} collision primitives ({TriangleCount} triangles) from '{SourcePath}' to '{TargetPath}'.",
                summary.Primitives,
                summary.Triangles,
                fileNode.FullPath,
                target.Path.LocalPath);
            MessageDialog.Info(
                "Export GEOM glTF",
                $"Exported {summary.Blocks} Blender objects, {summary.Primitives} collision material groups, " +
                $"and {summary.Triangles} triangles.\n\n" +
                "In Blender, use File > Import > glTF 2.0. File > Open only accepts Blender project files.");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Export GEOM glTF", exception);
        }
        finally
        {
            geometry?.CloseStream();
        }
    }

    private async Task ImportGeomGltfPositionsCoreAsync(FileNode fileNode)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Position-Only GEOM glTF",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("glTF 2.0") { Patterns = ["*.gltf"] }
            ]
        });
        var file = files.FirstOrDefault();
        if (_cancellationToken.IsCancellationRequested || file == null)
        {
            return;
        }

        GeomFile? geometry = null;
        try
        {
            var workspace = RequireWorkspace();
            geometry = new GeomFile(workspace.OpenRead(fileNode.WorkspacePath), workspace.Endianness);
            var summary = GeomMeshExchange.ImportPositions(geometry, file.Path.LocalPath);
            using var compiled = new MemoryStream();
            geometry.Save(compiled, workspace.Endianness);
            _cancellationToken.ThrowIfCancellationRequested();
            workspace.Replace(fileNode.WorkspacePath, compiled.ToArray());
            geometry.CloseStream();
            geometry = null;

            await _viewModel.LoadGeomFromWorkspacePathAsync(fileNode.WorkspacePath);
            var warningText = summary.Warnings.Count == 0
                ? string.Empty
                : $"\n\nWarnings:\n{string.Join("\n", summary.Warnings.Take(5))}";
            Log.Information(
                "[GEOM] Imported {UpdatedVertexCount} position edits from '{SourcePath}' into '{TargetPath}' with {WarningCount} warnings.",
                summary.UpdatedVertices,
                file.Path.LocalPath,
                fileNode.FullPath,
                summary.Warnings.Count);
            MessageDialog.Info(
                "Import GEOM glTF Positions",
                $"Updated {summary.UpdatedVertices} GEOM vertices.{warningText}");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Import GEOM glTF Positions", exception);
        }
        finally
        {
            geometry?.CloseStream();
        }
    }

    private async Task ImportGeomGltfTopologyCoreAsync(FileNode fileNode)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Edited GEOM glTF Mesh",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("glTF 2.0") { Patterns = ["*.gltf"] }
            ]
        });
        var file = files.FirstOrDefault();
        if (_cancellationToken.IsCancellationRequested || file == null)
        {
            return;
        }

        GeomFile? geometry = null;
        try
        {
            var workspace = RequireWorkspace();
            geometry = new GeomFile(workspace.OpenRead(fileNode.WorkspacePath), workspace.Endianness);
            var summary = GeomMeshExchange.ImportTopology(geometry, file.Path.LocalPath);
            using var compiled = new MemoryStream();
            geometry.Save(compiled, workspace.Endianness);
            _cancellationToken.ThrowIfCancellationRequested();
            workspace.Replace(fileNode.WorkspacePath, compiled.ToArray());
            geometry.CloseStream();
            geometry = null;

            await _viewModel.LoadGeomFromWorkspacePathAsync(fileNode.WorkspacePath);
            var warningText = summary.Warnings.Count == 0
                ? string.Empty
                : $"\n\nWarnings:\n{string.Join("\n", summary.Warnings.Take(5))}";
            Log.Information(
                "[GEOM] Imported topology for {UpdatedBlockCount} blocks ({VertexCount} vertices, {TriangleCount} triangles) from '{SourcePath}' into '{TargetPath}' with {WarningCount} warnings.",
                summary.UpdatedBlocks,
                summary.Vertices,
                summary.Triangles,
                file.Path.LocalPath,
                fileNode.FullPath,
                summary.Warnings.Count);
            MessageDialog.Info(
                "Import GEOM glTF Mesh",
                $"Rebuilt {summary.UpdatedBlocks} GEOM blocks from {summary.Triangles} triangles.{warningText}");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Import GEOM glTF Mesh", exception);
        }
        finally
        {
            geometry?.CloseStream();
        }
    }

    private async Task CreateGeomFromGltfCoreAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Blender glTF Collision Mesh",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("glTF 2.0") { Patterns = ["*.gltf"] }
            ]
        });
        var source = files.FirstOrDefault();
        if (_cancellationToken.IsCancellationRequested || source == null)
        {
            return;
        }

        var cellSize = await PickNewGeomCellSizeAsync();
        if (_cancellationToken.IsCancellationRequested || cellSize == null)
        {
            return;
        }

        var target = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save New GEOM in Workspace",
            SuggestedFileName = $"{Path.GetFileNameWithoutExtension(source.Name)}.geom",
            FileTypeChoices =
            [
                new FilePickerFileType("MGO2 GEOM") { Patterns = ["*.geom"] }
            ]
        });
        if (_cancellationToken.IsCancellationRequested || target == null)
        {
            return;
        }

        try
        {
            var workspace = RequireWorkspace();
            using var compiled = new MemoryStream();
            var summary = GeomMeshExchange.ImportAsNew(
                source.Path.LocalPath,
                compiled,
                cellSize.Value,
                endianness: workspace.Endianness);
            _cancellationToken.ThrowIfCancellationRequested();
            var targetPath = WorkspacePath.Physical(target.Path.LocalPath);
            workspace.Replace(targetPath, compiled.ToArray());
            _viewModel.RefreshWorkspaceTree();
            await _viewModel.LoadGeomFromWorkspacePathAsync(targetPath);

            var warningText = summary.Warnings.Count == 0
                ? string.Empty
                : $"\n\nWarnings:\n{string.Join("\n", summary.Warnings.Take(5))}";
            Log.Information(
                "[GEOM] Created '{TargetPath}' from '{SourcePath}' with {BlockCount} blocks, {TriangleCount} triangles, and {MaterialCount} collision materials.",
                target.Path.LocalPath,
                source.Path.LocalPath,
                summary.Blocks,
                summary.Triangles,
                summary.Materials);
            MessageDialog.Info(
                "Create GEOM from glTF",
                $"Created {summary.Blocks} GEOM blocks from {summary.Triangles} triangles " +
                $"using a {summary.CellSize:0.##}-unit grid.{warningText}");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Create GEOM from glTF", exception);
        }
    }

    private async Task TransportGeomEffectsCoreAsync(FileNode fileNode)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Source GEOM to Transport Effects From",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MGO2 GEOM") { Patterns = ["*.geom"] }
            ]
        });
        var file = files.FirstOrDefault();
        if (_cancellationToken.IsCancellationRequested || file == null || !File.Exists(file.Path.LocalPath))
        {
            return;
        }

        GeomFile? target = null;
        GeomFile? source = null;
        try
        {
            var workspace = RequireWorkspace();
            target = new GeomFile(workspace.OpenRead(fileNode.WorkspacePath), workspace.Endianness);
            source = new GeomFile(
                new FileStream(file.Path.LocalPath, FileMode.Open, FileAccess.Read),
                workspace.Endianness);

            var transported = target.TransportEffectsFrom(source);
            using var compiled = new MemoryStream();
            target.Save(compiled, workspace.Endianness);
            _cancellationToken.ThrowIfCancellationRequested();
            workspace.Replace(fileNode.WorkspacePath, compiled.ToArray());
            target.CloseStream();
            target = null;

            await _viewModel.LoadGeomFromWorkspacePathAsync(fileNode.WorkspacePath);
            Log.Information(
                "[GEOM] Transported {EffectCount} effects from '{SourcePath}' into '{TargetPath}'.",
                transported,
                file.Path.LocalPath,
                fileNode.FullPath);
            MessageDialog.Info(
                "Transport GEOM Effects",
                $"Transported {transported} effects from '{Path.GetFileName(file.Path.LocalPath)}' into {fileNode.Name}.");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Transport GEOM Effects", exception);
        }
        finally
        {
            target?.CloseStream();
            source?.CloseStream();
        }
    }

    private async Task RestoreArchiveFromFolderCoreAsync(FileNode fileNode)
    {
        var inputFolder = await PickOutputFolderAsync("Select Folder to Rebuild Archive From");
        if (inputFolder == null)
        {
            return;
        }

        try
        {
            var workspace = RequireWorkspace();
            var result = ArchiveRestoreService.BuildFromFolder(
                fileNode.WorkspacePath.FileName,
                inputFolder,
                workspace.Endianness);
            _cancellationToken.ThrowIfCancellationRequested();
            workspace.Replace(fileNode.WorkspacePath, result.Bytes);
            _viewModel.RefreshWorkspaceTree();
            Log.Information(
                "[Archive] Rebuilt '{ArchivePath}' from '{Folder}' with {EntryCount} entries.",
                fileNode.FullPath,
                inputFolder,
                result.EntryCount);
            MessageDialog.Info(
                "Restore from Folder",
                $"Rebuilt {fileNode.Name} from {result.EntryCount} files.");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Restore Archive from Folder", exception);
        }
    }

    private async Task RestoreTxnFromFolderCoreAsync(FileNode fileNode)
    {
        var inputFolder = await PickOutputFolderAsync("Select Folder of DDS Textures to Rebuild TXN");
        if (inputFolder == null)
        {
            return;
        }

        try
        {
            var workspace = RequireWorkspace();
            var service = new TxnTextureEditorService(fileNode.WorkspacePath, workspace);
            var summary = service.RestoreFromFolder(inputFolder);
            _cancellationToken.ThrowIfCancellationRequested();
            service.Save();
            _viewModel.RefreshWorkspaceTree();

            var skippedText = summary.Skipped.Count == 0
                ? string.Empty
                : $"\n\nSkipped {summary.Skipped.Count}:\n{string.Join("\n", summary.Skipped.Take(5))}";
            Log.Information(
                "[TXN] Rebuilt '{TxnPath}' from '{Folder}' with {Restored}/{Total} textures ({Skipped} skipped).",
                fileNode.FullPath,
                inputFolder,
                summary.Restored,
                summary.Total,
                summary.Skipped.Count);
            MessageDialog.Info(
                "Restore from Folder",
                $"Rebuilt {fileNode.Name} from {summary.Restored} textures.{skippedText}");
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure("Restore TXN from Folder", exception);
        }
    }

    private async Task<float?> PickNewGeomCellSizeAsync()
    {
        var input = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 100_000,
            Increment = 10,
            Value = 100,
            FormatString = "0.##",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var create = new Button { Content = "Create", Width = 90 };
        var cancel = new Button { Content = "Cancel", Width = 90 };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(create);
        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = "Radix cell size",
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = "100 units matches the common MGO2 grid. Increase it if a very large mesh would create too many cells.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        panel.Children.Add(input);
        panel.Children.Add(buttons);
        var dialog = new Window
        {
            Title = "New GEOM Grid",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };
        float? result = null;
        create.Click += (_, _) =>
        {
            if (input.Value is { } value)
            {
                result = (float)value;
                dialog.Close();
            }
        };
        cancel.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(_owner);
        return result;
    }

    private WorkspacePath FindPairedGeomPath(IWorkspaceCatalog workspace, WorkspacePath gcxPath)
    {
        var snapshot = workspace.Snapshot
            ?? throw new InvalidOperationException("The workspace has not been scanned.");
        var candidates = snapshot.WithExtension(".geom")
            .Select(file => file.Path)
            .Where(path => IsSameLogicalDirectory(gcxPath, path))
            .OrderBy(path => path.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new FileNotFoundException(
                $"No GEOM file was found beside '{gcxPath.FileName}'.");
        }

        var gcxStem = Path.GetFileNameWithoutExtension(gcxPath.FileName);
        var sameStem = candidates.FirstOrDefault(path =>
            Path.GetFileNameWithoutExtension(path.FileName)
                .Equals(gcxStem, StringComparison.OrdinalIgnoreCase));
        if (sameStem != null)
        {
            return sameStem;
        }

        if (!string.IsNullOrWhiteSpace(_viewModel.CollisionEditor.GeomPath))
        {
            var loaded = WorkspacePath.ParseLegacy(_viewModel.CollisionEditor.GeomPath);
            var current = candidates.FirstOrDefault(candidate => SameWorkspacePath(candidate, loaded));
            if (current != null)
            {
                return current;
            }
        }
        return candidates[0];
    }

    private static bool IsSameLogicalDirectory(WorkspacePath left, WorkspacePath right)
    {
        if (left.IsArchiveEntry != right.IsArchiveEntry)
        {
            return false;
        }
        if (left.IsArchiveEntry)
        {
            return left.PhysicalPath.Equals(right.PhysicalPath, StringComparison.OrdinalIgnoreCase) &&
                GetArchiveDirectory(left.ArchiveEntryName)
                    .Equals(GetArchiveDirectory(right.ArchiveEntryName), StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(
            Path.GetDirectoryName(left.PhysicalPath),
            Path.GetDirectoryName(right.PhysicalPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetArchiveDirectory(string? entryName)
    {
        var normalized = (entryName ?? string.Empty).Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');
        return separator < 0 ? string.Empty : normalized[..separator];
    }

    private static string ToWorkspaceRelativeSource(IWorkspaceCatalog workspace, WorkspacePath path)
    {
        var physical = Path.GetRelativePath(workspace.RootPath, path.PhysicalPath).Replace('\\', '/');
        return path.IsArchiveEntry
            ? $"{physical}::{path.ArchiveEntryName!.Replace('\\', '/')}"
            : physical;
    }

    private static WorkspacePath ResolveWorkspaceSource(IWorkspaceCatalog workspace, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidDataException("A map source path is empty.");
        }
        var separator = source.IndexOf("::", StringComparison.Ordinal);
        var physicalPart = separator < 0 ? source : source[..separator];
        var physicalPath = Path.GetFullPath(
            Path.Combine(workspace.RootPath, physicalPart.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(workspace.RootPath, physicalPath);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Map source '{source}' escapes the workspace root.");
        }

        var path = separator < 0
            ? WorkspacePath.Physical(physicalPath)
            : WorkspacePath.ArchiveEntry(physicalPath, source[(separator + 2)..]);
        if (workspace.Snapshot?.TryGetFile(path, out _) != true)
        {
            throw new FileNotFoundException($"Map source '{source}' was not found in the workspace.");
        }
        return path;
    }

    private static bool SameWorkspacePath(WorkspacePath left, WorkspacePath right)
    {
        return left.PhysicalPath.Equals(right.PhysicalPath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.ArchiveEntryName, right.ArchiveEntryName, StringComparison.Ordinal);
    }

    private IWorkspaceCatalog RequireWorkspace()
    {
        return _viewModel.Workspace ?? throw new InvalidOperationException("No workspace is open.");
    }

    private async Task<string?> PickOutputFolderAsync(string title)
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return _cancellationToken.IsCancellationRequested
            ? null
            : folders.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task ExecuteAsync(string operation, Func<Task> action)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await action();
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            PresentFailure(operation, exception);
        }
    }

    private static void PresentFailure(string operation, Exception exception)
    {
        Log.Error(exception, "{Operation} failed", operation);
        MessageDialog.Error(operation, exception.Message);
    }
}
