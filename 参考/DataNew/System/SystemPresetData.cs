using Slime.ConfigNew;

namespace Slime.ConfigNew.Systems;

/// <summary>
/// 系统预设（纯 POCO，不继承 Resource）。
/// </summary>
public class SystemPresetData
{
    /// <summary>全部系统预设实例。</summary>
    public static SystemPresetData[] All =>
    [
        Default
    ];

    /// <summary>按 PresetName 获取系统预设，找不到返回 null 并记录日志。</summary>
    public static SystemPresetData? Get(string name) => DataTable.GetByName<SystemPresetData>(name);

    /// <summary>默认系统预设。</summary>
    public static readonly SystemPresetData Default = new()
    {
        PresetName = "Default",
        IsActive = true,
        EnabledTags = SystemTag.Core
            | SystemTag.Gameplay
            | SystemTag.Combat
            | SystemTag.UI
            | SystemTag.Roguelike
            | SystemTag.Runtime,
        // 调试面板与鼠标选择是当前开发期常驻入口，显式开启避免把所有 Debug/Test 标签系统都纳入默认预设。
        EnabledSystemIds = ["TestSystem", "MouseSelectionSystem"],
        Description = "默认预设，加载核心、玩法、战斗、UI、运行时系统，并显式加载调试入口系统"
    };

    /// <summary>预设名称。</summary>
    public string PresetName { get; set; } = "DefaultPreset";

    /// <summary>是否激活此预设（只允许一个 Preset 为 true）。</summary>
    public bool IsActive { get; set; }

    /// <summary>启用的系统标签（Flags 组合）。</summary>
    public SystemTag EnabledTags { get; set; } = SystemTag.None;

    /// <summary>显式启用的系统 Id 列表。</summary>
    public string[] EnabledSystemIds { get; set; } = System.Array.Empty<string>();

    /// <summary>显式禁用的系统 Id 列表（Required 系统不能被禁用）。</summary>
    public string[] DisabledSystemIds { get; set; } = System.Array.Empty<string>();

    /// <summary>预设描述（用于文档和调试）。</summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 转为运行时仍使用的 SystemPreset Resource 对象。
    /// </summary>
    public SystemPreset ToResource()
    {
        return new SystemPreset
        {
            PresetName = PresetName,
            IsActive = IsActive,
            EnabledTags = EnabledTags,
            EnabledSystemIds = EnabledSystemIds,
            DisabledSystemIds = DisabledSystemIds,
            Description = Description
        };
    }
}
