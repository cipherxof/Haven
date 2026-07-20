using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Avalonia.Input;
using AvaloniaHex;
using AvaloniaHex.Rendering;
using Avalonia.Media;
using System.ComponentModel;
using Avalonia;
using Avalonia.Threading;
using AvaloniaHex.Editing;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using HavenStudio.Utils;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using HavenStudio.Editors;
using HavenStudio.Rendering;
using HavenStudio;
using HavenStudio.Services;
using HavenStudio.Services.FileExplorer;
using HavenStudio.Services.FileOpening;
using HavenStudio.Services.Workspace;
using HavenStudio.Views;
using Serilog;
using System.Xml;
using Avalonia.Layout;
using System.Threading;

namespace HavenStudio.Windows;

public partial class MainWindow : Window
{
    private static readonly ILogger _log = Log.ForContext<MainWindow>();

    private readonly MainWindowViewModel _viewModel;
    private readonly FileOpenCoordinator _fileOpenCoordinator;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly FileExplorerView? _fileExplorerView;
    private readonly GcxEditorView? _gcxEditorView;
    private Point? _lastViewportPointerPosition;
    private TextEditor? _decompilationEditor;
    private TreeView? _gcxScriptTreeView;
    private Window? _findWindow;
    private TextBox? _findTextBox;
    private (int index, int length)? _pendingFindSelection;
    private readonly HashSet<DispatcherTimer> _transientTimers = [];
    private bool _isClosed;

    public MainWindow()
    {
        InitializeComponent();
        using var iconStream = AssetLoader.Open(new Uri("avares://HavenStudio/Assets/icon.png"));
        Icon = new WindowIcon(iconStream);
        _viewModel = new MainWindowViewModel();
        _fileOpenCoordinator = new FileOpenCoordinator(new MainWindowFileOpenActions(this, _viewModel));
        DataContext = _viewModel;
        _fileExplorerView = this.FindControl<FileExplorerView>("FileExplorerView");
        _gcxEditorView = this.FindControl<GcxEditorView>("GcxEditorView");
        _fileExplorerView?.InitializeOperations(new FileExplorerOperations(
            this,
            _viewModel,
            _fileOpenCoordinator,
            _lifetimeCancellation.Token));
        _viewModel.SceneHost.ViewportControl.IsHitTestVisible = true;
        _viewModel.SceneHost.ViewportControl.Focusable = true;
        ConfigureHexEditor();
        ConfigureDecompilationEditor();
        UpdateViewportHotkeys();
        DictionaryFile.Load("./dictionary.txt", "./dictionary-aliases.txt");
        CommandFile.Load("./commands.txt");
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.GcxEditor.PropertyChanged += OnGcxEditorPropertyChanged;
        HookViewportEvents();
        _gcxScriptTreeView = _gcxEditorView?.ScriptTreeView;
        Closed += OnWindowClosed;
    }

    private void ConfigureHexEditor()
    {
        var hexEditor = _gcxEditorView?.HexEditor;
        if (hexEditor?.HexView != null)
        {
            hexEditor.HexView.BytesPerLine = 16;
            hexEditor.IsHeaderVisible = true;
            hexEditor.HexView.IsHeaderVisible = true;
            hexEditor.FontFamily = new FontFamily("DejaVu Sans Mono, Consolas, monospace");
            hexEditor.FontSize = 16;
            hexEditor.HexView.FontFamily = hexEditor.FontFamily;
            hexEditor.HexView.FontSize = hexEditor.FontSize;
            hexEditor.ColumnPadding = 12;
            hexEditor.Caret.Mode = EditingMode.Overwrite;
        }
    }

    private void ConfigureDecompilationEditor()
    {
        _decompilationEditor = _gcxEditorView?.DecompilationEditor;
        if (_decompilationEditor == null)
        {
            return;
        }

        var highlighting = LoadTclHighlighting();
        if (highlighting != null)
        {
            HighlightingManager.Instance.RegisterHighlighting(
                "TCL",
                new[] { ".gcl", ".tcl" },
                highlighting);
            _decompilationEditor.SyntaxHighlighting = highlighting;
        }
#if DEBUG
        else
        {
            _log.Warning("TCL highlighting failed to load from Assets/Syntax/Tcl.xshd.");
        }
#endif

        UpdateDecompilationText();

        _decompilationEditor.TextArea.TextView.PointerPressed += OnDecompilationTextViewPointerPressed;
    }

    private void OnGcxEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GcxEditorViewModel.DecompilationText))
        {
            UpdateDecompilationText();
        }
    }

    private void UpdateDecompilationText()
    {
        if (_decompilationEditor == null)
        {
            return;
        }

        var text = _viewModel.GcxEditor.DecompilationText ?? string.Empty;
        Dispatcher.UIThread.Post(() =>
        {
            if (_decompilationEditor != null)
            {
                _decompilationEditor.Text = text;
                if (_pendingFindSelection is { } match)
                {
                    _pendingFindSelection = null;
                    HighlightDecompilationMatch(match.index, match.length);
                }
            }
        });
    }

    private static IHighlightingDefinition? LoadTclHighlighting()
    {
        try
        {
            var uri = new Uri("avares://HavenStudio/Assets/Syntax/Tcl.xshd");
            if (!AssetLoader.Exists(uri))
            {
                return null;
            }

            using var stream = AssetLoader.Open(uri);
            using var reader = new XmlTextReader(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch (Exception e)
        {
            _log.Error(e, "Failed to load TCL highlighting");
            return null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTabIndex))
        {
            UpdateViewportHotkeys();
        }
    }

    private void UpdateViewportHotkeys()
    {
        _viewModel.SceneHost.ViewportControl.AllowHotkeys = _viewModel.IsMapSelected;

        if (_viewModel.IsMapSelected)
        {
            _viewModel.SceneHost.ViewportControl.Focus();
        }
    }

    private async void OnOpenFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Level Folder",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        var path = folder.Path.LocalPath;
        if (!Directory.Exists(path))
        {
            return;
        }

        await _viewModel.LoadFromFolderAsync(path);
    }

    private async void OnOpenFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file == null)
        {
            return;
        }

        var path = file.Path.LocalPath;
        if (!File.Exists(path))
        {
            return;
        }

        await OpenPathAsync(WorkspacePath.Physical(path));
    }

    private void OnOpenPreferences(object? sender, RoutedEventArgs e)
    {
        PreferencesWindow.ShowSingleton(this);
    }

    private void OnMinimapDoubleTapped(object? sender, TappedEventArgs e)
    {
        var level = _viewModel.Minimap.CurrentLevel;
        var geom = _viewModel.CollisionEditor.GeomFile;
        if (level is null || geom is null)
        {
            return;
        }

        var image = level.Image;
        var title = string.IsNullOrWhiteSpace(_viewModel.RootFolderName)
            ? "Minimap"
            : $"Minimap — {_viewModel.RootFolderName}";
        var root = MinimapSpawns.Build(
            geom.GeoEffects,
            image.PixelSize.Width,
            image.PixelSize.Height,
            level.Projection);
        var window = new MinimapWindow
        {
            DataContext = new MinimapWindowViewModel(image, title, root)
        };
        window.Show(this);
    }

    private async void OnEncryptDecryptFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select File",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file == null)
        {
            return;
        }

        var path = file.Path.LocalPath;
        if (!File.Exists(path))
        {
            return;
        }

        var defaultKey = CryptoWindow.BuildDefaultKey(path);
        CryptoWindow.ShowWindow(this, path, defaultKey);
    }

    private async void OnEncryptDecryptFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder == null)
        {
            return;
        }

        var path = folder.Path.LocalPath;
        if (!Directory.Exists(path))
        {
            return;
        }

        var defaultKey = CryptoWindow.BuildDefaultKey(path);
        CryptoWindow.ShowWindow(this, path, defaultKey);
    }

    private async void OnStringHashing(object? sender, RoutedEventArgs e)
    {
        var dialog = new StringHashingDialog();
        await dialog.ShowDialog(this);
    }

    internal Task AddGcxProcedureAsync()
    {
        return RunGcxOperationAsync(token => _viewModel.GcxEditor.AddProcAsync(token));
    }

    internal Task InsertGcxCommandAsync()
    {
        return RunGcxOperationAsync(async token =>
        {
            var dialog = new InsertCommandDialog(_viewModel.Workspace);
            await dialog.ShowDialog(this);
            token.ThrowIfCancellationRequested();

            if (dialog.ResultBytes != null && dialog.ResultBytes.Length > 0)
            {
                await _viewModel.GcxEditor.InsertCommandBytesAsync(
                    dialog.ResultBytes,
                    dialog.ResultInsertAtStart,
                    token);
            }
        });
    }

    internal Task UpdateGcxProcedureSizeAsync()
    {
        return RunGcxOperationAsync(token => _viewModel.GcxEditor.UpdateSelectedProcSizeAsync(token));
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_viewModel.IsMapSelected)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                _viewModel.MapEditor.Redo();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Z)
            {
                _viewModel.MapEditor.Undo();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Y)
            {
                _viewModel.MapEditor.Redo();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.V)
        {
            _viewModel.SceneHost.ViewportControl.ShowOverheadView();
            e.Handled = true;
            return;
        }

        var axis = ToDragAxis(e.Key);
        if (axis != MapDragAxis.None)
        {
            _viewModel.MapEditor.SetAxisConstraint(axis, true);
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.MapEditor.CancelManipulation();
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_viewModel.IsMapSelected)
        {
            return;
        }

        var axis = ToDragAxis(e.Key);
        if (axis != MapDragAxis.None)
        {
            _viewModel.MapEditor.SetAxisConstraint(axis, false);
        }
    }

    private static MapDragAxis ToDragAxis(Key key) => key switch
    {
        Key.X => MapDragAxis.X,
        Key.Y => MapDragAxis.Y,
        Key.Z => MapDragAxis.Z,
        _ => MapDragAxis.None
    };

    private void HookViewportEvents()
    {
        var viewport = _viewModel.SceneHost.ViewportControl;
        viewport.AddHandler(InputElement.PointerMovedEvent, OnViewportPointerMoved, RoutingStrategies.Tunnel, true);
        viewport.AddHandler(InputElement.PointerPressedEvent, OnViewportPointerPressed, RoutingStrategies.Tunnel, true);
        viewport.AddHandler(InputElement.PointerReleasedEvent, OnViewportPointerReleased, RoutingStrategies.Tunnel, true);
        viewport.AddHandler(InputElement.PointerCaptureLostEvent, OnViewportPointerCaptureLost, RoutingStrategies.Tunnel, true);
        viewport.AddHandler(InputElement.PointerExitedEvent, OnViewportPointerExited, RoutingStrategies.Tunnel, true);
        viewport.AddHandler(InputElement.DoubleTappedEvent, OnViewportDoubleTapped, RoutingStrategies.Tunnel, true);
    }

    private void UnhookViewportEvents()
    {
        var viewport = _viewModel.SceneHost.ViewportControl;
        viewport.RemoveHandler(InputElement.PointerMovedEvent, OnViewportPointerMoved);
        viewport.RemoveHandler(InputElement.PointerPressedEvent, OnViewportPointerPressed);
        viewport.RemoveHandler(InputElement.PointerReleasedEvent, OnViewportPointerReleased);
        viewport.RemoveHandler(InputElement.PointerCaptureLostEvent, OnViewportPointerCaptureLost);
        viewport.RemoveHandler(InputElement.PointerExitedEvent, OnViewportPointerExited);
        viewport.RemoveHandler(InputElement.DoubleTappedEvent, OnViewportDoubleTapped);
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_viewModel.IsMapSelected)
        {
            return;
        }

        var viewport = _viewModel.SceneHost.ViewportControl;
        var point = e.GetPosition(viewport);
        _lastViewportPointerPosition = point;
        _viewModel.MapEditor.PointerMoved(
            point,
            viewport,
            e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        _viewModel.CollisionEditor.UpdateHover(point, viewport);
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_viewModel.IsMapSelected)
        {
            return;
        }

        var viewport = _viewModel.SceneHost.ViewportControl;
        if (!e.GetCurrentPoint(viewport).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _lastViewportPointerPosition = e.GetPosition(viewport);
        viewport.Focus();
        e.Pointer.Capture(viewport);
        _viewModel.MapEditor.PointerPressed(_lastViewportPointerPosition.Value, viewport);
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_viewModel.IsMapSelected || e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        var viewport = _viewModel.SceneHost.ViewportControl;
        var point = e.GetPosition(viewport);
        _lastViewportPointerPosition = point;
        _viewModel.MapEditor.PointerReleased(
            point,
            viewport,
            e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        e.Pointer.Capture(null);
    }

    private void OnViewportDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!_viewModel.IsMapSelected)
        {
            return;
        }

        _viewModel.MapEditor.FocusSelected();
    }

    private void OnViewportPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _lastViewportPointerPosition = null;
        _viewModel.MapEditor.CancelManipulation();
    }

    private void OnViewportPointerExited(object? sender, PointerEventArgs e)
    {
        _lastViewportPointerPosition = null;
        _viewModel.CollisionEditor.ClearHover();
    }

    internal void OpenSelectedGcxScript(object? sender)
    {
        if (sender is not TreeView treeView)
        {
            return;
        }

        if (treeView.SelectedItem is GcxScriptNode scriptNode)
        {
            _viewModel.GcxEditor.SelectScript(scriptNode);
        }
    }

    internal Task SaveGcxScriptAsync()
    {
        return RunGcxOperationAsync(token => _viewModel.GcxEditor.SaveSelectedScriptAsync(token));
    }

    private async Task RunGcxOperationAsync(Func<CancellationToken, Task> operation)
    {
        try
        {
            await operation(_lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _log.Error(exception, "GCX editor operation failed");
            MessageDialog.Error("GCX Editor", exception.Message);
        }
    }

    internal Task CheckGcxErrorsAsync()
    {
        var errors = _viewModel.GcxEditor.GetProcSizeErrors();
        if (errors.Count == 0)
        {
            MessageDialog.Info("Script Check", "No errors found.");
            return Task.CompletedTask;
        }

        var message = string.Join(Environment.NewLine, errors);
        MessageDialog.Warning("Script Check", message);
        return Task.CompletedTask;
    }

    internal void HandleHexEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not HexEditor hexEditor)
        {
            return;
        }

        var modifiers = e.KeyModifiers;
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.C:
                    hexEditor.Copy();
                    e.Handled = true;
                    return;
                case Key.X:
                    hexEditor.Copy();
                    hexEditor.Delete();
                    e.Handled = true;
                    return;
                case Key.V:
                    if (modifiers.HasFlag(KeyModifiers.Shift))
                    {
                        PasteHexInsert();
                    }
                    else
                    {
                        hexEditor.Paste();
                    }
                    e.Handled = true;
                    return;
                case Key.A:
                    hexEditor.Selection?.SelectAll();
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Delete)
        {
            hexEditor.Delete();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back)
        {
            hexEditor.Backspace();
            e.Handled = true;
        }
    }

    internal void HandleDecompilationEditorKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowFindWindow();
            e.Handled = true;
        }
    }

    private void OnDecompilationTextViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not TextView textView || _decompilationEditor == null)
        {
            return;
        }

        if (!e.GetCurrentPoint(textView).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        var point = e.GetPosition(textView);
        if (TrySetCaretFromPoint(textView, point))
        {
            e.Handled = true;
            Dispatcher.UIThread.Post(NavigateToProcFromCaret, DispatcherPriority.Background);
            return;
        }

        Dispatcher.UIThread.Post(NavigateToProcFromCaret, DispatcherPriority.Background);
    }

    private void ShowFindWindow()
    {
        if (_findWindow == null)
        {
            CreateFindWindow();
        }

        if (_findWindow == null)
        {
            return;
        }

        _findWindow.Show(this);
        _findWindow.Activate();
        if (_findTextBox != null)
        {
            _findTextBox.Focus();
            _findTextBox.SelectAll();
        }
    }

    private void CreateFindWindow()
    {
        var input = new TextBox
        {
            Width = 260,
            Watermark = "Find text..."
        };

        var findNextButton = new Button
        {
            Content = "Find Next",
            MinWidth = 90
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12)
        };
        panel.Children.Add(input);
        panel.Children.Add(findNextButton);

        var window = new Window
        {
            Title = "Find",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Brushes.Black,
            Content = panel
        };

        findNextButton.Click += (_, _) => ExecuteFindNext();
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ExecuteFindNext();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                window.Close();
                e.Handled = true;
            }
        };
        window.Closed += (_, _) =>
        {
            _findWindow = null;
            _findTextBox = null;
        };

        _findWindow = window;
        _findTextBox = input;
    }

    private void ExecuteFindNext()
    {
        if (_findTextBox == null)
        {
            return;
        }

        var query = _findTextBox.Text ?? string.Empty;
        if (_viewModel.GcxEditor.TryFindNextInDecompilation(query, out int index, out int length, out var matchedNode))
        {
            _pendingFindSelection = (index, length);
            var currentText = _viewModel.GcxEditor.DecompilationText ?? string.Empty;
            if (_decompilationEditor != null && _decompilationEditor.Text == currentText)
            {
                _pendingFindSelection = null;
                HighlightDecompilationMatch(index, length);
            }

            if (matchedNode != null && _gcxScriptTreeView != null)
            {
                _gcxScriptTreeView.SelectedItem = matchedNode;
                _gcxScriptTreeView.ScrollIntoView(matchedNode);
            }
        }
        else
        {
            MessageDialog.Info("Find", "No matches found.");
        }
    }

    private void HighlightDecompilationMatch(int index, int length)
    {
        if (_decompilationEditor?.Document == null)
        {
            return;
        }

        if (index < 0 || length <= 0 || index + length > _decompilationEditor.Document.TextLength)
        {
            return;
        }

        _decompilationEditor.Select(index, length);
        var line = _decompilationEditor.Document.GetLineByOffset(index);
        _decompilationEditor.ScrollTo(line.LineNumber, 0);
        _decompilationEditor.TextArea.Caret.Offset = index;
    }

    private void NavigateToProcFromCaret()
    {
        if (_decompilationEditor?.Document == null)
        {
            return;
        }

        int length = _decompilationEditor.Document.TextLength;
        if (length == 0)
        {
            return;
        }

        int offset = Math.Clamp(_decompilationEditor.CaretOffset, 0, length);
        if (offset == length && length > 0)
        {
            offset = length - 1;
        }

        var text = _decompilationEditor.Document.Text;
        if (offset < 0 || offset >= text.Length)
        {
            return;
        }

        int start = offset;
        while (start > 0 && IsProcTokenChar(text[start - 1]))
        {
            start--;
        }

        int end = offset;
        while (end < text.Length && IsProcTokenChar(text[end]))
        {
            end++;
        }

        if (end <= start)
        {
            return;
        }

        var token = text.Substring(start, end - start);
        if (!TryParseProcToken(token, out string? targetName) || targetName == null)
        {
            return;
        }

        var node = _viewModel.GcxEditor.ScriptItems.FirstOrDefault(item =>
            item.Script != null && !item.IsAggregate &&
            string.Equals(item.Name, targetName, StringComparison.OrdinalIgnoreCase));
        if (node == null)
        {
            return;
        }

        _viewModel.GcxEditor.SelectScript(node);
        if (_gcxScriptTreeView != null)
        {
            _gcxScriptTreeView.SelectedItem = node;
            _gcxScriptTreeView.ScrollIntoView(node);
        }
    }

    private bool TrySetCaretFromPoint(TextView textView, Point point)
    {
        if (_decompilationEditor?.Document == null)
        {
            return false;
        }

        var position = GetTextViewPositionFromPoint(textView, point);
        if (position == null)
        {
            return false;
        }

        var location = position.Value.Location;
        if (location.Line < 1 || location.Column < 1)
        {
            return false;
        }

        int offset = _decompilationEditor.Document.GetOffset(location);
        _decompilationEditor.TextArea.Caret.Offset = offset;
        return true;
    }

    private static TextViewPosition? GetTextViewPositionFromPoint(TextView textView, Point point)
    {
        var type = textView.GetType();
        var withSnap = type.GetMethod("GetPositionFromPoint", new[] { typeof(Point), typeof(bool) });
        if (withSnap != null)
        {
            var result = withSnap.Invoke(textView, new object[] { point, true });
            return result as TextViewPosition?;
        }

        var withoutSnap = type.GetMethod("GetPositionFromPoint", new[] { typeof(Point) });
        if (withoutSnap != null)
        {
            var result = withoutSnap.Invoke(textView, new object[] { point });
            return result as TextViewPosition?;
        }

        return null;
    }

    private static bool IsProcTokenChar(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_' || value == '@';
    }

    private static bool TryParseProcToken(string token, out string? procName)
    {
        procName = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var trimmed = token.Trim();
        if (trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(1);
        }

        if (!trimmed.StartsWith("proc", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var numberPart = trimmed.Substring(4);
        if (!int.TryParse(numberPart, out int procId))
        {
            return false;
        }

        if (procId <= 0)
        {
            procName = "main";
        }
        else
        {
            procName = $"proc{procId}";
        }

        return true;
    }

    internal void CopyHexSelection()
    {
        _gcxEditorView?.HexEditor?.Copy();
    }

    internal void PasteHexSelection(bool insert)
    {
        if (insert)
        {
            PasteHexInsert();
        }
        else
        {
            _gcxEditorView?.HexEditor?.Paste();
        }
    }

    private void PasteHexInsert()
    {
        var hexEditor = _gcxEditorView?.HexEditor;
        if (hexEditor == null)
        {
            return;
        }

        var caret = hexEditor.Caret;
        if (caret == null)
        {
            hexEditor.Paste();
            return;
        }

        var previousMode = caret.Mode;
        try
        {
            caret.Mode = EditingMode.Insert;
            hexEditor.Paste();
        }
        finally
        {
            caret.Mode = previousMode;
        }
    }

    internal void DeleteHexSelection()
    {
        _gcxEditorView?.HexEditor?.Delete();
    }

    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
    {
        CloseOpenContextMenus();
    }

    private void CloseOpenContextMenus()
    {
        _fileExplorerView?.CloseContextMenu();
        _gcxEditorView?.CloseContextMenu();
    }

    private async Task OpenPathAsync(WorkspacePath path)
    {
        var result = await _fileOpenCoordinator.OpenAsync(
            path,
            _viewModel.Workspace,
            _lifetimeCancellation.Token);
        FileOpenResultPresenter.Present(result);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        _lifetimeCancellation.Cancel();
        Closed -= OnWindowClosed;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.GcxEditor.PropertyChanged -= OnGcxEditorPropertyChanged;
        UnhookViewportEvents();
        if (_decompilationEditor != null)
        {
            _decompilationEditor.TextArea.TextView.PointerPressed -= OnDecompilationTextViewPointerPressed;
        }

        foreach (var timer in _transientTimers)
        {
            timer.Stop();
        }
        _transientTimers.Clear();
        _findWindow?.Close();
        _findWindow = null;
        _findTextBox = null;
        _fileExplorerView?.ClearOperations();
        _lifetimeCancellation.Dispose();
        _viewModel.Dispose();
        DataContext = null;
    }

}
