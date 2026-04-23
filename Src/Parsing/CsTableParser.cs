using DataConfigEditor.Documents;

namespace DataConfigEditor.Parsing;

public sealed class CsTableParser
{
    public TableDocument ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return TableDocument.Error(
                filePath,
                Path.GetFileName(filePath),
                "文件不存在");
        }

        var classInfo = SourceParser.ParseClass(filePath);
        if (classInfo is null)
        {
            return TableDocument.Error(
                filePath,
                Path.GetFileName(filePath),
                "当前文件无法转换为表格视图",
                "未找到可解析的类定义。");
        }

        var comments = CsCommentParser.ParseFile(filePath);
        if (classInfo.Properties.Count == 0 || classInfo.Instances.Count == 0)
        {
            return TableDocument.Error(
                filePath,
                classInfo.ClassName,
                "当前文件无法转换为表格视图",
                "缺少 public 属性或静态实例。");
        }

        var columns = new List<TableColumn>
        {
            new()
            {
                Key = "__instance",
                Header = "实例名",
            },
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
                Value = instance.Values.TryGetValue(property.Name, out var value)
                    ? value
                    : property.DefaultValue,
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
