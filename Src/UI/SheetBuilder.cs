using DataConfigEditor.Documents;
using unvell.ReoGrid;

namespace DataConfigEditor.UI;

/// <summary>
/// ReoGrid 工作表构建器，仅负责渲染表格文档。
/// </summary>
public sealed class SheetBuilder
{
    private readonly WorksheetRangeStyle _headerStyle = new()
    {
        Flag = PlainStyleFlag.BackColor | PlainStyleFlag.TextColor
             | PlainStyleFlag.FontName | PlainStyleFlag.FontSize | PlainStyleFlag.FontStyleBold,
        BackColor = Color.FromArgb(45, 45, 48),
        TextColor = Color.White,
        FontName = "Microsoft YaHei UI",
        FontSize = 9.5f,
        Bold = true,
    };

    private readonly WorksheetRangeStyle _dataStyle = new()
    {
        Flag = PlainStyleFlag.FontName | PlainStyleFlag.FontSize,
        FontName = "Microsoft YaHei UI",
        FontSize = 9f,
    };

    private readonly WorksheetRangeStyle _instanceNameStyle = new()
    {
        Flag = PlainStyleFlag.BackColor | PlainStyleFlag.FontName | PlainStyleFlag.FontSize | PlainStyleFlag.FontStyleBold,
        BackColor = Color.FromArgb(240, 240, 240),
        FontName = "Microsoft YaHei UI",
        FontSize = 9f,
        Bold = true,
    };

    public void BuildSheet(Worksheet sheet, TableDocument document)
    {
        sheet.Reset();

        if (!document.IsTable || document.Columns.Count == 0 || document.Rows.Count == 0)
        {
            sheet[0, 0] = document.Diagnostic?.Message ?? "无数据";
            return;
        }

        sheet.Columns = document.Columns.Count;
        sheet.Rows = document.Rows.Count + 1;

        for (int columnIndex = 0; columnIndex < document.Columns.Count; columnIndex++)
        {
            var column = document.Columns[columnIndex];
            sheet.SetColumnsWidth(columnIndex, 1, (ushort)(columnIndex == 0 ? 120 : 160));
            sheet[0, columnIndex] = string.IsNullOrEmpty(column.Summary)
                ? column.Header
                : $"{column.Header}\n{column.Summary}";
            sheet.SetRangeStyles(0, columnIndex, 1, 1, _headerStyle);
        }

        for (int rowIndex = 0; rowIndex < document.Rows.Count; rowIndex++)
        {
            var row = document.Rows[rowIndex];
            var gridRowIndex = rowIndex + 1;

            sheet[gridRowIndex, 0] = row.Header;
            sheet.SetRangeStyles(gridRowIndex, 0, 1, 1, _instanceNameStyle);

            for (int cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
            {
                var gridColumnIndex = cellIndex + 1;
                sheet[gridRowIndex, gridColumnIndex] = row.Cells[cellIndex].Value;
                sheet.SetRangeStyles(gridRowIndex, gridColumnIndex, 1, 1, _dataStyle);
            }
        }

        sheet.SetRowsHeight(0, 1, 40);
        if (document.Rows.Count > 0)
            sheet.SetRowsHeight(1, (ushort)document.Rows.Count, 26);

        sheet.FreezeToCell(1, 1);
        sheet.Name = document.Title;
    }
}
