# BlockLeader

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**块** 与 **多重引线** 互转及块颜色批量调整；归类为「块引线」业务，与图签流程可配合使用。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `BlockToMLeaderCommand`、`BlockColorCommand` | 聚类包络与出图 → `Cluster` |
| 引线样式与块引用编辑 | MLeader 样式全局配置 → `Shell/Configuration` |

## 目录结构

- 根目录：命令类为主（体量小，未强制子目录）

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 |
|----|-----|
| `HYc2bl` | `BlockToMLeaderCommand` |
| `HYc2bc` | `BlockColorCommand` |

Blender 面板分类：`category` 为「块引线」。

## 依赖与协作

- **Shell**：命令面板/Ribbon 元数据来自 `CommandCatalog`。
- **Shared**：选集、实体扩展。

## 开发与审查要点

- [ ] 块与引线操作注意事务边界与 `Undo` 粒度。
- [ ] 勿在 Feature 内添加 `[CommandMethod]`。
