# Shell / Behaviors

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**可附加在控件上的 WPF/Blend 行为**：与具体业务弱耦合、可在多处复用的输入/拖放等逻辑。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| `Behavior<T>` 派生、焦点/拖放/附加属性 | 具体 Feature 面板的业务 VM |
| 纯 UI 交互胶水 | 需要 AutoCAD 事务的几何操作 → `Features`/`Shared` 服务层 |

## 目录结构

- `OutlinerDragDropBehavior.cs`：Outliner 等树形控件的拖放行为。

## 命令与入口

无命令行键；由 XAML 中 `Interaction.Behaviors` 或等效附加方式挂到视图上。

## 依赖与协作

- 使用方在 `Features` 或 `BlenderPanel` 的 XAML 中声明；不反向引用具体 Feature 命名空间以外的必要最小集合。

## 开发与审查要点

- [ ] 行为内异常不吞没导致静默失败；关键路径可记录一次。
- [ ] 避免在 `Behavior` 里长期持有大对象引用导致泄漏。
