using Godot;
using Slime.Config.Abilities;

/// <summary>
/// 链式技能配置 - 模板技能基类
/// 
/// 用途：
/// - 继承此类即可复用链式弹跳执行器（ChainLightningExecutor）
/// - 执行器通过 DataKey.Chain* 读取参数，遵循框架 DataKey 统一管理规范
/// 
/// 使用示例：
/// - 闪电链：创建 ChainLightningConfig.tres（类型选 ChainAbilityConfig），填入参数
/// - 冰霜链：创建 FrostChainConfig.tres（类型选 ChainAbilityConfig），填入参数
/// - 毒素链：创建 PoisonChainConfig.tres（类型选 ChainAbilityConfig），填入参数
/// </summary>
[GlobalClass]
public partial class ChainAbilityConfig : AbilityConfig
{
    /// <summary>链式弹跳次数</summary>
    [ExportGroup("链式效果")]
    [DataKey(nameof(DataKey.AbilityChainCount))]
    [Export] public int ChainCount { get; set; } = (int)DataKey.AbilityChainCount.DefaultValue!;

    /// <summary>链式弹跳范围（每跳的搜索半径）</summary>
    [DataKey(nameof(DataKey.AbilityChainRange))]
    [Export] public float ChainRange { get; set; } = (float)DataKey.AbilityChainRange.DefaultValue!;

    /// <summary>链式弹跳延时 (秒)</summary>
    [DataKey(nameof(DataKey.AbilityChainDelay))]
    [Export] public float ChainDelay { get; set; } = (float)DataKey.AbilityChainDelay.DefaultValue!;

    /// <summary>链式伤害衰减系数 (0-100，100=无衰减)</summary>
    [DataKey(nameof(DataKey.AbilityChainDamageDecay))]
    [Export] public float ChainDamageDecay { get; set; } = (float)DataKey.AbilityChainDamageDecay.DefaultValue!;

    /// <summary>链式连线特化表现（如不填则默认不表现）</summary>
    [DataKey(nameof(DataKey.AbilityChainLineEffect))]
    [Export] public PackedScene? LineEffectScene { get; set; }
}
