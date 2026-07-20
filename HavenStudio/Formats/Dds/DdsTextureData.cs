namespace HavenStudio.Formats.Dds;

public sealed record DdsTextureData(
    int Width,
    int Height,
    string FourCc,
    int MipMapCount,
    byte[] MainData,
    byte[] MipData
);
