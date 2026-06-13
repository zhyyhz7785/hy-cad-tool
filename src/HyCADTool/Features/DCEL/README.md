# DCEL

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**Doubly Connected Edge List（双向连通边表）**：平面剖分、面构造与可视化内核；从选定曲线构网并绘制。**AutoCAD 命令外壳不在本目录**。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `Domain/` 图结构、构网、`DCELSettings` | 选集、命令行提示、`Execute` 外壳 → `../Misc/DCELCommand.cs` |
| `Services/DCELRenderer` 等落图渲染 | 通用多段线编辑 → `Misc` 其他命令 |

## 目录结构

- `Domain/`：`DCELGraph`、半边/面/顶点、`DCELBuilderService`、`DCELSettings`、`IDCELBuilderService`
- `Services/`：`DCELRenderer`、`IDCELRenderer`、`DCELPipelineRunner`、`DCELTimingReporter`

## 命令与入口

| 键 | 位置 | 说明 |
|----|------|------|
| `HYDCEL` | `Misc/DCELCommand` | 选曲线 → Builder + Renderer |
| `HYDCELSET` | `Misc/DCELSettingsCommand` | 读写 `DCELSettings`（含 VerboseTiming 详细耗时） |

DI：`IDCELBuilderService` / `IDCELRenderer` 在 `AutofacModule` 注册（以项目为准）。

## 依赖与协作

- **Misc**：唯一对外命令类，依赖本目录接口实现。
- **Shared**：几何/AutoCAD 转换（按实际 `using`）。

## 开发与审查要点

- [ ] 新功能优先加在 `Domain`/`Services`；命令层仅增参或转发。
- [ ] 若移动命令类到 `DCEL/Commands/`，须同步 `commands.json` 的 `type` 全名与 ReCall 占位说明。
- [ ] 长耗时逻辑注意 STA 与禁止并行写库（与总纲开发循环一致）。
