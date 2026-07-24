using System;
using Avalonia3DControl.Core.Models;

namespace HavenStudio.Rendering;

/// <summary>
/// Classifies MGS4 stage MDN assets for the projected directional shadow pass.
///
/// MGS4 does not submit every visible packet to ShadowMakeObjectList. Static
/// architectural meshes form the dominant stage casters, while terrain, distant
/// scenery, overlay/object layers and placed gameplay props are receivers only.
/// Keeping that separation improves both fidelity and shadow-pass cost.
/// </summary>
public static class StageShadowClassifier
{

    private static readonly string[] ReceiverOnlyTokens =
    [
        "preview",
        "_ground",
        "ground_",
        "_enkei",
        "_sky",
        "sky_"
    ];

    /// <summary>
    /// Applies the stage-shadow role to one render packet.
    /// </summary>
    public static void Apply(Model3D model, string? sourceAssetName, bool isPlacedObject)
    {
        ArgumentNullException.ThrowIfNull(model);

        var assetName = Normalize(sourceAssetName);
        model.SourceAssetName = assetName;

        // Opaque/cutout visual geometry can receive the stage shadow. Transparent
        // overlays and non-depth-writing terrain layers must not sample it.
        model.ReceivesShadow = model.WriteDepth && !model.BlendEnabled;

        if (string.IsNullOrEmpty(assetName))
        {
            // Standalone model viewer compatibility: keep the historical behavior
            // when there is no workspace/source identity to classify.
            model.CastsShadow = model.WriteDepth && !model.BlendEnabled;
            return;
        }

        if (isPlacedObject)
        {
            // Placed props/characters are not part of the static building shadow
            // list shown by the 2006 Lighting Editor stage preview.
            model.CastsShadow = false;
            return;
        }

        model.CastsShadow = IsArchitecturalCaster(assetName) &&
                            model.WriteDepth &&
                            !model.BlendEnabled;
    }

    public static bool IsArchitecturalCaster(string? sourceAssetName)
    {
        var assetName = Normalize(sourceAssetName);
        if (string.IsNullOrEmpty(assetName))
        {
            return false;
        }

        // Dedicated proxy meshes override the generic exclusions.
        if (assetName.Contains("realshadow", StringComparison.Ordinal) ||
            assetName.Contains("shadow_obj", StringComparison.Ordinal))
        {
            return true;
        }

        for (var index = 0; index < ReceiverOnlyTokens.Length; index++)
        {
            if (assetName.Contains(ReceiverOnlyTokens[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        // Default is to cast: only the non-solid backdrop (sky, distant scenery,
        // previews) and the ground receiver are excluded by the tokens above.
        return true;
    }

    private static string Normalize(string? sourceAssetName)
    {
        if (string.IsNullOrWhiteSpace(sourceAssetName))
        {
            return string.Empty;
        }

        var value = sourceAssetName.Trim().Replace('\\', '/');
        var slash = value.LastIndexOf('/');
        if (slash >= 0)
        {
            value = value[(slash + 1)..];
        }

        if (value.EndsWith(".mdn", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^4];
        }

        return value.ToLowerInvariant();
    }
}
