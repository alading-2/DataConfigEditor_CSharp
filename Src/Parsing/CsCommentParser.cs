using System.Text.RegularExpressions;

namespace DataConfigEditor.Parsing;

/// <summary>
/// C# 源码解析器 - 从插件移植，去除 Godot 依赖
/// 解析 .cs 文件中的属性注释、分组信息、静态字段、枚举成员注释
/// </summary>
public static class CsCommentParser
{
    // ====== 属性解析 ======

    /// <summary>
    /// 解析 .cs 文件，提取所有 public 属性的注释和分组
    /// </summary>
    public static Dictionary<string, PropertyCommentInfo> ParseFile(string filePath)
    {
        var result = new Dictionary<string, PropertyCommentInfo>();
        if (!File.Exists(filePath)) return result;

        var lines = File.ReadAllLines(filePath);
        var groupPositions = BuildGroupPositions(lines);

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (!trimmed.StartsWith("public ") || !trimmed.Contains("{ get;")) continue;
            if (trimmed.Contains("static readonly")) continue;

            var propInfo = ParsePropertyLine(lines, i);
            if (propInfo == null) continue;

            string propName = propInfo.Value.Name;
            string summary = ExtractSummaryAbove(lines, i);
            string group = FindGroupForLine(groupPositions, i);

            result[propName] = new PropertyCommentInfo
            {
                Summary = summary,
                Group = group,
                TypeName = propInfo.Value.TypeName,
                DefaultValueExpression = propInfo.Value.DefaultExpr,
                SourceLine = i,
            };
        }

        return result;
    }

    private static (string Name, string TypeName, string DefaultExpr)? ParsePropertyLine(string[] lines, int lineIdx)
    {
        string line = lines[lineIdx].Trim();

        var match = Regex.Match(line,
            @"public\s+([\w<>\?\[\],\s]+?)\s+(\w+)\s*\{[^}]*\}(?:\s*=\s*([^;]+?))?\s*;?");
        if (match.Success)
            return (match.Groups[2].Value, match.Groups[1].Value.Trim(),
                match.Groups[3].Success ? match.Groups[3].Value.Trim() : "");

        // 多行属性声明
        if (line.Contains("{") && !line.Contains("}"))
        {
            var headerMatch = Regex.Match(line, @"public\s+([\w<>\?\[\],\s]+?)\s+(\w+)\s*\{");
            if (headerMatch.Success)
            {
                string name = headerMatch.Groups[2].Value;
                string type = headerMatch.Groups[1].Value.Trim();
                string defaultExpr = "";
                for (int j = lineIdx + 1; j < Math.Min(lines.Length, lineIdx + 5); j++)
                {
                    string sub = lines[j].Trim();
                    if (sub.Contains("}"))
                    {
                        var defMatch = Regex.Match(sub, @"\}\s*=\s*([^;]+)\s*;");
                        if (defMatch.Success) defaultExpr = defMatch.Groups[1].Value.Trim();
                        break;
                    }
                }
                return (name, type, defaultExpr);
            }
        }

        return null;
    }

    // ====== 静态字段解析 ======

    public static List<StaticFieldInfo> ParseStaticFields(string filePath)
    {
        var result = new List<StaticFieldInfo>();
        if (!File.Exists(filePath)) return result;

        string source = File.ReadAllText(filePath);
        var matches = Regex.Matches(source, @"public\s+(static\s+)?readonly\s+(\w+)\s+(\w+)\s*=");
        foreach (Match m in matches)
        {
            result.Add(new StaticFieldInfo
            {
                TypeName = m.Groups[2].Value,
                FieldName = m.Groups[3].Value,
            });
        }
        return result;
    }

    // ====== 枚举注释解析 ======

    /// <summary>
    /// 解析枚举 .cs 文件中每个成员的注释
    /// </summary>
    public static Dictionary<string, string> ParseEnumComments(string filePath)
    {
        var result = new Dictionary<string, string>();
        if (!File.Exists(filePath)) return result;

        var lines = File.ReadAllLines(filePath);

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            var memberMatch = Regex.Match(trimmed, @"^(\w+)\s*(?:=\s*[^,;]+)?\s*[,;]");
            if (!memberMatch.Success) continue;

            string memberName = memberMatch.Groups[1].Value;
            if (memberName is "//" or "}" or "{" or "None") continue;
            if (!IsInsideEnumBlock(lines, i)) continue;

            string summary = ExtractSummaryAbove(lines, i);

            // 行尾注释: Member = 0, // 中文注释
            if (string.IsNullOrEmpty(summary))
            {
                int commentIdx = trimmed.IndexOf("//", StringComparison.Ordinal);
                if (commentIdx > 0)
                    summary = trimmed[(commentIdx + 2)..].Trim();
            }

            if (!string.IsNullOrEmpty(summary))
                result[memberName] = summary;
        }

        return result;
    }

    private static bool IsInsideEnumBlock(string[] lines, int lineIdx)
    {
        for (int i = lineIdx - 1; i >= 0; i--)
        {
            string t = lines[i].Trim();
            if (t.StartsWith("public enum ") || t.StartsWith("internal enum ") || t.StartsWith("enum "))
                return true;
            if (t.StartsWith("public class ") || t.StartsWith("public struct ") || t.StartsWith("namespace "))
                return false;
        }
        return false;
    }

    // ====== 分组解析 ======

    private static List<(int line, string name)> BuildGroupPositions(string[] lines)
    {
        var groups = new List<(int line, string name)>();
        for (int i = 0; i < lines.Length; i++)
        {
            // 匹配 [ExportGroup("xxx")]
            var gm = Regex.Match(lines[i], @"\[ExportGroup\(""([^""]+)""\)\]");
            if (gm.Success)
            {
                groups.Add((i, gm.Groups[1].Value));
                continue;
            }

            // 匹配注释分组: // ====== 分组名 ======
            var commentGroup = Regex.Match(lines[i], @"//\s*=+\s*(.+?)\s*=+\s*$");
            if (commentGroup.Success)
                groups.Add((i, commentGroup.Groups[1].Value.Trim()));
        }
        return groups;
    }

    private static string FindGroupForLine(List<(int line, string name)> groups, int targetLine)
    {
        string group = "";
        foreach (var (line, name) in groups)
        {
            if (line <= targetLine) group = name;
            else break;
        }
        return group;
    }

    // ====== Summary 提取 ======

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
}

public class PropertyCommentInfo
{
    public string Summary = "";
    public string Group = "";
    public string DataKey = "";
    public string TypeName = "";
    public string DefaultValueExpression = "";
    public int SourceLine;
}

public class StaticFieldInfo
{
    public string TypeName = "";
    public string FieldName = "";
}
