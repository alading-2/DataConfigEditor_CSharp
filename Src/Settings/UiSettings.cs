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

    public static UiSettings Default => new();

    public int GridTopPadding { get; init; } = 12;
    public float GridFontSize { get; init; } = 9.5f;
    public int GridRowHeight { get; init; } = 28;
    public int FixedColumnWidth { get; init; } = 180;
    public GridColumnSizingMode ColumnSizingMode { get; init; } = GridColumnSizingMode.Fixed;

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
            ColumnSizingMode = mode,
        };
    }
}
