#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Slime.Addons.DataConfigEditor
{
    /// <summary>
    /// DataNew 配置表格面板（v5 重构版）
    /// 布局：属性行=列（横着），实例=行（竖着），与 Excel 一致
    /// 表头：属性名+中文注释两行显示在同一个格子
    /// 枚举：英文名+中文注释两行显示，点击弹出选择菜单
    /// </summary>
    public partial class ConfigTablePanel : VBoxContainer
    {
        // === 方法名常量（供 UndoRedo 使用） ===
        private static readonly StringName MethodNameSetCellValue = "SetCellValue";

        // === 工具栏 ===
        private HBoxContainer _toolbar = null!;
        private OptionButton _configTypeSelector = null!;
        private Button _refreshBtn = null!;
        private Button _saveBtn = null!;
        private Button _toggleLayoutBtn = null!;
        private Label _statusLabel = null!;
        private LineEdit _searchBox = null!;
        private OptionButton _bulkPropertySelector = null!;
        private LineEdit _bulkValueInput = null!;
        private Button _bulkApplyBtn = null!;

        // === 表格区域 ===
        private ScrollContainer _scrollV = null!;
        private GridContainer _grid = null!;
        private Panel _emptyPanel = null!;
        private Label _detailLabel = null!;

        // === 数据 ===
        private Type? _currentType;
        private string? _currentSourceFile;
        private List<PropertyMetadata> _allProperties = new();
        private List<PropertyMetadata> _filteredProperties = new();
        private List<ConfigReflectionCache.InstanceInfo> _instances = new();
        private Dictionary<string, PropertyCommentInfo> _comments = new();
        private bool _modified;
        private readonly Dictionary<string, DirtyCellInfo> _dirtyCells = new(StringComparer.Ordinal);

        private sealed class DirtyCellInfo
        {
            public ConfigReflectionCache.InstanceInfo Instance { get; init; } = null!;
            public PropertyMetadata Property { get; init; } = null!;
            public string ValueText { get; init; } = "";
        }

        // === 布局模式 ===
        private bool _layoutRowsAreInstances = true; // true=行=实例(属性横着), false=行=属性

        // === 多选 ===
        private readonly CellSelectionManager _selection = new();

        // === 图片预览 ===
        private readonly ImagePreviewCache _imagePreview = new();

        // === 搜索防抖 ===
        private string _pendingSearch = "";
        private double _searchDebounceTimer;
        private const double SearchDebounceInterval = 0.3;

        // === 尺寸常量 ===
        private const float HEADER_HEIGHT = 48;       // 两行表头高度
        private const float CELL_HEIGHT = 28;          // 普通单元格
        private const float ENUM_CELL_HEIGHT = 40;     // 枚举单元格（两行）
        private const float CELL_MIN_WIDTH = 130;
        private const float NAME_COL_WIDTH = 160;
        private const float THUMBNAIL_SIZE = 28;

        // === StyleBox 缓存 ===
        private static StyleBoxFlat? _cachedHeaderStyle;
        private static StyleBoxFlat? _cachedBodyStyle;
        private static StyleBoxFlat? _cachedSelectedStyle;
        private static StyleBoxFlat? _cachedRowNameStyle;

        // ============================================================
        //  初始化
        // ============================================================

        public override void _Ready()
        {
            try
            {
                BuildUI();
                PopulateTypeSelector();
            }
            catch (Exception e)
            {
                GD.PrintErr($"[DataConfigEditor] 初始化失败: {e}");
            }
        }

        public override void _Process(double delta)
        {
            if (_searchDebounceTimer > 0)
            {
                _searchDebounceTimer -= delta;
                if (_searchDebounceTimer <= 0)
                {
                    _searchDebounceTimer = 0;
                    ApplySearch();
                }
            }
        }

        // ============================================================
        //  UI 构建
        // ============================================================

        private void BuildUI()
        {
            _toolbar = new HBoxContainer { CustomMinimumSize = new Vector2(0, 36) };

            _toolbar.AddChild(new Label
            {
                Text = " 配置类: ",
                VerticalAlignment = VerticalAlignment.Center,
            });

            _configTypeSelector = new OptionButton { CustomMinimumSize = new Vector2(280, 30) };
            _configTypeSelector.ItemSelected += OnTypeSelected;
            _toolbar.AddChild(_configTypeSelector);

            _toolbar.AddChild(new VSeparator { CustomMinimumSize = new Vector2(8, 0) });

            _searchBox = new LineEdit
            {
                PlaceholderText = "搜索属性...",
                CustomMinimumSize = new Vector2(150, 28),
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            };
            _searchBox.TextChanged += OnSearchChanged;
            _toolbar.AddChild(_searchBox);

            _toolbar.AddChild(new VSeparator { CustomMinimumSize = new Vector2(4, 0) });

            _toggleLayoutBtn = new Button
            {
                Text = "布局: 行=实例",
                Flat = true,
                TooltipText = "切换表格布局方向",
            };
            _toggleLayoutBtn.Pressed += ToggleLayout;
            _toolbar.AddChild(_toggleLayoutBtn);

            _toolbar.AddChild(new VSeparator { CustomMinimumSize = new Vector2(4, 0) });

            _refreshBtn = new Button { Text = "刷新", Flat = true };
            _refreshBtn.Pressed += OnRefresh;
            _toolbar.AddChild(_refreshBtn);

            _saveBtn = new Button
            {
                Text = "保存C#",
                Flat = true,
                Disabled = true,
                TooltipText = "保存到当前 DataNew C# 源码文件；Godot 顶部/菜单保存不会触发这个按钮",
            };
            _saveBtn.Pressed += OnSave;
            _toolbar.AddChild(_saveBtn);

            _toolbar.AddChild(new VSeparator { CustomMinimumSize = new Vector2(8, 0) });

            _bulkPropertySelector = new OptionButton
            {
                CustomMinimumSize = new Vector2(160, 28),
                Disabled = true,
                TooltipText = "选择要批量修改的属性",
            };
            _bulkPropertySelector.ItemSelected += OnBulkPropertySelected;
            _toolbar.AddChild(_bulkPropertySelector);

            _bulkValueInput = new LineEdit
            {
                PlaceholderText = "批量值",
                CustomMinimumSize = new Vector2(120, 28),
                Editable = false,
            };
            _toolbar.AddChild(_bulkValueInput);

            _bulkApplyBtn = new Button
            {
                Text = "批量应用",
                Flat = true,
                Disabled = true,
                TooltipText = "把当前批量值应用到所有实例",
            };
            _bulkApplyBtn.Pressed += OnApplyBulkEdit;
            _toolbar.AddChild(_bulkApplyBtn);

            _toolbar.AddChild(new VSeparator { CustomMinimumSize = new Vector2(8, 0) });

            _statusLabel = new Label
            {
                Text = "选择配置类开始",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _toolbar.AddChild(_statusLabel);

            AddChild(_toolbar);

            _scrollV = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            };
            AddChild(_scrollV);

            _grid = new GridContainer { Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _scrollV.AddChild(_grid);

            _emptyPanel = new Panel
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            var emptyLabel = new Label
            {
                Text = "请选择配置类\n选择后将以表格形式展示所有配置属性",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            _emptyPanel.AddChild(emptyLabel);
            AddChild(_emptyPanel);

            SetContentVisibility(false);

            _detailLabel = new Label
            {
                Text = "",
                CustomMinimumSize = new Vector2(0, 24),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _detailLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.8f));
            AddChild(_detailLabel);
        }

        // ============================================================
        //  StyleBox 缓存
        // ============================================================

        private static StyleBoxFlat GetHeaderStyle()
        {
            if (_cachedHeaderStyle != null) return _cachedHeaderStyle;
            _cachedHeaderStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.20f, 0.24f),
                BorderColor = new Color(0.26f, 0.29f, 0.35f),
                BorderWidthBottom = 1,
                ContentMarginLeft = 4,
                ContentMarginRight = 4,
                ContentMarginTop = 2,
                ContentMarginBottom = 2,
            };
            return _cachedHeaderStyle;
        }

        private static StyleBoxFlat GetBodyStyle()
        {
            if (_cachedBodyStyle != null) return _cachedBodyStyle;
            _cachedBodyStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.11f, 0.12f, 0.14f),
                BorderColor = new Color(0.20f, 0.22f, 0.26f),
                BorderWidthBottom = 1,
                ContentMarginLeft = 3,
                ContentMarginRight = 3,
                ContentMarginTop = 2,
                ContentMarginBottom = 2,
            };
            return _cachedBodyStyle;
        }

        private static StyleBoxFlat GetSelectedStyle()
        {
            if (_cachedSelectedStyle != null) return _cachedSelectedStyle;
            _cachedSelectedStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.15f, 0.25f, 0.40f),
                BorderColor = new Color(0.30f, 0.50f, 0.80f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                ContentMarginLeft = 3,
                ContentMarginRight = 3,
                ContentMarginTop = 2,
                ContentMarginBottom = 2,
            };
            return _cachedSelectedStyle;
        }

        private static StyleBoxFlat GetRowNameStyle()
        {
            if (_cachedRowNameStyle != null) return _cachedRowNameStyle;
            _cachedRowNameStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.16f, 0.18f, 0.22f),
                BorderColor = new Color(0.26f, 0.29f, 0.35f),
                BorderWidthBottom = 1,
                ContentMarginLeft = 6,
                ContentMarginRight = 6,
                ContentMarginTop = 2,
                ContentMarginBottom = 2,
            };
            return _cachedRowNameStyle;
        }

        // ============================================================
        //  数据加载
        // ============================================================

        private void PopulateTypeSelector()
        {
            _configTypeSelector.Clear();
            var types = ConfigReflectionCache.GetAllConfigTypes();

            if (types.Count == 0)
            {
                _configTypeSelector.AddItem("(未找到配置类 - 请重启编辑器)");
                _statusLabel.Text = "未在 Data/DataNew 中找到配置类。";
                return;
            }

            foreach (var typeInfo in types)
            {
                int idx = _configTypeSelector.GetItemCount();
                _configTypeSelector.AddItem(typeInfo.Name, idx);
                _configTypeSelector.SetItemMetadata(idx, typeInfo.Type.FullName ?? typeInfo.Type.Name);
            }
        }

        private void OnTypeSelected(long index)
        {
            if (index < 0) return;
            string? fullName = _configTypeSelector.GetItemMetadata((int)index).AsString();
            if (string.IsNullOrWhiteSpace(fullName)) return;

            var type = FindType(fullName);
            if (type == null) { _statusLabel.Text = $"类型未找到: {fullName}"; return; }

            _currentType = type;
            _currentSourceFile = ConfigReflectionCache.FindSourceFile(type);
            _selection.ClearSelection();
            LoadTypeData(type);
            RebuildGrid();
        }

        private void LoadTypeData(Type type)
        {
            _allProperties = ConfigReflectionCache.GetProperties(type);

            if (_currentSourceFile != null)
            {
                _instances = ConfigReflectionCache.GetInstances(type, _currentSourceFile);
                _comments = ConfigReflectionCache.GetComments(type, _currentSourceFile);
            }
            else
            {
                _instances = new List<ConfigReflectionCache.InstanceInfo>();
                _comments = new Dictionary<string, PropertyCommentInfo>();
            }

            RefreshBulkPropertySelector();
        }

        // ============================================================
        //  表格重建
        // ============================================================

        private void RebuildGrid()
        {
            foreach (var child in _grid.GetChildren())
                child.QueueFree();

            _imagePreview.ClearCache();

            _filteredProperties = GetFilteredProperties();
            if (_filteredProperties.Count == 0 || _instances.Count == 0)
            {
                SetContentVisibility(false);
                _statusLabel.Text = _filteredProperties.Count == 0 ? "没有匹配的属性" : "没有配置实例";
                return;
            }

            if (_layoutRowsAreInstances)
                BuildInstanceRowsLayout();
            else
                BuildPropertyRowsLayout();

            SetContentVisibility(true);
            string srcInfo = _currentSourceFile != null ? Path.GetFileName(_currentSourceFile) : "未找到";
            _statusLabel.Text = $"{_currentType?.Name} | {_filteredProperties.Count} 属性 | {_instances.Count} 实例 | 源文件: {srcInfo}";
        }

        private void SetContentVisibility(bool showGrid)
        {
            _emptyPanel.Visible = !showGrid;
            _scrollV.Visible = showGrid;
            _grid.Visible = showGrid;
        }

        // ============================================================
        //  行=实例布局（一整张表，属性=列横着，实例=行竖着）
        // ============================================================

        private void BuildInstanceRowsLayout()
        {
            // 列 = 1(实例名) + N(全部属性)
            _grid.Columns = 1 + _filteredProperties.Count;

            // ---- 表头行：两行（属性名 + 中文注释）----
            _grid.AddChild(MakeInstanceNameHeaderCell());
            for (int propIdx = 0; propIdx < _filteredProperties.Count; propIdx++)
                _grid.AddChild(MakePropertyHeaderCell(_filteredProperties[propIdx]));

            // ---- 实例行 ----
            for (int instIdx = 0; instIdx < _instances.Count; instIdx++)
            {
                var instance = _instances[instIdx];

                var nameCell = MakeRowNameCell(instance.Name, NAME_COL_WIDTH);
                nameCell.SetMeta("cell_col", (Variant)0);
                nameCell.SetMeta("cell_row", (Variant)instIdx);
                RegisterCellClick(nameCell, 0, instIdx);
                _grid.AddChild(nameCell);

                for (int propIdx = 0; propIdx < _filteredProperties.Count; propIdx++)
                {
                    int col = propIdx + 1;
                    var cellControl = CreateTypedCell(_filteredProperties[propIdx], instIdx, instance);
                    cellControl.SetMeta("cell_col", (Variant)col);
                    cellControl.SetMeta("cell_row", (Variant)instIdx);
                    RegisterCellClick(cellControl, col, instIdx);
                    _grid.AddChild(cellControl);
                }
            }
        }

        // ============================================================
        //  行=属性布局
        // ============================================================

        private void BuildPropertyRowsLayout()
        {
            // 列 = 属性名列 + 注释列 + 每个实例一列
            _grid.Columns = 2 + _instances.Count;

            // 表头行
            _grid.AddChild(MakeHeaderCell("属性", NAME_COL_WIDTH, HEADER_HEIGHT));
            _grid.AddChild(MakeHeaderCell("说明", 120, HEADER_HEIGHT));
            foreach (var instance in _instances)
                _grid.AddChild(MakeHeaderCell(instance.Name, CELL_MIN_WIDTH, HEADER_HEIGHT));

            // 属性行
            for (int propIdx = 0; propIdx < _filteredProperties.Count; propIdx++)
            {
                var prop = _filteredProperties[propIdx];
                string summary = GetPropertySummary(prop);

                _grid.AddChild(MakeBodyTextCell(prop.Name, NAME_COL_WIDTH, GetPropertyTooltip(prop), GetCellTextColor(prop)));
                _grid.AddChild(MakeBodyTextCell(summary, 120, GetPropertyTooltip(prop), new Color(0.60f, 0.75f, 0.85f)));

                for (int instIdx = 0; instIdx < _instances.Count; instIdx++)
                    _grid.AddChild(CreateTypedCell(prop, instIdx, _instances[instIdx]));
            }
        }

        // ============================================================
        //  单元格点击 & 多选
        // ============================================================

        private void RegisterCellClick(Control cell, int col, int row)
        {
            cell.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    var changed = _selection.HandleClick(col, row,
                        Input.IsKeyPressed(Key.Ctrl),
                        Input.IsKeyPressed(Key.Shift));
                    RefreshCellSelectionVisuals(changed);
                }
            };
        }

        private void RefreshCellSelectionVisuals(List<CellSelectionManager.CellCoord> changed)
        {
            foreach (var coord in changed)
                UpdateCellVisualRecursive(_grid, coord.Col, coord.Row);
        }

        private void UpdateCellVisualRecursive(Node parent, int col, int row)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is Control control && control.HasMeta("cell_col"))
                {
                    int cCol = control.GetMeta("cell_col").AsInt32();
                    int cRow = control.GetMeta("cell_row").AsInt32();
                    if (cCol == col && cRow == row)
                    {
                        bool selected = _selection.IsSelected(col, row);
                        // 查找最近的 PanelContainer 祖先（可能是自身或父级）
                        var target = control is PanelContainer pc ? pc : control.GetParent() as PanelContainer;
                        if (target != null)
                        {
                            target.RemoveThemeStyleboxOverride("panel");
                            target.AddThemeStyleboxOverride("panel",
                                selected ? GetSelectedStyle() : GetBodyStyle());
                        }
                    }
                }
                if (child.GetChildCount() > 0)
                    UpdateCellVisualRecursive(child, col, row);
            }
        }

        // ============================================================
        //  类型化单元格创建
        // ============================================================

        private Control CreateTypedCell(PropertyMetadata prop, int instanceIdx, ConfigReflectionCache.InstanceInfo instance)
        {
            object? rawValue = prop.PropertyInfo.GetValue(instance.Instance);
            string currentValue = prop.FormatValue(rawValue);

            Control cell;

            if (!prop.IsEditable)
            {
                cell = MakeBodyTextCell(
                    rawValue?.ToString() ?? "(空)",
                    CELL_MIN_WIDTH,
                    $"只读: {prop.ReadOnlyReason}\n{GetPropertyTooltip(prop)}",
                    GetCellTextColor(prop));
            }
            else if (prop.IsEnum && prop.EnumType != null)
            {
                var enumMembers = EnumCommentCache.GetMembers(prop.EnumType);
                if (prop.IsFlags)
                    cell = CreateFlagsEditor(prop, instanceIdx, instance, rawValue, enumMembers);
                else
                    cell = CreateEnumCell(prop, instanceIdx, instance, rawValue, enumMembers);
            }
            else if (prop.IsBool)
            {
                var checkBox = new CheckBox
                {
                    ButtonPressed = string.Equals(currentValue, "true", StringComparison.OrdinalIgnoreCase),
                    CustomMinimumSize = new Vector2(CELL_MIN_WIDTH, CELL_HEIGHT),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    TooltipText = GetPropertyTooltip(prop),
                };
                checkBox.Toggled += pressed => OnCellEdited(prop, instance, pressed ? "true" : "false");
                cell = WrapInCellPanel(checkBox, CELL_HEIGHT);
            }
            else if (prop.IsNumeric)
            {
                cell = WrapInCellPanel(CreateNumericEditor(prop, instance, currentValue), CELL_HEIGHT);
            }
            else if (prop.IsString)
            {
                if (prop.IsPathString)
                    cell = CreatePathCell(prop, instance, currentValue);
                else
                    cell = WrapInCellPanel(CreateStringEditor(prop, instance, currentValue), CELL_HEIGHT);
            }
            else
            {
                cell = MakeBodyTextCell(
                    rawValue?.ToString() ?? "(空)",
                    CELL_MIN_WIDTH,
                    $"复杂类型暂不支持\n{GetPropertyTooltip(prop)}",
                    new Color(0.72f, 0.72f, 0.72f));
            }

            return cell;
        }

        private Control WrapInCellPanel(Control inner, float height)
        {
            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(CELL_MIN_WIDTH, height),
            };
            panel.AddThemeStyleboxOverride("panel", GetBodyStyle());
            panel.AddChild(inner);
            return panel;
        }

        // ============================================================
        //  枚举单元格：两行显示（英文名 + 中文注释），点击弹出选择
        // ============================================================

        private Control CreateEnumCell(
            PropertyMetadata prop,
            int instanceIdx,
            ConfigReflectionCache.InstanceInfo instance,
            object? rawValue,
            EnumCommentCache.EnumMemberInfo[] enumMembers)
        {
            if (enumMembers.Length == 0 && prop.EnumType != null)
                enumMembers = BuildEnumMembersFromReflection(prop.EnumType);

            string currentName = rawValue?.ToString() ?? "";
            string currentComment = "";
            foreach (var m in enumMembers)
            {
                if (m.Name == currentName) { currentComment = m.Comment; break; }
            }

            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(CELL_MIN_WIDTH, ENUM_CELL_HEIGHT),
                TooltipText = GetPropertyTooltip(prop),
            };
            panel.AddThemeStyleboxOverride("panel", GetBodyStyle());

            var vbox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };

            // 第一行：英文名
            var nameLabel = new Label
            {
                Text = currentName,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.80f, 0.65f));
            nameLabel.AddThemeFontSizeOverride("font_size", 12);

            // 第二行：中文注释
            var commentLabel = new Label
            {
                Text = currentComment,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            commentLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.75f, 0.85f));
            commentLabel.AddThemeFontSizeOverride("font_size", 10);

            vbox.AddChild(nameLabel);
            vbox.AddChild(commentLabel);
            panel.AddChild(vbox);

            // 点击弹出选择菜单
            panel.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    ShowEnumPopup(panel, prop, instance, enumMembers, nameLabel, commentLabel);
                }
            };

            return panel;
        }

        private void ShowEnumPopup(
            Control cell,
            PropertyMetadata prop,
            ConfigReflectionCache.InstanceInfo instance,
            EnumCommentCache.EnumMemberInfo[] members,
            Label nameLabel,
            Label commentLabel)
        {
            // --- 自定义 PopupPanel：两列布局（英文名 | 中文注释）---
            var popup = new PopupPanel();
            popup.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.13f, 0.15f, 0.19f),
                BorderColor = new Color(0.30f, 0.35f, 0.45f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                ContentMarginLeft = 2,
                ContentMarginRight = 2,
                ContentMarginTop = 2,
                ContentMarginBottom = 2,
            });

            var list = new VBoxContainer();

            // 行样式
            var normalBg = new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0),
                ContentMarginLeft = 4,
                ContentMarginRight = 4,
                ContentMarginTop = 2,
                ContentMarginBottom = 2,
            };
            var hoverBg = new StyleBoxFlat
            {
                BgColor = new Color(0.22f, 0.32f, 0.52f),
                ContentMarginLeft = 4,
                ContentMarginRight = 4,
                ContentMarginTop = 2,
                ContentMarginBottom = 2,
            };

            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];

                var row = new PanelContainer
                {
                    CustomMinimumSize = new Vector2(250, 26),
                };
                row.AddThemeStyleboxOverride("panel", normalBg);

                var hbox = new HBoxContainer();

                // 左：英文名
                var enLabel = new Label
                {
                    Text = member.Name,
                    CustomMinimumSize = new Vector2(110, 0),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                enLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.80f, 0.65f));
                enLabel.AddThemeFontSizeOverride("font_size", 12);

                // 分隔线
                var sep = new VSeparator();

                // 右：中文注释
                var zhLabel = new Label
                {
                    Text = member.Comment,
                    CustomMinimumSize = new Vector2(110, 0),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                zhLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.75f, 0.85f));
                zhLabel.AddThemeFontSizeOverride("font_size", 12);

                hbox.AddChild(enLabel);
                hbox.AddChild(sep);
                hbox.AddChild(zhLabel);
                row.AddChild(hbox);

                // 点击选择
                int capturedIdx = i;
                row.GuiInput += @event =>
                {
                    if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    {
                        var m = members[capturedIdx];
                        nameLabel.Text = m.Name;
                        commentLabel.Text = m.Comment;
                        OnCellEdited(prop, instance, m.Name);
                        popup.Hide();
                    }
                };

                // 鼠标悬浮高亮
                row.MouseEntered += () => row.AddThemeStyleboxOverride("panel", hoverBg);
                row.MouseExited += () => row.AddThemeStyleboxOverride("panel", normalBg);

                list.AddChild(row);
            }

            popup.AddChild(list);

            // 添加到面板顶层（非 ScrollContainer 内的 cell），避免坐标偏移
            AddChild(popup);

            // 使用全局坐标定位到单元格正下方
            var gRect = cell.GetGlobalRect();
            var contentMinSize = list.GetCombinedMinimumSize();
            popup.Popup(new Rect2I(
                (int)gRect.Position.X,
                (int)gRect.End.Y,
                Math.Max((int)contentMinSize.X + 8, 250),
                Math.Min((int)contentMinSize.Y + 8, 400)));

            popup.PopupHide += () => popup.QueueFree();
        }

        // ============================================================
        //  Flags 枚举编辑器
        // ============================================================

        private Control CreateFlagsEditor(
            PropertyMetadata prop,
            int instanceIdx,
            ConfigReflectionCache.InstanceInfo instance,
            object? rawValue,
            EnumCommentCache.EnumMemberInfo[] enumMembers)
        {
            if (prop.EnumType == null)
                return MakeBodyTextCell(rawValue?.ToString() ?? "(空)", CELL_MIN_WIDTH, GetPropertyTooltip(prop), new Color(0.72f, 0.72f, 0.72f));

            if (enumMembers.Length == 0)
                enumMembers = BuildEnumMembersFromReflection(prop.EnumType);

            var supportedMembers = enumMembers
                .Where(m => m.Value == 0 || IsSingleFlagValue(m.Value))
                .OrderBy(m => m.Value)
                .ToArray();

            int currentBits = rawValue != null ? Convert.ToInt32(rawValue) : 0;
            string zeroName = supportedMembers.FirstOrDefault(m => m.Value == 0).Name;
            if (string.IsNullOrWhiteSpace(zeroName)) zeroName = "None";

            var button = new MenuButton
            {
                Text = BuildFlagsButtonText(supportedMembers, currentBits, zeroName),
                CustomMinimumSize = new Vector2(CELL_MIN_WIDTH, CELL_HEIGHT),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = GetPropertyTooltip(prop),
            };

            var popup = button.GetPopup();
            for (int i = 0; i < supportedMembers.Length; i++)
            {
                var member = supportedMembers[i];
                string itemText = string.IsNullOrWhiteSpace(member.Comment)
                    ? member.Name
                    : $"{member.Name} ({member.Comment})";
                popup.AddCheckItem(itemText, i);
                bool isChecked = member.Value != 0 && (currentBits & member.Value) == member.Value;
                popup.SetItemChecked(i, isChecked);
            }

            popup.IdPressed += id =>
            {
                if (id < 0 || id >= supportedMembers.Length) return;
                int index = (int)id;
                var member = supportedMembers[index];

                if (member.Value == 0)
                {
                    for (int i = 0; i < supportedMembers.Length; i++)
                        popup.SetItemChecked(i, false);
                    currentBits = 0;
                }
                else
                {
                    bool newCheckedState = !popup.IsItemChecked(index);
                    popup.SetItemChecked(index, newCheckedState);
                    if (newCheckedState) currentBits |= member.Value;
                    else currentBits &= ~member.Value;
                }

                button.Text = BuildFlagsButtonText(supportedMembers, currentBits, zeroName);
                OnCellEdited(prop, instance, BuildFlagsEnumText(supportedMembers, currentBits, zeroName));
            };

            return WrapInCellPanel(button, CELL_HEIGHT);
        }

        // ============================================================
        //  路径单元格（缩略图在左，路径在右）
        // ============================================================

        private Control CreatePathCell(PropertyMetadata prop, ConfigReflectionCache.InstanceInfo instance, string currentValue)
        {
            var hbox = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(CELL_MIN_WIDTH, CELL_HEIGHT),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };

            // 缩略图在左
            var thumbnail = new TextureRect
            {
                CustomMinimumSize = new Vector2(THUMBNAIL_SIZE, THUMBNAIL_SIZE),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Visible = false,
            };
            hbox.AddChild(thumbnail);

            // 路径编辑框在右
            var pathEditor = new PathLineEdit(currentValue, newText => OnCellEdited(prop, instance, newText))
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            hbox.AddChild(pathEditor);

            // 加载预览
            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                var tex = _imagePreview.GetOrLoad(currentValue);
                if (tex != null) { thumbnail.Texture = tex; thumbnail.Visible = true; }
            }

            pathEditor.TextChanged += newPath =>
            {
                thumbnail.Visible = false;
                thumbnail.Texture = null;
                if (!string.IsNullOrWhiteSpace(newPath))
                {
                    var tex = _imagePreview.GetOrLoad(newPath);
                    if (tex != null) { thumbnail.Texture = tex; thumbnail.Visible = true; }
                }
            };

            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", GetBodyStyle());
            panel.AddChild(hbox);
            return panel;
        }

        // ============================================================
        //  数值 / 字符串编辑器
        // ============================================================

        private Control CreateNumericEditor(PropertyMetadata prop, ConfigReflectionCache.InstanceInfo instance, string currentValue)
        {
            var spin = new SpinBox
            {
                Value = double.TryParse(currentValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0d,
                MinValue = prop.MinNumericValue,
                MaxValue = prop.MaxNumericValue,
                Step = prop.PropertyType == typeof(int) ? 1 : 0.1,
                CustomMinimumSize = new Vector2(CELL_MIN_WIDTH, CELL_HEIGHT),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Prefix = "  ",
                TooltipText = GetPropertyTooltip(prop),
            };
            spin.ValueChanged += value =>
            {
                string text = prop.PropertyType == typeof(int)
                    ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                    : value.ToString("G", CultureInfo.InvariantCulture);
                OnCellEdited(prop, instance, text);
            };
            return spin;
        }

        private Control CreateStringEditor(PropertyMetadata prop, ConfigReflectionCache.InstanceInfo instance, string currentValue)
        {
            var textCell = new LineEdit
            {
                Text = currentValue,
                CustomMinimumSize = new Vector2(CELL_MIN_WIDTH, CELL_HEIGHT),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = GetPropertyTooltip(prop),
            };
            textCell.AddThemeColorOverride("font_color", GetCellTextColor(prop));
            textCell.TextChanged += newText => OnCellEdited(prop, instance, newText);
            return textCell;
        }

        // ============================================================
        //  撤销/重做
        // ============================================================

        public void SetCellValue(int instanceIdx, int propIdx, string valueText)
        {
            TrySetCellValue(instanceIdx, propIdx, valueText, false);
        }

        private bool TrySetCellValue(int instanceIdx, int propIdx, string valueText, bool printDiagnostics)
        {
            if (instanceIdx < 0 || instanceIdx >= _instances.Count) return false;
            if (propIdx < 0 || propIdx >= _filteredProperties.Count) return false;

            var prop = _filteredProperties[propIdx];
            if (!prop.IsEditable)
            {
                if (printDiagnostics)
                    GD.Print($"[DataConfigEditor] 编辑被拒绝: {prop.Name} 只读，原因={prop.ReadOnlyReason}");
                return false;
            }

            var instance = _instances[instanceIdx];
            try
            {
                object? beforeRaw = prop.PropertyInfo.GetValue(instance.Instance);
                string beforeValue = prop.FormatValue(beforeRaw);
                string normalizedValue = prop.IsPathString ? PathLineEdit.NormalizePath(valueText) : valueText;
                object? converted = ConvertValue(normalizedValue, prop.PropertyType);
                prop.PropertyInfo.SetValue(instance.Instance, converted);
                object? afterRaw = prop.PropertyInfo.GetValue(instance.Instance);
                string afterValue = prop.FormatValue(afterRaw);

                if (printDiagnostics)
                {
                    GD.Print($"[DataConfigEditor] 编辑写入内存: {instance.Name}.{prop.Name}, "
                        + $"type={prop.PropertyType.Name}, input={DescribeEditorValue(valueText)}, "
                        + $"normalized={DescribeEditorValue(normalizedValue)}, before={DescribeEditorValue(beforeValue)}, "
                        + $"after={DescribeEditorValue(afterValue)}");
                }

                return true;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[DataConfigEditor] SetCellValue 失败: {instance.Name}.{prop.Name}, input={DescribeEditorValue(valueText)}, error={e}");
                return false;
            }
        }

        private void OnCellEdited(PropertyMetadata prop, ConfigReflectionCache.InstanceInfo instance, string newValue)
        {
            if (!prop.IsEditable)
            {
                _detailLabel.Text = $"[只读] {prop.Name}: {prop.ReadOnlyReason}";
                return;
            }

            int instanceIdx = _instances.IndexOf(instance);
            int propIdx = _filteredProperties.IndexOf(prop);

            object? oldRawValue = prop.PropertyInfo.GetValue(instance.Instance);
            string oldValue = prop.FormatValue(oldRawValue);

            if (!TrySetCellValue(instanceIdx, propIdx, newValue, true))
                return;

            var undoMgr = EditorInterface.Singleton.GetEditorUndoRedo();
            undoMgr.CreateAction($"修改 {instance.Name}.{prop.Name}");
            undoMgr.AddDoMethod(this, MethodNameSetCellValue, instanceIdx, propIdx, newValue);
            undoMgr.AddUndoMethod(this, MethodNameSetCellValue, instanceIdx, propIdx, oldValue);
            // 当前编辑已由 C# 直接写入内存对象；UndoRedo 只登记撤销/重做步骤，避免依赖 Godot 字符串反射来让数据生效。
            undoMgr.CommitAction(false);

            _detailLabel.Text = $"[编辑] {instance.Name}.{prop.Name} = {newValue}";
            SaveSingleCellToSource(instance, prop, prop.IsPathString ? PathLineEdit.NormalizePath(newValue) : newValue, true);
        }

        // ============================================================
        //  UI 辅助 — 表头 / 属性列表头
        // ============================================================

        /// <summary>
        /// 普通表头格（单行文本）
        /// </summary>
        private Control MakeHeaderCell(string text, float minWidth, float height)
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(minWidth, height) };
            panel.AddThemeStyleboxOverride("panel", GetHeaderStyle());

            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 1.0f));
            label.AddThemeFontSizeOverride("font_size", 12);
            panel.AddChild(label);
            return panel;
        }

        /// <summary>
        /// 实例名列的表头格
        /// </summary>
        private Control MakeInstanceNameHeaderCell()
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(NAME_COL_WIDTH, HEADER_HEIGHT) };
            panel.AddThemeStyleboxOverride("panel", GetHeaderStyle());

            var label = new Label
            {
                Text = "实例",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 1.0f));
            label.AddThemeFontSizeOverride("font_size", 12);
            panel.AddChild(label);
            return panel;
        }

        /// <summary>
        /// 属性列表头格（两行：属性名 + 中文注释）
        /// </summary>
        private Control MakePropertyHeaderCell(PropertyMetadata prop)
        {
            string summary = GetPropertySummary(prop);

            var panel = new PanelContainer { CustomMinimumSize = new Vector2(CELL_MIN_WIDTH, HEADER_HEIGHT) };
            panel.AddThemeStyleboxOverride("panel", GetHeaderStyle());
            panel.TooltipText = GetPropertyTooltip(prop);

            var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            // 第一行：属性名
            var nameLabel = new Label
            {
                Text = prop.Name,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            nameLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 1.0f));
            nameLabel.AddThemeFontSizeOverride("font_size", 11);

            // 第二行：中文注释
            var commentLabel = new Label
            {
                Text = summary,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            commentLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.75f, 0.85f));
            commentLabel.AddThemeFontSizeOverride("font_size", 10);

            vbox.AddChild(nameLabel);
            vbox.AddChild(commentLabel);
            panel.AddChild(vbox);

            return panel;
        }

        /// <summary>
        /// 实例行左侧的名称格
        /// </summary>
        private Control MakeRowNameCell(string text, float minWidth)
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(minWidth, CELL_HEIGHT) };
            panel.AddThemeStyleboxOverride("panel", GetRowNameStyle());

            var label = new Label
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.90f, 0.95f));
            panel.AddChild(label);
            return panel;
        }

        /// <summary>
        /// 普通文本体格
        /// </summary>
        private Control MakeBodyTextCell(string text, float minWidth, string tooltip, Color fontColor)
        {
            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(minWidth, CELL_HEIGHT),
                TooltipText = tooltip,
            };
            panel.AddThemeStyleboxOverride("panel", GetBodyStyle());

            var label = new Label
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                TooltipText = tooltip,
            };
            label.AddThemeColorOverride("font_color", fontColor);
            label.AddThemeFontSizeOverride("font_size", 11);
            panel.AddChild(label);
            return panel;
        }

        private static Color GetCellTextColor(PropertyMetadata prop)
        {
            if (!prop.IsEditable) return new Color(0.55f, 0.58f, 0.62f);
            if (prop.IsNumeric) return new Color(0.60f, 0.90f, 0.70f);
            if (prop.IsBool) return new Color(0.90f, 0.75f, 0.50f);
            if (prop.IsString) return new Color(0.70f, 0.80f, 0.95f);
            if (prop.IsEnum) return new Color(0.95f, 0.80f, 0.65f);
            return new Color(0.90f, 0.90f, 0.90f);
        }

        // ============================================================
        //  Flags 辅助
        // ============================================================

        private static EnumCommentCache.EnumMemberInfo[] BuildEnumMembersFromReflection(Type enumType)
        {
            var names = Enum.GetNames(enumType);
            var values = Enum.GetValues(enumType);
            var members = new EnumCommentCache.EnumMemberInfo[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                members[i] = new EnumCommentCache.EnumMemberInfo
                {
                    Name = names[i],
                    Comment = "",
                    IsFlags = enumType.GetCustomAttribute<FlagsAttribute>() != null,
                    Value = Convert.ToInt32(values.GetValue(i)),
                };
            }
            return members;
        }

        private static bool IsSingleFlagValue(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static string BuildFlagsButtonText(EnumCommentCache.EnumMemberInfo[] members, int currentBits, string zeroName)
        {
            var names = members.Where(m => m.Value != 0 && (currentBits & m.Value) == m.Value).Select(m => m.Name).ToArray();
            return names.Length == 0 ? zeroName : string.Join(" | ", names);
        }

        private static string BuildFlagsEnumText(EnumCommentCache.EnumMemberInfo[] members, int currentBits, string zeroName)
        {
            var names = members.Where(m => m.Value != 0 && (currentBits & m.Value) == m.Value).Select(m => m.Name).ToArray();
            return names.Length == 0 ? zeroName : string.Join(", ", names);
        }

        // ============================================================
        //  批量编辑
        // ============================================================

        private void RefreshBulkPropertySelector()
        {
            _bulkPropertySelector.Clear();

            if (_allProperties.Count == 0)
            {
                _bulkPropertySelector.Disabled = true;
                _bulkValueInput.Editable = false;
                _bulkApplyBtn.Disabled = true;
                return;
            }

            foreach (var prop in _allProperties)
            {
                int index = _bulkPropertySelector.ItemCount;
                string label = prop.IsEditable
                    ? $"{prop.DisplayName} ({prop.Name})"
                    : $"[只读] {prop.DisplayName} ({prop.Name})";
                _bulkPropertySelector.AddItem(label, index);
                _bulkPropertySelector.SetItemMetadata(index, prop.Name);
                _bulkPropertySelector.SetItemTooltip(index, GetPropertyTooltip(prop));
                _bulkPropertySelector.SetItemDisabled(index, !prop.IsEditable);
            }

            _bulkPropertySelector.Disabled = false;
            _bulkPropertySelector.Select(0);
            UpdateBulkEditorHint(GetSelectedBulkProperty());
        }

        private void OnBulkPropertySelected(long index)
        {
            UpdateBulkEditorHint(GetSelectedBulkProperty());
        }

        private void UpdateBulkEditorHint(PropertyMetadata? prop)
        {
            if (prop == null)
            {
                _bulkValueInput.Editable = false;
                _bulkApplyBtn.Disabled = true;
                return;
            }

            _bulkValueInput.Editable = prop.IsEditable;
            _bulkApplyBtn.Disabled = !_bulkValueInput.Editable;
            _bulkValueInput.Text = "";

            if (!prop.IsEditable)
                _bulkValueInput.PlaceholderText = prop.ReadOnlyReason;
            else if (prop.IsEnum && prop.EnumType != null)
                _bulkValueInput.PlaceholderText = $"输入枚举名: {string.Join(", ", Enum.GetNames(prop.EnumType))}";
            else if (prop.IsBool)
                _bulkValueInput.PlaceholderText = "true / false";
            else if (prop.IsPathString)
                _bulkValueInput.PlaceholderText = "res://...";
            else
                _bulkValueInput.PlaceholderText = "输入批量值";
        }

        private void OnApplyBulkEdit()
        {
            var prop = GetSelectedBulkProperty();
            if (prop == null) { _detailLabel.Text = "[错误] 未选择批量属性"; return; }
            if (_instances.Count == 0) { _detailLabel.Text = "[错误] 当前没有可编辑实例"; return; }
            if (!prop.IsEditable) { _detailLabel.Text = $"[只读] {prop.Name}: {prop.ReadOnlyReason}"; return; }

            string rawText = _bulkValueInput.Text.Trim();
            try
            {
                string normalizedText = prop.IsPathString ? PathLineEdit.NormalizePath(rawText) : rawText;
                object? converted = ConvertValue(normalizedText, prop.PropertyType);

                var targetInstances = _selection.HasSelection && _selection.IsSingleColumnSelection()
                    ? _selection.GetSelectedRows().Select(r => _instances[r]).ToList()
                    : _instances;

                int savedCount = 0;
                foreach (var inst in targetInstances)
                {
                    prop.PropertyInfo.SetValue(inst.Instance, converted);
                    if (SaveSingleCellToSource(inst, prop, normalizedText, false))
                        savedCount++;
                }

                _detailLabel.Text = $"[批量] 已将 {prop.Name} 应用到 {targetInstances.Count} 个实例，写入 {savedCount} 个源码单元格";
                RebuildGrid();
            }
            catch (Exception e)
            {
                _detailLabel.Text = $"[错误] 批量修改 {prop.Name} 失败: {e.Message}";
            }
        }

        private PropertyMetadata? GetSelectedBulkProperty()
        {
            if (_bulkPropertySelector.ItemCount == 0 || _bulkPropertySelector.Selected < 0)
                return null;
            string propName = _bulkPropertySelector.GetItemMetadata(_bulkPropertySelector.Selected).AsString();
            return _allProperties.FirstOrDefault(p => p.Name == propName);
        }

        // ============================================================
        //  搜索（防抖）
        // ============================================================

        private void OnSearchChanged(string newText)
        {
            _pendingSearch = newText.Trim();
            _searchDebounceTimer = SearchDebounceInterval;
        }

        private void ApplySearch()
        {
            if (_currentType != null)
                RebuildGrid();
        }

        // ============================================================
        //  工具栏操作
        // ============================================================

        private void ToggleLayout()
        {
            _layoutRowsAreInstances = !_layoutRowsAreInstances;
            _toggleLayoutBtn.Text = _layoutRowsAreInstances ? "布局: 行=实例" : "布局: 行=属性";
            if (_currentType != null)
                RebuildGrid();
        }

        private void OnRefresh()
        {
            ConfigReflectionCache.ClearCache();
            EnumCommentCache.ClearCache();
            if (_currentType != null)
            {
                LoadTypeData(_currentType);
                RebuildGrid();
            }
            _statusLabel.Text = "已刷新";
        }

        private void OnSave()
        {
            if (_dirtyCells.Count == 0)
            {
                _statusLabel.Text = "当前没有待写入的 C# 单元格；编辑后会实时写入源码";
                return;
            }

            int savedCount = 0;
            foreach (var dirty in _dirtyCells.Values.ToList())
            {
                if (SaveSingleCellToSource(dirty.Instance, dirty.Property, dirty.ValueText, true))
                    savedCount++;
            }

            _statusLabel.Text = _dirtyCells.Count == 0
                ? $"已重试写入 {savedCount} 个 C# 单元格"
                : $"仍有 {_dirtyCells.Count} 个 C# 单元格未写入，请看 Output 的 [DataConfigEditor] 单元格写回诊断";
        }

        private bool SaveSingleCellToSource(
            ConfigReflectionCache.InstanceInfo instance,
            PropertyMetadata prop,
            string valueText,
            bool printDiagnostics)
        {
            string key = $"{instance.Name}.{prop.Name}";
            if (_currentSourceFile == null)
            {
                MarkDirtyCell(key, instance, prop, valueText);
                _statusLabel.Text = $"未找到源文件，无法实时写入 C#：{key}";
                GD.PrintErr($"[DataConfigEditor] 单元格写回失败: {key}, 当前类型={_currentType?.FullName ?? "(null)"} 未找到源码文件");
                return false;
            }

            var result = CsFileWriter.WriteSingleChangeWithDiagnostics(
                _currentSourceFile,
                instance,
                prop,
                valueText,
                verbose: printDiagnostics);

            if (printDiagnostics)
                PrintSaveDiagnostics("单元格写回", result);

            bool failed = !result.FileExists
                || result.InitializersMissing > 0
                || result.PropertiesUnsupported > 0
                || result.PropertiesComplexSkipped > 0;
            if (failed)
            {
                MarkDirtyCell(key, instance, prop, valueText);
                _statusLabel.Text = $"实时写入 C# 失败：{key}；请看 Output 诊断";
                return false;
            }

            _dirtyCells.Remove(key);
            _modified = _dirtyCells.Count > 0;
            _saveBtn.Disabled = !_modified;
            string action = result.PropertiesWritten > 0 ? "已实时写入" : "源码已是目标值";
            _statusLabel.Text = $"{action} C#：{key} → {Path.GetFileName(_currentSourceFile)}";
            return true;
        }

        private void MarkDirtyCell(
            string key,
            ConfigReflectionCache.InstanceInfo instance,
            PropertyMetadata prop,
            string valueText)
        {
            _dirtyCells[key] = new DirtyCellInfo
            {
                Instance = instance,
                Property = prop,
                ValueText = valueText,
            };
            _modified = true;
            _saveBtn.Disabled = false;
        }

        private static void PrintSaveDiagnostics(string title, CsFileWriter.WriteAllChangesResult result)
        {
            GD.Print($"[DataConfigEditor] {title}诊断汇总: {result.ToSummary()}");
            foreach (string message in result.Messages)
                GD.Print($"[DataConfigEditor] {title}诊断: {message}");
        }

        private static string DescribeEditorValue(string value)
        {
            const int maxLength = 160;
            string escaped = value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            return escaped.Length <= maxLength ? $"`{escaped}`" : $"`{escaped[..maxLength]}...`";
        }

        // ============================================================
        //  属性信息辅助
        // ============================================================

        private List<PropertyMetadata> GetFilteredProperties()
        {
            if (string.IsNullOrWhiteSpace(_pendingSearch))
                return _allProperties;

            return _allProperties.Where(prop =>
            {
                if (prop.Name.Contains(_pendingSearch, StringComparison.OrdinalIgnoreCase)) return true;
                if (prop.DisplayName.Contains(_pendingSearch, StringComparison.OrdinalIgnoreCase)) return true;
                if (prop.DataKeyName.Contains(_pendingSearch, StringComparison.OrdinalIgnoreCase)) return true;
                if (prop.DataDescription.Contains(_pendingSearch, StringComparison.OrdinalIgnoreCase)) return true;
                if (_comments.TryGetValue(prop.Name, out var comment))
                {
                    if (comment.Summary.Contains(_pendingSearch, StringComparison.OrdinalIgnoreCase)) return true;
                    if (comment.Group.Contains(_pendingSearch, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }).ToList();
        }

        private string GetPropertyTooltip(PropertyMetadata prop)
        {
            string summary = GetPropertySummary(prop);
            string group = GetPropertyGroup(prop);
            var lines = new List<string> { $"字段: {prop.Name}" };
            if (!prop.IsEditable) lines.Add($"只读: {prop.ReadOnlyReason}");
            if (!string.IsNullOrWhiteSpace(prop.DataKeyName)) lines.Add($"DataKey: {prop.DataKeyName}");
            if (!string.IsNullOrWhiteSpace(group)) lines.Add($"分组: {group}");
            if (!string.IsNullOrWhiteSpace(summary)) lines.Add($"注释: {summary}");
            return string.Join("\n", lines);
        }

        private string GetPropertySummary(PropertyMetadata prop)
        {
            // 优先：源码 <summary> 注释
            if (_comments.TryGetValue(prop.Name, out var info) && !string.IsNullOrWhiteSpace(info.Summary))
                return info.Summary;
            // 回退：DataMeta 描述
            if (!string.IsNullOrWhiteSpace(prop.DataDescription))
                return prop.DataDescription;
            return "";
        }

        private string GetPropertyGroup(PropertyMetadata prop)
        {
            return _comments.TryGetValue(prop.Name, out var info) ? info.Group : "";
        }

        // ============================================================
        //  类型转换
        // ============================================================

        private static object? ConvertValue(string text, Type targetType)
        {
            Type actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (actualType == typeof(string)) return text;
            if (actualType == typeof(int))
                return int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out int i) ? i : 0;
            if (actualType == typeof(float))
                return float.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out float f) ? f : 0f;
            if (actualType == typeof(double))
                return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0d;
            if (actualType == typeof(bool))
                return bool.TryParse(text, out bool b) ? b : string.Equals(text, "1", StringComparison.OrdinalIgnoreCase);
            if (actualType.IsEnum)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    var vals = Enum.GetValues(actualType);
                    return vals.Length > 0 ? vals.GetValue(0) : Activator.CreateInstance(actualType);
                }
                try { return Enum.Parse(actualType, text, ignoreCase: true); }
                catch
                {
                    if (int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out int n))
                        return Enum.ToObject(actualType, n);
                    throw;
                }
            }
            return text;
        }

        private Type? FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.FullName == fullName);
        }
    }
}
#endif
