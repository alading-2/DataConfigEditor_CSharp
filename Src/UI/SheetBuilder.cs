using DataConfigEditor.Documents;
using DataConfigEditor.Settings;

namespace DataConfigEditor.UI;

/// <summary>
/// 只读表格构建器，使用 DataGridView 承载浏览视图。
/// </summary>
public sealed class SheetBuilder
{
    public void BuildGrid(DataGridView grid, TableDocument document, UiSettings settings)
    {
        grid.SuspendLayout();
        try
        {
            settings = settings.Normalize();
            var layout = TableLayoutOptions.FromSettings(settings);
            grid.Rows.Clear();
            grid.Columns.Clear();
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = layout.HeaderHeight;
            grid.RowTemplate.Height = layout.RowHeight;

            if (!document.IsTable || document.Columns.Count == 0)
                return;

            foreach (var column in document.Columns)
            {
                var gridColumn = new DataGridViewTextBoxColumn
                {
                    Name = column.Key,
                    HeaderText = layout.ShowHeaderSummary && !string.IsNullOrEmpty(column.Summary)
                        ? $"{column.Header}\n{column.Summary}"
                        : column.Header,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    ReadOnly = true,
                    Frozen = layout.FreezeInstanceColumn && column.Key == "__instance",
                    Width = column.Key == "__instance" ? layout.InstanceColumnWidth : layout.DefaultColumnWidth,
                };
                gridColumn.HeaderCell.ToolTipText = BuildColumnTooltip(column);
                grid.Columns.Add(gridColumn);
            }

            foreach (var row in document.Rows)
            {
                var values = new List<string> { row.Header };
                values.AddRange(row.Cells.Select(cell => cell.Value));
                var rowIndex = grid.Rows.Add(values.Cast<object>().ToArray());
                grid.Rows[rowIndex].Height = layout.RowHeight;
                grid.Rows[rowIndex].Cells[0].ToolTipText = row.Header;

                for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
                {
                    var cell = row.Cells[cellIndex];
                    if (!string.IsNullOrWhiteSpace(cell.RawValue) &&
                        !string.Equals(cell.RawValue, cell.Value, StringComparison.Ordinal))
                    {
                        grid.Rows[rowIndex].Cells[cellIndex + 1].ToolTipText = cell.RawValue;
                        continue;
                    }

                    if (cell.IsImplicitDefault)
                    {
                        grid.Rows[rowIndex].Cells[cellIndex + 1].ToolTipText = "未显式赋值，当前显示 CLR 默认值";
                    }
                }
            }

            if (settings.ColumnSizingMode == GridColumnSizingMode.AutoFitDisplayedCells)
                grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
        }
        finally
        {
            grid.ResumeLayout();
        }
    }

    private static string BuildColumnTooltip(TableColumn column)
    {
        var parts = new List<string> { column.Header };

        if (!string.IsNullOrWhiteSpace(column.Summary) && column.Summary != "未注释")
            parts.Add(column.Summary);

        if (!string.IsNullOrWhiteSpace(column.TypeName))
            parts.Add($"类型: {column.TypeName}");

        if (column.IsEnum && column.EnumOptions.Count > 0)
            parts.Add($"枚举项: {string.Join(", ", column.EnumOptions.Select(option => option.Name))}");

        return string.Join(Environment.NewLine, parts);
    }
}
