---
name: wpf-paletteset-avoid-implicit-styles
description: |
  解决 AutoCAD PaletteSet 宿主 WPF 面板时因 ResourceDictionary 中隐式 Style（TargetType 无 x:Key）污染宿主控件树导致的 native crash（AutoCAD 错误报告弹窗、无托管异常）。适用于 HyCADTool.Refactored 所有 PaletteSet + UserControl + 主题 ResourceDictionary 场景，以及 Revit / Office TaskPane 等寄生式 WPF 宿主环境。
author: Cursor Agent
version: 1.0.0
date: 2026-04-18
---

# AutoCAD PaletteSet WPF 面板禁用隐式 Style

## Problem

在 AutoCAD PaletteSet 中承载自定义 WPF UserControl 时，若面板的 `ResourceDictionary`（或合并字典）含有 **无 `x:Key` 的隐式 Style**（`<Style TargetType="..."/>`，即使只是 `BasedOn` 复制），执行 `PaletteSet.AddVisual(...)` 时 AutoCAD 会发生 **原生崩溃**：

- 弹出 AutoCAD「错误报告」对话框
- **无任何 .NET 异常** 抛到命令行
- 面板首次 `new HyXxxPanel()` 构造耗时异常长（2+ 秒）

## Context / Trigger Conditions

### 触发条件

- **宿主**：AutoCAD PaletteSet（或 Revit DockablePane、Office TaskPane 等寄生 WPF 宿主）
- **资源结构**：UserControl 的 `Resources` 中合并了主题字典
- **字典内容**：存在形如下列的隐式 Style：
  ```xml
  <Style TargetType="{x:Type Button}" BasedOn="{StaticResource BlenderButton}"/>
  <Style TargetType="{x:Type ScrollBar}" BasedOn="{StaticResource BlenderScrollBar}"/>
  <Style TargetType="{x:Type Expander}" BasedOn="{StaticResource BlenderExpander}"/>
  ```
- **错误信号**：
  - AutoCAD 崩溃 + 错误报告弹窗
  - 调试日志显示 `AddVisual` 调用前最后一条日志后无任何后续打点
  - 同一套控件放在独立 WPF 窗口中不崩（只在 PaletteSet 宿主里崩）

### 为什么会崩

隐式 Style 没有 `x:Key`，WPF 资源查找时沿逻辑树向上冒泡；AutoCAD Palette 宿主本身是 WPF 树的上游节点，其内部的 `Button`/`ScrollBar`/`Expander` 等控件会被这些隐式 Style **反向命中**，模板递归/空引用进而 native crash。

## Solution

### 原则

**所有 Style 必须命名（`x:Key`），控件处显式绑定 `Style="{StaticResource XxxStyle}"`**。

### 错误写法

```xml
<!-- Theme.xaml -->
<Style TargetType="{x:Type Button}" x:Key="MyButton">...</Style>
<Style TargetType="{x:Type Button}" BasedOn="{StaticResource MyButton}"/>   <!-- 危险 -->
<Style TargetType="{x:Type TextBox}" BasedOn="{StaticResource MyTextBox}"/> <!-- 危险 -->
```

### 正确写法

```xml
<!-- Theme.xaml：只保留命名 Style -->
<Style TargetType="{x:Type Button}" x:Key="MyButton">...</Style>
<Style TargetType="{x:Type TextBox}" x:Key="MyTextBox">...</Style>

<!-- Panel.xaml：控件处显式引用 -->
<Button Style="{StaticResource MyButton}" .../>
<TextBox Style="{StaticResource MyTextBox}" .../>
<Expander Style="{StaticResource MyExpander}" .../>
```

### 替代方案（若必须用隐式 Style）

把主题字典放在 UserControl 的 **最深层 Panel** 的 `Resources` 里（不是 UserControl 根），限制作用域；但推荐直接用命名 Style 方案，更安全。

## Verification

### 复现步骤

1. 在 `BlenderTheme.xaml` 加一条 `<Style TargetType="{x:Type Button}" BasedOn="{StaticResource XxxButton}"/>`
2. 在 UserControl.Resources 合并该字典
3. 用 PaletteSet.AddVisual 挂入 AutoCAD → AutoCAD 崩溃

### 修复验证

1. 删除所有隐式 Style
2. 控件处显式 `Style="{StaticResource XxxStyle}"`
3. PaletteSet.AddVisual 正常执行，面板正确显示 Blender 风格

### 调试定位法（原生崩溃通用方法）

在关键路径打 NDJSON 日志（见 `HyCADTool.Refactored/Presentation/DebugLogger.cs`）：

- `CreateXxxPanel:enter`
- `ViewModel:before_init` / `after_init`
- `View:before_create` / `after_create`
- `PaletteSet:before_add_visual` / `after_add_visual`

**最后一条成功日志之后执行的下一段代码 = 根因点**。

## Notes

### 历史案例

- **2026-04-18**：`HyBlenderPanel` 首次启用时 AutoCAD 崩溃，`BlenderTheme.xaml` 中 9 条隐式 Style（Button / ToggleButton / TextBox / CheckBox / ComboBox / ScrollBar / Expander / ListBoxItem / ListBox / Separator / GroupBox）被删除后恢复正常。完整复盘见 `doc/Debug/045-Blender面板隐式样式致AutoCAD崩溃-2026-04-18-180000.md`。

### 参考对照

- **HyToolPanel**（正常）：`LibraryResources.xaml` 全部命名 Style，控件处显式绑定 → 从未崩溃
- **HyBlenderPanel**（崩溃）：隐式 Style + UserControl.Resources 合并 → 必崩

### 同类宿主

同样的风险存在于：
- Revit `DockablePaneProvider`
- Office VSTO `CustomTaskPane`
- Visual Studio `ToolWindowPane`（WPF 宿主）

### 相关 Skill

- `hycad-multidoc-panel-resource-init` — PaletteSet 多文档资源初始化
- `hycad-autocad-singleton-database-context` — PaletteSet 单例 Database 陷阱
