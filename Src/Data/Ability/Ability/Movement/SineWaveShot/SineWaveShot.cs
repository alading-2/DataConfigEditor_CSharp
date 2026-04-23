using System.Runtime.CompilerServices;
using Godot;

/// <summary>
/// 正弦波弹技能执行器 - 验证 SineWave 运动模式
/// 向最近敌人方向发射正弦波前进的投射物
/// </summary>
internal class SineWaveShotExecutor : AbilityFeatureHandlerBase
{
    private static readonly Log _log = new(nameof(SineWaveShotExecutor));

    [ModuleInitializer]
    public static void Initialize()
    {
        FeatureHandlerRegistry.Register(new SineWaveShotExecutor());
    }

    public override string FeatureId => global::FeatureId.Ability.Projectile.SineWaveShot;
    public override string FeatureGroup => global::FeatureId.Ability.Groups.Projectile;

    protected override AbilityExecutedResult ExecuteAbility(CastContext context)
    {
        var caster = context.Caster;
        var ability = context.Ability;
        if (caster == null || ability == null || caster is not Node2D casterNode)
            return new AbilityExecutedResult { TargetsHit = 0 };

        var damage = ability.Data.Get<float>(DataKey.AbilityDamage)
                   * caster.Data.Get<float>(DataKey.AbilityDamageBonus) / 100f;

        // 确定射击方向：优先最近敌人，否则朝右
        var dir = GetShootDirection(caster, casterNode);
        var projectileScene = ability.Data.Get<PackedScene>(DataKey.ProjectileScene);

        var projectile = ProjectileTool.Spawn(
            casterNode.GlobalPosition,
            new ProjectileSpawnOptions(projectileScene, "SineWaveShotProjectile"));
        if (projectile == null) return new AbilityExecutedResult { TargetsHit = 0 };

        float cachedDamage = damage;
        IEntity cachedCaster = caster;

        projectile.Events.On<GameEventType.Unit.MovementCollisionEventData>(
            GameEventType.Unit.MovementCollision,
            (evt) => OnHit(evt, cachedCaster, cachedDamage));

        projectile.Events.Emit(
            GameEventType.Unit.MovementStarted,
            new GameEventType.Unit.MovementStartedEventData(
                MoveMode.SineWave,
                new MovementParams
                {
                    Mode = MoveMode.SineWave,
                    Angle = Mathf.RadToDeg(dir.Angle()),
                    ActionSpeed = 350f,
                    WaveAmplitude = 60f,
                    WaveFrequency = 2f,
                    MaxDistance = 900f,
                    DestroyOnComplete = true,
                    DestroyOnCollision = true,
                    RotateToVelocity = true,
                }
            )
        );

        _log.Info($"正弦波弹: 方向={dir}, 角度={Mathf.RadToDeg(dir.Angle()):F1}°");
        return new AbilityExecutedResult { TargetsHit = 1 };
    }

    private static Vector2 GetShootDirection(IEntity caster, Node2D casterNode)
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
        if (targets.Count > 0 && targets[0] is Node2D targetNode)
            return (targetNode.GlobalPosition - casterNode.GlobalPosition).Normalized();
        return Vector2.Right;
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
