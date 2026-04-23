using System.Runtime.CompilerServices;
using Godot;

/// <summary>
/// 圆弧弹技能执行器 - 验证 CircularArc 运动模式
/// 向最近敌人发射沿圆弧轨迹飞行的投射物
/// </summary>
internal class ArcShotExecutor : AbilityFeatureHandlerBase
{
    private static readonly Log _log = new(nameof(ArcShotExecutor));

    [ModuleInitializer]
    public static void Initialize()
    {
        FeatureHandlerRegistry.Register(new ArcShotExecutor());
    }

    public override string FeatureId => global::FeatureId.Ability.Projectile.ArcShot;
    public override string FeatureGroup => global::FeatureId.Ability.Groups.Projectile;

    protected override AbilityExecutedResult ExecuteAbility(CastContext context)
    {
        var caster = context.Caster;
        var ability = context.Ability;
        if (caster == null || ability == null || caster is not Node2D casterNode)
            return new AbilityExecutedResult { TargetsHit = 0 };

        var damage = ability.Data.Get<float>(DataKey.AbilityDamage)
                   * caster.Data.Get<float>(DataKey.AbilityDamageBonus) / 100f;

        var targetPos = GetNearestEnemyPos(caster, casterNode);
        var projectileScene = ability.Data.Get<PackedScene>(DataKey.ProjectileScene);

        var projectile = ProjectileTool.Spawn(
            casterNode.GlobalPosition,
            new ProjectileSpawnOptions(projectileScene, "ArcShotProjectile"));
        if (projectile == null) return new AbilityExecutedResult { TargetsHit = 0 };

        float cachedDamage = damage;
        IEntity cachedCaster = caster;

        projectile.Events.On<GameEventType.Unit.MovementCollisionEventData>(
            GameEventType.Unit.MovementCollision,
            (evt) => OnHit(evt, cachedCaster, cachedDamage));

        projectile.Events.Emit(
            GameEventType.Unit.MovementStarted,
            new GameEventType.Unit.MovementStartedEventData(
                MoveMode.CircularArc,
                new MovementParams
                {
                    Mode = MoveMode.CircularArc,
                    TargetPoint = targetPos,
                    ActionSpeed = 360f,
                    CircularArcRadius = 220f,
                    CircularArcClockwise = true,
                    BowWorldUp = true,
                    DestroyOnComplete = true,
                    DestroyOnCollision = true,
                    RotateToVelocity = true,
                }
            )
        );

        _log.Info($"圆弧弹: 目标={targetPos}");
        return new AbilityExecutedResult { TargetsHit = 1 };
    }

    private static Vector2 GetNearestEnemyPos(IEntity caster, Node2D casterNode)
    {
        var query = new TargetSelectorQuery
        {
            Geometry = GeometryType.Circle,
            Origin = casterNode.GlobalPosition,
            Range = 700f,
            CenterEntity = caster,
            TeamFilter = AbilityTargetTeamFilter.Enemy,
            Sorting = TargetSorting.Nearest,
            MaxTargets = 1
        };
        var targets = EntityTargetSelector.Query(query);
        if (targets.Count > 0 && targets[0] is Node2D t)
            return t.GlobalPosition;
        return casterNode.GlobalPosition + new Vector2(400f, 0f);
    }

    private static void OnHit(GameEventType.Unit.MovementCollisionEventData evt, IEntity caster, float damage)
    {
        if (evt.Target is IUnit victim)
        {
            DamageService.Instance.Process(new DamageInfo
            {
                Attacker = caster as Godot.Node,
                Victim = victim,
                Damage = damage,
                Type = DamageType.Physical,
                Tags = DamageTags.Ability
            });
        }
    }
}
