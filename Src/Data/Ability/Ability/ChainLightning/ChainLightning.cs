
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

/// <summary>
/// 链式弹跳上下文 - 减少方法传递参数数量
/// </summary>
public readonly record struct ChainBounceContext(
    IEntity Caster,
    float Range,
    float Delay,
    float DamageDecay,
    AbilityTargetTeamFilter TeamFilter,
    TargetSorting Sorting,
    PackedScene? LineScene
);

/// <summary>
/// 链式闪电技能执行器 - 使用 Chain 执行模式
/// 
/// 触发方式：Manual（手动，玩家按键）
/// 执行模式：Chain（延时弹跳）
/// 目标选择：Entity（Circle 几何查询初始目标）
/// 特效：在每个被命中目标位置生成独立特效
/// 伤害：魔法伤害，每次弹跳衰减
/// </summary>
internal class ChainLightningExecutor : AbilityFeatureHandlerBase
{
    private static readonly Log _log = new(nameof(ChainLightningExecutor));

    [ModuleInitializer]
    public static void Initialize()
    {
        FeatureHandlerRegistry.Register(new ChainLightningExecutor());
    }

    /// <summary>
    /// 执行技能逻辑
    /// </summary>
    /// <param name="context">施法上下文，包含施法者、技能对象、初始目标等</param>
    /// <returns>返回执行结果，主要包含命中目标数</returns>
    public override string FeatureId => global::FeatureId.Ability.Active.ChainLightning;
    public override string FeatureGroup => global::FeatureId.Ability.Groups.Active;

    protected override AbilityExecutedResult ExecuteAbility(CastContext context)
    {
        var ability = context.Ability;
        if (ability == null) return new AbilityExecutedResult();

        var initialTargets = context.Targets;
        if (initialTargets == null || initialTargets.Count == 0 || initialTargets[0] == null)
        {
            _log.Warn("链式闪电施法失败：没有有效目标");
            return new AbilityExecutedResult();
        }

        var caster = context.Caster;
        if (caster == null) return new AbilityExecutedResult();

        var chainCount = ability.Data.Get<int>(DataKey.AbilityChainCount);
        if (chainCount <= 0) chainCount = 1;

        // 计算初始伤害：技能基础伤害 × 施法者技能伤害倍率
        var initialDamage = ability.Data.Get<float>(DataKey.AbilityDamage)
                          * caster.Data.Get<float>(DataKey.AbilityDamageBonus) / 100f;

        var bounceContext = new ChainBounceContext
        {
            Caster = caster,
            Delay = ability.Data.Get<float>(DataKey.AbilityChainDelay),
            Range = ability.Data.Get<float>(DataKey.AbilityChainRange),
            DamageDecay = ability.Data.Get<float>(DataKey.AbilityChainDamageDecay) / 100f,
            TeamFilter = ability.Data.Get<AbilityTargetTeamFilter>(DataKey.AbilityTargetTeamFilter),
            Sorting = ability.Data.Get<TargetSorting>(DataKey.TargetSorting),
            LineScene = ability.Data.Get<PackedScene>(DataKey.AbilityChainLineEffect)
        };

        var firstTarget = initialTargets[0];
        _log.Debug($"[Execute起步] 目标数: {initialTargets.Count}, 施法者: {(caster as Node)?.Name}, 第一目标: {(firstTarget as Node)?.Name}, 第一目标类型: {firstTarget.GetType().Name}, 第一目标是否死亡: {firstTarget.Data.Get<bool>(DataKey.IsDead)}");

        // 发起第一击
        ExecuteBounce(bounceContext, caster, firstTarget, initialDamage, chainCount, new HashSet<IEntity>());

        // 立即返回（后续弹跳通过 Timer 异步执行）
        return new AbilityExecutedResult { TargetsHit = 1 };
    }

    /// <summary>
    /// 执行一次闪电弹跳（造成伤害、画特效，并预定下一次弹跳）
    /// </summary>
    private void ExecuteBounce(
        ChainBounceContext context,
        IEntity fromEntity,
        IEntity currentTarget,
        float currentDamage,
        int remainingBounces,
        HashSet<IEntity> hitTargets)
    {
        // 1. 获取可靠的起点坐标（防止 fromEntity 此时已经回到了对象池）
        Vector2 fromPos = Vector2.Zero;
        if (fromEntity is Node2D fNode)
        {
            fromPos = fNode.GlobalPosition;
        }

        bool isDead = currentTarget.Data.Get<bool>(DataKey.IsDead);
        _log.Debug($"[弹跳准备] 起点: {(fromEntity as Node)?.Name}, 终点Entity: {(currentTarget as Node)?.Name}, 类型: {currentTarget.GetType().Name}, 死亡状态: {isDead}, 节点有效: {GodotObject.IsInstanceValid(currentTarget as GodotObject)}, 实例ID: {(currentTarget as Node)?.GetInstanceId()}");

        // 2. 造成伤害与画线
        if (currentTarget is IUnit unitVictim && currentTarget is Node2D targetNode)
        {
            Vector2 toPos = targetNode.GlobalPosition;

            var damageInfo = new DamageInfo
            {
                Attacker = context.Caster as Node,
                Victim = unitVictim,
                Damage = currentDamage,
                Type = DamageType.Magical,
                Tags = DamageTags.Ability
            };

            float dodgeChance = unitVictim.Data.Get<float>(DataKey.DodgeChance);
            _log.Debug($"[伤害发送前] 期望结算伤害: {currentDamage}, 目标当前Hp: {unitVictim.Data.Get<float>(DataKey.CurrentHp)}, IsDead: {isDead}, IsInvulnerable: {unitVictim.Data.Get<bool>(DataKey.IsInvulnerable)}, LifecycleState: {unitVictim.Data.Get<LifecycleState>(DataKey.LifecycleState)}, DodgeChance: {dodgeChance}");
            DamageService.Instance.Process(damageInfo);
            _log.Debug($"[伤害发送后] 实际最终伤害: {damageInfo.FinalDamage}, 目标当前Hp: {unitVictim.Data.Get<float>(DataKey.CurrentHp)} (如果无伤害，可能是被闪避或因为目标已标记为Dead)");

            // 绘制特效
            if (context.LineScene != null)
            {
                var effectNode = context.LineScene.Instantiate<Node2D>();
                targetNode.GetTree().CurrentScene.AddChild(effectNode);
                if (effectNode is ILineEffect iLine)
                {
                    iLine.PlayChain(fromPos, toPos);
                }
            }
            else
            {
                var pool = ObjectPoolManager.GetPool<LightningLineEffect>(ObjectPoolNames.LightningLinePool);
                if (pool != null)
                {
                    var lineEffect = pool.Get();
                    lineEffect?.PlayChain(fromPos, toPos);
                }
            }

            fromPos = toPos; // 下一次搜索的起点更新为命中位置
        }
        else
        {
            _log.Warn($"[目标异常] currentTarget 并非 IUnit 或并非 Node2D，无法处理！类型: {currentTarget?.GetType().Name}");
        }

        // 3. 记录已命中目标防止无脑循环
        hitTargets.Add(currentTarget);

        // 4. 判断是否结束
        if (remainingBounces <= 1)
        {
            _log.Info($"链式闪电执行完成: 总弹跳次数 {hitTargets.Count}");
            return;
        }

        // 5. 准备下一跳（延迟进行索敌与开劈）
        var nextDamage = currentDamage * context.DamageDecay;

        TimerManager.Instance.Delay(context.Delay).OnComplete(() =>
        {
            // 延迟结束后，在这个时间点查找最合适的目标，而不是提前查找
            var nextTarget = FindNextChainTarget(context, fromPos, hitTargets);
            if (nextTarget == null)
            {
                _log.Debug($"链式闪电中断: 未找到下一个目标，已弹跳 {hitTargets.Count} 次");
                return;
            }

            ExecuteBounce(context, currentTarget, nextTarget, nextDamage, remainingBounces - 1, hitTargets);
        });
    }

    /// <summary>
    /// 在指定范围内查找下一个目标
    /// </summary>
    private IEntity? FindNextChainTarget(
        ChainBounceContext context,
        Vector2 searchOrigin,
        HashSet<IEntity> excludeTargets)
    {
        // 构建目标搜索查询请求
        var query = new TargetSelectorQuery
        {
            Geometry = GeometryType.Circle,
            Origin = searchOrigin,
            Range = context.Range,
            CenterEntity = context.Caster,
            TeamFilter = context.TeamFilter,
            Sorting = context.Sorting,
            MaxTargets = -1 // 搜索所有候选者以进行手动去重，由于 TargetSelector 不直接支持排除列表，此处先获取候选集
        };

        // 执行查询
        var candidates = EntityTargetSelector.Query(query);
        _log.Debug($"[TargetQuery] 在 {searchOrigin} 半径 {context.Range} 内搜到 {candidates.Count} 个候选者, TeamFilter={context.TeamFilter}");

        // 遍历候选目标，返回第一个不在排除列表中的目标
        foreach (var candidate in candidates)
        {
            bool isExcluded = excludeTargets.Contains(candidate);
            _log.Debug($"[TargetQuery - 候选] Entity: {(candidate as Node)?.Name}, 类型: {candidate.GetType().Name}, 是否死亡: {candidate.Data.Get<bool>(DataKey.IsDead)}, 是否已命中: {isExcluded}");
            if (!isExcluded)
            {
                _log.Debug($"[TargetQuery - 选中] 决定弹跳至: {(candidate as Node)?.Name}");
                return candidate;
            }
        }

        return null;
    }
}
