using System.Globalization;
using DataConfigEditor.Documents;

namespace DataConfigEditor.UI;

public enum TableSortDirection
{
    None,
    Ascending,
    Descending,
}

public sealed record TableSort(string ColumnKey, TableSortDirection Direction);

public static class TableSorter
{
    public static IReadOnlyList<TableRow> Apply(
        IReadOnlyList<TableRow> rows,
        TableSort sort)
    {
        if (sort.Direction == TableSortDirection.None || string.IsNullOrWhiteSpace(sort.ColumnKey))
            return rows;

        var sorted = rows
            .Select((row, index) => new IndexedRow(row, index))
            .OrderBy(item => GetSortKey(GetCellValue(item.Row, sort.ColumnKey)), CellSortKeyComparer.Instance)
            .ThenBy(item => item.Index)
            .Select(item => item.Row);

        if (sort.Direction == TableSortDirection.Descending)
            sorted = sorted.Reverse();

        return sorted.ToArray();
    }

    internal static string GetCellValue(TableRow row, string columnKey)
    {
        if (columnKey == "__instance")
            return row.Header;

        return row.Cells.FirstOrDefault(cell => cell.ColumnKey == columnKey)?.Value ?? "";
    }

    private static CellSortKey GetSortKey(string value)
    {
        var normalized = value.Trim().Trim('"');
        if (normalized.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^1];

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? new CellSortKey(number, value)
            : new CellSortKey(null, value);
    }

    private sealed record IndexedRow(TableRow Row, int Index);

    private sealed record CellSortKey(double? Number, string Text);

    private sealed class CellSortKeyComparer : IComparer<CellSortKey>
    {
        public static readonly CellSortKeyComparer Instance = new();

        public int Compare(CellSortKey? x, CellSortKey? y)
        {
            if (x is null && y is null)
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;

            if (x.Number.HasValue && y.Number.HasValue)
                return x.Number.Value.CompareTo(y.Number.Value);

            if (x.Number.HasValue)
                return -1;

            if (y.Number.HasValue)
                return 1;

            return string.Compare(x.Text, y.Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
