using DataConfigEditor.Documents;
using DataConfigEditor.UI;

namespace DataConfigEditor.Tests.UI;

public class TableViewStateTests
{
    [Fact]
    public void Apply_ContainsFilter_ReturnsMatchingRows()
    {
        var document = CreateDocument();
        var state = TableViewState.Empty.WithFilter(
            new TableFilter("Name", TableFilterMode.Contains, "冲"));

        var result = state.Apply(document);

        Assert.Equal(3, result.TotalRows);
        Assert.Single(result.Rows);
        Assert.Equal("Dash", result.Rows[0].Header);
        Assert.Equal(1, result.ActiveFilterCount);
    }

    [Fact]
    public void Apply_EmptyFilter_ReturnsRowsWithEmptyCells()
    {
        var document = CreateDocument();
        var state = TableViewState.Empty.WithFilter(
            new TableFilter("Tags", TableFilterMode.IsEmpty, ""));

        var result = state.Apply(document);

        Assert.Equal(["Slam"], result.Rows.Select(row => row.Header));
    }

    [Fact]
    public void Apply_MultipleFilters_AreCombined()
    {
        var document = CreateDocument();
        var state = TableViewState.Empty
            .WithFilter(new TableFilter("Trigger", TableFilterMode.Equals, "Auto"))
            .WithFilter(new TableFilter("Tags", TableFilterMode.IsNotEmpty, ""));

        var result = state.Apply(document);

        Assert.Equal(["Blink"], result.Rows.Select(row => row.Header));
    }

    [Fact]
    public void Apply_SearchText_MatchesInstanceCellsAndColumnSummary()
    {
        var document = CreateDocument();

        Assert.Equal(["Dash"], (TableViewState.Empty with { SearchText = "Dash" }).Apply(document).Rows.Select(row => row.Header));
        Assert.Equal(["Slam"], (TableViewState.Empty with { SearchText = "裂地" }).Apply(document).Rows.Select(row => row.Header));
        Assert.Equal(3, (TableViewState.Empty with { SearchText = "触发模式" }).Apply(document).Rows.Count);
    }

    [Fact]
    public void Apply_SortByNumericText_OrdersRowsWithoutChangingDocument()
    {
        var document = CreateDocument();
        var state = TableViewState.Empty with
        {
            Sort = new TableSort("Cooldown", TableSortDirection.Ascending),
        };

        var result = state.Apply(document);

        Assert.Equal(["Blink", "Dash", "Slam"], result.Rows.Select(row => row.Header));
        Assert.Equal(["Dash", "Slam", "Blink"], document.Rows.Select(row => row.Header));
    }

    [Fact]
    public void Apply_SortNone_RestoresSourceOrder()
    {
        var document = CreateDocument();
        var state = TableViewState.Empty with
        {
            Sort = new TableSort("Cooldown", TableSortDirection.None),
        };

        var result = state.Apply(document);

        Assert.Equal(["Dash", "Slam", "Blink"], result.Rows.Select(row => row.Header));
    }

    private static TableDocument CreateDocument()
    {
        return new TableDocument
        {
            SourceFilePath = "AbilityConfigData.cs",
            Title = "AbilityConfigData",
            Columns =
            [
                new TableColumn { Key = "__instance", Header = "实例名" },
                new TableColumn { Key = "Name", Header = "Name", Summary = "名称" },
                new TableColumn { Key = "Trigger", Header = "Trigger", Summary = "触发模式" },
                new TableColumn { Key = "Cooldown", Header = "Cooldown", Summary = "冷却" },
                new TableColumn { Key = "Tags", Header = "Tags", Summary = "标签" },
            ],
            Rows =
            [
                CreateRow("Dash", ("Name", "冲刺"), ("Trigger", "Manual"), ("Cooldown", "1.0f"), ("Tags", "Move")),
                CreateRow("Slam", ("Name", "裂地"), ("Trigger", "Auto"), ("Cooldown", "3.5f"), ("Tags", "")),
                CreateRow("Blink", ("Name", "闪现"), ("Trigger", "Auto"), ("Cooldown", "0.25f"), ("Tags", "Move")),
            ],
        };
    }

    private static TableRow CreateRow(string header, params (string ColumnKey, string Value)[] cells)
    {
        return new TableRow
        {
            Header = header,
            Cells = cells.Select(cell => new TableCell
            {
                ColumnKey = cell.ColumnKey,
                Value = cell.Value,
            }).ToArray(),
        };
    }
}
