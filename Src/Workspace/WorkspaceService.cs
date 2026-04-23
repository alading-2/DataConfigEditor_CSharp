namespace DataConfigEditor.Workspace;

public sealed class WorkspaceService
{
    public WorkspaceEntry BuildTree(string rootPath, WorkspaceSettings? settings = null)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException(rootPath);

        var effectiveSettings = settings ?? WorkspaceSettings.Default;
        var matcher = new WorkspaceExcludeMatcher(rootPath, effectiveSettings);
        return BuildEntry(rootPath, matcher, effectiveSettings, inheritedHidden: false)
            ?? throw new DirectoryNotFoundException(rootPath);
    }

    private static WorkspaceEntry? BuildEntry(
        string path,
        WorkspaceExcludeMatcher matcher,
        WorkspaceSettings settings,
        bool inheritedHidden)
    {
        var isDirectory = Directory.Exists(path);
        var isHidden = inheritedHidden || matcher.IsExcluded(path, isDirectory);

        if (isHidden && !settings.ShowHiddenEntries)
            return null;

        if (isDirectory)
        {
            var children = Directory.GetDirectories(path)
                .Select(child => BuildEntry(child, matcher, settings, isHidden))
                .Concat(Directory.GetFiles(path).Select(child => BuildEntry(child, matcher, settings, isHidden)))
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .OrderBy(entry => entry.IsDirectory ? 0 : 1)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new WorkspaceEntry
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = true,
                IsHidden = isHidden,
                Children = children,
            };
        }

        return new WorkspaceEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            IsDirectory = false,
            IsHidden = isHidden,
        };
    }
}
