namespace DataConfigEditor.Settings;

public static class UiSettingsAdjuster
{
    public static UiSettings IncreaseTopPadding(UiSettings settings) =>
        settings with { GridTopPadding = settings.GridTopPadding + 4 };

    public static UiSettings DecreaseTopPadding(UiSettings settings) =>
        settings with { GridTopPadding = settings.GridTopPadding - 4 };

    public static UiSettings IncreaseFontSize(UiSettings settings)
    {
        var fontSize = settings.GridFontSize + 1f;
        var rowHeight = Math.Max(settings.GridRowHeight, GetReadableRowHeight(fontSize));
        return settings with { GridFontSize = fontSize, GridRowHeight = rowHeight };
    }

    public static UiSettings DecreaseFontSize(UiSettings settings) =>
        settings with { GridFontSize = settings.GridFontSize - 1f };

    public static UiSettings IncreaseRowHeight(UiSettings settings) =>
        settings with { GridRowHeight = settings.GridRowHeight + 2 };

    public static UiSettings DecreaseRowHeight(UiSettings settings) =>
        settings with { GridRowHeight = settings.GridRowHeight - 2 };

    public static UiSettings IncreaseColumnWidth(UiSettings settings) =>
        settings with { FixedColumnWidth = settings.FixedColumnWidth + 20 };

    public static UiSettings DecreaseColumnWidth(UiSettings settings) =>
        settings with { FixedColumnWidth = settings.FixedColumnWidth - 20 };

    public static UiSettings SetColumnSizingMode(UiSettings settings, GridColumnSizingMode mode) =>
        settings with { ColumnSizingMode = mode };

    public static UiSettings Reset() => UiSettings.Default;

    private static int GetReadableRowHeight(float fontSize) =>
        (int)Math.Ceiling(fontSize * 2.4f);
}
