namespace Slime.ConfigNew.Abilities
{
    /// <summary>
    /// 链式技能配置（纯 POCO，继承 AbilityConfigData）
    /// </summary>
    public class ChainAbilityConfigData : AbilityConfigData
    {
        // ====== 链式效果 ======

        /// <summary>
        /// 链式弹跳次数
        /// </summary>
        public int ChainCount { get; set; }

        /// <summary>
        /// 链式弹跳范围（每跳的搜索半径）
        /// </summary>
        public float ChainRange { get; set; }

        /// <summary>
        /// 链式弹跳延时 (秒)
        /// </summary>
        public float ChainDelay { get; set; }

        /// <summary>
        /// 链式伤害衰减系数 (0-100，100=无衰减)
        /// </summary>
        public float ChainDamageDecay { get; set; }

        /// <summary>
        /// 链式连线特化表现场景路径 (res:// 路径字符串)
        /// </summary>
        public string LineEffectScenePath { get; set; } = "";

        // ====== 实例 ======

        /// <summary>连锁闪电</summary>
        public static readonly ChainAbilityConfigData ChainLightning = new()
        {
            Name = "连锁闪电",
            FeatureGroupId = "技能.主动",
            FeatureHandlerId = "技能.主动.连锁闪电",
            Description = "释放链式闪电，在多个敌人间弹跳造成魔法伤害，每次弹跳伤害衰减",
            AbilityIconPath = "res://icon.svg",
            AbilityTriggerMode = AbilityTriggerMode.Manual,
            AbilityCostType = AbilityCostType.Mana,
            AbilityCooldown = 1.0f,
            AbilityTargetSelection = AbilityTargetSelection.Entity,
            AbilityCastRange = 600f,
            AbilityDamage = 50f,
        };
    }
}
