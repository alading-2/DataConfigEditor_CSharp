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
/// 枚举中文注释缓存 - 懒加载版本
/// 不再启动时全量扫描，而是按需加载当前需要的枚举类型
/// </summary>
    public static class EnumCommentCache
    {
        private static readonly Dictionary<Type, EnumMemberInfo[]> _cache = new();
        private static Dictionary<string, string>? _sourceFileMap;

        /// <summary>
        /// 枚举成员信息：名称 + 中文注释
        /// </summary>
        public struct EnumMemberInfo
        {
            public string Name;
            public string Comment;
            public bool IsFlags;
            public int Value;
        }

        /// <summary>
        /// 按需获取枚举成员信息，自动懒加载
        /// </summary>
        public static EnumMemberInfo[] GetMembers(Type enumType)
        {
            if (_cache.TryGetValue(enumType, out var cached))
                return cached;

            var members = LoadEnumMembers(enumType);
            _cache[enumType] = members;
            return members;
        }

        public static string GetComment(Type enumType, string memberName)
        {
            var members = GetMembers(enumType);
            foreach (var m in members)
            {
                if (m.Name == memberName) return m.Comment;
            }
            return "";
        }

        /// <summary>
        /// 保留兼容接口，但不再强制全量加载
        /// </summary>
        public static void EnsureLoaded()
        {
            // 懒加载模式下无需预加载
            EnsureSourceFileMap();
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
            _sourceFileMap = null;
        }

        private static EnumMemberInfo[] LoadEnumMembers(Type enumType)
        {
            EnsureSourceFileMap();

            string typeName = enumType.Name;
            string? sourceFile = null;
            if (_sourceFileMap != null && _sourceFileMap.TryGetValue(typeName, out var path))
                sourceFile = path;

            if (sourceFile == null || !File.Exists(sourceFile))
                return BuildFromReflection(enumType);

            var rawNames = Enum.GetNames(enumType);
            var rawValues = Enum.GetValues(enumType);
            var isFlags = enumType.GetCustomAttribute<FlagsAttribute>() != null;
            var comments = CsCommentParser.ParseEnumComments(sourceFile);

            var result = new EnumMemberInfo[rawNames.Length];
            for (int i = 0; i < rawNames.Length; i++)
            {
                string comment = comments.TryGetValue(rawNames[i], out var c) ? c : "";
                result[i] = new EnumMemberInfo
                {
                    Name = rawNames[i],
                    Comment = comment,
                    IsFlags = isFlags,
                    Value = Convert.ToInt32(rawValues.GetValue(i)),
                };
            }
            return result;
        }

        /// <summary>
        /// 一次性扫描项目建立 文件名→绝对路径 映射
        /// 替代原先每个枚举类型都全盘扫描的做法
        /// </summary>
        private static void EnsureSourceFileMap()
        {
            if (_sourceFileMap != null) return;

            _sourceFileMap = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                string projectRoot = ProjectSettings.GlobalizePath("res://");
                if (!Directory.Exists(projectRoot)) return;

                // 仅扫描 Src 和 Data 目录，避免扫描 .godot/addons 等
                var scanDirs = new[] { "Src", "Data" };
                foreach (var dir in scanDirs)
                {
                    string fullDir = Path.Combine(projectRoot, dir);
                    if (!Directory.Exists(fullDir)) continue;

                    foreach (var file in Directory.GetFiles(fullDir, "*.cs", SearchOption.AllDirectories))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        // 跳过 obj/bin/.godot
                        if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                            || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                            || file.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}"))
                            continue;

                        _sourceFileMap.TryAdd(fileName, file);
                    }
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[DataConfigEditor] EnumCommentCache 源文件映射构建失败: {e.Message}");
            }
        }

        private static EnumMemberInfo[] BuildFromReflection(Type enumType)
        {
            var rawNames = Enum.GetNames(enumType);
            var rawValues = Enum.GetValues(enumType);
            var isFlags = enumType.GetCustomAttribute<FlagsAttribute>() != null;
            var result = new EnumMemberInfo[rawNames.Length];
            for (int i = 0; i < rawNames.Length; i++)
            {
                result[i] = new EnumMemberInfo
                {
                    Name = rawNames[i],
                    Comment = "",
                    IsFlags = isFlags,
                    Value = Convert.ToInt32(rawValues.GetValue(i)),
                };
            }
            return result;
        }
    }
}
#endif
