namespace Slime.ConfigNew.Units
{
    /// <summary>
    /// 单位配置基类（纯 POCO，不继承 Resource）
    /// 包含 Player/Enemy/TargetingIndicator 共有的属性
    /// </summary>
    public abstract class UnitConfigData
    {
        // ====== 基础信息 ======

        /// <summary>
        /// 单位名称
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 所属队伍
        /// </summary>
        public Team Team { get; set; }

        /// <summary>
        /// 死亡类型
        /// </summary>
        public DeathType DeathType { get; set; }

        // ====== 视觉 ======

        /// <summary>
        /// 视觉场景路径 (res:// 路径字符串)
        /// </summary>
        public string VisualScenePath { get; set; } = "";

        /// <summary>
        /// 血条显示高度
        /// </summary>
        public float HealthBarHeight { get; set; }

        // ====== 生命属性 ======

        /// <summary>
        /// 基础最大生命值
        /// </summary>
        public float BaseHp { get; set; }

        /// <summary>
        /// 基础生命回复 (每秒)
        /// </summary>
        public float BaseHpRegen { get; set; }

        /// <summary>
        /// 吸血比例 (%)
        /// </summary>
        public float LifeSteal { get; set; }

        // ====== 攻击属性 ======

        /// <summary>
        /// 基础攻击力
        /// </summary>
        public float BaseAttack { get; set; }

        /// <summary>
        /// 基础攻击速度
        /// </summary>
        public float BaseAttackSpeed { get; set; }

        /// <summary>
        /// 攻击距离/范围
        /// </summary>
        public float AttackRange { get; set; }

        /// <summary>
        /// 暴击率 (%)
        /// </summary>
        public float CritRate { get; set; }

        /// <summary>
        /// 暴击伤害倍率 (%)
        /// </summary>
        public float CritDamage { get; set; }

        /// <summary>
        /// 护甲穿透
        /// </summary>
        public float Penetration { get; set; }

        // ====== 防御属性 ======

        /// <summary>
        /// 基础防御力/护甲
        /// </summary>
        public float BaseDefense { get; set; }

        /// <summary>
        /// 伤害减免 (%)
        /// </summary>
        public float DamageReduction { get; set; }

        // ====== 移动属性 ======

        /// <summary>
        /// 移动速度
        /// </summary>
        public float MoveSpeed { get; set; }

        // ====== 闪避属性 ======

        /// <summary>
        /// 闪避率 (%)
        /// </summary>
        public float DodgeChance { get; set; }
    }
}
