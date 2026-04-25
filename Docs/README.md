# DataConfigEditor 文档索引

> **Last Updated:** 2026-04-23

本文档作为 `Docs/` 目录入口。当前有效方案、执行计划和历史资料分开管理，避免旧思路和当前路线混在一起。

## 当前有效文档

| 文档 | 用途 |
|------|------|
| [DataConfigEditor_CSharp独立数据配置编辑器.md](./DataConfigEditor_CSharp独立数据配置编辑器.md) | 当前产品定位、架构和主功能说明 |
| [CSharp强类型配置可表格化规则.md](./CSharp强类型配置可表格化规则.md) | 定义哪些 `.cs` 文件能转换成表格 |
| [面向CSharp强类型配置的表格编辑器完善计划.md](./面向CSharp强类型配置的表格编辑器完善计划.md) | 完整产品计划：工作区、表格、设置、筛选、编辑、保存 |
| [面向CSharp强类型配置的表格编辑器方案执行文档.md](./面向CSharp强类型配置的表格编辑器方案执行文档.md) | 后续开发执行步骤和开发者验收标准 |
| [当前进度与查看阶段修复计划.md](./当前进度与查看阶段修复计划.md) | 当前实现进度、查看阶段显示异常根因和下一步修复顺序 |

## 背景文档

| 文档 | 说明 |
|------|------|
| [纯CSharpData存储方案.md](./纯CSharpData存储方案.md) | 记录从 `.tres` / Godot 插件转向 C# 强类型配置的历史背景；不是当前执行入口 |

## 阶段设计与计划

| 文档 | 说明 |
|------|------|
| [workspace-table-browser-design.md](./superpowers/specs/2026-04-23-workspace-table-browser-design.md) | 第一阶段工作区表格浏览器设计 |
| [workspace-table-browser.md](./superpowers/plans/2026-04-23-workspace-table-browser.md) | 第一阶段历史执行计划 |
| [strongly-typed-config-editor-next-phase.md](./superpowers/plans/2026-04-23-strongly-typed-config-editor-next-phase.md) | 下一阶段可执行计划：工作区隐藏、表格布局统一、设置面板 |

## 旧文档归档

旧资料统一放在：

- [旧文档/](./旧文档/)

当前已归档：

- [旧文档/其他/](./旧文档/其他/)

该目录保留历史 TODO、异常日志和截图。除非需要追溯问题，不应把归档文档作为当前实现依据。

## 维护规则

- 新方案文档放在 `Docs/` 根目录，文件名必须能表达用途。
- 已被新方案替代的文档移动到 `Docs/旧文档/`。
- 阶段性 spec 和执行计划可以继续放在 `Docs/superpowers/specs/` 与 `Docs/superpowers/plans/`。
- 每次新增重要方案文档后，同步更新本索引。
- 不再把临时截图、异常日志、聊天草稿散落在 `Docs/` 根目录。
