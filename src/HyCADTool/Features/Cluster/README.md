# Cluster

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**实体聚类**：按包络、距离等分组，输出到约定图层与标注；服务 `TitleBlock/MBRCommand` 等图签流程，并在首选项中暴露参数。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|--------------|
| `Domain/` 模型与 `IClusteringService` 实现 | 图签几何与视口排布 → `TitleBlock` |
| `Services/` 输入解析、分析、出图 | Shell 侧 `ClusterPanel` UI 壳 → `Shell/BlenderPanel` |

## 目录结构

- `Domain/`：配置、结果模型、聚类算法接口与实现
- `Services/`：输入、分析、绘制、标注

## 命令与入口

- **通常无独立 `commands.json` 业务键**：能力由 **图签命令**（如 `HYMBR`）或 **首选项 / HyB「块引线」Tab** 间接使用；算法入口以服务形式被解析注入。
- `Shell/BlenderPanel/ClusterPanelViewModel` 消费本域服务（见 `Shell` 与 `AutofacModule`）。

## 依赖与协作

- **Shell**：`ClusterPanelViewModel`、设置页聚类参数。
- **Shared**：`LayerBuiltinDefaults`（如 `ClusterMain` 等语义层名）。

## 开发与审查要点

- [ ] 修改聚类参数时同步 UI 默认值与持久化键。
- [ ] 多文档：`PilePanelViewModel` 等与 `PanelManager` 文档事件类似，聚类若缓存需按文档键失效。
- [ ] 禁止新增 `Shell → Features.Cluster.Domain` 以外的反向耦合；配置 schema 尽量留在 Feature 或中立层。
