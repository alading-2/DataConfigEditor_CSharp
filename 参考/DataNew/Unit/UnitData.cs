namespace Slime.ConfigNew.Units
{
    /// <summary>
    /// 单位配置基类（纯 POCO，不继承 Resource）
    /// 包含 Player/Enemy/TargetingIndicator 共有的属性
    /// </summary>
    public abstract class UnitData
    {
        // ====== 基础信息 ======

        /// <summary>
        /// 单位名称
        /// </summary>
        public string Name { get; set; } = (string)DataKey.Name.DefaultValue!;

        /// <summary>
        /// 所属队伍
        /// </summary>
        public Team Team { get; set; } = (Team)DataKey.Team.DefaultValue!;

        /// <summary>
        /// 实体类型
        /// </summary>
        public EntityType EntityType { get; set; } = EntityType.Unit;

        /// <summary>
        /// 死亡类型
        /// </summary>
        public DeathType DeathType { get; set; } = (DeathType)DataKey.DeathType.DefaultValue!;

        // ====== 视觉 ======

        /// <summary>
        /// 视觉场景路径 (res:// 路径字符串)
        /// </summary>
        public string VisualScenePath { get; set; } = "";

        /// <summary>
        /// 血条显示高度
        /// </summary>
        public float HealthBarHeight { get; set; } = (float)DataKey.HealthBarHeight.DefaultValue!;

        // ====== 生命属性 ======

        /// <summary>
        /// 基础最大生命值
        /// </summary>
        public float BaseHp { get; set; } = (float)DataKey.BaseHp.DefaultValue!;

        /// <summary>
        /// 基础生命回复 (每秒)
        /// </summary>
        public float BaseHpRegen { get; set; } = (float)DataKey.BaseHpRegen.DefaultValue!;

        /// <summary>
        /// 吸血比例 (%)
        /// </summary>
        public float LifeSteal { get; set; } = (float)DataKey.LifeSteal.DefaultValue!;

        // ====== 攻击属性 ======

        /// <summary>
        /// 基础攻击力
        /// </summary>
        public float BaseAttack { get; set; } = (float)DataKey.BaseAttack.DefaultValue!;

        /// <summary>
        /// 基础攻击速度
        /// </summary>
        public float BaseAttackSpeed { get; set; } = (float)DataKey.BaseAttackSpeed.DefaultValue!;

        /// <summary>
        /// 攻击距离/范围
        /// </summary>
        public float AttackRange { get; set; } = (float)DataKey.AttackRange.DefaultValue!;

        /// <summary>
        /// 暴击率 (%)
        /// </summary>
        public float CritRate { get; set; } = (float)DataKey.CritRate.DefaultValue!;

        /// <summary>
        /// 暴击伤害倍率 (%)
        /// </summary>
        public float CritDamage { get; set; } = (float)DataKey.CritDamage.DefaultValue!;

        /// <summary>
        /// 护甲穿透
        /// </summary>
        public float Penetration { get; set; } = (float)DataKey.Penetration.DefaultValue!;

        // ====== 防御属性 ======

        /// <summary>
        /// 基础防御力/护甲
        /// </summary>
        public float BaseDefense { get; set; } = (float)DataKey.BaseDefense.DefaultValue!;

        /// <summary>
        /// 伤害减免 (%)
        /// </summary>
        public float DamageReduction { get; set; } = (float)DataKey.DamageReduction.DefaultValue!;

        // ====== 移动属性 ======

        /// <summary>
        /// 移动速度
        /// </summary>
        public float MoveSpeed { get; set; } = (float)DataKey.MoveSpeed.DefaultValue!;

        // ====== 闪避属性 ======

        /// <summary>
        /// 闪避率 (%)
        /// </summary>
        public float DodgeChance { get; set; } = (float)DataKey.DodgeChance.DefaultValue!;
    }
}
