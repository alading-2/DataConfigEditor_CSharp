using System.Runtime.CompilerServices;
using Godot;

/// <summary>
/// 裂地猛击技能执行器
/// 
/// 触发方式：Manual（手动，玩家按键）
/// 目标选择：None（自动在角色周围随机选点）
/// 逻辑：
///   1. 在角色周围圆环内随机选一个点（AbilityCastRange 为选点半径）
///   2. 在该点造成圆形范围伤害（AbilityEffectRadius 为伤害半径）
/// 特效：Effect_020（在随机选点位置播放）
/// 伤害：物理伤害，带 Area + Melee 标签
/// </summary>
internal class SlamExecutor : AbilityFeatureHandlerBase
{
    private static readonly Log _log = new(nameof(SlamExecutor));

    [ModuleInitializer]
    public static void Initialize()
    {
        FeatureHandlerRegistry.Register(new SlamExecutor());
    }

    public override string FeatureId => global::FeatureId.Ability.Active.Slam;
    public override string FeatureGroup => global::FeatureId.Ability.Groups.Active;

    protected override AbilityExecutedResult ExecuteAbility(CastContext context)
    {
        var caster = context.Caster;
        var ability = context.Ability;

        // 安全检查
        if (caster == null || ability == null)
        {
            return new AbilityExecutedResult { TargetsHit = 0 };
        }

        var casterNode = caster as Node2D;
        if (casterNode == null)
        {
            _log.Warn("施法者不是 Node2D");
            return new AbilityExecutedResult { TargetsHit = 0 };
        }

        // 1. 获取技能参数
        var abilityRange = ability.Data.Get<float>(DataKey.AbilityCastRange);      // 选点范围（角色周围圆环半径）
        var damageRadius = ability.Data.Get<float>(DataKey.AbilityEffectRadius);   // 伤害范围（圆形半径）
        var maxTargets = ability.Data.Get<int>(DataKey.AbilityMaxTargets);

        // 2. 在角色周围随机选点
        var pointQuery = new TargetSelectorQuery
        {
            Geometry = GeometryType.Circle,         // 形状：圆形
            Origin = casterNode.GlobalPosition,     // 位置：施法者位置
            Range = abilityRange,                   // 半径：选点半径
            MaxTargets = 1                          // 只取一个随机点
        };
        var randomPoint = PositionTargetSelector.Query(pointQuery)[0];

        // 3. 获取特效场景
        var effectScene = ability.Data.Get<PackedScene>(DataKey.EffectScene);

        // 4. 执行命中（目标查询 + 特效生成 + 伤害结算，三步合一）
        var result = AbilityImpactTool.Execute(caster, new AbilityImpactOptions
        {
            Query = new TargetSelectorQuery
            {
                Geometry = GeometryType.Circle,                 // 圆形范围
                Origin = randomPoint,                           // 以随机点为圆心
                Range = damageRadius,                           // 伤害半径
                CenterEntity = caster,                          // 施法者作为阵营判断基准
                TeamFilter = AbilityTargetTeamFilter.Enemy,     // 只打敌人
                Sorting = TargetSorting.HighestThreat,          // 优先威胁最高的目标
                MaxTargets = maxTargets                         // 最大命中数
            },
            Effect = effectScene != null
                ? new EffectSpawnOptions(
                    effectScene,
                    Name: "裂地猛击特效",
                    Scale: Vector2.One * 0.6f)
                : null,
            Damage = new DamageApplyOptions
            {
                Damage = ability.Data.Get<float>(nameof(DataKey.FinalAbilityDamage)), // 技能最终伤害
                Type = DamageType.Magical,                                   // 魔法伤害
                Tags = DamageTags.Area | DamageTags.Ability,                 // 范围技能标签
                Attacker = casterNode                                        // 伤害来源
            }
        });

        _log.Info($"裂地猛击: 选点范围 {abilityRange}, 伤害半径 {damageRadius}, 最终伤害 {ability.Data.Get<float>(nameof(DataKey.FinalAbilityDamage)):F1}, 命中 {result.TargetsHit}");
        return new AbilityExecutedResult { TargetsHit = result.TargetsHit };
    }
}
