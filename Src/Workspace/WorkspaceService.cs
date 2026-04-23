namespace DataConfigEditor.Workspace;

public sealed class WorkspaceService
{
    public WorkspaceEntry BuildTree(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException(rootPath);

        return BuildEntry(rootPath);
    }

    private static WorkspaceEntry BuildEntry(string path)
    {
        if (Directory.Exists(path))
        {
            var children = Directory.GetDirectories(path)
                .Select(BuildEntry)
                .Concat(Directory.GetFiles(path, "*.cs").Select(BuildEntry))
                .OrderBy(entry => entry.IsDirectory ? 0 : 1)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new WorkspaceEntry
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = true,
                Children = children,
            };
        }

        return new WorkspaceEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            IsDirectory = false,
        };
    }
}
