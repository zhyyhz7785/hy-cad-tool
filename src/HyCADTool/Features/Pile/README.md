# Pile

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**桩基**：桩位绘制、按标高分组圆、Voronoi/Lloyd 类优化、分组中心标高文字；带 **`PilePanel`**，与 HyB「桩基」Tab 联动。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `DrawPilesCommand`、`GroupCirclesByElevationCommand`、`PileVoronoiOptimizationCommand` | 底板配筋六步流 → `BaseRein` |
| `PilePanelViewModel` 多文档 `Current` | Shell 配置 schema 中桩模块默认值 → `App/config.json` + `Shell/Configuration/Modules`（历史耦合见 Shell README） |

## 目录结构

- `Views/PilePanel.xaml`、`ViewModels/PilePanelViewModel.cs`
- `Services/`：绘制、分组等
- 根目录：命令类

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 / 方法 |
|----|-----------|
| `HYpile` | `DrawPilesCommand` |
| `HYpileV` | `PileVoronoiOptimizationCommand` |
| `HYpileG` | `GroupCirclesByElevationCommand` |
| `HYpileGT` | `GroupCirclesByElevationCommand.ExecutePlaceElevationTextAtCentroids` |

面板：`ShowPanelCommand.ShowPilePanel()` → Tab `"桩基"`。

## 依赖与协作

- **Shell**：`PanelManager.OnDocumentToBeDestroyed` 调用 `PilePanelViewModel.RemoveDocument`。
- **Shell.Configuration.User**：绘制服务常见依赖。

## 开发与审查要点

- [ ] 多文档缓存键使用文档全路径，与 `SettingsPanelViewModel` 模式一致。
- [ ] 修改 `Shell/Configuration/Modules/PileConfiguration` 时避免再引入 `Features.Pile.Domain` 到 Shell（历史债，见 `Shell/README`）。
- [ ] Voronoi 计算注意数值容差与 **`HyCAD.Geometry`**（如 `Tolerance`）一致性。
