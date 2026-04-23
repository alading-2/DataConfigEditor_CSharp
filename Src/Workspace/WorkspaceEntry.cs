namespace DataConfigEditor.Workspace;

public sealed class WorkspaceEntry
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }
    public bool IsHidden { get; init; }
    public IReadOnlyList<WorkspaceEntry> Children { get; init; } = Array.Empty<WorkspaceEntry>();
}
