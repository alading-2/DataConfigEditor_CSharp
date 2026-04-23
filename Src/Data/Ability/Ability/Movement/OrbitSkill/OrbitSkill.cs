using System.Runtime.CompilerServices;
using Godot;

/// <summary>
/// 环绕护盾技能执行器 - 验证 Orbit 运动模式
/// 生成多个投射物围绕玩家旋转，碰撞敌人时造成伤害
/// </summary>
internal class OrbitSkillExecutor : AbilityFeatureHandlerBase
{
    private static readonly Log _log = new(nameof(OrbitSkillExecutor));

    [ModuleInitializer]
    public static void Initialize()
    {
        FeatureHandlerRegistry.Register(new OrbitSkillExecutor());
    }

    public override string FeatureId => global::FeatureId.Ability.Passive.OrbitSkill;
    public override string FeatureGroup => global::FeatureId.Ability.Groups.Passive;

    protected override AbilityExecutedResult ExecuteAbility(CastContext context)
    {
        var caster = context.Caster;
        var ability = context.Ability;
        if (caster == null || ability == null || caster is not Node2D casterNode)
            return new AbilityExecutedResult { TargetsHit = 0 };

        var damage = ability.Data.Get<float>(DataKey.AbilityDamage)
                   * caster.Data.Get<float>(DataKey.AbilityDamageBonus) / 100f;
        var orbitCount = 3;
        var orbitRadius = 100f;
        var orbitDuration = 6f;
        var projectileScene = ability.Data.Get<PackedScene>(DataKey.ProjectileScene);

        for (int i = 0; i < orbitCount; i++)
        {
            float initAngle = i * (360f / orbitCount);
            var projectile = ProjectileTool.Spawn(
                casterNode.GlobalPosition,
                new ProjectileSpawnOptions(projectileScene, "OrbitSkillProjectile"));
            if (projectile == null) continue;

            float cachedDamage = damage;
            IEntity cachedCaster = caster;

            projectile.Events.On<GameEventType.Unit.MovementCollisionEventData>(
                GameEventType.Unit.MovementCollision,
                (evt) => OnHit(evt, cachedCaster, cachedDamage));

            projectile.Events.Emit(
                GameEventType.Unit.MovementStarted,
                new GameEventType.Unit.MovementStartedEventData(
                    MoveMode.Orbit,
                    new MovementParams
                    {
                        Mode = MoveMode.Orbit,
                        TargetNode = casterNode,
                        OrbitRadius = orbitRadius,
                        OrbitInitAngle = initAngle,
                        OrbitAngularSpeed = 180f,
                        IsOrbitClockwise = true,
                        MaxDuration = orbitDuration,
                        DestroyOnComplete = true,
                        DestroyOnCollision = false,
                        RotateToVelocity = false,
                    }
                )
            );
        }

        _log.Info($"环绕护盾: 生成 {orbitCount} 个轨道投射物");
        return new AbilityExecutedResult { TargetsHit = orbitCount };
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
                Type = DamageType.Magical,
                Tags = DamageTags.Area | DamageTags.Ability
            });
        }
    }
}
