using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class AppLaunchOptionsTests
{
    [Fact]
    public void Parse_UsesFirstExistingDirectoryArgument()
    {
        var tempDir = Directory.CreateTempSubdirectory();

        var options = AppLaunchOptions.Parse(new[] { tempDir.FullName, "ignored" });

        Assert.Equal(tempDir.FullName, options.InitialDirectory);
    }
}
