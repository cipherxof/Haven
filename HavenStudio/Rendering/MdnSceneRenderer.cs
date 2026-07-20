using System;
using System.Collections.Generic;
using Avalonia3DControl;
using Avalonia3DControl.Core.Models;
using Avalonia3DControl.Materials;
using HavenStudio.Editors.GcxEditing;
using HavenStudio.Formats.Mdn;
using HavenStudio.Services.Workspace;
using OpenTK.Mathematics;

namespace HavenStudio.Rendering;

public sealed record MdnSceneBatch(
    Mdn Document,
    IReadOnlyList<Model3D> Models,
    IReadOnlyDictionary<uint, ResolvedTexture> Textures,
    PlacedModelReference? Placement = null);

public static class MdnSceneRenderer
{
    public static List<Model3D> BuildModels(
        Mdn document,
        string? nameSuffix = null,
        Vector3? position = null,
        Vector3? rotation = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var models = MdnSceneBuilder.BuildModels(document);
        foreach (var model in models)
        {
            if (!string.IsNullOrEmpty(nameSuffix))
            {
                model.Name += nameSuffix;
            }

            if (position.HasValue)
            {
                model.Position = position.Value;
            }

            if (rotation.HasValue)
            {
                model.Rotation = rotation.Value;
            }

            model.Scale = Vector3.One;
            model.Visible = true;
        }

        return models;
    }

    public static IReadOnlyDictionary<uint, ResolvedTexture> ResolveTextures(
        Mdn document,
        IWorkspaceCatalog workspace)
    {
        var resolver = new MdnTextureResolver();
        return resolver.TryResolveAll(document, workspace, out var textures)
            ? textures
            : new Dictionary<uint, ResolvedTexture>();
    }

    public static MdnSceneBatch PrepareBatch(
        Mdn document,
        IWorkspaceCatalog workspace,
        string? nameSuffix = null,
        Vector3? position = null,
        Vector3? rotation = null)
    {
        return new MdnSceneBatch(
            document,
            BuildModels(document, nameSuffix, position, rotation),
            ResolveTextures(document, workspace));
    }

    public static void ApplyTextures(
        OpenGL3DControl viewport,
        Mdn document,
        IEnumerable<Model3D> models,
        IReadOnlyDictionary<uint, ResolvedTexture> textures)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        if (textures.Count == 0 || !TrySelectPrimaryTexture(document, textures, out var primaryTexture))
        {
            return;
        }

        viewport.SetShadingMode(ShadingMode.Texture);
        foreach (var model in models)
        {
            var texture = ResolveTextureForModel(document, model, textures) ?? primaryTexture;
            viewport.ApplyTextureFromDds(model, texture.Width, texture.Height, texture.FourCC, texture.Data);
        }
    }

    private static ResolvedTexture? ResolveTextureForModel(
        Mdn document,
        Model3D model,
        IReadOnlyDictionary<uint, ResolvedTexture> textures)
    {
        if (model.MaterialIndex < 0 || model.MaterialIndex >= document.Materials.Count)
        {
            return null;
        }

        var diffuseIndex = document.Materials[model.MaterialIndex].DiffuseIndex;
        if (diffuseIndex < 0 || diffuseIndex >= document.Textures.Count)
        {
            return null;
        }

        var textureHash = (uint)document.Textures[diffuseIndex].NameHash;
        return textures.TryGetValue(textureHash, out var texture) ? texture : null;
    }

    private static bool TrySelectPrimaryTexture(
        Mdn document,
        IReadOnlyDictionary<uint, ResolvedTexture> textures,
        out ResolvedTexture texture)
    {
        texture = default;
        if (document.Materials.Count > 0)
        {
            var diffuseIndex = document.Materials[0].DiffuseIndex;
            if (diffuseIndex >= 0 && diffuseIndex < document.Textures.Count)
            {
                var textureHash = (uint)document.Textures[diffuseIndex].NameHash;
                if (textures.TryGetValue(textureHash, out texture))
                {
                    return true;
                }
            }
        }

        foreach (var entry in textures)
        {
            texture = entry.Value;
            return true;
        }

        return false;
    }
}
