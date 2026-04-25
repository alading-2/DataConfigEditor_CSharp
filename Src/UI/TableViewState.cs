using DataConfigEditor.Documents;

namespace DataConfigEditor.UI;

public sealed record TableViewState
{
    public static TableViewState Empty { get; } = new();

    public string SearchText { get; init; } = "";

    public IReadOnlyDictionary<string, TableFilter> Filters { get; init; } =
        new Dictionary<string, TableFilter>(StringComparer.OrdinalIgnoreCase);

    public TableSort Sort { get; init; } = new("", TableSortDirection.None);

    public TableViewState WithFilter(TableFilter filter)
    {
        var filters = new Dictionary<string, TableFilter>(Filters, StringComparer.OrdinalIgnoreCase)
        {
            [filter.ColumnKey] = filter,
        };

        return this with { Filters = filters };
    }

    public TableViewState WithoutFilter(string columnKey)
    {
        var filters = new Dictionary<string, TableFilter>(Filters, StringComparer.OrdinalIgnoreCase);
        filters.Remove(columnKey);
        return this with { Filters = filters };
    }

    public TableViewState ClearFilters() =>
        this with { SearchText = "", Filters = new Dictionary<string, TableFilter>(StringComparer.OrdinalIgnoreCase) };

    public TableViewState ToggleSort(string columnKey)
    {
        var nextDirection = string.Equals(Sort.ColumnKey, columnKey, StringComparison.OrdinalIgnoreCase)
            ? Sort.Direction switch
            {
                TableSortDirection.None => TableSortDirection.Ascending,
                TableSortDirection.Ascending => TableSortDirection.Descending,
                _ => TableSortDirection.None,
            }
            : TableSortDirection.Ascending;

        return this with { Sort = new TableSort(columnKey, nextDirection) };
    }

    public TableViewResult Apply(TableDocument document)
    {
        var columnsByKey = document.Columns.ToDictionary(column => column.Key, StringComparer.OrdinalIgnoreCase);
        var rows = document.Rows
            .Where(row => MatchesFilters(row, columnsByKey))
            .Where(row => MatchesSearch(document, row))
            .ToArray();

        return new TableViewResult(
            Rows: TableSorter.Apply(rows, Sort),
            TotalRows: document.Rows.Count,
            ActiveFilterCount: Filters.Count(filter => IsActive(filter.Value)),
            SearchText: SearchText);
    }

    private bool MatchesFilters(TableRow row, IReadOnlyDictionary<string, TableColumn> columnsByKey)
    {
        foreach (var filter in Filters.Values.Where(IsActive))
        {
            var value = TableSorter.GetCellValue(row, filter.ColumnKey);
            var rawValue = TableSorter.GetRawCellValue(row, filter.ColumnKey);
            columnsByKey.TryGetValue(filter.ColumnKey, out var column);
            if (!filter.Matches(column, value, rawValue))
                return false;
        }

        return true;
    }

    private bool MatchesSearch(TableDocument document, TableRow row)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var term = SearchText.Trim();
        if (row.Header.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        if (row.Cells.Any(cell => cell.Value.Contains(term, StringComparison.OrdinalIgnoreCase)))
            return true;

        return document.Columns.Any(column =>
            column.Key.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            column.Header.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            column.Summary.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsActive(TableFilter filter)
    {
        return filter.Mode is TableFilterMode.IsEmpty or TableFilterMode.IsNotEmpty ||
               filter.Values.Count > 0 ||
               !string.IsNullOrWhiteSpace(filter.SecondaryValue) ||
               !string.IsNullOrWhiteSpace(filter.Value);
    }
}

public sealed record TableViewResult(
    IReadOnlyList<TableRow> Rows,
    int TotalRows,
    int ActiveFilterCount,
    string SearchText);
