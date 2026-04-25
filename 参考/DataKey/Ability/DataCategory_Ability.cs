/// <summary>
/// 技能属性分类枚举 - 用于 UI 展示和数据组织
/// </summary>
public enum DataCategory_Ability
{
    /// <summary>
    /// 基础信息（描述、图标、等级等）
    /// </summary>
    Basic,

    /// <summary>
    /// 冷却系统（冷却时间、状态）
    /// </summary>
    Cooldown,

    /// <summary>
    /// 充能系统（充能次数、时间）
    /// </summary>
    Charge,

    /// <summary>
    /// 消耗系统（消耗类型、数量）
    /// </summary>
    Cost,

    /// <summary>
    /// 触发配置（模式、事件、概率）
    /// </summary>
    Trigger,

    /// <summary>
    /// 目标配置（类型、数量）
    /// </summary>
    Target,

    /// <summary>
    /// 效果参数（伤害、治疗、范围等）
    /// </summary>
    Effect,

    /// <summary>
    /// 标签系统（激活条件、互斥标签）
    /// </summary>
    Tag,

    /// <summary>
    /// 状态标记（解锁、启用、激活）
    /// </summary>
    State,

    /// <summary>
    /// 输入（当前激活技能索引）
    /// </summary>
    Input
}
