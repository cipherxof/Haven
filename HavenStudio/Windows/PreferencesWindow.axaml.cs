using Avalonia.Controls;
using HavenStudio;

namespace HavenStudio.Windows;

public partial class PreferencesWindow : Window
{
    private static PreferencesWindow? _instance;

    public PreferencesWindow()
    {
        InitializeComponent();
        DataContext = SettingsStore.Current;
    }

    public static void ShowSingleton(Window owner)
    {
        if (_instance == null)
        {
            _instance = new PreferencesWindow();
            _instance.Closed += (_, _) => _instance = null;
        }

        _instance.Owner = owner;
        _instance.Show();
        _instance.Activate();
    }
}
