# Shell / BlenderPanel

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**统一 Blender 风格侧栏** 的壳：`HyBlenderPanel` 主用户控件、分类 Tab、过滤区、**PanelManager** 管理的 PaletteSet 生命周期及道路等附加 PaletteSet 打开逻辑（与 `ShowPanelCommand` 协同）。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| `HyBlenderPanel*`、`FilterPanel*`、`ClusterPanel*`、导航 VM | 各业务子面板内容 → `Features/<切片>/Views` 与对应 VM |
| `PanelManager`：显示/切 Tab/多 PaletteSet | 业务算法、Entity 写入服务 |

## 目录结构

- `HyBlenderPanel.xaml` / `HyBlenderPanelViewModel.cs`：主侧栏与 Tab 状态机。
- `FilterPanel*`、`ClusterPanel*`：壳级筛选/聚类区。
- `PanelManager.cs`：PaletteSet 创建、停靠、`OpenHyBlenderPanelAndSelectTab` 等（命名空间可能为 `HyCADTool.Presentation`）。

## 命令与入口

| 键/入口 | 说明 |
|---------|------|
| `Hy` / `HyB` 等 | 与 `../Commands/ShowPanelCommand`、`PanelManager` 配合，见 `src/ReCall/commands.json` 与主 Shell README |
| 开发联调 | `TestCommand` 中常调 `ShowHyBlenderPanel`（见 `.cursor/rules/01-AI热启动模式.mdc`） |

## 依赖与协作

- **Feature 侧栏**：通过 DI 与 Tab 内嵌的 `UserControl` 组合；**已知** `PanelManager` 对道路等 Feature 有直接引用，属历史耦合，新代码勿加码（见 `../README.md`「已知耦合」）。
- **主题**：`../Resources/BlenderTheme.xaml` 等，局部 `MergedDictionaries`（见 `05-AdWindows-WPF-PaletteSet宿主` 规则）。

## 开发与审查要点

- [ ] 新增 Tab 时优先通过约定 key，避免在 `PanelManager` 中无限堆 `new` 具体 Feature 视图；长期用插件点收敛。
- [ ] PaletteSet/主题变更遵循 B10/B11 与 ReCall 黑名单，勿改 `ResourceAssembly` 或 Badge 预热。
