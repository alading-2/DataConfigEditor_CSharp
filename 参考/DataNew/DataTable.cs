using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Slime.ConfigNew;

/// <summary>
/// DataNew 纯 C# 表数据读取工具。
/// <para>表由 C# 类型表示，例如 EnemyData / AbilityData；行由 public static readonly 静态实例表示。</para>
/// <para>首次访问某个表类型时建立缓存，之后按 Name 字典读取。</para>
/// </summary>
public static class DataTable
{
    private static readonly Log _log = new(nameof(DataTable));

    /// <summary>
    /// 获取指定表类型的全部数据行。
    /// </summary>
    /// <typeparam name="T">表类型，例如 EnemyData。</typeparam>
    /// <returns>全部数据行，只读列表。</returns>
    public static IReadOnlyList<T> GetAll<T>() where T : class
    {
        return Cache<T>.All;
    }

    /// <summary>
    /// 按数据 Name 获取数据行。
    /// <para>找不到时记录 Error 并返回 null，由调用方使用 ?? 处理默认值或中断逻辑。</para>
    /// </summary>
    /// <typeparam name="T">表类型，例如 EnemyData。</typeparam>
    /// <param name="name">数据 Name，例如“鱼人”。</param>
    /// <returns>匹配的数据行；找不到返回 null。</returns>
    public static T? GetByName<T>(string name) where T : class
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _log.Error($"{typeof(T).Name} 查询 Name 为空");
            return null;
        }

        if (Cache<T>.ByName.TryGetValue(name, out var value))
        {
            return value;
        }

        _log.Error($"{typeof(T).Name} 未找到 Name='{name}' 的数据");
        return null;
    }

    /// <summary>
    /// 泛型缓存桶：每个表类型 T 首次访问时自动构建并缓存全部数据行和按 Name 索引。
    /// <para>利用 C# 泛型静态字段的每类型独立性，无需手动管理缓存字典。</para>
    /// </summary>
    private static class Cache<T> where T : class
    {
        /// <summary>当前表类型的全部数据行（按类型全名 + 字段声明顺序排列）。</summary>
        public static readonly IReadOnlyList<T> All = BuildAll();

        /// <summary>以 Name 为键的索引字典（序数字符串比较）。</summary>
        public static readonly Dictionary<string, T> ByName = BuildByName(All);

        /// <summary>
        /// 扫描程序集中所有 T 的非抽象子类，收集其 public static readonly 字段值作为数据行。
        /// <para>排序：先按类型全名排列，再按字段声明顺序（MetadataToken）排列，保证结果稳定。</para>
        /// </summary>
        private static IReadOnlyList<T> BuildAll()
        {
            var result = new List<T>();
            var tableType = typeof(T);

            // 找到程序集中所有 T 的非抽象子类，按全名排序
            var dataTypes = tableType.Assembly.GetTypes()
                .Where(type => tableType.IsAssignableFrom(type) && !type.IsAbstract)
                .OrderBy(type => type.FullName, StringComparer.Ordinal);

            foreach (var dataType in dataTypes)
            {
                // 收集该子类中所有 T 类型的 public static 字段，按声明顺序排列
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => tableType.IsAssignableFrom(field.FieldType))
                    .OrderBy(field => field.MetadataToken);

                foreach (var field in fields)
                {
                    // 读取静态字段值（null 传入因为字段是 static）
                    if (field.GetValue(null) is T value)
                    {
                        result.Add(value);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 根据全部数据行构建 Name 索引字典。
        /// <para>跳过 Name 为空的行并记录错误；重复 Name 也跳过并记录错误。</para>
        /// </summary>
        private static Dictionary<string, T> BuildByName(IReadOnlyList<T> all)
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal); // 序数字符串比较，保证一致性
            for (int i = 0; i < all.Count; i++)
            {
                var data = all[i];
                var name = ResolveName(data); // 按 Name > SystemId > PresetName 优先级解析
                if (string.IsNullOrWhiteSpace(name))
                {
                    _log.Error($"{typeof(T).Name} 存在 Name 为空的数据: {data.GetType().Name}");
                    continue;
                }

                if (result.ContainsKey(name))
                {
                    _log.Error($"{typeof(T).Name} 存在重复 Name='{name}'，请保证同一张表内 Name 唯一");
                    continue;
                }

                result.Add(name, data);
            }

            return result;
        }

        /// <summary>
        /// 解析数据行的名称标识。
        /// <para>按优先级依次尝试：DataKey.Name → SystemId → PresetName，返回首个非空属性值。</para>
        /// </summary>
        private static string? ResolveName(T data)
        {
            var type = data.GetType();
            var property = type.GetProperty(DataKey.Name, BindingFlags.Public | BindingFlags.Instance) // 优先使用 DataKey.Name
                ?? type.GetProperty("SystemId", BindingFlags.Public | BindingFlags.Instance) // 兼容旧字段 SystemId
                ?? type.GetProperty("PresetName", BindingFlags.Public | BindingFlags.Instance); // 兼容旧字段 PresetName
            return property?.GetValue(data) as string;
        }
    }
}
