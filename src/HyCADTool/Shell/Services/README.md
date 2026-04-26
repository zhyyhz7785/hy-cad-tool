# Shell / Services

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**壳层可复用的小服务**（非 DI 单例大接口集）：如命令面板/设置中的**命令搜索、过滤、排序**（`CommandSearchService`），不绑定单一 Feature 领域。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 纯内存检索、与 `CommandCatalog` 协同的辅助类 | 长驻 AutoCAD 的服务单例：若全应用共享且已是接口，放 `Contracts` + 实现于 `App` 或 `Infrastructure` |
| 与 UI/命令发现相关的算法 | 读图/写图服务 → `Features`/`Shared` |

## 目录结构

- `CommandSearchService.cs`：命令名/说明搜索（以实际实现为准）。

## 命令与入口

无独立键；由设置/命令面板的 ViewModel 在运行时调用。

## 依赖与协作

- **与 `../Commands/CommandCatalog.cs`**：搜索数据源通常来自此。

## 开发与审查要点

- [ ] 保持无 UI 线程阻塞；大数据量时 debounce 或虚拟化在调用方（VM/View）处理。
- [ ] 新服务若膨胀为横切基础设施，考虑迁出 `Services` 并接口化，避免 `Shell` 成杂物间。
