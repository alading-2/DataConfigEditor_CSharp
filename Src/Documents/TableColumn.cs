namespace DataConfigEditor.Documents;

public sealed class TableColumn
{
    public required string Key { get; init; }
    public required string Header { get; init; }
    public string Summary { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string TypeFullName { get; init; } = "";
    public string Group { get; init; } = "";
    public bool IsEnum { get; init; }
    public bool IsFlags { get; init; }
    public bool IsNumeric { get; init; }
    public bool IsBool { get; init; }
    public bool IsString { get; init; }
    public IReadOnlyList<TableEnumOption> EnumOptions { get; init; } = Array.Empty<TableEnumOption>();
}

public sealed class TableEnumOption
{
    public required string Name { get; init; }
    public string Summary { get; init; } = "";
    public string Value { get; init; } = "";
}
