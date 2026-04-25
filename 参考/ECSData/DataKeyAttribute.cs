using System;

/// <summary>
/// 标记 Config 属性对应的数据键
/// 用于 Data.LoadFromResource 时自动映射，避免字符串拼写错误
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DataKeyAttribute : Attribute
{
    /// <summary>
    /// 对应的 DataKey 常量值
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="key">DataKey 常量</param>
    public DataKeyAttribute(string key)
    {
        Key = key;
    }
}
