using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class WorkspaceExcludeMatcherTests
{
    [Theory]
    [InlineData("bin", true)]
    [InlineData("obj", true)]
    [InlineData(".godot", true)]
    [InlineData(".git", true)]
    [InlineData(".idea", true)]
    [InlineData(".vscode", true)]
    [InlineData(".history", true)]
    [InlineData(".superpowers", true)]
    [InlineData("Ability", false)]
    public void IsExcluded_DefaultDirectoryRules_ReturnsExpectedResult(string directoryName, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var fullPath = Path.Combine(root, directoryName);
        var matcher = new WorkspaceExcludeMatcher(root, WorkspaceSettings.Default);

        var result = matcher.IsExcluded(fullPath, isDirectory: true);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("asset.uid", true)]
    [InlineData("AbilityConfigData.cs", false)]
    [InlineData("notes.txt", true)]
    public void IsExcluded_DefaultFileRules_ReturnsExpectedResult(string fileName, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var fullPath = Path.Combine(root, fileName);
        var matcher = new WorkspaceExcludeMatcher(root, WorkspaceSettings.Default);

        var result = matcher.IsExcluded(fullPath, isDirectory: false);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsExcluded_CustomPathSegmentPattern_HidesNestedDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var fullPath = Path.Combine(root, "DataNew", "Generated", "Config.cs");
        var settings = WorkspaceSettings.Default with
        {
            ExcludePatterns = WorkspaceSettings.DefaultExcludePatterns
                .Concat(new[] { "**/Generated/**" })
                .ToArray(),
        };
        var matcher = new WorkspaceExcludeMatcher(root, settings);

        var result = matcher.IsExcluded(fullPath, isDirectory: false);

        Assert.True(result);
    }
}
