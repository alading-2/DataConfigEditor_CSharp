using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class WorkspaceServiceTests
{
    [Fact]
    public void ScanDirectory_ReturnsFoldersAndCsFilesOnly()
    {
        var root = Directory.CreateTempSubdirectory();
        Directory.CreateDirectory(Path.Combine(root.FullName, "Child"));
        File.WriteAllText(Path.Combine(root.FullName, "Child", "Config.cs"), "public class Config {}");
        File.WriteAllText(Path.Combine(root.FullName, "Child", "notes.txt"), "ignore");

        var service = new WorkspaceService();

        var entry = service.BuildTree(root.FullName);

        Assert.Equal(root.FullName, entry.FullPath);
        Assert.Single(entry.Children);
        Assert.Single(entry.Children[0].Children);
        Assert.Equal("Config.cs", entry.Children[0].Children[0].Name);
    }

    [Fact]
    public void BuildTree_DefaultSettings_ExcludesHiddenDirectoriesAndUidFiles()
    {
        var root = Directory.CreateTempSubdirectory();
        Directory.CreateDirectory(Path.Combine(root.FullName, "Ability"));
        Directory.CreateDirectory(Path.Combine(root.FullName, "obj"));
        File.WriteAllText(Path.Combine(root.FullName, "Ability", "AbilityConfigData.cs"), "public class AbilityConfigData {}");
        File.WriteAllText(Path.Combine(root.FullName, "Ability", "AbilityConfigData.cs.uid"), "ignore");
        File.WriteAllText(Path.Combine(root.FullName, "obj", "Generated.cs"), "ignore");

        var service = new WorkspaceService();

        var entry = service.BuildTree(root.FullName, WorkspaceSettings.Default);

        var ability = Assert.Single(entry.Children);
        Assert.Equal("Ability", ability.Name);
        Assert.Single(ability.Children);
        Assert.Equal("AbilityConfigData.cs", ability.Children[0].Name);
    }

    [Fact]
    public void BuildTree_ShowHiddenEntries_IncludesHiddenEntriesMarkedHidden()
    {
        var root = Directory.CreateTempSubdirectory();
        Directory.CreateDirectory(Path.Combine(root.FullName, "obj"));
        File.WriteAllText(Path.Combine(root.FullName, "obj", "Generated.cs"), "ignore");
        var settings = WorkspaceSettings.Default with { ShowHiddenEntries = true };
        var service = new WorkspaceService();

        var entry = service.BuildTree(root.FullName, settings);

        var hiddenObj = Assert.Single(entry.Children);
        Assert.Equal("obj", hiddenObj.Name);
        Assert.True(hiddenObj.IsHidden);
        Assert.True(hiddenObj.Children[0].IsHidden);
    }
}
