using System.Text.Json;

namespace DataConfigEditor.Settings;

public sealed class UiSettingsStore
{
    private readonly string _filePath;

    public UiSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public UiSettings Load()
    {
        if (!File.Exists(_filePath))
            return UiSettings.Default;

        try
        {
            var json = File.ReadAllText(_filePath);
            return (JsonSerializer.Deserialize<UiSettings>(json) ?? UiSettings.Default).Normalize();
        }
        catch
        {
            return UiSettings.Default;
        }
    }

    public void Save(UiSettings settings)
    {
        var parent = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(settings.Normalize(), new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        File.WriteAllText(_filePath, json);
    }
}
