# DataNew 纯 C# 数据表使用说明

`Data/DataNew/` 是当前推荐的数据配置方式：一张表对应一个 C# 数据类，一个静态实例对应一行数据。
旧的 `Data/Data + Data/Config + .tres` 仅作为整体兼容模式保留。

## 1. 数据表怎么写

以敌人表为例：`EnemyData` 就是敌人表，`Yuren` / `Chailangren` 是表里的两行数据。

```csharp
public class EnemyData : UnitData
{
    /// <summary>全部敌人数据。</summary>
    public static IReadOnlyList<EnemyData> All => DataTable.GetAll<EnemyData>();

    /// <summary>按 Name 获取敌人数据，找不到返回 null 并写 Log.Error。</summary>
    public static EnemyData? Get(string name) => DataTable.GetByName<EnemyData>(name);

    /// <summary>鱼人</summary>
    public static readonly EnemyData Yuren = new()
    {
        Name = "鱼人",
        Team = Team.Enemy,
        VisualScenePath = "res://assets/Unit/Enemy/yuren/AnimatedSprite2D/yuren.tscn",
        BaseHp = 150f,
        BaseAttack = 6f,
        MoveSpeed = 150f,
    };
}
```

关键规则：

- `Name` 是默认查询字段，同一张表内必须唯一。
- 数据行使用 `public static readonly XxxData`。
- 默认值优先读取 `DataKey.Xxx.DefaultValue`。
- 不需要继承 `DataTable`；`DataTable` 是静态工具类。

## 2. 怎么通过名字获取数据

推荐业务侧调用每张表自己的 `Get` 包装：

```csharp
var enemy = EnemyData.Get("鱼人") ?? EnemyData.Yuren;
```

也可以直接使用通用工具：

```csharp
var enemy = DataTable.GetByName<EnemyData>("鱼人") ?? EnemyData.Yuren;
```

获取整张表：

```csharp
foreach (var enemy in EnemyData.All)
{
    GD.Print(enemy.Name);
}
```

`GetByName` 找不到数据时不会抛异常，会调用 `Log.Error`，并返回 `null`。调用方用 `??` 明确写兜底逻辑。

## 3. 怎么进入运行时 Entity.Data

`EntityManager.Spawn` 的 `Config` 现在支持 DataNew POCO：

```csharp
var enemyConfig = EnemyData.Get("鱼人") ?? EnemyData.Yuren;
var enemy = EntityManager.Spawn<EnemyEntity>(new EntitySpawnConfig
{
    Config = enemyConfig, // DataNew POCO，会通过 Data.LoadFromConfig 注入 Entity.Data
    UsingObjectPool = true,
    PoolName = ObjectPoolNames.EnemyPool,
    Position = spawnPosition
});
```

内部流程：

1. `EntityManager.Spawn` 接收 `EntitySpawnConfig.Config`。
2. 调用 `entity.Data.LoadFromConfig(config)`。
3. `LoadFromConfig` 反射读取配置对象公开属性。
4. 属性名或 `[DataKey(nameof(DataKey.Xxx))]` 映射到运行时 `DataKey`。
5. 写入 `Entity.Data`，组件通过 `DataKey` 读取。

## 4. 什么时候需要 `[DataKey]`

如果属性名和目标 `DataKey` 不一致，需要显式标注。

```csharp
[DataKey(nameof(DataKey.AbilityFeatureGroup))]
public string? FeatureGroupId { get; set; }

[DataKey(nameof(DataKey.AbilityDamageBonus))]
public float BaseSkillDamage { get; set; }
```

如果属性名本身就是目标 DataKey 名称，例如 `BaseHp`、`MoveSpeed`、`SpawnInterval`，可以不写 `[DataKey]`。

## 5. 数据源切换

全局开关在 `GlobalConfig`：

```csharp
GlobalConfig.DataSourceMode = DataSourceMode.PureCSharp;
```

- `PureCSharp`：推荐模式，使用 `Data/DataNew`。
- `GodotResource`：兼容模式，使用旧 `.tres`。

不要在同一次数据加载里混用两种模式；需要切换时整体切换。
