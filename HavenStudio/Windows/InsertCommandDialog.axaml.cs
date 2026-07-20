using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Windows;

public partial class InsertCommandDialog : Window
{
    private readonly IWorkspaceCatalog? _workspace;
    private List<ModelPickerItem>? _mdnModels;

    public byte[]? ResultBytes { get; private set; }
    public bool ResultInsertAtStart { get; private set; }
    public string? ResultTargetProcedure { get; private set; }
    public uint ResultModelHash { get; private set; }

    public InsertCommandDialog()
    {
        InitializeComponent();
        DataContext = new InsertCommandDialogViewModel();
    }

    public InsertCommandDialog(IWorkspaceCatalog? workspace) : this()
    {
        _workspace = workspace;
    }

    public void ConfigureNewPutObject(
        uint modelHash,
        OpenTK.Mathematics.Vector3 position,
        IEnumerable<string> targetProcedures,
        string? defaultTargetProcedure)
    {
        if (modelHash == 0 && _workspace?.Snapshot is { } snapshot)
        {
            modelHash = ProjectModelLoader.BuildPathLookup(snapshot)
                .OrderBy(item => Path.GetFileNameWithoutExtension(item.Value.FileName), System.StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Key)
                .FirstOrDefault();
        }
        if (DataContext is InsertCommandDialogViewModel viewModel)
        {
            viewModel.ConfigureNewPutObject(modelHash, position, targetProcedures, defaultTargetProcedure);
        }
        Title = "Add Object";
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        ResultBytes = null;
        ResultInsertAtStart = false;
        ResultTargetProcedure = null;
        ResultModelHash = 0;
        Close();
    }

    private void OnInsert(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InsertCommandDialogViewModel vm)
        {
            ResultBytes = vm.BuildCommand();
            ResultInsertAtStart = vm.InsertAtStart;
            ResultTargetProcedure = vm.SelectedTargetProcedure;
            ResultModelHash = vm.SelectedModelHash;
        }
        Close();
    }

    private async void OnBrowseModel(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ParameterViewModel param })
        {
            return;
        }

        if (_workspace?.Snapshot is null)
        {
            return;
        }

        // Lazy load MDN files
        if (_mdnModels == null)
        {
            _mdnModels = ProjectModelLoader.BuildPathLookup(_workspace.Snapshot)
                .Select(item => new ModelPickerItem(
                    item.Key,
                    Path.GetFileNameWithoutExtension(item.Value.FileName)))
                .OrderBy(item => item.DisplayName, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (_mdnModels.Count == 0)
        {
            return;
        }

        // Show file picker dialog
        var pickerWindow = new Window
        {
            Title = "Select Model",
            Width = 500,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Avalonia.Media.Brushes.Black
        };

        var listBox = new ListBox
        {
            ItemsSource = _mdnModels,
            Margin = new Avalonia.Thickness(8)
        };

        ModelPickerItem? selectedModel = null;

        listBox.DoubleTapped += (_, _) =>
        {
            if (listBox.SelectedItem is ModelPickerItem model)
            {
                selectedModel = model;
                pickerWindow.Close();
            }
        };

        var okButton = new Button { Content = "OK", Width = 80, Margin = new Avalonia.Thickness(4) };
        var cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Avalonia.Thickness(4) };

        okButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is ModelPickerItem model)
            {
                selectedModel = model;
            }
            pickerWindow.Close();
        };

        cancelButton.Click += (_, _) => pickerWindow.Close();

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(8)
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(okButton);

        var mainPanel = new DockPanel();
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        mainPanel.Children.Add(buttonPanel);
        mainPanel.Children.Add(listBox);

        pickerWindow.Content = mainPanel;

        await pickerWindow.ShowDialog(this);

        if (selectedModel != null)
        {
            param.TextValue = $"0x{selectedModel.Hash:X}";
        }
    }

    private sealed record ModelPickerItem(uint Hash, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
