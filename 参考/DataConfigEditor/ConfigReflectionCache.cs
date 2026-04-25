#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Slime.Addons.DataConfigEditor
{
    /// <summary>
    /// 配置类反射缓存
    /// 扫描 Data/DataNew 下的纯 POCO 配置类，递归收集继承属性，通过静态字段获取实例数据
    /// </summary>
    public static class ConfigReflectionCache
    {
        private static readonly Dictionary<Type, List<PropertyMetadata>> _propCache = new();
        private static readonly Dictionary<Type, List<InstanceInfo>> _instanceCache = new();
        private static readonly Dictionary<Type, Dictionary<string, PropertyCommentInfo>> _commentCache = new();
        private static readonly Dictionary<Type, List<string>> _groupOrderCache = new();
        private static readonly Dictionary<string, string> _dataKeyAliasMap = new(StringComparer.Ordinal)
        {
            ["FeatureGroupId"] = nameof(DataKey.AbilityFeatureGroup),
            ["FeatureHandlerId"] = nameof(DataKey.FeatureHandlerId),
            ["Category"] = nameof(DataKey.FeatureCategory),
            ["Enabled"] = nameof(DataKey.FeatureEnabled),
            ["Modifiers"] = DataKey.FeatureModifiers,
            ["EffectScenePath"] = DataKey.EffectScene,
            ["ProjectileScenePath"] = DataKey.ProjectileScene,
        };

        private static Dictionary<string, DataMeta>? _dataMetaByKey;
        private static HashSet<string>? _constStringKeys;
        private static List<ConfigTypeInfo>? _allTypes;

        public class ConfigTypeInfo
        {
            public Type Type;
            public string Name;
            public string SourceFile;
        }

        public class InstanceInfo
        {
            public string Name;
            public object Instance;
            public FieldInfo FieldInfo;
        }

        public static List<ConfigTypeInfo> GetAllConfigTypes()
        {
            if (_allTypes != null) return _allTypes;

            _allTypes = new List<ConfigTypeInfo>();
            string projectRoot = ProjectSettings.GlobalizePath("res://");
            GD.Print($"[DataConfigEditor] projectRoot={projectRoot}");

            string dataNewDir = Path.Combine(projectRoot, "Data", "DataNew");
            GD.Print($"[DataConfigEditor] dataNewDir={dataNewDir} exists={Directory.Exists(dataNewDir)}");

            if (!Directory.Exists(dataNewDir))
            {
                GD.PrintErr("[DataConfigEditor] Data/DataNew 目录不存在");
                return _allTypes;
            }

            var allTypes = new List<Type>();
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { allTypes.AddRange(a.GetTypes()); }
                catch { /* ignore */ }
            }

            // 调试：打印所有 Slime.ConfigNew 命名空间的类型
            var configNewTypes = allTypes.Where(t => t.Namespace != null && t.Namespace.Contains("ConfigNew")).ToList();
            GD.Print($"[DataConfigEditor] 找到 {configNewTypes.Count} 个 ConfigNew 命名空间类型:");
            foreach (var t in configNewTypes)
                GD.Print($"  - {t.FullName} (IsAbstract={t.IsAbstract}, IsClass={t.IsClass})");

            var csFiles = Directory.GetFiles(dataNewDir, "*.cs", SearchOption.AllDirectories);
            GD.Print($"[DataConfigEditor] 找到 {csFiles.Length} 个 .cs 文件");

            foreach (var csFile in csFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(csFile);
                var candidates = allTypes.Where(t =>
                    t.Name == fileName
                    && !t.IsAbstract
                    && t.IsClass
                    && t.IsPublic
                    && t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Any(p => p.CanRead && p.CanWrite && p.DeclaringType != typeof(object)))
                    .ToList();

                if (candidates.Count == 0)
                {
                    GD.Print($"[DataConfigEditor] 未找到类型: {fileName}");
                    continue;
                }

                var type = candidates[0];
                GD.Print($"[DataConfigEditor] 匹配: {fileName} → {type.FullName}");

                _allTypes.Add(new ConfigTypeInfo
                {
                    Type = type,
                    Name = $"{type.Name} ({type.Namespace})",
                    SourceFile = csFile,
                });
            }

            _allTypes = _allTypes.OrderBy(t => t.Name).ToList();
            return _allTypes;
        }

        public static List<PropertyMetadata> GetProperties(Type configType)
        {
            if (_propCache.TryGetValue(configType, out var cached))
                return cached;

            var props = new List<PropertyInfo>();
            CollectPropertiesRecursive(configType, props);

            cached = props
                .GroupBy(p => p.Name)
                .Select(g => g.First())
                .Select(p => new PropertyMetadata(p))
                .ToList();

            _propCache[configType] = cached;
            return cached;
        }

        public static (string DataKeyName, DataMeta? Meta) ResolveDataBinding(PropertyInfo prop)
        {
            EnsureDataKeyCache();

            string dataKeyName = prop.GetCustomAttribute<DataKeyAttribute>()?.Key ?? "";
            if (string.IsNullOrWhiteSpace(dataKeyName)
                && _dataKeyAliasMap.TryGetValue(prop.Name, out var aliasKey))
            {
                dataKeyName = aliasKey;
            }

            if (string.IsNullOrWhiteSpace(dataKeyName)
                && _dataMetaByKey != null
                && _dataMetaByKey.ContainsKey(prop.Name))
            {
                dataKeyName = prop.Name;
            }

            if (string.IsNullOrWhiteSpace(dataKeyName)
                && _constStringKeys != null
                && _constStringKeys.Contains(prop.Name))
            {
                dataKeyName = prop.Name;
            }

            if (string.IsNullOrWhiteSpace(dataKeyName))
                return ("", null);

            DataMeta? meta = null;
            if (_dataMetaByKey != null)
                _dataMetaByKey.TryGetValue(dataKeyName, out meta);

            meta ??= DataRegistry.GetMeta(dataKeyName);
            return (dataKeyName, meta);
        }

        private static void CollectPropertiesRecursive(Type type, List<PropertyInfo> result)
        {
            if (type == null || type == typeof(object) || type == typeof(Resource)
                || type == typeof(GodotObject) || type == typeof(RefCounted))
                return;

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.CanRead && p.CanWrite);

            result.AddRange(props);

            if (type.BaseType != null)
                CollectPropertiesRecursive(type.BaseType, result);
        }

        public static List<InstanceInfo> GetInstances(Type configType, string sourceFile)
        {
            if (_instanceCache.TryGetValue(configType, out var cached))
                return cached;

            cached = new List<InstanceInfo>();

            var staticFields = configType
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsInitOnly && f.FieldType == configType)
                .ToList();

            foreach (var field in staticFields)
            {
                try
                {
                    var value = field.GetValue(null);
                    if (value != null)
                    {
                        cached.Add(new InstanceInfo
                        {
                            Name = field.Name,
                            Instance = value,
                            FieldInfo = field,
                        });
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[DataConfigEditor] 读取静态字段 {field.Name} 失败: {e.Message}");
                }
            }

            if (cached.Count == 0)
            {
                try
                {
                    var instance = Activator.CreateInstance(configType);
                    if (instance != null)
                    {
                        cached.Add(new InstanceInfo
                        {
                            Name = "(默认值)",
                            Instance = instance,
                            FieldInfo = null,
                        });
                    }
                }
                catch { /* ignore */ }
            }

            _instanceCache[configType] = cached;
            return cached;
        }

        public static Dictionary<string, PropertyCommentInfo> GetComments(Type configType, string sourceFile)
        {
            if (_commentCache.TryGetValue(configType, out var cached))
                return cached;

            cached = CsCommentParser.ParseFile(sourceFile);

            if (configType.BaseType != null && configType.BaseType != typeof(object))
            {
                string? parentFile = FindSourceFile(configType.BaseType);
                if (parentFile != null)
                {
                    foreach (var kvp in CsCommentParser.ParseFile(parentFile))
                    {
                        if (!cached.ContainsKey(kvp.Key))
                            cached[kvp.Key] = kvp.Value;
                    }
                }
            }

            _commentCache[configType] = cached;
            return cached;
        }

        public static List<string> GetGroupOrder(Type configType, string sourceFile)
        {
            if (_groupOrderCache.TryGetValue(configType, out var cached))
                return cached;

            var comments = GetComments(configType, sourceFile);
            var seen = new HashSet<string>();
            cached = new List<string>();

            foreach (var info in comments.Values.OrderBy(c => c.SourceLine))
            {
                if (!string.IsNullOrEmpty(info.Group) && seen.Add(info.Group))
                    cached.Add(info.Group);
            }

            cached.Add("");

            _groupOrderCache[configType] = cached;
            return cached;
        }

        public static string? FindSourceFile(Type type)
        {
            string projectRoot = ProjectSettings.GlobalizePath("res://");

            string dataNewDir = Path.Combine(projectRoot, "Data", "DataNew");
            if (Directory.Exists(dataNewDir))
            {
                var files = Directory.GetFiles(dataNewDir, $"{type.Name}.cs", SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }

            string dataDir = Path.Combine(projectRoot, "Data", "Data");
            if (Directory.Exists(dataDir))
            {
                var files = Directory.GetFiles(dataDir, $"{type.Name}.cs", SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }

            var allFiles = Directory.GetFiles(projectRoot, $"{type.Name}.cs", SearchOption.AllDirectories);
            return allFiles.FirstOrDefault(f =>
                !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}"));
        }

        private static void EnsureDataKeyCache()
        {
            if (_dataMetaByKey != null && _constStringKeys != null)
                return;

            _dataMetaByKey = new Dictionary<string, DataMeta>(StringComparer.Ordinal);
            _constStringKeys = new HashSet<string>(StringComparer.Ordinal);

            var dataKeyType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.IsClass && t.Name == nameof(DataKey));

            if (dataKeyType == null)
            {
                GD.PrintErr("[DataConfigEditor] 未找到 DataKey 类型，无法解析 DataMeta");
                return;
            }

            var fields = dataKeyType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                try
                {
                    if (field.FieldType == typeof(DataMeta))
                    {
                        var meta = field.GetValue(null) as DataMeta;
                        if (meta != null && !string.IsNullOrWhiteSpace(meta.Key))
                            _dataMetaByKey[meta.Key] = meta;
                        continue;
                    }

                    if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                    {
                        var key = field.GetRawConstantValue() as string;
                        if (!string.IsNullOrWhiteSpace(key))
                            _constStringKeys.Add(key);
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[DataConfigEditor] 解析 DataKey 字段失败 {field.Name}: {e.Message}");
                }
            }
        }

        public static void ClearCache()
        {
            _propCache.Clear();
            _instanceCache.Clear();
            _commentCache.Clear();
            _groupOrderCache.Clear();
            _dataMetaByKey = null;
            _constStringKeys = null;
            _allTypes = null;
        }
    }

    public class PropertyMetadata
    {
        public string Name;
        public Type PropertyType;
        public PropertyInfo PropertyInfo;
        public string DataKeyName;
        public DataMeta? DataMeta;

        public bool IsEnum => PropertyType.IsEnum || (Nullable.GetUnderlyingType(PropertyType)?.IsEnum ?? false);
        public bool IsFlags
        {
            get
            {
                var et = EnumType;
                return et != null && et.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;
            }
        }
        public bool IsNumeric => PropertyType == typeof(int) || PropertyType == typeof(float) || PropertyType == typeof(double);
        public bool IsBool => PropertyType == typeof(bool);
        public bool IsString => PropertyType == typeof(string);
        public Type? EnumType => PropertyType.IsEnum ? PropertyType : Nullable.GetUnderlyingType(PropertyType);
        public bool HasDataKey => !string.IsNullOrWhiteSpace(DataKeyName);
        public bool IsPathString =>
            IsString
            && (Name.EndsWith("Path", StringComparison.Ordinal)
                || DataKeyName.EndsWith("Path", StringComparison.Ordinal)
                || DataKeyName is "EffectScene" or "ProjectileScene");
        public bool IsEditable => IsString || IsBool || IsNumeric || IsEnum;
        public string ReadOnlyReason
        {
            get
            {
                if (IsEditable) return "";
                if (!PropertyInfo.CanWrite) return "属性不可写";
                if (PropertyType.IsArray) return "数组暂不支持表格直接编辑";
                if (PropertyType != typeof(string)
                    && typeof(System.Collections.IEnumerable).IsAssignableFrom(PropertyType))
                    return "集合或字典暂不支持表格直接编辑";
                return "复杂类型暂不支持表格直接编辑";
            }
        }
        public string DisplayName => !string.IsNullOrWhiteSpace(DataMeta?.DisplayName) ? DataMeta.DisplayName : Name;
        public string CategoryName => DataMeta?.Category?.ToString() ?? "";
        public string DataDescription => DataMeta?.Description ?? "";
        public double MinNumericValue => DataMeta?.MinValue ?? -999999;
        public double MaxNumericValue => DataMeta?.MaxValue ?? 999999;

        public PropertyMetadata(PropertyInfo prop)
        {
            Name = prop.Name;
            PropertyType = prop.PropertyType;
            PropertyInfo = prop;

            var binding = ConfigReflectionCache.ResolveDataBinding(prop);
            DataKeyName = binding.DataKeyName;
            DataMeta = binding.Meta;
        }

        public string FormatValue(object? value)
        {
            if (value == null) return "";
            if (PropertyType == typeof(float)) return ((float)value).ToString("G");
            if (PropertyType == typeof(double)) return ((double)value).ToString("G");
            if (PropertyType == typeof(bool)) return (bool)value ? "true" : "false";
            if (PropertyType.IsEnum) return value.ToString() ?? "";
            return value.ToString() ?? "";
        }

        public string FriendlyTypeName => PropertyType.Name switch
        {
            "Int32" => "int",
            "Single" => "float",
            "Double" => "double",
            "Boolean" => "bool",
            "String" => "string",
            _ => PropertyType.Name,
        };
    }
}
#endif
