using DataConfigEditor.Settings;

namespace DataConfigEditor.UI;

public sealed record TableLayoutOptions
{
    // 内容区域与边框的内边距
    public int ContentPadding { get; init; } = 48;
    // 表头高度
    public int HeaderHeight { get; init; } = 64;
    // 行高度
    public int RowHeight { get; init; } = 28;
    // 实例列宽度
    public int InstanceColumnWidth { get; init; } = 140;
    // 默认列宽度
    public int DefaultColumnWidth { get; init; } = 180;
    // 是否冻结实例列
    public bool FreezeInstanceColumn { get; init; } = true;
    // 是否显示表头摘要
    public bool ShowHeaderSummary { get; init; } = true;

    public static TableLayoutOptions FromSettings(UiSettings settings)
    {
        settings = settings.Normalize();

        return new TableLayoutOptions
        {
            ContentPadding = Math.Max(48, settings.GridTopPadding),
            HeaderHeight = Math.Max(64, settings.HeaderHeight),
            RowHeight = settings.GridRowHeight,
            InstanceColumnWidth = settings.InstanceColumnWidth,
            DefaultColumnWidth = settings.FixedColumnWidth,
            FreezeInstanceColumn = settings.FreezeInstanceColumn,
            ShowHeaderSummary = settings.ShowHeaderSummary,
        };
    }
}
