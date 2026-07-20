using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using HavenStudio.Services;

namespace HavenStudio;

public sealed class MinimapSpawnDot
{
    public double Left { get; init; }
    public double Top { get; init; }
    public double Diameter { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class MinimapWindowViewModel : INotifyPropertyChanged
{
    private readonly double _dotRadius;

    private SpawnGroup? _selectedGroup;

    public MinimapWindowViewModel(Bitmap mapImage, string title, SpawnGroup root)
    {
        MapImage = mapImage;
        Title = title;
        MapWidth = mapImage.PixelSize.Width;
        MapHeight = mapImage.PixelSize.Height;
        // Radius in map-pixel space so dots read at a consistent on-screen size after the Viewbox scales.
        _dotRadius = Math.Max(MapWidth, MapHeight) / 96.0;
        Groups = new ObservableCollection<SpawnGroup> { root };
        Markers = new ObservableCollection<MinimapSpawnDot>();
        SelectedGroup = root;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Bitmap MapImage { get; }
    public string Title { get; }

    /// <summary>Overlay canvas size in the map texture's own pixels, so the aspect ratio matches the map.</summary>
    public double MapWidth { get; }
    public double MapHeight { get; }

    public ObservableCollection<SpawnGroup> Groups { get; }
    public ObservableCollection<MinimapSpawnDot> Markers { get; }

    public SpawnGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (ReferenceEquals(_selectedGroup, value))
            {
                return;
            }

            _selectedGroup = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectionText));
            RebuildMarkers();
        }
    }

    public string SelectionText => _selectedGroup is { } group
        ? $"{group.Label} — {group.SpawnCount} spawn{(group.SpawnCount == 1 ? "" : "s")}"
        : "Select a group";

    private void RebuildMarkers()
    {
        Markers.Clear();
        if (_selectedGroup is null)
        {
            return;
        }

        foreach (var spawn in _selectedGroup.Spawns)
        {
            Markers.Add(new MinimapSpawnDot
            {
                Left = spawn.U * MapWidth - _dotRadius,
                Top = spawn.V * MapHeight - _dotRadius,
                Diameter = _dotRadius * 2,
                Label = spawn.Label
            });
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
