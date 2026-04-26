# Shell / Contracts

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**壳层公共接口**：输入抽象、配置、样式、全局配置、模块配置、图层服务等，供 `Features`、`BlenderPanel` 与 `App` 的 DI 注册共同依赖，减少具体类型耦合。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| `IInputService`、`IConfigurationService`、`IStyleService`、`IGlobalConfigService` 等 | 接口的 Autofac 实现类（通常在 `App` 或 Infrastructure 程序集） |
| 与 AutoCAD 无直接绑定的纯契约 | 业务仓储接口若仅某 Feature 使用 → 该 Feature 内 |

## 目录结构

- `IInputService.cs`、`IConfigurationService.cs`、`IModuleConfigService.cs`。
- `IStyleService.cs`、`ILayerService.cs`、`IGlobalConfigService.cs`。

## 命令与入口

无；接口在解析 `PanelManager`、VM、命令时由 DI 注入。

## 依赖与协作

- **与 `../Configuration/`**：实现侧读写配置模型，契约在此声明能力边界。
- **与 Features**：Feature 引用 Shell 的接口视为「机箱服务」可接受；避免 Feature 为接口补充实现时反向污染契约定义。

## 开发与审查要点

- [ ] 新接口前评估是否更属于 `Shared`（若与 Shell 无必然关系）。
- [ ] 接口变更同步所有实现者（编译器会辅助）与文档中的 JSON/设置项说明。
