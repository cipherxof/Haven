using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using HavenStudio.Utils;

namespace HavenStudio.Editors;

public sealed record OctocamoRemapMaterial(uint Hash, string DisplayName);
public sealed record OctocamoPatternOption(uint Hash, string DisplayName, IImage? Preview, bool Registered);

public sealed class OctocamoRemapViewModel : INotifyPropertyChanged
{
    private readonly OctocamoSurfaceCatalog _catalog;
    private readonly Action _afterEdit;
    private readonly OctocamoPatternOption[] _patterns;
    private OctocamoRemapMaterial? _selectedMaterial;
    private OctocamoPatternOption? _selectedPattern;
    private bool _refreshingMaterials;
    private string _search = string.Empty;
    private string _status = "Choose a replacement, then Apply or Apply selected & Done. Changes remain pending until Save Map. Selecting a thumbnail alone does not write a mapping.";

    public OctocamoRemapViewModel(OctocamoSurfaceCatalog catalog, uint? selectedMaterial, Action afterEdit,
        Func<uint, IImage?>? previewImage = null)
    {
        _catalog = catalog;
        _afterEdit = afterEdit;
        _patterns = catalog.AvailablePatternHashes.Select(hash => new OctocamoPatternOption(hash,
            $"{Name(hash)}{(catalog.IsPatternRegistered(hash) ? string.Empty : " — not registered")}",
            (previewImage ?? catalog.PatternImage)(hash), catalog.IsPatternRegistered(hash))).ToArray();
        RefreshMaterials(selectedMaterial);
    }

    public IReadOnlyList<OctocamoRemapMaterial> Materials { get; private set; } = [];
    public IEnumerable<OctocamoPatternOption> Patterns => _patterns.Where(option =>
        option.DisplayName.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase));
    public string Search
    {
        get => _search;
        set { if (_search == value) return; _search = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(Patterns)); }
    }
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public string Summary => $"{Materials.Count} GEOM materials • {_patterns.Length} decoded SLOT patterns • {_catalog.MappingCount}/32 OCTT rows • {_catalog.AvailableMappingRows} rows available/reclaimable for unassigned materials";
    public string CurrentPatternText => _selectedMaterial == null ? "Select a material" :
        _catalog.GetPatternHash(_selectedMaterial.Hash) is var hash && hash != 0 ? Name(hash) : "Unassigned material — choose a pattern to create its mapping";
    public IImage? CurrentPreview => _selectedMaterial == null ? null :
        _patterns.FirstOrDefault(option => option.Hash == _catalog.GetPatternHash(_selectedMaterial.Hash))?.Preview;
    public bool HasPendingProposal => _selectedMaterial != null && _selectedPattern != null &&
        _catalog.GetPatternHash(_selectedMaterial.Hash) != _selectedPattern.Hash;
    public bool CanApply => _selectedMaterial != null && _selectedPattern?.Registered == true &&
        _catalog.GetPatternHash(_selectedMaterial.Hash) != _selectedPattern.Hash;
    public bool CanReset => _catalog.IsMappingDirty;
    public OctocamoRemapMaterial? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            if (_refreshingMaterials || _selectedMaterial == value) return;
            _selectedMaterial = value;
            Search = string.Empty;
            SelectedPattern = _patterns.FirstOrDefault(option => option.Hash == (value == null ? 0 : _catalog.GetPatternHash(value.Hash)));
            NotifyCurrent();
        }
    }
    public OctocamoPatternOption? SelectedPattern
    {
        get => _selectedPattern;
        set { if (_refreshingMaterials || _selectedPattern == value) return; _selectedPattern = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanApply)); OnPropertyChanged(nameof(HasPendingProposal)); }
    }

    public bool Apply()
    {
        if (_selectedMaterial == null || _selectedPattern == null) return false;
        try
        {
            var material = _selectedMaterial.Hash;
            var pattern = _selectedPattern.Hash;
            _catalog.RemapPattern(material, pattern);
            _afterEdit();
            RefreshMaterials(material);
            Status = $"Pending: {Name(material)} → {Name(pattern)} throughout the stage. Save Map writes the OCTT in cache.dar, not a GEOM texture change.";
            return true;
        }
        catch (Exception exception) { Status = exception.Message; return false; }
    }

    // Done is a commit gesture, not a silent dismissal of the selected proposal.
    // A capacity/registration error keeps the editor open with its explanation.
    public bool TryFinish() => !HasPendingProposal || Apply();

    public void Reset()
    {
        var material = _selectedMaterial?.Hash;
        _catalog.ResetMappings();
        _afterEdit();
        RefreshMaterials(material);
        Status = "All unsaved material-to-pattern remaps reverted to the last saved table.";
    }

    private void RefreshMaterials(uint? selected)
    {
        // Replacing ItemsSource can briefly send SelectedItem=null back through
        // two-way ListBox bindings. Do not let that erase the committed choice.
        _refreshingMaterials = true;
        try
        {
            Materials = _catalog.AllMaterialHashes.Select(hash => new OctocamoRemapMaterial(hash,
                $"{Name(hash)} → {(_catalog.GetPatternHash(hash) == 0 ? "UNASSIGNED" : Name(_catalog.GetPatternHash(hash)))}")).ToArray();
            _selectedMaterial = Materials.FirstOrDefault(option => option.Hash == selected) ?? Materials.FirstOrDefault();
            _selectedPattern = _patterns.FirstOrDefault(option => option.Hash == (_selectedMaterial == null ? 0 : _catalog.GetPatternHash(_selectedMaterial.Hash)));
            OnPropertyChanged(nameof(Materials));
            OnPropertyChanged(nameof(SelectedPattern));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(CanReset));
            NotifyCurrent();
        }
        finally { _refreshingMaterials = false; }
    }
    private void NotifyCurrent()
    {
        OnPropertyChanged(nameof(SelectedMaterial));
        OnPropertyChanged(nameof(CurrentPatternText));
        OnPropertyChanged(nameof(CurrentPreview));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(HasPendingProposal));
    }
    private static string Name(uint hash) => $"{DictionaryFile.GetHashString(hash)} (0x{hash:X6})";
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
