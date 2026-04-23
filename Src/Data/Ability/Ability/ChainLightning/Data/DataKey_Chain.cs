/// <summary>
/// 链式技能专属 DataKey 定义
/// 适用于所有使用链式弹跳逻辑的技能（ChainLightning、FrostChain 等）
/// </summary>
public static partial class DataKey
{
    // ============ 链式效果 ============
    // 弹跳次数
    public static readonly DataMeta AbilityChainCount = DataRegistry.Register(
        new DataMeta { Key = nameof(AbilityChainCount), DisplayName = "弹跳次数", Category = DataCategory_Ability.Target, Type = typeof(int), DefaultValue = 3, MinValue = 1, SupportModifiers = true });

    // 弹跳范围
    public static readonly DataMeta AbilityChainRange = DataRegistry.Register(
        new DataMeta { Key = nameof(AbilityChainRange), DisplayName = "弹跳范围", Description = "每跳的搜索半径", Category = DataCategory_Ability.Target, Type = typeof(float), DefaultValue = 300f, MinValue = 0, SupportModifiers = true });

    // 弹跳延时
    public static readonly DataMeta AbilityChainDelay = DataRegistry.Register(
        new DataMeta { Key = nameof(AbilityChainDelay), DisplayName = "弹跳延时", Description = "单位：秒", Category = DataCategory_Ability.Target, Type = typeof(float), DefaultValue = 0.2f, MinValue = 0 });

    // 伤害衰减
    public static readonly DataMeta AbilityChainDamageDecay = DataRegistry.Register(
        new DataMeta { Key = nameof(AbilityChainDamageDecay), DisplayName = "伤害衰减", Description = "0-100，100=无衰减", Category = DataCategory_Ability.Target, Type = typeof(float), DefaultValue = 100f, MinValue = 0, SupportModifiers = true, IsPercentage = true });

    // 特效场景路径，不走约束系统
    public const string AbilityChainLineEffect = "AbilityChainLineEffect";
}
