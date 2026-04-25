using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Godot;

/// <summary>
/// 增强版动态数据容器 - 统一数据管理系统
/// 支持：强类型访问、元数据约束、修改器系统、计算数据、事件监听
/// 
/// 【核心理念】
/// Data 是唯一数据源，所有数据（普通数据、可修改数据、计算数据）统一从 Data 容器访问。
/// 
/// 【公式】
/// 最终值 = (基础值 + Σ加法修改器) × Π乘法修改器
/// </summary>
public class Data
{
    private static readonly Log _log = new(nameof(Data), LogLevel.Warning);

    private readonly IEntity? _owner;

    public Data(IEntity? owner = null)
    {
        _owner = owner;
    }

    /// <summary>
    /// 内部存储基础数据的字典
    /// </summary>
    private readonly Dictionary<string, object> _data = new();

    /// <summary>
    /// 修改器字典：Key -> 修改器列表
    /// </summary>
    private readonly Dictionary<string, List<DataModifier>> _modifiers = new();

    /// <summary>
    /// 最终值缓存字典
    /// </summary>
    private readonly Dictionary<string, object> _cachedValues = new();

    /// <summary>
    /// 脏标记集合（需要重新计算的键）
    /// </summary>
    private readonly HashSet<string> _dirtyKeys = new();

    /// <summary>
    /// 当任何数据发生变化时触发的全局事件
    /// 参数依次为：键名 (Key), 旧值 (OldValue), 新值 (NewValue)
    /// </summary>
    // 事件监听移交给 Entity.Events


    // ================= 基础数据操作 =================

    /// <summary>
    /// 设置基础值（自动应用元数据约束）
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="key">键名</param>
    /// <param name="value">要设置的新值</param>
    /// <returns>如果值发生了实际变化则返回 true</returns>
    public bool Set<T>(string key, T value)
    {
        // 应用元数据约束
        var meta = DataRegistry.GetMeta(key);
        object finalValue = value!;
        if (meta != null)
        {
            // 选项验证
            if (meta.HasOptions && !meta.IsValidOption(value!))
            {
                _log.Error($"无效的选项值: {key} = {value}");
                return false;
            }
            finalValue = meta.Clamp(value!);
        }

        object? oldValue = null;
        if (_data.TryGetValue(key, out var existing))
        {
            oldValue = existing;
            if (Equals(existing, finalValue))
            {
                return false;
            }
        }

        _data[key] = finalValue;
        MarkDirty(key);
        NotifyChanged(key, oldValue, finalValue);
        return true;
    }

    /// <summary>
    /// 获取最终值（泛型访问，编译期类型安全）
    /// 核心逻辑：统一处理计算数据、修改器和基础值
    /// </summary>
    /// <typeparam name="T">期望获取的类型</typeparam>
    /// <param name="key">键名</param>
    /// <param name="defaultValue">默认值（可选）。如果未提供，将使用 DataMeta 中注册的默认值</param>
    /// <returns>最终计算值</returns>
    public T Get<T>(string key, object? defaultValue = null)
    {
        // ── 快速路径：未注册键直接查字典，零约束开销 ──────────────────────
        var meta = DataRegistry.GetMeta(key);
        if (meta == null)
        {
            object fallback = defaultValue ?? DataMeta.GetTypeDefaultValue(typeof(T));
            return _data.TryGetValue(key, out var rawValue) && rawValue != null
                ? (T)ConvertValueBoxed(rawValue, typeof(T), fallback)
                : (T)fallback;
        }

        // ── 计算键：最高优先级 ────────────────────────────────────────────
        if (meta.IsComputed)
        {
            object computedFallback = defaultValue ?? meta.GetDefaultValue();
            return (T)GetComputedValueBoxed(key, meta, computedFallback, typeof(T));
        }

        // ── 普通键：查基础值 ──────────────────────────────────────────────
        object effectiveDefault = defaultValue ?? meta.GetDefaultValue();
        if (!_data.TryGetValue(key, out var baseValue) || baseValue == null)
            return (T)effectiveDefault;

        // ── 属性键：有修改器时才进入修改器路径（避免无效进入） ────────────
        if (meta.SupportModifiers == true && _modifiers.ContainsKey(key))
            return (T)GetModifiedValueBoxed(key, baseValue, effectiveDefault, typeof(T));

        return (T)ConvertValueBoxed(baseValue, typeof(T), effectiveDefault);
    }

    /// <summary>
    /// 获取基础值（不应用修改器，用于计算数据内部调用）
    /// </summary>
    /// <typeparam name="T">期望获取的类型</typeparam>
    /// <param name="key">键名</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>基础值</returns>
    public T GetBase<T>(string key, T defaultValue = default!)
    {
        if (_data.TryGetValue(key, out var value) && value != null)
        {
            return (T)ConvertValueBoxed(value, typeof(T), defaultValue!);
        }
        return defaultValue;
    }

    /// <summary>
    /// 尝试获取数据值
    /// </summary>
    public bool TryGetValue<T>(string key, out T value)
    {
        var result = Get<T>(key);
        if (result != null && !result.Equals(default(T)))
        {
            value = result;
            return true;
        }
        value = default!;
        return _data.ContainsKey(key);
    }

    /// <summary>
    /// 检查是否存在指定的键名
    /// </summary>
    public bool Has(string key)
    {
        return _data.ContainsKey(key) || DataRegistry.IsComputed(key);
    }

    /// <summary>
    /// 移除指定的数据项
    /// </summary>
    public bool Remove(string key)
    {
        if (_data.TryGetValue(key, out var oldValue))
        {
            _data.Remove(key);
            _modifiers.Remove(key);
            _cachedValues.Remove(key);
            _dirtyKeys.Remove(key);
            NotifyChanged(key, oldValue, null);
            return true;
        }
        return false;
    }

    // ================= 算术运算 =================

    /// <summary>
    /// 对现有数值执行加法操作
    /// </summary>
    public void Add<T>(string key, T delta) where T : INumber<T>
    {
        var current = GetBase<T>(key, T.Zero);
        Set(key, current + delta);
    }

    /// <summary>
    /// 对现有数值执行乘法操作
    /// </summary>
    public void Multiply<T>(string key, T factor) where T : INumber<T>
    {
        var current = GetBase<T>(key, T.Zero);
        Set(key, current * factor);
    }

    /// <summary>
    /// 批量设置多个数据项
    /// </summary>
    public void SetMultiple(Dictionary<string, object> properties)
    {
        foreach (var kvp in properties)
        {
            Set(kvp.Key, kvp.Value);
        }
    }

    // ================= 修改器系统 =================

    /// <summary>
    /// 添加修改器
    /// </summary>
    /// <param name="key">目标数据键</param>
    /// <param name="modifier">修改器实例</param>
    public void AddModifier(string key, DataModifier modifier)
    {
        if (!DataRegistry.SupportModifiers(key))
        {
            _log.Warn($"数据 '{key}' 不支持修改器，已忽略");
            return;
        }

        if (!_modifiers.TryGetValue(key, out var list))
            _modifiers[key] = list = new List<DataModifier>();

        // 检查 ID 冲突
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == modifier.Id)
            {
                _log.Warn($"ID 为 '{modifier.Id}' 的修改器已存在于 '{key}'，已忽略");
                return;
            }
        }

        // 插入时维护 Priority 有序（替代每次 Get 时 OrderBy）
        int insertIndex = list.BinarySearch(modifier, ModifierPriorityComparer.Instance);
        if (insertIndex < 0) insertIndex = ~insertIndex;
        list.Insert(insertIndex, modifier);

        MarkDirty(key);
        _log.Debug($"添加修改器: {modifier.Id} ({modifier.Type} {modifier.Value}) -> {key}");

        var finalValue = Get<float>(key);
        NotifyChanged(key, null, finalValue);
    }

    /// <summary>
    /// 移除修改器
    /// </summary>
    /// <param name="key">目标数据键</param>
    /// <param name="modifierId">修改器 ID</param>
    public void RemoveModifier(string key, string modifierId)
    {
        if (_modifiers.TryGetValue(key, out var modifiers))
        {
            var removed = modifiers.RemoveAll(m => m.Id == modifierId);
            if (removed > 0)
            {
                MarkDirty(key);
                _log.Debug($"移除修改器: {modifierId} <- {key}");

                var finalValue = Get<float>(key);
                NotifyChanged(key, null, finalValue);
            }
        }
    }

    /// <summary>
    /// 根据 ID 移除所有匹配的修改器（跨所有数据键）
    /// </summary>
    /// <param name="modifierId">修改器 ID</param>
    public void RemoveModifierById(string modifierId)
    {
        foreach (var key in _modifiers.Keys.ToList())
        {
            RemoveModifier(key, modifierId);
        }
    }

    /// <summary>
    /// 根据来源对象移除所有匹配的修改器（跨所有数据键）
    /// 常用于：卸载装备、移除 Buff
    /// </summary>
    /// <param name="source">来源对象（如 ItemEntity）</param>
    public void RemoveModifiersBySource(object source)
    {
        if (source == null) return;

        foreach (var key in _modifiers.Keys.ToList())
        {
            if (_modifiers.TryGetValue(key, out var modifiers))
            {
                var removedCount = modifiers.RemoveAll(m => m.Source == source);
                if (removedCount > 0)
                {
                    MarkDirty(key);
                    _log.Debug($"移除来源为 {source} 的修改器: {removedCount} 个 <- {key}");

                    var finalValue = Get<float>(key);
                    NotifyChanged(key, null, finalValue);
                }
            }
        }
    }

    /// <summary>
    /// 将另一个 Data 容器的数据转换为修改器应用到当前容器
    /// 常用于：装备属性应用到角色
    /// </summary>
    /// <param name="sourceData">源数据容器</param>
    /// <param name="sourceEntity">来源实体（作为修改器 Source）</param>
    public void ApplyDataAsModifiers(Data sourceData, object sourceEntity)
    {
        if (sourceData == null || sourceEntity == null) return;

        var allData = sourceData.GetAll();
        foreach (var kvp in allData)
        {
            // 仅处理数值类型
            if (kvp.Value is float || kvp.Value is int)
            {
                float value = Convert.ToSingle(kvp.Value);
                if (Math.Abs(value) < float.Epsilon) continue;

                // 检查目标是否支持修改器
                if (DataRegistry.SupportModifiers(kvp.Key))
                {
                    var modifier = new DataModifier(
                        ModifierType.Additive,
                        value,
                        priority: 0,
                        source: sourceEntity
                    );
                    AddModifier(kvp.Key, modifier);
                }
            }
        }
    }

    /// <summary>
    /// 检查是否拥有特定修改器
    /// </summary>
    public bool HasModifier(string key, string modifierId)
    {
        return _modifiers.TryGetValue(key, out var modifiers) &&
               modifiers.Any(m => m.Id == modifierId);
    }

    /// <summary>
    /// 获取指定数据键的所有修改器副本
    /// 返回副本是为了确保迭代安全性，防止在遍历时修改器列表发生变动导致异常
    /// </summary>
    public List<DataModifier> GetModifiers(string key)
    {
        return _modifiers.TryGetValue(key, out var modifiers)
            ? new List<DataModifier>(modifiers)
            : new List<DataModifier>();
    }

    /// <summary>
    /// 清除指定数据键的所有修改器
    /// </summary>
    public void ClearModifiers(string key)
    {
        if (_modifiers.TryGetValue(key, out var modifiers) && modifiers.Count > 0)
        {
            modifiers.Clear();
            MarkDirty(key);
            _log.Debug($"清除所有修改器: {key}");
        }
    }

    /// <summary>
    /// 清除所有修改器
    /// </summary>
    public void ClearAllModifiers()
    {
        foreach (var key in _modifiers.Keys.ToList())
        {
            ClearModifiers(key);
        }
        _modifiers.Clear();
    }

    // ================= 事件监听 (已移除) =================
    // 请使用 Entity.Events.On(GameEventType.Data.PropertyChanged, ...)
    // 数据变更事件负载类型: (string Key, object? OldValue, object? NewValue)


    // ================= 工具方法 =================

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void Clear()
    {
        var keys = new List<string>(_data.Keys);
        foreach (var key in keys)
        {
            Remove(key);
        }
        _modifiers.Clear();
        _cachedValues.Clear();
        _dirtyKeys.Clear();
    }

    /// <summary>
    /// 获取当前所有基础数据的副本
    /// </summary>
    public Dictionary<string, object> GetAll()
    {
        return new Dictionary<string, object>(_data);
    }

    /// <summary>
    /// 反射属性缓存：Type → (PropertyInfo, key)[]，首次访问时构建，后续复用
    /// </summary>
    private static readonly Dictionary<Type, (PropertyInfo prop, string key)[]> _resourcePropCache = new();

    /// <summary>
    /// 从 Resource 加载数据到容器（反射结果静态缓存，波次刷怪时无额外反射开销）
    /// </summary>
    public void LoadFromResource(Resource resource)
    {
        if (resource == null) return;

        var type = resource.GetType();
        if (!_resourcePropCache.TryGetValue(type, out var cached))
            cached = _resourcePropCache[type] = BuildPropertyCache(type);

        foreach (var (prop, key) in cached)
        {
            try
            {
                var value = prop.GetValue(resource);
                if (value != null) Set(key, value);
            }
            catch (Exception ex)
            {
                _log.Warn($"加载属性 {prop.Name} 失败: {ex.Message}");
            }
        }
    }

    private static (PropertyInfo, string)[] BuildPropertyCache(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead
                && p.DeclaringType != typeof(Resource)
                && p.DeclaringType != typeof(Godot.RefCounted)
                && p.DeclaringType != typeof(Godot.GodotObject))
            .Select(p =>
            {
                var attr = p.GetCustomAttribute<DataKeyAttribute>();
                return (p, attr?.Key ?? p.Name);
            })
            .ToArray();
    }

    /// <summary>
    /// 按数据分类批量重置为默认值（仅重置容器中已存在的键）
    /// <para>
    /// 遍历该 Category 下所有已注册的 DataMeta，若容器中存在对应键则将其设为 DefaultValue。
    /// 使用 DataRegistry 的缓存查询，适合高频调用。
    /// </para>
    /// </summary>
    /// <param name="category">数据分类枚举值（如 DataCategory_Movement.Orbit）</param>
    public void ResetByCategory(Enum category)
    {
        var metas = DataRegistry.GetCachedMetaByCategory(category);
        for (int i = 0; i < metas.Length; i++)
        {
            var meta = metas[i];
            if (_data.ContainsKey(meta.Key))
            {
                Set(meta.Key, meta.GetDefaultValue());
            }
        }
    }

    /// <summary>
    /// 按多个数据分类批量重置为默认值
    /// </summary>
    /// <param name="categories">要重置的分类列表</param>
    public void ResetByCategories(params Enum[] categories)
    {
        for (int i = 0; i < categories.Length; i++)
        {
            ResetByCategory(categories[i]);
        }
    }

    /// <summary>
    /// 重置数据容器（用于对象池复用）
    /// </summary>
    public void Reset()
    {
        _data.Clear();
        _modifiers.Clear();
        _cachedValues.Clear();
        _dirtyKeys.Clear();
        // 注意：不清除监听器，由外部管理
        _log.Debug("Data 容器已重置");
    }

    // ================= 私有方法 =================

    /// <summary>
    /// 获取计算数据的值（带缓存逻辑）
    /// </summary>
    private object GetComputedValueBoxed(string key, DataMeta meta, object defaultValue, Type targetType)
    {
        // 1. 检查缓存：如果该键不是“脏”的，且缓存中存在值，则直接返回
        if (!_dirtyKeys.Contains(key) && _cachedValues.TryGetValue(key, out var cached))
        {
            if (cached == null) return defaultValue;
            return ConvertValueBoxed(cached, targetType, defaultValue);
        }

        // 2. 缓存失效或不存在，调用计算逻辑
        var result = meta.Compute(this);

        // 3. 更新缓存并移除脏标记
        _cachedValues[key] = result;
        _dirtyKeys.Remove(key);

        if (result == null) return defaultValue;
        return ConvertValueBoxed(result, targetType, defaultValue);
    }

    /// <summary>
    /// 获取应用修改器后的最终值（带缓存逻辑）
    /// </summary>
    private object GetModifiedValueBoxed(string key, object baseValue, object defaultValue, Type targetType)
    {
        // 1. 检查缓存：修改器变动或基础值变动会标记为脏
        if (!_dirtyKeys.Contains(key) && _cachedValues.TryGetValue(key, out var cached))
        {
            if (cached == null) return defaultValue;
            return ConvertValueBoxed(cached, targetType, defaultValue);
        }

        // 2. 核心计算：将基础值（如 float）应用所有已注册的修改器
        float baseFloat = Convert.ToSingle(baseValue);
        float finalValue = CalculateFinalValue(key, baseFloat);

        // 3. 更新缓存
        _cachedValues[key] = finalValue;
        _dirtyKeys.Remove(key);

        return ConvertValueBoxed(finalValue, targetType, defaultValue);
    }

    /// <summary>
    /// 修改器核心算法（无 LINQ，预排序列表直接遍历）
    /// 公式：step1 = (base + Σ[Additive]) × Π[Multiplicative] + Σ[FinalAdditive]
    ///       step2 = Override 存在时取最高优先级值
    ///       step3 = Cap 存在时取 min(step, cap)
    ///       step4 = Meta.Clamp 约束
    /// </summary>
    private float CalculateFinalValue(string key, float baseValue)
    {
        if (!_modifiers.TryGetValue(key, out var list) || list.Count == 0)
            return baseValue;

        float additive = 0f;
        float multiplicative = 1f;
        float finalAdditive = 0f;
        float? overrideValue = null;
        float? cap = null;

        foreach (var m in list)  // list 在 AddModifier 时已维护 Priority 有序
        {
            switch (m.Type)
            {
                case ModifierType.Additive: additive += m.Value; break;
                case ModifierType.Multiplicative: multiplicative *= m.Value; break;
                case ModifierType.FinalAdditive: finalAdditive += m.Value; break;
                case ModifierType.Override:
                    if (!overrideValue.HasValue) overrideValue = m.Value;  // 取最高优先级（Priority最小）
                    break;
                case ModifierType.Cap:
                    cap = cap.HasValue ? Math.Min(cap.Value, m.Value) : m.Value;
                    break;
            }
        }

        float result = overrideValue.HasValue
            ? overrideValue.Value
            : (baseValue + additive) * multiplicative + finalAdditive;

        if (cap.HasValue) result = Math.Min(result, cap.Value);

        var meta = DataRegistry.GetMeta(key);
        return meta != null ? (float)meta.Clamp(result) : result;
    }

    /// <summary>
    /// 标记数据及其依赖项为“脏”（Dirty）
    /// 当基础值改变或修改器增删时调用，确保下次获取时重新计算
    /// </summary>
    private void MarkDirty(string key)
    {
        // 1. 标记当前键为脏
        _dirtyKeys.Add(key);
        _cachedValues.Remove(key);

        // 2. 级联标记：查找所有依赖于此数据的计算数据 (ComputedData)
        // 例如：若 Damage 改变，则依赖它的 DPS 缓存也必须失效
        var dependents = DataRegistry.GetDependentComputedKeys(key);
        foreach (var depKey in dependents)
        {
            _dirtyKeys.Add(depKey);
            _cachedValues.Remove(depKey);
        }
    }

    /// <summary>
    /// 触发变更通知
    /// </summary>
    private void NotifyChanged(string key, object? oldValue, object? newValue)
    {
        if (_owner != null)
        {
            // 通过 Entity 事件总线广播数据变更
            // 下游监听示例: 
            // entity.Events.On<GameEventType.Data.PropertyChangedEvent>(GameEventType.Data.PropertyChanged, evt => ...);
            _owner.Events.Emit(GameEventType.Data.PropertyChanged, new GameEventType.Data.PropertyChangedEventData(key, oldValue, newValue));
        }
    }

    /// <summary>
    /// 修改器优先级比较器（Priority 越小越靠前 = 优先级越高）
    /// </summary>
    private sealed class ModifierPriorityComparer : IComparer<DataModifier>
    {
        public static readonly ModifierPriorityComparer Instance = new();
        public int Compare(DataModifier? x, DataModifier? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            return x.Priority.CompareTo(y.Priority);
        }
    }

    /// <summary>
    /// 类型转换辅助方法
    /// </summary>
    private object ConvertValueBoxed(object value, Type targetType, object defaultValue)
    {
        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return defaultValue;
        }
    }
}
