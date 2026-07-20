using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HavenStudio.Services;
using HavenStudio.Services.Workspace;
using HavenStudio.Utils;

namespace HavenStudio.Windows;

public partial class TxnEditorWindow : Window
{
    private readonly WorkspacePath? _txnPath;
    private readonly IWorkspaceCatalog? _workspace;
    private readonly ObservableCollection<RowModel> _rows = new();
    private readonly ObservableCollection<RowModel> _displayRows = new();
    private TxnTextureEditorService? _service;
    private readonly ListBox? _textureList;
    private readonly TextBlock? _statusText;
    private readonly TextBlock? _pathText;
    private readonly TextBox? _filterTextBox;
    private readonly ComboBox? _sortComboBox;
    private readonly ComboBox? _sortDirectionComboBox;

    public TxnEditorWindow()
    {
        _txnPath = null;
        _workspace = null;

        InitializeComponent();

        _textureList = this.FindControl<ListBox>("TextureList");
        _statusText = this.FindControl<TextBlock>("StatusTextBlock");
        _pathText = this.FindControl<TextBlock>("TxnPathTextBlock");
        _filterTextBox = this.FindControl<TextBox>("FilterTextBox");
        _sortComboBox = this.FindControl<ComboBox>("SortComboBox");
        _sortDirectionComboBox = this.FindControl<ComboBox>("SortDirectionComboBox");

        if (_textureList != null)
        {
            _textureList.ItemsSource = _displayRows;
        }

        if (_pathText != null)
        {
            _pathText.Text = "TXN Editor";
        }

        ConfigureSortControls();
    }

    public TxnEditorWindow(WorkspacePath txnPath, IWorkspaceCatalog workspace)
    {
        _txnPath = txnPath ?? throw new ArgumentNullException(nameof(txnPath));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

        InitializeComponent();

        _textureList = this.FindControl<ListBox>("TextureList");
        _statusText = this.FindControl<TextBlock>("StatusTextBlock");
        _pathText = this.FindControl<TextBlock>("TxnPathTextBlock");
        _filterTextBox = this.FindControl<TextBox>("FilterTextBox");
        _sortComboBox = this.FindControl<ComboBox>("SortComboBox");
        _sortDirectionComboBox = this.FindControl<ComboBox>("SortDirectionComboBox");

        _service = new TxnTextureEditorService(_txnPath, _workspace);
        if (_textureList != null)
        {
            _textureList.ItemsSource = _displayRows;
        }

        if (_pathText != null)
        {
            _pathText.Text = $"TXN: {_txnPath}";
        }

        ConfigureSortControls();
        ReloadRows();
    }

    private RowModel? SelectedRow => _textureList?.SelectedItem as RowModel;

    private async void OnPreview(object? sender, RoutedEventArgs e)
    {
        var row = SelectedRow;
        if (row == null)
        {
            SetStatus("Select a texture first.");
            return;
        }

        try
        {
            await DdsPreviewService.ShowPreviewDialogAsync(
                this,
                $"Preview - {row.Name}",
                row.Entry.Image.Width,
                row.Entry.Image.Height,
                row.Entry.Format,
                row.Entry.MainTexture?.Data,
                row.Entry.MipTexture?.Data);
            SetStatus($"Previewed {row.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Preview failed: {ex.Message}");
        }
    }

    private void OnTextureListDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        OnPreview(sender, new RoutedEventArgs());
    }

    private void OnFilterChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilterAndSort();
    }

    private void OnSortChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyFilterAndSort();
    }

    private async void OnReplace(object? sender, RoutedEventArgs e)
    {
        var row = SelectedRow;
        if (row == null)
        {
            SetStatus("Select a texture to replace.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select DDS Texture",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("DDS") { Patterns = ["*.dds"] }
            ]
        });

        var file = files.FirstOrDefault();
        if (file == null)
        {
            return;
        }

        try
        {
            if (_service == null)
            {
                return;
            }

            _service.ReplaceTexture(row.Entry.Index, file.Path.LocalPath);
            ReloadRows();
            SetStatus($"Replaced texture {row.Entry.Index}");
        }
        catch (Exception ex)
        {
            SetStatus($"Replace failed: {ex.Message}");
        }
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        var row = SelectedRow;
        if (row == null)
        {
            SetStatus("Select a texture to delete.");
            return;
        }

        try
        {
            if (_service == null)
            {
                return;
            }

            _service.DeleteTexture(row.Entry.Index);
            ReloadRows();
            SetStatus($"Deleted texture {row.Entry.Index}");
        }
        catch (Exception ex)
        {
            SetStatus($"Delete failed: {ex.Message}");
        }
    }

    private async void OnAdd(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select DDS Texture",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("DDS") { Patterns = ["*.dds"] }
            ]
        });

        var ddsFile = files.FirstOrDefault();
        if (ddsFile == null)
        {
            return;
        }

        var mainContainer = await PickContainerAsync("Select Main DLZ/DLD Container");
        if (mainContainer == null)
        {
            SetStatus("Main container is required.");
            return;
        }

        var mipContainer = await PickContainerAsync("Select Mipmap DLZ/DLD Container (optional)", optional: true);

        try
        {
            if (_service == null)
            {
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(ddsFile.Path.LocalPath);
            uint hash = Utils.String.HashString(baseName);
            _service.AddTexture(ddsFile.Path.LocalPath, hash, hash, mainContainer.Path, mipContainer?.Path);
            ReloadRows();
            SetStatus($"Added texture '{baseName}' ({hash:X8})");
        }
        catch (Exception ex)
        {
            SetStatus($"Add failed: {ex.Message}");
        }
    }

    private void OnRefresh(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_txnPath is null || _workspace is null)
            {
                return;
            }

            _service = new TxnTextureEditorService(_txnPath, _workspace);
            ReloadRows();
            SetStatus("Reloaded.");
        }
        catch (Exception ex)
        {
            SetStatus($"Reload failed: {ex.Message}");
        }
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_service == null)
            {
                return;
            }

            _service.Save();
            SetStatus("Saved TXN and modified DLZ/DLD containers.");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
    }

    private async Task<TxnTextureEditorService.ContainerRef?> PickContainerAsync(string title, bool optional = false)
    {
        if (_service == null)
        {
            return null;
        }

        var options = _service.Containers
            .Select(container => new ContainerChoice(container.Name, container.Path, container))
            .ToList();

        if (options.Count == 0)
        {
            return null;
        }

        var window = new Window
        {
            Title = title,
            Width = 620,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var listBox = new ListBox { ItemsSource = options };

        var okButton = new Button { Content = "OK", Width = 88 };
        var skipButton = new Button { Content = optional ? "Skip" : "Cancel", Width = 88 };

        TxnTextureEditorService.ContainerRef? result = null;
        okButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is ContainerChoice choice)
            {
                result = choice.Container;
            }

            window.Close();
        };
        skipButton.Click += (_, _) =>
        {
            result = null;
            window.Close();
        };

        var contentGrid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        contentGrid.Children.Add(listBox);
        var buttonRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(8)
        };
        buttonRow.Children.Add(skipButton);
        buttonRow.Children.Add(okButton);
        Grid.SetRow(buttonRow, 1);
        contentGrid.Children.Add(buttonRow);

        window.Content = contentGrid;

        await window.ShowDialog(this);
        return result;
    }

    private void ReloadRows()
    {
        if (_service == null)
        {
            return;
        }

        _rows.Clear();
        foreach (var entry in _service.Entries)
        {
            _rows.Add(new RowModel(entry));
        }

        ApplyFilterAndSort();
    }

    private void SetStatus(string status)
    {
        if (_statusText != null)
        {
            _statusText.Text = status;
        }
    }

    private void ConfigureSortControls()
    {
        if (_sortComboBox != null)
        {
            _sortComboBox.ItemsSource = new[]
            {
                "Index",
                "Name",
                "Format",
                "Resolution",
                "Main Container",
                "Mip Container"
            };
            _sortComboBox.SelectedIndex = 0;
        }

        if (_sortDirectionComboBox != null)
        {
            _sortDirectionComboBox.ItemsSource = new[]
            {
                "Ascending",
                "Descending"
            };
            _sortDirectionComboBox.SelectedIndex = 0;
        }
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<RowModel> query = _rows;

        var filter = _filterTextBox?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var needle = filter.ToLowerInvariant();
            query = query.Where(row =>
                row.NameLower.Contains(needle)
                || row.HashTextLower.Contains(needle)
                || row.MainContainerLower.Contains(needle)
                || row.MipContainerLower.Contains(needle)
                || row.FormatLower.Contains(needle));
        }

        var sortKey = _sortComboBox?.SelectedItem as string ?? "Index";
        bool descending = (_sortDirectionComboBox?.SelectedItem as string) == "Descending";

        query = sortKey switch
        {
            "Name" => descending ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
            "Format" => descending ? query.OrderByDescending(r => r.Format) : query.OrderBy(r => r.Format),
            "Resolution" => descending ? query.OrderByDescending(r => r.ResolutionSort) : query.OrderBy(r => r.ResolutionSort),
            "Main Container" => descending ? query.OrderByDescending(r => r.MainContainer) : query.OrderBy(r => r.MainContainer),
            "Mip Container" => descending ? query.OrderByDescending(r => r.MipContainer) : query.OrderBy(r => r.MipContainer),
            _ => descending ? query.OrderByDescending(r => r.Entry.Index) : query.OrderBy(r => r.Entry.Index)
        };

        _displayRows.Clear();
        foreach (var row in query)
        {
            _displayRows.Add(row);
        }

        SetStatus($"{_displayRows.Count} textures");
    }

    private sealed class ContainerChoice
    {
        public ContainerChoice(string name, string path, TxnTextureEditorService.ContainerRef container)
        {
            Name = name;
            Path = path;
            Container = container;
        }

        public string Name { get; }
        public string Path { get; }
        public TxnTextureEditorService.ContainerRef Container { get; }

        public override string ToString()
        {
            return $"{Name}  ({Path})";
        }
    }

    private sealed class RowModel
    {
        public RowModel(TxnTextureEditorService.TxnTextureEntry entry)
        {
            Entry = entry;
        }

        public TxnTextureEditorService.TxnTextureEntry Entry { get; }
        public string IndexText => Entry.Index.ToString("D4");
        public string Name => Entry.DisplayName;
        public string HashText => Entry.HashText;
        public string Resolution => Entry.Resolution;
        public string Format => Entry.Format;
        public string MainContainer => Entry.MainContainerText;
        public string MipContainer => Entry.MipContainerText;
        public string NameLower => Name.ToLowerInvariant();
        public string HashTextLower => HashText.ToLowerInvariant();
        public string FormatLower => Format.ToLowerInvariant();
        public string MainContainerLower => MainContainer.ToLowerInvariant();
        public string MipContainerLower => MipContainer.ToLowerInvariant();
        public int ResolutionSort => (Entry.Image.Width * 10000) + Entry.Image.Height;
    }
}
