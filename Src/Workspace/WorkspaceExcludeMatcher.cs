namespace DataConfigEditor.Workspace;

public sealed class WorkspaceExcludeMatcher
{
    private readonly string _rootPath;
    private readonly WorkspaceSettings _settings;

    public WorkspaceExcludeMatcher(string rootPath, WorkspaceSettings settings)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _settings = settings;
    }

    public bool IsExcluded(string fullPath, bool isDirectory)
    {
        var relativePath = Path.GetRelativePath(_rootPath, Path.GetFullPath(fullPath))
            .Replace('\\', '/');
        var name = Path.GetFileName(fullPath);

        if (!isDirectory && !name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var pattern in _settings.ExcludePatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            if (pattern.StartsWith("!", StringComparison.Ordinal))
                continue;

            if (MatchesPattern(relativePath, name, pattern))
                return true;
        }

        return false;
    }

    private static bool MatchesPattern(string relativePath, string name, string pattern)
    {
        pattern = pattern.Trim().Replace('\\', '/');

        if (pattern.StartsWith("**/", StringComparison.Ordinal) &&
            pattern.EndsWith("/**", StringComparison.Ordinal))
        {
            var segment = pattern[3..^3];
            return relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
        }

        if (pattern.StartsWith("*.", StringComparison.Ordinal))
            return name.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);

        return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase) ||
               relativePath.Contains($"/{pattern}/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith($"{pattern}/", StringComparison.OrdinalIgnoreCase);
    }
}
