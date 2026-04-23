# Strongly Typed Config Editor Next Phase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the next reviewable increment of DataConfigEditor: VS Code-like workspace hiding, consistent table layout, and a real settings panel with Apply/OK/Cancel behavior.

**Architecture:** Keep the current WinForms + DataGridView browser and avoid broad UI rewrites. Add focused models for workspace hiding and table layout, then route settings changes through a small view model so `MainForm` only wires events and renders state. This phase does not add editing, saving, filtering, or Roslyn semantics.

**Tech Stack:** .NET 10 WinForms, xUnit, `System.Text.Json`, existing `WorkspaceService`, `UiSettingsStore`, `SheetBuilder`, and `MainForm`.

---

## Scope

This plan implements the next phase only. It intentionally stops before the larger Excel-like table feature set.

In scope:

- Hide irrelevant workspace files and folders by default.
- Allow hidden entries to be shown as gray tree nodes.
- Persist workspace hiding options.
- Make the right-side table area use one consistent layout.
- Replace the settings dropdown with a settings dialog.
- Support Apply, OK, Cancel, and Reset in settings.
- Update docs and run automated verification.

Out of scope:

- Cell editing.
- Save/writeback.
- Filter, sort, search, and batch edit.
- Row/column transposition.
- Roslyn semantic analysis.
- Replacing DataGridView with ReoGrid.

## Current Code Context

Important existing files:

- `Src/MainForm.cs` builds the toolbar, tree, right-side grid host, status bar, and current settings dropdown.
- `Src/Workspace/WorkspaceService.cs` recursively includes all directories and `.cs` files.
- `Src/Workspace/WorkspaceEntry.cs` is the tree node model.
- `Src/Settings/UiSettings.cs` contains grid padding, font size, row height, fixed column width, and column sizing mode.
- `Src/Settings/UiSettingsStore.cs` persists UI settings as JSON.
- `Src/UI/SheetBuilder.cs` renders a read-only `TableDocument` into `DataGridView`.
- `tests/DataConfigEditor.Tests/Workspace/WorkspaceServiceTests.cs` already tests basic tree scanning.
- `tests/DataConfigEditor.Tests/Settings/UiSettingsTests.cs` and `UiSettingsStoreTests.cs` already test basic setting normalization/persistence.
- `tests/DataConfigEditor.Tests/Workspace/WorkspaceLayoutTests.cs` already covers splitter layout helper behavior.

## File Map

New production files:

- `Src/Workspace/WorkspaceSettings.cs` - workspace-level hide settings and defaults.
- `Src/Workspace/WorkspaceSettingsStore.cs` - JSON persistence for workspace settings.
- `Src/Workspace/WorkspaceExcludeMatcher.cs` - pure matcher for directory/file exclude rules.
- `Src/UI/TableLayoutOptions.cs` - normalized table layout values derived from UI settings.
- `Src/UI/SettingsViewModel.cs` - Apply/OK/Cancel/Reset behavior for settings dialog.
- `Src/UI/SettingsDialog.cs` - WinForms settings window.

Modified production files:

- `Src/Workspace/WorkspaceEntry.cs` - add `IsHidden`.
- `Src/Workspace/WorkspaceService.cs` - apply hide rules while building the tree.
- `Src/Settings/UiSettings.cs` - add table layout and workspace-related settings.
- `Src/Settings/UiSettingsStore.cs` - keep backward-compatible loading with normalization.
- `Src/UI/SheetBuilder.cs` - apply consistent header height, row height, frozen instance column, and widths.
- `Src/MainForm.cs` - add show-hidden toggle, gray hidden nodes, settings dialog entry, and unified grid padding.

New tests:

- `tests/DataConfigEditor.Tests/Workspace/WorkspaceExcludeMatcherTests.cs`
- `tests/DataConfigEditor.Tests/Workspace/WorkspaceSettingsStoreTests.cs`
- `tests/DataConfigEditor.Tests/UI/TableLayoutOptionsTests.cs`
- `tests/DataConfigEditor.Tests/UI/SettingsViewModelTests.cs`

Modified tests:

- `tests/DataConfigEditor.Tests/Workspace/WorkspaceServiceTests.cs`
- `tests/DataConfigEditor.Tests/Settings/UiSettingsTests.cs`
- `tests/DataConfigEditor.Tests/Settings/UiSettingsStoreTests.cs`

Docs:

- `Docs/README.md`
- `Docs/面向CSharp强类型配置的表格编辑器方案执行文档.md`
- `Docs/面向CSharp强类型配置的表格编辑器完善计划.md`

## Task 1: Workspace Hide Settings And Matcher

**Files:**

- Create: `Src/Workspace/WorkspaceSettings.cs`
- Create: `Src/Workspace/WorkspaceExcludeMatcher.cs`
- Test: `tests/DataConfigEditor.Tests/Workspace/WorkspaceExcludeMatcherTests.cs`

- [ ] **Step 1: Write matcher tests**

Create `tests/DataConfigEditor.Tests/Workspace/WorkspaceExcludeMatcherTests.cs`:

```csharp
using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class WorkspaceExcludeMatcherTests
{
    [Theory]
    [InlineData("bin", true)]
    [InlineData("obj", true)]
    [InlineData(".godot", true)]
    [InlineData(".git", true)]
    [InlineData(".idea", true)]
    [InlineData(".vscode", true)]
    [InlineData(".history", true)]
    [InlineData(".superpowers", true)]
    [InlineData("Ability", false)]
    public void IsExcluded_DefaultDirectoryRules_ReturnsExpectedResult(string directoryName, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var fullPath = Path.Combine(root, directoryName);
        var matcher = new WorkspaceExcludeMatcher(root, WorkspaceSettings.Default);

        var result = matcher.IsExcluded(fullPath, isDirectory: true);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("asset.uid", true)]
    [InlineData("AbilityConfigData.cs", false)]
    [InlineData("notes.txt", true)]
    public void IsExcluded_DefaultFileRules_ReturnsExpectedResult(string fileName, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var fullPath = Path.Combine(root, fileName);
        var matcher = new WorkspaceExcludeMatcher(root, WorkspaceSettings.Default);

        var result = matcher.IsExcluded(fullPath, isDirectory: false);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsExcluded_CustomPathSegmentPattern_HidesNestedDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var fullPath = Path.Combine(root, "DataNew", "Generated", "Config.cs");
        var settings = WorkspaceSettings.Default with
        {
            ExcludePatterns = WorkspaceSettings.DefaultExcludePatterns
                .Concat(new[] { "**/Generated/**" })
                .ToArray(),
        };
        var matcher = new WorkspaceExcludeMatcher(root, settings);

        var result = matcher.IsExcluded(fullPath, isDirectory: false);

        Assert.True(result);
    }
}
```

- [ ] **Step 2: Run matcher tests and verify failure**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter WorkspaceExcludeMatcherTests
```

Expected: FAIL because `WorkspaceSettings` and `WorkspaceExcludeMatcher` do not exist.

- [ ] **Step 3: Add workspace settings model**

Create `Src/Workspace/WorkspaceSettings.cs`:

```csharp
namespace DataConfigEditor.Workspace;

public sealed record WorkspaceSettings
{
    public static readonly string[] DefaultExcludePatterns =
    [
        "**/bin/**",
        "**/obj/**",
        "**/.godot/**",
        "**/.git/**",
        "**/.idea/**",
        "**/.vscode/**",
        "**/.history/**",
        "**/.superpowers/**",
        "*.uid",
        "!*.cs",
    ];

    public static WorkspaceSettings Default => new();

    public bool ShowHiddenEntries { get; init; }

    public IReadOnlyList<string> ExcludePatterns { get; init; } = DefaultExcludePatterns;
}
```

- [ ] **Step 4: Add matcher implementation**

Create `Src/Workspace/WorkspaceExcludeMatcher.cs`:

```csharp
namespace DataConfigEditor.Workspace;

public sealed class WorkspaceExcludeMatcher
{
    private readonly string _rootPath;
    private readonly WorkspaceSettings _settings;

    public WorkspaceExcludeMatcher(string rootPath, WorkspaceSettings settings)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _settings = settings;
    }

    public bool IsExcluded(string fullPath, bool isDirectory)
    {
        var relativePath = Path.GetRelativePath(_rootPath, Path.GetFullPath(fullPath))
            .Replace('\\', '/');
        var name = Path.GetFileName(fullPath);

        if (!isDirectory && !name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var pattern in _settings.ExcludePatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            if (pattern == "!*.cs" && !isDirectory && name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return false;

            if (MatchesPattern(relativePath, name, pattern))
                return true;
        }

        return false;
    }

    private static bool MatchesPattern(string relativePath, string name, string pattern)
    {
        pattern = pattern.Trim().Replace('\\', '/');

        if (pattern.StartsWith("**/", StringComparison.Ordinal) &&
            pattern.EndsWith("/**", StringComparison.Ordinal))
        {
            var segment = pattern[3..^3];
            return relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
        }

        if (pattern.StartsWith("*.", StringComparison.Ordinal))
            return name.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);

        return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase) ||
               relativePath.Contains($"/{pattern}/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith($"{pattern}/", StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 5: Run matcher tests and verify pass**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter WorkspaceExcludeMatcherTests
```

Expected: PASS.

- [ ] **Step 6: Commit task**

Run:

```bash
git add Src/Workspace/WorkspaceSettings.cs Src/Workspace/WorkspaceExcludeMatcher.cs tests/DataConfigEditor.Tests/Workspace/WorkspaceExcludeMatcherTests.cs
git commit -m "feat: add workspace exclude matcher"
```

If the user has not approved commits in this session, skip the commit and record the skipped commit in the phase summary.

## Task 2: Persist Workspace Settings

**Files:**

- Create: `Src/Workspace/WorkspaceSettingsStore.cs`
- Test: `tests/DataConfigEditor.Tests/Workspace/WorkspaceSettingsStoreTests.cs`

- [ ] **Step 1: Write settings store tests**

Create `tests/DataConfigEditor.Tests/Workspace/WorkspaceSettingsStoreTests.cs`:

```csharp
using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.Workspace;

public class WorkspaceSettingsStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaultSettings()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "workspace-settings.json");
        var store = new WorkspaceSettingsStore(filePath);

        var settings = store.Load();

        Assert.False(settings.ShowHiddenEntries);
        Assert.Contains("**/bin/**", settings.ExcludePatterns);
    }

    [Fact]
    public void SaveThenLoad_PreservesSettings()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "workspace-settings.json");
        var store = new WorkspaceSettingsStore(filePath);
        var saved = WorkspaceSettings.Default with
        {
            ShowHiddenEntries = true,
            ExcludePatterns = ["**/Generated/**", "*.tmp"],
        };

        store.Save(saved);
        var loaded = store.Load();

        Assert.True(loaded.ShowHiddenEntries);
        Assert.Equal(["**/Generated/**", "*.tmp"], loaded.ExcludePatterns);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaultSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "workspace-settings.json");
        File.WriteAllText(filePath, "{ invalid");
        var store = new WorkspaceSettingsStore(filePath);

        var settings = store.Load();

        Assert.False(settings.ShowHiddenEntries);
        Assert.Contains("**/obj/**", settings.ExcludePatterns);
    }
}
```

- [ ] **Step 2: Run store tests and verify failure**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter WorkspaceSettingsStoreTests
```

Expected: FAIL because `WorkspaceSettingsStore` does not exist.

- [ ] **Step 3: Add settings store implementation**

Create `Src/Workspace/WorkspaceSettingsStore.cs`:

```csharp
using System.Text.Json;

namespace DataConfigEditor.Workspace;

public sealed class WorkspaceSettingsStore
{
    private readonly string _filePath;

    public WorkspaceSettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public WorkspaceSettings Load()
    {
        if (!File.Exists(_filePath))
            return WorkspaceSettings.Default;

        try
        {
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<WorkspaceSettings>(json);
            return Normalize(settings);
        }
        catch
        {
            return WorkspaceSettings.Default;
        }
    }

    public void Save(WorkspaceSettings settings)
    {
        var parent = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(Normalize(settings), new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        File.WriteAllText(_filePath, json);
    }

    private static WorkspaceSettings Normalize(WorkspaceSettings? settings)
    {
        if (settings is null)
            return WorkspaceSettings.Default;

        var patterns = settings.ExcludePatterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return settings with
        {
            ExcludePatterns = patterns.Length == 0 ? WorkspaceSettings.DefaultExcludePatterns : patterns,
        };
    }
}
```

- [ ] **Step 4: Run store tests and verify pass**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter WorkspaceSettingsStoreTests
```

Expected: PASS.

- [ ] **Step 5: Commit task**

Run:

```bash
git add Src/Workspace/WorkspaceSettingsStore.cs tests/DataConfigEditor.Tests/Workspace/WorkspaceSettingsStoreTests.cs
git commit -m "feat: persist workspace settings"
```

If commits are not approved, skip and record it.

## Task 3: Integrate Hidden Entries Into Workspace Tree

**Files:**

- Modify: `Src/Workspace/WorkspaceEntry.cs`
- Modify: `Src/Workspace/WorkspaceService.cs`
- Modify: `Src/MainForm.cs`
- Modify: `tests/DataConfigEditor.Tests/Workspace/WorkspaceServiceTests.cs`

- [ ] **Step 1: Add workspace service tests**

Append these tests to `tests/DataConfigEditor.Tests/Workspace/WorkspaceServiceTests.cs`:

```csharp
[Fact]
public void BuildTree_DefaultSettings_ExcludesHiddenDirectoriesAndUidFiles()
{
    var root = Directory.CreateTempSubdirectory();
    Directory.CreateDirectory(Path.Combine(root.FullName, "Ability"));
    Directory.CreateDirectory(Path.Combine(root.FullName, "obj"));
    File.WriteAllText(Path.Combine(root.FullName, "Ability", "AbilityConfigData.cs"), "public class AbilityConfigData {}");
    File.WriteAllText(Path.Combine(root.FullName, "Ability", "AbilityConfigData.cs.uid"), "ignore");
    File.WriteAllText(Path.Combine(root.FullName, "obj", "Generated.cs"), "ignore");

    var service = new WorkspaceService();

    var entry = service.BuildTree(root.FullName, WorkspaceSettings.Default);

    var ability = Assert.Single(entry.Children);
    Assert.Equal("Ability", ability.Name);
    Assert.Single(ability.Children);
    Assert.Equal("AbilityConfigData.cs", ability.Children[0].Name);
}

[Fact]
public void BuildTree_ShowHiddenEntries_IncludesHiddenEntriesMarkedHidden()
{
    var root = Directory.CreateTempSubdirectory();
    Directory.CreateDirectory(Path.Combine(root.FullName, "obj"));
    File.WriteAllText(Path.Combine(root.FullName, "obj", "Generated.cs"), "ignore");
    var settings = WorkspaceSettings.Default with { ShowHiddenEntries = true };
    var service = new WorkspaceService();

    var entry = service.BuildTree(root.FullName, settings);

    var hiddenObj = Assert.Single(entry.Children);
    Assert.Equal("obj", hiddenObj.Name);
    Assert.True(hiddenObj.IsHidden);
    Assert.True(hiddenObj.Children[0].IsHidden);
}
```

- [ ] **Step 2: Run workspace service tests and verify failure**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter WorkspaceServiceTests
```

Expected: FAIL because `BuildTree(string, WorkspaceSettings?)` and `IsHidden` do not exist.

- [ ] **Step 3: Add hidden state to workspace entries**

Modify `Src/Workspace/WorkspaceEntry.cs`:

```csharp
namespace DataConfigEditor.Workspace;

public sealed class WorkspaceEntry
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }
    public bool IsHidden { get; init; }
    public IReadOnlyList<WorkspaceEntry> Children { get; init; } = Array.Empty<WorkspaceEntry>();
}
```

- [ ] **Step 4: Apply workspace settings in service**

Replace `Src/Workspace/WorkspaceService.cs` with:

```csharp
namespace DataConfigEditor.Workspace;

public sealed class WorkspaceService
{
    public WorkspaceEntry BuildTree(string rootPath, WorkspaceSettings? settings = null)
    {
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException(rootPath);

        var effectiveSettings = settings ?? WorkspaceSettings.Default;
        var matcher = new WorkspaceExcludeMatcher(rootPath, effectiveSettings);
        return BuildEntry(rootPath, matcher, effectiveSettings, inheritedHidden: false)
            ?? throw new DirectoryNotFoundException(rootPath);
    }

    private static WorkspaceEntry? BuildEntry(
        string path,
        WorkspaceExcludeMatcher matcher,
        WorkspaceSettings settings,
        bool inheritedHidden)
    {
        var isDirectory = Directory.Exists(path);
        var isHidden = inheritedHidden || matcher.IsExcluded(path, isDirectory);

        if (isHidden && !settings.ShowHiddenEntries)
            return null;

        if (isDirectory)
        {
            var children = Directory.GetDirectories(path)
                .Select(child => BuildEntry(child, matcher, settings, isHidden))
                .Concat(Directory.GetFiles(path).Select(child => BuildEntry(child, matcher, settings, isHidden)))
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .OrderBy(entry => entry.IsDirectory ? 0 : 1)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new WorkspaceEntry
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = true,
                IsHidden = isHidden,
                Children = children,
            };
        }

        return new WorkspaceEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            IsDirectory = false,
            IsHidden = isHidden,
        };
    }
}
```

- [ ] **Step 5: Run workspace service tests and verify pass**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter WorkspaceServiceTests
```

Expected: PASS.

- [ ] **Step 6: Wire workspace settings into MainForm**

Modify `Src/MainForm.cs`:

- Add field:

```csharp
private readonly WorkspaceSettingsStore _workspaceSettingsStore;
private WorkspaceSettings _workspaceSettings;
```

- In the constructor, after `UiSettingsStore` initialization:

```csharp
_workspaceSettingsStore = new WorkspaceSettingsStore(Path.Combine(appData, "workspace-settings.json"));
_workspaceSettings = _workspaceSettingsStore.Load();
```

- In `OpenWorkspace`, replace:

```csharp
var root = _workspaceService.BuildTree(directoryPath);
```

with:

```csharp
var root = _workspaceService.BuildTree(directoryPath, _workspaceSettings);
```

- In `CreateNode`, apply hidden style:

```csharp
private static TreeNode CreateNode(WorkspaceEntry entry)
{
    var node = new TreeNode(entry.Name) { Tag = entry.FullPath };
    if (entry.IsHidden)
        node.ForeColor = SystemColors.GrayText;

    foreach (var child in entry.Children)
        node.Nodes.Add(CreateNode(child));

    return node;
}
```

- [ ] **Step 7: Add toolbar toggle for hidden entries**

In `BuildUi`, after the recent directory button, add:

```csharp
var showHiddenButton = new ToolStripButton("显示隐藏项")
{
    CheckOnClick = true,
    Checked = _workspaceSettings.ShowHiddenEntries,
};
showHiddenButton.CheckedChanged += (_, _) =>
{
    _workspaceSettings = _workspaceSettings with { ShowHiddenEntries = showHiddenButton.Checked };
    _workspaceSettingsStore.Save(_workspaceSettings);
    if (!string.IsNullOrEmpty(_currentDirectory))
        OpenWorkspace(_currentDirectory);
};
_toolStrip.Items.Add(showHiddenButton);
```

- [ ] **Step 8: Run all workspace tests**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter Workspace
```

Expected: PASS.

- [ ] **Step 9: Commit task**

Run:

```bash
git add Src/Workspace/WorkspaceEntry.cs Src/Workspace/WorkspaceService.cs Src/MainForm.cs tests/DataConfigEditor.Tests/Workspace/WorkspaceServiceTests.cs
git commit -m "feat: apply workspace hiding to tree"
```

If commits are not approved, skip and record it.

## Task 4: Unified Table Layout

**Files:**

- Create: `Src/UI/TableLayoutOptions.cs`
- Modify: `Src/Settings/UiSettings.cs`
- Modify: `Src/UI/SheetBuilder.cs`
- Modify: `Src/MainForm.cs`
- Test: `tests/DataConfigEditor.Tests/UI/TableLayoutOptionsTests.cs`
- Modify: `tests/DataConfigEditor.Tests/Settings/UiSettingsTests.cs`

- [ ] **Step 1: Write table layout option tests**

Create `tests/DataConfigEditor.Tests/UI/TableLayoutOptionsTests.cs`:

```csharp
using DataConfigEditor.Settings;
using DataConfigEditor.UI;

namespace DataConfigEditor.Tests.UI;

public class TableLayoutOptionsTests
{
    [Fact]
    public void FromSettings_Defaults_UseStableTableLayout()
    {
        var options = TableLayoutOptions.FromSettings(UiSettings.Default);

        Assert.Equal(8, options.ContentPadding);
        Assert.Equal(48, options.HeaderHeight);
        Assert.Equal(28, options.RowHeight);
        Assert.Equal(140, options.InstanceColumnWidth);
        Assert.Equal(180, options.DefaultColumnWidth);
        Assert.True(options.FreezeInstanceColumn);
    }

    [Fact]
    public void FromSettings_OutOfRangeValues_AreClamped()
    {
        var settings = UiSettings.Default with
        {
            GridTopPadding = -100,
            GridRowHeight = 999,
            FixedColumnWidth = 999,
            HeaderHeight = 10,
        };

        var options = TableLayoutOptions.FromSettings(settings);

        Assert.Equal(8, options.ContentPadding);
        Assert.Equal(UiSettings.MaxGridRowHeight, options.RowHeight);
        Assert.Equal(UiSettings.MaxFixedColumnWidth, options.DefaultColumnWidth);
        Assert.Equal(UiSettings.MinHeaderHeight, options.HeaderHeight);
    }
}
```

- [ ] **Step 2: Run layout tests and verify failure**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter TableLayoutOptionsTests
```

Expected: FAIL because `TableLayoutOptions` and `HeaderHeight` do not exist.

- [ ] **Step 3: Extend UI settings**

Modify `Src/Settings/UiSettings.cs` by adding constants/properties:

```csharp
public const int MinContentPadding = 0;
public const int MaxContentPadding = 80;
public const int MinHeaderHeight = 36;
public const int MaxHeaderHeight = 96;
public const int MinInstanceColumnWidth = 80;
public const int MaxInstanceColumnWidth = 360;

public int HeaderHeight { get; init; } = 48;
public int InstanceColumnWidth { get; init; } = 140;
public bool FreezeInstanceColumn { get; init; } = true;
public bool ShowHeaderSummary { get; init; } = true;
```

Update `Normalize()` so it returns:

```csharp
return this with
{
    GridTopPadding = Math.Clamp(GridTopPadding, MinGridTopPadding, MaxGridTopPadding),
    GridFontSize = Math.Clamp(GridFontSize, MinGridFontSize, MaxGridFontSize),
    GridRowHeight = Math.Clamp(GridRowHeight, MinGridRowHeight, MaxGridRowHeight),
    FixedColumnWidth = Math.Clamp(FixedColumnWidth, MinFixedColumnWidth, MaxFixedColumnWidth),
    HeaderHeight = Math.Clamp(HeaderHeight, MinHeaderHeight, MaxHeaderHeight),
    InstanceColumnWidth = Math.Clamp(InstanceColumnWidth, MinInstanceColumnWidth, MaxInstanceColumnWidth),
    ColumnSizingMode = mode,
};
```

- [ ] **Step 4: Add layout options**

Create `Src/UI/TableLayoutOptions.cs`:

```csharp
using DataConfigEditor.Settings;

namespace DataConfigEditor.UI;

public sealed record TableLayoutOptions
{
    public int ContentPadding { get; init; } = 8;
    public int HeaderHeight { get; init; } = 48;
    public int RowHeight { get; init; } = 28;
    public int InstanceColumnWidth { get; init; } = 140;
    public int DefaultColumnWidth { get; init; } = 180;
    public bool FreezeInstanceColumn { get; init; } = true;
    public bool ShowHeaderSummary { get; init; } = true;

    public static TableLayoutOptions FromSettings(UiSettings settings)
    {
        settings = settings.Normalize();

        return new TableLayoutOptions
        {
            ContentPadding = settings.GridTopPadding == UiSettings.Default.GridTopPadding
                ? 8
                : settings.GridTopPadding,
            HeaderHeight = settings.HeaderHeight,
            RowHeight = settings.GridRowHeight,
            InstanceColumnWidth = settings.InstanceColumnWidth,
            DefaultColumnWidth = settings.FixedColumnWidth,
            FreezeInstanceColumn = settings.FreezeInstanceColumn,
            ShowHeaderSummary = settings.ShowHeaderSummary,
        };
    }
}
```

- [ ] **Step 5: Apply layout in SheetBuilder**

Modify `Src/UI/SheetBuilder.cs`:

- At the start of `BuildGrid`, after normalization:

```csharp
var layout = TableLayoutOptions.FromSettings(settings);
```

- Replace header text assignment with:

```csharp
HeaderText = layout.ShowHeaderSummary && !string.IsNullOrEmpty(column.Summary)
    ? $"{column.Header}\n{column.Summary}"
    : column.Header,
```

- Replace frozen/width assignments with:

```csharp
Frozen = layout.FreezeInstanceColumn && column.Key == "__instance",
Width = column.Key == "__instance" ? layout.InstanceColumnWidth : layout.DefaultColumnWidth,
```

- Replace row height assignment with:

```csharp
grid.Rows[rowIndex].Height = layout.RowHeight;
```

- After setting `AutoResizeColumns`, add:

```csharp
grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
grid.ColumnHeadersHeight = layout.HeaderHeight;
grid.RowTemplate.Height = layout.RowHeight;
```

- [ ] **Step 6: Apply layout in MainForm grid host**

In `BuildUi`, replace:

```csharp
Padding = new Padding(0, _uiSettings.GridTopPadding, 0, 0),
```

with:

```csharp
Padding = new Padding(TableLayoutOptions.FromSettings(_uiSettings).ContentPadding),
```

In `ApplyUiSettings`, replace:

```csharp
_gridHost.Padding = new Padding(0, _uiSettings.GridTopPadding, 0, 0);
```

with:

```csharp
_gridHost.Padding = new Padding(TableLayoutOptions.FromSettings(_uiSettings).ContentPadding);
```

In `CreateFreshGrid`, set:

```csharp
var layout = TableLayoutOptions.FromSettings(_uiSettings);
```

and replace `RowTemplate.Height = _uiSettings.GridRowHeight` with `RowTemplate.Height = layout.RowHeight`.

- [ ] **Step 7: Run layout and settings tests**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter "TableLayoutOptionsTests|UiSettingsTests"
```

Expected: PASS.

- [ ] **Step 8: Manual layout check**

Run on Windows:

```bash
dotnet run --project DataConfigEditor.csproj -- "E:\Godot\Games\MyGames\复刻土豆兄弟\brotato-my\Data\DataNew"
```

Manual expected result:

- `AbilityConfigData.cs` and `PlayerConfigData.cs` start at the same right-panel position.
- Table host fills the right panel.
- Header height is consistent.
- The horizontal scrollbar is at the bottom of the grid area.
- A small table does not look like a floating box at the top.

- [ ] **Step 9: Commit task**

Run:

```bash
git add Src/UI/TableLayoutOptions.cs Src/Settings/UiSettings.cs Src/UI/SheetBuilder.cs Src/MainForm.cs tests/DataConfigEditor.Tests/UI/TableLayoutOptionsTests.cs tests/DataConfigEditor.Tests/Settings/UiSettingsTests.cs
git commit -m "feat: unify table layout options"
```

If commits are not approved, skip and record it.

## Task 5: Settings View Model

**Files:**

- Create: `Src/UI/SettingsViewModel.cs`
- Test: `tests/DataConfigEditor.Tests/UI/SettingsViewModelTests.cs`

- [ ] **Step 1: Write settings view model tests**

Create `tests/DataConfigEditor.Tests/UI/SettingsViewModelTests.cs`:

```csharp
using DataConfigEditor.Settings;
using DataConfigEditor.UI;
using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.UI;

public class SettingsViewModelTests
{
    [Fact]
    public void Apply_ReturnsCurrentDraftWithoutClosing()
    {
        var model = new SettingsViewModel(UiSettings.Default, WorkspaceSettings.Default);
        model.UiDraft = model.UiDraft with { GridRowHeight = 36 };

        var result = model.Apply();

        Assert.False(result.ShouldClose);
        Assert.Equal(36, result.UiSettings.GridRowHeight);
    }

    [Fact]
    public void Confirm_ReturnsCurrentDraftAndCloses()
    {
        var model = new SettingsViewModel(UiSettings.Default, WorkspaceSettings.Default);
        model.WorkspaceDraft = model.WorkspaceDraft with { ShowHiddenEntries = true };

        var result = model.Confirm();

        Assert.True(result.ShouldClose);
        Assert.True(result.WorkspaceSettings.ShowHiddenEntries);
    }

    [Fact]
    public void Cancel_ReturnsOriginalSettingsAndCloses()
    {
        var originalUi = UiSettings.Default with { GridRowHeight = 30 };
        var model = new SettingsViewModel(originalUi, WorkspaceSettings.Default);
        model.UiDraft = model.UiDraft with { GridRowHeight = 44 };

        var result = model.Cancel();

        Assert.True(result.ShouldClose);
        Assert.Equal(30, result.UiSettings.GridRowHeight);
    }

    [Fact]
    public void ResetToDefault_ReplacesDrafts()
    {
        var model = new SettingsViewModel(
            UiSettings.Default with { GridRowHeight = 44 },
            WorkspaceSettings.Default with { ShowHiddenEntries = true });

        model.ResetToDefault();

        Assert.Equal(UiSettings.Default.Normalize(), model.UiDraft);
        Assert.Equal(WorkspaceSettings.Default, model.WorkspaceDraft);
    }
}
```

- [ ] **Step 2: Run view model tests and verify failure**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter SettingsViewModelTests
```

Expected: FAIL because `SettingsViewModel` does not exist.

- [ ] **Step 3: Add settings view model**

Create `Src/UI/SettingsViewModel.cs`:

```csharp
using DataConfigEditor.Settings;
using DataConfigEditor.Workspace;

namespace DataConfigEditor.UI;

public sealed class SettingsViewModel
{
    private readonly UiSettings _originalUiSettings;
    private readonly WorkspaceSettings _originalWorkspaceSettings;

    public SettingsViewModel(UiSettings uiSettings, WorkspaceSettings workspaceSettings)
    {
        _originalUiSettings = uiSettings.Normalize();
        _originalWorkspaceSettings = workspaceSettings;
        UiDraft = _originalUiSettings;
        WorkspaceDraft = _originalWorkspaceSettings;
    }

    public UiSettings UiDraft { get; set; }

    public WorkspaceSettings WorkspaceDraft { get; set; }

    public SettingsDialogResult Apply()
    {
        UiDraft = UiDraft.Normalize();
        return new SettingsDialogResult(UiDraft, WorkspaceDraft, ShouldClose: false);
    }

    public SettingsDialogResult Confirm()
    {
        UiDraft = UiDraft.Normalize();
        return new SettingsDialogResult(UiDraft, WorkspaceDraft, ShouldClose: true);
    }

    public SettingsDialogResult Cancel()
    {
        return new SettingsDialogResult(_originalUiSettings, _originalWorkspaceSettings, ShouldClose: true);
    }

    public void ResetToDefault()
    {
        UiDraft = UiSettings.Default.Normalize();
        WorkspaceDraft = WorkspaceSettings.Default;
    }
}

public sealed record SettingsDialogResult(
    UiSettings UiSettings,
    WorkspaceSettings WorkspaceSettings,
    bool ShouldClose);
```

- [ ] **Step 4: Run view model tests and verify pass**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj --filter SettingsViewModelTests
```

Expected: PASS.

- [ ] **Step 5: Commit task**

Run:

```bash
git add Src/UI/SettingsViewModel.cs tests/DataConfigEditor.Tests/UI/SettingsViewModelTests.cs
git commit -m "feat: add settings view model"
```

If commits are not approved, skip and record it.

## Task 6: Settings Dialog UI And MainForm Wiring

**Files:**

- Create: `Src/UI/SettingsDialog.cs`
- Modify: `Src/MainForm.cs`
- Modify: `Src/Settings/UiSettings.cs`
- Test: `tests/DataConfigEditor.Tests/UI/SettingsViewModelTests.cs`

- [ ] **Step 1: Add dialog class**

Create `Src/UI/SettingsDialog.cs`:

```csharp
using DataConfigEditor.Settings;
using DataConfigEditor.Workspace;

namespace DataConfigEditor.UI;

public sealed class SettingsDialog : Form
{
    private readonly SettingsViewModel _viewModel;
    private readonly Action<SettingsDialogResult> _applyResult;
    private readonly NumericUpDown _rowHeight = new();
    private readonly NumericUpDown _columnWidth = new();
    private readonly NumericUpDown _headerHeight = new();
    private readonly NumericUpDown _contentPadding = new();
    private readonly CheckBox _freezeInstanceColumn = new() { Text = "冻结实例名列", AutoSize = true };
    private readonly CheckBox _showHeaderSummary = new() { Text = "显示中文注释副标题", AutoSize = true };
    private readonly CheckBox _showHiddenEntries = new() { Text = "显示隐藏项", AutoSize = true };
    private readonly TextBox _excludePatterns = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 120 };

    public SettingsDialog(
        UiSettings uiSettings,
        WorkspaceSettings workspaceSettings,
        Action<SettingsDialogResult> applyResult)
    {
        _viewModel = new SettingsViewModel(uiSettings, workspaceSettings);
        _applyResult = applyResult;

        Text = "设置";
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(520, 560);

        BuildUi();
        LoadDrafts();
    }

    private void BuildUi()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateTablePage());
        tabs.TabPages.Add(CreateWorkspacePage());

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
            Height = 48,
        };

        var ok = new Button { Text = "确定", Width = 80 };
        ok.Click += (_, _) => ConfirmAndClose();
        var cancel = new Button { Text = "取消", Width = 80 };
        cancel.Click += (_, _) => CancelAndClose();
        var apply = new Button { Text = "应用", Width = 80 };
        apply.Click += (_, _) => ApplyWithoutClose();
        var reset = new Button { Text = "恢复默认", Width = 88 };
        reset.Click += (_, _) =>
        {
            SaveControlsToDraft();
            _viewModel.ResetToDefault();
            LoadDrafts();
            ApplyWithoutClose();
        };

        buttons.Controls.AddRange([ok, cancel, apply, reset]);
        Controls.Add(tabs);
        Controls.Add(buttons);
    }

    private TabPage CreateTablePage()
    {
        var page = new TabPage("表格");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 6,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddNumeric(layout, "内容边距", _contentPadding, UiSettings.MinGridTopPadding, UiSettings.MaxGridTopPadding);
        AddNumeric(layout, "表头高度", _headerHeight, UiSettings.MinHeaderHeight, UiSettings.MaxHeaderHeight);
        AddNumeric(layout, "行高", _rowHeight, UiSettings.MinGridRowHeight, UiSettings.MaxGridRowHeight);
        AddNumeric(layout, "列宽", _columnWidth, UiSettings.MinFixedColumnWidth, UiSettings.MaxFixedColumnWidth);
        layout.Controls.Add(_freezeInstanceColumn, 1, 4);
        layout.Controls.Add(_showHeaderSummary, 1, 5);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage CreateWorkspacePage()
    {
        var page = new TabPage("工作区");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(_showHiddenEntries);
        layout.Controls.Add(new Label { Text = "隐藏规则，每行一条", AutoSize = true });
        layout.Controls.Add(_excludePatterns);
        page.Controls.Add(layout);
        return page;
    }

    private static void AddNumeric(
        TableLayoutPanel layout,
        string label,
        NumericUpDown control,
        int minimum,
        int maximum)
    {
        var row = layout.RowCount++;
        control.Minimum = minimum;
        control.Maximum = maximum;
        control.Width = 120;
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void LoadDrafts()
    {
        _contentPadding.Value = _viewModel.UiDraft.GridTopPadding;
        _headerHeight.Value = _viewModel.UiDraft.HeaderHeight;
        _rowHeight.Value = _viewModel.UiDraft.GridRowHeight;
        _columnWidth.Value = _viewModel.UiDraft.FixedColumnWidth;
        _freezeInstanceColumn.Checked = _viewModel.UiDraft.FreezeInstanceColumn;
        _showHeaderSummary.Checked = _viewModel.UiDraft.ShowHeaderSummary;
        _showHiddenEntries.Checked = _viewModel.WorkspaceDraft.ShowHiddenEntries;
        _excludePatterns.Text = string.Join(Environment.NewLine, _viewModel.WorkspaceDraft.ExcludePatterns);
    }

    private void SaveControlsToDraft()
    {
        _viewModel.UiDraft = _viewModel.UiDraft with
        {
            GridTopPadding = (int)_contentPadding.Value,
            HeaderHeight = (int)_headerHeight.Value,
            GridRowHeight = (int)_rowHeight.Value,
            FixedColumnWidth = (int)_columnWidth.Value,
            FreezeInstanceColumn = _freezeInstanceColumn.Checked,
            ShowHeaderSummary = _showHeaderSummary.Checked,
        };
        _viewModel.WorkspaceDraft = _viewModel.WorkspaceDraft with
        {
            ShowHiddenEntries = _showHiddenEntries.Checked,
            ExcludePatterns = _excludePatterns.Lines
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray(),
        };
    }

    private void ApplyWithoutClose()
    {
        SaveControlsToDraft();
        _applyResult(_viewModel.Apply());
    }

    private void ConfirmAndClose()
    {
        SaveControlsToDraft();
        ApplyAndClose(_viewModel.Confirm());
    }

    private void CancelAndClose()
    {
        ApplyAndClose(_viewModel.Cancel());
    }

    private void ApplyAndClose(SettingsDialogResult result)
    {
        _applyResult(result);
        DialogResult = DialogResult.OK;
        Close();
    }
}
```

- [ ] **Step 2: Replace settings dropdown in MainForm**

In `Src/MainForm.cs`, replace `_settingsButton` type:

```csharp
private ToolStripButton _settingsButton = null!;
```

In `BuildUi`, replace dropdown setup:

```csharp
_settingsButton = new ToolStripButton("设置");
_settingsButton.Click += (_, _) => ShowSettingsDialog();
_toolStrip.Items.Add(_settingsButton);
```

Remove calls to `BindSettingsMenu()` from `BuildUi` and `UpdateUiSettings`.

Add:

```csharp
private void ShowSettingsDialog()
{
    using var dialog = new SettingsDialog(_uiSettings, _workspaceSettings, ApplySettingsResult);
    dialog.ShowDialog(this);
}

private void ApplySettingsResult(SettingsDialogResult result)
{
    _uiSettings = result.UiSettings.Normalize();
    _workspaceSettings = result.WorkspaceSettings;
    _uiSettingsStore.Save(_uiSettings);
    _workspaceSettingsStore.Save(_workspaceSettings);
    ApplyUiSettings();

    if (!string.IsNullOrEmpty(_currentDirectory))
        OpenWorkspace(_currentDirectory);

    UpdateStatus(result.ShouldClose ? "设置已保存。" : "设置已应用。");
}
```

Keep `UpdateUiSettings`, `BindSettingsMenu`, `CreateSettingsItem`, and `CreateColumnSizingItem` only if tests or compiler still reference them. Otherwise delete those methods in the same commit.

- [ ] **Step 3: Compile and fix WinForms wiring errors**

Run:

```bash
dotnet build DataConfigEditor.sln
```

Expected: PASS. If compile errors reference removed settings menu members, remove the stale fields/method calls.

- [ ] **Step 4: Manual settings check**

Run on Windows:

```bash
dotnet run --project DataConfigEditor.csproj -- "E:\Godot\Games\MyGames\复刻土豆兄弟\brotato-my\Data\DataNew"
```

Manual expected result:

- Clicking `设置` opens a panel, not a dropdown of one-off actions.
- Changing row height and clicking `应用` immediately changes the grid.
- Clicking `取消` restores settings to the snapshot from when the dialog opened.
- Clicking `恢复默认` applies default settings visibly.
- Workspace hidden rules can be edited as newline-separated patterns.

- [ ] **Step 5: Commit task**

Run:

```bash
git add Src/UI/SettingsDialog.cs Src/MainForm.cs Src/Settings/UiSettings.cs
git commit -m "feat: add settings dialog"
```

If commits are not approved, skip and record it.

## Task 7: Documentation And Verification

**Files:**

- Modify: `Docs/README.md`
- Modify: `Docs/面向CSharp强类型配置的表格编辑器方案执行文档.md`
- Modify: `Docs/面向CSharp强类型配置的表格编辑器完善计划.md`

- [ ] **Step 1: Update docs after implementation**

Update `Docs/面向CSharp强类型配置的表格编辑器方案执行文档.md`:

- Mark completed next-phase tasks with `[x]`.
- Add a short phase summary under the next-phase section.
- Record skipped commits if commits were not approved.
- Record any manual Windows checks that could not be run from the current environment.

- [ ] **Step 2: Ensure README links this plan**

`Docs/README.md` must include this plan under “阶段设计与计划”:

```markdown
| [strongly-typed-config-editor-next-phase.md](./superpowers/plans/2026-04-23-strongly-typed-config-editor-next-phase.md) | 下一阶段可执行计划：工作区隐藏、表格布局统一、设置面板 |
```

- [ ] **Step 3: Run full test suite**

Run:

```bash
dotnet test tests/DataConfigEditor.Tests/DataConfigEditor.Tests.csproj
```

Expected: PASS.

- [ ] **Step 4: Run solution build**

Run:

```bash
dotnet build DataConfigEditor.sln
```

Expected: PASS.

- [ ] **Step 5: Run docs sanity checks**

Run:

```bash
git diff --check -- Docs Src tests
```

Expected: no whitespace errors.

- [ ] **Step 6: Phase completion summary**

Report:

- Tests run and pass/fail status.
- Build pass/fail status.
- Manual Windows checks run or skipped.
- Files changed.
- Whether commits were made or skipped.
- User-facing acceptance checklist.

## User Acceptance Checklist

After this plan is executed, the user should verify:

- Opening `Data/DataNew` no longer shows `.uid`, `bin`, `obj`, `.git`, `.godot`, `.idea`, `.vscode`, `.history`, or `.superpowers` by default.
- `显示隐藏项` shows hidden entries in gray.
- The hidden-entry toggle persists after restart.
- Different `.cs` tables start from the same right-panel position.
- Table headers have consistent height.
- Settings opens as a dialog/panel, not a long dropdown.
- Apply changes are visible immediately.
- Cancel restores the settings snapshot from when the dialog opened.
- Reset returns table/workspace settings to default values.

## Execution Notes

- Execute tasks in order.
- Stop after Task 7 and ask the user to验收 before starting filter/sort/search or editing work.
- Use `superpowers:using-git-worktrees` before implementation if starting from the main worktree.
- Use `superpowers:test-driven-development` for implementation tasks.
- Use `superpowers:verification-before-completion` before claiming the phase is complete.
