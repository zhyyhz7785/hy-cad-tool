# BaseRein

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**底板配筋** 多步工作流：底图整理、区域选择、配筋面积、绘制钢筋、标注等；带专用面板，与通用 `Reinforcement` 命令互补。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| 底板多步流程、`BaseReinforcementService`、面板 VM/View | 通用双线钢筋 `gj/gg/...` → `Reinforcement` |
| 本域配置与步骤编排 | 全局比例/直径等**持久化字段**定义在 `SettingsPanelViewModel`，本域只消费 |

## 目录结构

- `Views/BaseReinPanel.xaml`：面板 UI
- `ViewModels/BaseReinPanelViewModel.cs`：状态与命令绑定
- `Services/BaseReinforcementService.cs` 等：各步骤实现

## 命令与入口

- **无独立 `commands.json` 键（以仓库当前为准）**：典型入口为 **HyB 面板 →「底板配筋」分类 Tab**，或 `ShowPanelCommand.ShowBaseReinPanel()`（见 `Shell/Commands/ShowPanelCommand.cs`）。
- 与 **设置** Tab 联动：`Shell/Preferences/HySettingsViewModel` 持有 `BaseReinVm`（机箱嵌 Feature VM）。

## 依赖与协作

- **Shell**：`IStyleService`、`SettingsPanelViewModel`、统一面板 Tab 键 `"底板配筋"`。
- **Shared**：几何、图层、选集。

## 开发与审查要点

- [ ] **坐标与单位**：面板参数区分「直接用 mm」与「× Scale」两类，见 `.cursor/rules/01-AI热启动模式.mdc` 坐标表。
- [ ] 锁定层写入使用 `LayerLockScope` 等模式（道路/通用 pitfalls）。
- [ ] 新增对外命令时补 `commands.json` + 文档双处更新。
