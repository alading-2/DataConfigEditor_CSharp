using DataConfigEditor.Documents;
using DataConfigEditor.Parsing;
using DataConfigEditor.Presentation;
using DataConfigEditor.UI;
using DataConfigEditor.Workspace;
using unvell.ReoGrid;

namespace DataConfigEditor;

/// <summary>
/// 主窗体 - 工作区表格浏览器。
/// 左侧显示文件夹和 .cs 文件，右侧只显示表格或提示态。
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppLaunchOptions _launchOptions;
    private readonly WorkspaceService _workspaceService = new();
    private readonly WorkspacePresenter _presenter = new();
    private readonly CsTableParser _tableParser = new();
    private readonly SheetBuilder _sheetBuilder = new();
    private readonly RecentDirectoryStore _recentStore;

    private SplitContainer _split = null!;
    private TreeView _workspaceTree = null!;
    private ReoGridControl _grid = null!;
    private Label _messageLabel = null!;
    private ToolStrip _toolStrip = null!;
    private ToolStripDropDownButton _recentButton = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;

    private string? _currentDirectory;

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
        Controls.Add(_toolStrip);

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Panel1MinSize = 220,
            Panel2MinSize = 300,
            SplitterDistance = 340,
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

        _grid = new ReoGridControl
        {
            Dock = DockStyle.Fill,
            Visible = false,
        };
        rightPanel.Controls.Add(_grid);

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
        var document = _tableParser.ParseFile(filePath);
        RenderState(_presenter.ShowDocument(document));
        UpdateStatus($"已打开文件: {filePath}");
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
            _messageLabel.Visible = false;
            _grid.Visible = true;
            _sheetBuilder.BuildSheet(_grid.CurrentWorksheet, state.Document);
            return;
        }

        _grid.CurrentWorksheet.Reset();
        _grid.Visible = false;
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
        _grid.Visible = false;
        _messageLabel.Visible = true;
        _messageLabel.Text = message;
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
