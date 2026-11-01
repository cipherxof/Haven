using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HavenStudio.Editors;
using HavenStudio.Windows;

namespace HavenStudio.Views;

public partial class MapEditorView : UserControl
{
    private bool _committingPlacementRotation;
    private bool _committingEffectSelectionRotation;

    public MapEditorView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnClearOctocamoFaceBox(object? sender, RoutedEventArgs eventArgs) =>
        ViewModel?.MapEditor.ClearOctocamoFaceBox();

    private void OnSelectAllFilteredOctocamoFaces(object? sender, RoutedEventArgs eventArgs) =>
        ViewModel?.MapEditor.SelectAllFilteredOctocamoFaces();

    private void OnClearOctocamoBatchSelection(object? sender, RoutedEventArgs eventArgs) =>
        ViewModel?.MapEditor.ClearOctocamoBatchSelection();

    private async void OnEditOctocamoBatch(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is not { } viewModel || TopLevel.GetTopLevel(this) is not Window owner) return;
        try { await new OctocamoBatchEditWindow(viewModel.MapEditor.CreateOctocamoBatchEditor()).ShowDialog(owner); }
        catch (Exception exception) { HavenStudio.Utils.MessageDialog.Error("OctoCamo Batch Edit", exception.Message); }
    }

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

    private void OnCameraFromView(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel?.MapEditor.SetSelectedCameraFromView() is { } error)
            HavenStudio.Utils.MessageDialog.Error("Spectator Camera", error);
    }

    private void OnViewCamera(object? sender, RoutedEventArgs eventArgs) => ViewModel?.MapEditor.ViewSelectedCamera();

    private void OnSnapToFloor(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel?.MapEditor.SnapSelectedToFloor() is { } error)
        {
            HavenStudio.Utils.MessageDialog.Error("Snap To Floor", error);
        }
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

    private void OnPlacementRotationLostFocus(object? sender, RoutedEventArgs eventArgs)
    {
        CommitPlacementRotation(sender);
    }

    private void OnEffectSelectionRotationLostFocus(object? sender, RoutedEventArgs eventArgs)
    {
        CommitEffectSelectionRotation(sender);
    }

    private void OnEffectSelectionRotationKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Enter)
        {
            return;
        }
        CommitEffectSelectionRotation(sender);
        eventArgs.Handled = true;
    }

    private void CommitEffectSelectionRotation(object? sender)
    {
        if (_committingEffectSelectionRotation ||
            sender is not TextBox { DataContext: EffectSelectionEntity selection } textBox)
        {
            return;
        }

        var parsed = float.TryParse(
            textBox.Text,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out var degrees);
        if (!parsed)
        {
            parsed = float.TryParse(
                textBox.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out degrees);
        }
        if (!parsed)
        {
            textBox.Text = selection.RotationYText;
            return;
        }

        _committingEffectSelectionRotation = true;
        try
        {
            selection.TryUpdateRotationYDegrees(degrees, out _);
            textBox.Text = selection.RotationYText;
        }
        finally
        {
            _committingEffectSelectionRotation = false;
        }
    }

    private void OnPlacementRotationKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Enter)
        {
            return;
        }
        CommitPlacementRotation(sender);
        eventArgs.Handled = true;
    }

    private void CommitPlacementRotation(object? sender)
    {
        if (_committingPlacementRotation)
        {
            return;
        }
        if (sender is not TextBox { DataContext: PlacementEntity placement } textBox ||
            !float.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            return;
        }

        var degrees = new OpenTK.Mathematics.Vector3(
            placement.RotationX,
            placement.RotationY,
            placement.RotationZ);
        degrees = textBox.Tag?.ToString() switch
        {
            "X" => new OpenTK.Mathematics.Vector3(value, degrees.Y, degrees.Z),
            "Y" => new OpenTK.Mathematics.Vector3(degrees.X, value, degrees.Z),
            "Z" => new OpenTK.Mathematics.Vector3(degrees.X, degrees.Y, value),
            _ => degrees
        };

        _committingPlacementRotation = true;
        try
        {
            if (!placement.TryUpdateRotationDegrees(degrees, out var error) && !string.IsNullOrWhiteSpace(error))
            {
                textBox.Text = textBox.Tag?.ToString() switch
                {
                    "X" => placement.RotationX.ToString(CultureInfo.CurrentCulture),
                    "Y" => placement.RotationY.ToString(CultureInfo.CurrentCulture),
                    "Z" => placement.RotationZ.ToString(CultureInfo.CurrentCulture),
                    _ => textBox.Text
                };
                HavenStudio.Utils.MessageDialog.Error("Placement Rotation Error", error);
            }
        }
        finally
        {
            _committingPlacementRotation = false;
        }
    }
}
