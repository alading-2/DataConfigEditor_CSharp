using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class WorkspaceSettingsStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaultSettings()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "workspace-settings.json");
        var store = new WorkspaceSettingsStore(filePath);

        var settings = store.Load();

        Assert.False(settings.ShowHiddenEntries);
        Assert.Contains("**/bin/**", settings.ExcludePatterns);
    }

    [Fact]
    public void SaveThenLoad_PreservesSettings()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "workspace-settings.json");
        var store = new WorkspaceSettingsStore(filePath);
        var saved = WorkspaceSettings.Default with
        {
            ShowHiddenEntries = true,
            ExcludePatterns = ["**/Generated/**", "*.tmp"],
        };

        store.Save(saved);
        var loaded = store.Load();

        Assert.True(loaded.ShowHiddenEntries);
        Assert.Equal(["**/Generated/**", "*.tmp"], loaded.ExcludePatterns);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaultSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "workspace-settings.json");
        File.WriteAllText(filePath, "{ invalid");
        var store = new WorkspaceSettingsStore(filePath);

        var settings = store.Load();

        Assert.False(settings.ShowHiddenEntries);
        Assert.Contains("**/obj/**", settings.ExcludePatterns);
    }
}
