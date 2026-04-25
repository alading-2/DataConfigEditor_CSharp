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

    [Fact]
    public void Parse_UsesDllArgumentForMetadataAssemblyPath()
    {
        var dllPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dll");
        File.WriteAllText(dllPath, "stub");

        var options = AppLaunchOptions.Parse(["--dll", dllPath]);

        Assert.Equal(dllPath, options.MetadataAssemblyPath);
    }

    [Fact]
    public void Parse_DllFlagDoesNotConsumeDirectoryArgument()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var dllPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dll");
        File.WriteAllText(dllPath, "stub");

        var options = AppLaunchOptions.Parse(["--dll", dllPath, tempDir.FullName]);

        Assert.Equal(dllPath, options.MetadataAssemblyPath);
        Assert.Equal(tempDir.FullName, options.InitialDirectory);
    }
}
