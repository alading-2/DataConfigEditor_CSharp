using System.Collections.Generic;
using Slime.ConfigNew;
namespace Slime.ConfigNew.Units
{
    /// <summary>
    /// 玩家配置（纯 POCO）
    /// </summary>
    public class PlayerData : UnitData
    {
        /// <summary>全部数据。</summary>
        public static IReadOnlyList<PlayerData> All => DataTable.GetAll<PlayerData>();

        /// <summary>按 Name 获取数据，找不到返回 null 并记录日志。</summary>
        public static PlayerData? Get(string name) => DataTable.GetByName<PlayerData>(name);

        // ====== 玩家专有 ======

        /// <summary>
        /// 基础法力值
        /// </summary>
        public float BaseMana { get; set; } = (float)DataKey.BaseMana.DefaultValue!;

        /// <summary>
        /// 基础法力回复 (每秒)
        /// </summary>
        public float BaseManaRegen { get; set; } = (float)DataKey.BaseManaRegen.DefaultValue!;

        /// <summary>
        /// 拾取范围
        /// </summary>
        public float PickupRange { get; set; } = (float)DataKey.PickupRange.DefaultValue!;

        /// <summary>
        /// 基础技能伤害
        /// </summary>
        [DataKey(nameof(DataKey.AbilityDamageBonus))]
        public float BaseSkillDamage { get; set; } = (float)DataKey.AbilityDamageBonus.DefaultValue!;

        /// <summary>
        /// 冷却缩减 (%)
        /// </summary>
        public float CooldownReduction { get; set; } = (float)DataKey.CooldownReduction.DefaultValue!;

        // ====== 实例 ======

        /// <summary>德鲁伊</summary>
        public static readonly PlayerData Deluyi = new()
        {
            Name = "德鲁伊",
            Team = Team.Player,
            DeathType = DeathType.Hero,
            VisualScenePath = "res://assets/Unit/Player/deluyi/AnimatedSprite2D/deluyi.tscn",
            HealthBarHeight = 120f,
            BaseHp = 100f,
            BaseHpRegen = 1f,
            BaseAttack = 10f,
            AttackRange = 150f,
            CritRate = 5f,
            BaseDefense = 5f,
            MoveSpeed = 200f,
        };
    }
}
