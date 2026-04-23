namespace DataConfigEditor.Documents;

public sealed class TableColumn
{
    public required string Key { get; init; }
    public required string Header { get; init; }
    public string Summary { get; init; } = "";
}
