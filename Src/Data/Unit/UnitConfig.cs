using Godot;

namespace Slime.Config.Units
{
    /// <summary>
    /// 单位配置基类
    /// 包含 Player 和 Enemy 共有的属性
    /// 属性名必须与 DataKey 中的字符串常量一致，以便自动加载
    /// </summary>
    public abstract partial class UnitConfig : Resource
    {
        /// <summary>
        /// 单位名称
        /// </summary>
        [ExportGroup("基础信息")]
        [DataKey(nameof(DataKey.Name))]
        [Export] public string Name { get; set; }
        /// <summary>
        /// 所属队伍
        /// </summary>
        [DataKey(nameof(DataKey.Team))]
        [Export] public Team Team { get; set; } = (Team)DataKey.Team.DefaultValue!;

        /// <summary>
        /// 死亡类型
        /// </summary>
        [DataKey(nameof(DataKey.DeathType))]
        [Export] public DeathType DeathType { get; set; } = (DeathType)DataKey.DeathType.DefaultValue!;

        /// <summary>
        /// 视觉场景路径 (PackedScene)
        /// Data.LoadFromResource 会自动读取此属性
        /// </summary>
        [ExportGroup("视觉")]
        [DataKey(nameof(DataKey.VisualScenePath))]
        [Export] public PackedScene? VisualScenePath { get; set; }

        /// <summary>
        /// 血条显示高度
        /// </summary>
        [DataKey(nameof(DataKey.HealthBarHeight))]
        [Export] public float HealthBarHeight { get; set; } = (float)DataKey.HealthBarHeight.DefaultValue!;

        /// <summary>
        /// 基础最大生命值
        /// </summary>
        [ExportGroup("生命属性")]
        [DataKey(nameof(DataKey.BaseHp))]
        [Export] public float BaseHp { get; set; } = (float)DataKey.BaseHp.DefaultValue!;
        /// <summary>
        /// 基础生命回复 (每秒)
        /// </summary>
        [DataKey(nameof(DataKey.BaseHpRegen))]
        [Export] public float BaseHpRegen { get; set; } = (float)DataKey.BaseHpRegen.DefaultValue!;
        /// <summary>
        /// 吸血比例 (%)
        /// </summary>
        [DataKey(nameof(DataKey.LifeSteal))]
        [Export] public float LifeSteal { get; set; } = (float)DataKey.LifeSteal.DefaultValue!;

        /// <summary>
        /// 基础攻击力
        /// </summary>
        [ExportGroup("攻击属性")]
        [DataKey(nameof(DataKey.BaseAttack))]
        [Export] public float BaseAttack { get; set; } = (float)DataKey.BaseAttack.DefaultValue!;
        /// <summary>
        /// 基础攻击速度
        /// </summary>
        [DataKey(nameof(DataKey.BaseAttackSpeed))]
        [Export] public float BaseAttackSpeed { get; set; } = (float)DataKey.BaseAttackSpeed.DefaultValue!;
        /// <summary>
        /// 攻击距离/范围
        /// </summary>
        [DataKey(nameof(DataKey.AttackRange))]
        [Export] public float AttackRange { get; set; } = (float)DataKey.AttackRange.DefaultValue!;
        /// <summary>
        /// 暴击率 (%)
        /// </summary>
        [DataKey(nameof(DataKey.CritRate))]
        [Export] public float CritRate { get; set; } = (float)DataKey.CritRate.DefaultValue!;
        /// <summary>
        /// 暴击伤害倍率 (%)
        /// </summary>
        [DataKey(nameof(DataKey.CritDamage))]
        [Export] public float CritDamage { get; set; } = (float)DataKey.CritDamage.DefaultValue!;
        /// <summary>
        /// 护甲穿透
        /// </summary>
        [DataKey(nameof(DataKey.Penetration))]
        [Export] public float Penetration { get; set; } = (float)DataKey.Penetration.DefaultValue!;

        /// <summary>
        /// 基础防御力/护甲
        /// </summary>
        [ExportGroup("防御属性")]
        [DataKey(nameof(DataKey.BaseDefense))]
        [Export] public float BaseDefense { get; set; } = (float)DataKey.BaseDefense.DefaultValue!;
        /// <summary>
        /// 伤害减免 (%)
        /// </summary>
        [DataKey(nameof(DataKey.DamageReduction))]
        [Export] public float DamageReduction { get; set; } = (float)DataKey.DamageReduction.DefaultValue!;

        /// <summary>
        /// 移动速度
        /// </summary>
        [ExportGroup("移动属性")]
        [DataKey(nameof(DataKey.MoveSpeed))]
        [Export] public float MoveSpeed { get; set; } = (float)DataKey.MoveSpeed.DefaultValue!;

        /// <summary>
        /// 闪避率 (%)
        /// </summary>
        [DataKey(nameof(DataKey.DodgeChance))]
        [Export] public float DodgeChance { get; set; } = (float)DataKey.DodgeChance.DefaultValue!;
    }
}
