using HavenStudio.Utils;

namespace HavenStudio.Services.FileOpening;

public static class FileOpenResultPresenter
{
    public static void Present(FileOpenResult result)
    {
        switch (result.Status)
        {
            case FileOpenStatus.Unsupported:
                MessageDialog.Error("Unsupported File Type", result.Message ?? "Unsupported file type.");
                break;
            case FileOpenStatus.Failed:
                MessageDialog.Error("Open File", result.Message ?? "The file could not be opened.");
                break;
        }
    }
}
