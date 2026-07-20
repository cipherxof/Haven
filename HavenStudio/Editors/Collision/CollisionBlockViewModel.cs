using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using HavenStudio.Formats.Geo;

namespace HavenStudio.Editors;

public sealed class CollisionBlockViewModel : INotifyPropertyChanged
{
    private readonly Action _onChanged;
    private readonly Action<CollisionBlockViewModel> _onVisibilityChanged;
    private string _flagText;
    private string _attributeText;
    private bool _isVisible = true;
    private bool _isExpanded;
    private ObservableCollection<CollisionPrimViewModel> _visiblePrims = new();

    public GeoBlock Block { get; }
    public int Index { get; }
    public IReadOnlyList<CollisionPrimViewModel> Prims { get; }
    public ObservableCollection<CollisionPrimViewModel> VisiblePrims
    {
        get => _visiblePrims;
        private set
        {
            _visiblePrims = value;
            OnPropertyChanged();
        }
    }
    public string DisplayName => $"Block #{Index}";
    public int VertexOffset => Block.VertexOffset;
    public int FaceOffset => Block.FaceOffset;
    public int MaterialOffset => Block.MaterialOffset;
    public ushort Size => Block.Size;
    public ushort Tail => Block.Tail;
    public ushort Head => Block.Head;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public string FlagText
    {
        get => _flagText;
        set
        {
            if (_flagText == value)
            {
                return;
            }

            if (TryParseNumber(value, out var parsed) && parsed <= byte.MaxValue)
            {
                Block.Flag = (byte)parsed;
                _flagText = FormatHex(Block.Flag);
                _onChanged();
            }
            else
            {
                _flagText = value;
            }

            OnPropertyChanged();
        }
    }

    public string AttributeText
    {
        get => _attributeText;
        set
        {
            if (_attributeText == value)
            {
                return;
            }

            if (TryParseNumber(value, out var parsed))
            {
                Block.Attribute = parsed;
                _attributeText = FormatHex(Block.Attribute);
                _onChanged();
            }
            else
            {
                _attributeText = value;
            }

            OnPropertyChanged();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            foreach (var prim in Prims)
            {
                prim.SetVisibilityFromParent(value);
            }
            _onVisibilityChanged(this);
            OnPropertyChanged();
        }
    }

    public CollisionBlockViewModel(GeoBlock block, int index, IReadOnlyList<CollisionPrimViewModel> prims, Action onChanged, Action<CollisionBlockViewModel> onVisibilityChanged)
    {
        Block = block;
        Index = index;
        Prims = prims;
        _onChanged = onChanged;
        _onVisibilityChanged = onVisibilityChanged;
        _flagText = FormatHex(block.Flag);
        _attributeText = FormatHex(block.Attribute);
        VisiblePrims = new ObservableCollection<CollisionPrimViewModel>(prims);
    }

    internal bool ApplyPrimFilter(string filter)
    {
        VisiblePrims.Clear();
        if (string.IsNullOrWhiteSpace(filter))
        {
            foreach (var prim in Prims)
            {
                VisiblePrims.Add(prim);
            }
            return Prims.Count > 0;
        }

        var any = false;
        foreach (var prim in Prims)
        {
            if (prim.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                prim.Children.Any(child => child.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                VisiblePrims.Add(prim);
                any = true;
            }
        }

        return any;
    }

    internal void UpdateVisibilityFromChildren()
    {
        var anyVisible = Prims.Any(prim => prim.IsVisible);
        if (_isVisible == anyVisible)
        {
            return;
        }

        _isVisible = anyVisible;
        _onVisibilityChanged(this);
        OnPropertyChanged(nameof(IsVisible));
    }

    private static bool TryParseNumber(string text, out ulong value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string FormatHex(ulong value) => $"0x{value:X}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
