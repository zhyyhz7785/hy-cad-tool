# Reinforcement

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**钢筋通用**：双线绘制、偏移多段线、弯钩/锚固、延伸/快速延伸、截断、多重引线标注、外侧筋、选钢筋文字等。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `DrawReinforcementCommand` 等命令、`ReinService`、`Domain` 工具 | 底板多步面板流 → `BaseRein` |
| 读 `SettingsPanelViewModel` 生成 `ReinParameters` | 配筋**尺寸**专域 → `DimensionForReinforcement` |

## 目录结构

- `Domain/`：参数、枚举、纯算法（`ReinforcementUtils` 等）
- `Services/ReinService.cs` 等
- 根目录：各 `*Command.cs`

## 命令与入口（`src/ReCall/commands.json`，节选）

| 键 | 说明 |
|----|------|
| `gj` | 绘制钢筋 |
| `gg` | 偏移多段线 |
| `ggj` | 外侧钢筋 |
| `g1` / `g2` | `ReinAddAnchorCommand`（构造参数区分弯钩向） |
| `ge` / `ge1` | 延伸 / 快速延伸 |
| `gd` | 截断 |
| `gb` / `gb1` / `gb2` | `MleaderReinCommand` 多模式 |
| `hysrt` | 选钢筋文字 |

完整列表以 `commands.json` 为准。

## 依赖与协作

- **Shell**：`IStyleService`、`SettingsPanelViewModel.Current`、`Shell.Contracts`。
- **Shared**：`IPolygonOffsetService`、`EntityExtensions` 等。

## 开发与审查要点

- [ ] **禁止并行**写图：注释已说明 AutoCAD STA + 单例服务（见 `DrawReinforcementCommand`）。
- [ ] 参数红/绿表：直接 mm vs ×Scale，见 `01-AI热启动模式`。
- [ ] 改命令键或类名同步 JSON + Ribbon/菜单三处元数据。
