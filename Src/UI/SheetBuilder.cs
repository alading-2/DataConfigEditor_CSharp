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
            grid.Rows.Clear();
            grid.Columns.Clear();
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            if (!document.IsTable || document.Columns.Count == 0 || document.Rows.Count == 0)
                return;

            foreach (var column in document.Columns)
            {
                var gridColumn = new DataGridViewTextBoxColumn
                {
                    Name = column.Key,
                    HeaderText = string.IsNullOrEmpty(column.Summary)
                        ? column.Header
                        : $"{column.Header}\n{column.Summary}",
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    ReadOnly = true,
                    Frozen = column.Key == "__instance",
                    Width = column.Key == "__instance" ? 140 : settings.FixedColumnWidth,
                };
                grid.Columns.Add(gridColumn);
            }

            foreach (var row in document.Rows)
            {
                var values = new List<string> { row.Header };
                values.AddRange(row.Cells.Select(cell => cell.Value));
                var rowIndex = grid.Rows.Add(values.Cast<object>().ToArray());
                grid.Rows[rowIndex].Height = settings.GridRowHeight;
            }

            if (grid.Rows.Count > 0 && grid.Columns.Count > 0)
            {
                grid.ClearSelection();
                grid.CurrentCell = grid.Rows[0].Cells[0];
            }

            if (settings.ColumnSizingMode == GridColumnSizingMode.AutoFitDisplayedCells)
                grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
        }
        finally
        {
            grid.ResumeLayout();
        }
    }
}
