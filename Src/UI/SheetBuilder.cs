using unvell.ReoGrid;
using unvell.ReoGrid.CellTypes;
using DataConfigEditor.Core;
using DataConfigEditor.Parsing;

namespace DataConfigEditor.UI;

/// <summary>
/// ReoGrid 工作表构建器（纯源码解析版，无反射）
/// </summary>
public class SheetBuilder
{
    private readonly EnumCommentCache _enumCache;
    private readonly string _projectRoot;

    private readonly WorksheetRangeStyle _headerStyle = new()
    {
        Flag = PlainStyleFlag.BackColor | PlainStyleFlag.TextColor
             | PlainStyleFlag.FontName | PlainStyleFlag.FontSize | PlainStyleFlag.FontStyleBold,
        BackColor = Color.FromArgb(45, 45, 48),
        TextColor = Color.White,
        FontName = "Microsoft YaHei UI",
        FontSize = 9.5f,
        Bold = true,
    };

    private readonly WorksheetRangeStyle _dataStyle = new()
    {
        Flag = PlainStyleFlag.FontName | PlainStyleFlag.FontSize,
        FontName = "Microsoft YaHei UI",
        FontSize = 9f,
    };

    private readonly WorksheetRangeStyle _instanceNameStyle = new()
    {
        Flag = PlainStyleFlag.BackColor | PlainStyleFlag.FontName | PlainStyleFlag.FontSize | PlainStyleFlag.FontStyleBold,
        BackColor = Color.FromArgb(240, 240, 240),
        FontName = "Microsoft YaHei UI",
        FontSize = 9f,
        Bold = true,
    };

    public SheetBuilder(EnumCommentCache enumCache, string projectRoot)
    {
        _enumCache = enumCache;
        _projectRoot = projectRoot;
    }

    /// <summary>
    /// 构建整个工作表
    /// </summary>
    public void BuildSheet(
        Worksheet sheet,
        ConfigTypeInfo typeInfo,
        List<PropertyMetadata> properties,
        List<InstanceInfo> instances,
        string searchFilter = "")
    {
        sheet.Reset();

        // 过滤属性
        var filteredProps = string.IsNullOrWhiteSpace(searchFilter)
            ? properties
            : properties.Where(p =>
                p.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)
                || p.Summary.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)
                || p.FriendlyTypeName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase)
                || p.Group.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filteredProps.Count == 0 || instances.Count == 0)
        {
            sheet[0, 0] = "无数据";
            return;
        }

        // 按分组组织属性
        var groups = BuildGroupedProperties(filteredProps);

        int totalColumns = 1 + filteredProps.Count;

        sheet.Columns = totalColumns;
        sheet.Rows = 1 + instances.Count + 10;

        // 设置列宽
        sheet.SetColumnsWidth(0, 1, 120);
        for (int i = 0; i < filteredProps.Count; i++)
        {
            int width = GetColumnWidth(filteredProps[i]);
            sheet.SetColumnsWidth(i + 1, 1, (ushort)width);
        }

        // === 构建表头 ===
        sheet[0, 0] = "实例名";
        sheet.SetRangeStyles(0, 0, 1, 1, _headerStyle);

        for (int i = 0; i < filteredProps.Count; i++)
        {
            int col = i + 1;
            string headerText = string.IsNullOrEmpty(filteredProps[i].Summary)
                ? filteredProps[i].Name
                : $"{filteredProps[i].Name}\n{filteredProps[i].Summary}";
            sheet[0, col] = headerText;
            sheet.SetRangeStyles(0, col, 1, 1, _headerStyle);
        }

        // 冻结表头行和实例名列
        sheet.FreezeToCell(1, 1);

        // === 构建数据行 ===
        for (int instIdx = 0; instIdx < instances.Count; instIdx++)
        {
            var inst = instances[instIdx];
            int row = 1 + instIdx;

            // 实例名
            sheet[row, 0] = inst.Name;
            sheet.SetRangeStyles(row, 0, 1, 1, _instanceNameStyle);

            // 属性值
            for (int propIdx = 0; propIdx < filteredProps.Count; propIdx++)
            {
                var prop = filteredProps[propIdx];
                int col = propIdx + 1;

                string val = inst.Values.TryGetValue(prop.Name, out var v) ? v : prop.DefaultValue;
                SetCellValue(sheet, row, col, prop, val);
                sheet.SetRangeStyles(row, col, 1, 1, _dataStyle);
            }
        }

        // 按分组创建列大纲
        int propColOffset = 1;
        foreach (var group in groups)
        {
            if (group.Properties.Count == 0) continue;
            int startCol = propColOffset;
            int count = group.Properties.Count;

            if (!string.IsNullOrEmpty(group.Name) && count > 0)
            {
                try { sheet.AddOutline(RowOrColumn.Column, startCol, count); }
                catch { }
            }

            propColOffset += count;
        }

        // 设置行高
        sheet.SetRowsHeight(0, 1, 40);
        if (instances.Count > 0)
            sheet.SetRowsHeight(1, (ushort)instances.Count, 26);

        sheet.Name = typeInfo.ClassName;
    }

    private void SetCellValue(Worksheet sheet, int row, int col, PropertyMetadata prop, string val)
    {
        if (prop.IsEnum)
        {
            // 枚举下拉
            var members = _enumCache.GetMembers(prop.TypeName);
            var items = new List<object>();
            foreach (var m in members)
            {
                string display = string.IsNullOrEmpty(m.Comment)
                    ? m.Name
                    : $"{m.Name} ({m.Comment})";
                items.Add(display);
            }

            var cell = sheet.Cells[row, col];
            var dropdown = new DropdownListCell(items);

            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].Name == val)
                {
                    dropdown.SelectedIndex = i;
                    break;
                }
            }

            cell.Body = dropdown;
            cell.Data = val;
        }
        else
        {
            sheet[row, col] = val;
        }
    }

    /// <summary>
    /// 从 ReoGrid 读回修改后的值到 InstanceInfo.Values 字典
    /// </summary>
    public void ReadBackValues(
        Worksheet sheet,
        ConfigTypeInfo typeInfo,
        List<PropertyMetadata> properties,
        List<InstanceInfo> instances)
    {
        for (int instIdx = 0; instIdx < instances.Count; instIdx++)
        {
            var inst = instances[instIdx];
            int row = 1 + instIdx;

            for (int propIdx = 0; propIdx < properties.Count; propIdx++)
            {
                var prop = properties[propIdx];
                int col = propIdx + 1;

                var cellData = sheet.Cells[row, col].Data;
                if (cellData == null) continue;

                string strVal = cellData.ToString() ?? "";
                inst.Values[prop.Name] = strVal;
            }
        }
    }

    private List<PropertyGroup> BuildGroupedProperties(List<PropertyMetadata> properties)
    {
        var groupMap = new Dictionary<string, List<PropertyMetadata>>();
        var groupOrder = new List<string>();

        foreach (var prop in properties)
        {
            string group = string.IsNullOrEmpty(prop.Group) ? "其他" : prop.Group;
            if (!groupMap.ContainsKey(group))
            {
                groupMap[group] = new List<PropertyMetadata>();
                groupOrder.Add(group);
            }
            groupMap[group].Add(prop);
        }

        return groupOrder.Select(name => new PropertyGroup
        {
            Name = name,
            Properties = groupMap[name],
        }).ToList();
    }

    private int GetColumnWidth(PropertyMetadata prop)
    {
        if (prop.IsPathString) return 250;
        if (prop.IsEnum) return 160;
        if (prop.IsBool) return 70;
        if (prop.IsString) return 150;
        if (prop.IsNumeric) return 90;
        return 120;
    }

    public void ExpandAllGroups(Worksheet sheet)
    {
        try
        {
            var outlines = sheet.GetOutlines(RowOrColumn.Column);
            if (outlines != null)
            {
                foreach (var group in outlines)
                    foreach (var outline in group)
                        outline.Expand();
            }
        }
        catch { }
    }

    public void CollapseAllGroups(Worksheet sheet)
    {
        try
        {
            var outlines = sheet.GetOutlines(RowOrColumn.Column);
            if (outlines != null)
            {
                foreach (var group in outlines)
                    foreach (var outline in group)
                        outline.Collapse();
            }
        }
        catch { }
    }

    private class PropertyGroup
    {
        public string Name = "";
        public List<PropertyMetadata> Properties = new();
    }
}
