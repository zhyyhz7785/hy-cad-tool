# HyCADTool 设置与配置文档（`doc/Setting`）

本目录汇总 **HyCADTool.Refactored**（及插件宿主）相关的**配置文件位置、字段含义、加载顺序与代码入口**，面向开发与排障，尽量与当前源码一致。

**文档日期**：2026-04-21  

## 阅读顺序建议

| 顺序 | 文档 | 内容概要 |
|------|------|----------|
| 1 | [01-配置体系总览](./01-配置体系总览.md) | 两套 JSON 体系、启动流程、路径总表 |
| 2 | [02-hy-settings-json-字段手册](./02-hy-settings-json-字段手册.md) | 主用户设置文件字段级说明 |
| 3 | [03-config-json-与全局服务](./03-config-json-与全局服务.md) | DLL 旁 `config.json`、全局/模块配置服务 |
| 4 | [04-图层与样式初始化](./04-图层与样式初始化.md) | 用户图层表（`UserLayerSettings`）、默认表、道路解析、样式初始化 |
| 5 | [05-模块独立设置文件](./05-模块独立设置文件.md) | 桩基、沉降等独立 JSON |
| 6 | [06-扩展排障与约定](./06-扩展排障与约定.md) | 自动保存、多文档、Scale 真相源、迁移注意 |
| 7 | [07-hy-settings-示例骨架](./07-hy-settings-示例骨架.md) | 全字段默认 JSON 骨架（便于手工合并） |
| 8 | [08-硬编码图层与样式改造清单](./08-硬编码图层与样式改造清单.md) | 后续可替换的硬编码与已落地项 |
| 9 | [09-新增设置扩展规范](./09-新增设置扩展规范.md) | 新参数接入 hy-settings / 图层语义约定 |

## 与仓库内其他文档的关系

- `HyCADTool.Refactored/Docs/01-配置系统报告-2026-02-11.md`：较早的「配置系统完整指南」，本目录在其基础上按**文件维度**拆分并补全**图层/道路**细节。
- `doc/034-配置映射与扩展点-2026-02-15-000110.md`：若存在，可与本目录交叉引用扩展点。

## 源码锚点（快速跳转）

| 主题 | 主要路径 |
|------|----------|
| 主设置 VM + `hy-settings.json` | `HyCADTool.Refactored/Presentation/ViewModels/SettingsPanelViewModel.cs` |
| 配置总协调 + Scale | `HyCADTool.Refactored/Infrastructure/Configuration/ConfigurationService.cs` |
| `config.json` 加载 | `HyCADTool.Refactored/Infrastructure/Configuration/JsonConfigurationLoader.cs` |
| 插件启动、样式与图层 | `HyCADTool.Refactored/Presentation/PluginInitializer.cs` |
| 道路图层名/色 | `HyCADTool.Refactored/Infrastructure/AutoCAD/Xdata/HyRoadLayers.cs` |
| 示例 `config.json` | `HyCADTool.Refactored/config.json` |
