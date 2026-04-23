# 纯 C# 数据配置方案文档

> **Session ID:** 完整会话记录见 `tres改纯C#.md`
> **Created:** 2026-04-09
> **Last Updated:** 2026-04-09

---

## 一、核心问题

### 1.1 .tres 与 C# 不互通

**问题描述：**
- `Dash.cs` 中定义 `FeatureId.Ability.Movement.Dash`（代码常量）
- `DashConfig.tres` 中定义 `FeatureGroupId="技能.位移"`（资源文件属性）
- 两者本应关联，但 Godot 无法让 .tres 直接引用 C# 常量，导致需要**双端维护**

**影响：**
- 分组变更时需要同时修改 C# 代码和 .tres 文件
- 容易出现不一致，维护成本高
- 无法实现"一改全改"

### 1.2 GDS 表格插件局限性

**SlimeDataTable_CSharp 插件问题：**
- **继承失效**：`EnemyConfig` 继承 `UnitConfig`，但表格中无法重置父类属性
- **无法读取 C# 注释**：GDScript 无法访问 C# 源码的 `<summary>` 注释
- **类型支持差**：枚举显示为整数，无下拉选择
- **编辑体验差**：不如 Excel 直观

### 1.3 历史方案反复

**之前尝试过纯 C# 方案又改回 .tres 的原因：**
1. **Godot UI 支持**：.tres 可在检查器中直接编辑
2. **现成插件**：`resources_spreadsheet_view` 提供了表格视图
3. **PackedScene 拖拽**：资源引用可通过拖拽设置

---

## 二、解决方案

### 2.1 整体架构

```
Data/
├── Data/                    # 原有 .tres 方案（保持不变）
│   ├── Ability/
│   ├── Unit/
│   └── Feature/
├── DataNew/                 # 新的纯 C# POCO 方案
│   ├── Ability/
│   │   ├── AbilityConfigData.cs      # 11 个技能实例
│   │   └── ChainAbilityConfigData.cs # 连锁闪电
│   ├── Unit/
│   │   ├── UnitConfigData.cs         # 抽象基类
│   │   ├── Player/PlayerConfigData.cs
│   │   ├── Enemy/EnemyConfigData.cs  # 2 个敌人实例
│   │   └── Targeting/...
│   └── Feature/
└── ResourceManagement/      # 路径常量（ResourceGenerator 自动生成）
```

### 2.2 核心设计决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 配置类类型 | 纯 POCO（不继承 Resource） | 完全脱离 Godot 资源系统，代码即数据 |
| 场景引用 | `string` 路径替代 `PackedScene` | 字符串更简单，无需 Godot 场景兼容 |
| 插件数据源 | 只读 `DataNew` | 与旧数据分离，渐进迁移 |
| 表格布局 | 支持切换（行=实例/行=属性） | 属性多时切换为行=属性更直观 |

### 2.3 数据迁移对照

| 原 .tres 文件 | 新 C# 文件 | 实例数量 |
|---------------|------------|---------|
| 16 个 .tres | 7 个 .cs | 16 个静态实例 |
| `DashConfig.tres` | `AbilityConfigData.Dash` | 静态字段 |
| `yuren.tres` | `EnemyConfigData.Yuren` | 静态字段 |
| `PackedScene` 引用 | `string` 路径 | 如 `"res://assets/..."` |

---

## 三、插件实现

### 3.1 文件结构

```
addons/DataConfigEditor/
├── plugin.cfg                    # 插件配置（v3.0）
├── DataConfigEditorPlugin.cs     # 主入口（主屏幕插件）
├── ConfigTablePanel.cs           # 表格 UI（GridContainer）
├── ConfigReflectionCache.cs      # 反射缓存
├── CsCommentParser.cs            # 源码注释解析
├── CsFileWriter.cs               # 保存回 .cs 文件
└── EnumCommentCache.cs           # 枚举中文注释缓存
```

### 3.2 功能特性

| 功能 | 实现方式 |
|------|---------|
| 普通 Enum 下拉 | `OptionButton` + 解析枚举成员注释 |
| `[Flags]` 枚举编辑 | `MenuButton + PopupMenu` 多选勾选，回写组合枚举名 |
| 多层表头 | 行=实例布局下显示“分类 / DataKey / DataKey说明 / 字段” |
| 元数据映射 | `Property -> DataKey -> DataMeta`，支持别名和 `const string` 路径键 |
| 布局切换 | 按钮：行=实例列=属性 ↔ 行=属性列=实例 |
| 类型化单元格 | Enum→OptionButton, bool→CheckBox, 数值→SpinBox |
| 路径字段 | `PathLineEdit` 支持拖拽项目内资源、自动转 `res://`、即时校验 |
| 批量修改 | 工具栏选择属性后，对当前配置类所有实例批量赋值 |
| 搜索过滤 | 按属性名/注释搜索 |
| 保存回源码 | 正则匹配静态初始化器，替换属性值 |

### 3.3 关键代码模式

**POCO 配置类示例：**
```csharp
namespace Slime.ConfigNew.Abilities
{
    public class AbilityConfigData
    {
        // ====== 基础信息 ======
        /// <summary>技能名称</summary>
        public string? Name { get; set; }
        
        /// <summary>触发模式</summary>
        public AbilityTriggerMode AbilityTriggerMode { get; set; }
        
        // ====== 实例 ======
        /// <summary>冲刺</summary>
        public static readonly AbilityConfigData Dash = new()
        {
            Name = "冲刺",
            AbilityTriggerMode = AbilityTriggerMode.Manual,
            AbilityCooldown = 1.0f,
        };
    }
}
```

**静态实例加载：**
```csharp
var staticFields = configType
    .GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.IsInitOnly && f.FieldType == configType);
    
foreach (var field in staticFields)
{
    var value = field.GetValue(null); // 获取静态字段值
    instances.Add(new InstanceInfo { Name = field.Name, Instance = value });
}
```

---

## 四、技术难点与解决

### 4.1 与 Godot 场景兼容

**问题：** 原 .tres 中 `PackedScene` 引用通过拖拽设置

**解决：**
- 改为 `string` 路径（如 `"res://assets/Effect/020/..."`）
- 路径常量由 `ResourceGenerator` 自动维护
- 插件中用 `LineEdit` 编辑路径

### 4.2 枚举中文注释

**问题：** 枚举值在 .tres 中是整数，用户不知道 `2` 代表什么

**解决：**
1. `EnumCommentCache` 扫描所有枚举 .cs 文件
2. 解析每个成员的 `<summary>` 注释
3. `OptionButton` 显示 `"Active / 主动技能"` 格式

### 4.3 继承属性

**问题：** `EnemyConfig` 继承 `UnitConfig`，但原插件只显示子类属性

**解决：**
```csharp
private static void CollectPropertiesRecursive(Type type, List<PropertyInfo> result)
{
    if (type == null || type == typeof(object)) return;
    
    var props = type.GetProperties(BindingFlags.DeclaredOnly | ...);
    result.AddRange(props);
    
    CollectPropertiesRecursive(type.BaseType, result); // 递归父类
}
```

### 4.4 保存回 .cs 文件

**问题：** 静态初始化器 `new() { }` 语法匹配困难

**解决：**
- 使用平衡组匹配嵌套大括号
- `FindMatchingBrace()` 找到配对的 `}`
- 精确替换属性值

---

## 五、与替代方案对比

### 5.1 Luban 配置生成

| 对比项 | Luban | 本方案 |
|--------|-------|--------|
| 数据格式 | Excel/JSON/YAML → C# | 直接 C# |
| 工作流 | 外部编辑器 → 生成代码 | Godot 内编辑 |
| 注释支持 | Excel 备注 | C# `<summary>` |
| Godot 集成 | 需要适配 | 原生 EditorPlugin |

### 5.2 resources_spreadsheet_view

| 对比项 | resources_spreadsheet_view | DataConfigEditor |
|--------|---------------------------|------------------|
| 数据源 | .tres 文件 | 纯 C# |
| Enum 显示 | 整数 | 中文下拉 |
| 继承支持 | 部分 | 完整递归 |
| 保存 | 自动保存 .tres | 手动保存 .cs |

---

## 六、遗留问题与优化方向

### 6.1 已知问题

| 问题 | 严重性 | 状态 |
|------|--------|------|
| 大量单元格性能问题 | 低 | 待优化（建议虚拟化滚动） |
| 无撤销/重做功能 | 低 | 待实现 |
| string 路径缺少文件选择器按钮 | 低 | 待补文件对话框（当前已支持拖拽 + 校验） |

### 6.2 优化方向

1. **分组视觉效果增强**：表头背景色区分分组
2. **Flags 枚举多选弹窗**：创建自定义控件
3. **路径选择器**：集成 Godot 文件对话框
4. **热重载支持**：监听 .cs 文件变化自动刷新

---

## 七、使用指南

### 7.1 启用插件

1. 项目 → 项目设置 → 插件 → 启用 `DataConfigEditor`
2. 编辑器顶部出现「DataConfig」标签页

### 7.2 编辑数据

1. 下拉选择配置类（如 `AbilityConfigData`）
2. 表格显示所有静态实例（如 `Dash`, `Slam` 等）
3. 行=实例布局下先看四层表头：分类 / DataKey / DataKey说明 / 字段
4. 直接编辑单元格（普通 Enum 下拉、`[Flags]` 勾选、路径拖拽/校验、数值 SpinBox）
5. 如需统一修改多个实例，可在工具栏右侧使用“批量属性 + 批量值 + 批量应用”
6. 点击「保存」写回 .cs 文件

### 7.3 布局切换

- 默认：**行=实例，列=属性**（适合实例少属性多）
- 点击「布局: 行=实例」按钮切换
- 切换后：**行=属性，列=实例**（适合属性少实例多）

### 7.4 常见故障排查

- 如果顶部状态栏已经显示“`33 属性 | 1 实例 | 源文件: xxx.cs`”之类信息，但中间表格仍然整块空白，优先检查 `addons/DataConfigEditor/ConfigTablePanel.cs`
- `ConfigTablePanel.RebuildGrid()` 不能只切换 `_grid.Visible`，还必须同步切换父级 `_scrollV.Visible`；父容器保持隐藏时，子节点即使已成功创建也不会显示
- 当前统一入口是 `SetContentVisibility()`，排查此类 UI 空白问题时优先看这里的 `_scrollV / _grid / _emptyPanel` 三者状态是否一致

---

## 八、总结

### 8.1 方案优势

1. **数据与代码合一**：修改 C# 常量自动影响所有配置
2. **脱离 Godot 资源系统**：不再依赖 .tres 文件
3. **完整继承支持**：递归收集父类属性
4. **中文注释友好**：解析 `<summary>` 显示在表格中
5. **类型化编辑**：Enum 下拉、bool 复选框、数值滑块

### 8.2 方案局限

1. **PackedScene 需改为字符串路径**：失去拖拽便利
2. **需要手动保存**：不像 .tres 自动保存
3. **新建实例需改代码**：不能在表格中新增行

### 8.3 迁移建议

1. **渐进迁移**：新数据放在 `DataNew`，旧数据保持 `Data/Data`
2. **优先迁移简单配置**：先迁移枚举多的配置类
3. **后续适配 Data.LoadFromResource()**：支持从纯 C# 加载

---

## 九、参考资料

- Godot EditorPlugin 文档：https://docs.godotengine.org/en/stable/tutorials/plugins/editor/making_main_screen_plugins.html
- C# 反射：https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/reflection
- 正则平衡组：https://learn.microsoft.com/en-us/dotnet/standard/base-types/grouping-constructs-in-regular-expressions#balancing-group-definitions
