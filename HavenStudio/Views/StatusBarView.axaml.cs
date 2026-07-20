using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Serilog;

namespace HavenStudio.Views;

public partial class StatusBarView : UserControl
{
    private static readonly ILogger Log = Serilog.Log.ForContext<StatusBarView>();

    public StatusBarView()
    {
        InitializeComponent();
    }

    private void OnOpenMgoSite(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://mgo2pc.com") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to open SaveMGO website");
        }
    }
}
