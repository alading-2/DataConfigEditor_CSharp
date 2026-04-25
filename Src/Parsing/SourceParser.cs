using System.Text.RegularExpressions;

namespace DataConfigEditor.Parsing;

/// <summary>
/// 纯源码解析器 — 不依赖任何 DLL 或反射
/// 解析 .cs 文件中的：类定义、属性、静态实例初始化值、枚举定义
/// </summary>
public static class SourceParser
{
    // ====== 类解析 ======

    /// <summary>
    /// 解析 .cs 文件中的类定义，提取类名、命名空间、基类、属性列表
    /// </summary>
    public static ClassInfo? ParseClass(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        var source = File.ReadAllText(filePath);
        return ParseClassFromSource(source, filePath);
    }

    public static ClassInfo? ParseClassFromSource(string source, string filePath)
    {
        // 匹配: public class ClassName [: BaseClass]
        var classMatch = Regex.Match(source,
            @"(?:public|internal)\s+(?:abstract\s+)?class\s+(\w+)(?:\s*:\s*(\w+))?",
            RegexOptions.Multiline);

        if (!classMatch.Success) return null;

        string className = classMatch.Groups[1].Value;
        string baseClass = classMatch.Groups[2].Success ? classMatch.Groups[2].Value : "";

        // 提取命名空间
        string ns = "";
        var nsMatch = Regex.Match(source, @"namespace\s+([\w.]+)");
        if (nsMatch.Success) ns = nsMatch.Groups[1].Value;

        // 解析属性
        var properties = ParseProperties(source);

        // 解析静态实例
        var instances = ParseStaticInstances(source);

        return new ClassInfo
        {
            ClassName = className,
            Namespace = ns,
            Usings = ParseUsingNamespaces(source),
            BaseClassName = baseClass,
            SourceFile = filePath,
            Properties = properties,
            Instances = instances,
        };
    }

    // ====== 属性解析 ======

    /// <summary>
    /// 解析源码中所有 public 自动属性
    /// </summary>
    public static List<PropertyParseResult> ParseProperties(string source)
    {
        var result = new List<PropertyParseResult>();
        var lines = source.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();

            // 匹配: public TypeName PropName { get; set; } [= default];
            if (!trimmed.StartsWith("public ") || !trimmed.Contains("{ get;")) continue;
            if (trimmed.Contains("static readonly")) continue;

            var match = Regex.Match(trimmed,
                @"public\s+([\w<>\?\[\],\s]+?)\s+(\w+)\s*\{[^}]*\}(?:\s*=\s*([^;]+))?\s*;?");
            if (!match.Success)
            {
                // 多行属性
                var headerMatch = Regex.Match(trimmed, @"public\s+([\w<>\?\[\],\s]+?)\s+(\w+)\s*\{");
                if (headerMatch.Success)
                {
                    string name = headerMatch.Groups[2].Value;
                    string type = headerMatch.Groups[1].Value.Trim();
                    string defaultExpr = "";
                    for (int j = i + 1; j < Math.Min(lines.Length, i + 5); j++)
                    {
                        string sub = lines[j].Trim();
                        if (sub.Contains("}"))
                        {
                            var defMatch = Regex.Match(sub, @"\}\s*=\s*([^;]+)\s*;");
                            if (defMatch.Success) defaultExpr = defMatch.Groups[1].Value.Trim();
                            break;
                        }
                    }
                    result.Add(new PropertyParseResult
                    {
                        Name = name,
                        TypeName = type,
                        DefaultValue = defaultExpr,
                        SourceLine = i,
                    });
                }
                continue;
            }

            result.Add(new PropertyParseResult
            {
                Name = match.Groups[2].Value,
                TypeName = match.Groups[1].Value.Trim(),
                DefaultValue = match.Groups[3].Success ? match.Groups[3].Value.Trim() : "",
                SourceLine = i,
            });
        }

        return result;
    }

    // ====== 静态实例解析 ======

    /// <summary>
    /// 解析源码中所有 public static readonly 实例及其初始化值
    /// 模式: public static readonly TypeName FieldName = new() { Prop1 = val1, ... };
    /// </summary>
    public static List<InstanceParseResult> ParseStaticInstances(string source)
    {
        var result = new List<InstanceParseResult>();
        var lines = source.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();

            // 匹配: public static readonly TypeName FieldName = new
            var fieldMatch = Regex.Match(trimmed,
                @"public\s+static\s+readonly\s+(\w+)\s+(\w+)\s*=\s*new\s*(?:\w+)?\s*(?:\(\s*\))?");
            if (!fieldMatch.Success) continue;

            string typeName = fieldMatch.Groups[1].Value;
            string fieldName = fieldMatch.Groups[2].Value;

            // 提取字段注释
            string summary = ExtractSummaryAbove(lines, i);

            // 提取初始化块 { ... } 中的所有属性值
            var values = ParseInitializerBlock(lines, i);

            result.Add(new InstanceParseResult
            {
                TypeName = typeName,
                FieldName = fieldName,
                Summary = summary,
                Values = values,
            });
        }

        return result;
    }

    /// <summary>
    /// 解析初始化块 { Prop1 = val1, Prop2 = val2, ... }
    /// </summary>
    private static Dictionary<string, string> ParseInitializerBlock(string[] lines, int startLine)
    {
        var values = new Dictionary<string, string>();

        // 找到开括号位置
        int braceLine = startLine;
        int braceStart = lines[braceLine].IndexOf('{');
        while (braceStart < 0 && braceLine + 1 < lines.Length)
        {
            braceLine++;
            braceStart = lines[braceLine].IndexOf('{');
        }

        if (braceStart < 0) return values;

        // 找到匹配的闭括号，同时提取属性赋值
        int depth = 0;
        for (int i = braceLine; i < lines.Length; i++)
        {
            string line = lines[i];
            for (int c = (i == braceLine ? braceStart : 0); c < line.Length; c++)
            {
                if (line[c] == '{') depth++;
                else if (line[c] == '}')
                {
                    depth--;
                    if (depth == 0) return values;
                }
            }

            // 跳过第一行（包含开括号之前的声明）
            if (i == braceLine) continue;

            // 尝试匹配: PropName = value, 或 PropName = value
            string trimmed = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//") || trimmed.StartsWith("}"))
                continue;

            var propMatch = Regex.Match(trimmed, @"(\w+)\s*=\s*(.+)\s*(?:,|$)");
            if (propMatch.Success)
            {
                string propName = propMatch.Groups[1].Value;
                string propValue = TrimTopLevelTrailingComma(propMatch.Groups[2].Value.Trim());

                propValue = RemoveLineCommentOutsideString(propValue);

                // 去掉尾部逗号
                propValue = propValue.TrimEnd(',');

                values[propName] = propValue;
            }
        }

        return values;
    }

    private static string RemoveLineCommentOutsideString(string value)
    {
        var inString = false;
        var inChar = false;
        var escaped = false;

        for (var i = 0; i < value.Length - 1; i++)
        {
            var ch = value[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if ((inString || inChar) && ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (!inChar && ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString && ch == '\'')
            {
                inChar = !inChar;
                continue;
            }

            if (!inString && !inChar && ch == '/' && value[i + 1] == '/')
                return value[..i].TrimEnd();
        }

        return value.Trim();
    }

    private static string TrimTopLevelTrailingComma(string value)
    {
        var trimmed = value.TrimEnd();
        if (trimmed.Length == 0 || trimmed[^1] != ',')
            return trimmed;

        var depth = 0;
        var inString = false;
        var inChar = false;
        var escaped = false;

        for (var i = 0; i < trimmed.Length - 1; i++)
        {
            var ch = trimmed[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if ((inString || inChar) && ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (!inChar && ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString && ch == '\'')
            {
                inChar = !inChar;
                continue;
            }

            if (inString || inChar)
                continue;

            if (ch is '(' or '{' or '[')
                depth++;
            else if (ch is ')' or '}' or ']')
                depth = Math.Max(0, depth - 1);
        }

        return depth == 0 ? trimmed[..^1].TrimEnd() : trimmed;
    }

    // ====== 枚举解析 ======

    /// <summary>
    /// 解析 .cs 文件中所有枚举定义
    /// </summary>
    public static List<EnumParseResult> ParseEnums(string filePath)
    {
        var result = new List<EnumParseResult>();
        if (!File.Exists(filePath)) return result;

        var source = File.ReadAllText(filePath);
        var lines = source.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();

            // 匹配: [Flags] public enum Name 或 public enum Name
            var enumMatch = Regex.Match(trimmed, @"(?:\[Flags\]\s*)?(?:public\s+)?enum\s+(\w+)");
            if (!enumMatch.Success) continue;

            string enumName = enumMatch.Groups[1].Value;
            bool isFlags = (i > 0 && lines[i - 1].Trim().StartsWith("[Flags]"))
                        || trimmed.StartsWith("[Flags]");

            var members = new List<EnumMemberParseResult>();
            for (int j = i + 1; j < lines.Length; j++)
            {
                string memberLine = lines[j].Trim();
                if (memberLine == "{" ) continue;
                if (memberLine == "}" || memberLine.StartsWith("}")) break;

                var memberMatch = Regex.Match(memberLine, @"^(\w+)\s*(?:=\s*([^,;]+))?\s*[,;]?");
                if (!memberMatch.Success) continue;

                string memberName = memberMatch.Groups[1].Value;
                if (memberName is "//" or "{" or "}") continue;

                string memberValue = memberMatch.Groups[2].Success ? memberMatch.Groups[2].Value.Trim() : "";
                string comment = ExtractSummaryAbove(lines, j);

                // 行尾注释: Member = 0, // 中文注释
                if (string.IsNullOrEmpty(comment))
                {
                    int commentIdx = memberLine.IndexOf("//", StringComparison.Ordinal);
                    if (commentIdx > 0)
                        comment = memberLine[(commentIdx + 2)..].Trim();
                }

                members.Add(new EnumMemberParseResult
                {
                    Name = memberName,
                    Value = memberValue,
                    Comment = comment,
                });
            }

            result.Add(new EnumParseResult
            {
                Name = enumName,
                IsFlags = isFlags,
                Members = members,
                SourceFile = filePath,
            });
        }

        return result;
    }

    /// <summary>
    /// 扫描目录中所有 .cs 文件的枚举
    /// </summary>
    public static List<EnumParseResult> ScanAllEnums(string directory)
    {
        var result = new List<EnumParseResult>();
        if (!Directory.Exists(directory)) return result;

        foreach (var file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            result.AddRange(ParseEnums(file));
        }

        return result;
    }

    // ====== 基类源文件查找 ======

    /// <summary>
    /// 在项目目录中查找某个类的源文件
    /// </summary>
    public static string? FindClassSourceFile(string projectRoot, string className)
    {
        // 优先在 Data/DataNew 查找
        var dataNew = Path.Combine(projectRoot, "Data", "DataNew");
        if (Directory.Exists(dataNew))
        {
            var files = Directory.GetFiles(dataNew, $"{className}.cs", SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }

        // 扩大到整个项目（排除 obj/bin/.godot）
        foreach (var file in Directory.GetFiles(projectRoot, $"{className}.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}Tools{Path.DirectorySeparatorChar}"))
                continue;
            return file;
        }

        return null;
    }

    // ====== 辅助方法 ======

    private static string ExtractSummaryAbove(string[] lines, int startLine)
    {
        var summaryLines = new List<string>();
        bool inSummary = false;

        for (int j = startLine - 1; j >= 0; j--)
        {
            string trimmed = lines[j].Trim();

            if (TryExtractSingleLineSummary(trimmed, out var singleLineSummary))
            {
                if (!string.IsNullOrWhiteSpace(singleLineSummary))
                    summaryLines.Insert(0, singleLineSummary);
                break;
            }

            if (trimmed.Contains("</summary>", StringComparison.Ordinal))
            {
                var beforeEnd = RemoveXmlDocPrefix(trimmed)
                    .Split("</summary>", StringSplitOptions.None)[0]
                    .Trim();
                if (!string.IsNullOrWhiteSpace(beforeEnd))
                    summaryLines.Insert(0, beforeEnd);

                inSummary = true;
                continue;
            }

            if (trimmed.Contains("<summary>", StringComparison.Ordinal))
            {
                var afterStart = RemoveXmlDocPrefix(trimmed)
                    .Split("<summary>", StringSplitOptions.None)
                    .Last()
                    .Trim();
                if (!string.IsNullOrWhiteSpace(afterStart))
                    summaryLines.Insert(0, afterStart);
                break;
            }

            if (inSummary)
            {
                string content = RemoveXmlDocPrefix(trimmed);

                if (!string.IsNullOrWhiteSpace(content))
                    summaryLines.Insert(0, content);
                continue;
            }

            if (trimmed.StartsWith("public ") || trimmed.StartsWith("private ")
                || trimmed.StartsWith("protected ") || trimmed.StartsWith("internal ")
                || trimmed.StartsWith("[") || trimmed.StartsWith("// ===")
                || trimmed.StartsWith("// ====="))
                break;
        }

        return string.Join(" ", summaryLines).Trim();
    }

    private static bool TryExtractSingleLineSummary(string trimmed, out string summary)
    {
        summary = "";
        if (!trimmed.Contains("<summary>", StringComparison.Ordinal) ||
            !trimmed.Contains("</summary>", StringComparison.Ordinal))
            return false;

        var content = RemoveXmlDocPrefix(trimmed);
        var start = content.IndexOf("<summary>", StringComparison.Ordinal);
        var end = content.IndexOf("</summary>", StringComparison.Ordinal);
        if (start < 0 || end < 0 || end < start)
            return false;

        summary = content[(start + "<summary>".Length)..end].Trim();
        return true;
    }

    private static string RemoveXmlDocPrefix(string text)
    {
        text = text.Trim();
        if (text.StartsWith("///", StringComparison.Ordinal))
            return text[3..].Trim();
        if (text.StartsWith("//", StringComparison.Ordinal))
            return text[2..].Trim();
        return text;
    }

    private static List<string> ParseUsingNamespaces(string source)
    {
        return Regex.Matches(source, @"^\s*using\s+([\w.]+)\s*;", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}

// ====== 数据类 ======

public class ClassInfo
{
    public string ClassName = "";
    public string Namespace = "";
    public List<string> Usings = new();
    public string BaseClassName = "";
    public string SourceFile = "";
    public List<PropertyParseResult> Properties = new();
    public List<InstanceParseResult> Instances = new();
}

public class PropertyParseResult
{
    public string Name = "";
    public string TypeName = "";
    public string DefaultValue = "";
    public int SourceLine;
}

public class InstanceParseResult
{
    public string TypeName = "";
    public string FieldName = "";
    public string Summary = "";
    public Dictionary<string, string> Values = new();
}

public class EnumParseResult
{
    public string Name = "";
    public bool IsFlags;
    public List<EnumMemberParseResult> Members = new();
    public string SourceFile = "";
}

public class EnumMemberParseResult
{
    public string Name = "";
    public string Value = "";
    public string Comment = "";
}
