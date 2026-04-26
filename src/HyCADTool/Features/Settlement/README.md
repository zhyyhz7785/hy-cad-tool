# Settlement

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**基础沉降** 计算：输入模型、土层与桩土参数、独立窗口/面板展示结果与报告生成（与 C1 联调入口独立）。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `Commands/SettlementCalculationCommand`、`SettlementReportGenerator`、面板 VM | 全局 hy-settings 字段定义 → `Shell` / `docs/settings` |
| `Domain` 输入输出与 `PileStructuralService` 等 | Markdown 报告模板若共用 → `docs/templates/settlement/` |

## 目录结构

- `Commands/SettlementCalculationCommand.cs`
- `ViewModels/`、`Views/`：面板与窗口
- `Domain/`：输入模型、服务
- 根目录：报告生成器等

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 |
|----|-----|
| `HYJC` | `Commands.SettlementCalculationCommand` |
| `hySC` | 同上（别名入口） |

**联调约定**：开发机可直接 **`HYJC`** / **`hySC`** 打开 UI，无需改 `TestCommand`（见 `.cursor/rules/01-AI热启动模式.mdc`）。

## 依赖与协作

- **文档**：`docs/hycad-internals/HYJC-README.md`、`docs/templates/settlement/00-09-*.md`（总纲 §十）。
- **Shared**：如几何/报表依赖以实际引用为准。

## 开发与审查要点

- [ ] 长计算避免阻塞 UI：使用进度或后台模式（若已有模式勿破坏）。
- [ ] 输出文件路径与用户目录权限。
- [ ] 新增命令键时同步 `commands.json` + 本 README。
