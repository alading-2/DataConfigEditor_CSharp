using System.Text.Json;

namespace DataConfigEditor.Workspace;

public sealed class WorkspaceSettingsStore
{
    private readonly string _filePath;

    public WorkspaceSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public WorkspaceSettings Load()
    {
        if (!File.Exists(_filePath))
            return WorkspaceSettings.Default;

        try
        {
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<WorkspaceSettings>(json);
            return Normalize(settings);
        }
        catch
        {
            return WorkspaceSettings.Default;
        }
    }

    public void Save(WorkspaceSettings settings)
    {
        var parent = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(Normalize(settings), new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(_filePath, json);
    }

    private static WorkspaceSettings Normalize(WorkspaceSettings? settings)
    {
        if (settings is null)
            return WorkspaceSettings.Default;

        var patterns = settings.ExcludePatterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return settings with
        {
            ExcludePatterns = patterns.Length == 0 ? WorkspaceSettings.DefaultExcludePatterns : patterns,
        };
    }
}
