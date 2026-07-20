using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using HavenStudio.Formats.Geo;

namespace HavenStudio.Editors;

public sealed class CollisionPrimViewModel : INotifyPropertyChanged
{
    private readonly Action _onChanged;
    private string _lengthText;
    private string _typeText;
    private string _field002Text;
    private string _field003Text;
    private string _nextText;
    private string _prevText;
    private string _childText;
    private string _flagText;
    private string _attributeText;
    private string _nameText;
    private string _field014Text;
    private string _dataText;
    private bool _isVisible = true;
    private bool _isExpanded;

    public ObservableCollection<CollisionGeoPrimViewModel> Children { get; } = new();
    public Geom Prim { get; }
    public int Index { get; }
    internal CollisionBlockViewModel? ParentBlock { get; set; }
    public string DisplayName => $"{ResolvePrimName()} ({Prim.GetPrimType()})";

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
            foreach (var child in Children)
            {
                child.SetVisibilityFromParent(value);
            }
            ParentBlock?.UpdateVisibilityFromChildren();
            OnPropertyChanged();
        }
    }

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
            if (TryParseNumber(value, out var parsed) && parsed <= uint.MaxValue)
            {
                Prim.Flag = (uint)parsed;
                _flagText = FormatHex(Prim.Flag);
                SyncHeaderFromFlag();
                _onChanged();
                OnPropertyChanged(nameof(DisplayName));
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
                Prim.Attribute = parsed;
                _attributeText = FormatHex(Prim.Attribute);
                _onChanged();
            }
            else
            {
                _attributeText = value;
            }
            OnPropertyChanged();
        }
    }

    public string NameText
    {
        get => _nameText;
        set
        {
            if (_nameText == value)
            {
                return;
            }
            if (TryParseNumber(value, out var parsed) && parsed <= uint.MaxValue)
            {
                Prim.Name = (uint)parsed;
                _nameText = FormatHex(Prim.Name);
                _onChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
            else
            {
                _nameText = value;
            }
            OnPropertyChanged();
        }
    }

    public string LengthText
    {
        get => _lengthText;
        set
        {
            if (_lengthText == value)
            {
                return;
            }
            if (TryParseByte(value, out var parsed))
            {
                Prim.Length = parsed;
                _lengthText = FormatHex(parsed);
                SyncFlagFromHeader();
                _onChanged();
            }
            else
            {
                _lengthText = value;
            }
            OnPropertyChanged();
        }
    }

    public string TypeText
    {
        get => _typeText;
        set
        {
            if (_typeText == value)
            {
                return;
            }
            if (TryParseByte(value, out var parsed))
            {
                Prim.Type = parsed;
                _typeText = FormatHex(parsed);
                SyncFlagFromHeader();
                _onChanged();
            }
            else
            {
                _typeText = value;
            }
            OnPropertyChanged();
        }
    }

    public string Field002Text
    {
        get => _field002Text;
        set
        {
            if (_field002Text == value)
            {
                return;
            }
            if (TryParseByte(value, out var parsed))
            {
                Prim.Field002 = parsed;
                _field002Text = FormatHex(parsed);
                SyncFlagFromHeader();
                _onChanged();
            }
            else
            {
                _field002Text = value;
            }
            OnPropertyChanged();
        }
    }

    public string Field003Text
    {
        get => _field003Text;
        set
        {
            if (_field003Text == value)
            {
                return;
            }
            if (TryParseByte(value, out var parsed))
            {
                Prim.Field003 = parsed;
                _field003Text = FormatHex(parsed);
                SyncFlagFromHeader();
                _onChanged();
            }
            else
            {
                _field003Text = value;
            }
            OnPropertyChanged();
        }
    }

    public string NextText
    {
        get => _nextText;
        set
        {
            if (_nextText == value)
            {
                return;
            }
            if (TryParseInt(value, out var parsed))
            {
                Prim.Next = parsed;
                _nextText = FormatHex((uint)parsed);
                _onChanged();
            }
            else
            {
                _nextText = value;
            }
            OnPropertyChanged();
        }
    }

    public string PrevText
    {
        get => _prevText;
        set
        {
            if (_prevText == value)
            {
                return;
            }
            if (TryParseInt(value, out var parsed))
            {
                Prim.Prev = parsed;
                _prevText = FormatHex((uint)parsed);
                _onChanged();
            }
            else
            {
                _prevText = value;
            }
            OnPropertyChanged();
        }
    }

    public string ChildText
    {
        get => _childText;
        set
        {
            if (_childText == value)
            {
                return;
            }
            if (TryParseInt(value, out var parsed))
            {
                Prim.Child = parsed;
                _childText = FormatHex((uint)parsed);
                _onChanged();
            }
            else
            {
                _childText = value;
            }
            OnPropertyChanged();
        }
    }

    public string Field014Text
    {
        get => _field014Text;
        set
        {
            if (_field014Text == value)
            {
                return;
            }
            if (TryParseInt(value, out var parsed))
            {
                Prim.Field014 = parsed;
                _field014Text = FormatHex((uint)parsed);
                _onChanged();
            }
            else
            {
                _field014Text = value;
            }
            OnPropertyChanged();
        }
    }

    public string DataText
    {
        get => _dataText;
        set
        {
            if (_dataText == value)
            {
                return;
            }
            if (TryParseByteArray(value, out var parsed))
            {
                Prim.Data = parsed;
                _dataText = FormatByteArray(parsed);
                _onChanged();
            }
            else
            {
                _dataText = value;
            }
            OnPropertyChanged();
        }
    }

    public CollisionPrimViewModel(Geom prim, int index, Action onChanged)
    {
        Prim = prim;
        Index = index;
        _onChanged = onChanged;
        _lengthText = FormatHex(prim.Length);
        _typeText = FormatHex(prim.Type);
        _field002Text = FormatHex(prim.Field002);
        _field003Text = FormatHex(prim.Field003);
        _nextText = FormatHex((uint)prim.Next);
        _prevText = FormatHex((uint)prim.Prev);
        _childText = FormatHex((uint)prim.Child);
        _flagText = FormatHex(prim.Flag);
        _attributeText = FormatHex(prim.Attribute);
        _nameText = FormatHex(prim.Name);
        _field014Text = FormatHex((uint)prim.Field014);
        _dataText = FormatByteArray(prim.Data);
        BuildChildren();
    }

    internal void SetVisibilityFromParent(bool value)
    {
        if (_isVisible == value)
        {
            return;
        }
        _isVisible = value;
        foreach (var child in Children)
        {
            child.SetVisibilityFromParent(value);
        }
        OnPropertyChanged(nameof(IsVisible));
    }

    internal void UpdateVisibilityFromChildren()
    {
        if (Children.Count == 0)
        {
            return;
        }
        var anyVisible = Children.Any(child => child.IsVisible);
        if (_isVisible == anyVisible)
        {
            return;
        }
        _isVisible = anyVisible;
        ParentBlock?.UpdateVisibilityFromChildren();
        OnPropertyChanged(nameof(IsVisible));
    }

    internal void NotifyChanged() => _onChanged();

    private void BuildChildren()
    {
        Children.Clear();
        switch (Prim.GetPrimType())
        {
            case Geom.Primitive.GEO_DOT:
                AddChildren("Dot", Prim.Dot?.Length ?? 0, null);
                break;
            case Geom.Primitive.GEO_LINE:
                AddChildren("Line", Prim.Line?.Length ?? 0, null);
                break;
            case Geom.Primitive.GEO_POLY:
                if (Prim.Poly != null)
                {
                    for (var index = 0; index < Prim.Poly.Length; index++)
                    {
                        Children.Add(new CollisionGeoPrimViewModel($"Poly {index}", this, Prim.Poly[index]));
                    }
                }
                break;
            case Geom.Primitive.GEO_BOX:
                AddChildren("Box", Prim.Box?.Length ?? 0, null);
                break;
            case Geom.Primitive.GEO_FIELD:
                AddChildren("Field", Prim.Field?.Length ?? 0, null);
                break;
            case Geom.Primitive.GEO_REF:
                AddChildren("Ref", Prim.Ref?.Length ?? 0, null);
                break;
        }
    }

    private void AddChildren(string label, int count, GeoPrimPoly? poly)
    {
        for (var index = 0; index < count; index++)
        {
            Children.Add(new CollisionGeoPrimViewModel($"{label} {index}", this, poly));
        }
    }

    private string ResolvePrimName()
    {
        var resolved = HavenStudio.Utils.DictionaryFile.GetHashString(Prim.Name);
        return string.IsNullOrWhiteSpace(resolved) ? $"0x{Prim.Name:X4}" : resolved;
    }

    private void SyncFlagFromHeader()
    {
        Prim.Flag = (uint)(Prim.Field003 | (Prim.Field002 << 8) | (Prim.Type << 16) | (Prim.Length << 24));
        _flagText = FormatHex(Prim.Flag);
        OnPropertyChanged(nameof(FlagText));
        OnPropertyChanged(nameof(DisplayName));
    }

    private void SyncHeaderFromFlag()
    {
        var bytes = BitConverter.GetBytes(Prim.Flag);
        Prim.Length = bytes[3];
        Prim.Type = bytes[2];
        Prim.Field002 = bytes[1];
        Prim.Field003 = bytes[0];
        _lengthText = FormatHex(Prim.Length);
        _typeText = FormatHex(Prim.Type);
        _field002Text = FormatHex(Prim.Field002);
        _field003Text = FormatHex(Prim.Field003);
        OnPropertyChanged(nameof(LengthText));
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(Field002Text));
        OnPropertyChanged(nameof(Field003Text));
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

    private static bool TryParseByte(string text, out byte value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        text = text.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? byte.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseInt(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        text = text.Trim();
        if (!text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
        if (!uint.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }
        value = unchecked((int)parsed);
        return true;
    }

    private static bool TryParseByteArray(string text, out byte[] value)
    {
        value = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }
        text = text.Replace(" ", string.Empty).Replace(",", string.Empty);
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }
        if (text.Length % 2 != 0)
        {
            return false;
        }
        var bytes = new byte[text.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            if (!byte.TryParse(text.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[index]))
            {
                return false;
            }
        }
        value = bytes;
        return true;
    }

    private static string FormatByteArray(byte[]? data) => data == null || data.Length == 0
        ? string.Empty
        : $"0x{BitConverter.ToString(data).Replace("-", string.Empty)}";

    private static string FormatHex(ulong value) => $"0x{value:X}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
