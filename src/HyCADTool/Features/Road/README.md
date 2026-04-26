# Road

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**道路全模块**（最大子域）：平面线位（PI / 工作台 / LandXML）、纵断面、横断面与模板、项目树与工程数据、交叉口与人行横道/缘石坡道/盲道、标线、JSON/ glTF / LandXML 导出、还原点与数据整理等。**60+ 条**命令，`roadPanelGroup` 驱动 Hy 面板道路 Tab 五区折叠。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `PlanAlignment/`、`CrossSection/`、`PlanProfile/`、`Intersection/`、`Marking/`、`Plan/`、`Export/`、`Events/`、`Shared/` | 通用几何内核 → `Shared/Geometry` |
| `HyRoad` XData、`.roaddesign.json`、图层语义 | 全局图层注册表 → `PluginInitializer.GetRequiredLayers` 与 `HyRoadLayers` **必须同步**（pitfalls） |

## 子目录规划（L3 索引）

详见 **[ROAD-SUBMODULES.md](./ROAD-SUBMODULES.md)**。与总纲 §三 对照：

| 子目录 | 职责摘要 |
|--------|----------|
| `PlanAlignment/` | 线位、PI、工作台 PaletteSet、桩号/方程/导出 CSV、LandXML 导入导出 |
| `PlanProfile/` | 纵断面 FG/EG、PVI、标注 |
| `CrossSection/` | 横断面 v2 绘制、模板、结构层、预设存取 |
| `Intersection/` | 交叉口、编辑、路缘链、人行横道 |
| `Marking/` | 停止线、车道线、导流箭头 |
| `Plan/` | 项目树、平面出图、历史/还原 |
| `Export/` | LandXML、JSON、glTF 等 |
| `Events/` | 道路事件总线 |
| `Shared/` | 短别名 `RoadCommandShortAliases.cs`、文件 watcher、跨模块常量 |

## 命令与入口

- **单一事实源**：`src/ReCall/commands.json` 中 `type` 前缀 `HyCADTool.Features.Road`（含子命名空间）的条目。
- **短别名**：`Shared/RoadCommandShortAliases.cs`（与 `CommandDispatcher`、Ribbon 一致）。
- **独立 PaletteSet**（非唯一 HyB）：`Shell/BlenderPanel/PanelManager` 创建 **路线工作台**、**项目树**、**横断面绘制** 等，宿主视图类型在 `Features/Road/.../Views`。
- **内部/勿手输**：部分 `hyRoadAln*` 仅供工作台按钮 `SendStringToExecute`（JSON `tooltip` 已标注）。

## 依赖与协作

- **Shell**：`CommandDispatcher`（工作台发命令）、`PanelManager`、道路设置子页。
- **Domain**（`HyCADTool.Domain`）：道路模型与服务接口（线位、项目等，以代码引用为准）。
- **Shared.AutoCAD**：`RoadProjectRegistry`、`LayerLockScope`、`HyRoadLayers`。

## 开发与审查要点

- [ ] 图层：改常量必双改 `HyRoadLayers` + `GetRequiredLayers`，否则静默落 0 层。
- [ ] 锁定层写入：`LayerLockScope`，`finally` 不向外抛。
- [ ] `hyRoadAlnStation` / `rSt`：`CommandDispatcher.Send` 需额外 `\n`（见 `Shell/Commands/CommandDispatcher.cs`）。
- [ ] 深入单主题可配合 `docs/road-design/041-道路设计-AI上下文索引.md`（总纲 §十）。
