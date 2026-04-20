---
name: wpf-paletteset-resource-pitfalls
description: AutoCAD PaletteSet 宿主下的 WPF ResourceDictionary / XAML 模板闭坑指南。覆盖 XDG-0001、XDG0066、StaticResource 跨字典、ColorsHost 可变 Brush、PaletteSet.AddVisual 后 e0434352 / “调度程序处理已暂停” 等问题。用户提到 PaletteSet、HyCAD.BlenderUI、Themes/Controls、ResourceDictionary、StaticResource、DynamicResource、XamlParseException、AddVisual、HyB 面板崩溃时使用。
---

# WPF + PaletteSet 资源字典闭坑指南

> 适用范围：
> - `HyCAD.BlenderUI/Themes/*.xaml`
> - `HyCAD.BlenderUI/Themes/Controls/*.xaml`
> - `HyCADTool.Refactored` 中挂到 AutoCAD `PaletteSet` 的 WPF 面板

> 目标：
> 把“设计器报红 / AddVisual 后崩 / e0434352 / Dispatcher paused”收敛成一套固定诊断与修复流程。

---

## 先记住 6 条硬规则

1. **`Binding.Source={DynamicResource ...}` 是非法 XAML。**
   典型反例：
   ```xml
   <GradientStop Color="{Binding Color, Source={DynamicResource Brush_EmbossHighlight}}"/>
   ```
   这会触发 `XDG0066`。

2. **`Themes/Controls/*.xaml` 必须自包含。**
   只要模板里用了别字典的 `StaticResource`，就必须在当前字典顶部显式 `MergedDictionaries` merge 依赖字典，不能只指望上层 `BlenderTheme.xaml`。

3. **`DynamicResource` 只放在真正的 DependencyProperty 上。**
   例如 `Foreground`、`Background`、`BorderBrush`、`FontSize` 可以；
   `Binding.Source`、普通对象属性、`StaticResourceHolder` 路径不行。

4. **PaletteSet 里出现 “调度程序处理已暂停，但仍在处理消息。”，通常是症状，不是根因。**
   真根因往往是更早的 `XamlParseException` / 资源找不到 / 模板解析异常。

5. **改了 `HyCAD.BlenderUI` 的 XAML/主题字典后，优先冷启动验证。**
   不要默认相信 C2 / 热重载一定吃到新 `HyCAD.BlenderUI.dll`。

6. **PaletteSet 宿主内禁止隐式 `Style TargetType`。**
   继续遵守现有 `hycad-project-pitfalls` 的 B1/B2 规则：全部命名样式，显式引用。

---

## 典型症状 → 对应根因

### 1. `XDG0066`

**症状**

- `不能在“Binding”类型的“Source”属性上设置“DynamicResourceExtension”`

**根因**

- 在 `Binding.Source` 上直接用了 `DynamicResource`

**修法**

- 改成 `Source={StaticResource Brush_Xxx}`
- 然后补当前字典的本地 `MergedDictionaries`

---

### 2. `XDG-0001 无法解析资源`

**症状**

- `无法解析资源“Brush_TextPrimary”`
- `无法解析资源“Metric_FontMain”`
- `无法解析资源“BlenderButton”`

**根因**

- 当前 `ResourceDictionary` 自己看不到这些资源
- 设计器与运行时模板静态解析都要求：`StaticResource` 能在**当前字典本地链路**命中

**修法**

- 在当前字典顶部显式 merge：
  - `Themes/Colors.xaml`
  - `Themes/Metrics.xaml`
  - `Themes/Fonts.xaml`
  - 以及依赖的兄弟控件字典（如 `WidgetBackdrop.xaml`、`Button.xaml`、`ScrollBar.xaml`）

---

### 3. `PaletteSet.AddVisual(...)` 后面板崩溃 / `e0434352`

**症状**

- `InitializeComponent()` 已成功
- `OnLoaded` 可能也进了
- `AddVisual(...)` 后几秒弹 AutoCAD 致命错误
- 日志只看见：
  `System.InvalidOperationException: 调度程序处理已暂停，但仍在处理消息。`

**根因**

- `AddVisual` 把视觉树挂进 PaletteSet 后，WPF 在首次 `ApplyTemplate/Measure` 时异步解析模板
- 某个模板里的 `StaticResource` / `ControlTemplate` / 资源链出错
- 真异常先发生，后续 Dispatcher 被污染，才看到“暂停但仍在处理消息”

**修法**

- 不要盯着 `Dispatcher paused`
- 去抓第一颗异常：`FirstChanceException` / `Dispatcher.UnhandledExceptionFilter`

---

### 4. `无法找到名为“Brush_EmbossHighlight”的资源`

**症状**

- 首个真实异常类似：
  `StaticResourceExtension.ProvideValue(): 无法找到名为“Brush_EmbossHighlight”的资源`
- 外层再包一层：
  `XamlObjectWriterException`
  `XamlParseException`
  `StaticResourceHolder`

**根因**

- 当前模板使用了 `Source={StaticResource Brush_EmbossHighlight}`
- 但当前字典本地未 merge `Colors.xaml`
- 或只 merge 了上层聚合字典，当前静态解析路径仍看不到

**修法**

- 当前字典本地 merge `Colors.xaml`
- 若还引用 `Metric_*` / `Font_*` / `BlenderWidgetBackdrop*`，同步补 `Metrics.xaml` / `Fonts.xaml` / 兄弟字典

---

## 必用修复模式

### 模式 A：`GradientStop.Color` 读取主题 Brush

**错误**
```xml
<GradientStop Offset="0" Color="{Binding Color, Source={DynamicResource Brush_EmbossHighlight}}"/>
```

**正确**
```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="pack://application:,,,/HyCAD.BlenderUI;component/Themes/Colors.xaml"/>
</ResourceDictionary.MergedDictionaries>

<GradientStop Offset="0" Color="{Binding Color, Source={StaticResource Brush_EmbossHighlight}}"/>
```

说明：

- 这里不能用 `DynamicResource` 做 `Binding.Source`
- 用 `StaticResource` 后，当前字典必须能本地解析到 `Brush_EmbossHighlight`

---

### 模式 B：子字典自包含

**错误**
```xml
<ResourceDictionary ...>
    <Style TargetType="{x:Type Button}" x:Key="BlenderButton">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type Button}">
                    <Border Style="{StaticResource BlenderWidgetBackdropIdle}"/>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
```

**正确**
```xml
<ResourceDictionary ...>
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="pack://application:,,,/HyCAD.BlenderUI;component/Themes/Colors.xaml"/>
        <ResourceDictionary Source="pack://application:,,,/HyCAD.BlenderUI;component/Themes/Metrics.xaml"/>
        <ResourceDictionary Source="pack://application:,,,/HyCAD.BlenderUI;component/Themes/Fonts.xaml"/>
        <ResourceDictionary Source="pack://application:,,,/HyCAD.BlenderUI;component/Themes/Controls/WidgetBackdrop.xaml"/>
    </ResourceDictionary.MergedDictionaries>
    ...
</ResourceDictionary>
```

---

### 模式 C：`Colors.xaml` 的定位

`Colors.xaml` 不是普通静态调色板，它是：

- `x:Class="HyCAD.BlenderUI.Themes.ColorsHost"`
- 构造时调用 `BlenderThemeManager.PopulateAndRegister(this)`
- 在字典自身写入 `Brush_*`
- 主题切换时只改已有 `SolidColorBrush.Color`

因此：

- **不要**把它理解成普通“固定色值字典”
- **不要**把整份 `BlenderTheme.xaml` merge 到 `Application.Current.Resources`
- **要**在需要 `StaticResource Brush_*` 的字典本地 merge `Colors.xaml`

---

## 推荐排查顺序

1. **先分层**：设计器噪音 vs 编译阻塞 vs 运行时崩溃
2. **先查非法语法**：
   - 全仓 `rg "Source=\\{DynamicResource"`  
   - 命中即先改
3. **再查静态资源链**：
   - 当前字典用了哪些 `StaticResource`
   - 这些资源是否在当前字典本地 merge 链内
4. **再查 PaletteSet 异步模板阶段**
   - `InitializeComponent` 成功不代表安全
   - `AddVisual` 后异步布局仍可能炸
5. **抓第一颗异常**
   - `FirstChanceException`
   - `Dispatcher.UnhandledExceptionFilter`
6. **冷启动验证**
   - 改 `HyCAD.BlenderUI` 后优先完全重启 AutoCAD

---

## 推荐埋点位置

若再次遇到类似问题，优先按这个顺序打点：

1. `HyBlenderPanelViewModel()` 入口 / `LoadFromCommandTable()` 后
2. `HyBlenderPanel()`：
   - `before InitializeComponent`
   - `after InitializeComponent`
3. `PanelManager.CreateHyBlenderPanel()`：
   - `before new VM`
   - `after new View`
   - `before AddVisual`
   - `after AddVisual`
   - `after Visible=true`
4. `OnLoaded()` 入口 / `HyKeyMap.Attach` 后
5. 若只看到 Dispatcher 污染，再补：
   - `AppDomain.FirstChanceException`
   - `Dispatcher.UnhandledExceptionFilter`

判断规则：

- **最后一条成功日志之后**的那段代码，就是首个嫌疑区
- 如果只剩“Dispatcher paused”，继续往前追第一颗异常，不要直接修这个表象

---

## 改动后验证清单

- [ ] `rg "Source=\\{DynamicResource"` 在 `HyCAD.BlenderUI` 中为 0 命中
- [ ] 新增 `StaticResource` 的字典都补了本地 `MergedDictionaries`
- [ ] `Themes/Controls/*.xaml` 没有隐式 `Style TargetType`
- [ ] 重新编译 `HyCAD.BlenderUI`
- [ ] 重新编译 `HyCADTool.Refactored`
- [ ] 完全重启 AutoCAD
- [ ] 执行 `HyB` 不再崩溃
- [ ] 若修设计器问题，`XDG-0001` / `XDG0066` 明显下降或清零

---

## 不要做的事

- 不要把 `Dispatcher paused` 当根因
- 不要把 `DynamicResource` 塞进 `Binding.Source`
- 不要假设“上层已经 merge 过，所以子字典一定看得见”
- 不要改完 `HyCAD.BlenderUI` 还只做 C2 热重载就下结论
- 不要为了“全局省事”把 `BlenderTheme.xaml` 整体灌进 `Application.Current.Resources`

---

## 与现有 skill 的关系

- 本 skill：聚焦 **PaletteSet + WPF 资源字典 / XAML 模板** 闭坑
- `hycad-project-pitfalls`：全项目总表，范围更大

当任务只涉及：

- `Themes/Controls/*.xaml`
- `BlenderTheme.xaml`
- `Colors.xaml` / `ColorsHost`
- `PaletteSet.AddVisual`
- `HyB` 面板 / `HyCAD.BlenderUI`

优先使用本 skill，再按需补读 `hycad-project-pitfalls`
