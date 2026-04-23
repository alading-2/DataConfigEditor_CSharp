using DataConfigEditor.Settings;
using DataConfigEditor.UI;

namespace DataConfigEditor.Tests.UI;

public class TableLayoutOptionsTests
{
    [Fact]
    public void FromSettings_Defaults_UseStableTableLayout()
    {
        var options = TableLayoutOptions.FromSettings(UiSettings.Default);

        Assert.Equal(8, options.ContentPadding);
        Assert.Equal(48, options.HeaderHeight);
        Assert.Equal(28, options.RowHeight);
        Assert.Equal(140, options.InstanceColumnWidth);
        Assert.Equal(180, options.DefaultColumnWidth);
        Assert.True(options.FreezeInstanceColumn);
    }

    [Fact]
    public void FromSettings_OutOfRangeValues_AreClamped()
    {
        var settings = UiSettings.Default with
        {
            GridTopPadding = -100,
            GridRowHeight = 999,
            FixedColumnWidth = 999,
            HeaderHeight = 10,
        };

        var options = TableLayoutOptions.FromSettings(settings);

        Assert.Equal(8, options.ContentPadding);
        Assert.Equal(UiSettings.MaxGridRowHeight, options.RowHeight);
        Assert.Equal(UiSettings.MaxFixedColumnWidth, options.DefaultColumnWidth);
        Assert.Equal(UiSettings.MinHeaderHeight, options.HeaderHeight);
    }
}
