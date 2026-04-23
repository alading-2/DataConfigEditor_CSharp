namespace DataConfigEditor.UI;

public enum TableFilterMode
{
    Contains,
    Equals,
    IsEmpty,
    IsNotEmpty,
}

public sealed record TableFilter(string ColumnKey, TableFilterMode Mode, string Value)
{
    public bool Matches(string cellValue)
    {
        return Mode switch
        {
            TableFilterMode.Contains => cellValue.Contains(Value, StringComparison.OrdinalIgnoreCase),
            TableFilterMode.Equals => string.Equals(cellValue, Value, StringComparison.OrdinalIgnoreCase),
            TableFilterMode.IsEmpty => string.IsNullOrWhiteSpace(cellValue),
            TableFilterMode.IsNotEmpty => !string.IsNullOrWhiteSpace(cellValue),
            _ => true,
        };
    }
}
