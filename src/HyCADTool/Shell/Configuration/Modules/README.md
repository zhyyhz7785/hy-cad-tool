# Shell / Configuration / Modules

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**按功能模块划分的配置块**：如标高、配筋、桩基、基础、设备基础等，被设置面板与对应 Feature 共享 schema。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 模块级 POCO（`ElevationConfiguration` 等） | 仅某一 Feature 内部使用的 DTO 若与 JSON 无对应 → 放 Feature |
| 与 `HySettings` 字段对齐的模型 | 对 `Features.*.Domain` 类型的直接引用（**新代码禁止**；历史如 `PileConfiguration` 待迁） |

## 目录结构

- `ElevationConfiguration.cs`、`ReinforcementConfiguration.cs`、`PileConfiguration.cs`、`FoundationConfiguration.cs` 等（以仓库实际文件为准）。

## 命令与入口

无；随用户设置保存/加载，或由 Feature 经配置服务读取。

## 依赖与协作

- **与 `../Preferences/`**：子 Tab 的 VM 常绑定到此处类型的实例或子集。

## 开发与审查要点

- [ ] 新增模块配置时检查是否应对应 `docs/settings` 或内部 JSON 键名，避免与 Feature 内重复定义。
- [ ] 若必须依赖 Domain 类型，在 PR 中单独说明并走搬迁计划，勿复制粘贴实体进 Shell。
