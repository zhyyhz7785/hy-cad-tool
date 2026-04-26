# AcadDimension

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**通用 AutoCAD 尺寸标注** 的编辑类能力：加点、拆分、对齐等。与配筋业务无关，区别于 `DimensionForReinforcement`（配筋专用尺寸）。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| 与 `Dimension` 实体交互的通用编辑命令 | 钢筋/洞口等业务语义标注 → `DimensionForReinforcement` |
| 命令类与拾取/事务编排 | 纯几何算法若跨域复用 → `Shared/Geometry` |

## 目录结构

- `Commands/`：`AddVertexAtIntersectionsCommand`、`SplitDimensionCommand`、`DimensionAlignCommand` 等

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 |
|----|-----|
| `hydimA` | `Commands.AddVertexAtIntersectionsCommand` |
| `sd` | `Commands.SplitDimensionCommand` |
| `ddaa` | `Commands.DimensionAlignCommand` |

注册与占位符流程：`.cursor/skills/hycad-new-command-registration/SKILL.md`。

## 依赖与协作

- **Shell**：通常不直接依赖；样式/比例可经 `SettingsPanelViewModel`（由调用方命令上下文决定）。
- **Shared**：AutoCAD 扩展、选集、几何工具。

## 开发与审查要点

- [ ] 不在此目录写 `[CommandMethod]`；反射入口以 `commands.json` 为准。
- [ ] 与配筋尺寸命令区分：审查 `category` 与 UI 文案是否误导用户。
- [ ] 多文档：勿缓存 `Database`，每次从当前 `Document` 取（见 `hycad-project-pitfalls`）。
