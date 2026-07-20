using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HavenStudio.Formats.Dds;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Services;

public static class DdsPreviewService
{
    public static async Task ShowPreviewFromFileAsync(Window owner, string filePath)
    {
        var path = WorkspacePath.ParseLegacy(filePath);
        if (path.IsArchiveEntry)
        {
            throw new InvalidOperationException("Archived DDS files require an open workspace.");
        }

        var dds = DdsFile.Read(path.PhysicalPath);
        var title = $"Preview - {path.FileName}";
        await ShowPreviewDialogAsync(owner, title, (ushort)dds.Width, (ushort)dds.Height, dds.FourCc, dds.MainData, dds.MipData);
    }

    public static async Task ShowPreviewFromFileAsync(
        Window owner,
        WorkspacePath filePath,
        IWorkspaceCatalog workspace)
    {
        using var stream = workspace.OpenRead(filePath);
        var dds = DdsFile.Read(stream);
        var title = $"Preview - {filePath.FileName}";
        await ShowPreviewDialogAsync(
            owner,
            title,
            (ushort)dds.Width,
            (ushort)dds.Height,
            dds.FourCc,
            dds.MainData,
            dds.MipData);
    }

    public static async Task ShowPreviewDialogAsync(
        Window owner,
        string title,
        ushort width,
        ushort height,
        string format,
        byte[]? mainData,
        byte[]? mipData)
    {
        var data = (mainData != null && mainData.Length > 0) ? mainData : (mipData ?? Array.Empty<byte>());
        if (data.Length == 0)
        {
            throw new InvalidOperationException("Texture has no previewable data.");
        }

        var decoded = DecodeForPreview(width, height, format, data);
        var bitmap = new WriteableBitmap(
            new PixelSize(decoded.Width, decoded.Height),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Rgba8888,
            Avalonia.Platform.AlphaFormat.Unpremul);

        using (var fb = bitmap.Lock())
        {
            Marshal.Copy(decoded.Rgba, 0, fb.Address, decoded.Rgba.Length);
        }

        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform
        };

        var previewWindow = new Window
        {
            Title = title,
            Width = Math.Max(420, decoded.Width + 40),
            Height = Math.Max(320, decoded.Height + 80),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Thickness(10),
                Child = image
            }
        };

        await previewWindow.ShowDialog(owner);
    }

    private static PreviewData DecodeForPreview(int width, int height, string format, byte[] data)
    {
        int blockSize = format switch
        {
            "DXT1" => 8,
            "DXT3" => 16,
            "DXT5" => 16,
            _ => throw new NotSupportedException($"Unsupported format '{format}'.")
        };

        int w = Math.Max(1, width);
        int h = Math.Max(1, height);

        for (int attempt = 0; attempt < 12; attempt++)
        {
            int expected = ((w + 3) / 4) * ((h + 3) / 4) * blockSize;
            if (expected <= 0)
            {
                break;
            }

            if (data.Length >= expected)
            {
                var slice = new byte[expected];
                Buffer.BlockCopy(data, 0, slice, 0, expected);
                try
                {
                    var rgba = DxtDecoder.DecodeToRgba(w, h, format, slice);
                    return new PreviewData(rgba, w, h);
                }
                catch
                {
                    // Try smaller mip dimensions below.
                }
            }

            if (w == 1 && h == 1)
            {
                break;
            }

            w = Math.Max(1, w / 2);
            h = Math.Max(1, h / 2);
        }

        throw new InvalidOperationException("Unable to decode texture data for preview.");
    }

    private sealed record PreviewData(byte[] Rgba, int Width, int Height);
}
