# Misc

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**杂项命令**：未单独开专域的小工具（轴线文字、筏板厚度、打断/合并、按层画线等）+ **DCEL / OverKill 部分入口**（外壳或分类归属）+ **调试/测试类** 命令。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| 单文件或少量文件即可完成的命令 | 成体系的 Domain/Services 树 → 独立 `Features/<专域>` |
| `DCELCommand` / `DCELSettingsCommand`（调用 `../DCEL`） | DCEL 内核 → `../DCEL` |

## 目录结构

- 根目录：各 `*Command.cs`（含 `DCELCommand`、`DCELSettingsCommand`）

## 命令与入口（`src/ReCall/commands.json`，节选）

| 键 | 类 |
|----|-----|
| `hyAxis` | `AlignedAxisTextCommand` |
| `HyRT` | `RaftThicknessTextCommand` |
| `hydl` | `DrawLinesOnEachLayerCommand` |
| `HYBL` | `BreakLinesCommand` |
| `HYBC` | `BreakCurvesCommand` |
| `HYJP` | `JoinParallelLinesCommand` |
| `HYDCEL` | `DCELCommand` |
| `HYDCELSET` | `DCELSettingsCommand` |
| `hySeg3` | `Road.RoadThreeSegmentChainCommand`（道路实现，杂项分类） |

**OverKill**：`HYOV` / `HYOVSET` 映射到 `OverKill.Commands.OverKillCommand`（实现代码在 `../OverKill`）。

**测试类**：`HYLOCATETIF`、`CHECKWPF` 等 `category` 为「测试」。

## 依赖与协作

- **DCEL**：DI 解析 `IDCELBuilderService` / `IDCELRenderer`。
- **Road**：`RoadThreeSegmentChainCommand` 本体在 `Features/Road`。
- **Shell.Configuration.User**：部分命令读取用户设置。

## 开发与审查要点

- [ ] 新命令若超过 ~3 文件或出现稳定 Domain，**迁出** `Misc` 建专域。
- [ ] 禁止在本目录堆积与「杂项」无关的大型服务。
- [ ] 更新 `commands.json` 后核对 Blender `category` 与 `CommandCatalog` 预设顺序。
