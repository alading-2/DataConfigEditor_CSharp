using Godot;

namespace Slime.Config.Units
{
    /// <summary>
    /// 瞄准指示器配置 - 继承自 UnitConfig
    /// 用于技能 Point 类型目标选择的可视化指示器
    /// </summary>
    [GlobalClass]
    public partial class TargetingIndicatorConfig : UnitConfig
    {

        /// <summary>
        /// 是否显示血条
        /// </summary>
        [DataKey(nameof(DataKey.IsShowHealthBar))]
        [Export] public bool IsShowHealthBar { get; set; } = false;

        /// <summary>
        /// 是否无敌
        /// </summary>
        [DataKey(nameof(DataKey.IsInvulnerable))]
        [Export] public bool IsInvulnerable { get; set; } = true;
    }
}
