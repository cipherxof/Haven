using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Serilog;

namespace HavenStudio;

public enum GameType
{
    MetalGearSolid4,
    MetalGearArcade,
    MetalGearSolid3GcxOnly
}

public sealed class SettingsStore : INotifyPropertyChanged
{
    private static readonly ILogger _log = Log.ForContext<SettingsStore>();

    private static readonly Lazy<SettingsStore> LazyInstance = new(() => new SettingsStore());
    public static SettingsStore Current => LazyInstance.Value;

    private bool _loadSceneFromGcx = true;
    private GameType _selectedGame = GameType.MetalGearSolid4;
    private bool _autoLoadGeomWhenOpeningStage;
    private bool _isLoaded;

    private SettingsStore()
    {
        Load();
        UpdateEndianness();
    }

    private void UpdateEndianness()
    {
        var endianness = _selectedGame == GameType.MetalGearSolid4
            ? Extensions.Endianness.Big
            : Extensions.Endianness.Little;

        Extensions.EndianBinaryReader.DefaultEndianness = endianness;
        Extensions.EndianBinaryWriter.DefaultEndianness = endianness;
    }

    public bool LoadSceneFromGcx
    {
        get => _loadSceneFromGcx;
        set
        {
            if (_loadSceneFromGcx == value)
            {
                return;
            }

            _loadSceneFromGcx = value;
            OnPropertyChanged();
            Save();
        }
    }

    public bool AutoLoadGeomWhenOpeningStage
    {
        get => _autoLoadGeomWhenOpeningStage;
        set
        {
            if (_autoLoadGeomWhenOpeningStage == value)
            {
                return;
            }

            _autoLoadGeomWhenOpeningStage = value;
            OnPropertyChanged();
            Save();
        }
    }

    public GameType SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (_selectedGame == value)
            {
                return;
            }

            _selectedGame = value;
            OnPropertyChanged();
            UpdateEndianness();
            Save();
        }
    }

    public bool IsMgs3 => SelectedGame == GameType.MetalGearSolid3GcxOnly;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Load()
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<SettingsDto>(json);
            if (dto != null)
            {
                _loadSceneFromGcx = dto.LoadSceneFromGcx;
                _autoLoadGeomWhenOpeningStage = dto.AutoLoadGeomWhenOpeningStage;
                _selectedGame = dto.SelectedGame;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Settings] Failed to load");
        }
    }

    private void Save()
    {
        try
        {
            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var dto = new SettingsDto
            {
                LoadSceneFromGcx = _loadSceneFromGcx,
                AutoLoadGeomWhenOpeningStage = _autoLoadGeomWhenOpeningStage,
                SelectedGame = _selectedGame
            };
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Settings] Failed to save");
        }
    }

    private static string GetSettingsPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(baseDir, "HavenStudio", "settings.json");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class SettingsDto
    {
        public bool LoadSceneFromGcx { get; set; } = true;
        public bool AutoLoadGeomWhenOpeningStage { get; set; }
        public GameType SelectedGame { get; set; } = GameType.MetalGearSolid4;
    }
}
