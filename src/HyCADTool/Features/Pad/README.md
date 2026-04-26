# Pad

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**垫层 / 板域**：由直线或多段线生成填充域，并支持单/批量多边形替换；对应制图习惯中的筏板垫层等。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `CreatePadFromLineCommand`、`CreatePadFromPolylineCommand`、`ReplacePolygon*` | 道路横断面结构层 → `Road/CrossSection` |
| 命令内图层与实体类型选择 | 全局图层常量 → `Shell/Configuration` + `PluginInitializer` |

## 目录结构

- 根目录：上述命令类（体量小）

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 |
|----|-----|
| `abrc` | `ReplacePolygonCommand` |
| `abrcs` | `ReplacePolygonBatchCommand` |
| `HyDcL` | `CreatePadFromLineCommand` |
| `hyDcP` | `CreatePadFromPolylineCommand` |

Blender 分类：`多段线垫层`。

## 依赖与协作

- **Shared**：多段线、面域、事务。
- **Shell.Configuration.User**：若读取用户默认厚度等。

## 开发与审查要点

- [ ] 批量替换注意单事务 vs 分批提交（避免一次失败全丢）。
- [ ] 与 `BaseRein` 底图整理步骤区分职责，避免重复实现「选多段线」逻辑。
