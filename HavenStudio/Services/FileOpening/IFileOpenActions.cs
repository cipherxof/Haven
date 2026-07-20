using System.Threading;
using System.Threading.Tasks;
using HavenStudio.Services.Workspace;

namespace HavenStudio.Services.FileOpening;

public interface IFileOpenActions
{
    Task OpenAsync(
        FileOpenKind kind,
        WorkspacePath path,
        IWorkspaceCatalog? workspace,
        CancellationToken cancellationToken);
}
