# C# 强类型配置可表格化规则

> **Created:** 2026-04-23
> **Status:** 第一版规则
> **适用范围:** DataConfigEditor 的工作区浏览、表格查看、后续编辑与诊断

---

## 一、产品边界

DataConfigEditor 的定位不是“用 `.cs` 存数据的编辑器”，也不是“任意 C# 代码编辑器”，而是：

> **面向 C# 强类型配置的表格化查看/编辑器。**

工具只承认一类受约束的 C# 配置形态：用 C# 类型定义 schema，用静态对象初始化器声明数据实例，用 XML 文档注释提供编辑说明。C# 项目自身的类型定义是唯一真相源，表格只是这个类型系统的查看和编辑界面。

因此，可表格化规则的核心目标是判断一个 `.cs` 文件是否能被稳定映射为：

```text
一个配置类型 schema + 多个静态实例行 + 一组强类型单元格
```

不在目标内：

- 不解析任意方法逻辑。
- 不执行用户代码。
- 不要求先加载游戏 DLL。
- 不把 Godot Resource、`.tres` 或 GDScript 插件语义作为前提。
- 不把工具变成通用 C# IDE。

---

## 二、文件分类

工作区中的 `.cs` 文件分为四类。UI 应该用诊断明确告诉用户文件属于哪一类，而不是静默失败。

| 类别 | 是否可表格化 | 说明 |
|------|--------------|------|
| 数据表文件 | 是 | 定义一个配置类，并包含该类的静态实例初始化器 |
| Schema 文件 | 否，但有价值 | 只定义基类、公共属性或抽象配置类，无实例行 |
| 类型辅助文件 | 否，但有价值 | 只定义 enum、Flags enum、常量、共享小类型 |
| 普通代码文件 | 否 | 行为代码、Godot 节点、handler、service、工具逻辑等 |

第一阶段右侧只显示两种结果：

- 数据表文件：显示表格。
- 其他文件：显示只读诊断视图，说明为什么不能转换为表格。

后续阶段可以在左侧树或状态栏中给 Schema 文件、enum 文件加轻量标识，但不把它们强行打开成空表。

---

## 三、数据表文件的硬规则

一个 `.cs` 文件只有同时满足以下规则，才算“可表格化数据表文件”。

### 3.1 文件规则

- 文件扩展名必须是 `.cs`。
- 文件必须在用户打开的工作区目录下。
- 扫描时应排除 `bin/`、`obj/`、`.godot/`、`.idea/`、`.vscode/`、`.history/` 等工具产物目录。
- 一个文件第一阶段只允许有一个主配置类。多个候选配置类属于歧义，显示诊断，不自动猜。
- 文件可以包含同名 namespace、using、注释、enum 或少量辅助类型，但表格主类必须唯一。

### 3.2 配置类规则

主配置类必须满足：

- 是 `class`，不是 `struct`、`record`、`interface`、`delegate`。
- 是非泛型类型。
- 不是 `static class`。
- 可以是 `public` 或 `internal`；推荐 `public`，便于游戏代码直接引用。
- 可以继承另一个配置基类；如果继承链不能在工作区内解析到，当前文件仍可降级显示，但继承属性不完整。
- 不要求继承 Godot `Resource`，推荐保持纯 POCO。
- 抽象类通常是 Schema 文件；除非同文件内有明确的具体静态实例，否则不作为数据表文件。

推荐命名：

```csharp
public class AbilityConfigData
{
}
```

不推荐把行为类和配置类混在一个文件里。包含方法并不会自动禁止表格化，但编辑器不会理解方法逻辑，方法也不能参与单元格求值。

### 3.3 属性规则

可成为列的属性必须满足：

- 是实例属性，不是 `static` 属性。
- 是 `public` 属性。
- 至少有可读访问器 `get`。
- 支持 `set` 或 `init` 才能进入后续可编辑列；只有 `get` 的计算属性第一阶段不进入列。
- 推荐使用自动属性：`public string? Name { get; set; }`
- 可以有默认值：`public int Level { get; set; } = 1;`
- 属性顺序以源码顺序为准；继承属性在子类属性之前展开。

推荐：

```csharp
/// <summary>技能名称</summary>
public string? Name { get; set; }

/// <summary>冷却时间</summary>
public float Cooldown { get; set; }
```

可接受但后续编辑能力会受限：

```csharp
public required string Id { get; init; }
public IReadOnlyList<string> Tags { get; init; } = [];
```

不进入表格列：

```csharp
public string DisplayName => $"{Id}:{Name}";
public static string Category { get; set; }
public string this[int index] => "";
```

### 3.4 实例规则

可成为行的实例必须满足：

- 是字段，不是属性。
- 是 `public static readonly`。
- 字段类型必须是当前配置类，或可解析为当前配置类的派生类。
- 字段名是实例 ID，必须在当前表内唯一。
- 初始化必须是对象创建表达式加对象初始化器。
- 初始化器中一条属性赋值对应一个单元格。

推荐：

```csharp
/// <summary>冲刺</summary>
public static readonly AbilityConfigData Dash = new()
{
    Name = "冲刺",
    Cooldown = 1.0f,
};
```

可接受：

```csharp
public static readonly AbilityConfigData Dash = new AbilityConfigData
{
    Name = "冲刺",
};
```

第一阶段不作为数据行：

```csharp
public static AbilityConfigData Dash { get; } = new() { Name = "冲刺" };
public static readonly AbilityConfigData Dash = CreateDash();
public static readonly AbilityConfigData Dash;
```

原因是这些写法无法稳定定位“可回写的对象初始化器块”。

### 3.5 单元格值规则

强类型单元格优先支持这些表达式：

- `string` / `string?`：字符串字面量或 `null`
- `bool`：`true` / `false`
- 数值：`int`、`long`、`float`、`double`、`decimal` 等字面量，保留后缀
- enum：`EnumType.Member`
- Flags enum：`EnumType.A | EnumType.B`
- 路径字符串：本质仍是 string，UI 可按 `*Path`、`*Scene`、`*Resource` 等命名约定增强

允许先降级为“原始表达式文本”的值：

- `nameof(...)`
- `SomeConst.Path`
- 简单数组、列表、字典初始化器
- 简单嵌套对象初始化器

不在第一阶段求值，也不应伪装成已理解：

- 方法调用：`CreateValue()`
- LINQ 查询
- 条件表达式或复杂计算
- 依赖运行时状态的表达式
- 需要执行用户代码才能得到值的表达式

这些值可以显示为原始文本，并在诊断中标记为“未类型化表达式”。

---

## 四、注释与分组规则

### 4.1 属性说明

属性说明来自紧邻属性声明上方的 XML 文档注释：

```csharp
/// <summary>技能名称</summary>
public string? Name { get; set; }
```

`<summary>` 是首选来源，用于表头副标题、tooltip 和后续字段说明。注释必须紧邻目标类型或成员，中间不应插入无关代码。

### 4.2 实例说明

实例字段上方的 `<summary>` 是实例中文名或说明：

```csharp
/// <summary>冲刺</summary>
public static readonly AbilityConfigData Dash = new()
{
};
```

表格中可显示为行 tooltip 或实例名副标题，但字段名仍是稳定 ID。

### 4.3 分组

分组使用现有轻量标记：

```csharp
// ====== 基础信息 ======
```

分组只影响显示折叠和视觉组织，不改变数据语义。分组标记归属其后第一个属性开始的一段属性，直到下一个分组标记。

---

## 五、enum 与 Flags 规则

enum 文件本身通常不是表格，但它是强类型单元格的重要输入。

规则：

- 支持普通 `enum`。
- 支持带 `[Flags]` 的 enum。
- 成员名是保存和显示的稳定值。
- `<summary>` 或成员行尾注释可作为中文说明。
- 下拉显示应优先保留英文成员名，中文说明作为 tooltip 或辅助文案。

推荐：

```csharp
public enum AbilityTriggerMode
{
    /// <summary>手动触发</summary>
    Manual,

    /// <summary>自动触发</summary>
    Auto,
}
```

Flags 推荐显式使用 2 的幂：

```csharp
[Flags]
public enum DamageTag
{
    None = 0,
    Fire = 1,
    Ice = 2,
    Lightning = 4,
}
```

---

## 六、诊断规则

不可表格化时必须给出明确原因。建议诊断分为三档。

### 6.1 阻断诊断

出现以下情况时不显示表格：

- 未找到 class。
- 找到多个主配置类，无法判断哪个是表格。
- 主类是泛型、record、struct、interface 或 static class。
- 没有可作为列的属性，且继承链也没有属性。
- 没有可作为行的静态实例。
- 静态实例不是对象初始化器。
- 文件语法结构无法解析。

### 6.2 降级诊断

出现以下情况时仍可显示表格，但部分能力降级：

- 找不到 enum 定义：按普通文本显示。
- 属性类型暂不支持：显示原始表达式，后续不可强类型编辑。
- 单元格值是复杂表达式：显示原始文本。
- 继承基类找不到：只显示当前文件属性，并提示继承信息不完整。
- 缺少 `<summary>`：表头只显示属性名。

### 6.3 约束诊断

出现以下情况时显示表格，但提醒用户整理源码：

- 属性没有分组。
- 实例缺少 `<summary>`。
- 类名和文件名不一致。
- 同一实例初始化器内重复给同一属性赋值。
- 初始化器里出现未声明属性。

---

## 七、阶段实现边界

更完整的产品阶段计划见 [面向 C# 强类型配置的表格编辑器完善计划](./面向CSharp强类型配置的表格编辑器完善计划.md)。本节只描述“可表格化规则”本身在不同阶段的解析边界。

### Phase 1：单文件表格查看

目标是稳定判断“这个 `.cs` 能不能变成表”。

必须支持：

- 单文件 class 解析。
- 当前类声明属性。
- 当前类 `public static readonly` 实例。
- `new()` 或 `new Type()` 对象初始化器。
- `<summary>` 属性注释。
- 失败时显示诊断视图。

暂不承诺：

- 完整继承展开。
- 跨文件 enum 下拉。
- 嵌套对象展开。
- 保存回写。
- 执行任何 C# 代码。

### Phase 2：工作区类型索引

在工作区范围建立索引：

- class 名称到源码文件。
- enum 名称到成员列表。
- 基类到属性链。
- 分组和注释合并。

此阶段后，Schema 文件和 enum 文件虽然仍不是表格，但会参与数据表文件的显示增强。

### Phase 3：Roslyn Syntax 解析

当正则解析开始成为限制时，切换到 Roslyn Syntax API。

目标不是引入完整工程语义，而是先替换单文件结构解析：

- class / property / field / enum 节点。
- 对象初始化器节点。
- trivia 中的 XML 文档注释和普通注释。
- 保留源码格式，服务后续精确回写。

### Phase 4：编辑与回写

只有满足更严格约束的单元格才允许编辑：

- 可定位到原始初始化器赋值。
- 可把 UI 值格式化回合法 C# 表达式。
- 可在不执行用户代码的情况下校验类型。

无法满足时保持只读或原始文本编辑，并显示明确诊断。

---

## 八、推荐配置写法

```csharp
namespace Game.Data;

public class AbilityConfigData
{
    // ====== 基础信息 ======
    /// <summary>技能名称</summary>
    public string? Name { get; set; }

    /// <summary>触发模式</summary>
    public AbilityTriggerMode TriggerMode { get; set; }

    /// <summary>图标路径</summary>
    public string? IconPath { get; set; }

    // ====== 数值 ======
    /// <summary>冷却时间</summary>
    public float Cooldown { get; set; }

    // ====== 实例 ======
    /// <summary>冲刺</summary>
    public static readonly AbilityConfigData Dash = new()
    {
        Name = "冲刺",
        TriggerMode = AbilityTriggerMode.Manual,
        IconPath = "res://assets/icons/dash.png",
        Cooldown = 1.0f,
    };
}

public enum AbilityTriggerMode
{
    /// <summary>手动触发</summary>
    Manual,

    /// <summary>自动触发</summary>
    Auto,
}
```

---

## 九、明确不推荐写法

```csharp
public class AbilityConfigData
{
    public string Name => BuildName();

    public static readonly AbilityConfigData Dash = CreateDash();

    public static AbilityConfigData CreateDash()
    {
        return new AbilityConfigData
        {
            // 编辑器不会执行这个方法
        };
    }
}
```

这类代码可以继续存在于游戏项目中，但它不是 DataConfigEditor 的表格数据格式。

---

## 十、参考资料

- Microsoft Learn: Object and collection initializers  
  https://learn.microsoft.com/en-ie/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers
- Microsoft Learn: Documentation comments / XML doc comments  
  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/documentation-comments
- Microsoft Learn: Recommended XML documentation tags  
  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags
- Microsoft Learn: C# enumerations  
  https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/enums
- Microsoft Learn: readonly keyword  
  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/readonly
- Microsoft Learn: Roslyn syntax analysis  
  https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-analysis
