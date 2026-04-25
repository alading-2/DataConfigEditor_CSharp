using System.Globalization;
using DataConfigEditor.Documents;

namespace DataConfigEditor.UI;

public enum TableFilterMode
{
    Contains,
    Equals,
    NotEquals,
    IsEmpty,
    IsNotEmpty,
    InSet,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,
}

public sealed record TableFilter
{
    public TableFilter(
        string columnKey,
        TableFilterMode mode,
        string value = "",
        string secondaryValue = "",
        IReadOnlyList<string>? values = null)
    {
        ColumnKey = columnKey;
        Mode = mode;
        Value = value;
        SecondaryValue = secondaryValue;
        Values = values ?? Array.Empty<string>();
    }

    public string ColumnKey { get; init; }
    public TableFilterMode Mode { get; init; }
    public string Value { get; init; }
    public string SecondaryValue { get; init; }
    public IReadOnlyList<string> Values { get; init; }

    public bool Matches(TableColumn? column, string cellValue, string rawValue)
    {
        var comparisonValue = string.IsNullOrWhiteSpace(rawValue) ? cellValue : rawValue;

        return Mode switch
        {
            TableFilterMode.Contains => cellValue.Contains(Value, StringComparison.OrdinalIgnoreCase),
            TableFilterMode.Equals => MatchesEquals(column, cellValue, comparisonValue, Value),
            TableFilterMode.NotEquals => !MatchesEquals(column, cellValue, comparisonValue, Value),
            TableFilterMode.IsEmpty => string.IsNullOrWhiteSpace(cellValue),
            TableFilterMode.IsNotEmpty => !string.IsNullOrWhiteSpace(cellValue),
            TableFilterMode.InSet => MatchesInSet(column, cellValue, comparisonValue),
            TableFilterMode.GreaterThan => CompareNumber(comparisonValue, Value) > 0,
            TableFilterMode.GreaterThanOrEqual => CompareNumber(comparisonValue, Value) >= 0,
            TableFilterMode.LessThan => CompareNumber(comparisonValue, Value) < 0,
            TableFilterMode.LessThanOrEqual => CompareNumber(comparisonValue, Value) <= 0,
            TableFilterMode.Between => CompareNumber(comparisonValue, Value) >= 0 &&
                                       CompareNumber(comparisonValue, SecondaryValue) <= 0,
            _ => true,
        };
    }

    private bool MatchesEquals(TableColumn? column, string cellValue, string comparisonValue, string expected)
    {
        if (column?.IsEnum == true)
        {
            return string.Equals(NormalizeEnumValue(comparisonValue), NormalizeEnumValue(expected), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(cellValue, expected, StringComparison.OrdinalIgnoreCase);
        }

        if (column?.IsNumeric == true && TryParseNumber(comparisonValue, out var left) && TryParseNumber(expected, out var right))
            return left == right;

        return string.Equals(cellValue, expected, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesInSet(TableColumn? column, string cellValue, string comparisonValue)
    {
        var selectedValues = Values.Count > 0
            ? Values
            : string.IsNullOrWhiteSpace(Value) ? Array.Empty<string>() : [Value];

        if (selectedValues.Count == 0)
            return true;

        if (column?.IsEnum == true)
        {
            var normalized = NormalizeEnumValue(comparisonValue);
            return selectedValues.Any(selected =>
                string.Equals(normalized, NormalizeEnumValue(selected), StringComparison.OrdinalIgnoreCase));
        }

        return selectedValues.Any(selected =>
            string.Equals(cellValue, selected, StringComparison.OrdinalIgnoreCase));
    }

    private static int CompareNumber(string actual, string expected)
    {
        if (!TryParseNumber(actual, out var left) || !TryParseNumber(expected, out var right))
            return int.MinValue;

        return left.CompareTo(right);
    }

    private static bool TryParseNumber(string value, out double number)
    {
        var normalized = value.Trim().Trim('"');
        if (normalized.EndsWith("f", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("d", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^1];
        }

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    private static string NormalizeEnumValue(string value)
    {
        var normalized = value.Trim();
        var lastDotIndex = normalized.LastIndexOf('.');
        return lastDotIndex >= 0 && lastDotIndex < normalized.Length - 1
            ? normalized[(lastDotIndex + 1)..].Trim()
            : normalized;
    }
}

public static class TableFilterCapabilities
{
    public static IReadOnlyList<TableFilterMode> GetSupportedModes(TableColumn column)
    {
        if (column.IsEnum)
        {
            return
            [
                TableFilterMode.InSet,
                TableFilterMode.IsEmpty,
                TableFilterMode.IsNotEmpty,
            ];
        }

        if (column.IsBool)
        {
            return
            [
                TableFilterMode.Equals,
                TableFilterMode.NotEquals,
                TableFilterMode.IsEmpty,
                TableFilterMode.IsNotEmpty,
            ];
        }

        if (column.IsNumeric)
        {
            return
            [
                TableFilterMode.Equals,
                TableFilterMode.NotEquals,
                TableFilterMode.GreaterThan,
                TableFilterMode.GreaterThanOrEqual,
                TableFilterMode.LessThan,
                TableFilterMode.LessThanOrEqual,
                TableFilterMode.Between,
                TableFilterMode.IsEmpty,
                TableFilterMode.IsNotEmpty,
            ];
        }

        return
        [
            TableFilterMode.Contains,
            TableFilterMode.Equals,
            TableFilterMode.NotEquals,
            TableFilterMode.IsEmpty,
            TableFilterMode.IsNotEmpty,
        ];
    }
}
