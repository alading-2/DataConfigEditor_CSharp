using DataConfigEditor.Parsing;

namespace DataConfigEditor.Core;

/// <summary>
/// 枚举注释缓存（纯源码解析）
/// 直接使用 ConfigTypeScanner 的枚举发现结果
/// </summary>
public class EnumCommentCache
{
    private readonly ConfigTypeScanner _scanner;
    private Dictionary<string, EnumParseResult>? _enums;
    private bool _loaded;

    public EnumCommentCache(ConfigTypeScanner scanner)
    {
        _scanner = scanner;
    }

    public void EnsureLoaded(string? _)
    {
        if (_loaded) return;
        _loaded = true;

        // 从 Scanner 获取所有已发现的枚举
        // 通过调用 GetAllConfigTypes 触发枚举扫描
        _scanner.GetAllConfigTypes();

        // 通过反射获取私有字段（简洁方案）
        _enums = new Dictionary<string, EnumParseResult>();
        var props = new Dictionary<string, List<PropertyMetadata>>();
        foreach (var type in _scanner.GetAllConfigTypes())
        {
            var typeProps = _scanner.GetProperties(type.ClassName, type.SourceFile);
            foreach (var p in typeProps.Where(p => p.IsEnum))
            {
                if (!_enums.ContainsKey(p.TypeName))
                    _enums[p.TypeName] = new EnumParseResult { Name = p.TypeName };
            }
        }
    }

    /// <summary>
    /// 获取枚举成员列表
    /// </summary>
    public EnumMemberInfo[] GetMembers(string enumName)
    {
        EnsureLoaded(null);

        // 直接从源码重新解析（确保有完整成员信息）
        return SourceParserScanEnumMembers(enumName);
    }

    private EnumMemberInfo[] SourceParserScanEnumMembers(string enumName)
    {
        // 在项目目录中搜索该枚举
        var projectRoot = _scanner.ProjectRoot;
        foreach (var file in Directory.GetFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}Tools{Path.DirectorySeparatorChar}"))
                continue;

            foreach (var enumInfo in SourceParser.ParseEnums(file))
            {
                if (enumInfo.Name == enumName)
                {
                    return enumInfo.Members.Select(m => new EnumMemberInfo
                    {
                        Name = m.Name,
                        Comment = m.Comment,
                    }).ToArray();
                }
            }
        }

        return Array.Empty<EnumMemberInfo>();
    }
}

public class EnumMemberInfo
{
    public string Name = "";
    public string Comment = "";
}
