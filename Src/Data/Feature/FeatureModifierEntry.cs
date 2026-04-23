using Godot;

/// <summary>
/// Feature 属性修改器条目 - 描述一条数据驱动的 DataModifier 配置
///
/// 用法：在 FeatureDefinition.Modifiers 中配置，
/// FeatureSystem 在 OnGranted 时自动施加，在 OnRemoved 时自动回滚。
///
/// 示例：攻击伤害 +10，类型 Additive
/// </summary>
[GlobalClass]
public partial class FeatureModifierEntry : Resource
{
    /// <summary>目标 DataKey 名称（如 "AttackDamage"、"MoveSpeed"）</summary>
    [Export] public string DataKeyName { get; set; } = "";

    /// <summary>修改器类型</summary>
    [Export] public ModifierType ModifierType { get; set; } = ModifierType.Additive;

    /// <summary>修改值</summary>
    [Export] public float Value { get; set; } = 0f;

    /// <summary>优先级（数值越高越先计算）</summary>
    [Export] public int Priority { get; set; } = 0;
}
