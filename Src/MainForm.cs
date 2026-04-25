using DataConfigEditor.Diagnostics;
using DataConfigEditor.Core;
using DataConfigEditor.Documents;
using DataConfigEditor.Parsing;
using DataConfigEditor.Presentation;
using DataConfigEditor.Settings;
using DataConfigEditor.UI;
using DataConfigEditor.Workspace;

namespace DataConfigEditor;

/// <summary>
/// 主窗体 - 工作区表格浏览器。
/// 左侧显示文件夹和 .cs 文件，右侧只显示表格或提示态。
/// </summary>
public sealed class MainForm : Form
{
    private const int LeftPanelMinSize = 220;
    private const int RightPanelMinSize = 300;
    private const int PreferredSplitterDistance = 340;

    private readonly AppLaunchOptions _launchOptions;
    private readonly WorkspaceService _workspaceService = new();
    private readonly WorkspacePresenter _presenter = new();
    private readonly SheetBuilder _sheetBuilder = new();
    private readonly RecentDirectoryStore _recentStore;
    private readonly UiSettingsStore _uiSettingsStore;
    private readonly WorkspaceSettingsStore _workspaceSettingsStore;

    private SplitContainer _split = null!;
    private Panel _gridHost = null!;
    private TreeView _workspaceTree = null!;
    private DataGridView _grid = null!;
    private Label _messageLabel = null!;
    private ToolStrip _toolStrip = null!;
    private ToolStripDropDownButton _recentButton = null!;
    private ToolStripDropDownButton _assemblyButton = null!;
    private ToolStripButton _showHiddenButton = null!;
    private ToolStripTextBox _searchBox = null!;
    private ToolStripButton _filterCurrentButton = null!;
    private ToolStripButton _clearFiltersButton = null!;
    private ToolStripButton _settingsButton = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ContextMenuStrip? _activeHeaderMenu;

    private string? _currentDirectory;
    private string? _currentFilePath;
    private TableDocument? _currentDocument;
    private TableViewState _tableViewState = TableViewState.Empty;
    private UiSettings _uiSettings;
    private WorkspaceSettings _workspaceSettings;
    private ITypeMetadataProvider? _typeMetadataProvider;

    public MainForm(AppLaunchOptions launchOptions)
    {
        _launchOptions = launchOptions;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataConfigEditor");
        _recentStore = new RecentDirectoryStore(Path.Combine(appData, "recent-directories.json"));
        _uiSettingsStore = new UiSettingsStore(Path.Combine(appData, "ui-settings.json"));
        _workspaceSettingsStore = new WorkspaceSettingsStore(Path.Combine(appData, "workspace-settings.json"));
        _uiSettings = _uiSettingsStore.Load();
        _workspaceSettings = _workspaceSettingsStore.Load();
        if (!string.IsNullOrWhiteSpace(_launchOptions.MetadataAssemblyPath))
        {
            _uiSettings = (_uiSettings with
            {
                MetadataAssemblyPath = _launchOptions.MetadataAssemblyPath,
            }).Normalize();
        }

        _typeMetadataProvider = CreateTypeMetadataProvider(_uiSettings.MetadataAssemblyPath);

        Text = "DataConfigEditor - 工作区表格浏览器";
        Size = new Size(1400, 800);
        StartPosition = FormStartPosition.CenterScreen;
    }

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
        UpdateStatus("请选择一个目录，或从最近目录中重新打开。");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyInitialSplitterDistance();
    }

    private void BuildUi()
    {
        _toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(4),
        };

        var btnOpen = new ToolStripButton("打开目录");
        btnOpen.Click += (_, _) => PromptOpenDirectory();
        _toolStrip.Items.Add(btnOpen);

        _recentButton = new ToolStripDropDownButton("最近目录");
        _toolStrip.Items.Add(_recentButton);

        _assemblyButton = new ToolStripDropDownButton("类型DLL");
        _assemblyButton.DropDownItems.Add("加载 DLL...", null, (_, _) => PromptLoadMetadataAssembly());
        _assemblyButton.DropDownItems.Add("清除 DLL", null, (_, _) => ClearMetadataAssembly());
        _toolStrip.Items.Add(_assemblyButton);
        RefreshAssemblyButton();

        _showHiddenButton = new ToolStripButton("显示隐藏项")
        {
            CheckOnClick = true,
            Checked = _workspaceSettings.ShowHiddenEntries,
        };
        _showHiddenButton.CheckedChanged += (_, _) =>
        {
            _workspaceSettings = _workspaceSettings with { ShowHiddenEntries = _showHiddenButton.Checked };
            _workspaceSettingsStore.Save(_workspaceSettings);
            if (!string.IsNullOrEmpty(_currentDirectory))
                OpenWorkspace(_currentDirectory);
        };
        _toolStrip.Items.Add(_showHiddenButton);

        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripLabel("搜索"));
        _searchBox = new ToolStripTextBox
        {
            AutoSize = false,
            Width = 180,
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _tableViewState = _tableViewState with { SearchText = _searchBox.Text };
            RenderCurrentTable();
        };
        _toolStrip.Items.Add(_searchBox);

        _filterCurrentButton = new ToolStripButton("筛选当前列");
        _filterCurrentButton.Click += (_, _) => ShowColumnFilterDialog();
        _toolStrip.Items.Add(_filterCurrentButton);

        _clearFiltersButton = new ToolStripButton("清除筛选");
        _clearFiltersButton.Click += (_, _) => ClearTableFilters();
        _toolStrip.Items.Add(_clearFiltersButton);

        _toolStrip.Items.Add(new ToolStripSeparator());
        _settingsButton = new ToolStripButton("设置");
        _settingsButton.Click += (_, _) => ShowSettingsDialog();
        _toolStrip.Items.Add(_settingsButton);
        Controls.Add(_toolStrip);

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
        };

        _workspaceTree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            Font = new Font("Microsoft YaHei UI", 9.5f),
        };
        _workspaceTree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is string fullPath && File.Exists(fullPath))
                OpenFile(fullPath);
        };
        _split.Panel1.Controls.Add(_workspaceTree);

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        _gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(TableLayoutOptions.FromSettings(_uiSettings).ContentPadding),
        };
        rightPanel.Controls.Add(_gridHost);

        CreateFreshGrid(hidden: true);

        _messageLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 11),
            Padding = new Padding(24),
        };
        rightPanel.Controls.Add(_messageLabel);

        _split.Panel2.Controls.Add(rightPanel);
        Controls.Add(_split);

        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("就绪");
        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);
    }

    private void ApplyInitialSplitterDistance()
    {
        _split.Panel1MinSize = LeftPanelMinSize;
        _split.Panel2MinSize = WorkspaceLayout.GetSafePanel2MinSize(
            _split.ClientSize.Width,
            LeftPanelMinSize,
            RightPanelMinSize);

        var safeDistance = WorkspaceLayout.GetSafeSplitterDistance(
            _split.ClientSize.Width,
            _split.Panel1MinSize,
            _split.Panel2MinSize,
            PreferredSplitterDistance);

        _split.SplitterDistance = safeDistance;
    }

    private void PromptOpenDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择工作区目录",
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog() == DialogResult.OK)
            OpenWorkspace(dialog.SelectedPath);
    }

    private void OpenWorkspace(string directoryPath)
    {
        try
        {
            _currentDirectory = directoryPath;
            var root = _workspaceService.BuildTree(directoryPath, _workspaceSettings);
            BindTree(root);

            var recents = _recentStore.Load()
                .Where(path => Directory.Exists(path))
                .Where(path => !string.Equals(path, directoryPath, StringComparison.OrdinalIgnoreCase))
                .Prepend(directoryPath)
                .Take(10)
                .ToList();

            _recentStore.Save(recents);
            RenderState(_presenter.ShowWelcome(recents));
            UpdateStatus($"已打开目录: {directoryPath}");
        }
        catch (Exception ex)
        {
            _workspaceTree.Nodes.Clear();
            RenderMessage($"无法打开目录。\n{ex.Message}");
            UpdateStatus("打开目录失败");
        }
    }

    private void OpenFile(string filePath)
    {
        try
        {
            AppLog.Info($"OpenFile: {filePath}");
            _currentFilePath = filePath;
            var parser = CreateTableParser();
            var document = parser.ParseFile(filePath);
            AppLog.Info(
                $"Parse result: title={document.Title}, isTable={document.IsTable}, columns={document.Columns.Count}, rows={document.Rows.Count}");

            _tableViewState = TableViewState.Empty;
            _searchBox.Text = "";
            RenderState(_presenter.ShowDocument(document));
            UpdateStatus($"已打开文件: {filePath}");
        }
        catch (Exception ex)
        {
            AppLog.Error($"OpenFile failed: {filePath}", ex);
            RenderMessage($"打开文件失败。\n{ex.Message}");
            UpdateStatus("打开文件失败");
        }
    }

    private void BindTree(WorkspaceEntry root)
    {
        _workspaceTree.BeginUpdate();
        _workspaceTree.Nodes.Clear();
        _workspaceTree.Nodes.Add(CreateNode(root));
        _workspaceTree.ExpandAll();
        _workspaceTree.EndUpdate();
    }

    private static TreeNode CreateNode(WorkspaceEntry entry)
    {
        var node = new TreeNode(entry.Name) { Tag = entry.FullPath };
        if (entry.IsHidden)
            node.ForeColor = SystemColors.GrayText;

        foreach (var child in entry.Children)
            node.Nodes.Add(CreateNode(child));
        return node;
    }

    private void RenderState(WorkspaceViewState state)
    {
        BindRecentDirectories(state.RecentDirectories);

        if (state.Kind == WorkspaceViewKind.Table && state.Document is not null)
        {
            try
            {
                _currentDocument = state.Document;
                CreateFreshGrid(hidden: false);
                _messageLabel.Visible = false;
                RenderCurrentTable();
                return;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Render table failed: {state.Document.SourceFilePath}", ex);
                RenderMessage($"表格渲染失败。\n{ex.Message}");
                return;
            }
        }

        _currentDocument = null;
        _currentFilePath = null;
        _tableViewState = TableViewState.Empty;
        CreateFreshGrid(hidden: true);
        _messageLabel.Visible = true;

        if (state.Kind == WorkspaceViewKind.Welcome)
        {
            var recents = state.RecentDirectories.Count == 0
                ? "暂无最近目录。"
                : $"最近目录:\n{string.Join("\n", state.RecentDirectories)}";
            _messageLabel.Text = $"请选择一个目录，或从最近目录中重新打开。\n\n{recents}";
            return;
        }

        RenderMessage(state.Message);
    }

    private void RenderMessage(string message)
    {
        CreateFreshGrid(hidden: true);
        _messageLabel.Visible = true;
        _messageLabel.Text = message;
    }

    private void CreateFreshGrid(bool hidden)
    {
        var layout = TableLayoutOptions.FromSettings(_uiSettings);

        if (_grid != null)
        {
            _gridHost.Controls.Remove(_grid);
            _grid.Dispose();
        }

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            Visible = !hidden,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
            RowTemplate =
            {
                Height = layout.RowHeight,
            },
        };

        _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", _uiSettings.GridFontSize, FontStyle.Bold);
        _grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", _uiSettings.GridFontSize);
        _grid.RowTemplate.Height = layout.RowHeight;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = layout.HeaderHeight;
        _grid.ColumnHeaderMouseClick += (_, e) =>
        {
            if (e.ColumnIndex < 0 || e.ColumnIndex >= _grid.Columns.Count)
                return;

            var columnKey = _grid.Columns[e.ColumnIndex].Name;
            ShowColumnFilterFromHeader(columnKey, e.ColumnIndex);
        };

        _gridHost.Controls.Add(_grid);
        _gridHost.Controls.SetChildIndex(_grid, 0);
    }

    private void RenderCurrentTable()
    {
        if (_currentDocument is null || !_currentDocument.IsTable)
            return;

        var result = _tableViewState.Apply(_currentDocument);
        var viewDocument = new TableDocument
        {
            SourceFilePath = _currentDocument.SourceFilePath,
            Title = _currentDocument.Title,
            Columns = _currentDocument.Columns,
            Rows = result.Rows,
        };

        var viewport = CaptureGridViewport();
        _sheetBuilder.BuildGrid(_grid, viewDocument, _uiSettings);
        RestoreGridViewport(viewport, viewDocument);
        UpdateTableStatus(result);
    }

    private TableGridViewportSnapshot? CaptureGridViewport()
    {
        if (_grid.Columns.Count == 0)
            return null;

        var currentRowIndex = _grid.Rows.Count > 0
            ? ClampGridIndex(_grid.CurrentCell?.RowIndex ?? 0, _grid.Rows.Count)
            : -1;
        var firstDisplayedRowIndex = _grid.Rows.Count > 0
            ? ClampGridIndex(GetFirstDisplayedScrollingRowIndex(currentRowIndex), _grid.Rows.Count)
            : -1;
        var firstDisplayedColumnIndex = ClampGridIndex(GetFirstDisplayedScrollingColumnIndex(0), _grid.Columns.Count);
        var currentColumnIndex = ClampGridIndex(
            _grid.CurrentCell?.ColumnIndex ?? firstDisplayedColumnIndex,
            _grid.Columns.Count);

        return new TableGridViewportSnapshot(
            CurrentRowHeader: GetGridRowHeader(currentRowIndex),
            CurrentColumnKey: GetGridColumnKey(currentColumnIndex),
            CurrentRowIndex: currentRowIndex,
            CurrentColumnIndex: currentColumnIndex,
            FirstDisplayedRowHeader: GetGridRowHeader(firstDisplayedRowIndex),
            FirstDisplayedRowIndex: firstDisplayedRowIndex,
            FirstDisplayedColumnKey: GetGridColumnKey(firstDisplayedColumnIndex),
            FirstDisplayedColumnIndex: firstDisplayedColumnIndex,
            HorizontalScrollingOffset: Math.Max(0, _grid.HorizontalScrollingOffset));
    }

    private void RestoreGridViewport(TableGridViewportSnapshot? snapshot, TableDocument viewDocument)
    {
        if (_grid.Columns.Count == 0)
            return;

        var target = TableGridViewport.Resolve(snapshot, viewDocument.Columns, viewDocument.Rows);
        if (target.CurrentColumnIndex < 0)
            return;

        if (target.CurrentRowIndex >= 0 && target.CurrentRowIndex < _grid.Rows.Count)
        {
            _grid.ClearSelection();
            _grid.CurrentCell = _grid.Rows[target.CurrentRowIndex].Cells[target.CurrentColumnIndex];
        }

        SetFirstDisplayedScrollingRowIndex(target.FirstDisplayedRowIndex);
        SetFirstDisplayedScrollingColumnIndex(target.FirstDisplayedColumnIndex);
        SetHorizontalScrollingOffset(target.HorizontalScrollingOffset);
    }

    private int GetFirstDisplayedScrollingRowIndex(int fallback)
    {
        try
        {
            return _grid.FirstDisplayedScrollingRowIndex;
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
        catch (ArgumentOutOfRangeException)
        {
            return fallback;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private int GetFirstDisplayedScrollingColumnIndex(int fallback)
    {
        try
        {
            return _grid.FirstDisplayedScrollingColumnIndex;
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
        catch (ArgumentOutOfRangeException)
        {
            return fallback;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private void SetFirstDisplayedScrollingRowIndex(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _grid.Rows.Count)
            return;

        try
        {
            _grid.FirstDisplayedScrollingRowIndex = rowIndex;
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
        catch (ArgumentException)
        {
        }
    }

    private void SetFirstDisplayedScrollingColumnIndex(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
            return;

        try
        {
            _grid.FirstDisplayedScrollingColumnIndex = columnIndex;
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
        catch (ArgumentException)
        {
        }
    }

    private void SetHorizontalScrollingOffset(int offset)
    {
        try
        {
            _grid.HorizontalScrollingOffset = Math.Max(0, offset);
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentOutOfRangeException)
        {
        }
        catch (ArgumentException)
        {
        }
    }

    private string? GetGridRowHeader(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _grid.Rows.Count || _grid.Columns.Count == 0)
            return null;

        return _grid.Rows[rowIndex].Cells[0].Value?.ToString();
    }

    private string? GetGridColumnKey(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
            return null;

        return _grid.Columns[columnIndex].Name;
    }

    private static int ClampGridIndex(int index, int count)
    {
        if (count <= 0)
            return -1;

        return Math.Min(Math.Max(index, 0), count - 1);
    }

    private void ShowColumnFilterDialog(string? explicitColumnKey = null)
    {
        if (_currentDocument is null || !_currentDocument.IsTable)
            return;

        var columnKey = explicitColumnKey;
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            if (_grid.CurrentCell is null)
                return;

            columnKey = _grid.Columns[_grid.CurrentCell.ColumnIndex].Name;
        }

        var column = _currentDocument.Columns.FirstOrDefault(item =>
            string.Equals(item.Key, columnKey, StringComparison.OrdinalIgnoreCase));
        if (column is null)
            return;

        var existingFilter = _tableViewState.Filters.TryGetValue(column.Key, out var filter)
            ? filter
            : null;

        using var dialog = new ColumnFilterDialog(column, existingFilter);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _tableViewState = dialog.SelectedFilter is null
            ? _tableViewState.WithoutFilter(column.Key)
            : _tableViewState.WithFilter(dialog.SelectedFilter);

        RenderCurrentTable();
    }

    private void ShowColumnFilterFromHeader(string columnKey, int columnIndex)
    {
        if (_currentDocument is null || !_currentDocument.IsTable)
            return;

        var column = _currentDocument.Columns.FirstOrDefault(item =>
            string.Equals(item.Key, columnKey, StringComparison.OrdinalIgnoreCase));
        if (column is null)
            return;

        if (column.IsEnum && column.EnumOptions.Count > 0)
        {
            ShowEnumFilterMenu(column, columnIndex);
            return;
        }

        if (column.IsBool)
        {
            ShowBooleanFilterMenu(column, columnIndex);
            return;
        }

        ShowColumnFilterDialog(columnKey);
    }

    private void ShowEnumFilterMenu(TableColumn column, int columnIndex)
    {
        var menu = CreateHeaderFilterMenu();
        AddClearFilterMenuItem(menu, column);
        menu.Items.Add(new ToolStripSeparator());

        foreach (var option in column.EnumOptions)
        {
            var item = new ToolStripMenuItem(string.IsNullOrWhiteSpace(option.Summary)
                ? option.Name
                : $"{option.Name} - {option.Summary}");
            item.Checked = _tableViewState.Filters.TryGetValue(column.Key, out var filter) &&
                           filter.Mode == TableFilterMode.InSet &&
                           filter.Values.Any(value => string.Equals(value, option.Name, StringComparison.OrdinalIgnoreCase));
            item.Click += (_, _) => ApplySingleValueFilter(column.Key, option.Name);
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        AddEmptyFilterMenuItems(menu, column);
        ShowHeaderMenu(menu, columnIndex);
    }

    private void ShowBooleanFilterMenu(TableColumn column, int columnIndex)
    {
        var menu = CreateHeaderFilterMenu();
        AddClearFilterMenuItem(menu, column);
        menu.Items.Add(new ToolStripSeparator());

        var trueItem = new ToolStripMenuItem("true");
        trueItem.Click += (_, _) => ApplyValueFilter(column.Key, TableFilterMode.Equals, "true");
        menu.Items.Add(trueItem);

        var falseItem = new ToolStripMenuItem("false");
        falseItem.Click += (_, _) => ApplyValueFilter(column.Key, TableFilterMode.Equals, "false");
        menu.Items.Add(falseItem);

        menu.Items.Add(new ToolStripSeparator());
        AddEmptyFilterMenuItems(menu, column);
        ShowHeaderMenu(menu, columnIndex);
    }

    private ContextMenuStrip CreateHeaderFilterMenu()
    {
        if (_activeHeaderMenu is { IsDisposed: false })
            _activeHeaderMenu.Close();

        var menu = new ContextMenuStrip();
        _activeHeaderMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_activeHeaderMenu, menu))
                _activeHeaderMenu = null;

            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(() => menu.Dispose());
            else
                menu.Dispose();
        };

        return menu;
    }

    private void AddClearFilterMenuItem(ContextMenuStrip menu, TableColumn column)
    {
        var clear = new ToolStripMenuItem("清除本列筛选")
        {
            Enabled = _tableViewState.Filters.ContainsKey(column.Key),
        };
        clear.Click += (_, _) =>
        {
            _tableViewState = _tableViewState.WithoutFilter(column.Key);
            RenderCurrentTable();
        };
        menu.Items.Add(clear);
    }

    private void AddEmptyFilterMenuItems(ContextMenuStrip menu, TableColumn column)
    {
        var empty = new ToolStripMenuItem("空值");
        empty.Click += (_, _) => ApplyValueFilter(column.Key, TableFilterMode.IsEmpty);
        menu.Items.Add(empty);

        var notEmpty = new ToolStripMenuItem("非空");
        notEmpty.Click += (_, _) => ApplyValueFilter(column.Key, TableFilterMode.IsNotEmpty);
        menu.Items.Add(notEmpty);
    }

    private void ApplySingleValueFilter(string columnKey, string value)
    {
        _tableViewState = _tableViewState.WithFilter(
            new TableFilter(columnKey, TableFilterMode.InSet, values: [value]));
        RenderCurrentTable();
    }

    private void ApplyValueFilter(string columnKey, TableFilterMode mode, string value = "")
    {
        _tableViewState = _tableViewState.WithFilter(new TableFilter(columnKey, mode, value));
        RenderCurrentTable();
    }

    private void ShowHeaderMenu(ContextMenuStrip menu, int columnIndex)
    {
        var headerRectangle = _grid.GetCellDisplayRectangle(columnIndex, -1, cutOverflow: true);
        menu.Show(_grid, new Point(headerRectangle.Left, headerRectangle.Bottom));
    }

    private void ClearTableFilters()
    {
        _tableViewState = TableViewState.Empty;
        if (_searchBox.Text.Length > 0)
            _searchBox.Text = "";
        else
            RenderCurrentTable();
    }

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
        _typeMetadataProvider = CreateTypeMetadataProvider(_uiSettings.MetadataAssemblyPath);
        RefreshAssemblyButton();
        ApplyUiSettings();

        if (_showHiddenButton.Checked != _workspaceSettings.ShowHiddenEntries)
            _showHiddenButton.Checked = _workspaceSettings.ShowHiddenEntries;

        if (!string.IsNullOrEmpty(_currentDirectory))
            OpenWorkspace(_currentDirectory);

        UpdateStatus(result.ShouldClose ? "设置已保存。" : "设置已应用。");
    }

    private void ApplyUiSettings()
    {
        _gridHost.Padding = new Padding(TableLayoutOptions.FromSettings(_uiSettings).ContentPadding);

        if (_currentDocument is not null)
        {
            CreateFreshGrid(hidden: false);
            _messageLabel.Visible = false;
            RenderCurrentTable();
        }
    }

    private void BindRecentDirectories(IReadOnlyList<string> recentDirectories)
    {
        _recentButton.DropDownItems.Clear();

        if (recentDirectories.Count == 0)
        {
            _recentButton.Enabled = false;
            return;
        }

        _recentButton.Enabled = true;
        foreach (var path in recentDirectories)
        {
            var item = new ToolStripMenuItem(path);
            item.Click += (_, _) => OpenWorkspace(path);
            _recentButton.DropDownItems.Add(item);
        }
    }

    private void UpdateStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void UpdateTableStatus(TableViewResult result)
    {
        var parts = new List<string>
        {
            $"显示 {result.Rows.Count} / 共 {result.TotalRows} 行",
        };

        if (result.ActiveFilterCount > 0)
            parts.Add($"筛选 {result.ActiveFilterCount} 项");

        if (!string.IsNullOrWhiteSpace(result.SearchText))
            parts.Add($"搜索: {result.SearchText}");

        if (_tableViewState.Sort.Direction != TableSortDirection.None)
            parts.Add($"排序: {_tableViewState.Sort.ColumnKey} {_tableViewState.Sort.Direction}");

        _statusLabel.Text = string.Join(" | ", parts);
    }

    private CsTableParser CreateTableParser()
    {
        return new CsTableParser(_typeMetadataProvider, allowAutoAssemblyDiscovery: false);
    }

    private static ITypeMetadataProvider? CreateTypeMetadataProvider(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
            return null;

        AppLog.Info($"CreateTypeMetadataProvider: {assemblyPath}");
        return new AssemblyTypeMetadataProvider(assemblyPath);
    }

    private void PromptLoadMetadataAssembly()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择类型元数据 DLL",
            Filter = "程序集 (*.dll)|*.dll",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = ResolveAssemblyInitialDirectory(),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            AppLog.Info($"Load metadata DLL requested: {dialog.FileName}");
            var provider = new AssemblyTypeMetadataProvider(dialog.FileName);
            _typeMetadataProvider = provider;
            _uiSettings = (_uiSettings with { MetadataAssemblyPath = provider.AssemblyPath }).Normalize();
            _uiSettingsStore.Save(_uiSettings);
            RefreshAssemblyButton();
            ReopenCurrentFile();
            AppLog.Info($"Load metadata DLL succeeded: {provider.AssemblyPath}");
            UpdateStatus($"已加载 DLL: {Path.GetFileName(provider.AssemblyPath)}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Load metadata DLL failed", ex);
            MessageBox.Show(this, $"加载 DLL 失败。\n{ex.Message}", "加载 DLL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateStatus("加载 DLL 失败");
        }
    }

    private void ClearMetadataAssembly()
    {
        if (string.IsNullOrWhiteSpace(_uiSettings.MetadataAssemblyPath))
            return;

        _typeMetadataProvider = null;
        _uiSettings = (_uiSettings with { MetadataAssemblyPath = "" }).Normalize();
        _uiSettingsStore.Save(_uiSettings);
        RefreshAssemblyButton();
        ReopenCurrentFile();
        AppLog.Info("Metadata DLL cleared.");
        UpdateStatus("已清除 DLL 绑定。");
    }

    private void RefreshAssemblyButton()
    {
        var hasAssembly = !string.IsNullOrWhiteSpace(_uiSettings.MetadataAssemblyPath);
        _assemblyButton.Text = hasAssembly
            ? $"DLL: {Path.GetFileName(_uiSettings.MetadataAssemblyPath)}"
            : "类型DLL";
        _assemblyButton.ToolTipText = hasAssembly
            ? _uiSettings.MetadataAssemblyPath
            : "未加载类型元数据 DLL。";

        if (_assemblyButton.DropDownItems.Count >= 2)
            _assemblyButton.DropDownItems[1].Enabled = hasAssembly;
    }

    private string ResolveAssemblyInitialDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_uiSettings.MetadataAssemblyPath))
        {
            var configuredDirectory = Path.GetDirectoryName(_uiSettings.MetadataAssemblyPath);
            if (!string.IsNullOrWhiteSpace(configuredDirectory) && Directory.Exists(configuredDirectory))
                return configuredDirectory;
        }

        if (!string.IsNullOrWhiteSpace(_currentDirectory) && Directory.Exists(_currentDirectory))
            return _currentDirectory;

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private void ReopenCurrentFile()
    {
        if (!string.IsNullOrWhiteSpace(_currentFilePath) && File.Exists(_currentFilePath))
            OpenFile(_currentFilePath);
    }
}
