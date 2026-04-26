# OverKill

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**重复与重叠实体清理**：在 OverKill 思路基础上结合圆角等步骤，整理多段线/线，支持标记层辅助审阅与设置窗。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `Commands/OverKillCommand` 主流程、`Domain/`、`Services/` | 杂项键名注册壳 → `commands.json` 指向本目录类 |
| `Views/HyovSettingsWindow.xaml` | 通用几何库 → `Shared` |

## 目录结构

- `Commands/OverKillCommand.cs`
- `Domain/`、`Services/`：几何判断、圆角、图层标记
- `Views/HyovSettingsWindow.xaml`：设置 UI
- `Doc/`：迁移与设计笔记（时间戳文件名）

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 方法 | 说明 |
|----|------|------|
| `HYOV` | `Execute` | 主清理 |
| `HYOVSET` | `ExecuteSettings` | 打开设置 |

当前 JSON 中 `category` 为「杂项」（与 Blender 分组一致，以文件为准）。

## 依赖与协作

- **Misc**：无代码依赖；仅命令表从 Misc 用户心智上「相邻」。
- **Shared**：图层、几何。

## 开发与审查要点

- [ ] 大批量删除须考虑 `Undo` 与性能。
- [ ] 设置窗 WPF：本地 merge 主题，遵循 PaletteSet 规则（见 `05-AdWindows`）。
- [ ] 子目录 `Doc/` 仅作档案，**不替代** `doc/` 命名规范的新文档。
