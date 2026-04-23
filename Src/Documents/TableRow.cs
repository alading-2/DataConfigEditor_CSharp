namespace DataConfigEditor.Documents;

public sealed class TableRow
{
    public required string Header { get; init; }
    public IReadOnlyList<TableCell> Cells { get; init; } = Array.Empty<TableCell>();
}
