using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using HavenStudio.Formats.Geo;
using HavenStudio.Utils;

namespace HavenStudio.Editors;

/// <summary>Edits only the material and colour selector bits of one GEOM polygon.</summary>
public sealed class OctocamoSelection : INotifyPropertyChanged
{
    private readonly CollisionGeoPrimViewModel _polygon;
    private readonly GeoBlock _block;
    private readonly OctocamoSurfaceCatalog _catalog;
    private readonly Action _afterEdit;
    private readonly Action<CollisionGeoPrimViewModel, ushort>? _editAttribute;
    private readonly Action<bool>? _showMusclePreview;
    private OctocamoMaterialOption? _selectedMaterial;
    private OctocamoColourOption? _selectedColour;

    public OctocamoSelection(CollisionGeoPrimViewModel polygon, GeoBlock block,
        OctocamoSurfaceCatalog catalog, Action afterEdit,
        Action<CollisionGeoPrimViewModel, ushort>? editAttribute = null,
        Action<bool>? showMusclePreview = null)
    {
        _polygon = polygon;
        _block = block;
        _catalog = catalog;
        _afterEdit = afterEdit;
        _editAttribute = editAttribute;
        _showMusclePreview = showMusclePreview;
        Materials = catalog.Materials(block);
        Colours = catalog.Colours(block);
        Synchronize();
    }

    public IReadOnlyList<OctocamoMaterialOption> Materials { get; }
    public IReadOnlyList<OctocamoColourOption> Colours { get; }
    public string PatternText => Current.PatternHash == 0
        ? "No mapped muscle-suit pattern"
        : $"{DictionaryFile.GetHashString(Current.PatternHash)} (0x{Current.PatternHash:X6})";
    public string ClothText => Current.HasClothMapping
        ? $"RGB {Current.ClothColour.X:0.###}, {Current.ClothColour.Y:0.###}, {Current.ClothColour.Z:0.###}"
        : "Missing cloth-colour mapping (independent of the muscle pattern)";
    public IBrush ClothPreview => OctocamoSurfaceCatalog.ToBrush(Current.ClothColour);
    public IImage? PatternPreview => _catalog.PatternImage(Current.PatternHash);
    public bool CanEdit => _polygon.Poly != null &&
        (_polygon.ParentPrim.Prim.Attribute & GeoCollisionAttributes.Player) != 0;
    public string Status => (_polygon.ParentPrim.Prim.Attribute & GeoCollisionAttributes.Player) == 0
        ? "This polygon is not in player collision; Snake cannot sample it."
        : Current.IsMapped ? "Mapped in the stage OctoCamo table." : "Unmapped selector: shown magenta in OctoCamo view.";
    public string ScopeText => $"Editing polygon {_polygon.ParentPrim.Children.IndexOf(_polygon)} at GEOM byte 0x{_polygon.ParentPrim.Prim.Offset + 0x20 + _polygon.ParentPrim.Children.IndexOf(_polygon) * 8 + 6:X}. Selected-face preview takes priority over overlapping collision faces. Other faces are unchanged; press Snake against this face when testing.";
    public string PreviewExplanation => (_catalog.PatternAtlas == null
        ? "Load camo previews from the online OctoCamo SLOT to see actual muscle textures. Until then, the viewport shows cloth RGB only."
        : "Muscle texture view projects the raw diffuse onto collision, not Snake's suit UVs or in-game lighting. Turn it off to view cloth RGB. Contact material and cloth colour are independent.")
        + (Current.PatternHash == 0xF563A9 ? " This N004A plastic donor has a pink/white muscle pattern; selecting green cloth does not recolour it." : string.Empty);

    public OctocamoMaterialOption? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            if (value == null || value == _selectedMaterial || !CanEdit) return;
            ApplyAttribute(OctocamoSurfaceCatalog.WithMaterial(_polygon.Poly!.Attribute, value.Slot));
            Synchronize();
            _showMusclePreview?.Invoke(true);
            _afterEdit();
        }
    }

    public OctocamoColourOption? SelectedColour
    {
        get => _selectedColour;
        set
        {
            if (value == null || value == _selectedColour || !CanEdit) return;
            ApplyAttribute(OctocamoSurfaceCatalog.WithColour(_polygon.Poly!.Attribute, value.Slot));
            Synchronize();
            _showMusclePreview?.Invoke(false);
            _afterEdit();
        }
    }

    private OctocamoSurface Current => _catalog.Resolve(_block, _polygon.Poly?.Attribute ?? 0);

    private void ApplyAttribute(ushort attribute)
    {
        if (_editAttribute != null) _editAttribute(_polygon, attribute);
        else _polygon.AttributeText = $"0x{attribute:X4}";
    }

    private void Synchronize()
    {
        var surface = Current;
        _selectedMaterial = Materials.FirstOrDefault(option => option.Slot == surface.MaterialSlot);
        _selectedColour = Colours.FirstOrDefault(option => option.Slot == surface.ColourSlot);
        OnPropertyChanged(nameof(SelectedMaterial));
        OnPropertyChanged(nameof(SelectedColour));
        OnPropertyChanged(nameof(PatternText));
        OnPropertyChanged(nameof(ClothText));
        OnPropertyChanged(nameof(ClothPreview));
        OnPropertyChanged(nameof(PatternPreview));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(PreviewExplanation));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
