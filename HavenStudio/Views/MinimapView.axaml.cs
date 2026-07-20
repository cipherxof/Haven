using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HavenStudio.Views;

public partial class MinimapView : UserControl
{
    public MinimapView()
    {
        InitializeComponent();
    }

    private void OnPrevious(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MinimapViewModel viewModel)
        {
            viewModel.Previous();
        }
    }

    private void OnNext(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MinimapViewModel viewModel)
        {
            viewModel.Next();
        }
    }
}
