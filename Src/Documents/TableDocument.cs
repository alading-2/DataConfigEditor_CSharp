namespace DataConfigEditor.Documents;

public sealed class TableDocument
{
    public required string SourceFilePath { get; init; }
    public required string Title { get; init; }
    public IReadOnlyList<TableColumn> Columns { get; init; } = Array.Empty<TableColumn>();
    public IReadOnlyList<TableRow> Rows { get; init; } = Array.Empty<TableRow>();
    public ParseDiagnostic? Diagnostic { get; init; }

    public bool IsTable => Diagnostic is null;

    public static TableDocument Error(string filePath, string title, string message, string details = "")
    {
        return new TableDocument
        {
            SourceFilePath = filePath,
            Title = title,
            Diagnostic = new ParseDiagnostic
            {
                Message = message,
                Details = details,
            },
        };
    }
}
