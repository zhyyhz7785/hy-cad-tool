# Shell / Resources

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**壳层 WPF 资源字典**：`BlenderTheme`、道路设计补充样式、控件库/第三方桥接的 `LibraryResources` 等，供 `BlenderPanel`、首选项、Feature 子面板在**局部** `MergedDictionaries` 中合并。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 通用画刷/模板/样式，与业务无关的视觉效果 | 仅某一 Feature 独占且不会复用 → 该 Feature 内资源 |
| XAML 字典 | 在 `PluginInitializer` 中挂 `Application.Current.Resources` 全局主题（**禁止**；见 WPF 规则） |

## 目录结构

- `BlenderTheme.xaml`：主主题片段。
- `RoadDesignerStyles.xaml`：道路相关壳层样式（若存在）。
- `LibraryResources.xaml`：库级合并入口。

## 命令与入口

无。

## 依赖与协作

- **与 `HyCAD.BlenderUI`**：共享控件时 pack URI 与程序集名正确；见 pitfall skill XDG-0001。
- **与 ReCall/AdWindows**：问题先查 `Recall.ResolveAssembly` 黑名单与 `bin` 中多余宿主 DLL，勿加 Badge 预热。

## 开发与审查要点

- [ ] `SolidColorBrush` 与 `BlenderThemeManager` 防 Freeze 策略一致，避免切主题时 `InvalidOperationException` 升级为原生错误。
- [ ] 跨字典 `StaticResource` 顺序在合并链中可解析（见 `wpf-paletteset-resource-pitfalls`）。
