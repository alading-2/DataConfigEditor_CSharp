using DataConfigEditor.Documents;
using DataConfigEditor.Parsing;

namespace DataConfigEditor.Tests.Parsing;

public class CsTableParserTests
{
    [Fact]
    public void ParseConfigFile_ReturnsTableDocument()
    {
        var parser = new CsTableParser();
        var filePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleConfig.cs");

        var document = parser.ParseFile(filePath);

        Assert.True(document.IsTable);
        Assert.Equal("SampleConfig", document.Title);
        Assert.Collection(document.Columns,
            column => Assert.Equal("实例名", column.Header),
            column => Assert.Equal("Name", column.Key),
            column => Assert.Equal("Cooldown", column.Key));
        Assert.Single(document.Rows);
        Assert.Equal("Dash", document.Rows[0].Header);
    }

    [Fact]
    public void ParseFile_WithoutInstances_ReturnsDiagnosticDocument()
    {
        var parser = new CsTableParser();
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        File.WriteAllText(filePath, """
namespace TestData;

public class EmptyConfig
{
    public string? Name { get; set; }
}
""");

        var document = parser.ParseFile(filePath);

        Assert.False(document.IsTable);
        Assert.Equal("当前文件无法转换为表格视图", document.Diagnostic?.Message);
    }
}
