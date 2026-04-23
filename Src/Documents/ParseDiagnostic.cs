namespace DataConfigEditor.Documents;

public sealed class ParseDiagnostic
{
    public required string Message { get; init; }
    public string Details { get; init; } = "";
}
