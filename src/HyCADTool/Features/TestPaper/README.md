# TestPaper

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**试验 / 纸型排样** 辅助：`PaperH`、`PaperV`、`PaperHH`、`PaperVV` 等，用于版式与联调验证；**非对外稳定业务 API**。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| 试验性质 `Paper*` 类与 `Test` 方法 | 生产图框流程 → `TitleBlock` |
| 占位与排版实验 | 新需求优先落在明确业务 Feature |

## 目录结构

- 根目录：`PaperH.cs`、`PaperV.cs`、`PaperHH.cs`、`PaperVV.cs` 等

## 命令与入口（`src/ReCall/commands.json`）

| 键 | 类 | 方法 |
|----|-----|------|
| `ph` | `PaperH` | `Test` |
| `pv` | `PaperV` | `Test` |
| `phh` | `PaperHH` | `Test` |
| `pvv` | `PaperVV` | `Test` |

Blender 分类：`测试`。

## 依赖与协作

- 尽量少依赖业务 Feature；以独立试验为准。

## 开发与审查要点

- [ ] 新功能默认**不**加在本目录；确属实验须标注并在 PR 说明用途。
- [ ] `Test` 方法异常勿升级为未捕获原生错误（AutoCAD 宿主）。
- [ ] 勿将试验代码路径耦合进生产 `TitleBlock` 主流程。
