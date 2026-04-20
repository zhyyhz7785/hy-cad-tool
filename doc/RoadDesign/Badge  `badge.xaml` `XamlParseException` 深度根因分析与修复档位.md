# Badge / `badge.xaml` `XamlParseException` 真根因分析与修复（v3 最终）

> 适用版本：HyCADTool.Refactored v1.x（含 ReCall 热重载、PaletteSet 宿主、AdWindows Ribbon Badge）
> 写给：决策修复方案的人（不只是改代码的人）
> **修复落地位置**：`ReCall/Recall.cs::ResolveAssembly` + `AutoCadHostAssemblyNames` 黑名单
> **本文档前两版的"根因 A/B"猜测均已被运行时取证证伪**，详见 §6 反例存档

---

## 1. 现象

### 1.1 用户实际看到的栈

```
✗ [WPF UI] XamlParseException: 对类型"Autodesk.Internal.Windows.Badge"
   的构造函数执行符合指定的绑定约束的调用时引发了异常。
  内层：Exception: 组件"Autodesk.Internal.Windows.Badge"不具有由
   URI"/AdWindows;component/themes/badge.xaml"识别的资源。
```

异常的 InnerException 栈（取证 debug session 276061）：
```
at System.Windows.Application.LoadComponent(Object component, Uri resourceLocator)
at Autodesk.Internal.Windows.Badge.InitializeComponent()
at Autodesk.Internal.Windows.Badge..ctor()
at System.RuntimeType.CreateInstanceDefaultCtor(...)
... PanelListView.MeasureOverride / ApplyTemplate ...
```

### 1.2 触发时机

- `PluginInitializer.Initialize()` 完成 6 秒后
- AutoCAD Ribbon 的 `PanelListView` 异步渲染 → `new Badge()` → `Badge.InitializeComponent` → `Application.LoadComponent(this, /AdWindows;component/themes/badge.xaml)` → 失败
- 与我们是否挂 HyCAD Tab 无关；AutoCAD 自带 Tab 任意一个 Badge 都会触发

---

## 2. 真根因（运行时取证 2026-04-21）

### 2.1 取证方法

`PluginInitializer` 内插桩，在 `Dispatcher.UnhandledException` handler 里捕获 Badge 异常时枚举 `AppDomain.GetAssemblies()` 中所有 short-name == "AdWindows" 的程序集，记录 FullName / Location / objHash / moduleHash。

### 2.2 关键证据

Initialize 完成时（异常前）：

| 时间 | AdWindows count | 描述 |
|---|---|---|
| Initialize 入口 | 1 | 5.1.1.1 @ Program Files |
| Initialize 完成 | 1 | 5.1.1.1 @ Program Files |
| **Badge 异常爆发（6 秒后）** | **3** | 1× 5.1.1.1 + 2× 5.0.1.2 (`location=""`) |

`location == ""` 是 `Assembly.Load(byte[])` 加载的特征——这正是 ReCall 的工作模式。

### 2.3 完整因果链

1. `HyCADTool.Refactored.csproj` 引用 `AutoCAD.NET 24.3.0` NuGet 包，其传递引用包含 `AdWindows 5.0.1.2`（旧版本，对应 AutoCAD 2021 时代）。
2. 项目设置 `<CopyLocalLockFileAssemblyies>true</CopyLocalLockFileAssemblies>` → `AdWindows.dll` 5.0.1.2 复制进 `bin/Debug`。
3. ReCall C2 把整个 `bin/Debug` 复制到 `_currentDependenciesPath`（临时目录）。
4. **AutoCAD 2025 进程实际加载 5.1.1.1**（位置 `C:\Program Files\Autodesk\AutoCAD 2025\AdWindows.dll`）—— 与 manifest 引用版本不同。
5. UI 渲染 `PanelListView` 触发 `Badge.InitializeComponent()`，此时 CLR 沿 type ref 链严格按 manifest 找 **5.0.1.2** → AppDomain 没有 → 触发 `AppDomain.AssemblyResolve`。
6. ReCall 旧版 `ResolveAssembly` 在 `_currentDependenciesPath` 找到 `AdWindows.dll`（5.0.1.2）→ `Assembly.Load(byte[])` 加载 → AppDomain 多出**第二份** AdWindows。
7. 不同 requesting assembly 触发**两次** AssemblyResolve → 加载**两份** 5.0.1.2（取证 line 7 显示 objHash=57916962 与 23765354 两个不同实例）。
8. **类型身份割裂**：CLR 把 `Badge` 类型按 `[Assembly+TypeFullName]` 哈希，5.0.1.2 的 `Badge` ≠ 5.1.1.1 的 `Badge`。BAML 资源系统按程序集身份找 `themes/badge.xaml`，5.0.1.2 程序集的 manifest 资源虽然包含 `themes/badge.xaml.baml`，但 BAML 内部记录的 type ref 与 5.1.1.1 的 Badge type 不匹配 → 抛"组件 Badge 不具有由 URI 识别的资源"。

---

## 3. 修复

**修改位置**：`ReCall/Recall.cs::ResolveAssembly`

**核心机制**：AutoCAD 进程已加载的宿主程序集请求一律走 `AppDomain.GetAssemblies()` 短名匹配，**绝不**从 ReCall 临时 deps 目录 byte[] 加载。返回的 5.1.1.1 与 manifest 要的 5.0.1.2 版本不同也无所谓——CLR `AssemblyResolve` handler 返回值是被信任的"版本替代"，CLR 不会拒绝。

```csharp
private static readonly string[] AutoCadHostAssemblyNames = {
    "AdWindows", "AcMr", "AcCoreMgd", "AcDbMgd", "AcMgd", "AcCui", "AcWindows",
    "Autodesk.AutoCAD.Interop", "Autodesk.AutoCAD.Interop.Common",
    "PresentationCore", "PresentationFramework", "WindowsBase", "System.Xaml",
};

private static Assembly ResolveAssembly(ResolveEventArgs args, string dependenciesPath, string nugetPackagesPath)
{
    if (args.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
        return null;

    string shortName = new AssemblyName(args.Name).Name;

    if (AutoCadHostAssemblyNames.Contains(shortName, StringComparer.OrdinalIgnoreCase))
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, shortName, StringComparison.OrdinalIgnoreCase));
    }

    // ... 原有 dependencies / nuget 路径不变
}
```

---

## 4. 为什么之前的"档位 1/2"修复都失败

### 4.1 档位 1（旧版）：Badge 双段预热

```csharp
// 错误做法：旧 WarmupAdWindowsBadgeTheme()
var info = Application.GetResourceStream(uri);
XamlReader.Load(info.Stream);  // ← API 误用：不能读 BAML 二进制流，抛 XmlException 0x0C
new ResourceDictionary { Source = packBadge };  // ← badge.xaml 不是顶级 ResourceDictionary
```

两段都失败被 `try/catch` 吞掉，对运行时 Badge 0 效果。但因为它先执行了 `EnsureAdWindowsAssemblyInAppDomain()` 主动 Load AdWindows，部分情况下 AppDomain 里反而**少**一份 5.0.1.2（运气好），让人误以为预热"有用"。

### 4.2 档位 2（旧版）：删 ResourceAssembly + SubView 预热绝对 URI

基于"根因 A：`Application.ResourceAssembly` 钉到插件让 Badge 解析被牵走"的猜测。但运行时取证显示：删 ResourceAssembly 之后 Badge 错误**仍然出现**，证明这条假设是错的。Badge 用的 `/AdWindows;component/themes/badge.xaml` 是带程序集名前缀的相对 URI，WPF 解析时优先用 URI 里指定的程序集名，**与 ResourceAssembly 无关**。

### 4.3 共同盲区

两个旧档位都把火力对准 WPF 资源系统（`Application.ResourceAssembly` / `LoadComponent`），但**真正的元凶是 ReCall 的 AssemblyResolve handler**——它在 6 秒后被 CLR 触发，悄悄把 5.0.1.2 byte[] 加载进 AppDomain。Initialize 同步阶段的快照看不到双载入，所以早期插桩没发现。

---

## 5. 副产物清理

修复同时撤销了所有"为修 Badge 而引入"的无效代码：

| 删除 | 位置 | 原因 |
|---|---|---|
| `WarmupAdWindowsBadgeTheme()` 函数 + 调用 | `PluginInitializer.cs` | API 误用 + 与真根因无关 |
| `WarmupSubViews()` 函数 + 调用 | `PluginInitializer.cs` | XamlReader.Load(stream) 不能读 BAML，11 个 SubView 全失败 |
| `EnsureAdWindowsAssemblyInAppDomain()` 函数 | `PluginInitializer.cs` | 真根因是 ReCall 反向 byte[] 加载，主动 Load 解决不了 |
| `using System.Windows.Markup;` | `PluginInitializer.cs` | XamlReader 不再使用 |

保留：

- `WarmupBlenderTheme()` —— 这个真正有效（顶级 ResourceDictionary，正确 API），跨程序集 BlenderTheme.xaml 同步驻留 PackUriHelper 缓存。

---

## 6. 反例存档（已被运行时证据证伪，禁止再走）

| 错误猜测 | 文档来源 | 证伪证据 |
|---|---|---|
| 根因 A：钉 `Application.ResourceAssembly` 让 Badge 解析被牵走 | 本文档 v1/v2 §3.1 | 删 ResourceAssembly 之后 Badge 错误仍出现（取证 line 7 `count=3`） |
| 根因 B：AppDomain 双 LoadContext（Default vs LoadFrom） | 本文档 v1/v2 §3.2 | 真正的双载入是 `Assembly.Load(byte[])` 引发的 anonymous LoadContext，不是 Default vs LoadFrom |
| WarmupAdWindowsBadgeTheme 双段预热可解决 | 本文档 v1/v2 §4 | 双段预热两段都失败被 catch 吞掉，对运行时 0 效果 |
| 主动 Assembly.Load("AdWindows") 防止双载入 | 本文档 v1/v2 §4 | 双载入由 ReCall AssemblyResolve handler 引发，主动 Load 与之无关 |
| WarmupSubViews 用绝对 pack URI + XamlReader.Load(stream) | 本文档 v2 §4 | API 误用，11 个 SubView 全部预热失败，命令行刷 11 条假阳性 |

---

## 7. 防回归清单

修改任何下列代码必须对照本文档：

1. **`ReCall/Recall.cs::ResolveAssembly`** —— 改前先看本文档 §3。删除黑名单 = 立刻复发 Badge 错误。
2. **`HyCADTool.Refactored.csproj`** —— 加新 `PackageReference` 时检查传递引用是否包含 AutoCAD 宿主程序集（`AcMr` / `AdWindows` / `AcCoreMgd` 等旧版）。如果是，加 `<ExcludeAssets>runtime</ExcludeAssets>` 防复制进 bin/Debug。
3. **`PluginInitializer.cs::Initialize`** —— **不要**重新引入 `Application.ResourceAssembly = ...`、`Assembly.Load("AdWindows")` 或任何 Badge / SubView 预热（已证伪的反向操作）。
4. **新发现 AutoCAD 宿主程序集**（如 AutoCAD 升级新版本引入新 AcXxx）—— 加进 `AutoCadHostAssemblyNames` 数组。

---

**版本**: 3.0 | **更新**: 2026-04-21（真根因落地：ReCall AssemblyResolve 双载入；档位 1/2 旧猜测全部证伪并存档）
