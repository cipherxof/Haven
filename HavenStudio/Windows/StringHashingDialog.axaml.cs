using Avalonia.Controls;
using Avalonia.Interactivity;
using HavenStudio.Utils;

namespace HavenStudio.Windows;

public partial class StringHashingDialog : Window
{
    public StringHashingDialog()
    {
        InitializeComponent();
    }

    private void OnHashString(object? sender, RoutedEventArgs e)
    {
        var input = InputStringBox.Text ?? string.Empty;
        var hash = Utils.String.HashString(input);
        HashResultBox.Text = $"0x{hash:X6}";
    }

    private void OnLookupHash(object? sender, RoutedEventArgs e)
    {
        var input = (LookupHashBox.Text ?? string.Empty).Trim();
        if (input.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
        {
            input = input.Substring(2);
        }

        if (!uint.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out uint hash))
        {
            LookupResultBox.Text = "(invalid hash)";
            return;
        }

        // Mask to 24 bits to match the hash function
        hash &= 0x00FFFFFF;

        LookupResultBox.Text = DictionaryFile.GetHashString(hash);
    }
}
