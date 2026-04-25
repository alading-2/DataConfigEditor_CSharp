using DataConfigEditor.Documents;
using DataConfigEditor.Core;
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

    [Fact]
    public void ParseConfigFile_WithAssemblyMetadata_EnrichesEnumColumnAndDisplayValue()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        File.WriteAllText(filePath, $$"""
using DataConfigEditor.Tests.Parsing;

namespace TestData;

public class AbilityConfig
{
    /// <summary>触发模式</summary>
    public TestAbilityTriggerMode TriggerMode { get; set; }

    public static readonly AbilityConfig Dash = new()
    {
        TriggerMode = TestAbilityTriggerMode.Manual,
    };
}
""");

        var provider = new AssemblyTypeMetadataProvider(typeof(TestAbilityTriggerMode).Assembly.Location);
        var parser = new CsTableParser(provider);

        var document = parser.ParseFile(filePath);

        var triggerColumn = Assert.Single(document.Columns.Where(column => column.Key == "TriggerMode"));
        Assert.Equal("TestAbilityTriggerMode", triggerColumn.TypeName);
        Assert.True(triggerColumn.IsEnum);
        Assert.False(triggerColumn.IsNumeric);
        Assert.Equal(["Manual", "Auto", "Passive"], triggerColumn.EnumOptions.Select(option => option.Name));

        var triggerCell = Assert.Single(document.Rows[0].Cells.Where(cell => cell.ColumnKey == "TriggerMode"));
        Assert.Equal("TestAbilityTriggerMode.Manual", triggerCell.RawValue);
        Assert.Equal("Manual", triggerCell.Value);
    }

    [Fact]
    public void ParseConfigFile_WithAssemblyMetadata_ShowsImplicitDefaultsForUnsetValueTypes()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        File.WriteAllText(filePath, $$"""
using DataConfigEditor.Tests.Parsing;

namespace TestData;

public class DefaultedConfig
{
    public int Level { get; set; }
    public bool Enabled { get; set; }
    public TestAbilityTriggerMode TriggerMode { get; set; }

    public static readonly DefaultedConfig Dash = new()
    {
    };
}
""");

        var provider = new AssemblyTypeMetadataProvider(typeof(TestAbilityTriggerMode).Assembly.Location);
        var parser = new CsTableParser(provider);

        var document = parser.ParseFile(filePath);
        var row = Assert.Single(document.Rows);

        Assert.Equal("0", row.Cells.Single(cell => cell.ColumnKey == "Level").Value);
        Assert.Equal("false", row.Cells.Single(cell => cell.ColumnKey == "Enabled").Value);
        Assert.Equal("Manual", row.Cells.Single(cell => cell.ColumnKey == "TriggerMode").Value);
        Assert.Equal("", row.Cells.Single(cell => cell.ColumnKey == "Level").RawValue);
        Assert.Equal("", row.Cells.Single(cell => cell.ColumnKey == "Enabled").RawValue);
        Assert.Equal("", row.Cells.Single(cell => cell.ColumnKey == "TriggerMode").RawValue);
    }

    [Fact]
    public void ParseConfigFile_PreservesFullPropertyDefaultExpressions()
    {
        var parser = new CsTableParser(new AssemblyTypeMetadataProvider(typeof(TestAbilityType).Assembly.Location));
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        File.WriteAllText(filePath, """
using DataConfigEditor.Tests.Parsing;

namespace TestData;

public class ExpressionDefaultConfig
{
    public int AbilityLevel { get; set; } = (int)DataKey.AbilityLevel.DefaultValue!;
    public TestAbilityType AbilityType { get; set; } = TestAbilityType.Passive;

    public static readonly ExpressionDefaultConfig Dash = new()
    {
    };
}
""");

        var document = parser.ParseFile(filePath);
        var row = Assert.Single(document.Rows);

        Assert.Equal("(int)DataKey.AbilityLevel.DefaultValue!", row.Cells.Single(cell => cell.ColumnKey == "AbilityLevel").RawValue);
        Assert.Equal("TestAbilityType.Passive", row.Cells.Single(cell => cell.ColumnKey == "AbilityType").RawValue);
        Assert.Equal("Passive", row.Cells.Single(cell => cell.ColumnKey == "AbilityType").Value);
    }

    [Fact]
    public void ParseConfigFile_ResolvesDataKeyDefaultValuesFromSiblingDataKeyDirectory()
    {
        var root = Directory.CreateTempSubdirectory();
        var dataNew = Directory.CreateDirectory(Path.Combine(root.FullName, "DataNew", "Ability"));
        var dataKey = Directory.CreateDirectory(Path.Combine(root.FullName, "DataKey", "Ability"));
        File.WriteAllText(Path.Combine(dataKey.FullName, "DataKey_Ability.cs"), """
using DataConfigEditor.Tests.Parsing;

public static partial class DataKey
{
    public static readonly DataMeta AbilityLevel = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityLevel), DisplayName = "技能等级", Type = typeof(int),
            DefaultValue = 1, MinValue = 1
        });

    public static readonly DataMeta AbilityType = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityType), DisplayName = "技能类型", Type = typeof(TestAbilityType),
            DefaultValue = TestAbilityType.Passive
        });
}
""");
        var filePath = Path.Combine(dataNew.FullName, "AbilityData.cs");
        File.WriteAllText(filePath, """
using DataConfigEditor.Tests.Parsing;

namespace TestData;

public class AbilityData
{
    public int AbilityLevel { get; set; } = (int)DataKey.AbilityLevel.DefaultValue!;
    public TestAbilityType AbilityType { get; set; } = (TestAbilityType)DataKey.AbilityType.DefaultValue!;

    public static readonly AbilityData Dash = new()
    {
    };
}
""");

        var parser = new CsTableParser(new AssemblyTypeMetadataProvider(typeof(TestAbilityType).Assembly.Location));

        var document = parser.ParseFile(filePath);
        var row = Assert.Single(document.Rows);

        Assert.Equal("1", row.Cells.Single(cell => cell.ColumnKey == "AbilityLevel").Value);
        Assert.Equal("Passive", row.Cells.Single(cell => cell.ColumnKey == "AbilityType").Value);
        Assert.Equal("(int)DataKey.AbilityLevel.DefaultValue!", row.Cells.Single(cell => cell.ColumnKey == "AbilityLevel").RawValue);
        Assert.Equal("(TestAbilityType)DataKey.AbilityType.DefaultValue!", row.Cells.Single(cell => cell.ColumnKey == "AbilityType").RawValue);
    }

    [Fact]
    public void ParseConfigFile_DisplaysStringAndPathLiteralsWithoutQuotesOrCommentTruncation()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        File.WriteAllText(filePath, """
namespace TestData;

public class PathConfig
{
    public string? FeatureGroupId { get; set; }
    public string EffectScenePath { get; set; } = "";

    public static readonly PathConfig Slam = new()
    {
        FeatureGroupId = "技能.主动",
        EffectScenePath = "res://assets/Effect/020/AnimatedSprite2D/020.tscn",
    };
}
""");

        var parser = new CsTableParser();

        var document = parser.ParseFile(filePath);
        var row = Assert.Single(document.Rows);

        Assert.Equal("技能.主动", row.Cells.Single(cell => cell.ColumnKey == "FeatureGroupId").Value);
        Assert.Equal("res://assets/Effect/020/AnimatedSprite2D/020.tscn", row.Cells.Single(cell => cell.ColumnKey == "EffectScenePath").Value);
        Assert.Equal("\"res://assets/Effect/020/AnimatedSprite2D/020.tscn\"", row.Cells.Single(cell => cell.ColumnKey == "EffectScenePath").RawValue);
    }

    [Fact]
    public void ParseConfigFile_ResolvesConstStringExpressionFromAssemblyMetadata()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        File.WriteAllText(filePath, """
using DataConfigEditor.Tests.Parsing;

namespace TestData;

public class StaticIdConfig
{
    public string? FeatureHandlerId { get; set; }

    public static readonly StaticIdConfig Slam = new()
    {
        FeatureHandlerId = TestFeatureId.Ability.Active.Slam,
    };
}
""");
        var parser = new CsTableParser(new AssemblyTypeMetadataProvider(typeof(TestFeatureId).Assembly.Location));

        var document = parser.ParseFile(filePath);
        var row = Assert.Single(document.Rows);

        Assert.Equal("Ability.Active.Slam", row.Cells.Single(cell => cell.ColumnKey == "FeatureHandlerId").Value);
        Assert.Equal("TestFeatureId.Ability.Active.Slam", row.Cells.Single(cell => cell.ColumnKey == "FeatureHandlerId").RawValue);
    }

    [Fact]
    public void ParseConfigFile_ResolvesStaticReadonlyStringExpressionFromLoadedAssembly()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
        File.WriteAllText(filePath, """
using DataConfigEditor.Tests.Parsing;

namespace TestData;

public class StaticReadonlyIdConfig
{
    public string? FeatureHandlerId { get; set; }

    public static readonly StaticReadonlyIdConfig OrbitSkill = new()
    {
        FeatureHandlerId = TestFeatureId.Ability.Passive.OrbitSkill,
    };
}
""");
        var parser = new CsTableParser(new AssemblyTypeMetadataProvider(typeof(TestFeatureId).Assembly.Location));

        var document = parser.ParseFile(filePath);
        var row = Assert.Single(document.Rows);

        Assert.Equal("Ability.Passive.OrbitSkill", row.Cells.Single(cell => cell.ColumnKey == "FeatureHandlerId").Value);
        Assert.Equal("TestFeatureId.Ability.Passive.OrbitSkill", row.Cells.Single(cell => cell.ColumnKey == "FeatureHandlerId").RawValue);
    }
}

public enum TestAbilityTriggerMode
{
    Manual,
    Auto,
    Passive,
}

public enum TestAbilityType
{
    Active,
    Passive,
}

public static class TestFeatureId
{
    public static class Ability
    {
        public static class Active
        {
            public const string Slam = "Ability.Active.Slam";
        }

        public static class Passive
        {
            public static readonly string OrbitSkill = "Ability.Passive.OrbitSkill";
        }
    }
}
