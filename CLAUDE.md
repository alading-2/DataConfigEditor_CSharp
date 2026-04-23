# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 工作区概览

多项目 C# 工作区，包含三个独立项目：

- **DataConfigEditor/** — 主项目。独立 WinForms 电子表格编辑器，用于编辑纯 C# POCO 配置数据（从 Godot 游戏项目剥离）。使用 ReoGrid 实现类 Excel 编辑体验。
- **WPF_Demo/EmployeeCrudApp/** — WPF + MVVM 员工 CRUD 示例，使用 Dapper + MySQL。
- **test/** — .NET 8 控制台实验项目。

没有顶层解决方案文件，每个项目有各自的 `.sln`。

## 构建与运行命令

### DataConfigEditor（主项目）
```bash
cd DataConfigEditor
dotnet restore
dotnet build
dotnet run
```
目标框架：.NET 10.0 WinForms（`net10.0-windows`），需要 Windows。

### WPF_Demo
```bash
cd WPF_Demo/EmployeeCrudApp
dotnet build
dotnet run
```
目标框架：.NET 10.0 WPF，需要 Windows + MySQL。

### test
```bash
cd test
dotnet build
dotnet run
```
目标框架：.NET 8.0 控制台。

## DataConfigEditor 架构

本工具从 Godot 游戏项目（复刻土豆兄弟）中剥离，用于编辑以纯 C# POCO 静态实例形式存储的游戏配置数据。不依赖 Godot，直接解析 `.cs` 源码。

### 数据流
1. 用户打开数据目录（如 `Data/DataNew`），包含 `.cs` 配置文件
2. `SourceParser` 解析 `.cs` 源码 → 提取类定义、属性、静态实例、枚举
3. `CsCommentParser` 提取 `<summary>` 注释和 `// ====== 分组名 ======` 分组标记
4. `ConfigTypeScanner` 编排扫描流程，自动检测项目根目录（向上查找 `.csproj` 或 `project.godot`），在 `Data/DataKey/`、`Src/` 及全局范围解析枚举
5. `SheetBuilder` 填充 ReoGrid 工作表，列=属性，行=实例
6. 用户编辑 → 内置 Ctrl+Z/Y 撤销重做
7. `CsFileWriter` 通过大括号匹配定位静态初始化器，将修改写回 `.cs` 源文件

### 源码结构（`DataConfigEditor/Src/`）
```
Program.cs              — 入口，启动 MainForm
MainForm.cs             — 主窗体：工具栏、SplitContainer（类型列表 + ReoGrid）、状态栏
Core/
  ConfigTypeScanner.cs  — 扫描数据目录，发现类型/枚举，缓存结果
  PropertyMetadata.cs   — 属性描述（名称、类型、分组、注释、isEnum/isFlags 等）
  InstanceInfo.cs       — 静态实例描述（字段名 + 属性值字典）
  EnumCommentCache.cs   — 枚举成员缓存，包含从 <summary> 提取的中文注释
Parsing/
  SourceParser.cs       — 基于正则的 .cs 解析器：类、属性、静态初始化器、枚举
  CsCommentParser.cs    — 提取 <summary> 注释和 // ===== 分组标记
  CsFileWriter.cs       — 将修改后的值写回 .cs 静态初始化器块
UI/
  SheetBuilder.cs       — 构建/填充 ReoGrid 工作表，枚举显示为下拉列表
```

### 关键设计决策
- **无反射/DLL 加载** — 所有类型信息来自基于正则的源码解析
- **POCO 配置类** — 游戏配置为纯 C# 类 + `public static readonly` 实例，非 Godot Resource
- **往返编辑** — 从 `.cs` 源码读取 → 网格编辑 → 通过大括号匹配替换写回 `.cs` 源码
- **枚举发现** — 多阶段扫描：`Data/DataKey/` → 数据目录自身 → `Src/` → 全局兜底

### 配置文件格式（游戏侧）
配置类遵循此模式：
```csharp
public class AbilityConfigData
{
    // ====== 基础信息 ======
    /// <summary>技能名称</summary>
    public string? Name { get; set; }

    /// <summary>触发模式</summary>
    public AbilityTriggerMode TriggerMode { get; set; }

    // ====== 实例 ======
    /// <summary>冲刺</summary>
    public static readonly AbilityConfigData Dash = new()
    {
        Name = "冲刺",
        TriggerMode = AbilityTriggerMode.Manual,
    };
}
```
注释使用 `/// <summary>` XML 文档存储中文说明。分组使用 `// ====== 分组名 ======` 标记。
