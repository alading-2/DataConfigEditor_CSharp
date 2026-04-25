using System;
using System.Collections.Generic;

/// <summary>
/// 数据元数据 - 运行时约束字段 + 展示字段合一
/// 运行时约束（Get/Set 热路径）：Key/Type/DefaultValue/MinValue/MaxValue/SupportModifiers/Compute
/// 展示字段（UI/编辑器）：DisplayName/Description/Category/IsPercentage/IconPath
/// </summary>
public class DataMeta
{
    // === 必填字段 ===

    /// <summary>数据键名（必填）</summary>
    public required string Key { get; init; }

    /// <summary>显示名称（用于 UI 标签）</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>数据类型（必填）</summary>
    public required Type Type { get; init; }

/// <summary>描述信息（用于 Tooltip 等）</summary>
public string Description { get; init; } = "";

/// <summary>数据分类（用于属性面板分组）</summary>
public Enum? Category { get; init; }

// === 可选约束字段 ===

/// <summary>默认值（可选，根据 Type 自动推断）</summary>
public object? DefaultValue { get; init; }

/// <summary>最小值（可选，仅数值类型有效）</summary>
public float? MinValue { get; init; }

/// <summary>最大值（可选，仅数值类型有效）</summary>
public float? MaxValue { get; init; }

/// <summary>是否为百分比值（影响格式化输出）</summary>
public bool IsPercentage { get; init; } = false;

/// <summary>是否支持修改器（可选，属性键设 true，状态键默认 false）</summary>
public bool? SupportModifiers { get; init; }

/// <summary>是否允许被 Entity 迁移流程复制到目标实体（默认允许）</summary>
public bool CanMigrate { get; init; } = true;

/// <summary>图标路径（可选）</summary>
public string IconPath { get; init; } = "";

/// <summary>可选值列表（用于运行时选项验证，索引对应实际存储的 int 值）</summary>
public List<string>? Options { get; init; }

// === 计算属性支持 ===

/// <summary>依赖的数据键列表（仅计算键使用）</summary>
public string[]? Dependencies { get; init; }

/// <summary>计算函数（仅计算键使用）</summary>
public Func<Data, object>? Compute { get; init; }

// ==============================================

/// <summary>是否为数值类型</summary>
public bool IsNumeric => Type == typeof(int) || Type == typeof(float) || Type == typeof(double);

/// <summary>是否为整数类型</summary>
public bool IsInteger => Type == typeof(int) || Type == typeof(long) || Type == typeof(short);

/// <summary>是否为浮点类型</summary>
public bool IsFloatingPoint => Type == typeof(float) || Type == typeof(double);

/// <summary>是否为布尔类型</summary>
public bool IsBoolean => Type == typeof(bool);

/// <summary>是否为字符串类型</summary>
public bool IsString => Type == typeof(string);

/// <summary>是否为枚举类型</summary>
public bool IsEnum => Type.IsEnum;

/// <summary>是否为值类型</summary>
public bool IsValueType => Type.IsValueType;

/// <summary>是否为引用类型</summary>
public bool IsReferenceType => !Type.IsValueType;

/// <summary>是否为计算属性</summary>
public bool IsComputed => Compute != null;

/// <summary>是否有选项约束（用于运行时验证）</summary>
public bool HasOptions => Options != null && Options.Count > 0;

// ==============================================

/// <summary>实际默认值（智能推断）</summary>
public object GetDefaultValue()
{
    if (DefaultValue != null) return DefaultValue;
    return GetTypeDefaultValue(Type);
}

/// <summary>根据类型推断默认值（静态方法，无需 DataMeta 实例）</summary>
public static object GetTypeDefaultValue(Type type)
{
    if (type == typeof(int)) return 0;
    if (type == typeof(float)) return 0f;
    if (type == typeof(double)) return 0.0;
    if (type == typeof(bool)) return false;
    if (type == typeof(string)) return "";
    if (type.IsEnum) return Enum.GetValues(type).GetValue(0)!;
    if (type.IsValueType) return Activator.CreateInstance(type)!;
    return null!;
}

/// <summary>验证并将数值限制在 [MinValue, MaxValue] 范围内</summary>
public object Clamp(object value)
{
    if (!IsNumeric) return value;

    float numValue = Convert.ToSingle(value);

    if (MinValue.HasValue)
        numValue = Math.Max(numValue, MinValue.Value);

    if (MaxValue.HasValue)
        numValue = Math.Min(numValue, MaxValue.Value);

    if (Type == typeof(int)) return (int)numValue;
    if (Type == typeof(float)) return numValue;
    if (Type == typeof(double)) return (double)numValue;

    return value;
}

/// <summary>验证值是否在有效选项范围内（运行时约束校验）</summary>
public bool IsValidOption(object value)
{
    if (!HasOptions) return true;
    if (value is int idx)
        return idx >= 0 && idx < Options!.Count;
    return false;
}

/// <summary>格式化展示值（UI 用）</summary>
public string FormatValue(object value)
{
    if (IsNumeric)
    {
        float numValue = Convert.ToSingle(value);
        return IsPercentage ? $"{numValue:F1}%" : $"{numValue:F1}";
    }

    if (IsEnum)
        return value.ToString() ?? "";

    if (HasOptions && value is int idx)
    {
        var optionName = GetOptionName(idx);
        if (optionName != null) return optionName;
    }

    return value?.ToString() ?? "";
}

/// <summary>获取选项的显示名称</summary>
public string? GetOptionName(int index)
{
    if (!HasOptions || index < 0 || index >= Options!.Count)
        return null;
    return Options[index];
}

/// <summary>
/// 隐式转换为 string（返回 Key 值）
/// 使 DataMeta 字段可直接作为 Data.Get/Set 参数，无需显式 .Key 调用
/// </summary>
public static implicit operator string(DataMeta meta) => meta.Key;
}
