using DataConfigEditor.Documents;

namespace DataConfigEditor.UI;

internal sealed class ColumnFilterDialog : Form
{
    private readonly TableColumn _column;
    private readonly ComboBox _modeBox;
    private readonly TextBox _valueBox;
    private readonly TextBox _secondaryValueBox;
    private readonly CheckedListBox _enumValuesList;
    private readonly Label _valueLabel;
    private readonly Label _secondaryValueLabel;

    public ColumnFilterDialog(TableColumn column, TableFilter? existingFilter)
    {
        _column = column;

        Text = $"筛选列 - {column.Header}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(column.IsEnum ? 420 : 360, column.IsEnum ? 420 : 240);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            RowCount = 6,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text = "列",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = BuildColumnCaption(column),
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        }, 1, 0);

        layout.Controls.Add(new Label
        {
            Text = "模式",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        }, 0, 1);
        _modeBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _modeBox.SelectedIndexChanged += (_, _) => ApplyModeVisibility();
        layout.Controls.Add(_modeBox, 1, 1);

        _valueLabel = new Label
        {
            Text = "值",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        };
        layout.Controls.Add(_valueLabel, 0, 2);
        _valueBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_valueBox, 1, 2);

        _secondaryValueLabel = new Label
        {
            Text = "结束值",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        };
        layout.Controls.Add(_secondaryValueLabel, 0, 3);
        _secondaryValueBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_secondaryValueBox, 1, 3);

        layout.Controls.Add(new Label
        {
            Text = "选项",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
        }, 0, 4);
        _enumValuesList = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            Height = 180,
            CheckOnClick = true,
        };
        layout.Controls.Add(_enumValuesList, 1, 4);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        var applyButton = new Button { Text = "应用", AutoSize = true };
        applyButton.Click += (_, _) => ConfirmSelection();
        var clearButton = new Button { Text = "清除本列筛选", AutoSize = true };
        clearButton.Click += (_, _) =>
        {
            SelectedFilter = null;
            DialogResult = DialogResult.OK;
            Close();
        };
        var cancelButton = new Button { Text = "取消", AutoSize = true };
        cancelButton.Click += (_, _) => Close();

        buttons.Controls.Add(applyButton);
        buttons.Controls.Add(clearButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 1, 5);

        AcceptButton = applyButton;
        CancelButton = cancelButton;

        InitializeModes();
        PopulateExistingFilter(existingFilter);
    }

    public TableFilter? SelectedFilter { get; private set; }

    private void InitializeModes()
    {
        foreach (var mode in GetSupportedModes(_column))
            _modeBox.Items.Add(mode);

        if (_column.IsEnum)
        {
            foreach (var option in _column.EnumOptions)
                _enumValuesList.Items.Add(BuildEnumCaption(option), false);
        }

        _modeBox.SelectedIndex = 0;
    }

    private void PopulateExistingFilter(TableFilter? existingFilter)
    {
        if (existingFilter is null)
            return;

        if (_modeBox.Items.Contains(existingFilter.Mode))
            _modeBox.SelectedItem = existingFilter.Mode;

        _valueBox.Text = existingFilter.Value;
        _secondaryValueBox.Text = existingFilter.SecondaryValue;

        if (_column.IsEnum && existingFilter.Values.Count > 0)
        {
            for (var i = 0; i < _column.EnumOptions.Count && i < _enumValuesList.Items.Count; i++)
            {
                var option = _column.EnumOptions[i];
                if (existingFilter.Values.Any(value => string.Equals(value, option.Name, StringComparison.OrdinalIgnoreCase)))
                    _enumValuesList.SetItemChecked(i, true);
            }
        }
    }

    private void ApplyModeVisibility()
    {
        var mode = SelectedMode;
        var needsValue = mode is not TableFilterMode.IsEmpty and not TableFilterMode.IsNotEmpty && mode != TableFilterMode.InSet;
        var needsSecondaryValue = mode == TableFilterMode.Between;
        var usesEnumList = mode == TableFilterMode.InSet && _column.IsEnum;

        _valueLabel.Visible = needsValue;
        _valueBox.Visible = needsValue;
        _secondaryValueLabel.Visible = needsSecondaryValue;
        _secondaryValueBox.Visible = needsSecondaryValue;
        _enumValuesList.Visible = usesEnumList;
    }

    private void ConfirmSelection()
    {
        var mode = SelectedMode;
        if (_column.IsEnum && mode == TableFilterMode.InSet)
        {
            var selectedValues = _enumValuesList.CheckedIndices
                .Cast<int>()
                .Select(index => _column.EnumOptions[index].Name)
                .ToArray();

            if (selectedValues.Length == 0)
            {
                MessageBox.Show(this, "请至少选择一个枚举选项。", "筛选", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SelectedFilter = new TableFilter(_column.Key, TableFilterMode.InSet, values: selectedValues);
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (mode is not TableFilterMode.IsEmpty and not TableFilterMode.IsNotEmpty &&
            string.IsNullOrWhiteSpace(_valueBox.Text))
        {
            MessageBox.Show(this, "请输入筛选值。", "筛选", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (mode == TableFilterMode.Between && string.IsNullOrWhiteSpace(_secondaryValueBox.Text))
        {
            MessageBox.Show(this, "请输入区间结束值。", "筛选", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectedFilter = new TableFilter(_column.Key, mode, _valueBox.Text.Trim(), _secondaryValueBox.Text.Trim());
        DialogResult = DialogResult.OK;
        Close();
    }

    private TableFilterMode SelectedMode => _modeBox.SelectedItem is TableFilterMode mode
        ? mode
        : TableFilterMode.Equals;

    private static IEnumerable<TableFilterMode> GetSupportedModes(TableColumn column)
    {
        if (column.IsEnum)
        {
            yield return TableFilterMode.InSet;
            yield return TableFilterMode.Equals;
            yield return TableFilterMode.IsEmpty;
            yield return TableFilterMode.IsNotEmpty;
            yield break;
        }

        if (column.IsNumeric)
        {
            yield return TableFilterMode.Equals;
            yield return TableFilterMode.NotEquals;
            yield return TableFilterMode.GreaterThan;
            yield return TableFilterMode.GreaterThanOrEqual;
            yield return TableFilterMode.LessThan;
            yield return TableFilterMode.LessThanOrEqual;
            yield return TableFilterMode.Between;
            yield return TableFilterMode.IsEmpty;
            yield return TableFilterMode.IsNotEmpty;
            yield break;
        }

        yield return TableFilterMode.Contains;
        yield return TableFilterMode.Equals;
        yield return TableFilterMode.NotEquals;
        yield return TableFilterMode.IsEmpty;
        yield return TableFilterMode.IsNotEmpty;
    }

    private static string BuildColumnCaption(TableColumn column)
    {
        var parts = new List<string> { column.Header };
        if (!string.IsNullOrWhiteSpace(column.Summary) && column.Summary != "未注释")
            parts.Add(column.Summary);
        if (!string.IsNullOrWhiteSpace(column.TypeName))
            parts.Add($"类型: {column.TypeName}");
        return string.Join(" | ", parts);
    }

    private static string BuildEnumCaption(TableEnumOption option)
    {
        return string.IsNullOrWhiteSpace(option.Summary)
            ? option.Name
            : $"{option.Name} - {option.Summary}";
    }
}
