using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using HavenStudio.Services;

namespace HavenStudio;

public sealed class MinimapViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly List<MinimapLevel> _levels = new();
    private int _selectedIndex = -1;
    private bool _isLoading;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsLoading => _isLoading;
    public bool HasLevels => _levels.Count > 0;
    public bool IsVisible => _isLoading || HasLevels;
    public bool CanNavigate => _levels.Count > 1;

    public MinimapLevel? CurrentLevel =>
        _selectedIndex >= 0 && _selectedIndex < _levels.Count ? _levels[_selectedIndex] : null;

    public Bitmap? CurrentImage => CurrentLevel?.Image;

    public string CurrentLabel => CurrentLevel?.Label ?? string.Empty;

    public string CounterText => HasLevels ? $"{_selectedIndex + 1} / {_levels.Count}" : string.Empty;

    public string StatusText => _isLoading ? "Loading minimap…" : string.Empty;

    public void SetLoading()
    {
        ClearLevels();
        _isLoading = true;
        RaiseAll();
    }

    public void SetEmpty()
    {
        ClearLevels();
        _isLoading = false;
        RaiseAll();
    }

    public void SetLevels(IEnumerable<MinimapLevel> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        ClearLevels();
        _levels.AddRange(levels);
        _selectedIndex = _levels.Count > 0 ? 0 : -1;
        _isLoading = false;
        RaiseAll();
    }

    public void Next()
    {
        if (_levels.Count > 1)
        {
            _selectedIndex = (_selectedIndex + 1) % _levels.Count;
            RaiseSelection();
        }
    }

    public void Previous()
    {
        if (_levels.Count > 1)
        {
            _selectedIndex = (_selectedIndex - 1 + _levels.Count) % _levels.Count;
            RaiseSelection();
        }
    }

    private void ClearLevels()
    {
        foreach (var level in _levels)
        {
            level.Image.Dispose();
        }

        _levels.Clear();
        _selectedIndex = -1;
    }

    private void RaiseSelection()
    {
        OnPropertyChanged(nameof(CurrentLevel));
        OnPropertyChanged(nameof(CurrentImage));
        OnPropertyChanged(nameof(CurrentLabel));
        OnPropertyChanged(nameof(CounterText));
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(HasLevels));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(CanNavigate));
        OnPropertyChanged(nameof(StatusText));
        RaiseSelection();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        ClearLevels();
    }

    public sealed record MinimapLevel(
        string Label,
        Bitmap Image,
        MinimapProjection? Projection);
}
