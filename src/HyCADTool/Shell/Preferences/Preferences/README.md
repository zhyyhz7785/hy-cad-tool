# Shell / Preferences / Preferences

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**设置面板内各子页** 的 WPF 视图：道路、标高、配筋、桩、底板、聚类、地脚螺栓、设备基础、主题、UI 缩放、尺寸、键位等，与 `../Configuration/Modules` 及对应 VM 数据绑定。目录名与父级 `Preferences` 重复，属历史布局；以路径区分「根级 Preferences」与「子视图 Preferences」即可。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| `*SettingsView.xaml` + code-behind、纯展示与校验提示 | 业务命令 `Execute`、Jig、Entity 服务 |
| 与设置 schema 一致的表单控件 | 把 Application 级 ResourceDictionary 全局合并（禁止） |

## 目录结构

- 各域 `*SettingsView.xaml` / `.xaml.cs`：如 `RoadSettingsView`、`ReinSettingsView`、`PileSettingsView`、`ThemeSettingsView`、`KeyMapSettingsView` 等（以仓库内实际文件列表为准）。

## 命令与入口

无独立键；由父级 `HyPreferencesView` 内 Tab 或导航切换加载对应 `UserControl`。

## 依赖与协作

- **与 `../*.cs` VM**：父目录中的 `HySettingsViewModel` / 各子 VM 提供 `DataContext`。
- **与 `../../Resources/`**：控件样式在局部 `MergedDictionaries` 中引用 `BlenderTheme` 等。

## 开发与审查要点

- [ ] 子视图不直接 `ServiceLocator` 拉取重型服务；经构造函数或主 VM 注入更利于测。
- [ ] 新增子页时同步 `../README.md` 或子域配置 README（若有），避免只改 XAML 不文档化。
