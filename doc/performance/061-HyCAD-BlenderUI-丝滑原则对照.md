# HyCAD.BlenderUI × Blender 丝滑感 · 原则对照

Blender 的交互顺滑主要来自**架构选择**，而非单点技巧。下表将 **7 条原则** 与 **本仓库 HyCAD.BlenderUI / Refactored** 的落实情况对照，供后续重构 backlog 使用。

---

## 已落实（保持）

| # | 原则 | Blender 侧含义 | HyCAD 侧落地 |
|---|------|------------------|--------------|
| 1 | **全局可变主题** | 改 `themes[0]` 字段即全 UI 一致变色 | [BlenderThemeManager.cs](../../HyCAD.BlenderUI/Theming/BlenderThemeManager.cs) v3：`SolidColorBrush.Color` DP + unfrozen brush（`Opacity` self-binding 防 Seal） |
| 2 | **Operator 状态机** | poll / invoke / modal，与入口解耦 | [WM/Operators/Operator.cs](../../HyCAD.BlenderUI/WM/Operators/Operator.cs)、`OperatorRegistry`、`DispatchOperator` |
| 3 | **KeyMap 集中** | 快捷键一张表，可上下文过滤 | [WM/KeyMap/KeyMap.cs](../../HyCAD.BlenderUI/WM/KeyMap/KeyMap.cs)、[BlenderInputRouter.cs](../../HyCAD.BlenderUI/WM/Dispatch/BlenderInputRouter.cs) |
| 4 | **搜索 / F3 式命令检索** | 减少鼠标路径 | `HyB`、[HyBlenderPanel](../../HyCADTool.Refactored/Presentation/Views/HyBlenderPanel.xaml.cs) |
| 5 | **区域 / 工作区层级** | 脏区思想、布局稳定 | [Screen/Areas](../../HyCAD.BlenderUI/Screen/Areas)、`BlenderScreen` 等 |

---

## 待落实 / 可增强（Backlog）

| # | 原则 | 说明 | 建议方向 |
|---|------|------|----------|
| 6 | **Modal 增量预览** | Blender 修改器偏增量更新，非整屏擦除 | AutoCAD 侧更多 **Transient** / diff 预览，减少全量 XOR JIG；与具体道路/对齐命令迭代 |
| 7 | **Immediate Mode UI 等价** | Blender UI 无 WPF measure/arrange 级联 | 减少面板 **Visual tree 深度**：扁平化 `ItemsControl` + `DataTemplate` 嵌套；大列表虚拟化（`VirtualizingStackPanel`） |

---

## 与 AutoCAD 宿主相关的边界（非 BlenderUI 能单独解决）

- **视口平移/缩放**：由 AutoCAD DirectX 管线负责；若 Layer 2 无插件仍卡，优化重点不在 WPF。
- **单线程 STA**：WPF `Binding` 同步错误、过重 `InvokePreHooks` 仍会阻塞 UI —— 见 `ReCallClass.Invoke` 与 `SettingsPanelViewModel` 钩子（性能调优在 ReCall/Refactored，不仅 BlenderUI）。

---

## 参考文档

- 性能测试步骤：[059-HyCAD-插件性能诊断-测试手册.md](059-HyCAD-插件性能诊断-测试手册.md)
- 结果记录：[060-HyCAD-插件性能-结果汇总模板.md](060-HyCAD-插件性能-结果汇总模板.md)
