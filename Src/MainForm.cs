using unvell.ReoGrid;
using DataConfigEditor.Core;
using DataConfigEditor.Parsing;
using DataConfigEditor.UI;

namespace DataConfigEditor;

/// <summary>
/// 主窗体 - 数据配置编辑器（纯源码解析版）
/// 用户直接打开 Data/DataNew 等数据目录
/// </summary>
public class MainForm : Form
{
    private ConfigTypeScanner? _scanner;
    private EnumCommentCache? _enumCache;
    private SheetBuilder? _sheetBuilder;

    // UI 控件
    private SplitContainer _split = null!;
    private ListBox _typeList = null!;
    private ReoGridControl _grid = null!;
    private ToolStrip _toolStrip = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private TextBox _searchBox = null!;

    // 当前状态
    private ConfigTypeInfo? _currentType;
    private List<PropertyMetadata> _currentProperties = new();
    private List<InstanceInfo> _currentInstances = new();
    private bool _modified;
    private string? _dataDir;

    public MainForm()
    {
        Text = "DataConfigEditor - 数据配置编辑器";
        Size = new Size(1400, 800);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildUI();
        UpdateStatus("请点击「打开目录」选择配置数据文件夹（如 Data/DataNew）");
    }

    private void BuildUI()
    {
        // === 工具栏 ===
        _toolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(4),
        };

        var btnOpen = new ToolStripButton("打开目录");
        btnOpen.Click += OnOpenDirectory;
        _toolStrip.Items.Add(btnOpen);

        _toolStrip.Items.Add(new ToolStripSeparator());

        _toolStrip.Items.Add(new ToolStripLabel("搜索:"));
        _searchBox = new TextBox { Width = 200 };
        _searchBox.TextChanged += (s, e) => ApplySearchFilter();
        _toolStrip.Items.Add(new ToolStripControlHost(_searchBox));

        _toolStrip.Items.Add(new ToolStripSeparator());

        var btnSave = new ToolStripButton("保存 (Ctrl+S)");
        btnSave.Click += OnSave;
        _toolStrip.Items.Add(btnSave);

        var btnRefresh = new ToolStripButton("刷新");
        btnRefresh.Click += OnRefresh;
        _toolStrip.Items.Add(btnRefresh);

        var btnUndo = new ToolStripButton("撤销 (Ctrl+Z)");
        btnUndo.Click += (s, e) => _grid.Undo();
        _toolStrip.Items.Add(btnUndo);

        var btnRedo = new ToolStripButton("重做 (Ctrl+Y)");
        btnRedo.Click += (s, e) => _grid.Redo();
        _toolStrip.Items.Add(btnRedo);

        _toolStrip.Items.Add(new ToolStripSeparator());

        var btnExpandAll = new ToolStripButton("全部展开");
        btnExpandAll.Click += (s, e) => _sheetBuilder?.ExpandAllGroups(_grid.CurrentWorksheet);
        _toolStrip.Items.Add(btnExpandAll);

        var btnCollapseAll = new ToolStripButton("全部折叠");
        btnCollapseAll.Click += (s, e) => _sheetBuilder?.CollapseAllGroups(_grid.CurrentWorksheet);
        _toolStrip.Items.Add(btnCollapseAll);

        Controls.Add(_toolStrip);

        // === 主体分割面板 ===
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Panel1MinSize = 150,
            Panel2MinSize = 200,
        };

        _typeList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10),
        };
        _typeList.SelectedIndexChanged += OnTypeSelected;
        _split.Panel1.Controls.Add(_typeList);

        _grid = new ReoGridControl { Dock = DockStyle.Fill };
        _grid.CurrentWorksheet.CellDataChanged += OnCellDataChanged;
        _split.Panel2.Controls.Add(_grid);

        Controls.Add(_split);

        // === 状态栏 ===
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("就绪");
        _statusStrip.Items.Add(_statusLabel);
        Controls.Add(_statusStrip);

        // === 快捷键 ===
        KeyDown += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.S) OnSave(s, e);
        };
    }

    private void OnOpenDirectory(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择配置数据目录（包含 .cs 配置文件的目录，如 Data/DataNew）",
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        OpenDataDirectory(dialog.SelectedPath);
    }

    private void OpenDataDirectory(string dataDir)
    {
        _dataDir = dataDir;
        _scanner = new ConfigTypeScanner(dataDir);
        _enumCache = new EnumCommentCache(_scanner);
        _sheetBuilder = new SheetBuilder(_enumCache, _scanner.ProjectRoot);

        if (!_scanner.LoadProjectAssembly())
        {
            MessageBox.Show(
                $"目录中未找到 .cs 文件。\n请确认选择的是正确的数据目录。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _enumCache.EnsureLoaded(null);
        RefreshTypeList();
    }

    private void RefreshTypeList()
    {
        _typeList.Items.Clear();
        if (_scanner == null) return;

        var types = _scanner.GetAllConfigTypes();
        foreach (var t in types)
            _typeList.Items.Add(t);

        UpdateStatus($"已加载 {types.Count} 个配置类 | 目录: {_dataDir} | 项目根: {_scanner.ProjectRoot}");
    }

    private void OnTypeSelected(object? sender, EventArgs e)
    {
        if (_typeList.SelectedItem is not ConfigTypeInfo typeInfo || _scanner == null || _sheetBuilder == null)
            return;

        _currentType = typeInfo;
        _currentProperties = _scanner.GetProperties(typeInfo.ClassName, typeInfo.SourceFile);
        _currentInstances = _scanner.GetInstances(typeInfo.ClassName);

        _sheetBuilder.BuildSheet(
            _grid.CurrentWorksheet,
            typeInfo,
            _currentProperties,
            _currentInstances);

        _modified = false;
        UpdateStatus();
    }

    private void OnCellDataChanged(object? sender, EventArgs e)
    {
        if (_currentType == null) return;
        _modified = true;
        UpdateStatus();
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (_currentType == null || !_modified || _sheetBuilder == null) return;

        try
        {
            _sheetBuilder.ReadBackValues(
                _grid.CurrentWorksheet,
                _currentType,
                _currentProperties,
                _currentInstances);

            int saved = CsFileWriter.WriteAllChanges(
                _currentType.SourceFile,
                _currentInstances,
                _currentProperties);

            _modified = false;
            UpdateStatus();
            MessageBox.Show($"已保存 {saved} 个实例的修改", "保存成功",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnRefresh(object? sender, EventArgs e)
    {
        if (_scanner == null) return;

        _scanner.ClearCache();
        _scanner.LoadProjectAssembly();
        _enumCache?.EnsureLoaded(null);
        RefreshTypeList();

        if (_currentType != null && _sheetBuilder != null)
        {
            _currentProperties = _scanner.GetProperties(_currentType.ClassName, _currentType.SourceFile);
            _currentInstances = _scanner.GetInstances(_currentType.ClassName);
            _sheetBuilder.BuildSheet(_grid.CurrentWorksheet, _currentType, _currentProperties, _currentInstances);
            _modified = false;
        }
    }

    private void ApplySearchFilter()
    {
        if (_currentType == null || _sheetBuilder == null) return;
        string filter = _searchBox.Text.Trim();
        _sheetBuilder.BuildSheet(
            _grid.CurrentWorksheet,
            _currentType,
            _currentProperties,
            _currentInstances,
            filter);
    }

    private void UpdateStatus(string? msg = null)
    {
        if (msg != null)
        {
            _statusLabel.Text = msg;
            return;
        }
        string modFlag = _modified ? " | 已修改（未保存）" : "";
        _statusLabel.Text = $"{_currentProperties.Count} 属性 | {_currentInstances.Count} 实例{modFlag}";
    }
}
