namespace Slime.ConfigNew.Units
{
    /// <summary>
    /// 玩家配置（纯 POCO）
    /// </summary>
    public class PlayerConfigData : UnitConfigData
    {
        // ====== 玩家专有 ======

        /// <summary>
        /// 基础法力值
        /// </summary>
        public float BaseMana { get; set; }

        /// <summary>
        /// 基础法力回复 (每秒)
        /// </summary>
        public float BaseManaRegen { get; set; }

        /// <summary>
        /// 拾取范围
        /// </summary>
        public float PickupRange { get; set; }

        /// <summary>
        /// 基础技能伤害
        /// </summary>
        public float BaseSkillDamage { get; set; }

        /// <summary>
        /// 冷却缩减 (%)
        /// </summary>
        public float CooldownReduction { get; set; }

        // ====== 实例 ======

        /// <summary>德鲁伊</summary>
        public static readonly PlayerConfigData Deluyi = new()
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
