# Shell / Configuration

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**全局/模块/用户** 三层的强类型配置模型与默认值：与 JSON/用户目录持久化配合（具体读写多在 `IConfigurationService` 实现侧），为设置面板和 Feature 提供统一 schema 入口。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| `Global/`、`Modules/`、`User/` 下的 POCO 与校验 | 业务实体（桩位、线位）若仅某 Feature 使用 → 该 Feature 内 |
| 中立命名（比例、公差、路径） | 新增 `Shell → Features.*.Domain` 引用（**禁止**，见主 README「已知耦合」） |

## 目录结构

- [`Global/`](./Global/README.md)：`GlobalConfiguration`、比例、公差、路径、文字/尺寸/线型等**全局**默认。
- [`Modules/`](./Modules/README.md)：按**模块/业务域**分组的配置块（如标高、配筋、桩、基础等）。
- [`User/`](./User/README.md)：**用户**侧覆盖（如图层清单、材料填充、持久化到 hy-settings 的模型）。

## 命令与入口

无命令行键；由应用启动、设置保存或 Feature 经 `IConfigurationService` 读写。

## 依赖与协作

- **与 `../Contracts/`**：`IConfigurationService`、`IModuleConfigService` 等组合使用。
- **历史问题**：`PileConfiguration` 等若仍引用 `Features.Pile.Domain`，新代码禁止效仿；搬迁见主 `Shell/README.md`。

## 开发与审查要点

- [ ] 新模块配置块放 `Modules/`，并文档化与 JSON 路径的对应关系（若有）。
- [ ] 不在此层做 AutoCAD 事务或 `Editor` 调用。
