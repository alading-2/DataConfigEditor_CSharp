using Godot;
using System.Collections.Generic;

/// <summary>
/// 技能系统相关的 DataKey 定义
/// </summary>
public static partial class DataKey
{
    // ============ 基础信息 ============

    // 技能分组 ID（用于 UI / 测试面板展示；不再作为运行时处理器主索引）
    public static readonly DataMeta AbilityFeatureGroup = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityFeatureGroup), DisplayName = "技能分组", Category = DataCategory_Ability.Basic,
            Type = typeof(string), DefaultValue = ""
        });

    // 技能图标
    public static readonly DataMeta AbilityIcon = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityIcon), DisplayName = "技能图标", Category = DataCategory_Ability.Basic,
            Type = typeof(Texture2D), DefaultValue = null
        });

    /// <summary>
    /// 技能类型。
    /// </summary>
    public static readonly DataMeta AbilityType = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityType), DisplayName = "技能类型", Category = DataCategory_Ability.Basic,
            Type = typeof(AbilityType), DefaultValue = global::AbilityType.Passive
        });

    // 技能等级
    public static readonly DataMeta AbilityLevel = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityLevel), DisplayName = "技能等级", Category = DataCategory_Ability.Basic, Type = typeof(int),
            DefaultValue = 1, MinValue = 1
        });

    // 最大等级
    public static readonly DataMeta AbilityMaxLevel = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityMaxLevel), DisplayName = "技能最大等级", Category = DataCategory_Ability.Basic,
            Type = typeof(int), DefaultValue = 10, MinValue = 1
        });

    // ========================================
    // 技能相关 (Skill)
    // ========================================
    // 技能伤害
    public static readonly DataMeta AbilityDamage = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityDamage), DisplayName = "技能伤害", Category = DataCategory_Ability.Effect,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0, SupportModifiers = true
        });

    // 技能伤害百分比
    public static readonly DataMeta AbilityDamageBonus = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityDamageBonus), DisplayName = "技能伤害百分比", Description = "技能伤害百分比",
            Category = DataCategory_Attribute.Skill, Type = typeof(float), DefaultValue = 100f, MinValue = 0,
            IsPercentage = true, SupportModifiers = true
        });

    // 最终伤害（由基础技能伤害和伤害加成计算得到）
    public static readonly DataMeta FinalAbilityDamage = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(FinalAbilityDamage),
            DisplayName = "最终伤害",
            Description = "技能最终伤害",
            Category = DataCategory_Attribute.Computed,
            Type = typeof(float),
            DefaultValue = 0f,
            MinValue = 0,
            SupportModifiers = false,
            Dependencies = [nameof(AbilityDamage), nameof(AbilityDamageBonus)],
            Compute = (data) =>
            {
                float baseDamage = data.Get<float>(nameof(AbilityDamage));
                float bonus = data.Get<float>(nameof(AbilityDamageBonus));
                return MyMath.AttributeBonusCalculation(baseDamage, bonus);
            }
        });

    // 技能冷却缩减
    public static readonly DataMeta CooldownReduction = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(CooldownReduction), DisplayName = "技能冷却缩减", Description = "技能冷却缩减百分比",
            Category = DataCategory_Attribute.Skill, Type = typeof(float), DefaultValue = 0f, MinValue = 0,
            MaxValue = GlobalConfig.MaxCooldownReduction, IsPercentage = true, SupportModifiers = true
        });


    public static readonly DataMeta AbilityDamageInterval = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityDamageInterval), DisplayName = "伤害间隔", Category = DataCategory_Ability.Effect,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0, SupportModifiers = true
        });

    public static readonly DataMeta AbilityDamageDuration = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityDamageDuration), DisplayName = "伤害持续时间", Category = DataCategory_Ability.Effect,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0, SupportModifiers = true
        });

    public static readonly DataMeta AbilityRepeatHitSameTarget = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityRepeatHitSameTarget), DisplayName = "允许重复命中同一目标",
            Category = DataCategory_Ability.Effect, Type = typeof(bool), DefaultValue = true
        });

    public static readonly DataMeta AbilityApplyImmediateDamage = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityApplyImmediateDamage), DisplayName = "立即造成一次伤害", Category = DataCategory_Ability.Effect,
            Type = typeof(bool), DefaultValue = true
        });

    // ============ 冷却系统 ============
    // 冷却时间
    public static readonly DataMeta AbilityCooldown = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityCooldown), DisplayName = "冷却时间", Category = DataCategory_Ability.Cooldown,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0, SupportModifiers = true
        });

    // ============ 充能系统 ============
    // 是否使用充能
    public static readonly DataMeta IsAbilityUsesCharges = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(IsAbilityUsesCharges), DisplayName = "是否使用充能", Category = DataCategory_Ability.Charge,
            Type = typeof(bool), DefaultValue = false
        });

    // 最大充能
    public static readonly DataMeta AbilityMaxCharges = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityMaxCharges), DisplayName = "最大充能", Category = DataCategory_Ability.Charge,
            Type = typeof(int), DefaultValue = 0, MinValue = 0
        });

    // 当前充能
    public static readonly DataMeta AbilityCurrentCharges = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityCurrentCharges), DisplayName = "当前充能", Category = DataCategory_Ability.Charge,
            Type = typeof(int), DefaultValue = 0, MinValue = 0
        });

    // 充能时间
    public static readonly DataMeta AbilityChargeTime = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityChargeTime), DisplayName = "充能时间", Category = DataCategory_Ability.Charge,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0
        });

    // ============ 消耗系统 ============
    // 消耗类型
    public static readonly DataMeta AbilityCostType = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityCostType), DisplayName = "消耗类型", Category = DataCategory_Ability.Cost,
            Type = typeof(AbilityCostType), DefaultValue = global::AbilityCostType.None
        });

    // 消耗数量
    public static readonly DataMeta AbilityCostAmount = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityCostAmount), DisplayName = "消耗数量", Category = DataCategory_Ability.Cost,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0
        });

    // ============ 触发配置 ============
    // 触发模式
    public static readonly DataMeta AbilityTriggerMode = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityTriggerMode), DisplayName = "触发模式", Category = DataCategory_Ability.Trigger,
            Type = typeof(AbilityTriggerMode), DefaultValue = global::AbilityTriggerMode.None
        });

    // 触发事件
    public static readonly DataMeta AbilityTriggerEvent = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityTriggerEvent), DisplayName = "触发事件", Category = DataCategory_Ability.Trigger,
            Type = typeof(List<string>), DefaultValue = new List<string>()
        });

    // 触发概率
    public static readonly DataMeta AbilityTriggerChance = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityTriggerChance), DisplayName = "触发概率", Category = DataCategory_Ability.Trigger,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0f, MaxValue = 100f, IsPercentage = true
        });

    // ============ 执行模式 ============
    // 执行模式
    public static readonly DataMeta AbilityExecutionMode = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityExecutionMode), DisplayName = "执行模式", Category = DataCategory_Ability.Effect,
            Type = typeof(AbilityExecutionMode), DefaultValue = global::AbilityExecutionMode.Instant
        });

    // ============ 目标系统 ============
    // 目标原点
    public static readonly DataMeta AbilityTargetSelection = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityTargetSelection), DisplayName = "目标原点", Category = DataCategory_Ability.Target,
            Type = typeof(AbilityTargetSelection), DefaultValue = global::AbilityTargetSelection.None
        });

    // 目标几何形状
    public static readonly DataMeta AbilityTargetGeometry = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityTargetGeometry), DisplayName = "目标几何形状", Category = DataCategory_Ability.Target,
            Type = typeof(GeometryType), DefaultValue = GeometryType.Single
        });

    // 阵营过滤
    public static readonly DataMeta AbilityTargetTeamFilter = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityTargetTeamFilter), DisplayName = "阵营过滤", Category = DataCategory_Ability.Target,
            Type = typeof(TeamFilter), DefaultValue = global::TeamFilter.None
        });

    // 目标排序
    public static readonly DataMeta TargetSorting = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(TargetSorting), DisplayName = "目标排序", Category = DataCategory_Ability.Target,
            Type = typeof(TargetSorting), DefaultValue = global::TargetSorting.None
        });

    // 最大目标
    public static readonly DataMeta AbilityMaxTargets = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityMaxTargets), DisplayName = "最大目标", Category = DataCategory_Ability.Target,
            Type = typeof(int), DefaultValue = -1, MinValue = -1
        });

    // ============ 目标几何参数 ============
    // 施法距离
    public static readonly DataMeta AbilityCastRange = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityCastRange), DisplayName = "施法距离", Category = DataCategory_Ability.Target,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0, SupportModifiers = true
        });

    // 技能效果半径
    public static readonly DataMeta AbilityEffectRadius = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityEffectRadius), DisplayName = "效果半径", Category = DataCategory_Ability.Target,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0, SupportModifiers = true
        });

    // 效果长度
    public static readonly DataMeta AbilityEffectLength = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityEffectLength), DisplayName = "效果长度", Category = DataCategory_Ability.Target,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0
        });

    // 效果宽度
    public static readonly DataMeta AbilityEffectWidth = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityEffectWidth), DisplayName = "效果宽度", Category = DataCategory_Ability.Target,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0
        });

    // 技能角度
    public static readonly DataMeta AbilityAngle = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityAngle), DisplayName = "技能角度", Category = DataCategory_Ability.Target,
            Type = typeof(float), DefaultValue = 0f, MinValue = 0, MaxValue = 360
        });

    // 特效场景路径，不走约束系统
    public const string EffectScene = "EffectScene";

    /// <summary>
    /// 投射物视觉场景路径，不走约束系统。
    /// </summary>
    public const string ProjectileScene = "ProjectileScene";

    // ============ 状态标记 ============
    // 已解锁
    public static readonly DataMeta AbilityUnlocked = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(AbilityUnlocked), DisplayName = "已解锁", Category = DataCategory_Ability.State,
            Type = typeof(bool), DefaultValue = true
        });

    // ============ 主动技能输入 ============
    // 当前激活技能索引
    public static readonly DataMeta CurrentActiveAbilityIndex = DataRegistry.Register(
        new DataMeta
        {
            Key = nameof(CurrentActiveAbilityIndex), DisplayName = "当前激活技能索引", Category = DataCategory_Ability.Input,
            Type = typeof(int), DefaultValue = 0, MinValue = 0
        });
}