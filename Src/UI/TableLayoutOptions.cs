using DataConfigEditor.Settings;

namespace DataConfigEditor.UI;

public sealed record TableLayoutOptions
{
    public int ContentPadding { get; init; } = 8;
    public int HeaderHeight { get; init; } = 48;
    public int RowHeight { get; init; } = 28;
    public int InstanceColumnWidth { get; init; } = 140;
    public int DefaultColumnWidth { get; init; } = 180;
    public bool FreezeInstanceColumn { get; init; } = true;
    public bool ShowHeaderSummary { get; init; } = true;

    public static TableLayoutOptions FromSettings(UiSettings settings)
    {
        settings = settings.Normalize();

        return new TableLayoutOptions
        {
            ContentPadding = Math.Max(8, settings.GridTopPadding),
            HeaderHeight = settings.HeaderHeight,
            RowHeight = settings.GridRowHeight,
            InstanceColumnWidth = settings.InstanceColumnWidth,
            DefaultColumnWidth = settings.FixedColumnWidth,
            FreezeInstanceColumn = settings.FreezeInstanceColumn,
            ShowHeaderSummary = settings.ShowHeaderSummary,
        };
    }
}
