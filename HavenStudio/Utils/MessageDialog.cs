using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace HavenStudio.Utils;

public enum MessageType
{
    None,
    Info,
    Warning,
    Error
}

public static class MessageDialog
{
    public static void Show(string title, string message, MessageType type = MessageType.None)
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;

        var (iconText, iconColor, borderColor, windowTitle) = type switch
        {
            MessageType.Info      => ("ℹ", Colors.Blue, new SolidColorBrush(Colors.Blue), "Info"),
            MessageType.Warning   => ("⚠", Colors.Orange, new SolidColorBrush(Colors.Orange), "Warning"),
            MessageType.Error     => ("✕", Colors.Red, new SolidColorBrush(Colors.Red), "Error"),
            _                     => (null, default(Color), null, string.Empty)
        };

        var panel = new StackPanel();

        if (type != MessageType.None && iconText != null)
        {
            // Icon + title on the same line
            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(16, 12, 16, 4),
                Spacing = 8,
            };

            headerRow.Children.Add(new TextBlock
            {
                Text = iconText,
                FontSize = 28,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(iconColor),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });

            if (!string.IsNullOrEmpty(title))
            {
                headerRow.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 16,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.White,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                });
            }

            panel.Children.Add(headerRow);
        }
        else if (!string.IsNullOrEmpty(title))
        {
            // No icon — show title as a standalone header
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(16, 12, 16, 4),
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16, 0, 16, 8),
            Foreground = Brushes.White,
        });

        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 12),
            Width = 90,
        };

        panel.Children.Add(okButton);

        var dialog = new Window
        {
            Title = windowTitle,
            Width = 450,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Brushes.Black,
            Content = panel
        };

        if (borderColor != null)
        {
            dialog.BorderThickness = new Thickness(2);
            dialog.BorderBrush = borderColor;
        }

        okButton.Click += (_, _) => dialog.Close();
        if (owner != null)
        {
            _ = dialog.ShowDialog(owner);
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Show();
        }
    }

    public static void Info(string title, string message)  => Show(title, message, MessageType.Info);
    public static void Warning(string title, string message) => Show(title, message, MessageType.Warning);
    public static void Error(string title, string message)   => Show(title, message, MessageType.Error);
}
