# Shell / Configuration / User

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**用户级持久化模型**：图层清单、图层层级定义、材料填充、与 hy-settings 等存储对齐的类型（`HyCadUserSettings`、`UserLayerSettings` 等），表示「本机/本用户」覆盖 Global/Modules 的意图。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 可序列化 POCO、列表项（如 `LayerDefinitionItem`） | 单会话缓存 → 各 VM 的私有字段 |
| 与 UI 绑定的**形状** of 数据 | 读盘/写盘 I/O 实现可位于 Infrastructure，不在此目录取名 |

## 目录结构

- `HyCadUserSettings.cs`：用户设置聚合（若存在）。
- `UserLayerSettings.cs`、`LayerDefinitionItem.cs`、`LayerSemanticIds.cs`。
- `RoadMaterialFillSettings.cs` 等按域延伸的用户子配置。

## 命令与首选项

无独立命令键；随「首选项/设置」保存或应用启动时加载。

## 依赖与协作

- **与 Global/Modules**：合并顺序一般为 Global → Modules → User 覆盖（以 `IConfigurationService` 实现为准）。

## 开发与审查要点

- [ ] 重命名字段时考虑与已有 JSON 文件向后兼容或迁移步。
- [ ] 不把 Document/Database 句柄塞入可序列化类型。
