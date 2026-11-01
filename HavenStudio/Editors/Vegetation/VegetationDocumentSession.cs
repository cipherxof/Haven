using System;
using System.IO;
using System.Linq;
using HavenStudio.Formats.Pdl;
using HavenStudio.Services.Workspace;
using OpenTK.Mathematics;

namespace HavenStudio.Editors.Vegetation;

public sealed class VegetationDocumentSession
{
    private readonly IWorkspaceCatalog _workspace;

    private VegetationDocumentSession(
        IWorkspaceCatalog workspace,
        WorkspacePath path,
        uint modelHash,
        PdlDocument document)
    {
        _workspace = workspace;
        Path = path;
        ModelHash = modelHash;
        Document = document;
    }

    public WorkspacePath Path { get; }
    public uint ModelHash { get; }
    public PdlDocument Document { get; }
    public bool IsDirty { get; private set; }

    public static VegetationDocumentSession Load(
        IWorkspaceCatalog workspace,
        WorkspacePath path,
        uint modelHash)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(path);
        return new VegetationDocumentSession(
            workspace,
            path,
            modelHash,
            PdlDocument.Read(workspace.ReadAllBytes(path)));
    }

    public void MoveGroupTo(PdlGroup group, Vector3 centroid)
    {
        if (!Document.Groups.Contains(group))
            throw new ArgumentException("The PDL group does not belong to this document.", nameof(group));
        var delta = centroid - group.Centroid;
        if (delta.LengthSquared < 0.000001f)
            return;
        foreach (var instance in group.Instances)
            instance.Position += delta;
        IsDirty = true;
    }

    public void UpdateInstancePosition(PdlGroup group, PdlInstance instance, Vector3 position)
    {
        if (!Document.Groups.Contains(group) || !group.Instances.Contains(instance))
            throw new ArgumentException("The PDL instance does not belong to this document.", nameof(instance));
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            throw new InvalidDataException("Vegetation position must contain finite values.");
        instance.Position = position;
        IsDirty = true;
    }

    public void Save()
    {
        if (!IsDirty)
            return;
        _workspace.Replace(Path, Document.WritePositions());
        IsDirty = false;
    }
}
