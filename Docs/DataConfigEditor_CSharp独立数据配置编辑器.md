# DataConfigEditor_CSharp独立数据配置编辑器

> **Created:** 2026-04-13
> **Last Updated:** 2026-04-23
> **Status:** 开发中
> **技术栈:** .NET 10 WinForms + ReoGrid

---

## 零、定位修正

本工具不再定义为“用 `.cs` 存数据的编辑器”，而应定义为：

> **面向 C# 强类型配置的表格化查看/编辑器。**

也就是说，核心不是“数据文件后缀是什么”，而是编辑器能否理解项目自己的 C# 配置 schema：POCO 属性、继承、静态实例、enum、Flags enum、`<summary>` 注释、分组标记，以及后续的强类型单元格编辑。

因此工具只支持“受约束的 C# 配置形态”，不支持任意 C# 代码表格化。哪些 `.cs` 算可表格化文件，以 [C# 强类型配置可表格化规则](./CSharp强类型配置可表格化规则.md) 为准。

## 一、背景

### 1.1 为什么做独立工具

原方案是 Godot 编辑器插件（`addons/DataConfigEditor/`），使用 `GridContainer` 手工搭建表格 UI。存在以下根本性问题：

| 问题 | 原因 |
|------|------|
| 分组不可折叠 | GridContainer 无折叠能力 |
| 无撤销/重做 | 需手工实现 Undo/Redo 栈 |
| 无 Ctrl 多选 | GridContainer 无选择概念 |
| 无图片预览 | 只有字符串路径 |
| 性能差 | 每个单元格一个 Node（26属性 x 11实例 = 286+ Node） |
| 枚举显示混乱 | 中英混搭，OptionButton 格式错误 |
| 启动不自动加载 | Godot `_Ready` 时序问题 |

**关键决策：** 配置 schema 是纯 POCO（不继承 Resource），编辑器第一阶段不依赖 Godot 运行时，也不以 DLL 加载作为启动前提。使用 .NET 生态的电子表格组件 [ReoGrid](https://github.com/unvell/ReoGrid)（MIT 许可）可以获得接近 Excel 的表格体验。

### 1.2 ReoGrid 能力

ReoGrid 是 .NET 开源电子表格控件，提供：

| 能力 | API |
|------|-----|
| 分组折叠 | `sheet.AddOutline(RowOrColumn.Row, start, count)` |
| 撤销/重做 | 内置 Ctrl+Z / Ctrl+Y |
| 多选 | Ctrl+Click、Shift+Click、框选 |
| 批量编辑 | 选中多格 → 输入值 |
| 图片单元格 | `ImageCell` 内置支持 |
| 下拉列表 | `DropdownListCellBody` 内置 |
| 冻结行列 | `sheet.FreezeToCell(row, col)` |
| 单元格合并 | `sheet.MergeRange(range)` |
| Excel 导出 | 支持 .xlsx |

---

## 二、工具架构

### 2.1 文件结构

```
DataConfigEditor.csproj            ← 独立 net10.0-windows WinForms 项目
Src/
├── Program.cs                      ← 应用入口
├── MainForm.cs                     ← 主窗体
├── Workspace/                      ← 工作区打开、目录树、最近目录
├── Documents/                      ← TableDocument / TableRow / TableCell
├── Presentation/                   ← WorkspacePresenter
├── Core/                           ← 旧扫描器与配置元数据
├── Parsing/                        ← CsTableParser / SourceParser / 注释与回写
└── UI/                             ← 表格构建
tests/                              ← 单元测试
```

### 2.2 数据流

```
┌──────────────────────────────────────────────────────┐
│ 1. 用户打开工作区目录                                  │
│    ↓ 左侧显示文件夹与 .cs 文件树                         │
│ 2. 点击单个 .cs 文件                                   │
│    ↓                                                  │
│ 3. CsTableParser / SourceParser 解析受限配置形态        │
│    ↓ 提取类、属性、静态实例、注释                         │
│ 4. 生成 TableDocument 中间模型                         │
│    ↓                                                  │
│ 5. SheetBuilder 把 TableDocument 显示为表格             │
│    ↓                                                  │
│ 6. 不可表格化文件显示诊断视图                           │
└──────────────────────────────────────────────────────┘
```

后续阶段再增强跨文件 enum、继承链、类型索引、强类型编辑和保存回写。旧文档中“先加载 DLL 拿 Type，再读 `.cs` 注释，再回写 `.cs`”的路线只作为历史方案保留，不再是第一阶段核心架构。

### 2.3 与项目的关系

```
DataConfigEditor_CSharp/   ← 独立 net10.0-windows WinForms 工具
  ├── 读取: 用户打开工作区内的 .cs 文件
  ├── 解析: 受限 C# 配置形态 → TableDocument
  ├── 显示: 表格视图或诊断视图
  └── 后续: 对可定位对象初始化器做保存回写

游戏项目 Data/DataNew/     ← 推荐作为主要配置工作区
游戏项目 Src/ 或 DataKey/   ← 后续作为 enum / 基类索引来源
```

---

## 三、核心功能

### 3.1 表格布局

```
┌────────┬──────────┬──────────────┬──────────┬─────────┬──────────┐
│ 实例名  │ Name     │ AbilityType  │ Cooldown │ IconPath│ Damage   │
│        │ 名称     │ 技能类型      │ 冷却时间  │ 图标    │ 伤害     │ ← 单行表头：字段名 + 中文注释
├────────┼──────────┼──────────────┼──────────┼─────────┼──────────┤
│ ▼ 基础信息                                                       │ ← 可折叠分组
│ Dash   │ 冲刺     │ Active 主动   │ 1.0      │ [缩略图]│ 0        │
│ Slam   │ 猛击     │ Active 主动   │ 0        │ [缩略图]│ 30       │
│ ▶ 触发模式  ← 已折叠                                              │
│ ▶ 消耗与冷却                                                     │
│ ▼ 目标选择                                                       │
│ Dash   │ 300      │ 300          │ 1        │ 0       │          │
└────────┴──────────┴──────────────┴──────────┴─────────┴──────────┘
```

### 3.2 枚举下拉

- 下拉选项显示**英文名**（如 `Active`、`Manual`、`Passive`）
- 选中后单元格显示英文名
- 悬停 tooltip 显示**中文注释**（如"主动技能"、"手动触发"）

### 3.3 图片预览

- 路径列（`*Path`、`*Scene`）显示缩略图
- `res://` 路径自动映射为项目本地文件路径
- 文件不存在时显示占位符（灰色边框 + "?"）

### 3.4 批量编辑

- Ctrl + 多选多个单元格
- 直接输入值 → 所有选中格同时修改
- 或 Shift + Click 范围选

### 3.5 撤销/重做

- ReoGrid 内置 Ctrl+Z / Ctrl+Y
- 无需手工实现

### 3.6 保存

- 点击保存按钮 → CsFileWriter 写回 .cs 源文件
- 正则匹配静态初始化器 `{ ... }`，替换属性值
- 状态栏显示已修改提示

---

## 四、使用方法

### 4.1 构建

```bash
cd DataConfigEditor_CSharp
dotnet restore
dotnet build
```

### 4.2 运行

```bash
dotnet run --project DataConfigEditor.csproj
```

或直接运行编译后的 exe。

### 4.3 操作流程

1. 启动工具。
2. 打开工作区目录，例如游戏项目的 `Data/DataNew`。
3. 左侧目录树选择 `.cs` 文件。
4. 如果文件符合可表格化规则，右侧显示表格。
5. 如果文件不符合规则，右侧显示诊断原因。
6. 第一阶段以只读查看为主，编辑、枚举下拉、批量修改、保存回写进入后续阶段。

---

## 五、技术要点

### 5.1 解析路线

第一阶段不加载游戏 DLL，也不执行用户代码。工具先把单个 `.cs` 文件解析成 `TableDocument`：

```text
.cs 文件 → CsTableParser → TableDocument → 表格或诊断视图
```

后续如果正则解析无法稳定覆盖对象初始化器、注释 trivia、继承节点等结构，再切换到 Roslyn Syntax API。工程级语义、MSBuildWorkspace 和 DLL 只在确实需要真实项目语义时再引入。

### 5.2 移植策略

从旧插件移植的文件，仅需以下修改：

| 修改项 | 说明 |
|--------|------|
| 移除 `#if TOOLS` / `#endif` | 不再是 Godot 插件 |
| 移除 `using Godot` | 替换 `GD.Print` → `Console.WriteLine` |
| 替换 `ProjectSettings.GlobalizePath` | 改为传入项目根路径参数 |
| 移除 `DataMeta`/`DataKey`/`DataRegistry` 依赖 | 简化为受限源码解析和表格文档模型 |

### 5.3 枚举注释来源

```
// AbilityTriggerMode.cs
public enum AbilityTriggerMode
{
    /// <summary>手动触发</summary>
    Manual,
    /// <summary>自动触发</summary>
    Auto,
    /// <summary>被动永久</summary>
    Permanent,
}
```

CsCommentParser 解析 `<summary>` 注释 → EnumCommentCache 缓存 → 枚举下拉 tooltip 显示"手动触发"。

---

## 六、与旧插件的关系

| | 旧方案（Godot 插件） | 新方案（独立工具） |
|---|---|---|
| 位置 | `addons/DataConfigEditor/` | `DataConfigEditor_CSharp/` |
| 依赖 | Godot SDK、项目 DLL | .NET 10 WinForms + ReoGrid，第一阶段不依赖游戏 DLL |
| 表格控件 | GridContainer（手工节点） | ReoGrid（电子表格） |
| 撤销/重做 | 无 | 内置 |
| 分组折叠 | 无 | 内置 Outlines |
| 多选 | 无 | 内置 |
| 图片预览 | 无 | ImageCell |
| 枚举显示 | 中英混搭 | 英文名 + 中文 tooltip |
| 保存 | CsFileWriter（共用） | 后续仅回写可定位对象初始化器 |

旧插件暂时保留在 `addons/DataConfigEditor/`，待新工具稳定后可移除。
