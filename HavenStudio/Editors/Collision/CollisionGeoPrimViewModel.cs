using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using HavenStudio.Formats.Geo;

namespace HavenStudio.Editors;

public sealed class CollisionGeoPrimViewModel : INotifyPropertyChanged
{
    private string _attributeText;
    private readonly string _name;
    private bool _isVisible = true;

    public CollisionGeoPrimViewModel(string name, CollisionPrimViewModel parentPrim, GeoPrimPoly? poly)
    {
        _name = name;
        ParentPrim = parentPrim;
        Poly = poly;
        _attributeText = poly == null ? string.Empty : $"0x{poly.Attribute:X}";
    }

    public string DisplayName => BuildDisplayName();
    public CollisionPrimViewModel ParentPrim { get; }
    public GeoPrimPoly? Poly { get; }
    public bool HasAttribute => Poly != null;
    public bool IsExpanded { get; set; }
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
            ParentPrim.UpdateVisibilityFromChildren();
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

            if (Poly != null && TryParseUShort(value, out var parsed))
            {
                Poly.Attribute = parsed;
                _attributeText = $"0x{Poly.Attribute:X}";
                ParentPrim.NotifyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
            else
            {
                _attributeText = value;
            }

            OnPropertyChanged();
        }
    }

    internal void SetVisibilityFromParent(bool value)
    {
        if (_isVisible == value)
        {
            return;
        }

        _isVisible = value;
        OnPropertyChanged(nameof(IsVisible));
    }

    private static bool TryParseUShort(string text, out ushort value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ushort.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private string BuildDisplayName() => Poly == null ? _name : $"{_name} | Attr {_attributeText}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
