using Avalonia.Controls;
using Avalonia.Interactivity;
using HavenStudio.Editors;

namespace HavenStudio.Windows;

public partial class OctocamoRemapWindow : Window
{
    public OctocamoRemapWindow() { InitializeComponent(); }
    public OctocamoRemapWindow(OctocamoRemapViewModel model) : this() { DataContext = model; }
    private void OnApply(object? sender, RoutedEventArgs args) => (DataContext as OctocamoRemapViewModel)?.Apply();
    private void OnReset(object? sender, RoutedEventArgs args) => (DataContext as OctocamoRemapViewModel)?.Reset();
    private void OnDone(object? sender, RoutedEventArgs args)
    {
        if (DataContext is OctocamoRemapViewModel model && model.TryFinish()) Close();
    }
}
