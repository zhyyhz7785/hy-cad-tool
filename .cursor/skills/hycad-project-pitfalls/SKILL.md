---
name: hycad-project-pitfalls
description: |
  HyCADTool 项目（HyCADTool.Refactored + HyCAD.BlenderUI + AutoCAD 插件）全部已验证的陷阱清单。
  覆盖：AutoCAD API 多文档 / 单例 Database 缓存 / Table Title 自动合并 / 样式与图层初始化 / 配置分裂；
  WPF + PaletteSet 宿主：隐式 Style 原生崩溃 / StaticResource 跨字典 / DynamicResource 类型错配 /
  ControlTemplate.Triggers 位置 / Trigger.TargetName 可达性 / MarkupExtension 当 Converter 递归 /
  子 UserControl 未本地 Merge 主题 / PaletteSet 原框强拆 /
  ResourceDictionary 自动 Seal 强冻 SolidColorBrush 致主题切换抛 InvalidOperationException 升级 e0434352；
  Debug 方法论：调试类与调用点同步删除 / session ID 不入生产代码 /
  Dispatcher.UnhandledException handler shutdown 流程 NRE 升级原生致命 /
  PresentationTraceSources Binding 错误同步阻塞 UI 线程 / VS 错误清单按编译阻塞性分类。
  使用场景：写 AutoCAD 命令 / 新建 WPF 面板 / 改资源字典 / 迁移命令到 Refactored / 多文档联调 / 建 Table /
  Cursor Debug 模式收尾撤埋点 / 处理 AutoCAD 关闭崩溃 / 处理统一面板点击卡顿。
  本 skill 替代：wpf-paletteset-avoid-implicit-styles / wpf-blender-panel-guideline /
  hycad-autocad-singleton-database-context / hycad-multidoc-panel-resource-init /
  .cursor/rules/04-AutoCAD-Table陷阱.mdc。
author: Cursor Agent
version: 1.3.0
date: 2026-04-19
---

# HyCAD 项目级闭坑清单

> 本 skill 收录本仓库**已踩过且已修复**的陷阱。条目按"症状 → 触发条件 → 根因 → 正确做法 → 反例 → 已修复案例"组织。
>
> 三大域：
>
> - **A 域**：AutoCAD 运行时（多文档、事务、Table、配置、样式）
> - **B 域**：WPF + PaletteSet 宿主 + XAML 资源字典
> - **D 域**：Debug 与诊断方法论（埋点撤除、Binding 噪音过滤、关闭流程异常防御、编译错误分类）

---

## A 域：AutoCAD 运行时

### A1 单例服务缓存 `Database` → `eNotFromThisDocument`

**现象**

- 切换 DWG 后，命令在选择/计算阶段正常，渲染或写库阶段抛 `eNotFromThisDocument`
- 堆栈落在 `LayerManager` / Renderer / Marker / Style 或 `Transaction.GetObject`
- 热重载（C2→C1）、多文档联调后更易复现

**触发条件**

- 服务通过 Autofac 注册为 `SingleInstance()`
- 构造里缓存了 `Application.DocumentManager.MdiActiveDocument.Database`

**根因**：事务与 `ObjectId` 必须来自**同一文档数据库**；单例固化了旧文档上下文。

**正确做法**

1. 单例服务**不要**在构造里保存 `Document` / `Database`
2. 每个方法入口重新解析当前 `Database = Application.DocumentManager.MdiActiveDocument.Database`
3. 事务、`LayerTableId` / `BlockTableId` / 字典 Id 都从当前数据库取
4. 服务本身无状态时，不必改 `InstancePerDependency()`，只修上下文获取即可

**反例**

```csharp
public class LayerManager : ILayerManager
{
    private readonly Database _db;
    public LayerManager()
    {
        _db = Application.DocumentManager.MdiActiveDocument.Database;
    }
}
```

**正例**

```csharp
public class LayerManager : ILayerManager
{
    public void EnsureLayer(string name)
    {
        var db = Application.DocumentManager.MdiActiveDocument.Database;
        using var tr = db.TransactionManager.StartTransaction();
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
    }
}
```

---

### A2 多文档切换后面板参数在但样式/图层缺失

**现象**

- `HY` 面板切到新文档后参数显示正常
- `gj` / `gb` / `gb1` 等命令在新图纸**首次执行**才发现样式或图层没建
- 或每次命令都卡顿一下（A3 复合现象）

**触发条件**

- `PluginInitializer.OnDocumentActivated / OnDocumentCreated` 为空实现
- `PanelManager` 已按文档切 `DataContext`，但资源未同步

**正确做法**

1. 分层：`PanelManager` 只管 UI / `DataContext` 切换；`PluginInitializer` 管文档级资源
2. `Initialize()` 时先对当前文档执行一次初始化
3. 订阅 `DocumentActivated` + `DocumentCreated`，每事件调用 `EnsureCurrentDocumentResourcesInitialized(force: false)`
4. 用 `HashSet<string> _initializedDocs` 按文档名幂等
5. 资源初始化固定流程：

   ```csharp
   var vm = SettingsPanelViewModel.GetOrCreate(docName, styleService);
   vm.LoadSettings();
   vm.EnsureStylesApplied();
   layerService.CreateMultipleLayers(...);
   ```

不要把资源初始化逻辑写进 WPF 面板事件。

---

### A3 `_stylesDirty` 每次 `LoadSettings` 无脑置脏 → 命令重复卡顿

**现象**：`gj` / `gb` / `gb1` 重复执行前有明显停顿（每次都在重建样式）。

**根因**

```csharp
// 错误：LoadSettings 末尾一律置脏
public void LoadSettings()
{
    ...
    _stylesDirty = true;   // ← 每次进命令都会再跑 EnsureStylesApplied
}
```

**正确做法**：LoadSettings 前后算"样式签名"，**变化时**才标 dirty：

```csharp
var oldSig = ComputeStyleSignature();
// ... 读 hy-settings.json 到 VM
var newSig = ComputeStyleSignature();
if (!string.Equals(oldSig, newSig, StringComparison.Ordinal))
    _stylesDirty = true;
```

命令执行前统一调 `EnsureStylesApplied()`；插件启动时 `PluginInitializer.InitializeStylesAndLayers()` 做一次基础初始化即可。

---

### A4 配置参数来源分裂

**现象**：同一个参数（如 `Scale`、钢筋直径）在 ViewModel、命令常量、`config.json`、旧静态类里都有一份，改一处不生效。

**正确做法**（参数真相源单一化）

| 参数类别 | 真相源 | 文件 |
|---|---|---|
| 样式、`Scale`、钢筋主参数、锚固、保护层 | `SettingsPanelViewModel.Current` | `hy-settings.json` |
| 底板配筋专用参数 | `BaseReinforcementConfig` | 嵌入 `hy-settings.json` |
| 容差、桩基路径、少量模块参数 | `ConfigurationService` | `config.json` |

新增面板参数优先挂 `SettingsPanelViewModel`。命令类里**不准**出现裸常量默认值，统一从 VM 读。

---

### A5 `Table` Row 0 默认 Title 样式自动合并所有列

**现象**

- AutoCAD 表格表头**只显示第一列**内容，其余被合并吞（用户看到 `#` 或首列标题覆盖全行）
- 若在 `SetSize` 后写 `table.Rows[r].Style = "Data"` 想避开 Title 行样式，在某些图纸/自定义 `TableStyle` 下直接抛 `eKeyNotFound`

**根因**：`db.Tablestyle` 的 Row 0 默认是 `Title` 样式，该行自动合并所有列为单个单元格。

**正确做法**：`SetSize(totalRows, cols)` 之后、写入任何单元格之前，遍历所有单元格解除默认自动合并：

```csharp
table.SetSize(totalRows, cols);

for (int r = 0; r < totalRows; r++)
{
    for (int c = 0; c < cols; c++)
    {
        try
        {
            var range = table.Cells[r, c].GetMergeRange();
            if (range.TopRow != range.BottomRow || range.LeftColumn != range.RightColumn)
                table.UnmergeCells(range);
        }
        catch
        {
            // 单元格本身未合并时 GetMergeRange 可能抛，吃掉即可
        }
    }
}
```

**不要**依赖字符串行样式名 `"Data"` / `"Title"`——部分图纸/自定义 `TableStyle` 没有。

**已修复文件清单**（2026-04）

| 文件 | 修复日期 |
|---|---|
| `SettlementTableService.cs` | 2026-04 |
| `DesignSpecService.cs` | 2026-04 |
| `EquipmentFoundationService.cs` | 2026-04 |
| `GroupCirclesByElevationCommand.cs` | 2026-04 |
| `PileDrawingService.cs` | 2026-04 |

**规则**：今后任何新建 `new Table()` 并逐列写表头的代码，必须先解除默认自动合并。

---

### A6 C2/C1 后 AutoCAD 原生崩溃：byte[] 加载的 Refactored + 跨程序集 pack URI

**现象**

- `C2` 成功，`C1` 后 **AutoCAD 整个进程原生崩溃**（错误报告对话框），**无任何 .NET 异常**打到命令行
- 打开任何 Refactored 的 WPF 面板就崩（不只是 `HyB`）
- 本仓库时间线触发点：**2026-04-18 `HyCAD.BlenderUI` 从 Refactored 拆出独立 csproj 后开始**

**触发条件**（本仓库 ReCall 热重载架构特有）

1. `ReCall.Reload()` 用 `Assembly.Load(File.ReadAllBytes(path))` 把 `HyCADTool.Refactored.dll` 加载到 AppDomain
2. Refactored.dll 依赖 `HyCAD.BlenderUI.dll`（`ProjectReference`），后者被复制到 `%TEMP%` 副本目录但**不预加载**
3. Refactored 面板 XAML 含大量跨程序集 pack URI：
   ```xml
   pack://application:,,,/HyCAD.BlenderUI;component/Themes/BlenderTheme.xaml
   ```
4. C1 触发 `TestCommand.Run` → 构造面板 → 解析 XAML → 跨家 pack URI → 崩

**根因**

WPF 解析 `pack://application:,,,/<AsmShortName>;component/...` 时，**不会触发 `AppDomain.AssemblyResolve`** 事件——它只遍历 `AppDomain.CurrentDomain.GetAssemblies()` 按 short name 匹配。`HyCAD.BlenderUI` 此时还没加载，WPF 在 `PresentationFramework.dll` 的 native resource helper 里读到空 baml 流 → 原生崩溃。

这是 **byte[] 加载 + 跨程序集 pack URI** 的组合坑。单独用 byte[] 加载、或 pack URI 指向同一程序集，都不会触发。

**正确做法**：在 `ReCall.Reload()` 里 `Assembly.Load(Refactored)` **之前**，预加载所有 `HyCAD.*.dll` 伙伴程序集到 AppDomain。

```csharp
// 在 AssemblyResolve 注册之后、Load 主程序集之前
PreloadCompanionAssemblies(loadDepsPath, ed);
Assembly asm = Assembly.Load(File.ReadAllBytes(loadPath));

// 实现（已幂等：Assembly 不可卸载，二次 C2 跳过）
private static void PreloadCompanionAssemblies(string loadDepsPath, Editor ed)
{
    var candidates = Directory.GetFiles(loadDepsPath, "HyCAD*.dll");
    var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
    {
        try { loaded.Add(a.GetName().Name); } catch { }
    }
    foreach (var path in candidates)
    {
        var shortName = Path.GetFileNameWithoutExtension(path);
        if (string.Equals(Path.GetFileName(path), TARGET_DLL_NAME,
                          StringComparison.OrdinalIgnoreCase)) continue;
        if (loaded.Contains(shortName)) continue;
        try { Assembly.Load(File.ReadAllBytes(path)); } catch { }
    }
}
```

**反例**（已实测崩）

- 只 Load 主 Refactored，依赖靠 `AssemblyResolve` 按需解析——`AssemblyResolve` 确实能解析 Refactored 对 `HyCAD.BlenderUI` 的**类型引用**（触发于 JIT / 反射），但**解析不了 pack URI 的资源流**（pack URI 不触发 AssemblyResolve）

**已修复**：`ReCall/Recall.cs`（2026-04-18）新增 `PreloadCompanionAssemblies`，`Reload()` 在 Load Refactored 前调用。改 ReCall.cs 自身需要关 AutoCAD 重 `NETLOAD`。

**规则**：今后新增 `HyCAD.*.dll` 伙伴程序集并在 Refactored XAML 里跨程序集引用 pack URI 的，**不需要**修改 ReCall——命名以 `HyCAD` 前缀开头即自动被预加载。非 `HyCAD*` 前缀的新伙伴程序集需回来改 `PreloadCompanionAssemblies` 的 glob 模式。

**同类宿主风险提示**：Revit `DockablePaneProvider` / Office VSTO 等 byte[] 加载的寄生式 WPF 宿主，跨程序集 pack URI 同样会触发本坑。

---

## B 域：WPF + PaletteSet 宿主 + XAML 资源字典

> 所有 B 类坑的公共背景：`HyCADTool.Refactored` 的 WPF 面板被托管在 AutoCAD `PaletteSet` 里，而 PaletteSet 会把 UserControl 嵌入宿主控件树。任何资源污染都可能通过可视/逻辑树反向命中 AutoCAD 原生控件，导致**无托管异常的原生崩溃**。

---

### B1 隐式 `Style TargetType` 污染 PaletteSet → AutoCAD 原生崩溃

**现象**

- `PaletteSet.AddVisual(...)` 无任何 .NET 异常抛到命令行
- AutoCAD 弹"错误报告"对话框 / 整个 AutoCAD 进程 native stack overflow
- 面板首次 `new HyXxxPanel()` 构造耗时 2+ 秒
- 同一套控件放独立 WPF 窗口中不崩——**只在 PaletteSet 里崩**

**触发条件**：`ResourceDictionary`（或其 Merge 字典）含形如下列的**隐式** Style（无 `x:Key`，纯 `TargetType`）：

```xml
<Style TargetType="{x:Type Button}" BasedOn="{StaticResource BlenderButton}"/>
<Style TargetType="{x:Type TextBox}" BasedOn="{StaticResource BlenderTextBox}"/>
<Style TargetType="{x:Type Expander}" BasedOn="{StaticResource BlenderExpander}"/>
```

**根因**：隐式 Style 没有 `x:Key`，WPF 资源查找沿逻辑树向上冒泡，AutoCAD Palette 宿主本身是上游节点，其内部 `Button` / `TextBox` / `Expander` 被这些隐式 Style 反向命中 → `ControlTemplate` 递归或空引用 → native crash。

**正确做法**：**所有** Style 必须命名（`x:Key`），控件处显式引用：

```xml
<!-- BlenderTheme.xaml -->
<Style x:Key="BlenderButtonFlat" TargetType="{x:Type Button}"> ... </Style>
<Style x:Key="BlenderTextBox"    TargetType="{x:Type TextBox}"> ... </Style>
<Style x:Key="BlenderExpander"   TargetType="{x:Type Expander}"> ... </Style>
```

```xml
<!-- Panel.xaml -->
<Button  Style="{StaticResource BlenderButtonFlat}" .../>
<TextBox Style="{StaticResource BlenderTextBox}"   .../>
<Expander Style="{StaticResource BlenderExpander}" .../>
```

**已修复**：2026-04-18 `BlenderTheme.xaml` 移除 9 条隐式 Style（Button / ToggleButton / TextBox / CheckBox / ComboBox / ScrollBar / Expander / ListBoxItem / Separator / GroupBox）。完整复盘见 `doc/Debug/045-Blender面板隐式样式致AutoCAD崩溃-2026-04-18-180000.md`。

**调试定位法**（原生崩溃通用）：在关键路径打 NDJSON 日志（参考 `HyCADTool.Refactored/Infrastructure/AutoCAD/Utilities/AgentDebugLogger.cs`，或 Cursor Debug 模式临时塞 `File.AppendAllText` NDJSON）——`CreateXxxPanel:enter` / `ViewModel:before_init` / `after_init` / `View:before_create` / `after_create` / `PaletteSet:before_add_visual` / `after_add_visual`。**最后一条成功日志之后的下一段代码 = 根因点**。

**同类宿主**同样风险：Revit `DockablePaneProvider` / Office VSTO `CustomTaskPane` / Visual Studio `ToolWindowPane`。

---

### B2 ScrollBar 隐式 Style"子字典隔离"在 PaletteSet 宿主下**依然必崩**

**现象**：同 B1（native stack overflow）。

**背景**：B1 修复后曾设想：把隐式 `<Style TargetType="ScrollBar"/>` 放进独立子字典 `BlenderScrollBars.xaml`，只在 UserControl 的 `Resources.MergedDictionaries` 里 Merge，应该能避免污染宿主。

**2026-04-18 H1 对照实验结论**：**子字典隔离不可靠**。

- 对照：注释掉 Merge → 3 次 C1 均不崩；启用 → 首次 C1 崩溃
- 根因：WPF 资源查找对 `ScrollViewer → ScrollBar` / `ListBox → ScrollBar` / `Popup` 等场景会沿可视/逻辑树向上搜索。PaletteSet 把 UserControl 嵌入宿主控件树时，宿主某些嵌套 ScrollBar 仍会命中子字典里的隐式 Style → 应用 `BlenderScrollBar` 模板 → native 递归栈溢出

**结论**：**PaletteSet 托管的 UserControl 不得使用任何形式的隐式 ScrollBar Style。**

**两条安全路径**

1. **命名 + 显式套**：定义命名 `BlenderScrollBar` + 命名 `BlenderScrollViewer`（`BlenderScrollViewer.Template` 里嵌 `ScrollBar` 显式 `Style="{StaticResource BlenderScrollBar}"`），控件处 `Style="{StaticResource BlenderScrollViewer}"`
2. **接受 Windows 默认滚动条**（本仓库当前方案）。视觉略不匹配 Blender 黑主题，但在 PaletteSet 内这是可接受折中

**反例**（已实测必崩）

```xml
<!-- BlenderScrollBars.xaml -->
<ResourceDictionary>
    <Style TargetType="ScrollBar" BasedOn="{StaticResource BlenderScrollBar}"/>
</ResourceDictionary>
```

---

### B3 子 UserControl 用 `{StaticResource BlenderXxx}` 但未本地 Merge 主题

**现象**：`"在 System.Windows.StaticResourceExtension 上提供值时引发了异常"`（运行时抛，编译可通过）。

**根因**：新建的子 `UserControl`（例如 `XxxSettingsView.xaml`）用 `{StaticResource BlenderButtonFlat}` 引用主题资源，但该 UserControl 自己的 `Resources` 没 Merge 主题字典。XAML 在解析期必须能从**本控件的 Resources 链**上解析 StaticResource，**不会穿透**到父 UserControl 的 Resources。

**正确做法**：**每个** 需要 Blender 主题的 UserControl，`UserControl.Resources` 必须独立合并一次主字典。

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

---

### B4 `DynamicResource` 绑 `double` 赋给 `GridLength` / `Thickness`

**现象**：`"设置属性 System.Windows.Controls.ColumnDefinition.Width 时引发了异常"`，指向某行 `ColumnDefinition.Width="{DynamicResource Metric_IconBarWidth}"`。

**根因**：`DynamicResource` 在运行时解析，错配类型不会编译报错，但 WPF 不会把 `double` 隐式转到 `GridLength` / `Thickness`。

**正确做法**

| 属性类型 | 取值方式 |
|---|---|
| `Brush` | `DynamicResource Brush_WindowBack` ✓ |
| `double`（FontSize 等） | `DynamicResource Metric_FontMain` ✓ |
| `GridLength` / `Thickness` / `CornerRadius` | 字面量 `Width="32"` 或 `StaticResource`（资源本身就是该类型） |

反例：`ColumnDefinition.Width="{DynamicResource Metric_IconBarWidth}"`（后者是 `double`）  
正例：`ColumnDefinition.Width="32"`

---

### B5【新 2026-04-18】`ResourceDictionary` 之间 `StaticResource` 跨字典查找在设计器失效

**现象**

- `XDG0066 "在 System.Windows.Markup.StaticResourceHolder 上提供值时引发了异常"`
- 运行时 OK 但 XAML 设计器报红波浪（例如 `Samples/DemoWindow.xaml` 线 21 附近）
- 或首次加载路径偶发抛异常

**触发条件**：`ResourceDictionary A` 内某个 `ControlTemplate` 用 `{StaticResource X}`，而 `X` 定义在 `ResourceDictionary B`；哪怕上层聚合字典已按"B 早于 A"顺序 Merge，设计器静态解析仍可能失败。

**根因**：XAML 静态解析阶段要求 `StaticResource X` 能从**本字典自身**或**本字典已声明的 `MergedDictionaries`**直接命中；"上层聚合字典 Merge 顺序正确"对设计器和某些运行时解析路径**不够可靠**。

**正确做法（自包含字典原则）**：凡 `Themes/Controls/*.xaml` 内部模板引用别字典资源（`BlenderScrollViewer` / `BoolToVisibility` / 命名 Brush），**该字典顶部必须显式 `MergedDictionaries` merge 依赖字典**，哪怕聚合字典已 merge 过。

**反例**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Style TargetType="ComboBox">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate>
                    <ScrollViewer Style="{StaticResource BlenderScrollViewer}"/>  <!-- XDG0066 -->
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
```

**正例**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="pack://application:,,,/HyCAD.BlenderUI;component/Themes/Controls/ScrollBar.xaml"/>
    </ResourceDictionary.MergedDictionaries>

    <Style TargetType="ComboBox">
        <!-- 现在 BlenderScrollViewer 可在本字典局部解析 -->
        ...
    </Style>
</ResourceDictionary>
```

**已修复**（2026-04-18）

- `HyCAD.BlenderUI/Themes/Controls/IconTabBar.xaml`
- `HyCAD.BlenderUI/Themes/Controls/PropertyEditor.xaml`
- `HyCAD.BlenderUI/Themes/Controls/ComboBox.xaml`
- `HyCAD.BlenderUI/Themes/Controls/ListBox.xaml`

---

### B6【新】`{x:Static MyMarkupExt.Instance}` 当 Converter 会在设计器触发 `ProvideValue` 递归

**现象**：`XDG0066 StaticResourceHolder` 异常；设计器崩/红波浪。

**触发条件**：某个 `IValueConverter` 继承 `MarkupExtension`，实现成可重用单例（`public static readonly MyConv Instance = new MyConv()`），XAML 里用：

```xml
<TextBlock Visibility="{Binding IsPinned, Converter={x:Static prim:BoolToVisibility.Instance}}"/>
```

设计器在静态解析阶段重复调用 `ProvideValue` / `StaticResourceHolder`，易触发递归或上下文缺失异常。

**正确做法**：把 `IValueConverter` 声明为 `ResourceDictionary` 命名资源，用 `{StaticResource}`：

```xml
<ResourceDictionary ...
                    xmlns:prim="clr-namespace:HyCAD.BlenderUI.Controls.Primitives">
    <prim:BoolToVisibility x:Key="BoolToVisibility"/>
    <prim:InverseBoolToVisibility x:Key="InverseBoolToVisibility"/>
    ...
</ResourceDictionary>
```

```xml
<TextBlock Visibility="{Binding IsPinned, Converter={StaticResource BoolToVisibility}}"/>
```

**已修复**（2026-04-18）：`HyCAD.BlenderUI/Themes/Controls/PanelHeader.xaml`、`PropertyEditor.xaml`。

---

### B7【新】`ControlTemplate.Triggers` 必须是 `ControlTemplate` 直接子元素

**现象**：`MC3015 "Grid 或其一个基类上未定义附加属性 ControlTemplate.Triggers"`。

**触发条件**：把 `<ControlTemplate.Triggers>` 写进根 `<Grid>` / `<StackPanel>` 内部了。

**正确做法**：`<ControlTemplate.Triggers>` 放在 `<ControlTemplate>` 同级末尾，与根元素并列。

**反例**

```xml
<ControlTemplate TargetType="...">
    <Grid>
        <!-- 根 Panel -->
        <Rectangle .../>
        <ControlTemplate.Triggers>   <!-- ✗ MC3015 -->
            <Trigger Property="IsMouseOver" Value="True">...</Trigger>
        </ControlTemplate.Triggers>
    </Grid>
</ControlTemplate>
```

**正例**

```xml
<ControlTemplate TargetType="...">
    <Grid>
        <Rectangle .../>
    </Grid>
    <ControlTemplate.Triggers>
        <Trigger Property="IsMouseOver" Value="True">...</Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>
```

**已修复**：`HyCAD.BlenderUI/Themes/Controls/NumericSlider.xaml`（2026-04-18）。

---

### B8【新】`Trigger.TargetName` 不能穿透进 `RenderTransform` / 命名资源

**现象**：`MC4111 "无法找到 Trigger 目标 ArrowRotate"`。

**触发条件**：尝试给 `Path.RenderTransform` 里的 `RotateTransform` 起 `x:Name="ArrowRotate"`，然后 `<Setter TargetName="ArrowRotate" Property="Angle" Value="90"/>`。

**根因**：`Trigger.TargetName` 只能指向 `ControlTemplate` 可视树里**同级、已具名**的元素；`RenderTransform` 中的 `Transform` 对象属于资源/变换树，不可达。

**正确做法**

- **推荐**：改 `Path.Data`，定义两套 geometry（折叠 / 展开）：

  ```xml
  <Path x:Name="Arrow" Fill="..." Data="M 0,0 L 8,4 L 0,8 Z"/>
  ...
  <ControlTemplate.Triggers>
      <Trigger Property="IsChecked" Value="True">
          <Setter TargetName="Arrow" Property="Data" Value="M 0,0 L 8,0 L 4,8 Z"/>
      </Trigger>
  </ControlTemplate.Triggers>
  ```

- 备选：用 `Storyboard` 控制 `(Path.RenderTransform).(RotateTransform.Angle)` 属性路径（而不是 `TargetName`）。

**已修复**：`HyCAD.BlenderUI/Themes/Controls/Expander.xaml`（2026-04-18）。

---

### B9 PaletteSet 原框不要强拆

**现象**：曾尝试调用 `PaletteSet.Style = PaletteSetStyles.NameEditable` / `ShowPropertiesMenu = false` 等方式去掉 AutoCAD 标题栏 → Dock 模式面板不可见或直接崩溃。

**正确做法**

- 保持 `PaletteSet.TitleBarLocation = Top`（或 `Left`），AutoCAD 原框保留
- 面板内容区 Blender 黑主题即可；视觉上接受"AutoCAD 原框 + Blender 内容"的组合
- 不要追求"完全去 AutoCAD 框"，风险极高、收益很小

---

### B10【新 2026-04-19】`ResourceDictionary` 加载即 `Seal` → `SolidColorBrush.Color = ...` 抛 `InvalidOperationException` → 升级 `e0434352`

**症状链（已验证）**

1. 用户在统一面板切换主题（4 选 1：BlenderDark / BlenderLight / AcadLight / AcadDark）后，AutoCAD 命令栏开始狂刷
   `System.InvalidOperationException: 无法在对象"#FF4772B3"上设置属性，因为它处于只读状态`，每个 brush 一条；
2. 紧接着 AutoCAD 自家 Ribbon 抛 `XamlParseException`：`组件 Badge 不具有由 URI '/AdWindows;component/themes/badge.xaml' 识别的资源`；
3. 数秒后弹原生 `Unhandled e0434352h Exception` 致命错误框，AutoCAD 进程整体倒下。

**根因（WPF 内部行为）**

`BlenderThemeManager` v3 设计核心是 "brush facade" — `ColorsHost.ctor` 创建一组未冻结的 `SolidColorBrush` 实例放进字典，主题切换时只改 `brush.Color`（DP 通知自动传播给所有 `{DynamicResource Brush_xxx}` 引用方）。

但 WPF `ResourceDictionary` 在 `Add(key, value)` 内部会调 `StyleHelper.SealIfSealable(value)`，命中以下任一条件就强行 `Seal()`/`Freeze()` 入参：

- 字典通过 `<ResourceDictionary Source="..."/>` 加载；
- 字典被 mark 为 `IsThemeDictionary` / `_ownerApps != null` / `IsReadOnly`；
- 字典的 owner 是已 sealed 的 `ResourceDictionary` / `Application.Resources` / `FrameworkElement.Resources` 链上的任一节点。

`Themes/Colors.xaml` 在 BlenderTheme 树里通过 `Source` 加载 → host 自动满足条件 → 添加进去的 brush 立刻被 `Freeze()`。后续 `Apply()` 改 `brush.Color` 必抛 `InvalidOperationException`，几十个异常累积到某 idle tick 污染 AutoCAD 自家 Ribbon Badge 的资源解析路径，升级为 native `e0434352`。

**根因修复（self-binding 防 freeze）**

`Freezable.CanFreeze` 在对象持有任何 binding / animation / dynamic resource expression 时返回 `false`，`SealIfSealable` 的 `if (sealable.CanSeal)` 条件 short-circuit，brush 不被 `Seal`。所以创建 brush 后立刻给一个**与业务无关的 DP**（这里选 `OpacityProperty`，默认值 1.0、binding 不改值）挂个 dummy `Binding(".") { Source = 1.0 }` 即可：

```csharp
foreach (var kv in palette)
{
    var brush = new SolidColorBrush(kv.Value);
    BindingOperations.SetBinding(brush, SolidColorBrush.OpacityProperty,
        new Binding(".") { Source = 1.0, Mode = BindingMode.OneWay });
    host[kv.Key] = brush;
}
```

后续 `Apply()` 改 `brush.Color` 完全独立于 `OpacityProperty` 的 binding，互不冲突。

**何时复用此模式**

任何"想做 mutable shared object 放进 `ResourceDictionary`，运行时改其 DP 触发全局更新"的场景，都要在 add 之前做这步 self-binding。例如：mutable `Thickness` token、mutable `FontFamily` token、mutable `CornerRadius` token 等若改用 `Freezable` 包装，同样需要这一步。

**为什么不能简单用 `Freezable.IsFrozen` / `Freeze()` 检查反着想（"已经冻就再造一个"）**

WPF 的 `DynamicResource` 解析后会把 brush 实例缓存到所有引用它的控件 DP 上，重新 `Add` 同名 key 不会让已解析的引用方重新查表。必须保证**初次注入的实例 永远不被 freeze**。

**修复文件**

- `HyCAD.BlenderUI/Theming/BlenderThemeManager.cs` `PopulateAndRegister`（2026-04-19）

---

## D 域：Debug 与诊断方法论

> 本域记录 Cursor Debug 模式 / 临时埋点 / WPF 异常兜底 三类常踩坑。
> 公共背景：AutoCAD .NET 插件没有 `app.config` 级别的统一日志，调试要么靠 `Editor.WriteMessage`（同步阻塞 UI 线程），要么靠 `File.AppendAllText` NDJSON。任一手段都需明确"会话期"与"长期保留"的边界。

---

### D1 删调试类**必须**先删全部调用点（否则全项目编译失败）

**现象**

- 上一轮 Debug 会话结束后清理：把临时调试类 `DebugSessionLog`（或 `DebugLogger`）的**类定义**删掉
- 立刻 35+ 处 `CS0103: 当前上下文中不存在名称 "DebugSessionLog"` + `CS0234: 命名空间不存在类型 "DebugSessionLog"`，跨 4 个文件
- 全项目编译挂死，下次 `C2` 热重载无 dll 可加载

**触发条件**：调试类被广泛 `using` / 调用，但清理时只删类、不删调用方。本仓库典型受害文件：
`Presentation/PluginInitializer.cs`、`Presentation/PanelManager.cs`、`Infrastructure/AutoCAD/UI/CuiMenuBuilder.cs`。

**正确做法**：撤埋点的"反向顺序"是**强制**的：

1. **先**全局 `Grep` 调试类名 → 列出所有调用点
2. **再**逐文件删调用 + 配套 `// #region agent log` / `_xxxInstalled` 标志位字段
3. **最后**才删类定义本身
4. 收尾：再 `Grep` 一次类名，必须 0 命中才算清理完

**反例**（本次会话踩坑路径）

```text
[误]
  Step 1: 删 PluginInitializer.cs 顶部 internal static class DebugSessionLog { ... }
  Step 2: （被用户中断，没继续删调用方）
  → CS0103 × 35+ 全项目编译失败
[正]
  Step 1: Grep "DebugSessionLog" → 4 文件 35+ 处
  Step 2: 逐文件删 .Write(...) 调用 + #region agent log 注释 + _shutdownTrapInstalled 等标志位
  Step 3: Grep 再扫一次确认 0 命中
  Step 4: 删类定义
  Step 5: 删 debug-<sessionId>.log 文件
```

**配套**：所有 Cursor Debug 模式生成的埋点都用 `// #region agent log ... // #endregion` 包裹，便于一次性 Grep 定位。

---

### D2 调试类不要把会话 ID 硬编码进生产代码

**现象**：Debug 模式生成的 `DebugLogger.cs` / `DebugSessionLog.cs` 把 `debug-eef710.log` 这种**会话专用路径**写到 `private const string LogPath = @"...\debug-eef710.log"`。会话结束后变成永远没人调的孤儿文件，且会话 ID 已失效。

**根因**：会话 ID 仅在 Cursor Debug 模式当前轮有效，下次开 Debug 会换新 ID。把它写进 `internal static class` 即把"临时基础设施"**永久化**到代码库。

**正确做法**

- 短命方案：把 NDJSON 写入直接内联到调用点（`File.AppendAllText("...debug-XXX.log", ...)`），用 `// #region agent log` 包裹
- 长命方案：用本仓库**早期已有**的 `Infrastructure/AutoCAD/Utilities/AgentDebugLogger.cs`（路径不带会话 ID，方法签名稳定）
- **禁止**新建 `internal static class XxxLogger` 把 session ID 写成 const

**已修复**：2026-04-18 删除孤儿 `Presentation/DebugLogger.cs`（无任何调用点）。

---

### D3 `Dispatcher.UnhandledException` handler 在 AutoCAD shutdown 时**自身会 NRE 升级原生致命**

**现象**

- 关 AutoCAD（点右上角 ×）时弹 `Fatal Error: Unhandled e0434352h Exception`
- 平时正常使用不崩，只在关闭流程触发

**根因**：`InstallWpfExceptionTraps` 装的 `Dispatcher.CurrentDispatcher.UnhandledException` handler 内部直接 `var doc = AcApp.DocumentManager.MdiActiveDocument; doc.Editor.WriteMessage(...)`。AutoCAD 关闭流程中 `MdiActiveDocument` 已被销毁（返回 null），handler 自己抛 NRE → CLR 视为"异常 handler 又抛异常" → 升级 native fatal。

**正确做法**：handler 内**全部**接触 AutoCAD 上下文的调用都要 null 安全 + try/catch 兜底：

```csharp
System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
{
    try
    {
        var ex = e.Exception;
        try
        {
            var doc = AcApp.DocumentManager?.MdiActiveDocument;
            if (doc != null)
            {
                doc.Editor?.WriteMessage($"\n  ✗ [WPF UI] {ex.GetType().Name}: {ex.Message}");
            }
        }
        catch { }
        e.Handled = true;
    }
    catch { }
};
```

**反例**（升级 native fatal）

```csharp
Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
{
    var doc = AcApp.DocumentManager.MdiActiveDocument;
    doc.Editor.WriteMessage(...);
    e.Handled = true;
};
```

**已修复**：2026-04-18 `PluginInitializer.InstallWpfExceptionTraps` 全部加 null 防御。

---

### D4 `PresentationTraceSources` Binding 错误**同步**转发到 `Editor.WriteMessage` → 点击卡死

**现象**

- 打开 Blender 二级菜单（统一面板内 TabControl 切换、Hover Ribbon 控件）→ AutoCAD UI **明显卡顿/假死**
- 命令行涌出几百条 `System.Windows.Data Error: 40 : ... target element is 'Border' (Name='mBorder'); ...`
- 进程不崩溃，关掉 AutoCAD 后又能工作

**根因**：

1. `InstallWpfExceptionTraps` 装了 `PresentationTraceSources.DataBindingSource` listener，把 WPF Binding 错误转发给 `Editor.WriteMessage`
2. AutoCAD 自家 Ribbon / Menu 模板里就有大量错误 Binding（`mBorder` / `IsEnabled` / `ToolTipResolver` / `ShowToolTipOnDisabled` / `IsVisible` 等），**与 HyCAD 项目无关**
3. `Editor.WriteMessage` 是**同步 IO** + 在 UI 线程执行，几百条排队 → UI 线程被串行阻塞 → 表现为"卡死"

**正确做法**：**两层防御**

```csharp
private void TryWrite(string message, bool newline)
{
    if (string.IsNullOrEmpty(message)) return;

    // 第 1 层：白名单——只保留本项目的 Binding 错误
    bool isProjectRelevant =
        message.IndexOf("HyCAD", StringComparison.OrdinalIgnoreCase) >= 0 ||
        message.IndexOf("BaseReinVm", StringComparison.Ordinal) >= 0 ||
        message.IndexOf("Settings.", StringComparison.Ordinal) >= 0 ||
        message.IndexOf("PreferencesVm", StringComparison.Ordinal) >= 0 ||
        message.IndexOf("FilterVm", StringComparison.Ordinal) >= 0 ||
        message.IndexOf("SettingsVm", StringComparison.Ordinal) >= 0;

    if (!isProjectRelevant) return;

    // 第 2 层：项目相关也不进 Editor.WriteMessage（同步 IO 阻塞 UI）
    System.Diagnostics.Debug.WriteLine("[HyCAD WPF Binding] " + message);
}
```

**关键判据**

- 黑名单不可行：AutoCAD Ribbon 错误消息**不**包含 `Autodesk.Windows` 字符串，关键字过滤会全部漏过
- 白名单**必须**用项目独有标识（`HyCAD` / 自定义 ViewModel 类名）
- 项目相关错误也**不要**写命令栏，丢 `Debug.WriteLine`（DebugView++ 看 / 仅 Debug build 输出）

**已修复**：2026-04-18 `PluginInitializer.BindingErrorListener.TryWrite` 改白名单 + Debug.WriteLine。

---

### D5 VS 错误清单要按"会不会阻断编译"分类，不要被 XDG 设计时报错带偏

**现象**：VS 错误窗口同时弹出 30+ 条错误，包括：

```text
[阻断编译]
  CS0103/CS0234 → 真编译错误，必修
[非阻断]
  XDG0008 命名空间不存在 XxxConverter → 设计时分析器，索引滞后
  XDG0010 必须使 Setter.Property 具有非 null 值 → XAML 设计时
  XDG0023/XDG0024 长度为空字符串 → XAML 设计时
  CS0006 未能找到元数据文件 ...\bin\Debug\XxxRefactored.dll
        → Tests 项目找不到主项目 dll，主项目编译失败的级联错误
```

**正确做法**：先按错误码前缀分类、再决定行动

| 错误码前缀 | 类别 | 处理 |
|---|---|---|
| `CS0xxx` / `CS1xxx` | C# 编译器（必阻塞） | 必修 |
| `MC3xxx` / `MC4xxx` | XAML 编译器（阻塞 baml 生成） | 必修 |
| `XDG0xxx` | VS 设计时分析器 + IDE 索引 | **大概率误报**，先 Build → Clean → Rebuild → 重启 VS；只有 Rebuild 后还在的才真要修 |
| `CS0006` 找 `bin\Debug\xxx.dll` | 级联错误 | 不要直接看，先解决主项目 CS0xxx |

**反例**：被 `XDG0008 BoolToVisibilityConverter` 带偏，去找/修 Converter 类，但实际**类一直在**（`Presentation/Views/Converters/BoolToVisibilityConverter.cs` 没动过），只是 Cursor / VS 索引没刷新。

**判断三步法**

1. 排序：CS / MC 在前，XDG / 级联在后
2. 第一波只修 CS / MC
3. Rebuild 一次，XDG 大概率自动消失；剩下的再处理

---

## Project Theme Application（新建面板骨架）

Refactored 面板所在 UserControl 根部资源合并模板——**只这一行 Merge**：

```xml
<UserControl x:Class="HyCADTool.Refactored.Presentation.Views.YourPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource Brush_WindowBack}">
    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/HyCADTool.Refactored;component/Presentation/Resources/BlenderTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
            <!-- 局部 Style / DataTemplate / Converter 写在这里 -->
        </ResourceDictionary>
    </UserControl.Resources>

    <Grid>
        <!-- 控件必须显式引用命名 Style -->
        <TextBox Style="{StaticResource BlenderTextBox}"/>
        <Button  Style="{StaticResource BlenderButtonFlat}"/>
    </Grid>
</UserControl>
```

**csproj 注册**：每个新增 `.xaml` / `.xaml.cs` 都要在 `HyCADTool.Refactored.csproj` 加 `<Page>` / `<Compile>` 项。

**`DynamicResource` 类型**：颜色/画刷、FontSize（double）OK；`GridLength` / `Thickness` / `CornerRadius` 用字面量或 `StaticResource`（见 B4）。

---

## Verification

**A 域验证**

- 切换 DWG 后执行命令不再报 `eNotFromThisDocument`（A1）
- 新 DWG 首次执行 `gj` / `gb` / `gb1` 不缺样式或图层（A2）
- 重复执行命令不再每次卡顿（A3）
- 样式/Scale/钢筋参数只从 `SettingsPanelViewModel.Current` 读（A4）
- 新 `Table` 表头各列文字都可见，不会被 Title 行合并吞（A5）

**B 域验证**

- `C2 → C1 → HyB`（或 `Hy`）打开统一面板，AutoCAD 不闪退（B1/B2）
- 所有 UserControl 打开不抛 `StaticResourceExtension` 异常（B3）
- XAML 设计器打开 `Samples/DemoWindow.xaml` 无 `XDG0066`（B5/B6）
- 编译无 `MC3015` / `MC4111`（B7/B8）
- `ColumnDefinition.Width` / `Margin` 类属性加载不报异常（B4）
- 面板 Dock 到 AutoCAD 侧边不消失（B9）

**D 域验证**

- 撤埋点后 `Grep "DebugSessionLog|DebugLogger"` 全工程 0 命中（D1/D2）
- 关 AutoCAD 不再弹 `e0434352h` 致命错误对话框（D3）
- 操作 Blender 二级菜单不再卡顿，命令栏不再涌出 `mBorder` Binding Error（D4）
- VS 错误窗口剩下的全是 CS/MC 类，无 XDG（Rebuild 后；D5）

**实测通过的 UserControl**（2026-04-18）

- `Presentation/Views/HyBlenderPanel.xaml`
- `Presentation/Views/HyPreferencesView.xaml`
- `Presentation/Views/Preferences/{Style,Rein,BasePlate,Pile,Cluster,Road,Elevation,Dim,AnchorBolt,EquipFoundation}SettingsView.xaml`
- `HyCAD.BlenderUI/Samples/DemoWindow.xaml`

---

## Related Skills

- `wpf-webview2-pitfalls`：MarkdownEditor 子项目专属的 WebView2 陷阱（跟 AutoCAD/PaletteSet/资源字典无关，独立领域）
- `hycad-refactored-migration-patterns`：Refactored 命令迁移与架构规范（不是坑，是"怎么写对"）

## Replaces

本 skill 合并并替代以下文件（执行时已删除）：

- `.cursor/skills/wpf-paletteset-avoid-implicit-styles/SKILL.md` → B1/B2
- `.cursor/skills/wpf-blender-panel-guideline/SKILL.md` → B1–B4、B9、模板节
- `.cursor/skills/hycad-autocad-singleton-database-context/SKILL.md` → A1
- `.cursor/skills/hycad-multidoc-panel-resource-init/SKILL.md` → A2/A3
- `.cursor/rules/04-AutoCAD-Table陷阱.mdc` → A5
