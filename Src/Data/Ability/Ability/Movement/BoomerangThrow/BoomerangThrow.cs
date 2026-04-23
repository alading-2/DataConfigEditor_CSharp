using System.Runtime.CompilerServices;
using Godot;

/// <summary>
/// 回旋镖技能执行器 - 验证 Boomerang 运动模式
/// 向最近敌人投掷回旋镖，飞出后自动返回，过程中碰撞造成伤害
/// </summary>
internal class BoomerangThrowExecutor : AbilityFeatureHandlerBase
{
    private static readonly Log _log = new(nameof(BoomerangThrowExecutor));

    [ModuleInitializer]
    public static void Initialize()
    {
        FeatureHandlerRegistry.Register(new BoomerangThrowExecutor());
    }

    public override string FeatureId => global::FeatureId.Ability.Projectile.BoomerangThrow;
    public override string FeatureGroup => global::FeatureId.Ability.Groups.Projectile;

    protected override AbilityExecutedResult ExecuteAbility(CastContext context)
    {
        var caster = context.Caster;
        var ability = context.Ability;
        if (caster == null || ability == null || caster is not Node2D casterNode)
            return new AbilityExecutedResult { TargetsHit = 0 };

        var damage = ability.Data.Get<float>(DataKey.AbilityDamage)
                   * caster.Data.Get<float>(DataKey.AbilityDamageBonus) / 100f;

        var throwTarget = GetThrowTarget(caster, casterNode);
        var projectileScene = ability.Data.Get<PackedScene>(DataKey.ProjectileScene);

        var projectile = ProjectileTool.Spawn(
            casterNode.GlobalPosition,
            new ProjectileSpawnOptions(projectileScene, "BoomerangThrowProjectile"));
        if (projectile == null) return new AbilityExecutedResult { TargetsHit = 0 };

        float cachedDamage = damage;
        IEntity cachedCaster = caster;

        // 回旋镖不销毁于碰撞，继续飞行并返回
        projectile.Events.On<GameEventType.Unit.MovementCollisionEventData>(
            GameEventType.Unit.MovementCollision,
            (evt) => OnHit(evt, cachedCaster, cachedDamage));

        projectile.Events.Emit(
            GameEventType.Unit.MovementStarted,
            new GameEventType.Unit.MovementStartedEventData(
                MoveMode.Boomerang,
                new MovementParams
                {
                    Mode = MoveMode.Boomerang,
                    TargetPoint = throwTarget,
                    TargetNode = casterNode, // 显式指定返程宿主，避免策略退回到不可靠的祖先回溯。
                    ActionSpeed = 460f,
                    BoomerangArcHeight = 26f,
                    BoomerangPauseTime = 0.08f,
                    BoomerangIsClockwise = true,
                    BoomerangReturnSpeedMultiplier = 1.2f,
                    DestroyOnComplete = true,
                    DestroyOnCollision = false,
                    RotateToVelocity = true,
                }
            )
        );

        _log.Info($"回旋镖投掷: 目标={throwTarget}");
        return new AbilityExecutedResult { TargetsHit = 1 };
    }

    private static Vector2 GetThrowTarget(IEntity caster, Node2D casterNode)
    {
        var query = new TargetSelectorQuery
        {
            Geometry = GeometryType.Circle,
            Origin = casterNode.GlobalPosition,
            Range = 600f,
            CenterEntity = caster,
            TeamFilter = AbilityTargetTeamFilter.Enemy,
            Sorting = TargetSorting.Nearest,
            MaxTargets = 1
        };
        var targets = EntityTargetSelector.Query(query);
        if (targets.Count > 0 && targets[0] is Node2D t)
            return t.GlobalPosition;
        return casterNode.GlobalPosition + new Vector2(280f, 0f);
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
