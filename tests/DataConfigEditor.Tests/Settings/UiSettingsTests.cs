using DataConfigEditor.Settings;

namespace DataConfigEditor.Tests.Settings;

public class UiSettingsTests
{
    [Fact]
    public void Normalize_ClampsValuesToUsableRanges()
    {
        var settings = new UiSettings
        {
            GridTopPadding = -100,
            GridFontSize = 100,
            GridRowHeight = 1,
            FixedColumnWidth = 20,
            HeaderHeight = 999,
            InstanceColumnWidth = 20,
        };

        var normalized = settings.Normalize();

        Assert.Equal(UiSettings.MinGridTopPadding, normalized.GridTopPadding);
        Assert.Equal(UiSettings.MaxGridFontSize, normalized.GridFontSize);
        Assert.Equal(UiSettings.MinGridRowHeight, normalized.GridRowHeight);
        Assert.Equal(UiSettings.MinFixedColumnWidth, normalized.FixedColumnWidth);
        Assert.Equal(UiSettings.MaxHeaderHeight, normalized.HeaderHeight);
        Assert.Equal(UiSettings.MinInstanceColumnWidth, normalized.InstanceColumnWidth);
    }

    [Fact]
    public void IncreaseFontSize_AlsoKeepsRowHeightReadable()
    {
        var settings = UiSettings.Default with { GridFontSize = 12f, GridRowHeight = 20 };

        var updated = UiSettingsAdjuster.IncreaseFontSize(settings);

        Assert.Equal(13f, updated.GridFontSize);
        Assert.True(updated.GridRowHeight >= 31);
    }

    [Fact]
    public void Normalize_TrimAssemblyPathAndDropWhitespaceOnlyValue()
    {
        var settings = new UiSettings
        {
            MetadataAssemblyPath = "  /tmp/game/bin/Debug/net10.0-windows/Game.dll  ",
        };

        var normalized = settings.Normalize();

        Assert.Equal("/tmp/game/bin/Debug/net10.0-windows/Game.dll", normalized.MetadataAssemblyPath);
        Assert.Equal("", (settings with { MetadataAssemblyPath = "   " }).Normalize().MetadataAssemblyPath);
    }
}
