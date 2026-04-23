using DataConfigEditor.Parsing;

namespace DataConfigEditor.Core;

/// <summary>
/// 扫描用户打开的数据目录，纯源码解析获取配置类型
/// 枚举/类型定义通过自动向上查找项目根目录发现
/// </summary>
public class ConfigTypeScanner
{
    /// <summary>用户打开的数据目录（如 Data/DataNew）</summary>
    private readonly string _dataDir;

    /// <summary>自动检测到的项目根目录</summary>
    private readonly string _projectRoot;

    private List<ConfigTypeInfo>? _allTypes;
    private Dictionary<string, EnumParseResult>? _enumCache;
    private readonly Dictionary<string, List<PropertyMetadata>> _propCache = new();
    private readonly Dictionary<string, List<InstanceInfo>> _instanceCache = new();
    private readonly Dictionary<string, Dictionary<string, PropertyCommentInfo>> _commentCache = new();
    private readonly Dictionary<string, ClassInfo> _classInfoCache = new();

    public ConfigTypeScanner(string dataDirectory)
    {
        _dataDir = dataDirectory;
        _projectRoot = DetectProjectRoot(dataDirectory);
    }

    /// <summary>
    /// 自动检测项目根目录：从打开的目录向上查找 .csproj 或 project.godot
    /// </summary>
    private static string DetectProjectRoot(string startDir)
    {
        var dir = startDir;
        for (int i = 0; i < 10; i++)
        {
            if (Directory.GetFiles(dir, "*.csproj").Length > 0
                || File.Exists(Path.Combine(dir, "project.godot")))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        // 回退：用打开目录的父目录
        var fallback = Directory.GetParent(startDir)?.FullName ?? startDir;
        return fallback;
    }

    /// <summary>
    /// 加载项目（纯源码解析，始终成功）
    /// </summary>
    public bool LoadProjectAssembly() => Directory.Exists(_dataDir);

    /// <summary>
    /// 获取所有配置类型
    /// </summary>
    public List<ConfigTypeInfo> GetAllConfigTypes()
    {
        if (_allTypes != null) return _allTypes;

        _allTypes = new List<ConfigTypeInfo>();

        if (!Directory.Exists(_dataDir))
            return _allTypes;

        // 预加载所有枚举
        var allEnums = GetAllEnums();

        // 扫描数据目录中的所有 .cs 文件
        var csFiles = Directory.GetFiles(_dataDir, "*.cs", SearchOption.AllDirectories);

        foreach (var csFile in csFiles)
        {
            var classInfo = SourceParser.ParseClass(csFile);
            if (classInfo == null) continue;

            // 跳过没有 public 属性的辅助类
            if (classInfo.Properties.Count == 0) continue;

            _classInfoCache[classInfo.ClassName] = classInfo;

            // 查找基类源文件
            string baseClassSource = "";
            if (!string.IsNullOrEmpty(classInfo.BaseClassName))
            {
                baseClassSource = SourceParser.FindClassSourceFile(_projectRoot, classInfo.BaseClassName) ?? "";
            }

            _allTypes.Add(new ConfigTypeInfo
            {
                ClassName = classInfo.ClassName,
                Namespace = classInfo.Namespace,
                Name = string.IsNullOrEmpty(classInfo.Namespace)
                    ? classInfo.ClassName
                    : $"{classInfo.ClassName} ({classInfo.Namespace})",
                SourceFile = csFile,
                BaseClassName = classInfo.BaseClassName,
                BaseClassSourceFile = baseClassSource,
            });
        }

        // 排序：有实例的排前面
        _allTypes = _allTypes
            .OrderByDescending(t =>
            {
                var ci = _classInfoCache.GetValueOrDefault(t.ClassName);
                return ci?.Instances.Count ?? 0;
            })
            .ThenBy(t => t.Name)
            .ToList();

        return _allTypes;
    }

    /// <summary>
    /// 获取配置类型的所有属性（含继承的基类属性）
    /// </summary>
    public List<PropertyMetadata> GetProperties(string className, string sourceFile)
    {
        if (_propCache.TryGetValue(className, out var cached))
            return cached;

        var allEnums = GetAllEnums();
        var comments = GetComments(className, sourceFile);

        // 收集属性（包括基类）
        var allProps = new List<PropertyParseResult>();
        CollectPropertiesRecursive(className, allProps);

        // 去重并构建 PropertyMetadata
        var seen = new HashSet<string>();
        cached = new List<PropertyMetadata>();

        foreach (var p in allProps)
        {
            if (!seen.Add(p.Name)) continue;

            string typeName = SimplifyTypeName(p.TypeName);
            bool isEnum = allEnums.ContainsKey(typeName);
            bool isFlags = isEnum && allEnums[typeName].IsFlags;

            cached.Add(new PropertyMetadata
            {
                Name = p.Name,
                TypeName = typeName,
                DefaultValue = p.DefaultValue,
                Group = comments.TryGetValue(p.Name, out var c) ? c.Group : "",
                Summary = comments.TryGetValue(p.Name, out var c2) ? c2.Summary : "",
                IsEnum = isEnum,
                IsFlags = isFlags,
            });
        }

        _propCache[className] = cached;
        return cached;
    }

    /// <summary>
    /// 获取静态实例列表
    /// </summary>
    public List<InstanceInfo> GetInstances(string className)
    {
        if (_instanceCache.TryGetValue(className, out var cached))
            return cached;

        cached = new List<InstanceInfo>();

        if (_classInfoCache.TryGetValue(className, out var classInfo))
        {
            foreach (var inst in classInfo.Instances)
            {
                cached.Add(new InstanceInfo
                {
                    Name = inst.FieldName,
                    Values = new Dictionary<string, string>(inst.Values),
                    Summary = inst.Summary,
                });
            }
        }

        _instanceCache[className] = cached;
        return cached;
    }

    /// <summary>
    /// 获取属性注释（含基类合并）
    /// </summary>
    public Dictionary<string, PropertyCommentInfo> GetComments(string className, string sourceFile)
    {
        if (_commentCache.TryGetValue(className, out var cached))
            return cached;

        cached = CsCommentParser.ParseFile(sourceFile);

        // 合并基类注释
        if (_classInfoCache.TryGetValue(className, out var ci)
            && !string.IsNullOrEmpty(ci.BaseClassName))
        {
            var baseFile = SourceParser.FindClassSourceFile(_projectRoot, ci.BaseClassName);
            if (baseFile != null)
            {
                foreach (var kvp in CsCommentParser.ParseFile(baseFile))
                {
                    if (!cached.ContainsKey(kvp.Key))
                        cached[kvp.Key] = kvp.Value;
                }
            }
        }

        _commentCache[className] = cached;
        return cached;
    }

    public void ClearCache()
    {
        _propCache.Clear();
        _instanceCache.Clear();
        _commentCache.Clear();
        _classInfoCache.Clear();
        _enumCache = null;
        _allTypes = null;
    }

    public string DataDir => _dataDir;
    public string ProjectRoot => _projectRoot;

    /// <summary>
    /// 获取所有枚举定义（缓存）
    /// 策略：从项目根目录扫描 Data/DataKey/ 和 Src/ 子目录
    /// </summary>
    private Dictionary<string, EnumParseResult> GetAllEnums()
    {
        if (_enumCache != null) return _enumCache;

        _enumCache = new Dictionary<string, EnumParseResult>();

        // 1. 扫描 Data/DataKey/（枚举集中存放处）
        var dataKeyDir = Path.Combine(_projectRoot, "Data", "DataKey");
        if (Directory.Exists(dataKeyDir))
        {
            foreach (var e in SourceParser.ScanAllEnums(dataKeyDir))
                _enumCache[e.Name] = e;
        }

        // 2. 扫描数据目录自身（可能有内联枚举）
        foreach (var e in SourceParser.ScanAllEnums(_dataDir))
            _enumCache.TryAdd(e.Name, e);

        // 3. 扫描 Src/ 目录（如 GeometryType, TargetSorting 等）
        var srcDir = Path.Combine(_projectRoot, "Src");
        if (Directory.Exists(srcDir))
        {
            foreach (var e in SourceParser.ScanAllEnums(srcDir))
                _enumCache.TryAdd(e.Name, e);
        }

        // 4. 如果还缺枚举，从数据目录的属性类型名反向查找
        //    在项目根下全局搜索未匹配的枚举类型
        ResolveMissingEnums();

        return _enumCache;
    }

    /// <summary>
    /// 收集所有属性中引用的类型名，对未找到的枚举进行全局搜索
    /// </summary>
    private void ResolveMissingEnums()
    {
        // 收集所有属性中引用的自定义类型名
        var referencedTypes = new HashSet<string>();
        var builtinTypes = new HashSet<string>
        {
            "int", "float", "double", "bool", "string", "string?",
            "long", "short", "byte", "char", "decimal",
            "List", "Dictionary", "object",
        };

        foreach (var ci in _classInfoCache.Values)
        {
            foreach (var prop in ci.Properties)
            {
                string typeName = SimplifyTypeName(prop.TypeName);
                // 去掉泛型参数
                int genIdx = typeName.IndexOf('<');
                if (genIdx > 0) typeName = typeName[..genIdx];

                if (!builtinTypes.Contains(typeName) && !_enumCache!.ContainsKey(typeName))
                    referencedTypes.Add(typeName);
            }
        }

        if (referencedTypes.Count == 0) return;

        // 在项目根下全局搜索这些类型的枚举定义
        foreach (var csFile in Directory.GetFiles(_projectRoot, "*.cs", SearchOption.AllDirectories))
        {
            // 跳过 obj/bin/.godot/Tools
            if (csFile.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || csFile.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || csFile.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}")
                || csFile.Contains($"{Path.DirectorySeparatorChar}Tools{Path.DirectorySeparatorChar}"))
                continue;

            foreach (var enumInfo in SourceParser.ParseEnums(csFile))
            {
                if (referencedTypes.Contains(enumInfo.Name))
                {
                    _enumCache!.TryAdd(enumInfo.Name, enumInfo);
                    referencedTypes.Remove(enumInfo.Name);
                    if (referencedTypes.Count == 0) return;
                }
            }
        }
    }

    // ====== 私有方法 ======

    private void CollectPropertiesRecursive(string className, List<PropertyParseResult> result)
    {
        if (!_classInfoCache.TryGetValue(className, out var ci)) return;

        if (!string.IsNullOrEmpty(ci.BaseClassName) && ci.BaseClassName != "object")
        {
            EnsureClassLoaded(ci.BaseClassName);
            CollectPropertiesRecursive(ci.BaseClassName, result);
        }

        result.AddRange(ci.Properties);
    }

    private void EnsureClassLoaded(string className)
    {
        if (_classInfoCache.ContainsKey(className)) return;

        var file = SourceParser.FindClassSourceFile(_projectRoot, className);
        if (file == null) return;

        var ci = SourceParser.ParseClass(file);
        if (ci != null)
            _classInfoCache[className] = ci;
    }

    private static string SimplifyTypeName(string typeName)
    {
        return typeName.Trim().TrimEnd('?');
    }
}
