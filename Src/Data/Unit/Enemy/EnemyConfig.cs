using Godot;

namespace Slime.Config.Units
{
    [GlobalClass]
    public partial class EnemyConfig : UnitConfig
    {
        /// <summary>
        /// 击杀经验值奖励
        /// </summary>
        [ExportGroup("敌人专有")]
        [DataKey(nameof(DataKey.ExpReward))]
        [Export] public int ExpReward { get; set; } = (int)DataKey.ExpReward.DefaultValue!;

        /// <summary>
        /// AI 检测范围
        /// </summary>
        [ExportGroup("AI 配置")]
        [DataKey(nameof(DataKey.DetectionRange))]
        [Export] public float DetectionRange { get; set; } = (float)DataKey.DetectionRange.DefaultValue!;

        /// <summary>
        /// 是否启用生成规则
        /// </summary>
        [ExportGroup("Spawn Rule")]
        [DataKey(nameof(DataKey.IsEnableSpawnRule))]
        [Export] public bool IsEnableSpawnRule { get; set; } = (bool)DataKey.IsEnableSpawnRule.DefaultValue!;
        /// <summary>
        /// 生成位置策略 (Rectangle/Circle)
        /// </summary>
        [DataKey(nameof(DataKey.SpawnStrategy))]
        [Export] public SpawnPositionStrategy SpawnStrategy { get; set; } = (SpawnPositionStrategy)DataKey.SpawnStrategy.DefaultValue!;
        /// <summary>
        /// 起始波次 (从第几波开始生成)
        /// </summary>
        [DataKey(nameof(DataKey.SpawnMinWave))]
        [Export] public int SpawnMinWave { get; set; } = (int)DataKey.SpawnMinWave.DefaultValue!;
        /// <summary>
        /// 截止波次 (-1表示无限制)
        /// </summary>
        [DataKey(nameof(DataKey.SpawnMaxWave))]
        [Export] public int SpawnMaxWave { get; set; } = (int)DataKey.SpawnMaxWave.DefaultValue!;
        /// <summary>
        /// 生成间隔 (秒)
        /// </summary>
        [DataKey(nameof(DataKey.SpawnInterval))]
        [Export] public float SpawnInterval { get; set; } = (float)DataKey.SpawnInterval.DefaultValue!;
        /// <summary>
        /// 单波次最大生成数量 (-1表示无限制)
        /// </summary>
        [DataKey(nameof(DataKey.SpawnMaxCountPerWave))]
        [Export] public int SpawnMaxCountPerWave { get; set; } = (int)DataKey.SpawnMaxCountPerWave.DefaultValue!;
        /// <summary>
        /// 单次生成数量
        /// </summary>
        [DataKey(nameof(DataKey.SingleSpawnCount))]
        [Export] public int SingleSpawnCount { get; set; } = (int)DataKey.SingleSpawnCount.DefaultValue!;
        /// <summary>
        /// 生成数量波动值 (最终数量 = Count ± Variance)
        /// </summary>
        [DataKey(nameof(DataKey.SingleSpawnVariance))]
        [Export] public int SingleSpawnVariance { get; set; } = (int)DataKey.SingleSpawnVariance.DefaultValue!;
        /// <summary>
        /// 波次开始后的首次生成延迟 (秒)
        /// </summary>
        [DataKey(nameof(DataKey.SpawnStartDelay))]
        [Export] public float SpawnStartDelay { get; set; } = (float)DataKey.SpawnStartDelay.DefaultValue!;
        /// <summary>
        /// 生成权重 (用于随机生成池)
        /// </summary>
        [DataKey(nameof(DataKey.SpawnWeight))]
        [Export] public int SpawnWeight { get; set; } = (int)DataKey.SpawnWeight.DefaultValue!;
    }
}
