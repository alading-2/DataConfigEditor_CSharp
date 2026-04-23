using DataConfigEditor.Settings;
using DataConfigEditor.Workspace;

namespace DataConfigEditor.UI;

public sealed class SettingsDialog : Form
{
    private readonly SettingsViewModel _viewModel;
    private readonly Action<SettingsDialogResult> _applyResult;
    private readonly NumericUpDown _contentPadding = new();
    private readonly NumericUpDown _headerHeight = new();
    private readonly NumericUpDown _rowHeight = new();
    private readonly NumericUpDown _columnWidth = new();
    private readonly CheckBox _freezeInstanceColumn = new() { Text = "冻结实例名列", AutoSize = true };
    private readonly CheckBox _showHeaderSummary = new() { Text = "显示中文注释副标题", AutoSize = true };
    private readonly CheckBox _showHiddenEntries = new() { Text = "显示隐藏项", AutoSize = true };
    private readonly TextBox _excludePatterns = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
    };

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

        AddNumeric(layout, 0, "内容边距", _contentPadding, UiSettings.MinGridTopPadding, UiSettings.MaxGridTopPadding);
        AddNumeric(layout, 1, "表头高度", _headerHeight, UiSettings.MinHeaderHeight, UiSettings.MaxHeaderHeight);
        AddNumeric(layout, 2, "行高", _rowHeight, UiSettings.MinGridRowHeight, UiSettings.MaxGridRowHeight);
        AddNumeric(layout, 3, "列宽", _columnWidth, UiSettings.MinFixedColumnWidth, UiSettings.MaxFixedColumnWidth);
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

        layout.Controls.Add(_showHiddenEntries, 0, 0);
        layout.Controls.Add(new Label { Text = "隐藏规则，每行一条", AutoSize = true }, 0, 1);
        layout.Controls.Add(_excludePatterns, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private static void AddNumeric(
        TableLayoutPanel layout,
        int row,
        string label,
        NumericUpDown control,
        int minimum,
        int maximum)
    {
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
