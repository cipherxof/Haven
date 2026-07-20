using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HavenStudio;

namespace HavenStudio.Windows;

public partial class CryptoWindow : Window
{
    private static CryptoWindow? _instance;

    public CryptoWindow()
    {
        InitializeComponent();
    }

    public static void ShowWindow(Window owner, string filePath, string defaultKey)
    {
        if (_instance == null)
        {
            _instance = new CryptoWindow();
            _instance.Closed += (_, _) => _instance = null;
        }

        var vm = new CryptoWindowViewModel(filePath, defaultKey);
        _instance.DataContext = vm;
        _instance.Owner = owner;
        _instance.Show();
        _instance.Activate();
    }

    public static string BuildDefaultKey(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        var info = new DirectoryInfo(directory);
        var parent = info.Parent?.Name ?? string.Empty;
        return $"{parent}/{info.Name}";
    }

    private async void OnEncrypt(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CryptoWindowViewModel vm)
        {
            return;
        }

        if (vm.IsFolder)
        {
            var outputFolder = await PickOutputFolderAsync("Select Output Folder for Encrypted Files");
            if (outputFolder == null)
            {
                return;
            }
            vm.RunEncrypt(outputFolder);
        }
        else
        {
            vm.RunEncrypt();
        }
    }

    private async void OnDecrypt(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CryptoWindowViewModel vm)
        {
            return;
        }

        if (vm.IsFolder)
        {
            var outputFolder = await PickOutputFolderAsync("Select Output Folder for Decrypted Files");
            if (outputFolder == null)
            {
                return;
            }
            vm.RunDecrypt(outputFolder);
        }
        else
        {
            vm.RunDecrypt();
        }
    }

    private async Task<string?> PickOutputFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        return folder?.Path.LocalPath;
    }
}
