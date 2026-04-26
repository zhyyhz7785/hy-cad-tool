# Shell / Ribbon

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**AutoCAD 功能区与 CUI/菜单** 的构建与事件桥接：`HyCadRibbonBuilder`、`RibbonCommandHandler`、以及菜单 JSON/CUI 辅助（`CuiMenuBuilder`），把 Ribbon 点击转到与面板一致的命令分发（`CommandDispatcher`）。**Palette 标题条**剥离等宿主修饰见 `PaletteTitleBarStripper`。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 构建 Ribbon Tab/面板项、写命令宏字符串 | 业务命令类实现体 |
| 与 `CommandCatalog` 对齐的**入口键** 一致 | 在 Ribbon 中直接 new Feature Service 并跑事务 |

## 目录结构

- `HyCadRibbonBuilder.cs`：主 Ribbon 装配。
- `RibbonCommandHandler.cs`：项点击处理。
- `CuiMenuBuilder.cs`：菜单/CUI 相关。
- `PaletteTitleBarStripper.cs`：PaletteSet 视觉修整（与宿主 UI 强相关，慎改）。

## 命令与入口

- Ribbon 调用的**仍是**已注册的 AutoCAD 命令名或 `CommandDispatcher.Send` 的 key，与 `commands.json` 一致；无本目录独立键名。

## 依赖与协作

- **与 `../Commands/CommandDispatcher`**：保持「一处键名」原则。
- **与多文档**：Ribbon 全局存在，命令执行时仍以 `MdiActiveDocument` 为准（各命令内部已处理则不必在此重复）。

## 开发与审查要点

- [ ] 新按钮优先复用 `CommandCatalog` 元数据，避免键名与面板漂移。
- [ ] 改 `PaletteTitleBarStripper` 时做宿主版本回归，避免与 AutoCAD 内部控件假设冲突。
