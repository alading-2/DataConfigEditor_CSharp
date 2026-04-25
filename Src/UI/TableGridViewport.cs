using DataConfigEditor.Documents;

namespace DataConfigEditor.UI;

public sealed record TableGridViewportSnapshot(
    string? CurrentRowHeader,
    string? CurrentColumnKey,
    int CurrentRowIndex,
    int CurrentColumnIndex,
    string? FirstDisplayedRowHeader,
    int FirstDisplayedRowIndex,
    string? FirstDisplayedColumnKey,
    int FirstDisplayedColumnIndex,
    int HorizontalScrollingOffset);

public sealed record TableGridViewportTarget(
    int CurrentRowIndex,
    int CurrentColumnIndex,
    int FirstDisplayedRowIndex,
    int FirstDisplayedColumnIndex,
    int HorizontalScrollingOffset);

public static class TableGridViewport
{
    public static TableGridViewportTarget Resolve(
        TableGridViewportSnapshot? snapshot,
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<TableRow> rows)
    {
        if (columns.Count == 0)
            return new TableGridViewportTarget(-1, -1, -1, -1, 0);

        if (snapshot is null)
            return new TableGridViewportTarget(
                rows.Count > 0 ? 0 : -1,
                0,
                rows.Count > 0 ? 0 : -1,
                0,
                0);

        var firstDisplayedRowIndex = -1;
        var currentRowIndex = -1;
        if (rows.Count > 0)
        {
            firstDisplayedRowIndex = FindRowIndex(rows, snapshot.FirstDisplayedRowHeader);
            if (firstDisplayedRowIndex < 0)
                firstDisplayedRowIndex = Clamp(snapshot.FirstDisplayedRowIndex, 0, rows.Count - 1);

            currentRowIndex = FindRowIndex(rows, snapshot.CurrentRowHeader);
            if (currentRowIndex < 0)
                currentRowIndex = firstDisplayedRowIndex;
        }

        var currentColumnIndex = FindColumnIndex(columns, snapshot.CurrentColumnKey);
        if (currentColumnIndex < 0)
            currentColumnIndex = Clamp(snapshot.CurrentColumnIndex, 0, columns.Count - 1);

        var firstDisplayedColumnIndex = FindColumnIndex(columns, snapshot.FirstDisplayedColumnKey);
        if (firstDisplayedColumnIndex < 0)
            firstDisplayedColumnIndex = Clamp(snapshot.FirstDisplayedColumnIndex, 0, columns.Count - 1);

        return new TableGridViewportTarget(
            currentRowIndex,
            currentColumnIndex,
            firstDisplayedRowIndex,
            firstDisplayedColumnIndex,
            Math.Max(0, snapshot.HorizontalScrollingOffset));
    }

    private static int FindRowIndex(IReadOnlyList<TableRow> rows, string? rowHeader)
    {
        if (string.IsNullOrWhiteSpace(rowHeader))
            return -1;

        for (var index = 0; index < rows.Count; index++)
        {
            if (string.Equals(rows[index].Header, rowHeader, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private static int FindColumnIndex(IReadOnlyList<TableColumn> columns, string? columnKey)
    {
        if (string.IsNullOrWhiteSpace(columnKey))
            return -1;

        for (var index = 0; index < columns.Count; index++)
        {
            if (string.Equals(columns[index].Key, columnKey, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private static int Clamp(int value, int min, int max) =>
        Math.Min(Math.Max(value, min), max);
}
