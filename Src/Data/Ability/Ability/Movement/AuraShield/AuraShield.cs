using System.Runtime.CompilerServices;
using Godot;

/// <summary>
/// 光环护盾技能执行器 - 验证 AttachToHost 运动模式
/// 在玩家右侧生成一个附着护盾，跟随玩家移动，接触敌人造成伤害
/// </summary>
internal class AuraShieldExecutor : AbilityFeatureHandlerBase
{
    private static readonly Log _log = new(nameof(AuraShieldExecutor));

    [ModuleInitializer]
    internal static void Initialize()
    {
        FeatureHandlerRegistry.Register(new AuraShieldExecutor());
    }

    public override string FeatureId => global::FeatureId.Ability.Passive.AuraShield;
    public override string FeatureGroup => global::FeatureId.Ability.Groups.Passive;

    protected override AbilityExecutedResult ExecuteAbility(CastContext context)
    {
        var caster = context.Caster;
        var ability = context.Ability;
        if (caster == null || ability == null || caster is not Node2D casterNode)
            return new AbilityExecutedResult { TargetsHit = 0 };

        var damage = ability.Data.Get<float>(DataKey.AbilityDamage)
                   * caster.Data.Get<float>(DataKey.AbilityDamageBonus) / 100f;
        var projectileScene = ability.Data.Get<PackedScene>(DataKey.ProjectileScene);

        var projectile = ProjectileTool.Spawn(
            casterNode.GlobalPosition + new Vector2(80f, 0f),
            new ProjectileSpawnOptions(projectileScene, "AuraShieldProjectile"));
        if (projectile == null) return new AbilityExecutedResult { TargetsHit = 0 };

        // 通过 Data 设置相对宿主偏移（AttachToHostStrategy 读取 DataKey.EffectOffset）
        projectile.Data.Set(DataKey.EffectOffset, new Vector2(80f, 0f));

        float cachedDamage = damage;
        IEntity cachedCaster = caster;

        // 光环持续存在，碰撞不销毁（DestroyOnCollision=false）
        projectile.Events.On<GameEventType.Unit.MovementCollisionEventData>(
            GameEventType.Unit.MovementCollision,
            (evt) => OnHit(evt, cachedCaster, cachedDamage));

        projectile.Events.Emit(
            GameEventType.Unit.MovementStarted,
            new GameEventType.Unit.MovementStartedEventData(
                MoveMode.AttachToHost,
                new MovementParams
                {
                    Mode = MoveMode.AttachToHost,
                    TargetNode = casterNode,
                    MaxDuration = 5f,
                    DestroyOnComplete = true,
                    DestroyOnCollision = false,
                    RotateToVelocity = false,
                }
            )
        );

        _log.Info("光环护盾: 已附着到玩家右侧，持续 5 秒");
        return new AbilityExecutedResult { TargetsHit = 1 };
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
