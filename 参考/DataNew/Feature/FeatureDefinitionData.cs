using System.Collections.Generic;

namespace Slime.ConfigNew.Features
{
    /// <summary>
    /// Feature 属性修改器条目（纯 POCO）
    /// </summary>
    public class FeatureModifierEntryData
    {
        /// <summary>目标 DataKey 名称（如 "AttackDamage"、"MoveSpeed"）</summary>
        public string DataKeyName { get; set; } = "";

        /// <summary>修改器类型</summary>
        public ModifierType ModifierType { get; set; }

        /// <summary>修改值</summary>
        public float Value { get; set; }

        /// <summary>优先级（数值越高越先计算）</summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Feature 配置定义（纯 POCO，不继承 Resource）
    /// </summary>
    public class FeatureDefinitionData
    {
        // ====== 基础信息 ======

        /// <summary>Feature 名称（唯一标识）</summary>
        public string? Name { get; set; } = (string)DataKey.Name.DefaultValue!;

        /// <summary>Feature 处理器 ID</summary>
        public string? FeatureHandlerId { get; set; } = (string)DataKey.FeatureHandlerId.DefaultValue!;

        /// <summary>Feature 描述（用于 UI Tooltip）</summary>
        public string? Description { get; set; } = (string)DataKey.Description.DefaultValue!;

        /// <summary>Feature 分类（用于 UI 分组）</summary>
        public string Category { get; set; } = (string)DataKey.FeatureCategory.DefaultValue!;

        /// <summary>Entity 类型标记</summary>
        public EntityType EntityType { get; set; } = EntityType.Ability;

        /// <summary>是否启用</summary>
        public bool Enabled { get; set; } = (bool)DataKey.FeatureEnabled.DefaultValue!;

        // ====== 属性修改器 ======

        /// <summary>属性修改器列表</summary>
        public List<FeatureModifierEntryData> Modifiers { get; set; } = new();
    }
}
