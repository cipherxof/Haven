using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using HavenStudio.Formats.Geo;

namespace HavenStudio.Editors;

public sealed class CollisionEffectViewModel : INotifyPropertyChanged
{
    private readonly Action _onChanged;
    private readonly Action<CollisionEffectViewModel> _onEffectChanged;
    private bool _isVisible;
    private bool _renderAsFlag;
    private string _nameText;
    private string _indexText;
    private float _x;
    private float _y;
    private float _z;
    private float _w;
    private float _rotationX;
    private float _rotationY;
    private float _rotationZ;
    private bool _isExpanded;

    public GeoEffect Effect { get; }
    public ObservableCollection<CollisionEffectViewModel> Children { get; } = new();
    public ObservableCollection<CollisionEffectViewModel> VisibleChildren { get; } = new();
    public CollisionEffectViewModel? Parent { get; set; }
    public string DisplayName => BuildDisplayName();
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
                child.IsVisible = value;
            }
            _onEffectChanged(this);
            OnPropertyChanged();
        }
    }

    public bool RenderAsFlag
    {
        get => _renderAsFlag;
        set
        {
            if (_renderAsFlag == value)
            {
                return;
            }

            _renderAsFlag = value;
            _onEffectChanged(this);
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

            if (TryParseNumber(value, out var parsed) && parsed <= int.MaxValue)
            {
                Effect.Name = (int)parsed;
                _nameText = FormatHex((ulong)Effect.Name);
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

    public string IndexText
    {
        get => _indexText;
        set
        {
            if (_indexText == value)
            {
                return;
            }

            if (TryParseNumber(value, out var parsed) && parsed <= int.MaxValue)
            {
                Effect.Index = (int)parsed;
                _indexText = FormatHex((ulong)Effect.Index);
                _onChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
            else
            {
                _indexText = value;
            }

            OnPropertyChanged();
        }
    }

    public float X
    {
        get => _x;
        set
        {
            if (Math.Abs(_x - value) < 0.0001f)
            {
                return;
            }

            _x = value;
            Effect.X = value;
            _onChanged();
            _onEffectChanged(this);
            OnPropertyChanged();
        }
    }

    public float Y
    {
        get => _y;
        set
        {
            if (Math.Abs(_y - value) < 0.0001f)
            {
                return;
            }

            _y = value;
            Effect.Y = value;
            _onChanged();
            _onEffectChanged(this);
            OnPropertyChanged();
        }
    }

    public float Z
    {
        get => _z;
        set
        {
            if (Math.Abs(_z - value) < 0.0001f)
            {
                return;
            }

            _z = value;
            Effect.Z = value;
            _onChanged();
            _onEffectChanged(this);
            OnPropertyChanged();
        }
    }

    public void SetPosition(float x, float y, float z)
    {
        _x = x;
        _y = y;
        _z = z;
        Effect.X = x;
        Effect.Y = y;
        Effect.Z = z;
        _onChanged();
        _onEffectChanged(this);
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
        OnPropertyChanged(nameof(Z));
    }

    public float W
    {
        get => _w;
        set
        {
            if (Math.Abs(_w - value) < 0.0001f)
            {
                return;
            }

            _w = value;
            Effect.W = value;
            _onChanged();
            _onEffectChanged(this);
            OnPropertyChanged();
        }
    }

    public float RotationX
    {
        get => _rotationX;
        set => SetRotation(ref _rotationX, value, axis: 0);
    }

    public float RotationY
    {
        get => _rotationY;
        set => SetRotation(ref _rotationY, value, axis: 1);
    }

    public float RotationZ
    {
        get => _rotationZ;
        set => SetRotation(ref _rotationZ, value, axis: 2);
    }

    public CollisionEffectViewModel(GeoEffect effect, Action onChanged, Action<CollisionEffectViewModel> onEffectChanged)
    {
        Effect = effect;
        _onChanged = onChanged;
        _onEffectChanged = onEffectChanged;
        _isVisible = true;
        _renderAsFlag = false;
        _nameText = FormatHex((ulong)effect.Name);
        _indexText = FormatHex((ulong)effect.Index);
        _x = effect.X;
        _y = effect.Y;
        _z = effect.Z;
        _w = effect.W;
        _rotationX = effect.RotationX;
        _rotationY = effect.RotationY;
        _rotationZ = effect.RotationZ;
        VisibleChildren = new ObservableCollection<CollisionEffectViewModel>(Children);
    }

    private void SetRotation(ref float field, float value, int axis)
    {
        if (Math.Abs(field - value) < 0.0001f)
        {
            return;
        }

        field = value;
        switch (axis)
        {
            case 0: Effect.RotationX = value; break;
            case 1: Effect.RotationY = value; break;
            case 2: Effect.RotationZ = value; break;
        }
        _onChanged();
        _onEffectChanged(this);
        OnPropertyChanged(axis switch
        {
            0 => nameof(RotationX),
            1 => nameof(RotationY),
            _ => nameof(RotationZ)
        });
    }

    private string BuildDisplayName()
    {
        var hash = unchecked((uint)Effect.Name);
        var resolved = HavenStudio.Utils.DictionaryFile.GetHashString(hash);
        if (string.IsNullOrWhiteSpace(resolved) ||
            string.Equals(resolved, hash.ToString("X4"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(resolved, hash.ToString("X8"), StringComparison.OrdinalIgnoreCase))
        {
            return $"0x{hash:X8}";
        }

        return resolved;
    }

    internal bool ApplyFilter(string filter)
    {
        VisibleChildren.Clear();
        if (string.IsNullOrWhiteSpace(filter))
        {
            foreach (var child in Children)
            {
                child.ApplyFilter(string.Empty);
                VisibleChildren.Add(child);
            }
            return true;
        }

        var match = DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase);
        var childMatch = false;
        foreach (var child in Children)
        {
            if (child.ApplyFilter(filter))
            {
                VisibleChildren.Add(child);
                childMatch = true;
            }
        }

        return match || childMatch;
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
