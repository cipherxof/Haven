using System;
using System.Collections.Generic;
using HavenStudio.Formats.Abc;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Rendering;

/// <summary>
/// Discovers ".abc" ambient-cube files in the current workspace and registers
/// them with <see cref="Mgs4AmbientCubeEvaluator"/>. Called once per map load,
/// right after light discovery.
///
/// Placement note (evidence-based): the engine routes each .abc to a table
/// slot (CMD_SetAmbcube, id = region&lt;&lt;16|index) or to the global slot
/// (CMD_SetGlobalAmbcube). For a stage carrying a single .abc whose region
/// covers the map — the sm_dd case — slot and global evaluation are
/// mathematically identical everywhere the region test passes (the wrapper
/// renormalises by Σ), so registering as a slot is exact, not a guess.
/// Stages with several .abc files will need the GCX SetAmbcube decoding to
/// reproduce ids/global routing; until then they are registered as one slot
/// each, which is flagged in the returned status.
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
        Mgs4Diagnostics.Log("ABC", $"workspace scan: {candidates.Count} '.abc' file(s) visible");
        foreach (var file in candidates)
        {
            Mgs4Diagnostics.Log("ABC", $"  candidate: {file.Name} (path {file.Path})");
            try
            {
                var abc = AbcFile.Read(workspace.ReadAllBytes(file.Path));
                slots.Add(abc.ToSlot());
                loaded.Add($"{file.Name} ({abc.Nodes.Length} node(s))");
                Mgs4Diagnostics.Log("ABC",
                    $"  loaded {file.Name}: v{abc.Version}, {abc.Nodes.Length} node(s), " +
                    $"region ({abc.RegionMin.X:F0},{abc.RegionMin.Y:F0},{abc.RegionMin.Z:F0}) .. " +
                    $"({abc.RegionMax.X:F0},{abc.RegionMax.Y:F0},{abc.RegionMax.Z:F0})");
                if (abc.Nodes.Length > 0)
                {
                    var n0 = abc.Nodes[0];
                    Mgs4Diagnostics.Log("ABC",
                        $"  node[0] faces: L={n0.Ambient[0]} R={n0.Ambient[1]} T={n0.Ambient[2]} " +
                        $"B={n0.Ambient[3]} F={n0.Ambient[4]} Bk={n0.Ambient[5]}");
                }
            }
            catch (Exception exception) when (
                exception is System.IO.InvalidDataException or System.IO.IOException)
            {
                errors.Add($"{file.Name}: {exception.Message}");
                Mgs4Diagnostics.Log("ABC", $"  FAILED {file.Name}: {exception.Message}");
            }
        }

        if (slots.Count == 0)
        {
            Mgs4Diagnostics.Log("ABC",
                "NO ambient-cube data registered -> the exact spatial ambient is INACTIVE " +
                "and the preview falls back to the flat LT3 header ambient.");
            Mgs4AmbientCubeEvaluator.Clear();
            return errors.Count == 0
                ? string.Empty
                : $"Ambient cube (.abc) errors: {string.Join("; ", errors)}";
        }

        Mgs4AmbientCubeEvaluator.SetSlots(slots, global: null);
        Mgs4Diagnostics.Log("ABC", $"registered {slots.Count} slot(s) -> exact spatial ambient ACTIVE");
        var status = $"MGS4 ambient cube active: {string.Join(", ", loaded)}";
        if (slots.Count > 1)
        {
            status += " [multi-.abc stage: slot/global routing pending GCX SetAmbcube decoding]";
        }
        if (errors.Count > 0)
        {
            status += $" — errors: {string.Join("; ", errors)}";
        }
        return status;
    }
}
