using System;
using System.Collections.Generic;
using HavenStudio.Formats.Abc;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Rendering;

/// <summary>
/// Discovers ".abc" ambient-cube files in the current workspace and registers
/// them with <see cref="Mgs4AmbientCubeEvaluator"/>. Called once per map load,
/// right after light discovery. A stage with a single region-covering .abc is
/// registered as one slot; stages with several .abc files are registered as one
/// slot each (flagged in the returned status) until slot/global routing is added.
/// </summary>
public static class Mgs4AmbientCubeLoader
{
    public static string RegisterFromWorkspace(
        IWorkspaceCatalog workspace, WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(snapshot);

        var slots = new List<Mgs4AmbientCubeEvaluator.Slot?>();
        var loaded = new List<string>();
        var errors = new List<string>();

        var candidates = System.Linq.Enumerable.ToList(snapshot.WithExtension(".abc"));
        foreach (var file in candidates)
        {
            try
            {
                var abc = AbcFile.Read(workspace.ReadAllBytes(file.Path));
                slots.Add(abc.ToSlot());
                loaded.Add($"{file.Name} ({abc.Nodes.Length} node(s))");
            }
            catch (Exception exception) when (
                exception is System.IO.InvalidDataException or System.IO.IOException)
            {
                errors.Add($"{file.Name}: {exception.Message}");
            }
        }

        if (slots.Count == 0)
        {
            Mgs4AmbientCubeEvaluator.Clear();
            return errors.Count == 0
                ? string.Empty
                : $"Ambient cube (.abc) errors: {string.Join("; ", errors)}";
        }

        Mgs4AmbientCubeEvaluator.SetSlots(slots, global: null);
        var status = $"MGS4 ambient cube active: {string.Join(", ", loaded)}";
        if (slots.Count > 1)
        {
            status += " [multi-.abc stage: slot/global routing pending]";
        }
        if (errors.Count > 0)
        {
            status += $" — errors: {string.Join("; ", errors)}";
        }
        return status;
    }
}
