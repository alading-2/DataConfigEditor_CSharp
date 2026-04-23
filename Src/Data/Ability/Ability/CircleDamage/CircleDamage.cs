using System.Runtime.CompilerServices;
using Godot;

/// <summary>
/// 烈焰光环技能执行器
/// 
/// 触发方式：Periodic（周期触发，由 TriggerComponent 每隔 AbilityCooldown 秒自动执行）
/// 目标选择：None（自动对施法者周围圆形范围内所有敌人）
/// 特效：Effect_003（每次触发在施法者位置播放一次独立特效）
/// 伤害：魔法伤害，带 Area 标签；当前未配置 TickInterval/TotalDuration，因此重复触发仅来自 TriggerComponent 的 Periodic 轴
/// </summary>
internal class CircleDamageExecutor : AbilityFeatureHandlerBase
{
    private static readonly Log _log = new(nameof(CircleDamageExecutor));

    [ModuleInitializer]
    public static void Initialize()
    {
        FeatureHandlerRegistry.Register(new CircleDamageExecutor());
    }

    public override string FeatureId => global::FeatureId.Ability.Active.CircleDamage;
    public override string FeatureGroup => global::FeatureId.Ability.Groups.Active;

    protected override AbilityExecutedResult ExecuteAbility(CastContext context)
    {
        var caster = context.Caster;
        var ability = context.Ability;

        // 安全检查：确保施法者和技能实体存在
        if (caster == null || ability == null) return new AbilityExecutedResult();

        var casterNode2D = caster as Node2D;
        if (casterNode2D == null) return new AbilityExecutedResult();

        // 从技能配置中读取参数
        var range = ability.Data.Get<float>(DataKey.AbilityEffectRadius);            // 伤害半径
        var teamFilter = ability.Data.Get<AbilityTargetTeamFilter>(DataKey.AbilityTargetTeamFilter); // 阵营过滤
        var effectScene = ability.Data.Get<PackedScene>(DataKey.EffectScene);        // 特效场景

        // 执行命中（目标查询 + 特效 + 伤害，三步合一；位置来源统一收口到 Query / Effect 参数中）
        var result = AbilityImpactTool.Execute(caster, new AbilityImpactOptions
        {
            Query = new TargetSelectorQuery
            {
                Geometry = GeometryType.Circle,         // 圆形范围，以施法者为圆心
                Origin = casterNode2D.GlobalPosition,   // 初始位置
                OriginProvider = () => casterNode2D.GlobalPosition, // DoT tick 时持续跟随施法者当前位置
                Range = range,                          // 查询半径
                CenterEntity = caster,                  // 阵营判断基准
                TeamFilter = teamFilter,                // 阵营过滤
                MaxTargets = -1                         // 不限命中数量
            },
            Effect = effectScene != null && casterNode2D != null
                ? new EffectSpawnOptions(
                    effectScene,
                    Name: "烈焰光环特效",
                    Scale: new Vector2(2.0f, 2.0f))
                : null,
            Damage = new DamageApplyOptions
            {
                Damage = ability.Data.Get<float>(nameof(DataKey.FinalAbilityDamage)), // 技能最终伤害
                Type = DamageType.Magical,                                   // 魔法伤害
                Tags = DamageTags.Area | DamageTags.Ability,                 // 范围技能标签
                Attacker = casterNode2D,                                     // 伤害来源
                ApplyImmediateTick = ability.Data.Get<bool>(DataKey.AbilityApplyImmediateDamage) // 当前未配置 DoT 时间参数，此字段暂不生效；补上 TickInterval/TotalDuration 后才会参与首跳控制
            }
        });

        _log.Info($"[烈焰光环] 触发! 范围: {range}, 命中: {result.TargetsHit}");
        return new AbilityExecutedResult { TargetsHit = result.TargetsHit };
    }
}
