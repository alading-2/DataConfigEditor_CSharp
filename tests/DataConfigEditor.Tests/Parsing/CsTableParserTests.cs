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
            column =>
            {
                Assert.Equal("Name", column.Key);
                Assert.Equal("显示名称", column.Summary);
            },
            column =>
            {
                Assert.Equal("Cooldown", column.Key);
                Assert.Equal("冷却时间", column.Summary);
            });
        Assert.Single(document.Rows);
        Assert.Equal("Dash", document.Rows[0].Header);
    }

    [Fact]
    public void ParseConfigFile_ThreeLineSummary_ReturnsPropertySummary()
    {
        var parser = new CsTableParser();
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        File.WriteAllText(filePath, """
namespace TestData;

public class ThreeLineSummaryConfig
{
    /// <summary>
    ///     技能分组 ID（用于 UI / 测试面板展示）
    /// </summary>
    public string? FeatureGroupId { get; set; }

    /// <summary>猛击</summary>
    public static readonly ThreeLineSummaryConfig Slam = new()
    {
        FeatureGroupId = "技能.主动",
    };
}
""");

        var document = parser.ParseFile(filePath);

        Assert.True(document.IsTable);
        Assert.Equal("技能分组 ID（用于 UI / 测试面板展示）", document.Columns[1].Summary);
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

    [Fact]
    public void ParseConfigFile_PropertyWithoutSummary_UsesUncommentedLabel()
    {
        var parser = new CsTableParser();
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        File.WriteAllText(filePath, """
namespace TestData;

public class MissingSummaryConfig
{
    public string? Name { get; set; }

    public static readonly MissingSummaryConfig Dash = new()
    {
        Name = "冲刺",
    };
}
""");

        var document = parser.ParseFile(filePath);

        Assert.True(document.IsTable);
        Assert.Equal("未注释", document.Columns[1].Summary);
    }
}
