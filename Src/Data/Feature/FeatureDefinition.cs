using Godot;
using Godot.Collections;

namespace Slime.Config.Features
{
    /// <summary>
    /// Feature 配置资源基类 - 数据驱动的 Feature 定义模板
    ///
    /// 使用方式：
    /// - 在 Godot 编辑器创建 FeatureDefinition 资源并配置参数
    /// - 通过 EntityManager.AddAbility(owner, featureDefinition) 授予
    /// - FeatureSystem 在 Granted/Removed 时自动应用/回滚 Modifiers
    /// - 若需复杂逻辑，注册同名 IFeatureHandler（由 FeatureHandlerRegistry 调用）
    /// </summary>
    [GlobalClass]
    public partial class FeatureDefinition : Resource
    {
        // ============ 基础信息 ============

        /// <summary>Feature 名称（唯一标识，与 IFeatureHandler.FeatureId 对应）</summary>
        [ExportGroup("基础信息")]
        [DataKey(nameof(DataKey.Name))]
        [Export] public string? Name { get; set; }

        /// <summary>Feature 处理器 ID（与 IFeatureHandler.FeatureId 精确对应；建议使用完整唯一 ID）</summary>
        [DataKey(nameof(DataKey.FeatureHandlerId))]
        [Export] public string? FeatureHandlerId { get; set; }

        /// <summary>Feature 描述（用于 UI Tooltip）</summary>
        [DataKey(nameof(DataKey.Description))]
        [Export] public string? Description { get; set; }

        /// <summary>Feature 分类（用于 UI 分组）</summary>
        [DataKey(nameof(DataKey.FeatureCategory))]
        [Export] public string Category { get; set; } = "";

        /// <summary>Entity 类型标记</summary>
        [DataKey(nameof(DataKey.EntityType))]
        [Export] public EntityType EntityType { get; set; } = EntityType.Ability;

        /// <summary>是否启用</summary>
        [DataKey(nameof(DataKey.FeatureEnabled))]
        [Export] public bool Enabled { get; set; } = true;

        // ============ 属性修改器 ============

        /// <summary>
        /// 属性修改器列表（Permanent Feature 最常用）
        /// 授予时自动施加，移除时自动回滚，无需编写 IFeatureHandler。
        /// </summary>
        [ExportGroup("属性修改器")]
        [DataKey(DataKey.FeatureModifiers)]
        [Export] public Array<FeatureModifierEntry> Modifiers { get; set; } = new();
    }
}
