using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HavenStudio.Editors;
using HavenStudio.Windows;

namespace HavenStudio.Views;

public partial class MapEditorView : UserControl
{
    public MapEditorView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void OnSaveCollision(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        try
        {
            await viewModel.MapEditor.SaveAsync();
        }
        catch (Exception exception)
        {
            HavenStudio.Utils.MessageDialog.Error("Map Save Error", exception.Message);
        }
    }

    private void OnShowAll(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.CollisionEditor.SetAllBlocksVisible(true);
        ViewModel?.CollisionEditor.SetAllEffectsVisible(true);
    }

    private void OnHideAll(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.CollisionEditor.SetAllBlocksVisible(false);
        ViewModel?.CollisionEditor.SetAllEffectsVisible(false);
    }

    private void OnOutlineDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        ViewModel?.MapEditor.FocusSelected();
    }

    private void OnSnapEffectToCamera(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.CollisionEditor.SnapSelectedEffectToCamera();
    }

    private void OnAddEffect(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            ViewModel?.MapEditor.AddEffectAtCamera();
        }
        catch (Exception exception)
        {
            HavenStudio.Utils.MessageDialog.Error("Add Effect Error", exception.Message);
        }
    }

    private void OnDeleteEffect(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            ViewModel?.MapEditor.DeleteSelectedEffect();
        }
        catch (Exception exception)
        {
            HavenStudio.Utils.MessageDialog.Error("Delete Effect Error", exception.Message);
        }
    }

    private void OnAddLightGroup(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.MapEditor.AddLightGroup();
    }

    private void OnAddLight(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.MapEditor.AddLightToSelectedGroup();
    }

    private void OnDeleteLight(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.MapEditor.DeleteSelectedLight();
    }

    private void OnDeleteLightGroup(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.MapEditor.DeleteSelectedLightGroup();
    }

    private void OnGrowLightBounds(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.MapEditor.GrowSelectedLightBounds();
    }

    private void OnToggleInspector(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.MapEditor.ToggleInspector();
    }

    private void OnUndo(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.MapEditor.Undo();
    }

    private void OnRedo(object? sender, RoutedEventArgs eventArgs)
    {
        ViewModel?.MapEditor.Redo();
    }

    private async void OnAddObject(object? sender, RoutedEventArgs eventArgs)
    {
        var viewModel = ViewModel;
        if (viewModel == null || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new InsertCommandDialog(viewModel.Workspace);
        dialog.ConfigureNewPutObject(
            modelHash: 0,
            viewModel.MapEditor.SpawnPosition,
            viewModel.GcxEditor.ProcedureNames,
            viewModel.GcxEditor.DefaultPlacementProcedureName);
        await dialog.ShowDialog(owner);
        if (dialog.ResultBytes is not { Length: > 0 } bytes ||
            string.IsNullOrWhiteSpace(dialog.ResultTargetProcedure))
        {
            return;
        }
        if (dialog.ResultModelHash == 0)
        {
            viewModel.MapEditor.ReportAddObjectStatus("Choose a workspace MDN before adding an object.");
            return;
        }

        await viewModel.MapEditor.AddObjectAsync(bytes, dialog.ResultTargetProcedure);
    }

    private async void OnDuplicatePlacement(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is not { } viewModel ||
            sender is not Control { DataContext: PlacementEntity placement })
        {
            return;
        }

        await viewModel.MapEditor.DuplicatePlacementAsync(placement);
    }
}
