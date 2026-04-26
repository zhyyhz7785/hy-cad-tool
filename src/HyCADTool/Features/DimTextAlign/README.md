# DimTextAlign

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**标注文字防重叠**：对给定 `Dimension` 集合做迭代平移，减轻测量文字互相遮挡。当前以**可复用服务**为主。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `Services/DimensionTextAlignService` | 创建/拆分尺寸实体 → `AcadDimension` |
| 纯算法 + AutoCAD 写回文字位置 | 配筋业务标注 → `DimensionForReinforcement` |

## 目录结构

- `Services/DimensionTextAlignService.cs`：`AlignDimensionTexts(ObjectId[] dimIds, out string diagnostics)` 等 API

## 命令与入口

- **当前无独立 `commands.json` 键（以仓库为准）**：由其他命令或服务在流程内调用。
- 若新增对外命令：在 `commands.json` 增加映射，方法名默认 `Execute`，并更新本 README。

## 依赖与协作

- **Shared/AutoCAD**：实体打开、几何读入。
- **Shell**：一般不直接依赖；调用方可为任意 Feature 命令。

## 开发与审查要点

- [ ] 平移算法变更时补充单元测试（`HyCADTool.Tests`）若可纯测部分。
- [ ] UI 线程：避免在 `Align` 内 `WriteMessage` 高频刷屏（总纲调试约束）。
- [ ] 与 `PresentationTraceSources` Binding 无关，但面板若调用本服务需注意 Dispatcher。
