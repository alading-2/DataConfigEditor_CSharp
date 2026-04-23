namespace TestData;

public class SampleConfig
{
    // ====== 基础 ======
    /// <summary>显示名称</summary>
    public string? Name { get; set; }

    /// <summary>冷却时间</summary>
    public float Cooldown { get; set; }

    /// <summary>冲刺</summary>
    public static readonly SampleConfig Dash = new()
    {
        Name = "冲刺",
        Cooldown = 1.5f,
    };
}
