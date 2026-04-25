#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Slime.Addons.DataConfigEditor
{
    /// <summary>
    /// .cs 文件保存器。
    /// 只写回对象初始化器顶层的简单属性，避免误改嵌套对象或复杂表达式。
    /// </summary>
    public static partial class CsFileWriter
    {
        public sealed class WriteAllChangesResult
        {
            public string FilePath { get; init; } = "";
            public bool FileExists { get; set; }
            public int InstancesTotal { get; set; }
            public int InstancesWithoutFieldInfo { get; set; }
            public int InitializersFound { get; set; }
            public int InitializersMissing { get; set; }
            public int RowsChanged { get; set; }
            public int PropertiesVisited { get; set; }
            public int PropertiesUnsupported { get; set; }
            public int PropertiesReplaced { get; set; }
            public int PropertiesInserted { get; set; }
            public int PropertiesUnchanged { get; set; }
            public int PropertiesMissingDefaultSkipped { get; set; }
            public int PropertiesComplexSkipped { get; set; }
            public List<string> Messages { get; } = new();

            public int PropertiesWritten => PropertiesReplaced + PropertiesInserted;

            public string ToSummary()
            {
                return $"file={Path.GetFileName(FilePath)}, exists={FileExists}, instances={InstancesTotal}, "
                    + $"initializers={InitializersFound}, missingInitializers={InitializersMissing}, "
                    + $"rowsChanged={RowsChanged}, propsVisited={PropertiesVisited}, propsWritten={PropertiesWritten}, "
                    + $"unchanged={PropertiesUnchanged}, skipDefaultMissing={PropertiesMissingDefaultSkipped}, "
                    + $"skipComplex={PropertiesComplexSkipped}, skipUnsupported={PropertiesUnsupported}";
            }
        }

        private enum PropertyWriteStatus
        {
            Replaced,
            Inserted,
            Unchanged,
            MissingDefaultSkipped,
            ComplexExpressionSkipped,
            InsertFailed,
        }

        /// <summary>
        /// 匹配静态字段初始化器，支持 new() 和 new Type() 语法
        /// 使用平衡组匹配嵌套大括号
        /// </summary>
        [GeneratedRegex(
            @"public\s+static\s+readonly\s+[\w<>,\.\s]+\s+(\w+)\s*=\s*new(?:\s+[\w<>,\.\s]+)?\s*(?:\([^;{}]*\))?\s*\{",
            RegexOptions.Compiled)]
        private static partial Regex StaticInitializerStartRegex();

        /// <summary>
        /// 将所有实例的所有属性变更合并写入 .cs 文件
        /// </summary>
        public static int WriteAllChanges(
            string filePath,
            List<ConfigReflectionCache.InstanceInfo> instances,
            List<PropertyMetadata> properties,
            Dictionary<string, PropertyCommentInfo> comments)
        {
            return WriteAllChangesWithDiagnostics(filePath, instances, properties, comments).RowsChanged;
        }

        /// <summary>
        /// 写回 .cs 文件，并返回用于 Godot Output 排查的详细诊断。
        /// </summary>
        public static WriteAllChangesResult WriteAllChangesWithDiagnostics(
            string filePath,
            List<ConfigReflectionCache.InstanceInfo> instances,
            List<PropertyMetadata> properties,
            Dictionary<string, PropertyCommentInfo> comments,
            bool verbose = false)
        {
            var result = new WriteAllChangesResult { FilePath = filePath };
            if (!File.Exists(filePath))
            {
                result.FileExists = false;
                result.Messages.Add($"源码文件不存在: {filePath}");
                return result;
            }

            result.FileExists = true;
            string source = File.ReadAllText(filePath);

            foreach (var inst in instances)
            {
                result.InstancesTotal++;
                if (inst.FieldInfo == null)
                {
                    result.InstancesWithoutFieldInfo++;
                    if (verbose)
                        result.Messages.Add($"跳过实例 {inst.Name}: 没有 FieldInfo，通常是默认值预览行");
                    continue;
                }

                var range = FindStaticInitializerRange(source, inst.Name);
                if (range == null)
                {
                    result.InitializersMissing++;
                    result.Messages.Add($"找不到静态初始化器: {inst.Name}，需要形如 public static readonly XxxData {inst.Name} = new() {{ ... }}");
                    continue;
                }

                result.InitializersFound++;
                string body = source.Substring(range.Value.Start, range.Value.Length);
                string newBody = body;
                object? defaultInstance = CreateDefaultInstance(inst.Instance);

                foreach (var prop in properties)
                {
                    result.PropertiesVisited++;
                    object? val = prop.PropertyInfo.GetValue(inst.Instance);
                    if (!TryFormatValueForCs(val, prop.PropertyType, out string csValue))
                    {
                        result.PropertiesUnsupported++;
                        if (verbose)
                            result.Messages.Add($"跳过 {inst.Name}.{prop.Name}: 字段类型 {prop.PropertyType.Name} 暂不支持写成简单 C# 表达式");
                        continue;
                    }

                    object? defaultValue = defaultInstance != null
                        ? prop.PropertyInfo.GetValue(defaultInstance)
                        : null;
                    bool shouldInsertMissing = defaultInstance == null || !ValuesEqual(val, defaultValue);
                    string oldBody = newBody;
                    newBody = ReplacePropertyInBody(
                        newBody,
                        prop.Name,
                        csValue,
                        prop.PropertyType,
                        shouldInsertMissing,
                        out var status,
                        out string? oldExpression);

                    RecordPropertyStatus(result, inst.Name, prop.Name, csValue, oldExpression, status, verbose);
                    if (oldBody == newBody && status is PropertyWriteStatus.Replaced or PropertyWriteStatus.Inserted)
                        result.Messages.Add($"警告 {inst.Name}.{prop.Name}: 标记为写入但源码片段没有变化，请检查保存器逻辑");
                }

                if (newBody != body)
                {
                    source = source.Remove(range.Value.Start, range.Value.Length)
                                   .Insert(range.Value.Start, newBody);
                    result.RowsChanged++;
                }
            }

            if (result.RowsChanged > 0)
                File.WriteAllText(filePath, source);

            return result;
        }

        /// <summary>
        /// 只写回一个实例的一个属性，用于表格单元格实时保存。
        /// </summary>
        public static WriteAllChangesResult WriteSingleChangeWithDiagnostics(
            string filePath,
            ConfigReflectionCache.InstanceInfo instance,
            PropertyMetadata property,
            string? editorValueText = null,
            bool verbose = false)
        {
            var result = new WriteAllChangesResult { FilePath = filePath, InstancesTotal = 1 };
            if (!File.Exists(filePath))
            {
                result.FileExists = false;
                result.Messages.Add($"源码文件不存在: {filePath}");
                return result;
            }

            result.FileExists = true;
            if (instance.FieldInfo == null)
            {
                result.InstancesWithoutFieldInfo++;
                result.Messages.Add($"跳过实例 {instance.Name}: 没有 FieldInfo，无法定位源码静态字段");
                return result;
            }

            string source = File.ReadAllText(filePath);
            var range = FindStaticInitializerRange(source, instance.Name);
            if (range == null)
            {
                result.InitializersMissing++;
                result.Messages.Add($"找不到静态初始化器: {instance.Name}，需要形如 public static readonly XxxData {instance.Name} = new() {{ ... }}");
                return result;
            }

            result.InitializersFound++;
            result.PropertiesVisited = 1;

            object? val = property.PropertyInfo.GetValue(instance.Instance);
            if (!TryFormatEditedValueForCs(val, property.PropertyType, editorValueText, out string csValue))
            {
                result.PropertiesUnsupported++;
                result.Messages.Add($"跳过 {instance.Name}.{property.Name}: 字段类型 {property.PropertyType.Name} 暂不支持写成简单 C# 表达式");
                return result;
            }

            object? defaultInstance = CreateDefaultInstance(instance.Instance);
            object? defaultValue = defaultInstance != null
                ? property.PropertyInfo.GetValue(defaultInstance)
                : null;
            bool shouldInsertMissing = defaultInstance == null || !ValuesEqual(val, defaultValue);

            string body = source.Substring(range.Value.Start, range.Value.Length);
            string newBody = ReplacePropertyInBody(
                body,
                property.Name,
                csValue,
                property.PropertyType,
                shouldInsertMissing,
                out var status,
                out string? oldExpression);

            RecordPropertyStatus(result, instance.Name, property.Name, csValue, oldExpression, status, verbose);
            if (newBody == body)
                return result;

            source = source.Remove(range.Value.Start, range.Value.Length)
                           .Insert(range.Value.Start, newBody);
            result.RowsChanged = 1;
            File.WriteAllText(filePath, source);
            return result;
        }

        /// <summary>
        /// 找到静态字段初始化器的 { } 范围（支持嵌套）
        /// </summary>
        private static (int Start, int Length)? FindStaticInitializerRange(string source, string fieldName)
        {
            // 匹配开始: public static readonly Type FieldName = new(...) {
            var startMatch = StaticInitializerStartRegex().Match(source);
            while (startMatch.Success)
            {
                if (startMatch.Groups[1].Value != fieldName)
                {
                    startMatch = startMatch.NextMatch();
                    continue;
                }

                int braceStart = source.IndexOf('{', startMatch.Index + startMatch.Length - 1);
                if (braceStart < 0) return null;

                int braceEnd = FindMatchingBrace(source, braceStart);
                if (braceEnd < 0) return null;

                // 包含 { 和 }
                int bodyStart = braceStart;
                int bodyLength = braceEnd - braceStart + 1;
                return (bodyStart, bodyLength);
            }
            return null;
        }

        /// <summary>
        /// 找到匹配的右大括号（支持嵌套）
        /// </summary>
        private static int FindMatchingBrace(string source, int openBraceIdx)
        {
            int depth = 0;
            var scanState = ScanState.Code;

            for (int i = openBraceIdx; i < source.Length; i++)
            {
                if (UpdateScanState(source, ref i, ref scanState))
                    continue;
                if (scanState != ScanState.Code)
                    continue;

                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 替换初始化器体中的属性值
        /// </summary>
        private static object? CreateDefaultInstance(object instance)
        {
            try
            {
                return Activator.CreateInstance(instance.GetType());
            }
            catch
            {
                return null;
            }
        }

        private static bool ValuesEqual(object? left, object? right)
        {
            if (left == null || right == null)
                return left == right;
            return left.Equals(right);
        }

        private static string ReplacePropertyInBody(
            string body,
            string propName,
            string newValue,
            Type propertyType,
            bool shouldInsertMissing,
            out PropertyWriteStatus status,
            out string? oldExpression)
        {
            var assignment = FindTopLevelAssignment(body, propName);
            if (assignment != null)
            {
                oldExpression = body.Substring(assignment.Value.ValueStart, assignment.Value.ValueLength).Trim();
                if (oldExpression == newValue)
                {
                    status = PropertyWriteStatus.Unchanged;
                    return body;
                }

                if (!IsSimpleWritableExpression(oldExpression, propertyType))
                {
                    status = PropertyWriteStatus.ComplexExpressionSkipped;
                    return body;
                }

                status = PropertyWriteStatus.Replaced;
                return body.Remove(assignment.Value.ValueStart, assignment.Value.ValueLength)
                    .Insert(assignment.Value.ValueStart, newValue);
            }

            oldExpression = null;
            if (!shouldInsertMissing)
            {
                status = PropertyWriteStatus.MissingDefaultSkipped;
                return body;
            }

            string inserted = InsertPropertyBeforeClosingBrace(body, propName, newValue);
            status = inserted == body ? PropertyWriteStatus.InsertFailed : PropertyWriteStatus.Inserted;
            return inserted;
        }

        private static void RecordPropertyStatus(
            WriteAllChangesResult result,
            string instanceName,
            string propName,
            string csValue,
            string? oldExpression,
            PropertyWriteStatus status,
            bool verbose)
        {
            switch (status)
            {
                case PropertyWriteStatus.Replaced:
                    result.PropertiesReplaced++;
                    result.Messages.Add($"写入 {instanceName}.{propName}: {oldExpression} -> {csValue}");
                    break;

                case PropertyWriteStatus.Inserted:
                    result.PropertiesInserted++;
                    result.Messages.Add($"补写 {instanceName}.{propName}: {csValue}");
                    break;

                case PropertyWriteStatus.Unchanged:
                    result.PropertiesUnchanged++;
                    if (verbose)
                        result.Messages.Add($"不变 {instanceName}.{propName}: 源码已经是 {csValue}");
                    break;

                case PropertyWriteStatus.MissingDefaultSkipped:
                    result.PropertiesMissingDefaultSkipped++;
                    if (verbose)
                        result.Messages.Add($"跳过 {instanceName}.{propName}: 源码缺少该字段，当前值等于默认值，不补写");
                    break;

                case PropertyWriteStatus.ComplexExpressionSkipped:
                    result.PropertiesComplexSkipped++;
                    result.Messages.Add($"跳过 {instanceName}.{propName}: 源码现有表达式 `{oldExpression}` 不是简单字面量，避免覆盖常量/方法/nameof 等表达式");
                    break;

                case PropertyWriteStatus.InsertFailed:
                    result.Messages.Add($"失败 {instanceName}.{propName}: 找不到对象初始化器右大括号，无法补写 {csValue}");
                    break;
            }
        }

        /// <summary>
        /// 将运行时值格式化为 C# 源码表达式
        /// </summary>
        private static bool TryFormatValueForCs(object? value, Type type, out string csValue)
        {
            Type actualType = Nullable.GetUnderlyingType(type) ?? type;

            if (value == null)
            {
                csValue = type == typeof(string) || Nullable.GetUnderlyingType(type) != null
                    ? "null"
                    : "default";
                return true;
            }

            if (actualType == typeof(string))
            {
                csValue = ToCSharpStringLiteral((string)value);
                return true;
            }

            if (actualType == typeof(bool))
            {
                csValue = (bool)value ? "true" : "false";
                return true;
            }

            if (actualType == typeof(int))
            {
                csValue = ((int)value).ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (actualType == typeof(float))
            {
                csValue = FormatFloat((float)value);
                return true;
            }

            if (actualType == typeof(double))
            {
                csValue = FormatDouble((double)value);
                return true;
            }

            if (actualType.IsEnum)
            {
                csValue = FormatEnumValue(value, actualType);
                return true;
            }

            csValue = "";
            return false;
        }

        private static bool TryFormatEditedValueForCs(object? value, Type type, string? editorValueText, out string csValue)
        {
            Type actualType = Nullable.GetUnderlyingType(type) ?? type;
            string? trimmedText = editorValueText?.Trim();

            if (!string.IsNullOrWhiteSpace(trimmedText))
            {
                if (actualType == typeof(int)
                    && int.TryParse(trimmedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                {
                    csValue = intValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                if (actualType == typeof(float)
                    && float.TryParse(trimmedText, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    csValue = NormalizeEditorNumberLiteral(trimmedText, "f");
                    return true;
                }

                if (actualType == typeof(double)
                    && double.TryParse(trimmedText, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    csValue = NormalizeEditorNumberLiteral(trimmedText, "d");
                    return true;
                }
            }

            return TryFormatValueForCs(value, type, out csValue);
        }

        private static string NormalizeEditorNumberLiteral(string text, string suffix)
        {
            string normalized = text.Trim();
            if (normalized.EndsWith("f", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith("d", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^1];
            }

            if (!normalized.Contains('.', StringComparison.Ordinal)
                && !normalized.Contains('E', StringComparison.OrdinalIgnoreCase))
            {
                normalized += ".0";
            }

            return normalized + suffix;
        }

        private static (int ValueStart, int ValueLength)? FindTopLevelAssignment(string body, string propName)
        {
            var scanState = ScanState.Code;
            int depth = 0;

            for (int i = 0; i < body.Length; i++)
            {
                if (UpdateScanState(body, ref i, ref scanState))
                    continue;
                if (scanState != ScanState.Code)
                    continue;

                char ch = body[i];
                if (ch == '{' || ch == '[' || ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == '}' || ch == ']' || ch == ')')
                {
                    depth--;
                    continue;
                }

                if (depth != 1 || !IsIdentifierStart(ch))
                    continue;

                int nameStart = i;
                int nameEnd = i + 1;
                while (nameEnd < body.Length && IsIdentifierPart(body[nameEnd]))
                    nameEnd++;

                string name = body.Substring(nameStart, nameEnd - nameStart);
                if (name != propName)
                {
                    i = nameEnd - 1;
                    continue;
                }

                int equalsIndex = SkipWhitespace(body, nameEnd);
                if (equalsIndex >= body.Length || body[equalsIndex] != '=')
                    continue;

                int valueStart = SkipWhitespace(body, equalsIndex + 1);
                int valueEnd = FindTopLevelValueEnd(body, valueStart);
                while (valueEnd > valueStart && char.IsWhiteSpace(body[valueEnd - 1]))
                    valueEnd--;

                return (valueStart, valueEnd - valueStart);
            }

            return null;
        }

        private static int FindTopLevelValueEnd(string body, int valueStart)
        {
            var scanState = ScanState.Code;
            int depth = 1;

            for (int i = valueStart; i < body.Length; i++)
            {
                if (UpdateScanState(body, ref i, ref scanState))
                    continue;
                if (scanState != ScanState.Code)
                    continue;

                char ch = body[i];
                if (ch == '{' || ch == '[' || ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == '}' || ch == ']' || ch == ')')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                    continue;
                }

                if (ch == ',' && depth == 1)
                    return i;
            }

            return body.Length;
        }

        private static string InsertPropertyBeforeClosingBrace(string body, string propName, string newValue)
        {
            int closeBrace = body.LastIndexOf('}');
            if (closeBrace < 0)
                return body;

            string newline = body.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            string closeIndent = GetLineIndent(body, closeBrace);
            string propIndent = FindFirstTopLevelPropertyIndent(body) ?? closeIndent + "    ";

            int lastContent = closeBrace - 1;
            while (lastContent >= 0 && char.IsWhiteSpace(body[lastContent]))
                lastContent--;

            var builder = new StringBuilder(body);
            if (lastContent > 0 && body[lastContent] != '{' && body[lastContent] != ',')
            {
                builder.Insert(lastContent + 1, ",");
                if (lastContent + 1 <= closeBrace)
                    closeBrace++;
            }

            builder.Insert(closeBrace, $"{newline}{propIndent}{propName} = {newValue},");
            return builder.ToString();
        }

        private static bool IsSimpleWritableExpression(string expression, Type propertyType)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            Type actualType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (expression is "null" or "default")
                return true;

            if (actualType == typeof(string))
            {
                return expression.StartsWith("\"", StringComparison.Ordinal)
                    || expression.StartsWith("@\"", StringComparison.Ordinal)
                    || expression.StartsWith("\"\"\"", StringComparison.Ordinal);
            }

            if (actualType == typeof(bool))
                return expression is "true" or "false";

            if (actualType == typeof(int) || actualType == typeof(float) || actualType == typeof(double))
                return NumberLiteralRegex().IsMatch(expression);

            if (actualType.IsEnum)
                return IsSimpleEnumExpression(expression, actualType);

            return false;
        }

        [GeneratedRegex(@"^-?(?:\d+(?:\.\d+)?|\.\d+)(?:[eE][+-]?\d+)?[fFdD]?$", RegexOptions.Compiled)]
        private static partial Regex NumberLiteralRegex();

        private static bool IsSimpleEnumExpression(string expression, Type enumType)
        {
            if (expression == $"({enumType.Name})0")
                return true;

            foreach (string part in expression.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!part.StartsWith(enumType.Name + ".", StringComparison.Ordinal))
                    return false;

                string memberName = part[(enumType.Name.Length + 1)..];
                if (string.IsNullOrWhiteSpace(memberName) || !Enum.IsDefined(enumType, memberName))
                    return false;
            }

            return true;
        }

        private static string? FindFirstTopLevelPropertyIndent(string body)
        {
            var assignment = FindFirstTopLevelAssignmentStart(body);
            return assignment < 0 ? null : GetLineIndent(body, assignment);
        }

        private static int FindFirstTopLevelAssignmentStart(string body)
        {
            var scanState = ScanState.Code;
            int depth = 0;

            for (int i = 0; i < body.Length; i++)
            {
                if (UpdateScanState(body, ref i, ref scanState))
                    continue;
                if (scanState != ScanState.Code)
                    continue;

                char ch = body[i];
                if (ch == '{' || ch == '[' || ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == '}' || ch == ']' || ch == ')')
                {
                    depth--;
                    continue;
                }

                if (depth == 1 && IsIdentifierStart(ch))
                {
                    int nameEnd = i + 1;
                    while (nameEnd < body.Length && IsIdentifierPart(body[nameEnd]))
                        nameEnd++;

                    int equalsIndex = SkipWhitespace(body, nameEnd);
                    if (equalsIndex < body.Length && body[equalsIndex] == '=')
                        return i;
                }
            }

            return -1;
        }

        private static string GetLineIndent(string text, int index)
        {
            int lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;

            int cursor = lineStart;
            while (cursor < text.Length && (text[cursor] == ' ' || text[cursor] == '\t'))
                cursor++;

            return text.Substring(lineStart, cursor - lineStart);
        }

        private static string ToCSharpStringLiteral(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char ch in value)
            {
                builder.Append(ch switch
                {
                    '\\' => "\\\\",
                    '"' => "\\\"",
                    '\0' => "\\0",
                    '\a' => "\\a",
                    '\b' => "\\b",
                    '\f' => "\\f",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    '\v' => "\\v",
                    _ => ch,
                });
            }
            builder.Append('"');
            return builder.ToString();
        }

        private static string FormatFloat(float value)
        {
            if (float.IsNaN(value)) return "float.NaN";
            if (float.IsPositiveInfinity(value)) return "float.PositiveInfinity";
            if (float.IsNegativeInfinity(value)) return "float.NegativeInfinity";

            string text = value.ToString("G9", CultureInfo.InvariantCulture);
            if (!text.Contains('.', StringComparison.Ordinal) && !text.Contains('E', StringComparison.OrdinalIgnoreCase))
                text += ".0";
            return text + "f";
        }

        private static string FormatDouble(double value)
        {
            if (double.IsNaN(value)) return "double.NaN";
            if (double.IsPositiveInfinity(value)) return "double.PositiveInfinity";
            if (double.IsNegativeInfinity(value)) return "double.NegativeInfinity";

            string text = value.ToString("G17", CultureInfo.InvariantCulture);
            if (!text.Contains('.', StringComparison.Ordinal) && !text.Contains('E', StringComparison.OrdinalIgnoreCase))
                text += ".0";
            return text + "d";
        }

        private static string FormatEnumValue(object value, Type enumType)
        {
            if (enumType.GetCustomAttribute<FlagsAttribute>() == null)
                return $"{enumType.Name}.{value}";

            ulong rawValue = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            if (rawValue == 0)
            {
                string? zeroName = Enum.GetNames(enumType)
                    .FirstOrDefault(name => Convert.ToUInt64(Enum.Parse(enumType, name), CultureInfo.InvariantCulture) == 0);
                return zeroName != null ? $"{enumType.Name}.{zeroName}" : $"({enumType.Name})0";
            }

            var parts = new List<string>();
            ulong remaining = rawValue;
            foreach (string name in Enum.GetNames(enumType))
            {
                ulong memberValue = Convert.ToUInt64(Enum.Parse(enumType, name), CultureInfo.InvariantCulture);
                if (memberValue == 0 || !IsSingleBit(memberValue))
                    continue;

                if ((rawValue & memberValue) == memberValue)
                {
                    parts.Add($"{enumType.Name}.{name}");
                    remaining &= ~memberValue;
                }
            }

            return remaining == 0 && parts.Count > 0
                ? string.Join(" | ", parts)
                : $"({enumType.Name}){rawValue.ToString(CultureInfo.InvariantCulture)}";
        }

        private static bool IsSingleBit(ulong value)
        {
            return value != 0 && (value & (value - 1)) == 0;
        }

        private static int SkipWhitespace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
            return index;
        }

        private static bool IsIdentifierStart(char ch)
        {
            return ch == '_' || char.IsLetter(ch);
        }

        private static bool IsIdentifierPart(char ch)
        {
            return ch == '_' || char.IsLetterOrDigit(ch);
        }

        private static bool UpdateScanState(string text, ref int index, ref ScanState state)
        {
            char ch = text[index];
            char next = index + 1 < text.Length ? text[index + 1] : '\0';

            switch (state)
            {
                case ScanState.Code:
                    if (ch == '"' && next == '"' && index + 2 < text.Length && text[index + 2] == '"')
                    {
                        state = ScanState.RawString;
                        index += 2;
                        return true;
                    }
                    if (ch == '@' && next == '"')
                    {
                        state = ScanState.VerbatimString;
                        index++;
                        return true;
                    }
                    if (ch == '"')
                    {
                        state = ScanState.String;
                        return true;
                    }
                    if (ch == '\'')
                    {
                        state = ScanState.Char;
                        return true;
                    }
                    if (ch == '/' && next == '/')
                    {
                        state = ScanState.LineComment;
                        index++;
                        return true;
                    }
                    if (ch == '/' && next == '*')
                    {
                        state = ScanState.BlockComment;
                        index++;
                        return true;
                    }
                    return false;

                case ScanState.String:
                    if (ch == '\\')
                    {
                        index++;
                        return true;
                    }
                    if (ch == '"')
                        state = ScanState.Code;
                    return true;

                case ScanState.VerbatimString:
                    if (ch == '"' && next == '"')
                    {
                        index++;
                        return true;
                    }
                    if (ch == '"')
                        state = ScanState.Code;
                    return true;

                case ScanState.RawString:
                    if (ch == '"' && next == '"' && index + 2 < text.Length && text[index + 2] == '"')
                    {
                        state = ScanState.Code;
                        index += 2;
                    }
                    return true;

                case ScanState.Char:
                    if (ch == '\\')
                    {
                        index++;
                        return true;
                    }
                    if (ch == '\'')
                        state = ScanState.Code;
                    return true;

                case ScanState.LineComment:
                    if (ch == '\n')
                        state = ScanState.Code;
                    return true;

                case ScanState.BlockComment:
                    if (ch == '*' && next == '/')
                    {
                        state = ScanState.Code;
                        index++;
                    }
                    return true;

                default:
                    return false;
            }
        }

        private enum ScanState
        {
            Code,
            String,
            VerbatimString,
            RawString,
            Char,
            LineComment,
            BlockComment,
        }
    }
}
#endif
