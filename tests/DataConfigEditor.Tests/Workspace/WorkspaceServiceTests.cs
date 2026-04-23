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
}
