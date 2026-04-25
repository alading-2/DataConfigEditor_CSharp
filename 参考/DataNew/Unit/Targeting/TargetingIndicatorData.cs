using System.Collections.Generic;
using Slime.ConfigNew;
namespace Slime.ConfigNew.Units
{
    /// <summary>
    /// 瞄准指示器配置（纯 POCO）
    /// </summary>
    public class TargetingIndicatorData : UnitData
    {
        /// <summary>全部数据。</summary>
        public static IReadOnlyList<TargetingIndicatorData> All => DataTable.GetAll<TargetingIndicatorData>();

        /// <summary>按 Name 获取数据，找不到返回 null 并记录日志。</summary>
        public static TargetingIndicatorData? Get(string name) => DataTable.GetByName<TargetingIndicatorData>(name);

        /// <summary>
        /// 是否显示血条
        /// </summary>
        public bool IsShowHealthBar { get; set; } = false;

        /// <summary>
        /// 是否无敌
        /// </summary>
        public bool IsInvulnerable { get; set; } = true;

        // ====== 实例 ======

        /// <summary>瞄准指示器</summary>
        public static readonly TargetingIndicatorData Default = new()
        {
            Name = "TargetingIndicator",
            BaseHp = 1000000f,
            BaseAttackSpeed = 0f,
            MoveSpeed = 400f,
        };
    }
}
