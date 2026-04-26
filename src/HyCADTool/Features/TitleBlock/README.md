# TitleBlock

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**图签与布图**：最小包围矩形、绘制图签、布局视口创建与紧凑排列、与 **聚类** 能力结合的多步流程（`MBRCommand`）。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `DrawTitleBlockCommand`、`CreateLayoutViewportsCommand`、`PackViewportsCommand`、`MBRCommand` | 聚类算法本体 → `../Cluster` |
| 图签几何与视口服务 | 全局文字/标注样式 → `Shell/Configuration` |

## 目录结构

- 根目录：上述命令类
- `Services/`：如 `DrawingSheetTitleDrawer`、`TitleBlockService`（以仓库为准）

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 |
|----|-----|
| `HYMBR` | `MBRCommand` |
| `HYMBRD` | `DrawTitleBlockCommand` |
| `HYMBRC` | `CreateLayoutViewportsCommand` |
| `HY_PackViewports` | `PackViewportsCommand` |

Blender 分类：`图框视口`。

## 依赖与协作

- **Cluster**：`IClusteringService` 等（`MBRCommand` 流程）。
- **Shell**：`Shell.Configuration.User`（`TitleBlockService` 等）。
- **首选项**：聚类参数在 Shell `ClusterPanel` / 设置页。

## 开发与审查要点

- [ ] 执行 MBR 前确认聚类默认层已注册（`LayerBuiltinDefaults`）。
- [ ] 布局空间与 Model 空间切换注意 `LockDocument` 模式。
- [ ] 表格与块属性写入遵循 AutoCAD API 顺序（Table `SetSize` 等）。
