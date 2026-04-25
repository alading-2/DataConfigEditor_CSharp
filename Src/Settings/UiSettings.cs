namespace DataConfigEditor.Settings;

public sealed record UiSettings
{
    public const int MinGridTopPadding = 0;
    public const int MaxGridTopPadding = 80;
    public const float MinGridFontSize = 8f;
    public const float MaxGridFontSize = 18f;
    public const int MinGridRowHeight = 22;
    public const int MaxGridRowHeight = 72;
    public const int MinFixedColumnWidth = 80;
    public const int MaxFixedColumnWidth = 480;
    public const int MinContentPadding = 0;
    public const int MaxContentPadding = 80;
    public const int MinHeaderHeight = 36;
    public const int MaxHeaderHeight = 96;
    public const int MinInstanceColumnWidth = 80;
    public const int MaxInstanceColumnWidth = 360;

    public static UiSettings Default => new();

    public int GridTopPadding { get; init; } = 48;
    public float GridFontSize { get; init; } = 9.5f;
    public int GridRowHeight { get; init; } = 28;
    public int FixedColumnWidth { get; init; } = 180;
    public int HeaderHeight { get; init; } = 64;
    public int InstanceColumnWidth { get; init; } = 140;
    public bool FreezeInstanceColumn { get; init; } = true;
    public bool ShowHeaderSummary { get; init; } = true;
    public GridColumnSizingMode ColumnSizingMode { get; init; } = GridColumnSizingMode.Fixed;
    public string MetadataAssemblyPath { get; init; } = "";

    public UiSettings Normalize()
    {
        var mode = Enum.IsDefined(typeof(GridColumnSizingMode), ColumnSizingMode)
            ? ColumnSizingMode
            : GridColumnSizingMode.Fixed;

        return this with
        {
            GridTopPadding = Math.Clamp(GridTopPadding, MinGridTopPadding, MaxGridTopPadding),
            GridFontSize = Math.Clamp(GridFontSize, MinGridFontSize, MaxGridFontSize),
            GridRowHeight = Math.Clamp(GridRowHeight, MinGridRowHeight, MaxGridRowHeight),
            FixedColumnWidth = Math.Clamp(FixedColumnWidth, MinFixedColumnWidth, MaxFixedColumnWidth),
            HeaderHeight = Math.Clamp(HeaderHeight, MinHeaderHeight, MaxHeaderHeight),
            InstanceColumnWidth = Math.Clamp(InstanceColumnWidth, MinInstanceColumnWidth, MaxInstanceColumnWidth),
            ColumnSizingMode = mode,
            MetadataAssemblyPath = string.IsNullOrWhiteSpace(MetadataAssemblyPath)
                ? ""
                : MetadataAssemblyPath.Trim(),
        };
    }
}
