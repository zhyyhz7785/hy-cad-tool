---
name: wpf-blender-panel-guideline
description: |
  HyCADTool.Refactored 中所有 WPF 面板（托管在 AutoCAD PaletteSet 内）如何正确套用 Blender 黑色主题的
  统一规范。覆盖：ResourceDictionary 合并顺序、隐式 Style 禁令、滚动条子字典隔离、PaletteSet 原框保留、
  新面板/新子视图的落盘清单与 csproj 注册。适用场景：新建 UserControl、补齐已有面板 Blender 主题、
  解决 `StaticResourceExtension` / `XamlParseException` / AutoCAD 启动面板崩溃 (native stack overflow)。
author: Cursor Agent
version: 1.0.0
date: 2026-04-18
---

# WPF Blender 面板规范（AutoCAD PaletteSet 宿主）

## Problem

在 AutoCAD PaletteSet 里托管 WPF 面板时，直接套一套全局 Blender 黑主题会踩到三类坑：

1. **AutoCAD 死机 / 原生栈溢出**：`BlenderTheme.xaml` 里放了 `<Style TargetType="TextBox"/>` 等隐式样式，
   WPF 主题字典被 PaletteSet 宿主的内部可视树"共享命中"，AutoCAD 原生控件被强制套 Blender 模板，
   导致 `PresentationFramework.dll` 递归 StackOverflow，AutoCAD 直接崩掉。
2. **`StaticResourceExtension 异常`**：新建的子 `UserControl`（例如 `XxxSettingsView.xaml`）用 `{StaticResource BlenderButtonFlat}`
   引用主题资源，但该 UserControl **没有本地合并** `BlenderTheme.xaml`。XAML 在解析期必须能从**本控件**的
   `Resources` 链上解析 StaticResource，不会穿透到父 UserControl 的 Resources，导致编译通过运行时抛异常。
3. **ScrollBar 不统一**：各面板里默认 ScrollBar 还是 Windows 银灰风，跟 Blender 黑主题格格不入。
   直接在 `BlenderTheme.xaml` 放隐式 `ScrollBar Style` 又会触发问题 1（污染宿主）。

## Context / Trigger Conditions

- 在 `HyCADTool.Refactored/Presentation/Views/` 下新增任何 `UserControl`
- 运行 `C1 → ShowHyBlenderPanel` 时 AutoCAD 崩溃/闪退/死机
- 新 XAML 加载失败：`在"System.Windows.StaticResourceExtension"上提供值时引发了异常`
- 新 XAML 加载失败：`设置属性"System.Windows.Controls.ColumnDefinition.Width"时引发了异常`
  （`DynamicResource` 绑了 `double` 而不是 `GridLength`）

## Solution

### 1. 资源字典只允许"命名资源"，禁止隐式 `TargetType`

`BlenderTheme.xaml`（主字典）只能放：

```xml
<!-- OK：所有 Style 必须带 x:Key -->
<Style x:Key="BlenderButtonFlat" TargetType="{x:Type Button}"> ... </Style>
<Style x:Key="BlenderTextBox"    TargetType="{x:Type TextBox}"> ... </Style>
<Style x:Key="BlenderExpander"   TargetType="{x:Type Expander}"> ... </Style>
```

**禁止**（会污染 AutoCAD 宿主）：

```xml
<!-- 禁止：隐式样式，任何 Button 都会被套 -->
<Style TargetType="{x:Type Button}"> ... </Style>
<Style TargetType="TextBox"> ... </Style>
```

### 2. ScrollBar 的坑：PaletteSet 下"子字典隔离"依然会崩（2026-04-18 实测确认）

曾经以为：把隐式 `<Style TargetType="ScrollBar"/>` 放进一个独立的 `BlenderScrollBars.xaml` 子字典、
只在 UserControl 的 `Resources.MergedDictionaries` 里 Merge，就能避免污染宿主。**经过 H1 假设的对照实验**
（注释掉 → 3 次 C1 均不崩；启用 → 首次 C1 崩溃）**证实这种"子字典隔离"在 PaletteSet 宿主下依然不可靠**：

- WPF 资源查找对 `ScrollViewer → ScrollBar` / `ListBox → ScrollBar` / `Popup` 等场景会沿可视/逻辑树向上搜索
- PaletteSet 把 UserControl 嵌入宿主控件树时，宿主某些嵌套 ScrollBar 会命中我们的隐式 Style
- 命中后应用 `BlenderScrollBar` 的 `ControlTemplate` → native 递归栈溢出 → AutoCAD 原生崩溃（无托管异常）

**结论：不要在任何被 PaletteSet 托管的 UserControl 里使用任何形式的隐式 ScrollBar Style。**

#### 如果一定要 Blender 风滚动条（两种安全路径）

1. **命名 Style + 显式套到每个 ScrollViewer.Template**：把 ScrollBar 嵌在 `ScrollViewer` 自定义 Template 里，
   ScrollBar 上显式 `Style="{StaticResource BlenderScrollBar}"`，再定义 `BlenderScrollViewer` 命名 Style
   让需要的地方 `Style="{StaticResource BlenderScrollViewer}"`。
2. **接受默认 Windows 滚动条**：当前本仓库采用此方案（见 045 + 046 修复）。滚动条功能性完整，只是视觉
   不匹配 Blender 黑主题。在 PaletteSet 内这是可接受的折中。

**反面做法（已实测必崩）**：

```xml
<!-- ❌ BlenderScrollBars.xaml + UserControl 里 Merge：PaletteSet 宿主下必崩 -->
<ResourceDictionary>
    <Style TargetType="ScrollBar" BasedOn="{StaticResource BlenderScrollBar}"/>
</ResourceDictionary>
```

### 3. 每个 UserControl 的资源合并模板

**所有** Refactored 下需要用 Blender 主题的 UserControl，`UserControl.Resources` 就这一行 Merge：

```xml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/HyCADTool.Refactored;component/Presentation/Resources/BlenderTheme.xaml"/>
        </ResourceDictionary.MergedDictionaries>

        <!-- 本视图的局部 Style / DataTemplate 写在这里 -->
    </ResourceDictionary>
</UserControl.Resources>
```

**不要**再 Merge `BlenderScrollBars.xaml`（已废弃，见上节）。

### 4. 控件样式：显式 `Style="{StaticResource BlenderXxx}"`

由于主字典里没有隐式样式，凡是需要 Blender 外观的控件都要显式指定：

```xml
<TextBox Style="{StaticResource BlenderTextBox}" .../>
<Button  Style="{StaticResource BlenderButtonFlat}" .../>
<Expander Style="{StaticResource BlenderExpander}" .../>
```

### 5. `DynamicResource` 与类型匹配

`DynamicResource` 会在运行时解析，错配类型不会编译报错但会在加载时抛 XAML 解析异常。

- 颜色/画刷：`Background="{DynamicResource Brush_WindowBack}"` ✓
- 数字：只用 `DynamicResource` 取 `double`。给 `ColumnDefinition.Width` / `RowDefinition.Height` 这种
  期望 `GridLength` 的属性时**禁止**用 `DynamicResource`，直接写字面量 `Width="32"`。

### 6. csproj 注册

每个新增的 `.xaml` 都要在 `HyCADTool.Refactored.csproj` 添加：

```xml
<Page Include="Presentation\Views\Preferences\XxxSettingsView.xaml">
  <Generator>MSBuild:Compile</Generator>
  <SubType>Designer</SubType>
</Page>
<Compile Include="Presentation\Views\Preferences\XxxSettingsView.xaml.cs">
  <DependentUpon>XxxSettingsView.xaml</DependentUpon>
</Compile>
```

对应资源字典 `.xaml` 也要注册 `<Page>` 项。

### 7. PaletteSet 原框保留（不要强行去掉 AutoCAD 标题栏）

`PaletteSet.TitleBarLocation = Top / Left` 保持 AutoCAD 原框即可。
曾尝试调用 `PaletteSet.Style = PaletteSetStyles.NameEditable` / `ShowPropertiesMenu = false`
等去框，风险极高（Palette 在 Dock 模式会直接不可见或崩溃）。规则：**AutoCAD 原框保留，
面板内容区全是 Blender 黑主题即可**。

### 8. 面板设计模板（最小骨架）

```xml
<UserControl x:Class="HyCADTool.Refactored.Presentation.Views.YourPanel"
             xmlns="..." xmlns:x="..."
             Background="{DynamicResource Brush_WindowBack}">
    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/HyCADTool.Refactored;component/Presentation/Resources/BlenderTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </UserControl.Resources>

    <Grid>
        <!-- 内容：显式引用命名 Style -->
        <TextBox Style="{StaticResource BlenderTextBox}"/>
        <Button  Style="{StaticResource BlenderButtonFlat}"/>
    </Grid>
</UserControl>
```

## Verification

本仓库实测通过的 13 个 UserControl（2026-04-18）：

- `Presentation/Views/HyBlenderPanel.xaml`
- `Presentation/Views/HyPreferencesView.xaml`
- `Presentation/Views/Preferences/StyleSettingsView.xaml`
- `Presentation/Views/Preferences/ReinSettingsView.xaml`
- `Presentation/Views/Preferences/BasePlateSettingsView.xaml`
- `Presentation/Views/Preferences/PileSettingsView.xaml`
- `Presentation/Views/Preferences/ClusterSettingsView.xaml`
- `Presentation/Views/Preferences/RoadSettingsView.xaml`
- `Presentation/Views/Preferences/ElevationSettingsView.xaml`
- `Presentation/Views/Preferences/DimSettingsView.xaml`
- `Presentation/Views/Preferences/AnchorBoltSettingsView.xaml`
- `Presentation/Views/Preferences/EquipFoundationSettingsView.xaml`

验证步骤：

1. VS 编译通过（没有 XAML 编译错误）。
2. C2 → C1 → `HyB`（或 `Hy`）打开统一面板，AutoCAD 不闪退。
3. 拖动面板滚动条，检查是否为 Blender 深色窄条样式（8px 宽 / 无箭头 / 悬停加亮）。
4. 关闭 AutoCAD、重开新图，再次打开面板，主题和滚动条样式保持一致。

## Notes / Anti-patterns

- ❌ 在 `BlenderTheme.xaml` 放 `<Style TargetType="Button"/>` 这类不带 key 的隐式样式 → AutoCAD 必崩
- ❌ 在 `App.xaml` Resources 合并任何含隐式 Style 的字典（因为 AutoCAD 没有 App.xaml，这在 CAD 场景意义不大，但别对其他宿主犯同样错）
- ❌ 子视图直接 `<TextBox Style="{StaticResource BlenderTextBox}"/>` 但 `UserControl.Resources` 没 Merge 主字典
- ❌ 用 `{DynamicResource}` 给 `GridLength / Thickness` 等复杂值类型赋值 → 运行时抛 XAML 异常
- ❌ 强行调用 `PaletteSet.Style` 去掉 AutoCAD 原框 → 不稳定，得不偿失
- ❌ `BlenderScrollBars.xaml` 放隐式 `<Style TargetType="ScrollBar"/>` 再 Merge 到 UserControl → PaletteSet 下必崩（2026-04-18 H1 对照实验验证）
- ✅ 新 UserControl 先把资源合并模板抄一份，再开始写布局，避免写一半发现 `BlenderButtonFlat` 引不到
- ✅ 滚动条保留 Windows 默认样式，或用显式"ScrollViewer.Template 内嵌命名 ScrollBar Style"方案（参见第 2 节）

## Related Files / Commits

- `HyCADTool.Refactored/Presentation/Resources/BlenderTheme.xaml`（命名资源 + 基础 Brush / 命名 ScrollBar Style）
- `doc/045-Blender面板隐式样式致AutoCAD崩溃.md`（首次"隐式 Style 致 native crash"历史修复）
- 2026-04-18 修复：`BlenderScrollBars.xaml` 方案（子字典隔离隐式 ScrollBar Style）经 H1 对照实验证伪，已删除；
  所有 UserControl 现在只 Merge `BlenderTheme.xaml` 一个字典。
