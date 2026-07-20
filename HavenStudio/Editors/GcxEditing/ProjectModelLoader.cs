using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Formats.Mdn;
using HavenStudio.Rendering;
using HavenStudio.Services.Workspace;
using Serilog;

namespace HavenStudio.Editors.GcxEditing;

public sealed record ProjectModelLoadProgress(double Value, string Text);

public enum ProjectModelLoadStatus
{
    Completed,
    Cancelled,
    NoModels
}

public sealed class ProjectModelLoader : IDisposable
{
    private static readonly ILogger Log = Serilog.Log.ForContext<ProjectModelLoader>();

    private CancellationTokenSource? _activeLoad;
    private long _generation;
    private bool _disposed;

    public async Task<ProjectModelLoadStatus> LoadAsync(
        GcxModelReferences references,
        IWorkspaceCatalog workspace,
        WorkspaceSnapshot snapshot,
        Func<IReadOnlyList<MdnSceneBatch>, CancellationToken, Task> publishAsync,
        IProgress<ProjectModelLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(publishAsync);

        Cancel();
        var generation = Interlocked.Increment(ref _generation);
        var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeLoad = loadCancellation;
        var token = loadCancellation.Token;

        try
        {
            var requiredHashes = references.RequiredModelHashes().ToHashSet();
            if (requiredHashes.Count == 0)
            {
                return ProjectModelLoadStatus.NoModels;
            }

            var batches = await Task.Run(
                () => PrepareBatches(references, requiredHashes, workspace, snapshot, progress, token),
                token);
            token.ThrowIfCancellationRequested();
            if (generation != Volatile.Read(ref _generation))
            {
                return ProjectModelLoadStatus.Cancelled;
            }

            await publishAsync(batches, token);
            token.ThrowIfCancellationRequested();
            return generation == Volatile.Read(ref _generation)
                ? ProjectModelLoadStatus.Completed
                : ProjectModelLoadStatus.Cancelled;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return ProjectModelLoadStatus.Cancelled;
        }
        finally
        {
            if (ReferenceEquals(_activeLoad, loadCancellation))
            {
                _activeLoad = null;
            }

            loadCancellation.Dispose();
        }
    }

    public void Cancel()
    {
        Interlocked.Increment(ref _generation);
        _activeLoad?.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
        _activeLoad?.Dispose();
        _activeLoad = null;
    }

    private static List<MdnSceneBatch> PrepareBatches(
        GcxModelReferences references,
        IReadOnlySet<uint> requiredHashes,
        IWorkspaceCatalog workspace,
        WorkspaceSnapshot snapshot,
        IProgress<ProjectModelLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var pathByHash = BuildPathLookup(requiredHashes, snapshot, progress, cancellationToken);
        var assetsByPath = new Dictionary<WorkspacePath, ModelAsset>();
        var textureResolver = new MdnTextureResolver();
        var batches = new List<MdnSceneBatch>();
        var total = references.StageModelHashes.Count + references.PlacedModels.Count;
        var processed = 0;

        foreach (var hash in references.StageModelHashes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ProjectModelLoadProgress(
                total > 0 ? (double)processed / total : 1,
                $"Loading stage models {processed + 1}/{total}"));
            processed++;

            if (!TryGetAsset(
                    hash,
                    pathByHash,
                    assetsByPath,
                    textureResolver,
                    workspace,
                    cancellationToken,
                    out var asset))
            {
                continue;
            }

            batches.Add(new MdnSceneBatch(
                asset.Document,
                MdnSceneRenderer.BuildModels(asset.Document),
                asset.Textures,
                Placement: null));
        }

        foreach (var placed in references.PlacedModels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ProjectModelLoadProgress(
                total > 0 ? (double)processed / total : 1,
                $"Loading placed objects {processed + 1}/{total}"));
            processed++;

            AddPlacedBatch(placed.ModelHash, "_placed");
            foreach (var additionalHash in placed.AdditionalModelHashes)
            {
                AddPlacedBatch(additionalHash, "_placed_add");
            }

            void AddPlacedBatch(uint hash, string suffix)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetAsset(
                        hash,
                        pathByHash,
                        assetsByPath,
                        textureResolver,
                        workspace,
                        cancellationToken,
                        out var asset))
                {
                    return;
                }

                batches.Add(new MdnSceneBatch(
                    asset.Document,
                    MdnSceneRenderer.BuildModels(asset.Document, suffix, placed.Position, placed.Rotation),
                    asset.Textures,
                    placed));
            }
        }

        progress?.Report(new ProjectModelLoadProgress(1, $"Prepared {batches.Count} model group(s)"));
        return batches;
    }

    private static Dictionary<uint, WorkspacePath> BuildPathLookup(
        IReadOnlySet<uint> requiredHashes,
        WorkspaceSnapshot snapshot,
        IProgress<ProjectModelLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var paths = new Dictionary<uint, WorkspacePath>();
        var modelFiles = snapshot.WithExtension(".mdn").ToList();
        for (var index = 0; index < modelFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workspaceFile = modelFiles[index];
            var name = Path.GetFileNameWithoutExtension(workspaceFile.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                var hash = Utils.String.HashString(name);
                if (requiredHashes.Contains(hash))
                {
                    paths.TryAdd(hash, workspaceFile.Path);
                }
            }

            if (index % 200 == 0)
            {
                progress?.Report(new ProjectModelLoadProgress(
                    modelFiles.Count > 0 ? (double)index / modelFiles.Count : 1,
                    $"Scanning {index}/{modelFiles.Count}"));
            }
        }

        return paths;
    }

    public static IReadOnlyDictionary<uint, WorkspacePath> BuildPathLookup(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var paths = new Dictionary<uint, WorkspacePath>();
        foreach (var workspaceFile in snapshot.WithExtension(".mdn"))
        {
            var name = Path.GetFileNameWithoutExtension(workspaceFile.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                paths.TryAdd(Utils.String.HashString(name), workspaceFile.Path);
            }
        }
        return paths;
    }

    private static bool TryGetAsset(
        uint hash,
        IReadOnlyDictionary<uint, WorkspacePath> pathByHash,
        IDictionary<WorkspacePath, ModelAsset> assetsByPath,
        MdnTextureResolver textureResolver,
        IWorkspaceCatalog workspace,
        CancellationToken cancellationToken,
        out ModelAsset asset)
    {
        asset = null!;
        if (hash == 0 || !pathByHash.TryGetValue(hash, out var path))
        {
            return false;
        }

        if (assetsByPath.TryGetValue(path, out var cachedAsset))
        {
            asset = cachedAsset;
            return true;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = workspace.OpenRead(path);
            var document = MdnFile.Read(stream);
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyDictionary<uint, ResolvedTexture> textures;
            try
            {
                textures = textureResolver.TryResolveAll(document, workspace, out var resolvedTextures)
                    ? resolvedTextures
                    : new Dictionary<uint, ResolvedTexture>();
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to resolve textures for project model {ModelPath}", path);
                textures = new Dictionary<uint, ResolvedTexture>();
            }

            asset = new ModelAsset(document, textures);
            assetsByPath[path] = asset;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load project model {ModelPath}", path);
            return false;
        }
    }

    private sealed record ModelAsset(
        Mdn Document,
        IReadOnlyDictionary<uint, ResolvedTexture> Textures);
}
