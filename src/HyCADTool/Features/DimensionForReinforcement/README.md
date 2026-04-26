# DimensionForReinforcement

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**配筋场景专用尺寸标注**：单段与批量，为钢筋、洞口等加尺寸；业务语义与通用 `AcadDimension` 分离。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `DimensionForReinforcementCommand` / `Batch`、服务实现 | 通用拆分/对齐尺寸 → `AcadDimension` |
| 与配筋图层、样式相关的标注逻辑 | 仅文字避让算法 → `DimTextAlign`（服务级复用） |

## 目录结构

- 根目录：`DimensionForReinforcementCommand.cs`、`DimensionForReinforcementBatchCommand.cs`、`DimensionForReinforcementService.cs` 等（以仓库为准）

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 |
|----|-----|
| `dds` | `DimensionForReinforcementCommand` |
| `ddss` | `DimensionForReinforcementBatchCommand` |

## 依赖与协作

- **Shell**：`Shell.Configuration.User`、`SettingsPanelViewModel`（标注样式、Scale）。
- **Shared**：选集、实体扩展。

## 开发与审查要点

- [ ] 大批量标注注意事务与 `Editor` 提示节奏。
- [ ] 与 `dds/ddss` 旧别名、Ribbon 分组一致，避免重复注册键。
- [ ] 服务类勿膨胀为「上帝对象」；可抽 Domain 纯函数到同目录或 `Shared`。
