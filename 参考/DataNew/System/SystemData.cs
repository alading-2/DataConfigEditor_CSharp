using Slime.ConfigNew;

namespace Slime.ConfigNew.Systems;

/// <summary>
/// 系统配置（纯 POCO，不继承 Resource）。
/// <para>用于让 DataNew 支持 Data/Config/System 的纯 C# 数据源。</para>
/// </summary>
public class SystemData
{
    /// <summary>全部系统配置实例。</summary>
    public static SystemData[] All =>
    [
        ObjectPoolInit,
        TimerManager,
        ProjectStateBridge,
        EntityManager,
        DamageService,
        DamageStatisticsSystem,
        RecoverySystem,
        SpawnSystem,
        TargetingManagerRuntime,
        PauseMenuSystem,
        UIManager,
        DamageNumberRuntimeBridge,
        TestSystem,
        MouseSelectionSystem
    ];

    /// <summary>按 SystemId 获取系统配置，找不到返回 null 并记录日志。</summary>
    public static SystemData? Get(string name) => DataTable.GetByName<SystemData>(name);

    /// <summary>对象池初始化系统。</summary>
    public static readonly SystemData ObjectPoolInit = new()
    {
        SystemId = "ObjectPoolInit",
        MountGroup = SystemGroup.Base,
        Tags = SystemTag.Core | SystemTag.Runtime,
        Required = true,
        Priority = 0,
        Description = "对象池初始化系统，负责预热常用对象池"
    };

    /// <summary>定时器管理系统。</summary>
    public static readonly SystemData TimerManager = new()
    {
        SystemId = "TimerManager",
        MountGroup = SystemGroup.Base,
        Tags = SystemTag.Core | SystemTag.Runtime,
        Required = true,
        Priority = 1,
        Description = "定时器管理系统，提供全局定时器服务"
    };

    /// <summary>项目状态桥接系统。</summary>
    public static readonly SystemData ProjectStateBridge = new()
    {
        SystemId = "ProjectStateBridge",
        MountGroup = SystemGroup.Base,
        Tags = SystemTag.Core | SystemTag.Runtime,
        Required = true,
        Priority = 2,
        Description = "项目状态桥接系统，监听全局事件并同步到 ProjectStateService"
    };

    /// <summary>实体管理器。</summary>
    public static readonly SystemData EntityManager = new()
    {
        SystemId = "EntityManager",
        MountGroup = SystemGroup.Base,
        Tags = SystemTag.Core | SystemTag.Runtime,
        Required = true,
        Priority = 5,
        Description = "实体管理器，负责实体的生成、注册、销毁和组件管理。"
    };

    /// <summary>伤害处理服务。</summary>
    public static readonly SystemData DamageService = GameplayCombat(
        "DamageService",
        priority: 10,
        description: "伤害处理服务，负责伤害计算、暴击、闪避等核心战斗逻辑");

    /// <summary>伤害统计系统。</summary>
    public static readonly SystemData DamageStatisticsSystem = GameplayCombat(
        "DamageStatisticsSystem",
        priority: 11,
        description: "伤害统计系统，记录和分析战斗数据",
        dependencies: ["DamageService"]);

    /// <summary>恢复系统。</summary>
    public static readonly SystemData RecoverySystem = GameplayCombat(
        "RecoverySystem",
        priority: 12,
        description: "恢复系统，处理生命值和护盾恢复逻辑");

    /// <summary>生成系统。</summary>
    public static readonly SystemData SpawnSystem = new()
    {
        SystemId = "SpawnSystem",
        MountGroup = SystemGroup.Gameplay,
        Tags = SystemTag.Gameplay | SystemTag.Runtime,
        Priority = 13,
        AllowedFlowStates = GameFlowState.Gameplay,
        BlockedOverlays = OverlayFlags.Blocking,
        AllowedSimulationStates = SimulationState.Running,
        Description = "生成系统，负责敌人和道具的生成逻辑"
    };

    /// <summary>目标选择管理系统。</summary>
    public static readonly SystemData TargetingManagerRuntime = GameplayCombat(
        "TargetingManagerRuntime",
        priority: 14,
        description: "目标选择管理系统，提供目标查询和筛选服务");

    /// <summary>暂停菜单系统。</summary>
    public static readonly SystemData PauseMenuSystem = new()
    {
        SystemId = "PauseMenuSystem",
        MountGroup = SystemGroup.UI,
        Tags = SystemTag.UI | SystemTag.Runtime,
        Priority = 20,
        AllowedFlowStates = GameFlowState.Gameplay,
        AllowedSimulationStates = SimulationState.Any,
        Description = "暂停菜单系统，处理暂停菜单的显示和交互"
    };

    /// <summary>UI 管理系统。</summary>
    public static readonly SystemData UIManager = new()
    {
        SystemId = "UIManager",
        MountGroup = SystemGroup.UI,
        Tags = SystemTag.Core | SystemTag.UI | SystemTag.Runtime,
        Priority = 21,
        Description = "UI 管理系统，负责 UI 的创建、显示和销毁"
    };

    /// <summary>伤害数字 UI 桥接系统。</summary>
    public static readonly SystemData DamageNumberRuntimeBridge = new()
    {
        SystemId = "DamageNumberRuntimeBridge",
        MountGroup = SystemGroup.UI,
        Tags = SystemTag.Combat | SystemTag.UI | SystemTag.Runtime,
        Priority = 22,
        AllowedFlowStates = GameFlowState.Gameplay,
        BlockedOverlays = OverlayFlags.Blocking,
        AllowedSimulationStates = SimulationState.Running,
        Description = "伤害数字 UI 桥接系统，监听伤害事件并显示伤害数字"
    };

    /// <summary>测试系统。</summary>
    public static readonly SystemData TestSystem = new()
    {
        SystemId = "TestSystem",
        MountGroup = SystemGroup.Test,
        Tags = SystemTag.Debug | SystemTag.Test,
        AutoLoad = false,
        Priority = 100,
        Description = "测试系统，用于调试和监控系统运行状态"
    };

    /// <summary>鼠标选择系统。</summary>
    public static readonly SystemData MouseSelectionSystem = new()
    {
        SystemId = "MouseSelectionSystem",
        MountGroup = SystemGroup.Debug,
        Tags = SystemTag.Debug | SystemTag.Test,
        AutoLoad = false,
        Priority = 101,
        Description = "鼠标选择系统，用于调试时选择和查看实体"
    };

    /// <summary>系统唯一 Id。</summary>
    public string SystemId { get; set; } = "";

    /// <summary>系统挂载分组（Host 位置，单选）。</summary>
    public SystemGroup MountGroup { get; set; } = SystemGroup.Else;

    /// <summary>系统标签（逻辑分类和预设筛选），多选 Flags。</summary>
    public SystemTag Tags { get; set; } = SystemTag.None;

    /// <summary>是否为必需系统（无论预设如何都装载）。</summary>
    public bool Required { get; set; }

    /// <summary>默认是否自动装载（没有激活预设时使用）。</summary>
    public bool AutoLoad { get; set; } = true;

    /// <summary>首次纳入管理时的人工开关默认值。</summary>
    public bool StartEnabled { get; set; } = true;

    /// <summary>加载优先级（数值越小越优先，用于依赖排序）。</summary>
    public int Priority { get; set; }

    /// <summary>允许的流程状态（Flags 组合，为 None 表示不限制；优先使用 GameFlowState 预设组合）。</summary>
    public GameFlowState AllowedFlowStates { get; set; } = GameFlowState.None;

    /// <summary>要求存在的覆盖层（Flags 组合，为 None 表示不要求覆盖层；优先使用 OverlayFlags 预设组合）。</summary>
    public OverlayFlags RequiredOverlays { get; set; } = OverlayFlags.None;

    /// <summary>禁止的覆盖层（Flags 组合，为 None 表示不屏蔽覆盖层；优先使用 OverlayFlags 预设组合）。</summary>
    public OverlayFlags BlockedOverlays { get; set; } = OverlayFlags.None;

    /// <summary>允许的模拟状态（Flags 组合，为 None 表示不限制；优先使用 SimulationState 预设组合）。</summary>
    public SimulationState AllowedSimulationStates { get; set; } = SimulationState.None;

    /// <summary>依赖系统 Id 列表。</summary>
    public string[] Dependencies { get; set; } = System.Array.Empty<string>();

    /// <summary>系统描述（用于文档和调试）。</summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 转为运行时仍使用的 SystemConfig Resource 对象。
    /// </summary>
    public SystemConfig ToResource()
    {
        return new SystemConfig
        {
            SystemId = SystemId,
            MountGroup = MountGroup,
            Tags = Tags,
            Required = Required,
            AutoLoad = AutoLoad,
            StartEnabled = StartEnabled,
            Priority = Priority,
            AllowedFlowStates = AllowedFlowStates,
            RequiredOverlays = RequiredOverlays,
            BlockedOverlays = BlockedOverlays,
            AllowedSimulationStates = AllowedSimulationStates,
            Dependencies = Dependencies,
            Description = Description
        };
    }

    private static SystemData GameplayCombat(
        string systemId,
        int priority,
        string description,
        string[]? dependencies = null)
    {
        return new SystemData
        {
            SystemId = systemId,
            MountGroup = SystemGroup.Combat,
            Tags = SystemTag.Core | SystemTag.Combat | SystemTag.Runtime,
            Priority = priority,
            AllowedFlowStates = GameFlowState.Gameplay,
            BlockedOverlays = OverlayFlags.Blocking,
            AllowedSimulationStates = SimulationState.Running,
            Dependencies = dependencies ?? System.Array.Empty<string>(),
            Description = description
        };
    }
}
