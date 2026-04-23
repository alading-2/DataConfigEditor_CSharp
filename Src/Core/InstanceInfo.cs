namespace DataConfigEditor.Core;

/// <summary>
/// 配置类型信息
/// </summary>
public class ConfigTypeInfo
{
    public string ClassName = "";       // 类名，如 "AbilityConfigData"
    public string Namespace = "";       // 命名空间
    public string Name = "";            // 显示名，如 "AbilityConfigData (Slime.ConfigNew.Abilities)"
    public string SourceFile = "";      // 源文件路径
    public string BaseClassName = "";   // 基类名（用于合并属性）
    public string BaseClassSourceFile = ""; // 基类源文件路径
}

/// <summary>
/// 静态实例信息（纯源码解析，值存储为字典）
/// </summary>
public class InstanceInfo
{
    public string Name = "";                                    // 静态字段名，如 "Slam"
    public Dictionary<string, string> Values = new();           // 属性名 → 值字符串
    public string Summary = "";                                 // 字段注释
}
