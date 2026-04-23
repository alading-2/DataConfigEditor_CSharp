using Godot;

namespace Slime.Config.Units
{
    [GlobalClass]
    public partial class PlayerConfig : UnitConfig
    {
        /// <summary>
        /// 基础法力值
        /// </summary>
        [ExportGroup("玩家专有")]
        [DataKey(nameof(DataKey.BaseMana))]
        [Export] public float BaseMana { get; set; } = (float)DataKey.BaseMana.DefaultValue!;
        /// <summary>
        /// 基础法力回复 (每秒)
        /// </summary>
        [DataKey(nameof(DataKey.BaseManaRegen))]
        [Export] public float BaseManaRegen { get; set; } = (float)DataKey.BaseManaRegen.DefaultValue!;

        /// <summary>
        /// 拾取范围
        /// </summary>
        [DataKey(nameof(DataKey.PickupRange))]
        [Export] public float PickupRange { get; set; } = (float)DataKey.PickupRange.DefaultValue!;
        /// <summary>
        /// 基础技能伤害
        /// </summary>
        [DataKey(nameof(DataKey.AbilityDamageBonus))]
        [Export] public float BaseSkillDamage { get; set; } = (float)DataKey.AbilityDamageBonus.DefaultValue!;
        /// <summary>
        /// 冷却缩减 (%)
        /// </summary>
        [DataKey(nameof(DataKey.CooldownReduction))]
        [Export] public float CooldownReduction { get; set; } = (float)DataKey.CooldownReduction.DefaultValue!;
    }
}
