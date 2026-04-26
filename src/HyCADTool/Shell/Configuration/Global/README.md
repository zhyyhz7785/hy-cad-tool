# Shell / Configuration / Global

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**跨功能默认**：比例（`ScaleConfig`）、公差、路径、文字样式、尺寸、线型与线型目录、MLeader 等，聚合在 `GlobalConfiguration` 中，作为全插件级起点。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 默认值与 `IsValid` 类轻量校验 | 用户机上次写入值 → 见 `../User/` |
| 与 UI 无绑定的纯数据 | 具体命令里的临时参数 |

## 目录结构

- `GlobalConfiguration.cs`：根聚合对象。
- `ScaleConfig.cs`、`ToleranceConfig.cs`、`PathConfig.cs` 等。
- `TextStyleConfig.cs`、`DimensionStyleConfig.cs`、`MLeaderStyleConfig.cs`、`LayerConfig.cs`。
- `HyLinetypeNames.cs`、`LinetypeCatalogModels.cs`、`StylesConfig.cs`。

## 命令与入口

无；由配置服务在启动或设置变更时合并进工作配置。

## 依赖与协作

- **与 User**：User 层可覆盖同名字段，合并策略在配置服务中实现（以代码为准）。

## 开发与审查要点

- [ ] 新增全局字段时同步默认工厂方法与校验。
- [ ] 比例/单位与 Feature 中「红字直接 mm、绿字×Scale」规则一致（见热启动规则 §坐标与单位）。
