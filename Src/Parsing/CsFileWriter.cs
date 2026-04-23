using System.Text.RegularExpressions;

namespace DataConfigEditor.Parsing;

/// <summary>
/// .cs 文件保存器（纯源码版，无反射）
/// 将 InstanceInfo.Values 字典写回 .cs 静态初始化器
/// </summary>
public static class CsFileWriter
{
    /// <summary>
    /// 将所有实例的属性变更合并写入 .cs 文件
    /// </summary>
    public static int WriteAllChanges(
        string filePath,
        List<Core.InstanceInfo> instances,
        List<Core.PropertyMetadata> properties)
    {
        if (!File.Exists(filePath)) return 0;

        string source = File.ReadAllText(filePath);
        int saved = 0;

        foreach (var inst in instances)
        {
            var range = FindStaticInitializerRange(source, inst.Name);
            if (range == null) continue;

            string body = source.Substring(range.Value.Start, range.Value.Length);
            string newBody = body;

            foreach (var prop in properties)
            {
                if (!inst.Values.TryGetValue(prop.Name, out var val)) continue;
                string csValue = FormatValueForCs(val, prop.TypeName);
                newBody = ReplacePropertyInBody(newBody, prop.Name, csValue);
            }

            if (newBody != body)
            {
                source = source.Remove(range.Value.Start, range.Value.Length)
                               .Insert(range.Value.Start, newBody);
                saved++;
            }
        }

        if (saved > 0)
            File.WriteAllText(filePath, source);

        return saved;
    }

    /// <summary>
    /// 找到静态字段初始化器的 { } 范围
    /// </summary>
    private static (int Start, int Length)? FindStaticInitializerRange(string source, string fieldName)
    {
        var startRegex = new Regex(
            @"public\s+static\s+readonly\s+\w+\s+" + Regex.Escape(fieldName) + @"\s*=\s*new\s*(?:\w+)?\s*(?:<[^>]+>)?\s*\(\s*\{");

        var startMatch = startRegex.Match(source);
        if (!startMatch.Success)
        {
            var altRegex = new Regex(
                @"public\s+static\s+readonly\s+\w+\s+" + Regex.Escape(fieldName) + @"\s*=\s*new\s+\w+\s*(?:<[^>]+>)?\s*\(\s*\)\s*\{");
            startMatch = altRegex.Match(source);
        }

        if (!startMatch.Success) return null;

        int braceStart = source.IndexOf('{', startMatch.Index);
        if (braceStart < 0) return null;

        int braceEnd = FindMatchingBrace(source, braceStart);
        if (braceEnd < 0) return null;

        return (braceStart, braceEnd - braceStart + 1);
    }

    private static int FindMatchingBrace(string source, int openBraceIdx)
    {
        int depth = 0;
        for (int i = openBraceIdx; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static string ReplacePropertyInBody(string body, string propName, string newValue)
    {
        var pattern = $@"({Regex.Escape(propName)}\s*=\s*)([^,\n}}]+)";
        return Regex.Replace(body, pattern, match => match.Groups[1].Value + newValue);
    }

    /// <summary>
    /// 将值字符串格式化为 C# 源码表达式
    /// </summary>
    public static string FormatValueForCs(string value, string typeName)
    {
        if (string.IsNullOrEmpty(value) || value == "null")
        {
            if (typeName is "string" or "string?")
                return "null";
            return "default";
        }

        // 字符串：加引号
        if (typeName is "string" or "string?")
        {
            return $"\"{value}\"";
        }

        // 布尔
        if (typeName == "bool")
        {
            return value.ToLower() == "true" ? "true" : "false";
        }

        // int
        if (typeName == "int")
        {
            return value;
        }

        // float
        if (typeName == "float")
        {
            string s = value;
            if (!s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
                s += ".0";
            return s + "f";
        }

        // double
        if (typeName == "double")
        {
            string s = value;
            if (!s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
                s += ".0";
            return s + "d";
        }

        // 枚举
        if (value.Contains('.'))
            return value;

        // 其他
        return value;
    }
}
