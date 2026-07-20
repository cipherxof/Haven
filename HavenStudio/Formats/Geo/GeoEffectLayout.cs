using System;

namespace HavenStudio.Formats.Geo;

/// <summary>
/// Decodes the three relative payload slots packed into a GEOM chunk-6 effect index.
/// Slots are measured in eight-byte units from the start of the effect record.
/// </summary>
public static class GeoEffectLayout
{
    public const int SlotMask = 0x3FF;

    public static int GetPositionSlot(int index) => index & SlotMask;

    public static int GetRotationSlot(int index) => (index >> 10) & SlotMask;

    public static int GetScaleSlot(int index) => (index >> 20) & SlotMask;

    public static int GetPositionOffset(GeoEffect effect) =>
        GetPayloadOffset(effect, GetPositionSlot(effect.Index));

    public static int GetRotationOffset(GeoEffect effect) =>
        GetPayloadOffset(effect, GetRotationSlot(effect.Index));

    public static int GetScaleOffset(GeoEffect effect) =>
        GetPayloadOffset(effect, GetScaleSlot(effect.Index));

    private static int GetPayloadOffset(GeoEffect effect, int slot)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return checked(effect.ChunkOffset + slot * 8);
    }
}
