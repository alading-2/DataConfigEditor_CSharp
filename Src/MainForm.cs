using DataConfigEditor.Diagnostics;
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
    private readonly CsTableParser _tableParser = new();
    private readonly SheetBuilder _sheetBuilder = new();
    private readonly RecentDirectoryStore _recentStore;
    private readonly UiSettingsStore _uiSettingsStore;

    private SplitContainer _split = null!;
    private Panel _gridHost = null!;
    private TreeView _workspaceTree = null!;
    private DataGridView _grid = null!;
    private Label _messageLabel = null!;
    private ToolStrip _toolStrip = null!;
    private ToolStripDropDownButton _recentButton = null!;
    private ToolStripDropDownButton _settingsButton = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;

    private string? _currentDirectory;
    private TableDocument? _currentDocument;
    private UiSettings _uiSettings;

    public MainForm(AppLaunchOptions launchOptions)
    {
        _launchOptions = launchOptions;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataConfigEditor");
        _recentStore = new RecentDirectoryStore(Path.Combine(appData, "recent-directories.json"));
        _uiSettingsStore = new UiSettingsStore(Path.Combine(appData, "ui-settings.json"));
        _uiSettings = _uiSettingsStore.Load();

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

        _settingsButton = new ToolStripDropDownButton("设置");
        _toolStrip.Items.Add(_settingsButton);
        BindSettingsMenu();
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
            Padding = new Padding(0, _uiSettings.GridTopPadding, 0, 0),
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
            var root = _workspaceService.BuildTree(directoryPath);
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
            var document = _tableParser.ParseFile(filePath);
            AppLog.Info(
                $"Parse result: title={document.Title}, isTable={document.IsTable}, columns={document.Columns.Count}, rows={document.Rows.Count}");

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
                _sheetBuilder.BuildGrid(_grid, state.Document, _uiSettings);
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
                Height = _uiSettings.GridRowHeight,
            },
        };

        _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", _uiSettings.GridFontSize, FontStyle.Bold);
        _grid.DefaultCellStyle.Font = new Font("Microsoft YaHei UI", _uiSettings.GridFontSize);
        _grid.RowTemplate.Height = _uiSettings.GridRowHeight;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

        _gridHost.Controls.Add(_grid);
        _gridHost.Controls.SetChildIndex(_grid, 0);
    }

    private void BindSettingsMenu()
    {
        _settingsButton.DropDownItems.Clear();
        _settingsButton.DropDownItems.Add(CreateSettingsItem(
            $"表格顶部留白 +  当前: {_uiSettings.GridTopPadding}px",
            settings => UiSettingsAdjuster.IncreaseTopPadding(settings)));
        _settingsButton.DropDownItems.Add(CreateSettingsItem(
            "表格顶部留白 -",
            settings => UiSettingsAdjuster.DecreaseTopPadding(settings)));
        _settingsButton.DropDownItems.Add(new ToolStripSeparator());
        _settingsButton.DropDownItems.Add(CreateSettingsItem(
            $"字体 +  当前: {_uiSettings.GridFontSize:0.#}",
            settings => UiSettingsAdjuster.IncreaseFontSize(settings)));
        _settingsButton.DropDownItems.Add(CreateSettingsItem(
            "字体 -",
            settings => UiSettingsAdjuster.DecreaseFontSize(settings)));
        _settingsButton.DropDownItems.Add(CreateSettingsItem(
            $"行高 +  当前: {_uiSettings.GridRowHeight}px",
            settings => UiSettingsAdjuster.IncreaseRowHeight(settings)));
        _settingsButton.DropDownItems.Add(CreateSettingsItem(
            "行高 -",
            settings => UiSettingsAdjuster.DecreaseRowHeight(settings)));
        _settingsButton.DropDownItems.Add(new ToolStripSeparator());
        _settingsButton.DropDownItems.Add(CreateSettingsItem(
            $"固定列宽 +  当前: {_uiSettings.FixedColumnWidth}px",
            settings => UiSettingsAdjuster.IncreaseColumnWidth(settings)));
        _settingsButton.DropDownItems.Add(CreateSettingsItem(
            "固定列宽 -",
            settings => UiSettingsAdjuster.DecreaseColumnWidth(settings)));
        _settingsButton.DropDownItems.Add(CreateColumnSizingItem("列宽: 固定", GridColumnSizingMode.Fixed));
        _settingsButton.DropDownItems.Add(CreateColumnSizingItem("列宽: 自动适配", GridColumnSizingMode.AutoFitDisplayedCells));
        _settingsButton.DropDownItems.Add(new ToolStripSeparator());
        _settingsButton.DropDownItems.Add(CreateSettingsItem(
            "恢复默认设置",
            _ => UiSettingsAdjuster.Reset()));
    }

    private ToolStripMenuItem CreateSettingsItem(string text, Func<UiSettings, UiSettings> update)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => UpdateUiSettings(update);
        return item;
    }

    private ToolStripMenuItem CreateColumnSizingItem(string text, GridColumnSizingMode mode)
    {
        var item = CreateSettingsItem(text, settings => UiSettingsAdjuster.SetColumnSizingMode(settings, mode));
        item.Checked = _uiSettings.ColumnSizingMode == mode;
        return item;
    }

    private void UpdateUiSettings(Func<UiSettings, UiSettings> update)
    {
        _uiSettings = update(_uiSettings).Normalize();
        _uiSettingsStore.Save(_uiSettings);
        ApplyUiSettings();
        BindSettingsMenu();
        UpdateStatus("设置已保存并应用。");
    }

    private void ApplyUiSettings()
    {
        _gridHost.Padding = new Padding(0, _uiSettings.GridTopPadding, 0, 0);

        if (_currentDocument is not null)
        {
            CreateFreshGrid(hidden: false);
            _messageLabel.Visible = false;
            _sheetBuilder.BuildGrid(_grid, _currentDocument, _uiSettings);
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
}
