using DataConfigEditor.Settings;

namespace DataConfigEditor.Tests.Settings;

public class UiSettingsStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultSettings()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new UiSettingsStore(filePath);

        var settings = store.Load();

        Assert.Equal(UiSettings.Default.GridTopPadding, settings.GridTopPadding);
        Assert.Equal(UiSettings.Default.GridFontSize, settings.GridFontSize);
        Assert.Equal(UiSettings.Default.GridRowHeight, settings.GridRowHeight);
        Assert.Equal(UiSettings.Default.ColumnSizingMode, settings.ColumnSizingMode);
    }

    [Fact]
    public void SaveAndLoad_PreservesTableViewSettings()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new UiSettingsStore(filePath);
        var settings = new UiSettings
        {
            GridTopPadding = 18,
            GridFontSize = 11.5f,
            GridRowHeight = 32,
            FixedColumnWidth = 220,
            ColumnSizingMode = GridColumnSizingMode.AutoFitDisplayedCells,
        };

        store.Save(settings);

        var loaded = store.Load();
        Assert.Equal(18, loaded.GridTopPadding);
        Assert.Equal(11.5f, loaded.GridFontSize);
        Assert.Equal(32, loaded.GridRowHeight);
        Assert.Equal(220, loaded.FixedColumnWidth);
        Assert.Equal(GridColumnSizingMode.AutoFitDisplayedCells, loaded.ColumnSizingMode);
    }
}
