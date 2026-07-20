using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia3DControl;
using Avalonia3DControl.Core.Cameras;
using HavenStudio.Formats.Mdn;
using HavenStudio.Rendering;
using HavenStudio.Services.Workspace;
using OpenTK.Mathematics;
using Serilog;

namespace HavenStudio.Windows;

public partial class ModelViewerWindow : Window
{
    private static readonly ILogger _log = Log.ForContext<ModelViewerWindow>();

    private OpenGL3DControl _viewport;
    private readonly CancellationTokenSource _loadCancellation = new();
    private Vector3? _pendingCenter;
    private float _pendingRadius;

    public ModelViewerWindow()
    {
        InitializeComponent();
        _viewport = this.FindControl<OpenGL3DControl>("ViewportControl")!;

        // Use orbital controls, but still frame the model like the scene.
        _viewport.CameraMode = CameraMode.Orbital;
        _viewport.CameraSpeedScale = 0.5f;
        _viewport.SetZoomLimits(0.000001f, 1_000_000f);
        _viewport.SetZoomScale(0.005f);
        _viewport.AllowHotkeys = false;
        _viewport.OpenGlInitialized += OnViewportOpenGlInitialized;
        Closed += OnWindowClosed;
    }

    public static void Open(WorkspacePath filePath, IWorkspaceCatalog workspace)
    {
        var window = new ModelViewerWindow();
        window.Title = $"Model Viewer - {filePath.FileName}";
        window.Show();
        window.Activate();
        _ = window.LoadModelAsync(filePath, workspace);
    }

    private async Task LoadModelAsync(WorkspacePath filePath, IWorkspaceCatalog workspace)
    {
        var cancellationToken = _loadCancellation.Token;
        try
        {
            var batch = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = workspace.OpenRead(filePath);
                var document = MdnFile.Read(stream);
                cancellationToken.ThrowIfCancellationRequested();
                return MdnSceneRenderer.PrepareBatch(document, workspace);
            }, cancellationToken);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var model in batch.Models)
                {
                    _viewport.Scene.Models.Add(model);
                }

                CenterCameraOnMdn(batch.Document);
                MdnSceneRenderer.ApplyTextures(_viewport, batch.Document, batch.Models, batch.Textures);
                _viewport.RequestNextFrameRendering();
            });

            _log.Information("[ModelViewer] Loaded '{FilePath}' with {Count} model(s).", filePath, batch.Models.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[ModelViewer] Failed to load '{FilePath}'", filePath);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Closed -= OnWindowClosed;
        _viewport.OpenGlInitialized -= OnViewportOpenGlInitialized;
        _loadCancellation.Cancel();
        _loadCancellation.Dispose();
    }

    private void CenterCameraOnMdn(Mdn mdn)
    {
        var bounds = mdn.Bounds;
        if (bounds is null) return;

        var min = new Vector3(bounds.MinX, bounds.MinY, bounds.MinZ);
        var max = new Vector3(bounds.MaxX, bounds.MaxY, bounds.MaxZ);
        var center = (min + max) * 0.5f;
        var size = max - min;
        var radius = Math.Max(size.X, Math.Max(size.Y, size.Z)) * 0.5f;
        if (radius <= 0.001f) radius = 1.0f;

        _pendingCenter = center;
        _pendingRadius = radius;
        TryFocusPendingBounds();
    }

    private void OnViewportOpenGlInitialized()
    {
        TryFocusPendingBounds();
    }

    private void TryFocusPendingBounds()
    {
        if (!_pendingCenter.HasValue || _pendingRadius <= 0f)
        {
            return;
        }

        if (!_viewport.IsOpenGlInitialized)
        {
            return;
        }

        var camera = _viewport.Scene.Camera;
        camera.Mode = ProjectionMode.Perspective;
        camera.FieldOfView = MathHelper.DegreesToRadians(45f);
        camera.ViewLock = ViewLockMode.None;
        camera.Target = _pendingCenter.Value;
        camera.Up = Vector3.UnitY;

        _viewport.FocusOnBounds(_pendingCenter.Value, _pendingRadius, 1.5f);
        _pendingCenter = null;
        _pendingRadius = 0f;
    }
}
