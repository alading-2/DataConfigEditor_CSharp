namespace DataConfigEditor.Workspace;

public sealed record WorkspaceSettings
{
    public static readonly string[] DefaultExcludePatterns =
    [
        "**/bin/**",
        "**/obj/**",
        "**/.godot/**",
        "**/.git/**",
        "**/.idea/**",
        "**/.vscode/**",
        "**/.history/**",
        "**/.superpowers/**",
        "*.uid",
        "!*.cs",
    ];

    public static WorkspaceSettings Default => new();

    public bool ShowHiddenEntries { get; init; }

    public IReadOnlyList<string> ExcludePatterns { get; init; } = DefaultExcludePatterns;
}
