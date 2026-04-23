# Workspace Table Browser Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first usable version of the desktop tool as a workspace-based C# table browser: open a directory, show folders and `.cs` files on the left, and show only a table view or a parse-status view on the right.

**Architecture:** Keep `WinForms + ReoGrid`, but stop driving the UI directly from `ConfigTypeScanner`. Introduce a workspace layer for directory browsing, a document layer for a UI-independent `TableDocument` model, and a single-file parser that converts one `.cs` file into a table or a diagnostic state. Refactor `MainForm` to orchestrate these services instead of holding all behavior inline.

**Tech Stack:** .NET 10 WinForms, ReoGrid, xUnit for non-UI tests, existing `SourceParser` and `CsCommentParser`, JSON persistence for recent directories.

---

## File Map

### Existing files to modify

- `DataConfigEditor.csproj`
- `DataConfigEditor.sln`
- `Src/Program.cs`
- `Src/MainForm.cs`
- `Src/UI/SheetBuilder.cs`

### New production files

- `Src/Documents/TableDocument.cs`
- `Src/Documents/TableColumn.cs`
- `Src/Documents/TableRow.cs`
- `Src/Documents/TableCell.cs`
- `Src/Documents/ParseDiagnostic.cs`
- `Src/Workspace/AppLaunchOptions.cs`
- `Src/Workspace/WorkspaceEntry.cs`
- `Src/Workspace/WorkspaceService.cs`
- `Src/Workspace/RecentDirectoryStore.cs`
- `Src/Parsing/CsTableParser.cs`
- `Src/Presentation/WorkspacePresenter.cs`
- `Src/Presentation/WorkspaceViewState.cs`

### New test files

- `tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj`
- `tests/DataConfigEditor.Tests/Workspace/AppLaunchOptionsTests.cs`
- `tests/DataConfigEditor.Tests/Workspace/RecentDirectoryStoreTests.cs`
- `tests/DataConfigEditor.Tests/Workspace/WorkspaceServiceTests.cs`
- `tests/DataConfigEditor.Tests/Parsing/CsTableParserTests.cs`
- `tests/DataConfigEditor.Tests/Presentation/WorkspacePresenterTests.cs`
- `tests/DataConfigEditor.Tests/Fixtures/SampleConfig.cs`

### Tooling/docs files

- `.vscode/tasks.json`
- `Docs/2026-04-23-工作区表格浏览器执行方案.md`

### Responsibility boundaries

- `Workspace*` only knows directories, files, and recent-history state.
- `CsTableParser` only knows how to convert a single `.cs` file into a `TableDocument`.
- `WorkspacePresenter` only knows view-state transitions.
- `SheetBuilder` only renders `TableDocument` into ReoGrid.
- `MainForm` wires UI events to services and renders presenter output.

## Task 1: Repair project boundaries and add test scaffolding

**Files:**
- Modify: `DataConfigEditor.csproj`
- Modify: `DataConfigEditor.sln`
- Create: `tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj`

- [ ] **Step 1: Capture the current failing build**

Run:

```bash
dotnet build
```

Expected: FAIL with missing `Godot`, `ExportAttribute`, `DataKey`, and related symbols from `Src/Data/**`.

- [ ] **Step 2: Stop compiling the embedded sample/game source**

Update `DataConfigEditor.csproj` so the tool excludes `Src/Data/**` from `Compile` items while still compiling the real tool code:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationIcon />
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <AssemblyName>DataConfigEditor</AssemblyName>
    <RootNamespace>DataConfigEditor</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <Compile Remove="Src/Data/**/*.cs" />
    <None Include="Src/Data/**/*.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="unvell.ReoGrid.dll" Version="3.0.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Add a test project for non-UI behavior**

Create `tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\DataConfigEditor.csproj" />
  </ItemGroup>
</Project>
```

Add it to the solution:

```bash
dotnet sln DataConfigEditor.sln add tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj
```

- [ ] **Step 4: Rebuild to verify the tool now compiles**

Run:

```bash
dotnet build DataConfigEditor.sln
```

Expected: PASS for the app project. The test project may have `0` tests at this point, but the solution build should succeed.

- [ ] **Step 5: Commit**

```bash
git add DataConfigEditor.csproj DataConfigEditor.sln tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj
git commit -m "build: isolate tool project from embedded game sources"
```

## Task 2: Add the table document model

**Files:**
- Create: `Src/Documents/TableDocument.cs`
- Create: `Src/Documents/TableColumn.cs`
- Create: `Src/Documents/TableRow.cs`
- Create: `Src/Documents/TableCell.cs`
- Create: `Src/Documents/ParseDiagnostic.cs`
- Create: `tests/DataConfigEditor.Tests/Fixtures/SampleConfig.cs`
- Create: `tests/DataConfigEditor.Tests/Parsing/CsTableParserTests.cs`

- [ ] **Step 1: Write the failing parser-facing tests for the document shape**

Create `tests/DataConfigEditor.Tests/Fixtures/SampleConfig.cs`:

```csharp
namespace TestData;

public class SampleConfig
{
    // ====== 基础 ======
    /// <summary>显示名称</summary>
    public string? Name { get; set; }

    /// <summary>冷却时间</summary>
    public float Cooldown { get; set; }

    /// <summary>冲刺</summary>
    public static readonly SampleConfig Dash = new()
    {
        Name = "冲刺",
        Cooldown = 1.5f,
    };
}
```
Create `tests/DataConfigEditor.Tests/Parsing/CsTableParserTests.cs`:

```csharp
using DataConfigEditor.Documents;
using DataConfigEditor.Parsing;

namespace DataConfigEditor.Tests.Parsing;

public class CsTableParserTests
{
    [Fact]
    public void ParseConfigFile_ReturnsTableDocument()
    {
        var parser = new CsTableParser();
        var filePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SampleConfig.cs");

        var document = parser.ParseFile(filePath);

        Assert.True(document.IsTable);
        Assert.Equal("SampleConfig", document.Title);
        Assert.Collection(document.Columns,
            column => Assert.Equal("实例名", column.Header),
            column => Assert.Equal("Name", column.Key),
            column => Assert.Equal("Cooldown", column.Key));
        Assert.Single(document.Rows);
        Assert.Equal("Dash", document.Rows[0].Header);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter ParseConfigFile_ReturnsTableDocument
```

Expected: FAIL with `CsTableParser` and `TableDocument` not found.

- [ ] **Step 3: Add the document model types**

Create `Src/Documents/TableColumn.cs`:

```csharp
namespace DataConfigEditor.Documents;

public sealed class TableColumn
{
    public required string Key { get; init; }
    public required string Header { get; init; }
    public string Summary { get; init; } = "";
}
```

Create `Src/Documents/TableCell.cs`:

```csharp
namespace DataConfigEditor.Documents;

public sealed class TableCell
{
    public required string ColumnKey { get; init; }
    public string Value { get; init; } = "";
}
```

Create `Src/Documents/TableRow.cs`:

```csharp
namespace DataConfigEditor.Documents;

public sealed class TableRow
{
    public required string Header { get; init; }
    public IReadOnlyList<TableCell> Cells { get; init; } = Array.Empty<TableCell>();
}
```

Create `Src/Documents/ParseDiagnostic.cs`:

```csharp
namespace DataConfigEditor.Documents;

public sealed class ParseDiagnostic
{
    public required string Message { get; init; }
    public string Details { get; init; } = "";
}
```

Create `Src/Documents/TableDocument.cs`:

```csharp
namespace DataConfigEditor.Documents;

public sealed class TableDocument
{
    public required string SourceFilePath { get; init; }
    public required string Title { get; init; }
    public IReadOnlyList<TableColumn> Columns { get; init; } = Array.Empty<TableColumn>();
    public IReadOnlyList<TableRow> Rows { get; init; } = Array.Empty<TableRow>();
    public ParseDiagnostic? Diagnostic { get; init; }

    public bool IsTable => Diagnostic is null;

    public static TableDocument Error(string filePath, string title, string message, string details = "")
    {
        return new TableDocument
        {
            SourceFilePath = filePath,
            Title = title,
            Diagnostic = new ParseDiagnostic { Message = message, Details = details },
        };
    }
}
```

- [ ] **Step 4: Re-run the test to verify the model exists and the parser is still missing**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter ParseConfigFile_ReturnsTableDocument
```

Expected: FAIL with `CsTableParser` not found or missing `ParseFile`.

- [ ] **Step 5: Commit**

```bash
git add Src/Documents tests/DataConfigEditor.Tests/Fixtures tests/DataConfigEditor.Tests/Parsing
git commit -m "test: add table document model and parser contract"
```

## Task 3: Add startup options and recent-directory persistence

**Files:**
- Create: `Src/Workspace/AppLaunchOptions.cs`
- Create: `Src/Workspace/RecentDirectoryStore.cs`
- Create: `tests/DataConfigEditor.Tests/Workspace/AppLaunchOptionsTests.cs`
- Create: `tests/DataConfigEditor.Tests/Workspace/RecentDirectoryStoreTests.cs`

- [ ] **Step 1: Write failing tests for launch arguments and recent-directory round-tripping**

Create `tests/DataConfigEditor.Tests/Workspace/AppLaunchOptionsTests.cs`:

```csharp
using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class AppLaunchOptionsTests
{
    [Fact]
    public void Parse_UsesFirstExistingDirectoryArgument()
    {
        var tempDir = Directory.CreateTempSubdirectory();

        var options = AppLaunchOptions.Parse(new[] { tempDir.FullName, "ignored" });

        Assert.Equal(tempDir.FullName, options.InitialDirectory);
    }
}
```

Create `tests/DataConfigEditor.Tests/Workspace/RecentDirectoryStoreTests.cs`:

```csharp
using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class RecentDirectoryStoreTests
{
    [Fact]
    public void SaveAndLoad_PreservesMostRecentDirectories()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new RecentDirectoryStore(filePath);

        store.Save(new[] { @"E:\A", @"E:\B" });

        var loaded = store.Load();

        Assert.Equal(new[] { @"E:\A", @"E:\B" }, loaded);
    }
}
```

- [ ] **Step 2: Run the workspace tests to verify they fail**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter FullyQualifiedName~DataConfigEditor.Tests.Workspace
```

Expected: FAIL with missing `AppLaunchOptions` and `RecentDirectoryStore`.

- [ ] **Step 3: Implement the launch-options parser and recent-directory store**

Create `Src/Workspace/AppLaunchOptions.cs`:

```csharp
namespace DataConfigEditor.Workspace;

public sealed class AppLaunchOptions
{
    public string? InitialDirectory { get; init; }

    public static AppLaunchOptions Parse(string[] args)
    {
        var initialDirectory = args.FirstOrDefault(Directory.Exists);
        return new AppLaunchOptions { InitialDirectory = initialDirectory };
    }
}
```

Create `Src/Workspace/RecentDirectoryStore.cs`:

```csharp
using System.Text.Json;

namespace DataConfigEditor.Workspace;

public sealed class RecentDirectoryStore
{
    private readonly string _filePath;

    public RecentDirectoryStore(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(_filePath))
            return Array.Empty<string>();

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    public void Save(IEnumerable<string> directories)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(directories.ToList(), new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(_filePath, json);
    }
}
```

- [ ] **Step 4: Re-run the workspace tests**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter FullyQualifiedName~DataConfigEditor.Tests.Workspace
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Src/Workspace/AppLaunchOptions.cs Src/Workspace/RecentDirectoryStore.cs tests/DataConfigEditor.Tests/Workspace
git commit -m "feat: add startup options and recent directory persistence"
```

## Task 4: Add workspace scanning and presenter state

**Files:**
- Create: `Src/Workspace/WorkspaceEntry.cs`
- Create: `Src/Workspace/WorkspaceService.cs`
- Create: `Src/Presentation/WorkspaceViewState.cs`
- Create: `Src/Presentation/WorkspacePresenter.cs`
- Create: `tests/DataConfigEditor.Tests/Workspace/WorkspaceServiceTests.cs`
- Create: `tests/DataConfigEditor.Tests/Presentation/WorkspacePresenterTests.cs`

- [ ] **Step 1: Write failing tests for directory scanning and empty-state behavior**

Create `tests/DataConfigEditor.Tests/Workspace/WorkspaceServiceTests.cs`:

```csharp
using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class WorkspaceServiceTests
{
    [Fact]
    public void ScanDirectory_ReturnsFoldersAndCsFilesOnly()
    {
        var root = Directory.CreateTempSubdirectory();
        Directory.CreateDirectory(Path.Combine(root.FullName, "Child"));
        File.WriteAllText(Path.Combine(root.FullName, "Child", "Config.cs"), "public class Config {}");
        File.WriteAllText(Path.Combine(root.FullName, "Child", "notes.txt"), "ignore");

        var service = new WorkspaceService();

        var entry = service.BuildTree(root.FullName);

        Assert.Equal(root.FullName, entry.FullPath);
        Assert.Single(entry.Children);
        Assert.Single(entry.Children[0].Children);
        Assert.Equal("Config.cs", entry.Children[0].Children[0].Name);
    }
}
```

Create `tests/DataConfigEditor.Tests/Presentation/WorkspacePresenterTests.cs`:

```csharp
using DataConfigEditor.Documents;
using DataConfigEditor.Presentation;

namespace DataConfigEditor.Tests.Presentation;

public class WorkspacePresenterTests
{
    [Fact]
    public void ShowWelcome_UsesRecentDirectories()
    {
        var presenter = new WorkspacePresenter();

        var state = presenter.ShowWelcome(new[] { @"E:\One", @"E:\Two" });

        Assert.Equal(WorkspaceViewKind.Welcome, state.Kind);
        Assert.Equal(2, state.RecentDirectories.Count);
    }

    [Fact]
    public void ShowDocumentError_UsesDiagnosticView()
    {
        var presenter = new WorkspacePresenter();
        var document = TableDocument.Error("A.cs", "A", "无法转换为表格视图");

        var state = presenter.ShowDocument(document);

        Assert.Equal(WorkspaceViewKind.Message, state.Kind);
        Assert.Equal("无法转换为表格视图", state.Message);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter FullyQualifiedName~WorkspaceServiceTests|FullyQualifiedName~WorkspacePresenterTests
```

Expected: FAIL with missing workspace and presenter types.

- [ ] **Step 3: Implement workspace entries and presenter state**

Create `Src/Workspace/WorkspaceEntry.cs`:

```csharp
namespace DataConfigEditor.Workspace;

public sealed class WorkspaceEntry
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }
    public IReadOnlyList<WorkspaceEntry> Children { get; init; } = Array.Empty<WorkspaceEntry>();
}
```

Create `Src/Workspace/WorkspaceService.cs`:

```csharp
namespace DataConfigEditor.Workspace;

public sealed class WorkspaceService
{
    public WorkspaceEntry BuildTree(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException(rootPath);

        return BuildEntry(rootPath);
    }

    private static WorkspaceEntry BuildEntry(string path)
    {
        if (Directory.Exists(path))
        {
            var children = Directory.GetDirectories(path)
                .Select(BuildEntry)
                .Concat(Directory.GetFiles(path, "*.cs").Select(BuildEntry))
                .OrderBy(entry => entry.IsDirectory ? 0 : 1)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new WorkspaceEntry
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = true,
                Children = children,
            };
        }

        return new WorkspaceEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            IsDirectory = false,
        };
    }
}
```

Create `Src/Presentation/WorkspaceViewState.cs`:

```csharp
using DataConfigEditor.Documents;

namespace DataConfigEditor.Presentation;

public enum WorkspaceViewKind
{
    Welcome,
    Table,
    Message,
}

public sealed class WorkspaceViewState
{
    public required WorkspaceViewKind Kind { get; init; }
    public TableDocument? Document { get; init; }
    public string Message { get; init; } = "";
    public IReadOnlyList<string> RecentDirectories { get; init; } = Array.Empty<string>();
}
```

Create `Src/Presentation/WorkspacePresenter.cs`:

```csharp
using DataConfigEditor.Documents;

namespace DataConfigEditor.Presentation;

public sealed class WorkspacePresenter
{
    public WorkspaceViewState ShowWelcome(IReadOnlyList<string> recentDirectories)
    {
        return new WorkspaceViewState
        {
            Kind = WorkspaceViewKind.Welcome,
            RecentDirectories = recentDirectories,
        };
    }

    public WorkspaceViewState ShowDocument(TableDocument document)
    {
        if (document.Diagnostic is not null)
        {
            return new WorkspaceViewState
            {
                Kind = WorkspaceViewKind.Message,
                Message = document.Diagnostic.Message,
                Document = document,
            };
        }

        return new WorkspaceViewState
        {
            Kind = WorkspaceViewKind.Table,
            Document = document,
        };
    }
}
```

- [ ] **Step 4: Re-run the workspace and presenter tests**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter FullyQualifiedName~WorkspaceServiceTests|FullyQualifiedName~WorkspacePresenterTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Src/Workspace/WorkspaceEntry.cs Src/Workspace/WorkspaceService.cs Src/Presentation tests/DataConfigEditor.Tests/Workspace tests/DataConfigEditor.Tests/Presentation
git commit -m "feat: add workspace scanning and view-state presenter"
```

## Task 5: Implement the single-file table parser

**Files:**
- Create: `Src/Parsing/CsTableParser.cs`
- Modify: `tests/DataConfigEditor.Tests/Parsing/CsTableParserTests.cs`

- [ ] **Step 1: Extend the parser tests to cover the non-table case**

Append this test to `tests/DataConfigEditor.Tests/Parsing/CsTableParserTests.cs`:

```csharp
[Fact]
public void ParseFile_WithoutInstances_ReturnsDiagnosticDocument()
{
    var parser = new CsTableParser();
    var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cs");
    File.WriteAllText(filePath, """
namespace TestData;

public class EmptyConfig
{
    public string? Name { get; set; }
}
""");

    var document = parser.ParseFile(filePath);

    Assert.False(document.IsTable);
    Assert.Equal("当前文件无法转换为表格视图", document.Diagnostic?.Message);
}
```

- [ ] **Step 2: Run the parser tests to verify they fail**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter FullyQualifiedName~CsTableParserTests
```

Expected: FAIL with missing `CsTableParser`.

- [ ] **Step 3: Implement the parser using the existing source/comment parsers**

Create `Src/Parsing/CsTableParser.cs`:

```csharp
using DataConfigEditor.Documents;

namespace DataConfigEditor.Parsing;

public sealed class CsTableParser
{
    public TableDocument ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
            return TableDocument.Error(filePath, Path.GetFileName(filePath), "文件不存在");

        var classInfo = SourceParser.ParseClass(filePath);
        if (classInfo is null)
            return TableDocument.Error(filePath, Path.GetFileName(filePath), "当前文件无法转换为表格视图", "未找到可解析的类定义。");

        var comments = CsCommentParser.ParseFile(filePath);
        if (classInfo.Properties.Count == 0 || classInfo.Instances.Count == 0)
            return TableDocument.Error(filePath, classInfo.ClassName, "当前文件无法转换为表格视图", "缺少 public 属性或静态实例。");

        var columns = new List<TableColumn>
        {
            new() { Key = "__instance", Header = "实例名" }
        };

        columns.AddRange(classInfo.Properties.Select(property =>
        {
            comments.TryGetValue(property.Name, out var comment);
            return new TableColumn
            {
                Key = property.Name,
                Header = property.Name,
                Summary = comment?.Summary ?? "",
            };
        }));

        var rows = classInfo.Instances.Select(instance =>
        {
            var cells = classInfo.Properties.Select(property => new TableCell
            {
                ColumnKey = property.Name,
                Value = instance.Values.TryGetValue(property.Name, out var value) ? value : property.DefaultValue,
            }).ToList();

            return new TableRow
            {
                Header = instance.FieldName,
                Cells = cells,
            };
        }).ToList();

        return new TableDocument
        {
            SourceFilePath = filePath,
            Title = classInfo.ClassName,
            Columns = columns,
            Rows = rows,
        };
    }
}
```

- [ ] **Step 4: Re-run the parser tests**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter FullyQualifiedName~CsTableParserTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Src/Parsing/CsTableParser.cs tests/DataConfigEditor.Tests/Parsing/CsTableParserTests.cs
git commit -m "feat: parse single csharp files into table documents"
```

## Task 6: Refactor the grid renderer to consume `TableDocument`

**Files:**
- Modify: `Src/UI/SheetBuilder.cs`

- [ ] **Step 1: Write the minimal rendering contract before touching the form**

Update `SheetBuilder` to expose a document-oriented API:

```csharp
public void BuildSheet(Worksheet sheet, TableDocument document)
{
    sheet.Reset();

    if (!document.IsTable || document.Columns.Count == 0 || document.Rows.Count == 0)
    {
        sheet[0, 0] = document.Diagnostic?.Message ?? "无数据";
        return;
    }

    sheet.Columns = document.Columns.Count;
    sheet.Rows = document.Rows.Count + 1;

    for (int columnIndex = 0; columnIndex < document.Columns.Count; columnIndex++)
    {
        var column = document.Columns[columnIndex];
        sheet[0, columnIndex] = string.IsNullOrEmpty(column.Summary)
            ? column.Header
            : $"{column.Header}\n{column.Summary}";
        sheet.SetRangeStyles(0, columnIndex, 1, 1, _headerStyle);
    }

    for (int rowIndex = 0; rowIndex < document.Rows.Count; rowIndex++)
    {
        var row = document.Rows[rowIndex];
        sheet[rowIndex + 1, 0] = row.Header;
        sheet.SetRangeStyles(rowIndex + 1, 0, 1, 1, _instanceNameStyle);

        for (int cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
        {
            sheet[rowIndex + 1, cellIndex + 1] = row.Cells[cellIndex].Value;
            sheet.SetRangeStyles(rowIndex + 1, cellIndex + 1, 1, 1, _dataStyle);
        }
    }

    sheet.FreezeToCell(1, 1);
}
```

- [ ] **Step 2: Build after the API change to confirm `MainForm` breaks**

Run:

```bash
dotnet build DataConfigEditor.sln
```

Expected: FAIL because `MainForm` still calls the old `BuildSheet(...)` overload.

- [ ] **Step 3: Remove the old configuration-specific rendering path**

Delete or stop using these document-hostile entry points after the new overload is in place:

```csharp
public void BuildSheet(
    Worksheet sheet,
    ConfigTypeInfo typeInfo,
    List<PropertyMetadata> properties,
    List<InstanceInfo> instances,
    string searchFilter = "")
```

Keep group/enum-specific helpers only if they are still used by the new document pipeline. Do not leave both rendering APIs active.

- [ ] **Step 4: Rebuild to confirm only `MainForm` integration remains**

Run:

```bash
dotnet build DataConfigEditor.sln
```

Expected: FAIL only where `MainForm` still uses the removed API.

- [ ] **Step 5: Commit**

```bash
git add Src/UI/SheetBuilder.cs
git commit -m "refactor: render grid from table documents"
```

## Task 7: Refactor `MainForm` and `Program` into the workspace browser

**Files:**
- Modify: `Src/Program.cs`
- Modify: `Src/MainForm.cs`

- [ ] **Step 1: Add the launch-options flow at the application entry point**

Update `Src/Program.cs`:

```csharp
using DataConfigEditor.Workspace;

namespace DataConfigEditor;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var options = AppLaunchOptions.Parse(args);
        Application.Run(new MainForm(options));
    }
}
```

- [ ] **Step 2: Replace the type-list UI with workspace services and a tree view**

Refactor `Src/MainForm.cs` around these field changes:

```csharp
private readonly AppLaunchOptions _launchOptions;
private readonly WorkspaceService _workspaceService = new();
private readonly WorkspacePresenter _presenter = new();
private readonly CsTableParser _tableParser = new();
private readonly RecentDirectoryStore _recentStore;

private TreeView _workspaceTree = null!;
private ReoGridControl _grid = null!;
private Label _messageLabel = null!;
private ToolStripDropDownButton _recentButton = null!;
private string? _currentDirectory;
```

Construct the recent store in the constructor:

```csharp
public MainForm(AppLaunchOptions launchOptions)
{
    _launchOptions = launchOptions;
    var appData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DataConfigEditor");
    _recentStore = new RecentDirectoryStore(Path.Combine(appData, "recent-directories.json"));

    Text = "DataConfigEditor - 工作区表格浏览器";
    Size = new Size(1400, 800);
    StartPosition = FormStartPosition.CenterScreen;
}
```

- [ ] **Step 3: Implement the startup and open-directory flow**

Add these methods to `MainForm`:

```csharp
protected override void OnLoad(EventArgs e)
{
    base.OnLoad(e);
    BuildUi();

    if (!string.IsNullOrEmpty(_launchOptions.InitialDirectory))
    {
        OpenWorkspace(_launchOptions.InitialDirectory);
        return;
    }

    RenderState(_presenter.ShowWelcome(_recentStore.Load()));
}

private void OpenWorkspace(string directoryPath)
{
    _currentDirectory = directoryPath;
    var root = _workspaceService.BuildTree(directoryPath);
    BindTree(root);

    var recents = _recentStore.Load()
        .Where(path => !string.Equals(path, directoryPath, StringComparison.OrdinalIgnoreCase))
        .Prepend(directoryPath)
        .Take(10)
        .ToList();

    _recentStore.Save(recents);
    RenderState(_presenter.ShowWelcome(recents));
    UpdateStatus($"已打开目录: {directoryPath}");
}

private void OpenFile(string filePath)
{
    var document = _tableParser.ParseFile(filePath);
    RenderState(_presenter.ShowDocument(document));
    UpdateStatus($"已打开文件: {filePath}");
}
```

- [ ] **Step 4: Implement tree binding and right-panel rendering**

Add these UI helpers:

```csharp
private void BindTree(WorkspaceEntry root)
{
    _workspaceTree.BeginUpdate();
    _workspaceTree.Nodes.Clear();
    _workspaceTree.Nodes.Add(CreateNode(root));
    _workspaceTree.ExpandAll();
    _workspaceTree.EndUpdate();
}

private TreeNode CreateNode(WorkspaceEntry entry)
{
    var node = new TreeNode(entry.Name) { Tag = entry.FullPath };
    foreach (var child in entry.Children)
        node.Nodes.Add(CreateNode(child));
    return node;
}

private void RenderState(WorkspaceViewState state)
{
    _messageLabel.Visible = state.Kind != WorkspaceViewKind.Table;
    _grid.Visible = state.Kind == WorkspaceViewKind.Table;

    if (state.Kind == WorkspaceViewKind.Table && state.Document is not null)
    {
        _sheetBuilder.BuildSheet(_grid.CurrentWorksheet, state.Document);
        return;
    }

    _grid.CurrentWorksheet.Reset();
    _messageLabel.Text = state.Kind == WorkspaceViewKind.Welcome
        ? "请选择一个目录，或从最近目录中重新打开。"
        : state.Message;
}
```

Wire the tree selection event:

```csharp
_workspaceTree.AfterSelect += (_, e) =>
{
    if (e.Node?.Tag is string fullPath && File.Exists(fullPath))
        OpenFile(fullPath);
};
```

- [ ] **Step 5: Build and manually smoke-test the workspace flow**

Run:

```bash
dotnet build DataConfigEditor.sln
dotnet run --project DataConfigEditor.csproj -- "E:\Godot\Games\MyGames\复刻土豆兄弟\brotato-my\Data\DataNew"
```

Expected:

- Build PASS.
- App opens directly into the provided directory.
- Left side shows folders and `.cs` files only.
- Clicking a parseable `.cs` file shows a table.
- Clicking a non-parseable `.cs` file shows a message instead of code.

- [ ] **Step 6: Commit**

```bash
git add Src/Program.cs Src/MainForm.cs
git commit -m "feat: turn main window into a workspace table browser"
```

## Task 8: Add VS Code tasks and update the execution doc

**Files:**
- Create: `.vscode/tasks.json`
- Modify: `Docs/2026-04-23-工作区表格浏览器执行方案.md`

- [ ] **Step 1: Write the task configuration**

Create `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "type": "process",
      "command": "dotnet",
      "args": ["build", "DataConfigEditor.sln"],
      "group": "build",
      "problemMatcher": "$msCompile"
    },
    {
      "label": "run",
      "type": "process",
      "command": "dotnet",
      "args": ["run", "--project", "DataConfigEditor.csproj"],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "run:test-data",
      "type": "process",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "DataConfigEditor.csproj",
        "--",
        "E:\\Godot\\Games\\MyGames\\复刻土豆兄弟\\brotato-my\\Data\\DataNew"
      ],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

- [ ] **Step 2: Update the execution doc so it matches the implemented entry points**

Append this closing note to `Docs/2026-04-23-工作区表格浏览器执行方案.md` if it is not already present:

```md
## 10. 开发入口

开发阶段统一使用以下入口：

- `dotnet build DataConfigEditor.sln`
- `dotnet run --project DataConfigEditor.csproj`
- VS Code task: `run:test-data`

命令行参数路径优先于最近目录恢复。第一阶段不自动打开固定路径，固定测试目录只通过 task 或显式参数进入。
```

- [ ] **Step 3: Verify tasks and tests**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj
dotnet build DataConfigEditor.sln
```

Expected: PASS for tests and build.

- [ ] **Step 4: Commit**

```bash
git add .vscode/tasks.json Docs/2026-04-23-工作区表格浏览器执行方案.md
git commit -m "chore: add vscode tasks for workspace browser flow"
```

## Spec coverage check

- Startup behavior: covered by Tasks 3 and 7.
- Recent directories: covered by Tasks 3 and 7.
- Left-side folder + `.cs` tree only: covered by Tasks 4 and 7.
- Right side shows only table or message: covered by Tasks 5, 6, and 7.
- Parameter-based test-data launch: covered by Tasks 3, 7, and 8.
- First-stage scope excludes DLL and code editor: preserved by Tasks 1, 5, and 7.

## Placeholder scan

- No `TODO`, `TBD`, or “implement later” placeholders remain.
- Every task includes exact file paths, commands, and expected outcomes.
- Every code-writing step includes concrete code to start from.

## Type consistency check

- `Program` passes `AppLaunchOptions` into `MainForm`.
- `MainForm` delegates file parsing to `CsTableParser`.
- `CsTableParser` returns `TableDocument`.
- `SheetBuilder` renders `TableDocument`.
- `WorkspacePresenter` returns `WorkspaceViewState`.

