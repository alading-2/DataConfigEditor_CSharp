using DataConfigEditor.Core;
using DataConfigEditor.Documents;

namespace DataConfigEditor.Parsing;

public sealed class CsTableParser
{
    private readonly ITypeMetadataProvider? _typeMetadataProvider;
    private readonly bool _allowAutoAssemblyDiscovery;

    public CsTableParser(
        ITypeMetadataProvider? typeMetadataProvider = null,
        bool allowAutoAssemblyDiscovery = true)
    {
        _typeMetadataProvider = typeMetadataProvider;
        _allowAutoAssemblyDiscovery = allowAutoAssemblyDiscovery;
    }

    public TableDocument ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return TableDocument.Error(
                filePath,
                Path.GetFileName(filePath),
                "文件不存在");
        }

        var classInfo = SourceParser.ParseClass(filePath);
        if (classInfo is null)
        {
            return TableDocument.Error(
                filePath,
                Path.GetFileName(filePath),
                "当前文件无法转换为表格视图",
                "未找到可解析的类定义。");
        }

        var comments = CsCommentParser.ParseFile(filePath);
        var dataKeyDefaults = DataKeyDefaultValueResolver.TryCreateForSourceFile(filePath);
        var typeMetadataProvider = _typeMetadataProvider;
        if (typeMetadataProvider is null && _allowAutoAssemblyDiscovery)
            typeMetadataProvider = AssemblyTypeMetadataProvider.TryCreateForSourceFile(filePath);
        if (classInfo.Properties.Count == 0 || classInfo.Instances.Count == 0)
        {
            return TableDocument.Error(
                filePath,
                classInfo.ClassName,
                "当前文件无法转换为表格视图",
                "缺少 public 属性或静态实例。");
        }

        var columns = new List<TableColumn>
        {
            new()
            {
                Key = "__instance",
                Header = "实例名",
            },
        };

        columns.AddRange(classInfo.Properties.Select(property =>
        {
            comments.TryGetValue(property.Name, out var comment);
            var typeMetadata = ResolveTypeMetadata(
                property.TypeName,
                classInfo.Namespace,
                classInfo.Usings,
                typeMetadataProvider);
            return new TableColumn
            {
                Key = property.Name,
                Header = property.Name,
                Summary = string.IsNullOrWhiteSpace(comment?.Summary)
                    ? "未注释"
                    : comment.Summary,
                Group = comment?.Group ?? "",
                TypeName = typeMetadata.TypeName,
                TypeFullName = typeMetadata.TypeFullName,
                IsEnum = typeMetadata.IsEnum,
                IsFlags = typeMetadata.IsFlags,
                IsNumeric = typeMetadata.IsNumeric,
                IsBool = typeMetadata.IsBool,
                IsString = typeMetadata.IsString,
                EnumOptions = typeMetadata.EnumOptions.Select(option => new TableEnumOption
                {
                    Name = option.Name,
                    Summary = option.Summary,
                    Value = option.Value,
                }).ToArray(),
            };
        }));

        var columnsByKey = columns.ToDictionary(column => column.Key, StringComparer.OrdinalIgnoreCase);
        var rows = classInfo.Instances.Select(instance =>
        {
            var cells = classInfo.Properties.Select(property =>
            {
                var column = columnsByKey[property.Name];
                var explicitOrDeclaredValue = instance.Values.TryGetValue(property.Name, out var value)
                    ? value
                    : property.DefaultValue;
                var effectiveValue = ResolveEffectiveValue(
                    explicitOrDeclaredValue,
                    column,
                    dataKeyDefaults,
                    typeMetadataProvider);

                return new TableCell
                {
                    ColumnKey = property.Name,
                    RawValue = explicitOrDeclaredValue,
                    Value = effectiveValue.DisplayValue,
                    IsImplicitDefault = effectiveValue.IsImplicitDefault,
                };
            }).ToList();

            return new TableRow
            {
                Header = instance.FieldName,
                Cells = cells,
            };
        }).ToList();

        return new TableDocument
        {
            SourceFilePath = filePath,
            Title = classInfo.ClassName,
            Columns = columns,
            Rows = rows,
        };
    }

    private static TypeMetadata ResolveTypeMetadata(
        string typeSyntax,
        string currentNamespace,
        IReadOnlyList<string> usingNamespaces,
        ITypeMetadataProvider? provider)
    {
        if (provider is not null &&
            provider.TryResolve(typeSyntax, currentNamespace, usingNamespaces, out var metadata))
        {
            return metadata;
        }

        return TypeMetadata.FromTypeSyntax(typeSyntax);
    }

    private static string FormatDisplayValue(string rawValue, TableColumn column)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return "";

        if (column.IsString && TryParseCSharpStringLiteral(rawValue, out var stringValue))
            return stringValue;

        if (!column.IsEnum)
            return rawValue;

        return string.Join(" | ",
            rawValue
                .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(ExtractEnumMemberName));
    }

    private static EffectiveCellValue ResolveEffectiveValue(
        string rawValue,
        TableColumn column,
        DataKeyDefaultValueResolver? dataKeyDefaults,
        ITypeMetadataProvider? typeMetadataProvider)
    {
        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            var displaySource = ResolveDisplaySource(rawValue, column, dataKeyDefaults, typeMetadataProvider);
            return new EffectiveCellValue(FormatDisplayValue(displaySource, column), IsImplicitDefault: false);
        }

        var implicitDefault = InferImplicitDefaultValue(column);
        return new EffectiveCellValue(implicitDefault, !string.IsNullOrWhiteSpace(implicitDefault));
    }

    private static string ResolveDisplaySource(
        string rawValue,
        TableColumn column,
        DataKeyDefaultValueResolver? dataKeyDefaults,
        ITypeMetadataProvider? typeMetadataProvider)
    {
        if (dataKeyDefaults is not null &&
            dataKeyDefaults.TryResolveDefaultExpression(rawValue, out var dataKeyDefault))
        {
            return dataKeyDefault;
        }

        if (!column.IsEnum &&
            typeMetadataProvider is not null &&
            typeMetadataProvider.TryResolveConstantExpression(rawValue, out var constantValue))
        {
            return constantValue;
        }

        return rawValue;
    }

    private static string InferImplicitDefaultValue(TableColumn column)
    {
        if (column.IsBool)
            return "false";

        if (column.IsNumeric)
            return "0";

        if (column.IsEnum)
        {
            var zeroOption = column.EnumOptions.FirstOrDefault(option =>
                string.Equals(option.Value, "0", StringComparison.Ordinal));

            if (zeroOption is not null)
                return zeroOption.Name;
        }

        return "";
    }

    private static string ExtractEnumMemberName(string rawValue)
    {
        var normalized = rawValue.Trim();
        var lastDotIndex = normalized.LastIndexOf('.');
        return lastDotIndex >= 0 && lastDotIndex < normalized.Length - 1
            ? normalized[(lastDotIndex + 1)..].Trim()
            : normalized;
    }

    private static bool TryParseCSharpStringLiteral(string rawValue, out string value)
    {
        value = "";
        var normalized = rawValue.Trim();
        if (normalized.Length < 2 || normalized[0] != '"' || normalized[^1] != '"')
            return false;

        value = normalized[1..^1]
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);
        return true;
    }

    private sealed record EffectiveCellValue(string DisplayValue, bool IsImplicitDefault);
}
