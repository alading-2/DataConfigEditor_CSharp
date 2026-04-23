using DataConfigEditor.Documents;

namespace DataConfigEditor.Presentation;

public sealed class WorkspacePresenter
{
    public WorkspaceViewState ShowWelcome(IReadOnlyList<string> recentDirectories)
    {
        return new WorkspaceViewState
        {
            Kind = WorkspaceViewKind.Welcome,
            RecentDirectories = recentDirectories,
        };
    }

    public WorkspaceViewState ShowDocument(TableDocument document)
    {
        if (document.Diagnostic is not null)
        {
            return new WorkspaceViewState
            {
                Kind = WorkspaceViewKind.Message,
                Message = document.Diagnostic.Message,
                Document = document,
            };
        }

        return new WorkspaceViewState
        {
            Kind = WorkspaceViewKind.Table,
            Document = document,
        };
    }
}
