# 面向 C# 强类型配置的表格编辑器 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有工作区浏览和基础表格查看能力上，分阶段完成一个可验收的 C# 强类型配置表格查看/编辑器。

**Architecture:** 先稳定工作区与表格布局，再重构设置系统，然后逐步引入表格视图状态、类型索引、编辑操作、撤销重做和源码回写。UI 只消费工作区模型、表格文档、视图状态和设置模型；解析与回写逻辑保持在 Parser/Document 层，避免 MainForm 继续膨胀。

**Tech Stack:** .NET 10 WinForms, DataGridView/ReoGrid 评估路径, xUnit, System.Text.Json, 后续可选 Roslyn Syntax API。

---

## 当前可执行计划

本文件保留完整产品路线和阶段验收边界。下一阶段不要直接按本文件的 Phase 2-4 粗粒度条目开工，而应执行更细的 superpowers 计划：

- [Strongly Typed Config Editor Next Phase Implementation Plan](./superpowers/plans/2026-04-23-strongly-typed-config-editor-next-phase.md)

该计划只覆盖下一批可验收功能：工作区隐藏规则、表格布局统一、设置面板重构。完成并通过用户验收后，再为筛选/排序/搜索生成下一份阶段计划。

执行记录：2026-04-23 已在 `feat/strongly-typed-config-editor-next-phase` worktree 中完成该 next-phase 计划的代码实现和自动验证；Windows 手工验收仍需用户在本机界面确认。

## 0. 执行原则

- 按阶段执行，每个阶段完成后停止并让用户验收。
- 每个阶段必须能独立构建、测试、手工验证。
- 优先修正当前明显体验问题：目录树隐藏、表格布局统一、设置面板。
- 不把工具做成通用 C# IDE；只围绕受限 C# 配置表。
- 不执行用户项目代码。
- 不破坏用户已有未提交改动。
- 每阶段完成后更新对应文档和验收记录。

## 1. 文件职责规划

### 1.1 现有文件继续使用

| 文件 | 职责 |
|------|------|
| `Src/MainForm.cs` | 主窗体和顶层 UI 组装；后续需要把设置、表格工具栏、诊断面板拆出去 |
| `Src/Workspace/WorkspaceService.cs` | 构建工作区树；后续加入隐藏规则 |
| `Src/Workspace/WorkspaceEntry.cs` | 工作区节点模型；后续加入隐藏状态 |
| `Src/UI/SheetBuilder.cs` | 构建表格；后续承担统一布局、列样式、基础排序/筛选入口 |
| `Src/Documents/TableDocument.cs` | 表格文档模型入口 |
| `Src/Documents/TableColumn.cs` | 列 schema；后续加入类型、分组、可编辑性、来源 |
| `Src/Documents/TableRow.cs` | 行模型；后续加入实例注释和源码定位 |
| `Src/Documents/TableCell.cs` | 单元格模型；后续加入原始表达式、类型化值、诊断和可编辑性 |
| `Src/Parsing/CsTableParser.cs` | 单文件解析为表格文档 |
| `Src/Parsing/SourceParser.cs` | 当前正则解析器；后续可逐步替换为 Roslyn |
| `Src/Settings/UiSettings.cs` | 全局 UI 设置；后续拆分 App/Workspace/Table/Edit 设置 |
| `Src/Settings/UiSettingsStore.cs` | 设置持久化 |

### 1.2 建议新增文件

| 文件 | 职责 |
|------|------|
| `Src/Workspace/WorkspaceSettings.cs` | 工作区隐藏规则、显示隐藏项、目录展开状态 |
| `Src/Workspace/WorkspaceSettingsStore.cs` | 工作区设置读写 |
| `Src/Workspace/WorkspaceExcludeMatcher.cs` | 匹配 `**/bin/**`、`**/*.uid` 等隐藏规则 |
| `Src/UI/SettingsDialog.cs` | 设置面板窗口 |
| `Src/UI/SettingsViewModel.cs` | 设置面板状态、应用/确认/取消逻辑 |
| `Src/UI/TableLayoutOptions.cs` | 表格边距、表头高度、列宽等布局选项 |
| `Src/UI/TableViewState.cs` | 当前表格筛选、排序、隐藏列、选择状态 |
| `Src/UI/TableFilter.cs` | 列筛选条件 |
| `Src/UI/TableSorter.cs` | 按列类型排序 |
| `Src/Editing/EditOperation.cs` | 单格/批量编辑操作 |
| `Src/Editing/EditHistory.cs` | 撤销/重做栈 |
| `Src/Editing/CellValueFormatter.cs` | UI 值转 C# 表达式 |
| `Src/Parsing/CsTableWriter.cs` | 保存回 `.cs` 对象初始化器 |
| `Src/Diagnostics/TableDiagnostic.cs` | 文件/列/单元格/保存诊断统一模型 |

### 1.3 建议新增测试

| 文件 | 覆盖 |
|------|------|
| `tests/DataConfigEditor.Tests/Workspace/WorkspaceExcludeMatcherTests.cs` | 隐藏规则匹配 |
| `tests/DataConfigEditor.Tests/Workspace/WorkspaceSettingsStoreTests.cs` | 工作区设置持久化 |
| `tests/DataConfigEditor.Tests/UI/TableLayoutOptionsTests.cs` | 布局设置归一化 |
| `tests/DataConfigEditor.Tests/UI/TableViewStateTests.cs` | 筛选、排序、隐藏列状态 |
| `tests/DataConfigEditor.Tests/Editing/EditHistoryTests.cs` | 撤销重做 |
| `tests/DataConfigEditor.Tests/Editing/CellValueFormatterTests.cs` | 类型值格式化为 C# 表达式 |
| `tests/DataConfigEditor.Tests/Parsing/CsTableWriterTests.cs` | 保存回写 |

---

## Phase 1: 文档管理和计划固化

**目标:** 文档入口清晰，旧资料归档，新方案执行文档可直接指导后续开发。

**Files:**

- Create: `Docs/README.md`
- Create: `Docs/面向CSharp强类型配置的表格编辑器方案执行文档.md`
- Move: `Docs/其他/` -> `Docs/旧文档/其他/`
- Modify: `Docs/面向CSharp强类型配置的表格编辑器完善计划.md`

### Task 1.1: 归档旧文档

- [x] **Step 1: 创建旧文档目录**

Run: `mkdir -p Docs/旧文档`

Expected: `Docs/旧文档/` exists.

- [x] **Step 2: 移动旧资料**

Run: `mv Docs/其他 Docs/旧文档/其他`

Expected: `Docs/旧文档/其他/问题.md` and screenshots exist.

- [x] **Step 3: 验证旧路径只出现在归档说明和执行记录中**

Run: `rg -n "Docs/其他|\\./其他|其他/" . --glob '!bin/**' --glob '!obj/**'`

Expected: only this execution document and `Docs/README.md` mention the old path for archive tracking.

### Task 1.2: 建立文档索引

- [x] **Step 1: 新增 `Docs/README.md`**

内容必须包含：

- 当前有效文档。
- 背景文档。
- 阶段设计与计划。
- 旧文档归档位置。
- 文档维护规则。

- [x] **Step 2: 验证索引链接目标存在**

Run: `find Docs -maxdepth 3 -type f -print | sort`

Expected: 能看到 `Docs/README.md`、当前有效文档、`Docs/旧文档/其他/*`。

### Task 1.3: 固化执行文档

- [x] **Step 1: 新增本执行文档**

Create: `Docs/面向CSharp强类型配置的表格编辑器方案执行文档.md`

- [x] **Step 2: 在完善计划中引用执行文档**

Modify: `Docs/面向CSharp强类型配置的表格编辑器完善计划.md`

应加入：

```markdown
执行步骤与开发者验收见 [面向 C# 强类型配置的表格编辑器方案执行文档](./面向CSharp强类型配置的表格编辑器方案执行文档.md)。
```

**开发者验收:**

- `Docs/README.md` 是文档入口。
- 旧资料不再散落在 `Docs/` 根目录。
- 新执行文档能回答“下一步开发先做什么、做到什么算完成”。

---

## Phase 2: 工作区隐藏规则

**目标:** 左侧目录树像 VS Code 一样支持隐藏无关目录和文件。

**Files:**

- Create: `Src/Workspace/WorkspaceSettings.cs`
- Create: `Src/Workspace/WorkspaceSettingsStore.cs`
- Create: `Src/Workspace/WorkspaceExcludeMatcher.cs`
- Modify: `Src/Workspace/WorkspaceService.cs`
- Modify: `Src/Workspace/WorkspaceEntry.cs`
- Modify: `Src/MainForm.cs`
- Test: `tests/DataConfigEditor.Tests/Workspace/WorkspaceExcludeMatcherTests.cs`
- Test: `tests/DataConfigEditor.Tests/Workspace/WorkspaceSettingsStoreTests.cs`
- Test: `tests/DataConfigEditor.Tests/Workspace/WorkspaceServiceTests.cs`

### Task 2.1: 增加隐藏规则模型

- [ ] **Step 1: 写 `WorkspaceExcludeMatcherTests`**

测试要求：

- `bin/`、`obj/`、`.godot/`、`.git/`、`.idea/`、`.vscode/`、`.history/`、`.superpowers/` 默认隐藏。
- `*.uid` 默认隐藏。
- `.cs` 文件默认显示。
- 开启 `ShowHiddenEntries` 后隐藏项仍返回，但标记为 hidden。

- [ ] **Step 2: 实现 `WorkspaceSettings`**

需要包含：

```csharp
public sealed record WorkspaceSettings
{
    public bool ShowHiddenEntries { get; init; }
    public IReadOnlyList<string> ExcludePatterns { get; init; } = DefaultExcludePatterns;
    public static IReadOnlyList<string> DefaultExcludePatterns { get; }
}
```

- [ ] **Step 3: 实现 `WorkspaceExcludeMatcher`**

先支持最小可用规则：

- 精确目录名：`bin`、`obj`、`.git`
- 后缀通配：`*.uid`
- 路径段匹配：`**/bin/**`

- [ ] **Step 4: 更新 `WorkspaceService.BuildTree`**

签名建议：

```csharp
public WorkspaceEntry BuildTree(string rootPath, WorkspaceSettings? settings = null)
```

隐藏项默认不进入树；`ShowHiddenEntries = true` 时进入树并标记。

- [ ] **Step 5: 更新 `WorkspaceEntry`**

新增：

```csharp
public bool IsHidden { get; init; }
```

- [ ] **Step 6: 跑测试**

Run: `dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter Workspace`

Expected: workspace tests pass.

**开发者验收:**

- 打开测试 DataNew 时不显示 `.uid`。
- 默认不显示 `bin/obj/.git/.godot`。
- 可通过设置显示隐藏项，隐藏项在树里灰显。
- 隐藏规则保存后重启仍生效。

---

## Phase 3: 表格布局统一

**目标:** 修复右侧表格位置不统一、部分表格过于靠上、滚动条位置异常等体验问题。

**Files:**

- Create: `Src/UI/TableLayoutOptions.cs`
- Modify: `Src/Settings/UiSettings.cs`
- Modify: `Src/UI/SheetBuilder.cs`
- Modify: `Src/MainForm.cs`
- Test: `tests/DataConfigEditor.Tests/UI/TableLayoutOptionsTests.cs`

### Task 3.1: 固定表格主区域布局规则

- [ ] **Step 1: 新增布局设置测试**

测试要求：

- 默认顶部边距为 8px。
- 表头高度有最小值。
- 行高、列宽、表头高度都被限制在合理范围。

- [ ] **Step 2: 新增 `TableLayoutOptions`**

包含：

```csharp
public sealed record TableLayoutOptions
{
    public int ContentPadding { get; init; } = 8;
    public int HeaderHeight { get; init; } = 48;
    public int RowHeight { get; init; } = 28;
    public int InstanceColumnWidth { get; init; } = 140;
    public int DefaultColumnWidth { get; init; } = 180;
    public bool FreezeInstanceColumn { get; init; } = true;
}
```

- [ ] **Step 3: 调整 `MainForm` 右侧布局**

要求：

- `_gridHost.Dock = DockStyle.Fill`。
- `_gridHost.Padding` 来自统一 `ContentPadding`。
- grid 永远 `DockStyle.Fill`。
- 不按行数改变 grid 容器高度。

- [ ] **Step 4: 调整 `SheetBuilder`**

要求：

- 固定表头高度。
- 固定实例名列宽。
- 不自动让少量行压缩表格区域。
- 空白区域属于 grid，不是独立漂浮容器。

- [ ] **Step 5: 跑测试和手工检查**

Run: `dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter TableLayout`

Manual:

- 打开 `AbilityConfigData.cs`。
- 打开 `PlayerConfigData.cs`。
- 对比两张表左上角位置、表头高度、滚动条位置。

**开发者验收:**

- 多个 `.cs` 表格从同一位置开始。
- 表头高度统一。
- 横向滚动条在底部，不漂在中间。
- 少量行表格不会像一个小框贴在顶部。

---

## Phase 4: 设置面板重构

**目标:** 用成熟设置面板替代当前下拉碎片设置，支持应用、确定、取消、恢复默认和即时预览。

**Files:**

- Create: `Src/UI/SettingsDialog.cs`
- Create: `Src/UI/SettingsViewModel.cs`
- Modify: `Src/Settings/UiSettings.cs`
- Modify: `Src/Settings/UiSettingsStore.cs`
- Modify: `Src/MainForm.cs`
- Test: `tests/DataConfigEditor.Tests/Settings/SettingsViewModelTests.cs`

### Task 4.1: 设计设置 ViewModel

- [ ] **Step 1: 写 `SettingsViewModelTests`**

测试要求：

- 修改临时设置后，`Apply` 返回新设置但不关闭。
- `Cancel` 返回打开设置前的快照。
- `Reset` 返回默认设置。
- `Ok` 保存当前设置。

- [ ] **Step 2: 实现 `SettingsViewModel`**

需要管理：

- 原始设置快照。
- 当前草稿设置。
- `Apply()`。
- `Confirm()`。
- `Cancel()`。
- `ResetToDefault()`。

- [ ] **Step 3: 实现 `SettingsDialog`**

分类：

- 应用。
- 工作区。
- 表格。
- 编辑。
- 诊断。

第一版至少实现：

- 字体。
- 行高。
- 列宽。
- 表头高度。
- 内容边距。
- 是否显示中文注释副标题。
- 是否冻结实例名列。
- 工作区隐藏规则。
- 显示隐藏项。

- [ ] **Step 4: 修改 `MainForm` 设置入口**

移除或弱化当前逐项下拉设置。点击“设置”打开 `SettingsDialog`。

- [ ] **Step 5: 手工验收设置预览**

Manual:

- 打开表格。
- 打开设置。
- 修改行高，点“应用”。
- 表格立即变化。
- 再点“取消”。
- 表格恢复到打开设置前状态。

**开发者验收:**

- 设置不再需要一个个菜单点。
- 应用/确定/取消语义正确。
- 设置项有默认值和说明。
- 工作区隐藏规则能在设置面板中编辑。

---

## Phase 5: 表格浏览能力

**目标:** 补齐成熟表格查看能力：筛选、排序、搜索、列管理。

**Files:**

- Create: `Src/UI/TableViewState.cs`
- Create: `Src/UI/TableFilter.cs`
- Create: `Src/UI/TableSorter.cs`
- Modify: `Src/UI/SheetBuilder.cs`
- Modify: `Src/MainForm.cs`
- Test: `tests/DataConfigEditor.Tests/UI/TableViewStateTests.cs`

### Task 5.1: 表格筛选

- [ ] **Step 1: 写筛选测试**

覆盖：

- 文本 contains。
- 空/非空。
- enum 多选。
- 多列筛选叠加。

- [ ] **Step 2: 实现 `TableFilter`**

先支持：

- Contains。
- Equals。
- IsEmpty。
- IsNotEmpty。
- InSet。

- [ ] **Step 3: UI 接入**

表头菜单提供：

- 按当前列筛选。
- 清除当前列筛选。
- 清除全部筛选。

- [ ] **Step 4: 状态栏显示**

显示：

```text
显示 8 / 共 11 行 | 筛选 2 项
```

**开发者验收:**

- 多列筛选叠加正确。
- 清除筛选恢复全部行。
- 筛选后批量编辑只作用于可见选中单元格。

### Task 5.2: 排序和搜索

- [ ] **Step 1: 写排序测试**

覆盖：

- 文本排序。
- 数值排序。
- 恢复源码顺序。

- [ ] **Step 2: 实现 `TableSorter`**

排序只影响视图，不改变 `TableDocument.Rows` 源顺序。

- [ ] **Step 3: 实现搜索条**

搜索范围：

- 实例名。
- 属性名。
- 中文注释。
- 单元格值。

**开发者验收:**

- 点击表头可升序/降序/恢复。
- 搜索能定位值和表头。
- 排序不改变保存后的源码实例顺序。

---

## Phase 6: 类型系统增强

**目标:** 表格知道每一列和单元格的 C# 类型，支持 enum、Flags、继承属性和诊断。

**Files:**

- Modify: `Src/Documents/TableColumn.cs`
- Modify: `Src/Documents/TableCell.cs`
- Modify: `Src/Parsing/CsTableParser.cs`
- Modify: `Src/Core/EnumCommentCache.cs`
- Test: `tests/DataConfigEditor.Tests/Parsing/CsTableParserTests.cs`

### Task 6.1: 扩展文档模型

- [ ] **Step 1: 写解析测试**

覆盖：

- 属性类型名进入 `TableColumn`。
- `<summary>` 进入 `TableColumn.Summary`。
- enum 列标记为 enum。
- 找不到 enum 时生成降级诊断。

- [ ] **Step 2: 扩展 `TableColumn`**

新增：

```csharp
public string TypeName { get; init; } = "";
public string Group { get; init; } = "";
public bool IsEnum { get; init; }
public bool IsFlags { get; init; }
public bool IsEditable { get; init; }
```

- [ ] **Step 3: 扩展 `TableCell`**

新增：

```csharp
public string RawExpression { get; init; } = "";
public bool IsEditable { get; init; }
public IReadOnlyList<TableDiagnostic> Diagnostics { get; init; } = Array.Empty<TableDiagnostic>();
```

**开发者验收:**

- 表头 tooltip 能显示类型、注释、是否可编辑。
- enum 列能知道候选项来源。
- 找不到类型时有诊断，不静默降级。

---

## Phase 7: 编辑、批量编辑、撤销重做

**目标:** 支持类型化单元格编辑、多选批量编辑、复制粘贴和撤销重做。

**Files:**

- Create: `Src/Editing/EditOperation.cs`
- Create: `Src/Editing/EditHistory.cs`
- Create: `Src/Editing/CellValueFormatter.cs`
- Modify: `Src/UI/SheetBuilder.cs`
- Modify: `Src/MainForm.cs`
- Test: `tests/DataConfigEditor.Tests/Editing/EditHistoryTests.cs`
- Test: `tests/DataConfigEditor.Tests/Editing/CellValueFormatterTests.cs`

### Task 7.1: 编辑历史

- [ ] **Step 1: 写 `EditHistoryTests`**

覆盖：

- 单格编辑可撤销。
- 批量编辑作为一个撤销步骤。
- 重做恢复修改。

- [ ] **Step 2: 实现 `EditOperation` 和 `EditHistory`**

操作包含：

- 目标单元格。
- 修改前值。
- 修改后值。
- 操作描述。

### Task 7.2: 类型化编辑器

- [ ] **Step 1: 写 `CellValueFormatterTests`**

覆盖：

- string 转 C# 字符串字面量。
- null。
- bool。
- int/float。
- enum 成员。
- Flags enum 组合。

- [ ] **Step 2: UI 接入编辑器**

映射：

- string -> 文本框。
- number -> 数值文本框加校验。
- bool -> checkbox。
- enum -> dropdown。
- Flags -> checked list popup。

**开发者验收:**

- 文本、数字、bool、enum 能编辑并校验。
- 多选同类型单元格可批量赋值。
- 批量编辑能一次撤销。
- 错误输入不会污染文档模型。

---

## Phase 8: 保存回写

**目标:** 修改后的单元格安全回写到 `.cs` 对象初始化器。

**Files:**

- Create: `Src/Parsing/CsTableWriter.cs`
- Modify: `Src/Parsing/CsFileWriter.cs`
- Modify: `Src/Documents/TableCell.cs`
- Modify: `Src/MainForm.cs`
- Test: `tests/DataConfigEditor.Tests/Parsing/CsTableWriterTests.cs`

### Task 8.1: 回写定位

- [ ] **Step 1: 写 `CsTableWriterTests`**

覆盖：

- 修改已有属性赋值。
- 插入缺失属性赋值。
- 保留注释。
- 保留无关代码。
- 找不到实例时返回保存诊断。

- [ ] **Step 2: 实现 `CsTableWriter`**

输入：

- `TableDocument`
- dirty cells

输出：

- 更新后的源码文本或保存诊断。

- [ ] **Step 3: UI 接入保存**

入口：

- 工具栏保存。
- `Ctrl+S`。
- 切换文件前提示保存。

**开发者验收:**

- 保存只改目标初始化器赋值。
- 注释、分组、无关代码不被破坏。
- 无法定位的修改不能静默保存。
- 保存后修改状态清除。

---

## Phase 9: 诊断面板和长期增强

**目标:** 完善诊断展示、图片/路径预览、行列切换、多页签和 Roslyn 迁移准备。

**Files:**

- Create: `Src/Diagnostics/TableDiagnostic.cs`
- Create: `Src/UI/DiagnosticsPanel.cs`
- Modify: `Src/Documents/TableDocument.cs`
- Modify: `Src/UI/SheetBuilder.cs`

### Task 9.1: 诊断面板

- [ ] **Step 1: 统一诊断模型**

诊断分级：

- Info。
- Warning。
- Error。

范围：

- File。
- Column。
- Cell。
- Save。

- [ ] **Step 2: 实现诊断面板**

要求：

- 状态栏显示诊断数量。
- 点击诊断定位到文件/列/单元格。
- 不可表格化文件显示完整诊断原因。

**开发者验收:**

- 用户能知道为什么某个文件不能表格化。
- 用户能知道为什么某个单元格不能编辑或保存。
- 诊断可点击定位。

---

## 总体验收命令

每阶段至少运行：

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj
dotnet build DataConfigEditor.sln
```

Windows 手工验收至少覆盖：

```bash
dotnet run --project DataConfigEditor.csproj -- "E:\Godot\Games\MyGames\复刻土豆兄弟\brotato-my\Data\DataNew"
```

## 总体验收标准

- 工具能打开 `Data/DataNew`。
- 左侧树默认隐藏无关目录和 `.uid`。
- 点击配置 `.cs` 后右侧表格布局稳定。
- 表头显示英文属性名和 `<summary>` 中文注释。
- 设置面板可预览并应用设置。
- 筛选、排序、搜索可用。
- 类型化编辑和批量编辑可用。
- 保存回写不破坏源码结构。
- 每个失败场景有明确诊断。

## 执行方式

推荐后续执行方式：

1. AI 直接按 Phase 推进。
2. 每个 Phase 完成后运行自动测试和手工验收。
3. AI 输出阶段验收摘要。
4. 用户确认后进入下一 Phase。

不需要在每个小控件上询问用户；只有阶段验收失败、需求冲突、或会影响产品方向时再停下来确认。
