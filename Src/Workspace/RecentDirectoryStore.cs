using System.Text.Json;

namespace DataConfigEditor.Workspace;

public sealed class RecentDirectoryStore
{
    private readonly string _filePath;

    public RecentDirectoryStore(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(_filePath))
            return Array.Empty<string>();

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    public void Save(IEnumerable<string> directories)
    {
        var parent = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(directories.ToList(), new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        File.WriteAllText(_filePath, json);
    }
}
