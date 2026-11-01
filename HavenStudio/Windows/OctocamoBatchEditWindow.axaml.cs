using Avalonia.Controls;
using Avalonia.Interactivity;
using HavenStudio.Editors;

namespace HavenStudio.Windows;

public partial class OctocamoBatchEditWindow : Window
{
    public OctocamoBatchEditWindow() { InitializeComponent(); }
    public OctocamoBatchEditWindow(OctocamoBatchEditViewModel model) : this() { DataContext = model; }
    private void OnKeepMaterial(object? sender, RoutedEventArgs args)
    {
        if (DataContext is OctocamoBatchEditViewModel model) model.SelectedMaterial = null;
    }
    private void OnKeepColour(object? sender, RoutedEventArgs args)
    {
        if (DataContext is OctocamoBatchEditViewModel model) model.SelectedColour = null;
    }
    private void OnApply(object? sender, RoutedEventArgs args)
    {
        if (DataContext is OctocamoBatchEditViewModel model && model.Apply()) Close();
    }
    private void OnCancel(object? sender, RoutedEventArgs args) => Close();
}
