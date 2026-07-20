using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HavenStudio.Services.Workspace;
using Serilog;

namespace HavenStudio.Windows;

public partial class TextEditorWindow : Window
{
    private static readonly ILogger _log = Log.ForContext<TextEditorWindow>();

    private string _filePath = string.Empty;
    private WorkspacePath? _workspacePath;
    private IWorkspaceCatalog? _workspace;

    public TextEditorWindow()
    {
        InitializeComponent();
    }

    public TextEditorWindow(string filePath)
    {
        _filePath = filePath;
        InitializeComponent();
        Title = $"Text Editor - {Path.GetFileName(filePath)}";
        LoadText();
    }

    public TextEditorWindow(WorkspacePath filePath, IWorkspaceCatalog workspace)
    {
        _workspacePath = filePath;
        _workspace = workspace;
        _filePath = filePath.ToLegacyString();
        InitializeComponent();
        Title = $"Text Editor - {filePath.FileName}";
        LoadText();
    }

    public static void Open(Window? owner, string filePath)
    {
        var window = new TextEditorWindow(filePath);
        if (owner != null)
        {
            window.Owner = owner;
        }

        window.Show();
        window.Activate();
    }

    public static void Open(Window? owner, WorkspacePath filePath, IWorkspaceCatalog workspace)
    {
        var window = new TextEditorWindow(filePath, workspace);
        if (owner != null)
        {
            window.Owner = owner;
        }

        window.Show();
        window.Activate();
    }

    private void LoadText()
    {
        if (_workspacePath is not null && _workspace is not null)
        {
            using var stream = _workspace.OpenRead(_workspacePath);
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            SetEditorText(reader.ReadToEnd());
            return;
        }

        if (!File.Exists(_filePath))
        {
            return;
        }

        var text = File.ReadAllText(_filePath);
        SetEditorText(File.ReadAllText(_filePath));
    }

    private void SetEditorText(string text)
    {
        var textBox = this.FindControl<TextBox>("EditorTextBox");
        if (textBox != null)
        {
            textBox.Text = text;
            Dispatcher.UIThread.Post(() => textBox.CaretIndex = 0);
        }
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("EditorTextBox");
        if (textBox == null)
        {
            return;
        }

        try
        {
            if (_workspacePath is not null && _workspace is not null)
            {
                _workspace.Replace(
                    _workspacePath,
                    System.Text.Encoding.UTF8.GetBytes(textBox.Text ?? string.Empty));
            }
            else
            {
                File.WriteAllText(_filePath, textBox.Text ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[TextEditor] Failed to save '{FilePath}'", _filePath);
        }
    }
}
