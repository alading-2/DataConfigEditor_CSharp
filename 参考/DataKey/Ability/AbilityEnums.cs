using System;

/// <summary>
/// 技能类型
/// </summary>
public enum AbilityType
{
    /// <summary>主动技能 - 需要玩家手动触发，可能有充能</summary>
    Active = 0,

    /// <summary>被动技能</summary>
    Passive = 1,

    /// <summary>武器技能</summary>
    Weapon = 2,
}

/// <summary>
/// 技能触发模式 - [Flags] 位运算，支持多种触发同时生效
/// </summary>
[Flags]
public enum AbilityTriggerMode
{
    None = 0,

    // ============ 主动技能触发 ============
    /// <summary>手动触发 - 需要玩家按键输入施放技能</summary>
    Manual = 1 << 0,

    // ============ 被动技能触发 ============
    /// <summary>事件触发 - 监听特定事件 (如受击、击杀)</summary>
    OnEvent = 1 << 1,

    /// <summary>周期触发 - 固定时间间隔 (如光环每0.5秒)</summary>
    Periodic = 1 << 2,

    /// <summary>永久生效 - 无触发概念 (如属性加成)</summary>
    Permanent = 1 << 3,


    // ============ 组合预设 ============
    /// <summary>事件 + 周期 (如反伤光环)</summary>
    EventAndPeriodic = OnEvent | Periodic,

    /// <summary>手动 + 事件 (如条件主动技能)</summary>
    ManualAndEvent = Manual | OnEvent,
}

/// <summary>
/// 目标选取 - 决定从哪里开始选取目标
/// </summary>
public enum AbilityTargetSelection
{
    /// <summary>无目标（直接使用）</summary>
    None = 0,

    /// <summary>指定Entity</summary>
    Entity = 1,

    /// <summary>指定地点</summary>
    Point = 2,

    /// <summary>Entity/地点</summary>
    EntityOrPoint = 3,
}

/// <summary>
/// 技能执行模式 - 决定技能如何执行效果
/// </summary>
public enum AbilityExecutionMode
{
    /// <summary>即时执行 - 一次性对所有目标生效</summary>
    Instant = 0,

    /// <summary>链式弹跳 - 延时逐个弹跳到多个目标</summary>
    Chain = 1,

    /// <summary>持续施法 - 引导技能</summary>
    Channel = 2,

    /// <summary>投射物 - 发射弹道</summary>
    Projectile = 3,
}

/// <summary>
/// 技能消耗类型
/// </summary>
public enum AbilityCostType
{
    /// <summary>无消耗</summary>
    None = 0,

    /// <summary>魔法值</summary>
    Mana = 1,

    /// <summary>能量</summary>
    Energy = 2,

    /// <summary>弹药</summary>
    Ammo = 3,

    /// <summary>生命值</summary>
    Health = 4
}

/// <summary>
/// 技能激活结果
/// </summary>
public enum AbilityActivateResult
{
    /// <summary>成功</summary>
    Success = 0,

    /// <summary>已在执行中</summary>
    FailHasActivated = 1,

    /// <summary>标签条件不满足</summary>
    FailTagRequirement = 2,

    /// <summary>消耗不足</summary>
    FailCost = 3,

    /// <summary>冷却中</summary>
    FailCooldown = 4,

    /// <summary>无充能</summary>
    FailNoCharge = 5,

    /// <summary>无目标</summary>
    FailNoTarget = 6,
}