using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia3DControl.Core.Cameras;
using Avalonia3DControl.Core.Lighting;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Materials;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Avalonia3DControl.Rendering.OpenGL;

/// <summary>
/// Camera-dependent Haven Studio MGS4 renderer update directional shadow-buffer preview.
///
/// The MGS4 debug ELFs show a view-volume-driven shadow path with persistent
/// caster lists and separate ShadowProjection/TSM/TSM2 state. Haven reconstructs
/// the inlined MakeTSMTransform geometry (centre line, support lines, projection
/// point q and trapezoidal projective matrix), with an orthographic fallback only
/// for degenerate camera/light configurations.
///
/// Performance rules:
/// - no LINQ or managed allocation in the navigation path;
/// - no complete-stage signature or AABB rebuild per frame;
/// - local packet bounds are computed once, then only eight corners are transformed;
/// - a persistent X/Z spatial grid avoids scanning every stage packet;
/// - shadow updates are throttled and protected by draw/triangle budgets;
/// - the FBO and textures are created once and reused.
/// </summary>
public sealed class ShadowMapRenderer : IDisposable
{
    public const int DefaultResolution = 8192;  // 4096 over a large TSM extent still read as mush/acne at the ranges needed to cover the map. 8192 halves world-units-per-texel (e.g. 100k range -> 12 u/texel, as crisp as 50k/4096 was) so a moderate range gives BOTH coverage and 2006-style crisp cast shadows. Needs GPU max texture size >= 8192 (clamped at runtime).

    private const int MinimumResolution = 512;
    private const int MaximumResolution = 8192;  // raised with DefaultResolution; runtime still clamps to the GPU's max texture size
    private const float DefaultShadowDistance = 50000.0f; // ENGINE-VERIFIED: default max_shadow_range from DG_ResetViewportSystem (+984 @0xDD35C, ELF 2739) = 50000. 50k/4096 = 12.2 u/texel. Per-stage override lives in DM_SetShadowRange (input x 5248, scenerio.gcx) when present.
    private const float SpatialCellSize = 20000.0f;
    private const float FootprintPaddingFactor = 0.075f;
    private const float MinimumFootprintPadding = 100.0f;
    private const long MaximumSubmittedIndices = 14_400_000; // 4.8M triangles - sized for the full caster set
    private const int MaximumShadowDrawCalls = 4096;  // covers every registered caster (2210 on sm_dd) with headroom
    private const int MaximumSpatialCellsPerQuery = 4096;
    private const int ShadowTextureUnit = 10;
    private const double MaximumUpdateRate = 8.0;
    private static readonly long MinimumUpdateTicks =
        (long)(Stopwatch.Frequency / MaximumUpdateRate);

    private sealed class CasterEntry
    {
        public CasterEntry(Model3D model)
        {
            Model = model;
        }

        public Model3D Model { get; }
        public Vector3 LocalMinimum;
        public Vector3 LocalMaximum;
        public Vector3 Minimum;
        public Vector3 Maximum;
        public Vector3 Center;
        public float Radius;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
        public int PositionCount;
        public int VertexDataCount;
        public int IndexCount;
        public int TextureId;
        public float DistanceSquared;

        public bool GeometryOrMaterialChanged =>
            Model.Positions.Length != PositionCount ||
            Model.Vertices.Length != VertexDataCount ||
            Model.Indices.Length != IndexCount ||
            Model.TextureId != TextureId;

        public bool TransformChanged =>
            Model.Position != Position ||
            Model.Rotation != Rotation ||
            Model.Scale != Scale;

        public bool InitializeBounds()
        {
            return RefreshLocalBounds() && RefreshWorldBounds();
        }

        public bool RefreshAfterEdit()
        {
            if (GeometryOrMaterialChanged && !RefreshLocalBounds())
            {
                return false;
            }
            return RefreshWorldBounds();
        }

        private bool RefreshLocalBounds()
        {
            var localMinimum = new Vector3(float.MaxValue);
            var localMaximum = new Vector3(float.MinValue);
            var found = false;

            var positions = Model.Positions;
            for (var offset = 0; offset + 2 < positions.Length; offset += 3)
            {
                var point = new Vector3(positions[offset], positions[offset + 1], positions[offset + 2]);
                if (!IsFinite(point))
                {
                    continue;
                }
                localMinimum = Vector3.ComponentMin(localMinimum, point);
                localMaximum = Vector3.ComponentMax(localMaximum, point);
                found = true;
            }

            if (!found)
            {
                var vertices = Model.Vertices;
                for (var offset = 0; offset + 2 < vertices.Length; offset += 6)
                {
                    var point = new Vector3(vertices[offset], vertices[offset + 1], vertices[offset + 2]);
                    if (!IsFinite(point))
                    {
                        continue;
                    }
                    localMinimum = Vector3.ComponentMin(localMinimum, point);
                    localMaximum = Vector3.ComponentMax(localMaximum, point);
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            LocalMinimum = localMinimum;
            LocalMaximum = localMaximum;
            PositionCount = Model.Positions.Length;
            VertexDataCount = Model.Vertices.Length;
            IndexCount = Model.Indices.Length;
            TextureId = Model.TextureId;
            return true;
        }

        private bool RefreshWorldBounds()
        {
            var modelMatrix = Model.GetModelMatrix();
            var worldMinimum = new Vector3(float.MaxValue);
            var worldMaximum = new Vector3(float.MinValue);

            for (var corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    (corner & 1) == 0 ? LocalMinimum.X : LocalMaximum.X,
                    (corner & 2) == 0 ? LocalMinimum.Y : LocalMaximum.Y,
                    (corner & 4) == 0 ? LocalMinimum.Z : LocalMaximum.Z);
                var world = Vector3.TransformPosition(local, modelMatrix);
                if (!IsFinite(world))
                {
                    return false;
                }
                worldMinimum = Vector3.ComponentMin(worldMinimum, world);
                worldMaximum = Vector3.ComponentMax(worldMaximum, world);
            }

            Minimum = worldMinimum;
            Maximum = worldMaximum;
            Center = (Minimum + Maximum) * 0.5f;
            Radius = MathF.Max(1.0f, (Maximum - Minimum).Length * 0.5f);
            Position = Model.Position;
            Rotation = Model.Rotation;
            Scale = Model.Scale;
            return true;
        }
    }

    private readonly int _requestedResolution;
    private float _shadowDistance;
    private readonly List<CasterEntry> _casters = new();
    private readonly List<CasterEntry> _candidateCasters = new();
    private readonly List<CasterEntry> _visibleCasters = new();
    private readonly Dictionary<long, List<CasterEntry>> _spatialBins = new();
    private readonly int[] _savedViewport = new int[4];

    private int _resolution;
    private int _framebuffer;
    private int _depthTexture;
    private int _colorTexture;
    private bool _initialized;
    private bool _permanentlyDisabled;
    private string? _disabledReason;

    private bool _sceneCacheDirty = true;
    private bool _transformCacheDirty = true;
    private bool _sceneStateChanged = true;
    private int _cachedSourceModelCount = -1;
    private float _maximumCasterRadius;
    private bool _hasCachedMap;
    private bool _loggedSpatialFallback;
    private long _lastUpdateTimestamp;

    private Vector3 _lastCameraPosition;
    private Vector3 _lastCameraForward;
    private float _lastCameraFov;
    private float _lastCameraAspect;
    private Vector3 _lastLightDirection;

    private int _candidateCount;
    private int _visibleDrawCount;
    private int _lastSubmittedTriangles;
    private double _lastCullMilliseconds;
    private double _lastDrawMilliseconds;
    private int _updateCounter;
    private bool _lastProjectionWasTsm;

    public ShadowMapRenderer(
        int resolution = DefaultResolution,
        float shadowDistance = DefaultShadowDistance)
    {
        _requestedResolution = Math.Clamp(resolution, MinimumResolution, MaximumResolution);
        _resolution = _requestedResolution;
        _shadowDistance = MathF.Max(1000.0f, shadowDistance);
    }

    /// <summary>
    /// Live shadow coverage range, driven by the toolbar slider. Shorter =
    /// denser texels = sharper silhouettes (range/4096 world units per texel);
    /// longer = wider coverage but softer. The engine equivalent is
    /// DM_SetShadowRange (input x 5248) from the stage GCX.
    /// </summary>
    public float ShadowDistance
    {
        get => _shadowDistance;
        set
        {
            var clamped = MathF.Max(1000.0f, value);
            if (MathF.Abs(clamped - _shadowDistance) < 0.5f)
            {
                return;
            }
            _shadowDistance = clamped;
            _transformCacheDirty = true;
            _hasCachedMap = false;
        }
    }

    public int DepthTexture => _depthTexture;
    public Matrix4 LightSpaceMatrix { get; private set; } = Matrix4.Identity;
    public Matrix4 ShadowProjection { get; private set; } = Matrix4.Identity;
    /// <summary>Console diagnostics for the engine-aligned TSM path.</summary>
    public static bool EnableTsmDiagnostics { get; set; } = true;

    /// <summary>
    /// File diagnostics ("HavenStudio-shadows.log" next to the executable).
    /// Console output is invisible in a GUI build, so decisions are logged to a
    /// file, deduplicated (only when the message changes) and hard-capped.
    /// </summary>
    private static class ShadowLog
    {
        private static readonly object Sync = new();
        private static string? _last;
        private static int _budget = 300;
        private static bool _header;

        public static void Log(string message)
        {
            if (!EnableTsmDiagnostics)
            {
                return;
            }
            lock (Sync)
            {
                if (message == _last || _budget <= 0)
                {
                    return;
                }
                _last = message;
                _budget--;
                try
                {
                    var path = System.IO.Path.Combine(AppContext.BaseDirectory, "HavenStudio-shadows.log");
                    if (!_header)
                    {
                        _header = true;
                        System.IO.File.AppendAllText(path,
                            $"{Environment.NewLine}==== shadows session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===={Environment.NewLine}");
                    }
                    System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                }
                catch
                {
                }
            }
        }
    }

    public Matrix4 ShadowProjectionTsm { get; private set; } = Matrix4.Identity;
    public Matrix4 ShadowProjectionTsm2 { get; private set; } = Matrix4.Identity;
    public Vector4 ShadowProjectionRange { get; private set; } = Vector4.Zero;
    public Vector3 LightDirection { get; private set; } = new(-1.0f, -1.0f, -1.0f);
    public bool HasShadowMap => !_permanentlyDisabled && _hasCachedMap && _depthTexture != 0;
    public string? DisabledReason => _disabledReason;

    public void InvalidateScene()
    {
        _sceneCacheDirty = true;
        _transformCacheDirty = true;
        _sceneStateChanged = true;
    }

    public void InvalidateTransforms()
    {
        _transformCacheDirty = true;
        _sceneStateChanged = true;
    }

    public bool Render(
        Camera camera,
        IReadOnlyList<Model3D> models,
        IReadOnlyList<Light> lights,
        ModelRenderer modelRenderer,
        int depthShaderProgram)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(lights);
        ArgumentNullException.ThrowIfNull(modelRenderer);

        if (_permanentlyDisabled || depthShaderProgram == 0)
        {
            return false;
        }

        // Let the normal colour pass finish any buffer upload first. This scan only
        // occurs after an explicit scene/transform invalidation, never while merely
        // navigating with a clean scene.
        if ((_sceneCacheDirty || _transformCacheDirty) && HasPendingGpuUpdates(models))
        {
            _sceneStateChanged = true;
            return _hasCachedMap;
        }

        if (!EnsureCasterCache(models))
        {
            _hasCachedMap = false;
            return false;
        }

        var lightDirection = ResolveLightDirection(lights);
        var cameraForward = SafeNormalize(camera.Target - camera.Position, -Vector3.UnitZ);
        var lightChanged = (lightDirection - _lastLightDirection).LengthSquared > 0.000001f;
        var cameraChanged = CameraChanged(camera, cameraForward);
        var updateRequired = _sceneStateChanged || _transformCacheDirty || lightChanged || cameraChanged;

        if (_hasCachedMap && !updateRequired)
        {
            return true;
        }

        var now = Stopwatch.GetTimestamp();
        if (_hasCachedMap && !lightChanged && now - _lastUpdateTimestamp < MinimumUpdateTicks)
        {
            return true;
        }

        if (_transformCacheDirty)
        {
            _sceneStateChanged |= RefreshChangedCasterBounds();
            _transformCacheDirty = false;
        }

        var cullTimer = Stopwatch.StartNew();
        if (!TryBuildShadowState(camera, cameraForward, lightDirection))
        {
            _hasCachedMap = false;
            return false;
        }
        cullTimer.Stop();
        _lastCullMilliseconds = cullTimer.Elapsed.TotalMilliseconds;

        try
        {
            EnsureInitialized();
            if (_permanentlyDisabled)
            {
                return false;
            }

            var drawTimer = Stopwatch.StartNew();
            RenderDepthPass(modelRenderer, depthShaderProgram);
            drawTimer.Stop();

            _lastDrawMilliseconds = drawTimer.Elapsed.TotalMilliseconds;
            _lastUpdateTimestamp = now;
            _lastCameraPosition = camera.Position;
            _lastCameraForward = cameraForward;
            _lastCameraFov = camera.FieldOfView;
            _lastCameraAspect = camera.AspectRatio;
            _lastLightDirection = lightDirection;
            LightDirection = lightDirection;
            _sceneStateChanged = false;
            _hasCachedMap = true;

            _updateCounter++;
            if (_updateCounter == 1 || _updateCounter % 120 == 0)
            {
                ShadowLog.Log(
                    $"[SHADOW] candidates {_candidateCount}/{_casters.Count} casters registered");
                Console.WriteLine(
                    $"[Haven Shadows] candidates {_candidateCount}/{_casters.Count}, " +
                    $"draws {_visibleDrawCount}, {_lastSubmittedTriangles:N0} triangles, " +
                    $"{_resolution}px, {(_lastProjectionWasTsm ? "TSM" : "ortho fallback")}, " +
                    $"cull {_lastCullMilliseconds:0.00} ms, " +
                    $"draw CPU {_lastDrawMilliseconds:0.00} ms.");
            }
            return true;
        }
        catch (Exception exception)
        {
            Disable($"Shadow preview disabled after an OpenGL error: {exception.Message}");
            return false;
        }
    }

    public void BindForLighting(int shaderProgram, bool enabled, float strength)
    {
        var active = enabled && HasShadowMap;
        SetBool(shaderProgram, "uShadowsEnabled", active);
        if (!active)
        {
            return;
        }

        SetMatrix(shaderProgram, "lightSpaceMatrix", LightSpaceMatrix);
        SetMatrix(shaderProgram, "uShadowProjection", ShadowProjection);
        SetMatrix(shaderProgram, "uShadowProjectionTsm", ShadowProjectionTsm);
        SetMatrix(shaderProgram, "uShadowProjectionTsm2", ShadowProjectionTsm2);
        SetVector4(shaderProgram, "uShadowProjectionRange", ShadowProjectionRange);
        SetVector3(shaderProgram, "uShadowLightDirection", LightDirection);
        SetFloat(shaderProgram, "uShadowStrength", Math.Clamp(strength, 0.0f, 1.0f));
        SetVector2(
            shaderProgram,
            "uShadowTexelSize",
            new Vector2(1.0f / _resolution, 1.0f / _resolution));

        GL.ActiveTexture(TextureUnit.Texture10);
        GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
        var samplerLocation = GL.GetUniformLocation(shaderProgram, "shadowMap");
        if (samplerLocation >= 0)
        {
            GL.Uniform1(samplerLocation, ShadowTextureUnit);
        }
        GL.ActiveTexture(TextureUnit.Texture0);
    }

    private bool EnsureCasterCache(IReadOnlyList<Model3D> models)
    {
        if (!_sceneCacheDirty && _cachedSourceModelCount == models.Count)
        {
            return _casters.Count > 0;
        }

        _casters.Clear();
        _candidateCasters.Clear();
        _visibleCasters.Clear();
        _spatialBins.Clear();
        _maximumCasterRadius = 0.0f;
        _cachedSourceModelCount = models.Count;
        var casterAssets = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < models.Count; index++)
        {
            var model = models[index];
            if (!IsPotentialShadowCaster(model))
            {
                continue;
            }

            var entry = new CasterEntry(model);
            if (entry.InitializeBounds())
            {
                _casters.Add(entry);
                _maximumCasterRadius = MathF.Max(_maximumCasterRadius, entry.Radius);
                var assetName = string.IsNullOrWhiteSpace(model.SourceAssetName)
                    ? "<standalone>"
                    : model.SourceAssetName;
                casterAssets.TryGetValue(assetName, out var packetCount);
                casterAssets[assetName] = packetCount + 1;
            }
        }

        if (_candidateCasters.Capacity < _casters.Count)
        {
            _candidateCasters.Capacity = _casters.Count;
        }
        if (_visibleCasters.Capacity < _casters.Count)
        {
            _visibleCasters.Capacity = _casters.Count;
        }
        RebuildSpatialBins();
        _sceneCacheDirty = false;
        _transformCacheDirty = false;
        _sceneStateChanged = true;
        Console.WriteLine(
            $"[Haven Shadows] Static cache: {_casters.Count} packets in {_spatialBins.Count} spatial cells.");
        if (casterAssets.Count > 0)
        {
            var summary = string.Join(", ", casterAssets
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key} ({pair.Value})"));
            Console.WriteLine($"[Haven Shadows] Caster assets: {summary}");
        }
        return _casters.Count > 0;
    }

    private bool RefreshChangedCasterBounds()
    {
        var changed = false;
        for (var index = 0; index < _casters.Count; index++)
        {
            var entry = _casters[index];
            if (!entry.GeometryOrMaterialChanged && !entry.TransformChanged)
            {
                continue;
            }

            if (entry.RefreshAfterEdit())
            {
                changed = true;
            }
        }

        if (changed)
        {
            RebuildSpatialBins();
        }
        return changed;
    }

    private void RebuildSpatialBins()
    {
        _spatialBins.Clear();
        _maximumCasterRadius = 0.0f;
        for (var index = 0; index < _casters.Count; index++)
        {
            var entry = _casters[index];
            _maximumCasterRadius = MathF.Max(_maximumCasterRadius, entry.Radius);
            var key = SpatialKey(ToCell(entry.Center.X), ToCell(entry.Center.Z));
            if (!_spatialBins.TryGetValue(key, out var bin))
            {
                bin = new List<CasterEntry>();
                _spatialBins.Add(key, bin);
            }
            bin.Add(entry);
        }
    }

    private bool TryBuildShadowState(
        Camera camera,
        Vector3 cameraForward,
        Vector3 lightDirection)
    {
        Span<Vector3> frustumCorners = stackalloc Vector3[8];
        if (!TryGetViewFrustumCorners(camera, cameraForward, frustumCorners))
        {
            return false;
        }

        var frustumCenter = Vector3.Zero;
        for (var index = 0; index < frustumCorners.Length; index++)
        {
            frustumCenter += frustumCorners[index];
        }
        frustumCenter /= frustumCorners.Length;

        var up = MathF.Abs(Vector3.Dot(lightDirection, Vector3.UnitY)) > 0.96f
            ? Vector3.UnitZ
            : Vector3.UnitY;
        var lightEye = frustumCenter + lightDirection * (_shadowDistance * 2.25f);
        var lightView = Matrix4.LookAt(lightEye, frustumCenter, up);

        var lightMinimum = new Vector3(float.MaxValue);
        var lightMaximum = new Vector3(float.MinValue);
        for (var index = 0; index < frustumCorners.Length; index++)
        {
            var point = Vector3.TransformPosition(frustumCorners[index], lightView);
            lightMinimum = Vector3.ComponentMin(lightMinimum, point);
            lightMaximum = Vector3.ComponentMax(lightMaximum, point);
        }

        var width = MathF.Max(1.0f, lightMaximum.X - lightMinimum.X);
        var height = MathF.Max(1.0f, lightMaximum.Y - lightMinimum.Y);
        var padding = MathF.Max(MinimumFootprintPadding, MathF.Max(width, height) * FootprintPaddingFactor);
        var left = lightMinimum.X - padding;
        var right = lightMaximum.X + padding;
        var bottom = lightMinimum.Y - padding;
        var top = lightMaximum.Y + padding;

        CollectSpatialCandidates(frustumCorners, lightDirection);
        _candidateCount = _candidateCasters.Count;
        _visibleCasters.Clear();
        var distanceLimit = _shadowDistance * 1.35f;
        var minimumZ = lightMinimum.Z;
        var maximumZ = lightMaximum.Z;

        for (var index = 0; index < _candidateCasters.Count; index++)
        {
            var entry = _candidateCasters[index];
            var model = entry.Model;
            if (!model.Visible || !IsPotentialShadowCaster(model))
            {
                continue;
            }

            var toCenter = entry.Center - frustumCenter;
            entry.DistanceSquared = toCenter.LengthSquared;
            var expandedDistance = distanceLimit + entry.Radius;
            if (entry.DistanceSquared > expandedDistance * expandedDistance)
            {
                continue;
            }

            var lightCenter = Vector3.TransformPosition(entry.Center, lightView);
            var radius = entry.Radius;
            if (lightCenter.X + radius < left || lightCenter.X - radius > right ||
                lightCenter.Y + radius < bottom || lightCenter.Y - radius > top)
            {
                continue;
            }

            minimumZ = MathF.Min(minimumZ, lightCenter.Z - radius);
            maximumZ = MathF.Max(maximumZ, lightCenter.Z + radius);
            _visibleCasters.Add(entry);
        }

        if (_visibleCasters.Count > 1)
        {
            // Diagnosed on sm_dd (v0.9.23 logs): 1547 candidates, hard cap 256,
            // nearest-first ordering - the large far walls whose long shadows
            // define the 2006 reference were consistently dropped in favour of
            // nearby crates. Order by footprint first (radius descending) so
            // architecture always casts; distance breaks ties.
            _visibleCasters.Sort(static (leftEntry, rightEntry) =>
            {
                var byRadius = rightEntry.Radius.CompareTo(leftEntry.Radius);
                return byRadius != 0
                    ? byRadius
                    : leftEntry.DistanceSquared.CompareTo(rightEntry.DistanceSquared);
            });
        }

        long submittedIndices = 0;
        _visibleDrawCount = 0;
        for (var index = 0; index < _visibleCasters.Count && _visibleDrawCount < MaximumShadowDrawCalls; index++)
        {
            var indexCount = _visibleCasters[index].Model.Indices.Length;
            if (_visibleDrawCount > 0 && submittedIndices + indexCount > MaximumSubmittedIndices)
            {
                break;
            }
            submittedIndices += indexCount;
            _visibleDrawCount++;
        }
        _lastSubmittedTriangles = (int)Math.Min(int.MaxValue, submittedIndices / 3);

        if (_visibleDrawCount <= 0)
        {
            return false;
        }

        // The debug ELF keeps three separate matrices in DG_VIEWPORT and inlines
        // MakeTSMTransform inside DG_DrawShadowBufferStage. Reconstruct that same
        // decomposition here: light view, center-line alignment, then the
        // trapezoidal projective warp. A conservative orthographic fallback remains
        // for degenerate camera/light configurations.
        if (!TryBuildTrapezoidalProjection(
                frustumCorners,
                lightView,
                lightMinimum,
                lightMaximum,
                minimumZ,
                maximumZ))
        {
            BuildOrthographicFallback(
                lightView,
                left,
                right,
                bottom,
                top,
                minimumZ,
                maximumZ);
        }

        return true;
    }

    private bool TryBuildTrapezoidalProjection(
        Span<Vector3> frustumCorners,
        Matrix4 lightView,
        Vector3 lightMinimum,
        Vector3 lightMaximum,
        float minimumZ,
        float maximumZ)
    {
        var nearCenter = Vector3.Zero;
        var farCenter = Vector3.Zero;
        for (var index = 0; index < frustumCorners.Length; index++)
        {
            var lightPoint = Vector3.TransformPosition(frustumCorners[index], lightView);
            if (index < 4) nearCenter += lightPoint;
            else farCenter += lightPoint;
        }
        nearCenter *= 0.25f;
        farCenter *= 0.25f;

        var centerDirection = new Vector2(
            farCenter.X - nearCenter.X,
            farCenter.Y - nearCenter.Y);
        if (!IsFinite(centerDirection) || centerDirection.LengthSquared < 0.0001f)
        {
            return false;
        }
        centerDirection.Normalize();

        // Rotate light post-perspective X/Y so the near-to-far frustum centre line
        // becomes the vertical trapezoid axis. The X translation is snapped to the
        // shadow texel grid to avoid sub-texel swimming during camera translation.
        var yAxis = centerDirection;
        var xAxis = new Vector2(yAxis.Y, -yAxis.X);
        var nearLineX = nearCenter.X * xAxis.X + nearCenter.Y * xAxis.Y;
        var farLineX = farCenter.X * xAxis.X + farCenter.Y * xAxis.Y;
        var centerLineX = (nearLineX + farLineX) * 0.5f;
        var lightWidth = MathF.Max(1.0f, lightMaximum.X - lightMinimum.X);
        var snap = MathF.Max(0.01f, lightWidth / _resolution);
        centerLineX = MathF.Round(centerLineX / snap) * snap;

        var alignment = new Matrix4();
        alignment.M11 = xAxis.X;
        alignment.M21 = xAxis.Y;
        alignment.M12 = yAxis.X;
        alignment.M22 = yAxis.Y;
        alignment.M33 = 1.0f;
        alignment.M41 = -centerLineX;
        alignment.M44 = 1.0f;
        var alignedView = lightView * alignment;

        var receiverTop = float.MaxValue;
        var receiverBase = float.MinValue;
        var alignedNearCenter = Vector3.Zero;
        var alignedFarCenter = Vector3.Zero;
        Span<Vector3> alignedCorners = stackalloc Vector3[8];
        for (var index = 0; index < frustumCorners.Length; index++)
        {
            var point = Vector3.TransformPosition(frustumCorners[index], alignedView);
            alignedCorners[index] = point;
            receiverTop = MathF.Min(receiverTop, point.Y);
            receiverBase = MathF.Max(receiverBase, point.Y);
            if (index < 4) alignedNearCenter += point;
            else alignedFarCenter += point;
        }
        alignedNearCenter *= 0.25f;
        alignedFarCenter *= 0.25f;

        var lambda = receiverBase - receiverTop;
        if (!float.IsFinite(lambda) || lambda < 1.0f)
        {
            return false;
        }

        // ENGINE-ALIGNED (build 2739, sessions 3-7). The focus point is the
        // CENTRE of the near/far anchor line: pool constant f1 = [-32692] = 0.5
        // (read @0x123430/0x1233C8) - NOT an arbitrary fraction. The engine's
        // eta denominator is literally 1.6*D - 2*delta (fdiv @0x125D40), which
        // is this formula with focusImageY = -0.6 ([-32584]); the (1+focusImageY)
        // numerator factor is the fmadd @0x125D2C. Two engine gates replace the
        // old clamp:
        //   - delta/lambda >= 0.8  -> reject to the plain fit (the 80% rule's
        //     second form, @0x12382C-0x123854: 1.6*D - 2*delta > 0 branches to
        //     the warp; its complement falls back);
        //   - xi >= -0.6 (focus not deep enough) -> plain fit (@0x123820).
        const float focusFraction = 0.50f;   // f1 = 0.5, CONFIRMED
        const float focusImageY = -0.60f;    // [-32584], CONFIRMED
        var focusY = alignedNearCenter.Y +
            (alignedFarCenter.Y - alignedNearCenter.Y) * focusFraction;
        var delta0 = focusY - receiverTop;
        if (!float.IsFinite(delta0) || delta0 <= lambda * 0.001f)
        {
            // Engine xi >= -0.6 territory: take the plain fit.
            ShadowLog.Log("[TSM] plain fit: focus too shallow (xi >= -0.6 territory)");
            return false;
        }
        if (delta0 >= lambda * 0.8f)
        {
            // Engine 80% rule: focus beyond the band -> plain fit.
            ShadowLog.Log($"[TSM] plain fit: 80% rule (delta/lambda = {delta0 / lambda:P0})");
            return false;
        }
        var etaDenominator = lambda - 2.0f * delta0 - lambda * focusImageY;
        if (MathF.Abs(etaDenominator) < 0.0001f)
        {
            return false;
        }
        var eta = lambda * delta0 * (1.0f + focusImageY) / etaDenominator;
        if (!float.IsFinite(eta) || eta <= 0.001f)
        {
            return false;
        }

        ShadowLog.Log(
            $"[TSM] ACTIVE lambda={lambda:F0} delta={delta0:F0} ({delta0 / lambda:P0}) " +
            $"eta(q)={eta:F0} focus=CENTRE(0.5) casters visible={_visibleDrawCount}");

        var qY = receiverTop - eta;

        // Casters are extruded toward the light and can sit in front of the receiver
        // hull. Keep the projection centre behind every submitted caster so no
        // building disappears merely because it lies before the camera near plane.
        var casterMinimumY = receiverTop;
        for (var entryIndex = 0; entryIndex < _visibleDrawCount; entryIndex++)
        {
            var entry = _visibleCasters[entryIndex];
            for (var corner = 0; corner < 8; corner++)
            {
                var point = Vector3.TransformPosition(GetWorldBoundsCorner(entry, corner), alignedView);
                casterMinimumY = MathF.Min(casterMinimumY, point.Y);
            }
        }
        var qMargin = MathF.Max(10.0f, lambda * 0.015f);
        qY = MathF.Min(qY, casterMinimumY - qMargin);

        var leftSlope = float.MaxValue;
        var rightSlope = float.MinValue;
        var minimumDepthRatio = float.MaxValue;
        var maximumDepthRatio = float.MinValue;

        for (var index = 0; index < alignedCorners.Length; index++)
        {
            if (!AccumulateTrapezoidPoint(
                    alignedCorners[index],
                    qY,
                    ref leftSlope,
                    ref rightSlope,
                    ref minimumDepthRatio,
                    ref maximumDepthRatio))
            {
                return false;
            }
        }
        for (var entryIndex = 0; entryIndex < _visibleDrawCount; entryIndex++)
        {
            var entry = _visibleCasters[entryIndex];
            for (var corner = 0; corner < 8; corner++)
            {
                var point = Vector3.TransformPosition(GetWorldBoundsCorner(entry, corner), alignedView);
                if (!AccumulateTrapezoidPoint(
                        point,
                        qY,
                        ref leftSlope,
                        ref rightSlope,
                        ref minimumDepthRatio,
                        ref maximumDepthRatio))
                {
                    return false;
                }
            }
        }

        var slopeWidth = rightSlope - leftSlope;
        if (!float.IsFinite(slopeWidth) || slopeWidth < 0.000001f)
        {
            return false;
        }
        var slopePadding = slopeWidth * 0.035f + 0.00001f;
        leftSlope -= slopePadding;
        rightSlope += slopePadding;
        slopeWidth = rightSlope - leftSlope;

        var topW = receiverTop - qY;
        var baseW = receiverBase - qY;
        if (topW <= 0.0001f || baseW <= topW + 0.0001f)
        {
            return false;
        }

        var depthRange = maximumDepthRatio - minimumDepthRatio;
        if (!float.IsFinite(depthRange) || depthRange < 0.000001f)
        {
            // Preserve a valid depth interval even for an almost planar receiver set.
            var zDepth = MathF.Max(1.0f, maximumZ - minimumZ);
            minimumDepthRatio -= 0.5f / zDepth;
            maximumDepthRatio += 0.5f / zDepth;
            depthRange = maximumDepthRatio - minimumDepthRatio;
        }
        var depthPadding = depthRange * 0.06f + 0.000001f;
        minimumDepthRatio -= depthPadding;
        maximumDepthRatio += depthPadding;
        depthRange = maximumDepthRatio - minimumDepthRatio;

        var xScale = 2.0f / slopeWidth;
        var xOffset = -(rightSlope + leftSlope) / slopeWidth;
        var yB = 2.0f * topW * baseW / (baseW - topW);
        var yA = 1.0f - yB / topW;
        var zScale = 2.0f / depthRange;
        var zOffset = -(maximumDepthRatio + minimumDepthRatio) / depthRange;

        // Row-vector form used by OpenTK on the CPU. After upload this produces:
        //   clip.x = xScale*x + xOffset*(y-q)
        //   clip.y = yA*(y-q) + yB
        //   clip.z = zScale*z + zOffset*(y-q)
        //   clip.w = y-q
        // so the divide maps the trapezoid and its depth interval to NDC.
        var trapezoidalProjection = new Matrix4();
        trapezoidalProjection.M11 = xScale;
        trapezoidalProjection.M21 = xOffset;
        trapezoidalProjection.M41 = -xOffset * qY;
        trapezoidalProjection.M22 = yA;
        trapezoidalProjection.M42 = yB - yA * qY;
        trapezoidalProjection.M23 = zOffset;
        trapezoidalProjection.M33 = zScale;
        trapezoidalProjection.M43 = -zOffset * qY;
        trapezoidalProjection.M24 = 1.0f;
        trapezoidalProjection.M44 = -qY;

        if (!IsFinite(trapezoidalProjection))
        {
            return false;
        }

        _lastProjectionWasTsm = true;
        ShadowProjection = lightView;
        ShadowProjectionTsm = alignment;
        ShadowProjectionTsm2 = trapezoidalProjection;
        ShadowProjectionRange = new Vector4(receiverTop, receiverBase, qY, eta);
        LightSpaceMatrix = lightView * alignment * trapezoidalProjection;
        return IsFinite(LightSpaceMatrix);
    }

    private void BuildOrthographicFallback(
        Matrix4 lightView,
        float left,
        float right,
        float bottom,
        float top,
        float minimumZ,
        float maximumZ)
    {
        var width = MathF.Max(1.0f, right - left);
        var height = MathF.Max(1.0f, top - bottom);
        var centerX = (left + right) * 0.5f;
        var centerY = (bottom + top) * 0.5f;
        var texelX = width / _resolution;
        var texelY = height / _resolution;
        centerX = MathF.Round(centerX / texelX) * texelX;
        centerY = MathF.Round(centerY / texelY) * texelY;
        left = centerX - width * 0.5f;
        right = centerX + width * 0.5f;
        bottom = centerY - height * 0.5f;
        top = centerY + height * 0.5f;

        var depth = MathF.Max(1.0f, maximumZ - minimumZ);
        var zPadding = depth * 0.08f + 100.0f;
        var nearPlane = MathF.Max(1.0f, -maximumZ - zPadding);
        var farPlane = MathF.Max(nearPlane + 1.0f, -minimumZ + zPadding);
        var projection = Matrix4.CreateOrthographicOffCenter(
            left,
            right,
            bottom,
            top,
            nearPlane,
            farPlane);

        _lastProjectionWasTsm = false;
        ShadowProjection = lightView;
        ShadowProjectionTsm = projection;
        ShadowProjectionTsm2 = Matrix4.Identity;
        ShadowProjectionRange = new Vector4(
            nearPlane,
            farPlane,
            1.0f / MathF.Max(1.0f, farPlane - nearPlane),
            _shadowDistance);
        LightSpaceMatrix = lightView * projection;
    }

    private static bool AccumulateTrapezoidPoint(
        Vector3 point,
        float qY,
        ref float leftSlope,
        ref float rightSlope,
        ref float minimumDepthRatio,
        ref float maximumDepthRatio)
    {
        if (!IsFinite(point))
        {
            return false;
        }
        var w = point.Y - qY;
        if (!float.IsFinite(w) || w <= 0.00001f)
        {
            return false;
        }
        var slope = point.X / w;
        var depthRatio = point.Z / w;
        if (!float.IsFinite(slope) || !float.IsFinite(depthRatio))
        {
            return false;
        }
        leftSlope = MathF.Min(leftSlope, slope);
        rightSlope = MathF.Max(rightSlope, slope);
        minimumDepthRatio = MathF.Min(minimumDepthRatio, depthRatio);
        maximumDepthRatio = MathF.Max(maximumDepthRatio, depthRatio);
        return true;
    }

    private static Vector3 GetWorldBoundsCorner(CasterEntry entry, int corner) => new(
        (corner & 1) == 0 ? entry.Minimum.X : entry.Maximum.X,
        (corner & 2) == 0 ? entry.Minimum.Y : entry.Maximum.Y,
        (corner & 4) == 0 ? entry.Minimum.Z : entry.Maximum.Z);

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool IsFinite(Matrix4 value) =>
        float.IsFinite(value.M11) && float.IsFinite(value.M12) &&
        float.IsFinite(value.M13) && float.IsFinite(value.M14) &&
        float.IsFinite(value.M21) && float.IsFinite(value.M22) &&
        float.IsFinite(value.M23) && float.IsFinite(value.M24) &&
        float.IsFinite(value.M31) && float.IsFinite(value.M32) &&
        float.IsFinite(value.M33) && float.IsFinite(value.M34) &&
        float.IsFinite(value.M41) && float.IsFinite(value.M42) &&
        float.IsFinite(value.M43) && float.IsFinite(value.M44);

    private void CollectSpatialCandidates(Span<Vector3> frustumCorners, Vector3 lightDirection)
    {
        _candidateCasters.Clear();

        var worldMinimum = new Vector3(float.MaxValue);
        var worldMaximum = new Vector3(float.MinValue);
        var extrusion = MathF.Min(_shadowDistance * 0.40f, 75000.0f);
        for (var index = 0; index < frustumCorners.Length; index++)
        {
            var corner = frustumCorners[index];
            var towardLight = corner + lightDirection * extrusion;
            worldMinimum = Vector3.ComponentMin(worldMinimum, corner);
            worldMaximum = Vector3.ComponentMax(worldMaximum, corner);
            worldMinimum = Vector3.ComponentMin(worldMinimum, towardLight);
            worldMaximum = Vector3.ComponentMax(worldMaximum, towardLight);
        }

        var expansion = _maximumCasterRadius + MinimumFootprintPadding;
        var minimumCellX = ToCell(worldMinimum.X - expansion);
        var maximumCellX = ToCell(worldMaximum.X + expansion);
        var minimumCellZ = ToCell(worldMinimum.Z - expansion);
        var maximumCellZ = ToCell(worldMaximum.Z + expansion);
        var cellsX = (long)maximumCellX - minimumCellX + 1;
        var cellsZ = (long)maximumCellZ - minimumCellZ + 1;
        var cellCount = cellsX > 0 && cellsZ > 0 ? cellsX * cellsZ : long.MaxValue;

        if (cellCount <= 0 || cellCount > MaximumSpatialCellsPerQuery)
        {
            _candidateCasters.AddRange(_casters);
            if (!_loggedSpatialFallback)
            {
                Console.WriteLine(
                    $"[Haven Shadows] Spatial query spans {cellCount:N0} cells; using cached full list fallback.");
                _loggedSpatialFallback = true;
            }
            return;
        }

        for (var cellZ = minimumCellZ; cellZ <= maximumCellZ; cellZ++)
        {
            for (var cellX = minimumCellX; cellX <= maximumCellX; cellX++)
            {
                if (!_spatialBins.TryGetValue(SpatialKey(cellX, cellZ), out var bin))
                {
                    continue;
                }
                for (var index = 0; index < bin.Count; index++)
                {
                    _candidateCasters.Add(bin[index]);
                }
            }
        }
    }

    private void RenderDepthPass(ModelRenderer modelRenderer, int depthShaderProgram)
    {
        GL.GetInteger(GetPName.FramebufferBinding, out var previousFramebuffer);
        GL.GetInteger(GetPName.CurrentProgram, out var previousProgram);
        GL.GetInteger(GetPName.Viewport, _savedViewport);
        var blendWasEnabled = GL.IsEnabled(EnableCap.Blend);
        var cullWasEnabled = GL.IsEnabled(EnableCap.CullFace);

        try
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            GL.Viewport(0, 0, _resolution, _resolution);
            GL.ColorMask(false, false, false, false);
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.Blend);
            // Render BACK faces into the shadow map (cull front faces): the
            // stored depth is then the far side of each occluder, so lit front
            // faces sit well in front of it and no longer self-shadow. Fixes the
            // vertical-wall acne that read as "inverted" shadows and changed with
            // range (the old no-cull path stored the lit face's own depth).
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Front);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(1.5f, 3.0f);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.UseProgram(depthShaderProgram);
            SetMatrix(depthShaderProgram, "lightSpaceMatrix", LightSpaceMatrix);

            for (var index = 0; index < _visibleDrawCount; index++)
            {
                modelRenderer.RenderShadowModel(_visibleCasters[index].Model, depthShaderProgram);
            }
        }
        finally
        {
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.CullFace(CullFaceMode.Back);
            if (cullWasEnabled) GL.Enable(EnableCap.CullFace);
            else GL.Disable(EnableCap.CullFace);
            if (blendWasEnabled) GL.Enable(EnableCap.Blend);
            else GL.Disable(EnableCap.Blend);
            GL.ColorMask(true, true, true, true);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, previousFramebuffer);
            GL.Viewport(_savedViewport[0], _savedViewport[1], _savedViewport[2], _savedViewport[3]);
            GL.UseProgram(previousProgram);
            GL.ActiveTexture(TextureUnit.Texture0);
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized || _permanentlyDisabled)
        {
            return;
        }

        GL.GetInteger(GetPName.MaxTextureSize, out var maximumTextureSize);
        if (maximumTextureSize < MinimumResolution)
        {
            Disable($"GPU maximum texture size ({maximumTextureSize}) is too small for shadow maps.");
            return;
        }
        _resolution = Math.Min(_requestedResolution, Math.Min(maximumTextureSize, MaximumResolution));


        GL.GetInteger(GetPName.FramebufferBinding, out var previousFramebuffer);
        GL.GetInteger(GetPName.TextureBinding2D, out var previousTexture);

        try
        {
            _framebuffer = GL.GenFramebuffer();
            _depthTexture = GL.GenTexture();
            _colorTexture = GL.GenTexture();
            if (_framebuffer == 0 || _depthTexture == 0 || _colorTexture == 0)
            {
                throw new InvalidOperationException("OpenGL could not allocate Haven shadow-map objects.");
            }

            GL.BindTexture(TextureTarget.Texture2D, _depthTexture);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.DepthComponent24,
                _resolution,
                _resolution,
                0,
                PixelFormat.DepthComponent,
                PixelType.UnsignedInt,
                IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            // GLES-safe completeness target. R8 uses 1 MiB at 1024² instead of the
            // previous 4 MiB RGBA attachment; only the depth texture is sampled.
            GL.BindTexture(TextureTarget.Texture2D, _colorTexture);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.R8,
                _resolution,
                _resolution,
                0,
                PixelFormat.Red,
                PixelType.UnsignedByte,
                IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D,
                _depthTexture,
                0);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                _colorTexture,
                0);

            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new InvalidOperationException($"Haven shadow framebuffer is incomplete: {status}.");
            }

            _initialized = true;
            Console.WriteLine(
                $"[Haven Shadows] Shadow map {_resolution}x{_resolution}, texture unit {ShadowTextureUnit}.");
        }
        finally
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, previousFramebuffer);
            GL.BindTexture(TextureTarget.Texture2D, previousTexture);
        }
    }

    private bool CameraChanged(Camera camera, Vector3 forward)
    {
        if (!_hasCachedMap)
        {
            return true;
        }

        return (camera.Position - _lastCameraPosition).LengthSquared > 122500.0f ||
               Vector3.Dot(forward, _lastCameraForward) < 0.9986f ||
               MathF.Abs(camera.FieldOfView - _lastCameraFov) > 0.001f ||
               MathF.Abs(camera.AspectRatio - _lastCameraAspect) > 0.001f;
    }

    private bool TryGetViewFrustumCorners(
        Camera camera,
        Vector3 forward,
        Span<Vector3> corners)
    {
        if (corners.Length < 8 || !IsFinite(camera.Position) || !IsFinite(forward))
        {
            return false;
        }

        // POSITION-ANCHORED coverage (v0.9.37). The previous implementation fit
        // the shadow volume to the camera's VIEW frustum, so approaching a wall
        // or turning the camera shifted the covered window and shadows at its
        // edge truncated ("le shadow ne reste pas complet"). Coverage is now a
        // box centred on the camera POSITION, independent of look direction:
        // rotating never changes the shadow map, translating only recentres it.
        // ShadowDistance is the box DIAMETER (half-extent = 0.5x) so a given
        // slider value keeps a texel density comparable to the old frustum fit.
        // The vertical half-extent is capped (stage tuning, not engine-derived):
        // sm_dd architecture spans well under 60k vertically, and not spending
        // light-space extent on empty sky keeps lambda - and sharpness - tight.
        var halfHorizontal = MathF.Max(500.0f, _shadowDistance * 0.5f);
        var halfVertical = MathF.Min(halfHorizontal, 30000.0f);
        var center = camera.Position;
        var index = 0;
        for (var y = -1; y <= 1; y += 2)
        {
            for (var x = -1; x <= 1; x += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    corners[index++] = new Vector3(
                        center.X + halfHorizontal * x,
                        center.Y + halfVertical * y,
                        center.Z + halfHorizontal * z);
                }
            }
        }

        return true;
    }

    private static bool HasPendingGpuUpdates(IReadOnlyList<Model3D> models)
    {
        for (var index = 0; index < models.Count; index++)
        {
            var model = models[index];
            if (model.CastsShadow && (model.VerticesNeedUpdate || model.IndicesNeedUpdate))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsPotentialShadowCaster(Model3D model)
    {
        if (!model.Visible || !model.CastsShadow || model.Indices.Length < 3 ||
            !model.WriteDepth || model.BlendEnabled)
        {
            return false;
        }

        var alpha = model.Alpha * (model.Material?.Alpha ?? 1.0f);
        return float.IsFinite(alpha) && alpha >= 0.99f;
    }

    private static Vector3 ResolveLightDirection(IReadOnlyList<Light> lights)
    {
        for (var index = 0; index < lights.Count; index++)
        {
            if (lights[index] is DirectionalLight directional && directional.Enabled &&
                directional.Direction.LengthSquared > 0.000001f &&
                IsFinite(directional.Direction))
            {
                // directional.Direction is the surface-to-light vector (points
                // toward the sun, +Y up). The shadow camera must sit ON THE SUN
                // SIDE looking back at the scene, so it is placed along this
                // vector: lightEye = center + dir * distance. Verified in-game:
                // with the raw surface-to-light vector the shadows landed on the
                // rooftops instead of the ground - the camera was looking from
                // below. Negating here puts the eye above and casts building
                // shadows down onto the floor, matching the 2006 reference.
                return -Vector3.Normalize(directional.Direction);
            }
        }
        return Vector3.Normalize(new Vector3(-1.0f, -1.0f, -1.0f));
    }

    private static int ToCell(float coordinate)
    {
        var value = MathF.Floor(coordinate / SpatialCellSize);
        if (value <= int.MinValue) return int.MinValue;
        if (value >= int.MaxValue) return int.MaxValue;
        return (int)value;
    }

    private static long SpatialKey(int cellX, int cellZ) =>
        ((long)cellX << 32) | (uint)cellZ;

    private void Disable(string reason)
    {
        _disabledReason = reason;
        _permanentlyDisabled = true;
        _hasCachedMap = false;
        Console.Error.WriteLine($"[Haven Shadows] {reason}");
        DeleteResources();
    }

    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        return IsFinite(value) && value.LengthSquared > 0.000001f
            ? Vector3.Normalize(value)
            : fallback;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void SetMatrix(int program, string name, Matrix4 value)
    {
        var location = GL.GetUniformLocation(program, name);
        if (location >= 0) GL.UniformMatrix4(location, false, ref value);
    }

    private static void SetVector2(int program, string name, Vector2 value)
    {
        var location = GL.GetUniformLocation(program, name);
        if (location >= 0) GL.Uniform2(location, value);
    }

    private static void SetVector3(int program, string name, Vector3 value)
    {
        var location = GL.GetUniformLocation(program, name);
        if (location >= 0) GL.Uniform3(location, value);
    }

    private static void SetVector4(int program, string name, Vector4 value)
    {
        var location = GL.GetUniformLocation(program, name);
        if (location >= 0) GL.Uniform4(location, value);
    }

    private static void SetFloat(int program, string name, float value)
    {
        var location = GL.GetUniformLocation(program, name);
        if (location >= 0) GL.Uniform1(location, value);
    }

    private static void SetBool(int program, string name, bool value)
    {
        var location = GL.GetUniformLocation(program, name);
        if (location >= 0) GL.Uniform1(location, value ? 1 : 0);
    }

    private void DeleteResources()
    {
        if (_colorTexture != 0)
        {
            GL.DeleteTexture(_colorTexture);
            _colorTexture = 0;
        }
        if (_depthTexture != 0)
        {
            GL.DeleteTexture(_depthTexture);
            _depthTexture = 0;
        }
        if (_framebuffer != 0)
        {
            GL.DeleteFramebuffer(_framebuffer);
            _framebuffer = 0;
        }
        _initialized = false;
    }

    public void Dispose()
    {
        DeleteResources();
        _casters.Clear();
        _candidateCasters.Clear();
        _visibleCasters.Clear();
        _spatialBins.Clear();
        _hasCachedMap = false;
    }
}
