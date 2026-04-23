namespace DataConfigEditor.Core;

/// <summary>
/// 属性元数据（纯源码解析，无反射依赖）
/// </summary>
public class PropertyMetadata
{
    public string Name = "";
    public string TypeName = "";       // "int", "float", "string", "AbilityTriggerMode" 等
    public string DefaultValue = "";   // 属性声明中的默认值表达式
    public string Group = "";
    public string Summary = "";

    // 类型判断（基于 TypeName 字符串）
    public bool IsEnum { get; set; }
    public bool IsFlags { get; set; }
    public bool IsNumeric => TypeName is "int" or "float" or "double";
    public bool IsBool => TypeName == "bool";
    public bool IsString => TypeName is "string" or "string?";
    public bool IsPathString => IsString && (Name.EndsWith("Path", StringComparison.Ordinal) || Name.EndsWith("Scene", StringComparison.Ordinal));

    public string FormatValue(string? value)
    {
        return value ?? "";
    }

    public string FriendlyTypeName => TypeName switch
    {
        "Int32" => "int",
        "Single" => "float",
        "Double" => "double",
        "Boolean" => "bool",
        "String" => "string",
        _ => TypeName,
    };
}
