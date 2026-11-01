using System;
using System.ComponentModel;
using HavenStudio.Rendering;
using HavenStudio.Utils;

namespace HavenStudio.Editors;

/// <summary>One editable polygon, not one rendered triangle. Shared serialized
/// aliases are collapsed within the same static/placement instance.</summary>
public sealed class OctocamoFaceCandidate : INotifyPropertyChanged
{
    private readonly OctocamoSurfaceCatalog _catalog;
    private bool _isBatchSelected;
    public bool IsBatchSelected
    {
        get => _isBatchSelected;
        set
        {
            if (_isBatchSelected == value) return;
            _isBatchSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBatchSelected)));
        }
    }
    public OctocamoFaceCandidate(CollisionPrimViewModel primitive, CollisionGeoPrimViewModel polygon,
        SelectionHit hit, object? instance, string owner, OctocamoSurfaceCatalog catalog)
    {
        Primitive = primitive;
        Polygon = polygon;
        Hit = hit;
        Instance = instance;
        Owner = owner;
        _catalog = catalog;
    }
    public CollisionPrimViewModel Primitive { get; }
    public CollisionGeoPrimViewModel Polygon { get; }
    public SelectionHit Hit { get; }
    public object? Instance { get; }
    public string Owner { get; }
    public int SelectorOffset => Primitive.Prim.Offset + 0x26 + Primitive.Children.IndexOf(Polygon) * 8;
    public string AddressText => $"0x{SelectorOffset:X} · polygon {Primitive.Children.IndexOf(Polygon)}";
    public string MaterialText
    {
        get
        {
            var surface = _catalog.Resolve(Primitive.ParentBlock!.Block, Polygon.Poly!.Attribute);
            var pattern = surface.HasPatternMapping ? $"{DictionaryFile.GetHashString(surface.PatternHash)} (0x{surface.PatternHash:X6})" : "UNMAPPED";
            return $"{DictionaryFile.GetHashString(surface.MaterialHash)} (0x{surface.MaterialHash:X6}) → {pattern}";
        }
    }
    public string DetailsText => $"{Owner} · block {Primitive.ParentBlock!.Index} · attr 0x{Polygon.Poly!.Attribute:X4}";
    public string ScopeText => Instance == null ? "Edits this polygon and its serialized aliases only."
        : "Placement reference: edits affect all instances sharing this source polygon.";
    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaterialText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetailsText)));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
