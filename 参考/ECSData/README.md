# Data 容器系统 - 统一数据管理

## 📋 概述

Data 容器是一个增强版的动态数据管理系统，提供类型安全、元数据驱动、修改器支持和计算数据的统一数据访问方案。

**核心理念**：Data 是唯一数据源，所有数据（普通数据、可修改数据、计算数据）统一从 Data 容器访问。

## ✨ 核心特性

- ✅ **类型安全**：DataKey 为 `static readonly DataMeta`，编译期检查，智能提示
- ✅ **元数据内嵌**：每个 DataKey 直接携带类型、默认值、约束等信息
- ✅ **隐式转换**：`DataMeta` 隐式转换为 `string`，保持 `Data.Get/Set(DataKey.*)` 兼容
- ✅ **修改器系统**：支持 Buff/Debuff，自动计算最终值
- ✅ **计算数据**：自动依赖追踪，缓存优化
- ✅ **事件监听**：数据变更通过 `Entity.Events` 自动通知
- ✅ **性能优化**：脏标记缓存，避免重复计算

## 📚 文档分工

本 README 只负责 **`Src/ECS/Base/Data/` 运行时容器**。

- `Data/README.md`：`Data/` 顶层目录分工
- `Data/Data/README.md`：Config / Resource 到 Data 的映射规范
- `Data/DataKey/README.md`：DataKey / DataMeta 定义规范

如果你在改 `Data/Config`、`Data/Data`、`Data/DataKey`、`Data/EventType`，优先看这些 `Data/` 目录文档，而不是只看本文件。

## 🏗️ 架构设计

```
┌─────────────────────────────────────────────────────┐
│  Data (核心数据容器)                                 │
│  - 存储所有数据（基础值 + 修改器）                   │
│  - 支持元数据约束                                    │
│  - 支持计算数据                                      │
│  - 支持修改器（可选）                                │
└─────────────────────────────────────────────────────┘
                        ↓
┌─────────────────────────────────────────────────────┐
│  DataKey (静态 DataMeta 字段)                        │
│  - 类型安全的键 + 元数据合一                         │
│  - 隐式转换为 string，兼容 Data.Get/Set              │
│  - 定义时自动注册到 DataRegistry                     │
└─────────────────────────────────────────────────────┘
```

## 📦 核心组件

### 1. Data 容器 (`Data.cs`)

核心数据容器，提供统一的数据访问接口。

**核心公式**：

```
最终值 = (基础值 + Σ加法修改器) × Π乘法修改器
```

### 2. DataKey (`Data/DataKey/`)

**新架构（2026-03）**：DataKey 不再是 `const string`，而是 `static readonly DataMeta`。

**目录结构**：

```
Data/DataKey/
├── Base/       → 通用键（Name、Id、Team 等）
├── Attribute/  → 属性系统（BaseAttack、FinalHp、CritRate 等）
├── Unit/       → 单位状态（CurrentHp、UnitState 等）
├── Ability/    → 技能数据（Cooldown、CastRange 等）
├── Movement/   → 运动参数（MoveMode、OrbitRadius 等）
├── AI/         → AI 数据（AIState、DetectionRange 等）
└── Effect/     → 特效数据（EffectScale、EffectPlayRate 等）
```

**定义示例**：

```csharp
public static partial class DataKey
{
    // 普通属性键
    public static readonly DataMeta BaseAttack = DataRegistry.Register(
        new DataMeta { Key = nameof(BaseAttack), DisplayName = "基础攻击力", 
            Category = DataCategory_Attribute.Attack, Type = typeof(float), 
            DefaultValue = 0f, MinValue = 0, SupportModifiers = true });

    // 计算属性键
    public static readonly DataMeta FinalAttack = DataRegistry.Register(
        new DataMeta { Key = nameof(FinalAttack), DisplayName = "攻击力",
            Category = DataCategory_Attribute.Computed, Type = typeof(float),
            DefaultValue = 0f, SupportModifiers = false,
            Dependencies = [nameof(BaseAttack), nameof(AttackBonus)],
            Compute = (data) => {
                float baseAttack = data.Get<float>(nameof(BaseAttack));
                float bonus = data.Get<float>(nameof(AttackBonus));
                return MyMath.AttributeBonusCalculation(baseAttack, bonus);
            }});

    // Node2D 引用等非注册类型仍使用 const string
    public const string TargetNode = "TargetNode";
}
```

### 3. DataMeta (`DataMeta.cs`)

数据元数据，描述数据的所有特性，同时作为 DataKey 的类型。

**核心字段**：

| 字段 | 用途 | 必填 |
|------|------|------|
| `Key` | 数据键名 | ✅ |
| `Type` | 数据类型（`typeof(float)` 等） | ✅ |
| `DisplayName` | UI 显示名称 | ❌ |
| `Description` | 描述信息 | ❌ |
| `Category` | 分类枚举 | ❌ |
| `DefaultValue` | 默认值 | ❌（自动推断） |
| `MinValue` / `MaxValue` | 数值约束 | ❌ |
| `IsPercentage` | 是否为百分比 | ❌ |
| `SupportModifiers` | 是否支持修改器 | ❌ |
| `Dependencies` | 计算数据依赖 | ❌ |
| `Compute` | 计算函数 | ❌ |

**隐式转换**：

```csharp
// DataMeta 隐式转换为 string（返回 Key 值）
public static implicit operator string(DataMeta meta) => meta.Key;

// 因此可以直接使用：
_data.Get<float>(DataKey.BaseAttack);  // 等价于 _data.Get<float>("BaseAttack")
```

### 4. DataRegistry (`DataRegistry.cs`)

数据注册表，管理所有数据的元数据和计算规则。

**功能**：

- `Register(DataMeta)` - 注册元数据并返回同一实例（支持链式定义）
- `GetMeta(key)` - 查询元数据
- `SupportModifiers(key)` - 检查是否支持修改器
- `GetDependentComputedKeys(key)` - 获取依赖此键的计算键

### 5. DataModifier (`DataModifier.cs`)

数据修改器，用于实现 Buff/Debuff 系统。

**修改器类型**：

- **Additive（加法）**：直接加到基础值
- **Multiplicative（乘法）**：作为乘数（1.0 = 100%，1.5 = 150%）
- **FinalAdditive**：最终加法（在乘法之后）
- **Override**：覆盖值（最高优先级）
- **Cap**：上限约束

## 🚀 快速开始

### 基础使用

```csharp
public partial class Player : Entity
{
    // Data 作为 Entity 的属性
    public Data Data { get; private set; } = new Data();

    public override void _Ready()
    {
        // ✅ 设置基础数据
        Data.Set(DataKey.Name, "玩家");
        Data.Set(DataKey.Level, 1);
        Data.Set(DataKey.MaxHp, 100f);
        Data.Set(DataKey.Damage, 10f);
        Data.Set(DataKey.Speed, 300f);

        // ✅ 获取数据（两种方式效果一致）
        float maxHp = Data.Get<float>(DataKey.MaxHp); // 显式指定类型
        var damage = Data.Get(DataKey.Damage);        // 自动推断类型 (var)
        var name = Data.Get(DataKey.Name);

        // ✅ 算术运算
        Data.Add(DataKey.MaxHp, 20f);      // 生命值 +20
        Data.Multiply(DataKey.Damage, 1.5f); // 伤害 ×1.5

        // ✅ 获取计算数据（自动计算）
        float dps = Data.Get<float>(DataKey.DPS);
        float attackInterval = Data.Get<float>(DataKey.AttackInterval);
    }
}
```

### 修改器系统（Buff/Debuff）

```csharp
// 添加攻速 Buff（+50%）
var buffId = "Buff_Haste";
Data.AddModifier(DataKey.AttackSpeed, new DataModifier(
    ModifierType.Multiplicative,
    1.5f,  // 150% = 1.5 倍
    priority: 0,
    id: buffId
));

// 添加伤害 Buff（+10 点）
Data.AddModifier(DataKey.Damage, new DataModifier(
    ModifierType.Additive,
    10f,
    id: "Buff_Strength"
));

// 5 秒后移除 Buff
GetTree().CreateTimer(5.0).Timeout += () => {
    Data.RemoveModifier(DataKey.AttackSpeed, buffId);
};

// 检查是否拥有 Buff
bool hasBuff = Data.HasModifier(DataKey.AttackSpeed, buffId);

// 获取所有修改器
var modifiers = Data.GetModifiers(DataKey.Damage);

// 清除所有修改器
Data.ClearModifiers(DataKey.Damage);
```

### 事件监听

**注意：Data.On 方法已移除，统一使用 Entity.Events 事件总线。**

```csharp
// 1. 获取 Entity (Data 的拥有者)
var entity = EntityManager.GetEntityByComponent(this);

// 2. 监听数据变更事件
entity.Events.On<GameEventType.Data.PropertyChangedEventData>(
    GameEventType.Data.PropertyChanged, 
    OnDataChanged
);

// 3. 处理事件
private void OnDataChanged(GameEventType.Data.PropertyChangedEventData evt)
{
    // 过滤感兴趣的数据键
    if (evt.Key == DataKey.MaxHp)
    {
        GD.Print($"生命值变更: {evt.OldValue} -> {evt.NewValue}");
    }
}

// 4. 取消监听
// Entity.Events 会在 Destroy 时自动清理，通常无需手动 Off
// 如果需要中途取消：
entity.Events.Off<GameEventType.Data.PropertyChangedEventData>(
    GameEventType.Data.PropertyChanged, 
    OnDataChanged
);
```

### 元数据查询

```csharp
// 获取元数据
var meta = DataRegistry.GetMeta(DataKey.Damage);
if (meta != null)
{
    GD.Print($"显示名称: {meta.DisplayName}");
    GD.Print($"描述: {meta.Description}");
    GD.Print($"默认值: {meta.DefaultValue}");
    GD.Print($"范围: {meta.MinValue} - {meta.MaxValue}");
    GD.Print($"支持修改器: {meta.SupportModifiers}");

    // 格式化显示
    float damage = Data.Get<float>(DataKey.Damage);
    GD.Print($"伤害: {meta.FormatValue(damage)}");
}

// 按分类查询
var attackData = DataRegistry.GetMetaByCategory(DataCategory.Attack);
foreach (var meta in attackData)
{
    GD.Print($"{meta.DisplayName}: {meta.Description}");
}
```

### 批量操作

```csharp
// 批量设置
Data.SetMultiple(new Dictionary<string, object>
{
    { DataKey.MaxHp, 150f },
    { DataKey.Damage, 20f },
    { DataKey.Speed, 350f }
});

// 获取所有数据
var allData = Data.GetAll();
foreach (var kvp in allData)
{
    GD.Print($"{kvp.Key} = {kvp.Value}");
}

// 清空所有数据
Data.Clear();
```

## 📊 计算数据详解

计算数据是由其他数据派生的只读数据，自动追踪依赖关系并缓存结果。

### 内置计算数据

#### 1. 攻击间隔 (AttackInterval)

```csharp
// 公式：1.0 / (攻击速度 / 100)
// 依赖：AttackSpeed
float attackInterval = Data.Get<float>(DataKey.AttackInterval);
```

#### 2. 有效生命值 (EffectiveHp)

```csharp
// 公式：最大生命值 / (1 - 伤害减免)
// 依赖：MaxHp, DamageReduction
float effectiveHp = Data.Get<float>(DataKey.EffectiveHp);
```

#### 3. 每秒伤害 (DPS)

```csharp
// 公式：伤害 × 攻击速度 × (1 + 暴击率 × (暴击伤害 - 1))
// 依赖：Damage, AttackSpeed, CritChance, CritDamage
float dps = Data.Get<float>(DataKey.DPS);
```

### 自动依赖追踪

当基础数据变更时，依赖它的计算数据会自动标记为脏并重新计算：

```csharp
// 设置基础数据
Data.Set(DataKey.Damage, 10f);
Data.Set(DataKey.AttackSpeed, 100f);

// 获取计算数据（自动计算）
float dps1 = Data.Get<float>(DataKey.DPS); // 计算：10 × 1.0 = 10

// 修改基础数据
Data.Set(DataKey.Damage, 20f);

// 计算数据自动更新
float dps2 = Data.Get<float>(DataKey.DPS); // 重新计算：20 × 1.0 = 20
```

## 🔧 高级用法

### 获取基础值（不应用修改器）

```csharp
// 获取最终值（包含修改器）
float finalDamage = Data.Get<float>(DataKey.Damage);

// 获取基础值（不包含修改器）
float baseDamage = Data.GetBase<float>(DataKey.Damage);
```

### 修改器优先级

```csharp
// 优先级越小越先计算
Data.AddModifier(DataKey.Damage, new DataModifier(
    ModifierType.Additive,
    10f,
    priority: 0,  // 先计算
    id: "Buff_1"
));

Data.AddModifier(DataKey.Damage, new DataModifier(
    ModifierType.Multiplicative,
    1.5f,
    priority: 1,  // 后计算
    id: "Buff_2"
));

// 计算顺序：(基础值 + 10) × 1.5
```

### 资源自动映射

```csharp
// 从 Resource 自动映射数据
// 内部调用 LoadFromResource
Data.ApplyResource("PlayerData");

// 或者直接传入 Resource 对象
Data.LoadFromResource(resource);
```

### 装备系统集成示例

```csharp
// 假设 ItemEntity 有自己的 Data
var itemData = itemEntity.GetData();

// 将 Item 的数据作为修改器应用到 Player
// 自动将 Item 的数值属性（Damage, Speed 等）转换为 Player 的 Additive Modifiers
// 并将 sourceEntity 设置为 itemEntity，以便后续追踪
playerData.ApplyDataAsModifiers(itemData, itemEntity);

// 卸载装备时，通过 Source 批量移除
playerData.RemoveModifiersBySource(itemEntity);
```

### 对象池复用

```csharp
// 重置数据容器（用于对象池）
Data.Reset();

// 注意：不会清除事件监听器，需要手动管理
```

## 📝 扩展指南

### 添加新数据

#### 1. 在 DataKey 中定义常量

```csharp
public static class DataKey
{
    // 添加新数据键
    public const string ManaRegen = "ManaRegen";
}
```

#### 2. 在 DataRegistry 中注册元数据

```csharp
private static void RegisterBasicData()
{
    Register(new DataMeta
    {
        Key = DataKey.ManaRegen,
        DisplayName = "魔法恢复",
        Description = "每秒恢复的魔法值",
        Category = DataCategory.Resource,
        Type = typeof(float),
        DefaultValue = 0f,
        MinValue = 0,
        MaxValue = 1000,
        SupportModifiers = true  // 是否支持修改器
    });
}
```

#### 3. 使用新数据

```csharp
Data.Set(DataKey.ManaRegen, 5f);
float manaRegen = Data.Get<float>(DataKey.ManaRegen);
```

### 添加计算数据

#### 1. 在 DataKey 中定义常量

```csharp
public const string MaxMana = "MaxMana";
public const string EffectiveMana = "EffectiveMana";
```

#### 2. 在 DataRegistry 中注册计算规则

```csharp
private static void RegisterComputedData()
{
    RegisterComputed(new ComputedData
    {
        Key = DataKey.EffectiveMana,
        Dependencies = new[] { DataKey.MaxMana, DataKey.ManaRegen },
        Compute = (data) =>
        {
            float maxMana = data.Get<float>(DataKey.MaxMana);
            float manaRegen = data.Get<float>(DataKey.ManaRegen);
            return maxMana + manaRegen * 10f; // 示例公式
        }
    });
}
```

#### 3. 使用计算数据

```csharp
// 自动计算，无需手动维护
float effectiveMana = Data.Get<float>(DataKey.EffectiveMana);
```

## ⚠️ 注意事项

### 1. 修改器支持

只有在元数据中声明 `SupportModifiers = true` 的数据才支持修改器：

```csharp
// ✅ 支持修改器（数值类型）
Data.AddModifier(DataKey.Damage, modifier);

// ❌ 不支持修改器（字符串类型）
Data.AddModifier(DataKey.Name, modifier); // 会输出警告并忽略
```

### 2. 计算数据只读

计算数据是只读的，不能直接设置：

```csharp
// ❌ 错误：计算数据不能直接设置
Data.Set(DataKey.DPS, 100f);

// ✅ 正确：修改依赖的基础数据
Data.Set(DataKey.Damage, 20f);
float dps = Data.Get<float>(DataKey.DPS); // 自动重新计算
```

### 3. 事件监听清理

在 `_ExitTree` 中清理事件监听，避免内存泄漏，一般事件监听和注销都在 Component 设置：

```csharp
public override void _ExitTree()
{
    Data.OnValueChanged -= OnDataChanged;
    Data.Off(DataKey.MaxHp, OnHpChanged);
}
```

### 4. 类型转换

使用泛型方法时注意类型匹配：

```csharp
// ✅ 正确
float damage = Data.Get<float>(DataKey.Damage);
int level = Data.Get<int>(DataKey.Level);

// ⚠️ 类型不匹配时会尝试自动转换
int damageInt = Data.Get<int>(DataKey.Damage); // float -> int
```

### 5. 默认值处理

当数据不存在或为 null 时，返回默认值：

```csharp
// 数据不存在，返回默认值 0
float unknown = Data.Get<float>("UnknownKey", 0f);

// 数据为 null，返回默认值
Data.Set(DataKey.Damage, null);
float damage = Data.Get<float>(DataKey.Damage, 10f); // 返回 10
```

### 6. 默认值的智能处理

Data 系统会自动处理默认值，优先级如下：

```csharp
// 优先级 1: 用户传入的默认值（可选参数）
float damage = Data.Get<float>(DataKey.Damage, 999f);  // 如果不存在，返回 999f

// 优先级 2: DataRegistry 中注册的默认值
float damage = Data.Get<float>(DataKey.Damage);  // 如果不存在，返回 DataMeta 中定义的默认值

// 优先级 3: 类型推断的默认值
float unknownValue = Data.Get<float>("UnknownKey");  // 如果不存在且未注册，返回 0f (float 的默认值)
```

**最佳实践**：

```csharp
// ✅ 推荐：对于已注册的数据键，无需传入默认值参数
float armor = Data.Get<float>(DataKey.Armor);
float critChance = Data.Get<float>(DataKey.CritRate);

// ✅ 可选：对于未注册但需要特定默认值的情况，传入默认值参数
float customValue = Data.Get<float>("CustomKey", 100f);

// ❌ 不推荐：对已注册的数据键传入与 DataMeta 定义不同的默认值
float damage = Data.Get<float>(DataKey.Damage, 999f);  // 可能导致行为不一致
```

## 🎯 最佳实践

### 1. 使用常量而非字符串

```csharp
// ❌ 不推荐：字符串易错，无智能提示
float damage = Data.Get<float>("Damage");

// ✅ 推荐：使用常量，类型安全
float damage = Data.Get<float>(DataKey.Damage);
```

### 2. 利用元数据驱动 UI

```csharp
// 自动生成属性面板
var attackData = DataRegistry.GetMetaByCategory(DataCategory.Attack);
foreach (var meta in attackData)
{
    var value = Data.Get<float>(meta.Key);
    var label = new Label();
    label.Text = $"{meta.DisplayName}: {meta.FormatValue(value)}";
    AddChild(label);
}
```

### 3. 统一修改器命名

```csharp
// 推荐命名规范：类型_来源_效果
"Buff_Weapon_Damage"
"Debuff_Poison_Speed"
"Passive_Skill_CritChance"
```

### 4. 批量操作优化

```csharp
// ❌ 不推荐：多次触发事件
Data.Set(DataKey.MaxHp, 100f);
Data.Set(DataKey.Damage, 10f);
Data.Set(DataKey.Speed, 300f);

// ✅ 推荐：批量设置，减少事件触发
Data.SetMultiple(new Dictionary<string, object>
{
    { DataKey.MaxHp, 100f },
    { DataKey.Damage, 10f },
    { DataKey.Speed, 300f }
});
```

### 5. 性能优化

```csharp
// ✅ 缓存频繁访问的数据
private float _cachedDamage;

public override void _Ready()
{
    _cachedDamage = Data.Get<float>(DataKey.Damage);

    // 注意：实际开发中应在 OnComponentRegistered 中订阅 Entity.Events
    // 这里仅演示逻辑
    var entity = this.GetData().Owner; // 假设有扩展方法获取 Owner
    if (entity != null)
    {
        entity.Events.On<GameEventType.Data.PropertyChangedEventData>(
            GameEventType.Data.PropertyChanged, 
            evt => {
                if (evt.Key == DataKey.Damage)
                    _cachedDamage = (float)evt.NewValue;
            }
        );
    }
}

public override void _Process(double delta)
{
    // 使用缓存值，避免重复计算
    ApplyDamage(_cachedDamage);
}
```

## 📚 相关文档

- [设计文档](../../../../Docs/框架/ECS/Data/DataSystem_Design.md) - 当前 Data 系统主说明文档
- [项目规则](../../../../.agent/rules/project_rules.md) - 项目级别的使用规范

## 🔄 版本历史

- **v2.3** (2025-01-08)
  - 改进 `Data.Get` 方法的默认值处理
  - 无需为已注册的数据键传入默认值参数
  - 自动使用 `DataRegistry` 中定义的默认值
  - 更新所有 Damage System Processor 代码以遵循新的最佳实践

- **v2.2** (2025-01-03) - `DataModifier` 新增 `Source` 属性，支持按来源追踪 - 新增 `RemoveModifiersBySource`，优化装备/Buff 移除逻辑 - 新增 `ApplyDataAsModifiers`，支持将 Data 转换为修改器 - 新增 `LoadFromResource`，统一 Resource 注入逻辑（原 `EntityManager.InjectResourceData`）

  - **v2.1** (2025-01-03)

  - 使用 `System.Type` 替代 `DataType` 枚举
  - 支持 `Data.Get(key)` 自动推断返回类型
  - 支持 `Options` 固定选项约束（int 存储，string 显示）
  - 优化 `DataMeta` 注册，支持智能默认值推断

- **v2.0** (2025-01-03)

- **v1.0** (2024-12-XX)

  - 初始版本
  - 基础键值存储
  - 事件监听

---

**维护者**: Trae AI
**最后更新**: 2025-01-08
