namespace DataConfigEditor.Core;

public sealed class TypeMetadata
{
    public required string TypeName { get; init; }
    public string TypeFullName { get; init; } = "";
    public bool IsEnum { get; init; }
    public bool IsFlags { get; init; }
    public bool IsNumeric { get; init; }
    public bool IsBool { get; init; }
    public bool IsString { get; init; }
    public IReadOnlyList<EnumOptionMetadata> EnumOptions { get; init; } = Array.Empty<EnumOptionMetadata>();

    public static TypeMetadata FromTypeSyntax(string typeSyntax)
    {
        var normalized = NormalizeTypeSyntax(typeSyntax);
        return new TypeMetadata
        {
            TypeName = normalized,
            TypeFullName = normalized,
            IsNumeric = normalized is "int" or "float" or "double" or "decimal" or "long" or "short",
            IsBool = normalized == "bool",
            IsString = normalized == "string",
        };
    }

    public static string NormalizeTypeSyntax(string typeSyntax)
    {
        var normalized = typeSyntax.Trim();
        if (normalized.EndsWith("?", StringComparison.Ordinal))
            normalized = normalized[..^1];

        var genericTickIndex = normalized.IndexOf('<');
        if (genericTickIndex >= 0)
            normalized = normalized[..genericTickIndex];

        if (normalized.EndsWith("[]", StringComparison.Ordinal))
            normalized = normalized[..^2];

        return normalized.Trim();
    }
}

public sealed class EnumOptionMetadata
{
    public required string Name { get; init; }
    public string Summary { get; init; } = "";
    public string Value { get; init; } = "";
}
