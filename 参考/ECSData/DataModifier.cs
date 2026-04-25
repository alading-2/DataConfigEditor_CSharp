/// <summary>
/// 修改器类型枚举
/// 完整公式：
///   step1 = (base + Σ[Additive]) × Π[Multiplicative] + Σ[FinalAdditive]
///   step2 = Override 存在时，取优先级最高（Priority 最小）的 Override 值
///   step3 = Cap 存在时，result = min(result, min(all Cap values))
///   step4 = Meta.Clamp 约束 [MinValue, MaxValue]
/// </summary>
public enum ModifierType
{
    /// <summary>
    /// 加法修改器：叠加到基础值（在乘法前计算）
    /// e.g. 基础攻击 100 + Additive(50) = 150，再乘以乘法系数
    /// </summary>
    Additive,

    /// <summary>
    /// 乘法修改器：系数相乘（在加法后计算）
    /// e.g. Multiplicative(1.2) × Multiplicative(1.1) = ×1.32
    /// </summary>
    Multiplicative,

    /// <summary>
    /// 后置加法修改器：在乘法之后再加（最终值 = step1 + Σ[FinalAdditive]）
    /// 适合"固定数值增益"不受乘法放大的场景，e.g. +20 物理穿透（不随攻击力系数放大）
    /// </summary>
    FinalAdditive,

    /// <summary>
    /// 覆盖修改器：无视其他所有修改器，强制设为此值
    /// 多个 Override 时取 Priority 最小（优先级最高）的值
    /// 适合"锁定属性"场景，e.g. 被冰冻时移速强制为 0
    /// </summary>
    Override,

    /// <summary>
    /// 上限修改器：限制最终结果不超过此值（多个 Cap 取最小值）
    /// 在所有修改器计算完毕后、Meta.Clamp 之前生效
    /// e.g. 暴击率上限 75%（取最严格的 Cap）
    /// </summary>
    Cap,
}

/// <summary>
/// 数据修改器 - 用于 Buff/Debuff 系统
/// 公式：最终值 = (基础值 + Σ加法) × Π乘法
/// </summary>
public class DataModifier
{
    /// <summary>
    /// 修改器唯一标识符
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// 修改器类型（加法/乘法）
    /// </summary>
    public ModifierType Type { get; init; }

    /// <summary>
    /// 修改值
    /// 加法类型：直接加到基础值
    /// 乘法类型：作为乘数（1.0 = 100%，1.5 = 150%）
    /// </summary>
    public float Value { get; init; }

    /// <summary>
    /// 计算优先级（数值越小越先计算）
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// 修改器来源对象（例如：装备 Entity、Buff 实例）
    /// 用于按来源批量移除修改器
    /// </summary>
    public object? Source { get; init; }

    /// <summary>
    /// 创建数据修改器
    /// </summary>
    /// <param name="type">修改器类型</param>
    /// <param name="value">修改值</param>
    /// <param name="priority">优先级（默认 0）</param>
    /// <param name="id">唯一标识符（默认自动生成）</param>
    /// <param name="source">来源对象（可选）</param>
    public DataModifier(ModifierType type, float value, int priority = 0, string? id = null, object? source = null)
    {
        Id = id ?? System.Guid.NewGuid().ToString();
        Type = type;
        Value = value;
        Priority = priority;
        Source = source;
    }
}
