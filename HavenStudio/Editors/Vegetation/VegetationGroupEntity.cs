using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia3DControl.Core.Models;
using HavenStudio.Formats.Pdl;
using OpenTK.Mathematics;

namespace HavenStudio.Editors.Vegetation;

public sealed record VegetationPreviewInstance(
    PdlInstance Instance,
    float PreviewScale,
    IReadOnlyList<Model3D> Models);

public sealed record VegetationGroupEntity : MapEntity, INotifyPropertyChanged
{
    private readonly Func<VegetationGroupEntity, Vector3, string?> _updatePosition;
    private string? _editStatus;

    public VegetationGroupEntity(
        VegetationDocumentSession session,
        PdlGroup group,
        string modelName,
        IReadOnlyList<VegetationPreviewInstance> previews,
        Func<VegetationGroupEntity, Vector3, string?> updatePosition,
        Func<VegetationInstanceEntity, Vector3, string?> updateInstance)
        : base($"{Path.GetFileName(session.Path.FileName)} — Group {group.Index + 1} ({group.Instances.Count})")
    {
        Session = session;
        Group = group;
        ModelName = modelName;
        Previews = previews;
        Models = previews.SelectMany(preview => preview.Models).ToArray();
        Children = previews.Select((preview, index) => new VegetationInstanceEntity(
            this, preview, index, updateInstance)).ToArray();
        _updatePosition = updatePosition;
    }

    public VegetationDocumentSession Session { get; }
    public PdlGroup Group { get; }
    public string ModelName { get; }
    public IReadOnlyList<VegetationPreviewInstance> Previews { get; }
    public IReadOnlyList<Model3D> Models { get; }
    public IReadOnlyList<VegetationInstanceEntity> Children { get; }
    public int InstanceCount => Group.Instances.Count;
    public string PdlName => Session.Path.FileName;
    public string ModelText => $"{ModelName}  (0x{Session.ModelHash:X6})";
    public string EditStatus => _editStatus ?? "Moves every instance in this PDL group; scale values and all other cache data are preserved.";
    public Vector3 Position => Group.Centroid;
    public float PositionX
    {
        get => Position.X;
        set => SetPosition(new Vector3(value, Position.Y, Position.Z));
    }
    public float PositionY
    {
        get => Position.Y;
        set => SetPosition(new Vector3(Position.X, value, Position.Z));
    }
    public float PositionZ
    {
        get => Position.Z;
        set => SetPosition(new Vector3(Position.X, Position.Y, value));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool TryUpdatePosition(Vector3 position, out string? error)
    {
        if ((position - Position).LengthSquared < 0.000001f)
        {
            error = null;
            return true;
        }
        _editStatus = _updatePosition(this, position);
        error = _editStatus;
        RefreshFromSource();
        return error == null;
    }

    public void RefreshFromSource()
    {
        foreach (var preview in Previews)
        {
            foreach (var model in preview.Models)
            {
                model.Position = preview.Instance.Position;
                model.Scale = Vector3.One * preview.PreviewScale;
                model.VerticesNeedUpdate = true;
            }
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Position)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PositionX)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PositionY)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PositionZ)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditStatus)));
    }

    private void SetPosition(Vector3 position) => TryUpdatePosition(position, out _);
}

public sealed record VegetationInstanceEntity : MapEntity, INotifyPropertyChanged
{
    private readonly Func<VegetationInstanceEntity, Vector3, string?> _update;
    private string? _editStatus;

    public VegetationInstanceEntity(
        VegetationGroupEntity parent,
        VegetationPreviewInstance preview,
        int index,
        Func<VegetationInstanceEntity, Vector3, string?> update)
        : base($"Instance {index + 1}")
    {
        Parent = parent;
        Preview = preview;
        Index = index;
        _update = update;
    }

    public VegetationGroupEntity Parent { get; }
    public VegetationPreviewInstance Preview { get; }
    public PdlInstance Instance => Preview.Instance;
    public IReadOnlyList<Model3D> Models => Preview.Models;
    public int Index { get; }
    public string PdlName => Parent.PdlName;
    public string ModelText => Parent.ModelText;
    public string GroupText => $"Group {Parent.Group.Index + 1}";
    public string EditStatus => _editStatus ?? "Edits this instance position. Scale is previewed from the GCX vegetation manager and is not stored per instance.";
    public Vector3 Position => Instance.Position;
    public float PositionX { get => Position.X; set => Apply(new Vector3(value, Position.Y, Position.Z)); }
    public float PositionY { get => Position.Y; set => Apply(new Vector3(Position.X, value, Position.Z)); }
    public float PositionZ { get => Position.Z; set => Apply(new Vector3(Position.X, Position.Y, value)); }
    public float PreviewScale => Preview.PreviewScale;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshFromSource()
    {
        foreach (var model in Models)
        {
            model.Position = Instance.Position;
            model.Scale = Vector3.One * Preview.PreviewScale;
            model.VerticesNeedUpdate = true;
        }
        foreach (var name in new[] { nameof(Position), nameof(PositionX), nameof(PositionY), nameof(PositionZ), nameof(PreviewScale), nameof(EditStatus) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        Parent.RefreshFromSource();
    }

    private void Apply(Vector3 position)
    {
        if (position == Position)
            return;
        _editStatus = _update(this, position);
        RefreshFromSource();
    }
}
