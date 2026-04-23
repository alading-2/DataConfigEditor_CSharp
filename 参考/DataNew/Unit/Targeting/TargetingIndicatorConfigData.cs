namespace Slime.ConfigNew.Units
{
    /// <summary>
    /// 瞄准指示器配置（纯 POCO）
    /// </summary>
    public class TargetingIndicatorConfigData : UnitConfigData
    {
        /// <summary>
        /// 是否显示血条
        /// </summary>
        public bool IsShowHealthBar { get; set; }

        /// <summary>
        /// 是否无敌
        /// </summary>
        public bool IsInvulnerable { get; set; }

        // ====== 实例 ======

        /// <summary>瞄准指示器</summary>
        public static readonly TargetingIndicatorConfigData Default = new()
        {
            Name = "TargetingIndicator",
            BaseHp = 1000000f,
            BaseAttackSpeed = 0f,
            MoveSpeed = 400f,
        };
    }
}
