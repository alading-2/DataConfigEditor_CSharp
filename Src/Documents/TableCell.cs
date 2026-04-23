namespace DataConfigEditor.Documents;

public sealed class TableCell
{
    public required string ColumnKey { get; init; }
    public string Value { get; init; } = "";
}
