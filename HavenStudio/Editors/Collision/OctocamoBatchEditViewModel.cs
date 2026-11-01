using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using HavenStudio.Formats.Geo;
using HavenStudio.Utils;

namespace HavenStudio.Editors;

public sealed record OctocamoBatchMaterial(uint Hash, uint PatternHash, string DisplayName);
public sealed record OctocamoBatchColour(uint Hash, IBrush Preview, string DisplayName);
public sealed record OctocamoPolygonEdit(int Offset, CollisionGeoPrimViewModel Polygon, ushort Before, ushort After);

/// <summary>Hash-based batch selector editing. Each serialized alias must resolve
/// to a compatible local slot before ANY face is changed.</summary>
public sealed class OctocamoBatchEditViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<OctocamoFaceCandidate> _faces;
    private readonly IReadOnlyList<CollisionGeoPrimViewModel> _aliases;
    private readonly OctocamoSurfaceCatalog _catalog;
    private readonly Action<IReadOnlyList<OctocamoPolygonEdit>, bool> _commit;
    private OctocamoBatchMaterial? _material;
    private OctocamoBatchColour? _colour;
    private string _status = "Choose one or both channels. Empty dropdowns leave that channel unchanged. Apply changes stages one undoable edit; Save Map writes it.";

    public OctocamoBatchEditViewModel(IReadOnlyList<OctocamoFaceCandidate> faces,
        IReadOnlyList<CollisionGeoPrimViewModel> aliases, OctocamoSurfaceCatalog catalog,
        Action<IReadOnlyList<OctocamoPolygonEdit>, bool> commit)
    {
        if (faces.Count == 0) throw new InvalidOperationException("Select some faces first.");
        _faces = faces.ToArray(); _aliases = aliases.ToArray(); _catalog = catalog; _commit = commit;
        var groups = _aliases.GroupBy(Offset).ToArray();
        if (!groups.Select(group => group.Key).ToHashSet().SetEquals(faces.Select(face => face.SelectorOffset)))
            throw new InvalidOperationException("Some selected polygons are no longer in the loaded GEOM.");
        var firstTable = Table(_aliases[0]);
        // Only show a hash if all serialized aliases have it at the SAME slot for
        // each selector offset. Different independent offsets may use different slots.
        Materials = firstTable.Materials.Take(32).Where(hash => hash != 0).Distinct()
            .Where(hash => Compatible(groups, hash, colour: false))
            .Select(hash => new OctocamoBatchMaterial(hash, catalog.GetPatternHash(hash),
                $"{Name(hash)} → {(catalog.GetPatternHash(hash) == 0 ? "UNMAPPED" : Name(catalog.GetPatternHash(hash)))}")).ToArray();
        Colours = firstTable.Colors.Take(32).Where(hash => hash != 0).Distinct()
            .Where(hash => Compatible(groups, hash, colour: true))
            .Select(hash =>
            {
                var slot = firstTable.Colors.IndexOf(hash);
                var surface = catalog.Resolve(_aliases[0].ParentPrim.ParentBlock!.Block,
                    OctocamoSurfaceCatalog.WithColour(_aliases[0].Poly!.Attribute, slot));
                return new OctocamoBatchColour(hash, OctocamoSurfaceCatalog.ToBrush(surface.ClothColour),
                    Name(hash) + (surface.HasClothMapping ? string.Empty : " (unmapped)"));
            }).ToArray();
    }
    public IReadOnlyList<OctocamoBatchMaterial> Materials { get; }
    public IReadOnlyList<OctocamoBatchColour> Colours { get; }
    public string Summary => $"{_faces.Count} selected list items · {_faces.Select(face => face.SelectorOffset).Distinct().Count()} unique GEOM selectors";
    public string Scope => "Only this selection is edited. Shared byte-offset aliases and all placement instances using those source polygons also change. Independent unselected polygons, stage-wide OCTT mappings and normal texture resources are unchanged.";
    public string CurrentMaterial => CurrentLabel(colour: false);
    public string CurrentColour => CurrentLabel(colour: true);
    public string Status => _status;
    public bool CanApply => _material != null || _colour != null;
    public IImage? PatternPreview => _material == null ? null : _catalog.PatternImage(_material.PatternHash);
    public OctocamoBatchMaterial? SelectedMaterial
    {
        get => _material;
        set { _material = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanApply)); OnPropertyChanged(nameof(PatternPreview)); }
    }
    public OctocamoBatchColour? SelectedColour
    {
        get => _colour;
        set { _colour = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanApply)); }
    }
    public IReadOnlyList<OctocamoPolygonEdit> BuildPlan()
    {
        if (_material != null && !Materials.Contains(_material) || _colour != null && !Colours.Contains(_colour))
            throw new InvalidOperationException("Choose an option supported by every selected polygon and its aliases.");
        var result = new List<OctocamoPolygonEdit>();
        foreach (var group in _aliases.GroupBy(Offset))
        {
            var before = group.First().Poly!.Attribute;
            ushort? after = null;
            foreach (var alias in group)
            {
                if (alias.Poly!.Attribute != before)
                    throw new InvalidOperationException($"Aliases at 0x{group.Key:X} disagree. No changes were made.");
                var value = before;
                var table = Table(alias);
                if (_material != null) value = OctocamoSurfaceCatalog.WithMaterial(value, Slot(table.Materials, _material.Hash));
                if (_colour != null) value = OctocamoSurfaceCatalog.WithColour(value, Slot(table.Colors, _colour.Hash));
                if (after != null && after != value)
                    throw new InvalidOperationException($"Material tables at shared selector 0x{group.Key:X} require incompatible slots. No changes were made.");
                after = value;
            }
            if (after != before) result.Add(new OctocamoPolygonEdit(group.Key, group.First(), before, after!.Value));
        }
        return result;
    }
    public bool Apply()
    {
        try
        {
            if (!CanApply) { SetStatus("Choose a material or cloth colour to apply."); return false; }
            var plan = BuildPlan();
            if (plan.Count == 0) { SetStatus("All selected faces already have these settings."); return true; }
            _commit(plan, _material != null);
            SetStatus($"Changed {plan.Count} unique face selectors. Save Map to persist; Undo reverts the batch.");
            return true;
        }
        catch (InvalidOperationException exception) { SetStatus(exception.Message); return false; }
    }
    private string CurrentLabel(bool colour)
    {
        var hashes = _faces.Select(face => _catalog.Resolve(face.Primitive.ParentBlock!.Block, face.Polygon.Poly!.Attribute))
            .Select(surface => colour ? surface.ColourHash : surface.MaterialHash).Distinct().ToArray();
        return hashes.Length == 1 ? Name(hashes[0]) : $"Mixed ({hashes.Length} different assignments)";
    }
    private bool Compatible(IGrouping<int, CollisionGeoPrimViewModel>[] groups, uint hash, bool colour) =>
        groups.All(group =>
        {
            var slots = group.Select(alias => (colour ? Table(alias).Colors : Table(alias).Materials).IndexOf(hash)).ToArray();
            return slots.All(slot => slot >= 0 && slot < 32) && slots.Distinct().Count() == 1;
        });
    private GeoMaterialHeader Table(CollisionGeoPrimViewModel polygon) =>
        _catalog.GetTable(polygon.ParentPrim.ParentBlock!.Block) ?? throw new InvalidOperationException("A selected polygon has no material table.");
    private static int Slot(List<uint> values, uint hash)
    {
        var slot = values.IndexOf(hash);
        return slot >= 0 && slot < 32 ? slot : throw new InvalidOperationException("A chosen identity is missing from a selected polygon's table. No changes were made.");
    }
    private static int Offset(CollisionGeoPrimViewModel polygon) =>
        polygon.ParentPrim.Prim.Offset + 0x26 + polygon.ParentPrim.Children.IndexOf(polygon) * 8;
    private static string Name(uint hash) => $"{DictionaryFile.GetHashString(hash)} (0x{hash:X6})";
    private void SetStatus(string value) { _status = value; OnPropertyChanged(nameof(Status)); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
