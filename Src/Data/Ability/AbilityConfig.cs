using Godot;


namespace Slime.Config.Abilities
{
    [GlobalClass]
    public partial class AbilityConfig : Resource
    {
        /// <summary>
        /// 技能名称
        /// </summary>
        [ExportGroup("基础信息")]
        [DataKey(nameof(DataKey.Name))]
        [Export] public string? Name { get; set; }
        /// <summary>
        /// 技能分组 ID（FeatureGroup 前缀）。
        /// 与 Name 组合自动派生完整 FeatureHandlerId：{FeatureGroupId}.{Name}
        /// 例：FeatureGroupId="技能.位移"，Name="冲刺" → "技能.位移.冲刺"
        /// </summary>
        [DataKey(nameof(DataKey.AbilityFeatureGroup))]
        [Export] public string? FeatureGroupId { get; set; }
        /// <summary>
        /// 技能逻辑处理器 ID（可选覆盖）。
        /// 为空时由 FeatureGroupId + Name 自动派生；仅在需要特殊映射时才手动填写。
        /// </summary>
        [DataKey(nameof(DataKey.FeatureHandlerId))]
        [Export] public string? FeatureHandlerId { get; set; }

        /// <summary>
        /// 解析最终 FeatureHandlerId。
        /// 优先使用显式 FeatureHandlerId；为空则由 featureGroupId + "." + name 派生。
        /// </summary>
        public static string? ResolveFeatureHandlerId(string? featureHandlerId, string? featureGroupId, string? name)
        {
            if (!string.IsNullOrEmpty(featureHandlerId)) return featureHandlerId;
            if (!string.IsNullOrEmpty(featureGroupId) && !string.IsNullOrEmpty(name))
                return $"{featureGroupId}.{name}";
            return null;
        }
        /// <summary>
        /// 技能描述
        /// </summary>
        [DataKey(nameof(DataKey.Description))]
        [Export] public string? Description { get; set; }
        /// <summary>
        /// 技能图标
        /// </summary>
        [DataKey(nameof(DataKey.AbilityIcon))]
        [Export] public Texture2D? AbilityIcon { get; set; }
        /// <summary>
        /// 当前级别
        /// </summary>
        [DataKey(nameof(DataKey.AbilityLevel))]
        [Export] public int AbilityLevel { get; set; } = (int)DataKey.AbilityLevel.DefaultValue!;
        /// <summary>
        /// 最大级别
        /// </summary>
        [DataKey(nameof(DataKey.AbilityMaxLevel))]
        [Export] public int AbilityMaxLevel { get; set; } = (int)DataKey.AbilityMaxLevel.DefaultValue!;

        /// <summary>
        /// 实体类型
        /// </summary>
        [ExportGroup("技能类型")]
        [DataKey(nameof(DataKey.EntityType))]
        [Export] public EntityType EntityType { get; set; } = EntityType.Ability;
        /// <summary>
        /// 技能类型
        /// </summary>
        [DataKey(nameof(DataKey.AbilityType))]
        [Export] public AbilityType AbilityType { get; set; }
        /// <summary>
        /// 触发模式
        /// </summary>
        [DataKey(nameof(DataKey.AbilityTriggerMode))]
        [Export] public AbilityTriggerMode AbilityTriggerMode { get; set; }

        /// <summary>
        /// 消耗类型
        /// </summary>
        [ExportGroup("消耗与冷却")]
        [DataKey(nameof(DataKey.AbilityCostType))]
        [Export] public AbilityCostType AbilityCostType { get; set; }
        /// <summary>
        /// 消耗数值
        /// </summary>
        [DataKey(nameof(DataKey.AbilityCostAmount))]
        [Export] public float AbilityCostAmount { get; set; }
        /// <summary>
        /// 冷却时间 (秒)
        /// </summary>
        [DataKey(nameof(DataKey.AbilityCooldown))]
        [Export] public float AbilityCooldown { get; set; }

        /// <summary>
        /// 是否使用充能系统
        /// </summary>
        [ExportGroup("充能系统")]
        [DataKey(nameof(DataKey.IsAbilityUsesCharges))]
        [Export] public bool IsAbilityUsesCharges { get; set; }
        /// <summary>
        /// 最大充能层数
        /// </summary>
        [DataKey(nameof(DataKey.AbilityMaxCharges))]
        [Export] public int AbilityMaxCharges { get; set; }
        /// <summary>
        /// 充能时间 (秒)
        /// </summary>
        [DataKey(nameof(DataKey.AbilityChargeTime))]
        [Export] public float AbilityChargeTime { get; set; }

        /// <summary>
        /// 目标选择方式
        /// </summary>
        [ExportGroup("目标选择")]
        [DataKey(nameof(DataKey.AbilityTargetSelection))]
        [Export] public AbilityTargetSelection AbilityTargetSelection { get; set; }
        /// <summary>
        /// 目标几何形状
        /// </summary>
        [DataKey(nameof(DataKey.AbilityTargetGeometry))]
        [Export] public GeometryType AbilityTargetGeometry { get; set; }
        /// <summary>
        /// 目标阵营过滤
        /// </summary>
        [DataKey(nameof(DataKey.AbilityTargetTeamFilter))]
        [Export] public AbilityTargetTeamFilter AbilityTargetTeamFilter { get; set; }
        /// <summary>
        /// 目标排序方式
        /// </summary>
        [DataKey(nameof(DataKey.TargetSorting))]
        [Export] public TargetSorting TargetSorting { get; set; }

        /// <summary>
        /// 施法距离（索敌/瞄准射程；Dash 等位移技能可复用为位移距离；0=无限制）
        /// </summary>
        [DataKey(nameof(DataKey.AbilityCastRange))]
        [Export] public float AbilityCastRange { get; set; }
        /// <summary>
        /// 效果半径（圆形/扇形 AOE 半径；Dash 等落点结算技能可复用为伤害范围）
        /// </summary>
        [DataKey(nameof(DataKey.AbilityEffectRadius))]
        [Export] public float AbilityEffectRadius { get; set; }
        /// <summary>
        /// 效果长度（矩形/线形 AOE 长度维度）
        /// </summary>
        [DataKey(nameof(DataKey.AbilityEffectLength))]
        [Export] public float AbilityEffectLength { get; set; }
        /// <summary>
        /// 效果宽度（矩形/线形 AOE 宽度维度）
        /// </summary>
        [DataKey(nameof(DataKey.AbilityEffectWidth))]
        [Export] public float AbilityEffectWidth { get; set; }

        /// <summary>
        /// 最大目标数量
        /// </summary>
        [DataKey(nameof(DataKey.AbilityMaxTargets))]
        [Export] public int AbilityMaxTargets { get; set; } = (int)DataKey.AbilityMaxTargets.DefaultValue!;

        /// <summary>
        /// 技能表现特效（施法/命中/爆炸等通用表现）
        /// </summary>
        [ExportGroup("视觉与表现")]
        [DataKey(nameof(DataKey.EffectScene))]
        [Export] public PackedScene? EffectScene { get; set; }

        /// <summary>
        /// 技能投射物视觉场景。
        /// </summary>
        [DataKey(nameof(DataKey.ProjectileScene))]
        [Export] public PackedScene? ProjectileScene { get; set; }

        /// <summary>
        /// 技能伤害数值
        /// </summary>
        [ExportGroup("伤害效果")]
        // 这里只是示例，也许应该有一个DamageInfo配置？暂时先这样
        [DataKey(nameof(DataKey.AbilityDamage))]
        [Export] public float AbilityDamage { get; set; } = (float)DataKey.AbilityDamage.DefaultValue!;
    }
}
