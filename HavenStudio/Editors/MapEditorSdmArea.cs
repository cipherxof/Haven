using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia3DControl.Core.Models;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Rendering;
using HavenStudio.Utils;
using OpenTK.Mathematics;

namespace HavenStudio.Editors;

public sealed record SdmAreaEntity : MapEntity, INotifyPropertyChanged
{
    private const int SegmentCount = 128;
    private readonly Func<SdmAreaEntity, int, string?> _editMaximum;

    public GcxSdmAreaReference Source { get; private set; }
    public Vector3? Center { get; }
    public IReadOnlyList<Model3D> Models { get; }
    public string SourceText =>
        $"GCX {Source.DefinitionScript}, called by {Source.CallerScript}.";
    public string PropertyDirectoryText => $"0x{Source.PropertyDirectoryHash:X6}";
    public string CenterText => Center is { } center
        ? FormattableString.Invariant($"{center.X:0.###}, {center.Y:0.###}, {center.Z:0.###}")
        : "PRP_STAGE_CENTER was not found beneath the configured property directory.";
    public string StartingRadiusText => FormatRadius(Source.StartingRadius, Source.AreaMaximum);
    public string MinimumRadiusText => FormatRadius(Source.MinimumRadius, Source.AreaMinimum);
    public int AreaMaximum
    {
        get => Source.AreaMaximum;
        set
        {
            if (value == Source.AreaMaximum)
            {
                return;
            }
            EditStatus = _editMaximum(this, value) ?? string.Empty;
            Changed();
        }
    }
    public string EditStatus { get; private set; } = string.Empty;
    public string TimingText =>
        $"time {Source.AreaTime}, timer {Source.AreaTimer}, move {Source.AreaMove}, change {Source.AreaChange}";
    public string VisualizationText => Center == null
        ? "The GCX values were decoded, but Haven cannot place the overlay until the matching GEOM center exists."
        : "Cyan is the starting area_max boundary; magenta is the final area_min boundary. This overlay is editor-only.";

    public SdmAreaEntity(
        GcxSdmAreaReference source,
        Vector3? center,
        Func<SdmAreaEntity, int, string?>? editMaximum = null)
        : base("SDM area")
    {
        Source = source;
        Center = center;
        _editMaximum = editMaximum ?? ((_, _) => "This SDM area is read-only.");
        Models = center is { } position
            ? [
                BuildBoundary(position, source.StartingRadius, new Vector3(0.05f, 0.85f, 1f), "SDM area_max"),
                BuildBoundary(position, source.MinimumRadius, new Vector3(1f, 0.12f, 0.75f), "SDM area_min")
            ]
            : [];
    }

    public void ApplyAreaMaximum(int maximum)
    {
        Source = Source with { AreaMaximum = maximum };
        if (Center is { } center && Models.Count > 0)
        {
            CopyModel(Models[0], BuildBoundary(
                center,
                Source.StartingRadius,
                new Vector3(0.05f, 0.85f, 1f),
                "SDM area_max"));
        }
        EditStatus = string.Empty;
        Changed();
    }

    private static void CopyModel(Model3D target, Model3D source)
    {
        target.Name = source.Name;
        target.Position = source.Position;
        target.Scale = source.Scale;
        target.Color = source.Color;
        target.Alpha = source.Alpha;
        target.MaterialIndex = source.MaterialIndex;
        target.RenderAfterTransparent = source.RenderAfterTransparent;
        target.Positions = source.Positions;
        target.Indices = source.Indices;
        target.VertexCount = source.VertexCount;
        target.IndexCount = source.IndexCount;
        target.VerticesNeedUpdate = true;
        target.IndicesNeedUpdate = true;
    }

    private static string FormatRadius(float worldRadius, int configuredValue) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{worldRadius:0} world units ({configuredValue} × {GcxSdmAreaReference.WorldUnitsPerAreaUnit:0})");

    private static Model3D BuildBoundary(
        Vector3 center,
        float radius,
        Vector3 color,
        string name)
    {
        var halfWidth = Math.Clamp(radius * 0.004f, 150f, 600f);
        var halfHeight = Math.Clamp(radius * 0.08f, 5000f, 15000f);
        var innerRadius = Math.Max(0f, radius - halfWidth);
        var outerRadius = radius + halfWidth;
        var positions = new List<float>(SegmentCount * 24);
        var indices = new List<uint>(SegmentCount * 12);

        for (var segment = 0; segment < SegmentCount; segment++)
        {
            var angle0 = MathF.Tau * segment / SegmentCount;
            var angle1 = MathF.Tau * (segment + 1) / SegmentCount;
            var inner0 = new Vector3(MathF.Cos(angle0) * innerRadius, 0f, MathF.Sin(angle0) * innerRadius);
            var inner1 = new Vector3(MathF.Cos(angle1) * innerRadius, 0f, MathF.Sin(angle1) * innerRadius);
            var outer0 = new Vector3(MathF.Cos(angle0) * outerRadius, 0f, MathF.Sin(angle0) * outerRadius);
            var outer1 = new Vector3(MathF.Cos(angle1) * outerRadius, 0f, MathF.Sin(angle1) * outerRadius);

            AddQuad(positions, indices, inner0, outer0, outer1, inner1);

            var wall0Bottom = new Vector3(MathF.Cos(angle0) * radius, -halfHeight, MathF.Sin(angle0) * radius);
            var wall1Bottom = new Vector3(MathF.Cos(angle1) * radius, -halfHeight, MathF.Sin(angle1) * radius);
            var wall0Top = new Vector3(wall0Bottom.X, halfHeight, wall0Bottom.Z);
            var wall1Top = new Vector3(wall1Bottom.X, halfHeight, wall1Bottom.Z);
            AddQuad(positions, indices, wall0Bottom, wall1Bottom, wall1Top, wall0Top);
        }

        return new Model3D
        {
            Name = name,
            Position = center,
            Scale = Vector3.One,
            Color = color,
            Alpha = 0.42f,
            MaterialIndex = -1,
            RenderAfterTransparent = true,
            Positions = positions.ToArray(),
            Indices = indices.ToArray(),
            VertexCount = positions.Count / 3,
            IndexCount = indices.Count,
            VerticesNeedUpdate = true,
            IndicesNeedUpdate = true
        };
    }

    private static void AddQuad(
        ICollection<float> positions,
        ICollection<uint> indices,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d)
    {
        var start = (uint)(positions.Count / 3);
        foreach (var point in new[] { a, b, c, d })
        {
            positions.Add(point.X);
            positions.Add(point.Y);
            positions.Add(point.Z);
        }
        // Emit both windings so the ring remains visible from inside/outside the
        // play area and from cameras above or below the center elevation.
        foreach (var index in new[]
        {
            start, start + 1, start + 2, start + 2, start + 3, start,
            start + 2, start + 1, start, start, start + 3, start + 2
        })
        {
            indices.Add(index);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

public sealed partial class MapEditorViewModel
{
    private const uint StageCenterHash = 0x0007B6AA;
    private readonly MapOutlineGroup _sdmAreasGroup = new("SDM area");
    private readonly Dictionary<Model3D, SdmAreaEntity> _sdmAreaByModel = [];

    public bool SdmAreaVisible
    {
        get => _sceneHost.IsLayerVisible(SceneLayer.SdmArea);
        set => SetLayerVisible(SceneLayer.SdmArea, value);
    }

    private void RefreshSdmAreas()
    {
        if (_selectedEntity is SdmAreaEntity)
        {
            ClearSelection();
        }

        _sdmAreaByModel.Clear();
        var allEffects = TreeTraversal.Flatten(_collisionEditor.Effects, effect => effect.Children).ToArray();
        var entities = new List<SdmAreaEntity>();
        foreach (var reference in _gcxEditor.GetSdmAreas())
        {
            var directory = allEffects.FirstOrDefault(effect =>
                unchecked((uint)effect.Effect.Name) == reference.PropertyDirectoryHash);
            var directoryEffects = directory == null
                ? Array.Empty<CollisionEffectViewModel>()
                : TreeTraversal.Flatten(directory.Children, effect => effect.Children).ToArray();
            var centerEffect = directoryEffects.FirstOrDefault(effect =>
                    unchecked((uint)effect.Effect.Name) == StageCenterHash)
                ?? allEffects.FirstOrDefault(effect =>
                    unchecked((uint)effect.Effect.Name) == StageCenterHash);
            Vector3? center = centerEffect == null
                ? null
                : new Vector3(centerEffect.Effect.X, centerEffect.Effect.Y, centerEffect.Effect.Z);
            var entity = new SdmAreaEntity(reference, center, EditSdmAreaMaximum);
            entities.Add(entity);
            foreach (var model in entity.Models)
            {
                _sdmAreaByModel[model] = entity;
            }
        }

        ReplaceChildren(_sdmAreasGroup, entities.Cast<object>());
        _sceneHost.ReplaceLayer(SceneLayer.SdmArea, _sdmAreaByModel.Keys.ToArray());
    }

    private void SelectSdmArea(SdmAreaEntity area)
    {
        _collisionEditor.ClearSelection();
        SetSelectedEntity(area, area);
    }

    private string? EditSdmAreaMaximum(SdmAreaEntity area, int maximum)
    {
        if (maximum <= area.Source.AreaMinimum)
        {
            return $"The starting radius must be greater than area_min ({area.Source.AreaMinimum}).";
        }
        try
        {
            var previousMaximum = area.Source.AreaMaximum;
            var original = _gcxEditor.GetSdmAreaScriptBytes(area.Source);
            var updated = GcxSdmAreaWriter.Write(
                original,
                area.Source.PropertyDirectoryHash,
                previousMaximum,
                maximum);
            if (original.AsSpan().SequenceEqual(updated))
            {
                return null;
            }

            _history.Execute(
                $"set SDM starting radius to {maximum}",
                () => ApplySdmAreaBytes(area, updated, maximum),
                () => ApplySdmAreaBytes(area, original, previousMaximum));
            return null;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or InvalidOperationException or
            ArgumentOutOfRangeException or OverflowException)
        {
            return exception.Message;
        }
    }

    private void ApplySdmAreaBytes(SdmAreaEntity area, byte[] bytes, int maximum)
    {
        _gcxEditor.ApplySdmAreaScript(area.Source, bytes);
        area.ApplyAreaMaximum(maximum);
        _sceneHost.ViewportControl.RequestNextFrameRendering();
    }
}
