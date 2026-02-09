# AutoCAD PaletteSet 与 WPF TabItem 学习文档

> **适用环境**：AutoCAD 2024 + .NET Framework 4.8 + WPF  
> **项目**：HyCADTool.Refactored  
> **更新日期**：2026-02-09

---

## 目录

1. [核心概念总览](#1-核心概念总览)
2. [PaletteSet 详解](#2-paletteset-详解)
3. [WPF TabControl / TabItem 详解](#3-wpf-tabcontrol--tabitem-详解)
4. [两种选项卡方案对比](#4-两种选项卡方案对比)
5. [项目中的实际实现](#5-项目中的实际实现)
6. [WPF 嵌入 PaletteSet 的技术细节](#6-wpf-嵌入-paletteset-的技术细节)
7. [样式与主题定制](#7-样式与主题定制)
8. [常见问题与最佳实践](#8-常见问题与最佳实践)
9. [代码模板速查](#9-代码模板速查)

---

## 1. 核心概念总览

### 1.1 两个东西，不同层级

| 概念 | 所属 | 作用 | 比喻 |
|------|------|------|------|
| **PaletteSet** | AutoCAD .NET API (`Autodesk.AutoCAD.Windows`) | 承载面板的**容器窗口**，可停靠、自动隐藏、浮动 | 一个**抽屉柜** |
| **TabItem** | WPF (`System.Windows.Controls`) | TabControl 内的**选项卡页** | 抽屉柜里的**每个抽屉** |

### 1.2 关系图

```
┌─────────────────────────────────────────────────────┐
│  AutoCAD 主窗口                                      │
│                                                     │
│   ┌──────────────────────────────────────────┐      │
│   │  PaletteSet ("HY 主面板")                 │      │
│   │  ┌──────┬──────┬──────┐                  │      │
│   │  │ Tab1 │ Tab2 │ Tab3 │  ← PaletteSet    │      │
│   │  ├──────┴──────┴──────┤    自带的选项卡    │      │
│   │  │                    │                  │      │
│   │  │  WPF UserControl   │                  │      │
│   │  │  ┌────────────────┐│                  │      │
│   │  │  │ TabControl     ││  ← WPF 内部的    │      │
│   │  │  │ ┌────┬────┐   ││    TabItem        │      │
│   │  │  │ │TA  │TB  │   ││                  │      │
│   │  │  │ ├────┴────┤   ││                  │      │
│   │  │  │ │ 内容区域 │   ││                  │      │
│   │  │  │ └─────────┘   ││                  │      │
│   │  │  └────────────────┘│                  │      │
│   │  └────────────────────┘                  │      │
│   └──────────────────────────────────────────┘      │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### 1.3 核心区别

| 对比维度 | PaletteSet 选项卡 | WPF TabItem |
|----------|-------------------|-------------|
| **实现方式** | `paletteSet.Add()` 或 `AddVisual()` | XAML 中 `<TabItem>` |
| **选项卡位置** | PaletteSet 底部（AutoCAD 原生样式） | WPF 控件内部（完全可自定义） |
| **每个选项卡** | 一个独立的 UserControl | 同一 UserControl 内的不同区域 |
| **独立性** | 每个 Tab 是独立面板，可单独加载 | 共享同一个 ViewModel |
| **适合场景** | 功能差异大的多个面板（钢筋、过滤器、桩基） | 同一功能的不同参数组（样式设置、钢筋参数） |

---

## 2. PaletteSet 详解

### 2.1 核心类

**命名空间**：`Autodesk.AutoCAD.Windows`

```csharp
using Autodesk.AutoCAD.Windows;
```

**继承链**：
```
PaletteSet → IDisposable
```

### 2.2 创建 PaletteSet

#### 最小示例

```csharp
// 最简形式 - 只需标题
var ps = new PaletteSet("我的面板");
ps.AddVisual("页签名", new MyUserControl());
ps.Visible = true;
```

#### 完整配置

```csharp
var ps = new PaletteSet("HY 主面板", new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"))
{
    // 尺寸
    Size = new System.Drawing.Size(280, 600),         // 初始大小
    MinimumSize = new System.Drawing.Size(200, 400),  // 最小大小
    
    // 停靠
    DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right),
    
    // 样式
    Style = PaletteSetStyles.ShowCloseButton       // 关闭按钮
          | PaletteSetStyles.ShowAutoHideButton     // 自动隐藏按钮
          | PaletteSetStyles.Snappable              // 可吸附到边缘
};
```

### 2.3 构造函数重载

| 重载 | 参数 | 说明 |
|------|------|------|
| `PaletteSet(string)` | title | 最简形式 |
| `PaletteSet(string, Guid)` | title, guid | **推荐**，Guid 用于保存/恢复停靠状态 |
| `PaletteSet(string, string, Guid)` | title, helpFile, guid | 带帮助文件 |

> **Guid 的作用**：AutoCAD 会用 Guid 记住面板的停靠位置、大小、是否自动隐藏等状态。同一个 Guid 的 PaletteSet 在下次打开时恢复上次的布局。

### 2.4 PaletteSetStyles 枚举

```csharp
[Flags]
public enum PaletteSetStyles
{
    ShowCloseButton      = 1,    // 标题栏显示关闭按钮
    ShowAutoHideButton   = 2,    // 标题栏显示自动隐藏（图钉）按钮
    ShowPropertiesMenu   = 4,    // 右键显示属性菜单
    Snappable            = 8,    // 面板可吸附到 AutoCAD 窗口边缘
    UsePaletteNameAsTitleForSingle = 16, // 单个选项卡时用其名称作标题
    SinglePalette        = 32    // 不显示选项卡栏（只有一个面板时用）
}
```

**常用组合**：

```csharp
// 标准组合（本项目使用）
Style = PaletteSetStyles.ShowAutoHideButton 
      | PaletteSetStyles.ShowCloseButton 
      | PaletteSetStyles.Snappable;

// 单面板（不需要选项卡栏）
Style = PaletteSetStyles.ShowAutoHideButton 
      | PaletteSetStyles.ShowCloseButton 
      | PaletteSetStyles.SinglePalette;
```

### 2.5 DockSides 枚举

```csharp
public enum DockSides
{
    None   = 0,   // 不可停靠（只能浮动）
    Left   = 1,   // 可停靠到左侧
    Top    = 2,   // 可停靠到顶部
    Right  = 4,   // 可停靠到右侧
    Bottom = 8    // 可停靠到底部
}
```

**常见配置**：

```csharp
// 左右停靠（最常用，本项目使用）
DockEnabled = (DockSides)((int)DockSides.Left | (int)DockSides.Right);

// 四面停靠
DockEnabled = DockSides.Left | DockSides.Top | DockSides.Right | DockSides.Bottom;

// 禁止停靠（只能浮动）
DockEnabled = DockSides.None;
```

### 2.6 添加内容到 PaletteSet

有两种方式将内容添加到 PaletteSet 中：

#### 方式 A：`AddVisual()` — 直接添加 WPF 控件（推荐，简洁）

```csharp
// 直接添加 WPF UserControl
ps.AddVisual("钢筋", new ReinPanel());
ps.AddVisual("过滤器", new FilterPanel());
```

**特点**：
- AutoCAD 内部自动创建 `ElementHost` 包装
- 代码最简洁
- 每个 `AddVisual()` 调用生成一个 PaletteSet 选项卡

#### 方式 B：`Add()` + `ElementHost` — 手动包装（灵活）

```csharp
// 手动创建 ElementHost 再添加
var elementHost = new ElementHost
{
    AutoSize = true,
    Dock = System.Windows.Forms.DockStyle.Fill,
    Child = new SettingsPanel()  // WPF UserControl
};
ps.Add("设置", elementHost);
```

**特点**：
- 可以精确控制 `ElementHost` 的行为
- 可以设置 `DockStyle`、`AutoSize` 等属性
- 调试时可以在 `ElementHost` 层做处理

#### 方式 C：`Add()` + WinForms 控件

```csharp
// 直接添加 WinForms 控件（不需要 WPF）
var winFormsPanel = new System.Windows.Forms.Panel();
ps.Add("旧面板", winFormsPanel);
```

**特点**：
- 纯 WinForms 方案
- 不需要 WPF 依赖
- 现代开发中较少使用

### 2.7 PaletteSet 生命周期管理

```csharp
public class PanelManager
{
    private static PaletteSet _ps;  // 静态引用，保持单例
    
    public void Show()
    {
        if (_ps == null)
        {
            _ps = new PaletteSet("面板");
            // 添加内容...
        }
        _ps.Visible = true;  // 显示
    }
    
    public void Hide()
    {
        if (_ps != null)
            _ps.Visible = false;  // 隐藏（不销毁）
    }
    
    public void Toggle()
    {
        if (_ps == null)
            Show();
        else
            _ps.Visible = !_ps.Visible;  // 切换
    }
    
    public void Close()
    {
        if (_ps != null)
        {
            _ps.Visible = false;
            _ps.Dispose();  // 销毁
            _ps = null;
        }
    }
}
```

### 2.8 PaletteSet 事件

```csharp
ps.StateChanged += (s, e) =>
{
    // 面板状态变化（停靠、浮动、最小化等）
};

ps.PaletteActivated += (s, e) =>
{
    // 某个选项卡被激活
    var activePalette = e.Activated;
};

ps.PaletteAdded += (s, e) =>
{
    // 新选项卡被添加
};

ps.Load += (s, e) =>
{
    // PaletteSet 加载（可以恢复状态）
};

ps.Save += (s, e) =>
{
    // PaletteSet 保存（可以保存状态）
};
```

---

## 3. WPF TabControl / TabItem 详解

### 3.1 基本结构

```xml
<TabControl>
    <TabItem Header="选项卡1">
        <!-- 选项卡1的内容 -->
        <StackPanel>
            <TextBlock Text="这是第一个选项卡" />
        </StackPanel>
    </TabItem>
    
    <TabItem Header="选项卡2">
        <!-- 选项卡2的内容 -->
        <StackPanel>
            <TextBlock Text="这是第二个选项卡" />
        </StackPanel>
    </TabItem>
</TabControl>
```

### 3.2 TabControl 关键属性

| 属性 | 类型 | 说明 | 常用值 |
|------|------|------|--------|
| `TabStripPlacement` | `Dock` | 选项卡位置 | `Top`（默认）, `Bottom`, `Left`, `Right` |
| `SelectedIndex` | `int` | 当前选中索引 | `0` 为第一个 |
| `SelectedItem` | `object` | 当前选中项 | 通常绑定 |
| `Background` | `Brush` | 背景色 | `"#3b4453"` |
| `BorderThickness` | `Thickness` | 边框 | `"0"` 无边框 |

### 3.3 TabItem 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Header` | `object` | 选项卡头部（可以是文本、图标、任意控件） |
| `Content` | `object` | 选项卡内容区域 |
| `IsSelected` | `bool` | 是否被选中 |
| `IsEnabled` | `bool` | 是否启用 |

### 3.4 TabItem Header 自定义

#### 纯文本

```xml
<TabItem Header="样式设置">
```

#### 图标 + 文本

```xml
<TabItem>
    <TabItem.Header>
        <StackPanel Orientation="Horizontal">
            <Image Source="icon.png" Width="16" Height="16" Margin="0,0,4,0" />
            <TextBlock Text="样式设置" />
        </StackPanel>
    </TabItem.Header>
</TabItem>
```

#### 带关闭按钮的选项卡

```xml
<TabItem>
    <TabItem.Header>
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="样式设置" VerticalAlignment="Center" />
            <Button Content="×" Margin="5,0,0,0" 
                    Background="Transparent" BorderThickness="0"
                    Click="CloseTab_Click" />
        </StackPanel>
    </TabItem.Header>
</TabItem>
```

### 3.5 TabStripPlacement — 选项卡位置

```xml
<!-- 默认：顶部 -->
<TabControl TabStripPlacement="Top" />

<!-- 底部 -->
<TabControl TabStripPlacement="Bottom" />

<!-- 左侧（垂直选项卡） -->
<TabControl TabStripPlacement="Left" />

<!-- 右侧 -->
<TabControl TabStripPlacement="Right" />
```

### 3.6 数据绑定方式

#### 静态定义（本项目使用）

```xml
<TabControl>
    <TabItem Header="样式设置">
        <StackPanel>
            <TextBox Text="{Binding Scale}" />
        </StackPanel>
    </TabItem>
    <TabItem Header="钢筋">
        <StackPanel>
            <TextBox Text="{Binding RebarDiameter}" />
        </StackPanel>
    </TabItem>
</TabControl>
```

#### 动态绑定（ItemsSource）

```xml
<TabControl ItemsSource="{Binding Tabs}">
    <TabControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding TabName}" />
        </DataTemplate>
    </TabControl.ItemTemplate>
    <TabControl.ContentTemplate>
        <DataTemplate>
            <StackPanel>
                <TextBox Text="{Binding Value}" />
            </StackPanel>
        </DataTemplate>
    </TabControl.ContentTemplate>
</TabControl>
```

---

## 4. 两种选项卡方案对比

### 4.1 方案一：PaletteSet 多选项卡

```
一个 PaletteSet 容器
├── Tab "钢筋"     → ReinPanel.xaml     (独立 UserControl)
├── Tab "过滤器"   → FilterPanel.xaml   (独立 UserControl)
├── Tab "基础钢筋" → BaseReinPanel.xaml (独立 UserControl)
├── Tab "桩"       → PilePanel.xaml     (独立 UserControl)
└── Tab "螺栓"     → ClusterPanel.xaml  (独立 UserControl)
```

**代码**：

```csharp
var ps = new PaletteSet("HY 主面板");
ps.AddVisual("钢筋", new ReinPanel());       // 每个 Tab 一个 UserControl
ps.AddVisual("过滤器", new FilterPanel());
ps.AddVisual("基础钢筋", new BaseReinPanel());
ps.Visible = true;
```

**效果**：选项卡在 PaletteSet **底部**，AutoCAD 原生外观。

### 4.2 方案二：WPF TabControl 内部分页

```
一个 PaletteSet 容器
└── Tab "设置" → SettingsPanel.xaml (一个 UserControl)
                 └── TabControl (WPF 内部分页)
                     ├── TabItem "样式设置"  → 文字样式、标注样式...
                     ├── TabItem "钢筋"      → 钢筋参数、尺寸参数...
                     ├── TabItem "C"         → 预留
                     └── TabItem "D"         → 预留
```

**代码（XAML）**：

```xml
<UserControl x:Class="...SettingsPanel">
    <TabControl>
        <TabItem Header="样式设置">
            <!-- 样式参数 -->
        </TabItem>
        <TabItem Header="钢筋">
            <!-- 钢筋参数 -->
        </TabItem>
    </TabControl>
</UserControl>
```

**效果**：选项卡在 WPF 控件**顶部**，完全自定义外观。

### 4.3 如何选择？

| 场景 | 推荐方案 | 原因 |
|------|----------|------|
| 功能模块差异大（钢筋 vs 桩基） | **PaletteSet 多选项卡** | 每个模块独立 ViewModel，互不干扰 |
| 同一功能的参数分组（样式 vs 钢筋参数） | **WPF TabItem** | 共享 ViewModel，数据联动方便 |
| 需要独立加载/卸载某个面板 | **PaletteSet 多选项卡** | 可以独立 `TryAddPanel` |
| 需要高度自定义的选项卡外观 | **WPF TabItem** | 完全控制 ControlTemplate |
| 混合使用 | **两者结合** | PaletteSet 分大模块，每个模块内用 TabItem 分细项 |

### 4.4 本项目的混合策略

```
PaletteSet "HY 主面板"
├── [PaletteSet Tab] "钢筋"     → ReinPanel
├── [PaletteSet Tab] "过滤器"   → FilterPanel
└── [PaletteSet Tab] "基础钢筋" → BaseReinPanel

PaletteSet "HY 设置"
└── [PaletteSet Tab] "设置"     → SettingsPanel
                                   └── [WPF TabControl]
                                       ├── [TabItem] "样式设置"
                                       ├── [TabItem] "钢筋"
                                       ├── [TabItem] "C" (预留)
                                       └── [TabItem] "D" (预留)
```

---

## 5. 项目中的实际实现

### 5.1 旧项目方式（HyCADtool）

**文件**：`HyCADtool/Commands/PanelCommand.cs`

```csharp
public class HyCommand
{
    private static PaletteSet ps;  // 静态单例

    [CommandMethod("hy")]
    public static void ShowPanel()
    {
        if (ps == null)
        {
            ps = new PaletteSet("HY的面板")
            {
                Style = PaletteSetStyles.ShowAutoHideButton 
                      | PaletteSetStyles.ShowCloseButton 
                      | PaletteSetStyles.Snappable
            };
            // 直接 new 面板，手动传依赖
            ps.AddVisual("钢筋", new ReinPanel());
            ps.AddVisual("过滤器", new FilterPanel());
            ps.AddVisual("基础钢筋", new BaseReinPanel());
            ps.AddVisual("桩", new PilePanel(_cadService, _areaFactory, _configService));
            ps.AddVisual("螺栓聚类与基础标注", new ClusterPanel());
        }
        ps.Visible = true;
    }
}
```

**特点**：
- 静态 `PaletteSet` 实例
- 手动创建面板，手动传依赖
- 简单直接，但不利于测试和扩展

### 5.2 新项目方式（HyCADTool.Refactored）

#### PanelManager — 通用面板管理器

**文件**：`HyCADTool.Refactored/Presentation/PanelManager.cs`

```csharp
public class PanelManager
{
    private readonly Dictionary<Type, PaletteSet> _paletteSets;
    private readonly Dictionary<Type, object> _panelInstances;
    private readonly IComponentContext _componentContext;

    public PanelManager(IComponentContext componentContext) { ... }

    // 泛型方法：显示指定类型的面板
    public void ShowPanel<TPanel>(string title, Guid guid) 
        where TPanel : UserControl
    {
        // 1. 已存在 → 直接显示
        if (_paletteSets.ContainsKey(typeof(TPanel)))
        {
            _paletteSets[typeof(TPanel)].Visible = true;
            return;
        }

        // 2. 创建 PaletteSet
        var ps = new PaletteSet(title, guid) { ... };

        // 3. 通过 DI 容器解析面板实例
        var panel = _componentContext.Resolve<TPanel>();

        // 4. ElementHost 包装
        var host = new ElementHost
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Child = panel
        };
        ps.Add(title, host);

        // 5. 缓存并显示
        _paletteSets[typeof(TPanel)] = ps;
        ps.Visible = true;
    }

    public void TogglePanel<TPanel>(...) { ... }  // 切换
    public void HidePanel<TPanel>() { ... }       // 隐藏
    public void ClosePanel<TPanel>() { ... }      // 销毁
}
```

**特点**：
- 依赖注入（Autofac）
- 泛型方法，类型安全
- 字典管理多个面板的生命周期

#### ShowMainPanelCommand — 集成多面板

**文件**：`HyCADTool.Refactored/Presentation/Commands/ShowMainPanelCommand.cs`

```csharp
public class ShowMainPanelCommand
{
    private static PaletteSet _mainPalette;

    [CommandMethod("HYREFACTOR")]
    public static void ShowMainPanel()
    {
        if (_mainPalette == null)
        {
            _mainPalette = new PaletteSet("HY 主面板 (重构版)") { ... };

            // DI 解析方式
            TryAddPanel<ReinPanel>(_mainPalette, "钢筋", ed);
            TryAddPanel<FilterPanel>(_mainPalette, "过滤器", ed);

            // 手动创建方式（DI 尚未就绪时的过渡方案）
            TryAddPanelDirect(() => new BaseReinPanel(), _mainPalette, "基础钢筋", ed);
        }
        _mainPalette.Visible = true;
    }

    // 通过 DI 安全添加
    private static int TryAddPanel<T>(PaletteSet palette, string tabName, Editor ed) 
        where T : UserControl
    {
        try
        {
            var panel = ServiceLocator.Container.Resolve<T>();
            palette.AddVisual(tabName, panel);
            return 1;
        }
        catch (Exception ex)
        {
            ed.WriteMessage($"\n  {tabName} 加载失败: {ex.Message}");
            return 0;
        }
    }
}
```

### 5.3 SettingsPanel — WPF TabControl 实现

**文件**：`HyCADTool.Refactored/Presentation/Views/SettingsPanel.xaml`

```xml
<UserControl x:Class="...SettingsPanel" Background="#3b4453">
    <TabControl Background="#3b4453" BorderThickness="0">
        
        <!-- 自定义 TabItem 样式 -->
        <TabControl.Resources>
            <Style TargetType="TabItem">
                <Setter Property="Foreground" Value="White" />
                <Setter Property="Background" Value="#2e3440" />
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="TabItem">
                            <Border x:Name="TabBorder" 
                                    Background="{TemplateBinding Background}" 
                                    BorderBrush="#555" BorderThickness="1,1,1,0"
                                    Padding="10,4" Margin="1,0">
                                <ContentPresenter ContentSource="Header" 
                                    HorizontalAlignment="Center" 
                                    VerticalAlignment="Center" />
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsSelected" Value="True">
                                    <Setter TargetName="TabBorder" 
                                            Property="Background" Value="#4c566a" />
                                </Trigger>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="TabBorder" 
                                            Property="Background" Value="#434c5e" />
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>
        </TabControl.Resources>

        <!-- Tab A: 样式设置 -->
        <TabItem Header="样式设置">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel>
                    <!-- 比例、文字样式、标注样式、引线样式 -->
                    <!-- 使用 Expander 折叠展开 -->
                </StackPanel>
            </ScrollViewer>
        </TabItem>

        <!-- Tab B: 钢筋参数 -->
        <TabItem Header="钢筋">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel>
                    <!-- 钢筋参数、尺寸参数 -->
                </StackPanel>
            </ScrollViewer>
        </TabItem>

        <!-- Tab C / D: 预留 -->
        <TabItem Header="C">...</TabItem>
        <TabItem Header="D">...</TabItem>
    </TabControl>
</UserControl>
```

---

## 6. WPF 嵌入 PaletteSet 的技术细节

### 6.1 关键：ElementHost 桥接

AutoCAD 的 PaletteSet 本质上是 **WinForms** 容器。要放入 WPF 控件，需要 `ElementHost`（Windows Forms Integration）作为桥接：

```
PaletteSet (WinForms 容器)
  └── ElementHost (WinForms → WPF 桥接)
       └── WPF UserControl
```

```csharp
using System.Windows.Forms.Integration;  // 需要引用

var elementHost = new ElementHost
{
    AutoSize = true,                          // 自动调整大小
    Dock = System.Windows.Forms.DockStyle.Fill,  // 填充父容器
    Child = new SettingsPanel()                // WPF UserControl
};
paletteSet.Add("设置", elementHost);
```

### 6.2 AddVisual vs Add + ElementHost

| 方法 | 底层实现 | 何时用 |
|------|----------|--------|
| `AddVisual(name, wpfControl)` | 内部自动创建 ElementHost | 简单场景，快速开发 |
| `Add(name, elementHost)` | 手动控制 ElementHost | 需要精确控制布局/行为 |

`AddVisual` 是 AutoCAD API 提供的便捷方法，内部帮你做了 `ElementHost` 的创建和配置。

### 6.3 注意事项

#### 线程安全

WPF 控件必须在 UI 线程创建。AutoCAD 命令通常在 UI 线程执行，但要注意：

```csharp
// 旧项目中的线程检查
if (Dispatcher.CurrentDispatcher.CheckAccess())
{
    DisplayPalette(ed);  // 已在 UI 线程
}
else
{
    Dispatcher.CurrentDispatcher.Invoke(() => DisplayPalette(ed));  // 切回 UI 线程
}
```

#### 资源字典加载

WPF UserControl 如果引用了资源字典，路径需要使用 `pack://` URI：

```xml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/HyCADTool.Refactored;component/Presentation/Resources/LibraryResources.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</UserControl.Resources>
```

格式说明：
```
pack://application:,,,/程序集名称;component/路径/文件名.xaml
```

#### 键盘输入问题

在 PaletteSet 中嵌入 WPF TextBox 时，可能遇到键盘输入被 AutoCAD 拦截的问题。本项目通过 `TextBoxHelper` 解决：

```xml
<TextBox Text="{Binding Scale}" 
         helper:TextBoxHelper.EnableCustomHandlers="True" />
```

---

## 7. 样式与主题定制

### 7.1 本项目的 Nord 主题配色

项目使用类 Nord 暗色主题：

| 元素 | 颜色 | 用途 |
|------|------|------|
| `#2e3440` | 深灰蓝 | TabItem 默认背景 |
| `#3b4453` | 灰蓝 | UserControl 背景、TabControl 背景 |
| `#434c5e` | 中灰蓝 | TabItem 鼠标悬停 |
| `#4c566a` | 浅灰蓝 | TabItem 选中状态 |
| `#88c0d0` | 浅蓝 | 高亮文本（预览值） |
| `#a3be8c` | 绿色 | 状态消息 |
| `#808080` | 灰色 | 辅助文本 |
| `White` | 白色 | 主要文本 |

### 7.2 完整 TabItem 样式模板

```xml
<Style TargetType="TabItem">
    <!-- 默认属性 -->
    <Setter Property="Foreground" Value="White" />
    <Setter Property="Background" Value="#2e3440" />
    
    <!-- 自定义控件模板 -->
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="TabItem">
                <Border x:Name="TabBorder" 
                        Background="{TemplateBinding Background}" 
                        BorderBrush="#555" 
                        BorderThickness="1,1,1,0"
                        Padding="10,4" 
                        Margin="1,0">
                    <ContentPresenter 
                        ContentSource="Header" 
                        HorizontalAlignment="Center" 
                        VerticalAlignment="Center" />
                </Border>
                
                <!-- 视觉状态触发器 -->
                <ControlTemplate.Triggers>
                    <!-- 选中状态 -->
                    <Trigger Property="IsSelected" Value="True">
                        <Setter TargetName="TabBorder" 
                                Property="Background" Value="#4c566a" />
                    </Trigger>
                    <!-- 悬停状态 -->
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="TabBorder" 
                                Property="Background" Value="#434c5e" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

### 7.3 Trigger 执行顺序

WPF Trigger 的优先级：**后定义的覆盖先定义的**。

```
正常状态  → Background = #2e3440
鼠标悬停  → Background = #434c5e  (覆盖正常)
选中状态  → Background = #4c566a  (覆盖悬停)
```

如果需要"选中 + 悬停"的组合效果，需要 `MultiTrigger`：

```xml
<MultiTrigger>
    <MultiTrigger.Conditions>
        <Condition Property="IsSelected" Value="True" />
        <Condition Property="IsMouseOver" Value="True" />
    </MultiTrigger.Conditions>
    <Setter TargetName="TabBorder" Property="Background" Value="#5e81ac" />
</MultiTrigger>
```

---

## 8. 常见问题与最佳实践

### 8.1 FAQ

#### Q1：PaletteSet 的选项卡和 WPF TabItem 能同时存在吗？

**能**。PaletteSet 负责最外层的选项卡（在底部），WPF TabControl 负责内部的选项卡（在顶部）。它们互不干扰。

#### Q2：PaletteSet 关闭后数据丢失了？

PaletteSet 关闭（`Visible = false`）后控件仍然存在于内存中，数据不会丢失。只有调用 `Dispose()` 才会真正销毁。

使用**静态变量**或**字典缓存**保持引用：

```csharp
private static PaletteSet _ps;  // 静态引用，永不丢失
```

#### Q3：为什么 TextBox 不能输入？

AutoCAD 会拦截键盘输入作为命令。解决方案：

1. **附加属性方案**（本项目使用）：
```csharp
// TextBoxHelper.cs
public static class TextBoxHelper
{
    public static readonly DependencyProperty EnableCustomHandlersProperty = ...;
    // 在 GotFocus 时调用 SystemObjects.DynamicLinker.SetDialogLockState(true)
}
```

2. **PreviewKeyDown 方案**：
```csharp
textBox.PreviewKeyDown += (s, e) =>
{
    e.Handled = false;  // 确保 WPF 处理按键
};
```

#### Q4：多个 PaletteSet 还是一个 PaletteSet 多选项卡？

| 方案 | 优点 | 缺点 |
|------|------|------|
| **多个 PaletteSet** | 可以分别停靠在不同位置 | 管理复杂 |
| **一个 PaletteSet 多选项卡** | 统一管理，用户体验一致 | 不能拆分停靠 |

**建议**：大多数情况用一个 PaletteSet + 多选项卡。只有当面板需要同时显示在不同位置时才用多个 PaletteSet。

#### Q5：PaletteSet 的 Guid 有什么用？

Guid 让 AutoCAD 记住面板的**停靠位置、大小、自动隐藏状态**。下次打开同一个 Guid 的 PaletteSet 时，会恢复上次的布局。

**最佳实践**：每个 PaletteSet 使用固定的、唯一的 Guid。

```csharp
// 使用常量，确保每次运行都一致
private static readonly Guid PANEL_GUID = 
    new Guid("F6A7B8C9-D0E1-2345-FA67-890ABCDEF123");
```

### 8.2 最佳实践清单

| 实践 | 说明 |
|------|------|
| **使用固定 Guid** | 保证停靠状态持久化 |
| **静态/单例管理 PaletteSet** | 避免重复创建 |
| **DI 解析面板** | 方便测试和替换 |
| **ElementHost.Dock = Fill** | 确保 WPF 填满面板区域 |
| **处理键盘输入** | TextBox 需要特殊处理 |
| **ScrollViewer 包裹内容** | TabItem 内容可能超出面板高度 |
| **使用 Expander 折叠** | 参数多时分组折叠，节省空间 |
| **浅色文字 + 暗色背景** | AutoCAD 用户习惯暗色主题 |

---

## 9. 代码模板速查

### 9.1 最小可运行示例

```csharp
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

public class MyPanelCommand
{
    private static PaletteSet _ps;

    [CommandMethod("MYPANEL")]
    public static void ShowMyPanel()
    {
        if (_ps == null)
        {
            _ps = new PaletteSet("我的面板", 
                new Guid("11111111-2222-3333-4444-555555555555"))
            {
                Style = PaletteSetStyles.ShowCloseButton 
                      | PaletteSetStyles.ShowAutoHideButton 
                      | PaletteSetStyles.Snappable
            };
            _ps.AddVisual("主页", new MyPanel());
        }
        _ps.Visible = true;
    }
}
```

### 9.2 带 DI 的完整面板

```csharp
// Command
[CommandMethod("MYPANEL")]
public static void ShowMyPanel()
{
    var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
    panelManager.TogglePanel<MyPanel>(
        "我的面板",
        new Guid("11111111-2222-3333-4444-555555555555"));
}
```

### 9.3 WPF TabControl 模板

```xml
<UserControl x:Class="MyNamespace.MyPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="#3b4453">
    
    <TabControl Background="#3b4453" BorderThickness="0">
        <!-- TabItem 暗色样式（复制 SettingsPanel 的 Resources） -->
        <TabControl.Resources>
            <Style TargetType="TabItem">
                <Setter Property="Foreground" Value="White" />
                <Setter Property="Background" Value="#2e3440" />
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="TabItem">
                            <Border x:Name="TabBorder" 
                                    Background="{TemplateBinding Background}" 
                                    BorderBrush="#555" BorderThickness="1,1,1,0"
                                    Padding="10,4" Margin="1,0">
                                <ContentPresenter ContentSource="Header" 
                                    HorizontalAlignment="Center" 
                                    VerticalAlignment="Center" />
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsSelected" Value="True">
                                    <Setter TargetName="TabBorder" 
                                            Property="Background" Value="#4c566a" />
                                </Trigger>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="TabBorder" 
                                            Property="Background" Value="#434c5e" />
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>
        </TabControl.Resources>

        <TabItem Header="基本">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="5,10,5,5">
                    <!-- 内容 -->
                </StackPanel>
            </ScrollViewer>
        </TabItem>

        <TabItem Header="高级">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="5,10,5,5">
                    <!-- 内容 -->
                </StackPanel>
            </ScrollViewer>
        </TabItem>
    </TabControl>
</UserControl>
```

### 9.4 快速对照表

| 我想要... | 用什么 | 代码 |
|-----------|--------|------|
| 创建可停靠面板 | `PaletteSet` | `new PaletteSet(title, guid)` |
| 添加一个选项卡 | `AddVisual` / `Add` | `ps.AddVisual("名称", control)` |
| 面板内部分页 | WPF `TabControl` | `<TabControl><TabItem>...` |
| 显示/隐藏面板 | `Visible` | `ps.Visible = true/false` |
| 保存停靠位置 | `Guid` | 构造函数传入固定 Guid |
| 自定义选项卡外观 | `ControlTemplate` | 重写 `TabItem` 的 Template |
| 折叠参数组 | `Expander` | `<Expander Header="参数组">` |
| 解决键盘输入 | `TextBoxHelper` | 附加属性或 Focus 处理 |

---

## 附录：项目文件索引

| 文件 | 用途 |
|------|------|
| `Presentation/PanelManager.cs` | 通用面板管理器（DI + 生命周期） |
| `Presentation/Commands/ShowMainPanelCommand.cs` | HYREFACTOR 命令（多面板集成） |
| `Presentation/Commands/ShowPanelCommand.cs` | 独立面板显示命令 |
| `Presentation/Views/SettingsPanel.xaml` | 设置面板（TabControl 示例） |
| `Presentation/Views/ReinPanel.xaml` | 钢筋面板（Expander 示例） |
| `Presentation/Views/Helpers/TextBoxHelper.cs` | TextBox 键盘输入修复 |
| `Presentation/Resources/LibraryResources.xaml` | 共享样式资源字典 |

---

> **学习建议**：先理解 PaletteSet 是"容器"、WPF 控件是"内容"、ElementHost 是"桥梁"这三者的关系。然后从 `ShowMainPanelCommand.cs`（PaletteSet 多选项卡）和 `SettingsPanel.xaml`（WPF TabControl）两个文件入手，对比理解两种选项卡方案。
