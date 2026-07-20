using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaHex;
using HavenStudio.Windows;

namespace HavenStudio.Views;

public partial class GcxEditorView : UserControl
{
    public GcxEditorView()
    {
        InitializeComponent();
    }

    public HexEditor? HexEditor => this.FindControl<HexEditor>("ScriptHexEditor");
    public TextEditor? DecompilationEditor => this.FindControl<TextEditor>("GcxDecompilationEditor");
    public TreeView? ScriptTreeView => this.FindControl<TreeView>("GcxScriptTreeView");

    public void CloseContextMenu()
    {
        if (this.FindControl<ContextMenu>("HexEditorContextMenu") is { } menu)
        {
            menu.Close();
        }
    }

    private MainWindow? OwnerWindow => TopLevel.GetTopLevel(this) as MainWindow;

    private async void OnAddProc(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } owner)
        {
            await owner.AddGcxProcedureAsync();
        }
    }

    private async void OnInsertCommand(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } owner)
        {
            await owner.InsertGcxCommandAsync();
        }
    }

    private async void OnSaveScript(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } owner)
        {
            await owner.SaveGcxScriptAsync();
        }
    }

    private async void OnCheckGcxErrors(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } owner)
        {
            await owner.CheckGcxErrorsAsync();
        }
    }

    private void OnScriptTreeDoubleTapped(object? sender, TappedEventArgs e) => OwnerWindow?.OpenSelectedGcxScript(sender);
    private void OnHexEditorKeyDown(object? sender, KeyEventArgs e) => OwnerWindow?.HandleHexEditorKeyDown(sender, e);
    private void OnHexCopy(object? sender, RoutedEventArgs e) => OwnerWindow?.CopyHexSelection();
    private void OnHexPaste(object? sender, RoutedEventArgs e) => OwnerWindow?.PasteHexSelection(insert: false);
    private void OnHexPasteInsert(object? sender, RoutedEventArgs e) => OwnerWindow?.PasteHexSelection(insert: true);
    private void OnHexDelete(object? sender, RoutedEventArgs e) => OwnerWindow?.DeleteHexSelection();
    private async void OnUpdateProcSize(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } owner)
        {
            await owner.UpdateGcxProcedureSizeAsync();
        }
    }
    private void OnDecompilationEditorKeyDown(object? sender, KeyEventArgs e) => OwnerWindow?.HandleDecompilationEditorKeyDown(e);
}
