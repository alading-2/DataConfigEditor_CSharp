using System.Text.RegularExpressions;

namespace DataConfigEditor.Parsing;

internal sealed class DataKeyDefaultValueResolver
{
    private static readonly Regex DataKeyDefaultExpression = new(
        @"(?:global::)?DataKey\.(\w+)\.DefaultValue!?",
        RegexOptions.Compiled);

    private readonly Dictionary<string, string> _defaults;

    private DataKeyDefaultValueResolver(Dictionary<string, string> defaults)
    {
        _defaults = defaults;
    }

    public static DataKeyDefaultValueResolver? TryCreateForSourceFile(string sourceFilePath)
    {
        var dataKeyDirectory = FindDataKeyDirectory(sourceFilePath);
        return string.IsNullOrWhiteSpace(dataKeyDirectory)
            ? null
            : new DataKeyDefaultValueResolver(ParseDefaults(dataKeyDirectory));
    }

    public bool TryResolveDefaultExpression(string expression, out string defaultExpression)
    {
        defaultExpression = "";
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        var match = DataKeyDefaultExpression.Match(expression);
        if (!match.Success)
            return false;

        if (!_defaults.TryGetValue(match.Groups[1].Value, out var value))
            return false;

        defaultExpression = value;
        return true;
    }

    private static string? FindDataKeyDirectory(string sourceFilePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var candidate = Path.Combine(directory, "DataKey");
            if (Directory.Exists(candidate))
                return candidate;

            directory = Directory.GetParent(directory)?.FullName ?? "";
        }

        return null;
    }

    private static Dictionary<string, string> ParseDefaults(string dataKeyDirectory)
    {
        var defaults = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var filePath in Directory.GetFiles(dataKeyDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(filePath);
            foreach (Match match in Regex.Matches(source, @"public\s+static\s+readonly\s+DataMeta\s+(\w+)\s*="))
            {
                var keyName = match.Groups[1].Value;
                var newMetaIndex = source.IndexOf("new DataMeta", match.Index, StringComparison.Ordinal);
                if (newMetaIndex < 0)
                    continue;

                var openBraceIndex = source.IndexOf('{', newMetaIndex);
                if (openBraceIndex < 0)
                    continue;

                var closeBraceIndex = FindMatchingBrace(source, openBraceIndex);
                if (closeBraceIndex < 0)
                    continue;

                var body = source[(openBraceIndex + 1)..closeBraceIndex];
                var defaultExpression = ReadAssignmentExpression(body, "DefaultValue");
                if (!string.IsNullOrWhiteSpace(defaultExpression))
                    defaults[keyName] = defaultExpression;
            }
        }

        return defaults;
    }

    private static string ReadAssignmentExpression(string body, string propertyName)
    {
        var match = Regex.Match(body, $@"\b{Regex.Escape(propertyName)}\s*=");
        if (!match.Success)
            return "";

        var start = match.Index + match.Length;
        var depth = 0;
        var inString = false;
        var inChar = false;
        var escaped = false;

        for (var i = start; i < body.Length; i++)
        {
            var ch = body[i];

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
            else if (ch == ',' && depth == 0)
                return body[start..i].Trim();
        }

        return body[start..].Trim();
    }

    private static int FindMatchingBrace(string source, int openBraceIndex)
    {
        var depth = 0;
        var inString = false;
        var inChar = false;
        var escaped = false;

        for (var i = openBraceIndex; i < source.Length; i++)
        {
            var ch = source[i];

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

            if (ch == '{')
                depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }
}
