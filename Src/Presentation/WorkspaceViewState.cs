using DataConfigEditor.Documents;

namespace DataConfigEditor.Presentation;

public enum WorkspaceViewKind
{
    Welcome,
    Table,
    Message,
}

public sealed class WorkspaceViewState
{
    public required WorkspaceViewKind Kind { get; init; }
    public TableDocument? Document { get; init; }
    public string Message { get; init; } = "";
    public IReadOnlyList<string> RecentDirectories { get; init; } = Array.Empty<string>();
}
