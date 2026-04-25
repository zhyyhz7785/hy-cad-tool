---
name: hycad-project-pitfalls
description: |
  HyCADTool 项目（HyCADTool.Refactored + HyCAD.BlenderUI + AutoCAD 插件）全部已验证的陷阱清单。
  覆盖：AutoCAD API 多文档 / 单例 Database 缓存 / Table Title 自动合并 / 样式与图层初始化 / 配置分裂 /
  HyRoad 图层注册清单分裂（HyRoadLayers 常量 ↔ PluginInitializer.GetRequiredLayers() 手写清单必须同步，
  否则 Entity.Layer 静默回落 0 层；锁定层写入强制用 LayerLockScope.Unlock，finally 吞异常）；
  WPF + PaletteSet 宿主：隐式 Style 原生崩溃 / StaticResource 跨字典 / DynamicResource 类型错配 /
  ControlTemplate.Triggers 位置 / Trigger.TargetName 可达性 / MarkupExtension 当 Converter 递归 /
  子 UserControl 未本地 Merge 主题 / PaletteSet 原框强拆 /
  ResourceDictionary 自动 Seal 强冻 SolidColorBrush 致主题切换抛 InvalidOperationException 升级 e0434352；
  AdWindows Badge/badge.xaml：真根因为 ReCall AssemblyResolve 把项目传递引用的旧 AdWindows 5.0.1.2
  byte[] 加载，与 AutoCAD 进程 5.1.1.1 双载入 → 类型身份割裂。修复在 ReCall/Recall.cs
  ResolveAssembly 增加 AutoCAD 宿主程序集黑名单（B11 真根因落地 / doc/RoadDesign/00.md）；
  构建系统：Refactored→ReCall 用 ProjectReference 链式触发 ReCall obj→bin 复制，
  AutoCAD 锁着 ReCall.dll 时致 MSB3027/MSB3021 整个方案构建中断、Refactored.dll 无产出，
  C2 热重载机制失效。修复改为 Reference+HintPath 单向二进制引用断开 MSBuild 项目依赖链
  + EnsureReCallDllExists cold-start 兜底（C1 落地）；
  csproj `<PlatformTarget>x64</PlatformTarget>` 致 dll 编译为 PE32+ x64-only，
  而 .NET Framework WPF 项目的 VS XAML 设计器宿主（XDesProc.exe / WpfSurface.exe）
  永远以 32 位运行且没有 64 位开关，加载 x64 dll 抛 BadImageFormatException
  → 错误列表表现为 XDG0023/XDG0003「未能加载 HyCADTool.Refactored 或它的某一个依赖项」+
  XDG-0001 跨程序集 pack URI 全员阵亡 + XDG0010 BlenderButton/Splitter 全部找不到（连带）。
  修复改 PlatformTarget=AnyCPU + Prefer32Bit=false（设计时 32 位能 LoadFrom，
  运行时在 64 位 AutoCAD 进程里 JIT 成 64 位行为等同 x64）。ReCall.csproj 不动，
  设计器从不引用它（C2 落地）；
  Debug 方法论：调试类与调用点同步删除 / session ID 不入生产代码 /
  Dispatcher.UnhandledException handler shutdown 流程 NRE 升级原生致命 /
  PresentationTraceSources Binding 错误同步阻塞 UI 线程 / VS 错误清单按编译阻塞性分类。
  使用场景：写 AutoCAD 命令 / 新建 WPF 面板 / 改资源字典 / 迁移命令到 Refactored / 多文档联调 / 建 Table /
  Cursor Debug 模式收尾撤埋点 / 处理 AutoCAD 关闭崩溃 / 处理统一面板点击卡顿 /
  增删跨项目引用 / 处理 AutoCAD 锁文件致 Build 失败 /
  处理 VS XAML 设计器报 XDG0023/XDG0003/XDG-0001 全员加载失败 / 改 csproj 平台目标。
  本 skill 替代：wpf-paletteset-avoid-implicit-styles / wpf-blender-panel-guideline /
  hycad-autocad-singleton-database-context / hycad-multidoc-panel-resource-init /
  .cursor/rules/04-AutoCAD-Table陷阱.mdc。
author: Cursor Agent
version: 1.11.0
date: 2026-04-25
---

# HyCAD 项目级闭坑清单

> 本 skill 收录本仓库**已踩过且已在关键路径落实缓解或修复**的陷阱。条目按"症状 → 触发条件 → 根因 → 正确做法 → 反例 → 已修复案例"组织。
>
> **§ 验证状态与残留风险**（2026-04-25 对照 `HyCADTool.Refactored` / `ReCall` / `HyCAD.BlenderUI` 代码核对）见文末 **§ 验证状态与残留风险** 一节；**不要**把本 skill 当成「永不再现」的数学保证——宿主为 AutoCAD + 多程序集 WPF，仍有版本差与环境差。
>
> 四大域：
>
> - **A 域**：AutoCAD 运行时（多文档、事务、Table、配置、样式）
> - **B 域**：WPF + PaletteSet 宿主 + XAML 资源字典
> - **C 域**：构建系统（MSBuild 项目依赖、AutoCAD 锁文件、热重载与编译期引用解耦）
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

**正确做法**（与当前 `PluginInitializer` 实现一致）

1. 分层：`PanelManager` 只管 UI / `DataContext` 切换；`PluginInitializer` 管文档级资源
2. `Initialize()` → `InitializeStylesAndLayers()` 里对**当前活动文档** `EnsureCurrentDocumentResourcesInitialized(force: true)` 一次
3. 订阅 `DocumentActivated`：每次激活文档调用 `EnsureCurrentDocumentResourcesInitialized(force: false)`（内部用 `MdiActiveDocument` + `_initializedDocuments` 幂等）
4. 订阅 `DocumentCreated`：**仅** `SettingsPanelViewModel.GetOrCreate(e.Document.Name, …)` 预热 VM，**不**在此处跑完整 Ensure —— 因 `EnsureCurrentDocumentResourcesInitialized` 实现绑定 `MdiActiveDocument`，新建图当下活动文档可能仍是旧图，强行 Ensure 会写到错误库；完整图层/样式以**首次切换到该图**（`DocumentActivated`）为准
5. 资源初始化固定流程（Ensure 内部）：

   ```csharp
   var vm = SettingsPanelViewModel.GetOrCreate(documentName, styleService);
   vm.LoadSettings();
   vm.EnsureStylesApplied();
   layerService.CreateMultipleLayers(...);
   ```

不要把资源初始化逻辑写进 WPF 面板事件。

> **曾写入旧版 skill 的表述**「DocumentCreated 也每次 Ensure」与**当前代码**不一致；若产品要求「新图一创建、尚未切换就要图层齐全」，需重构 `Ensure…` 为接受显式 `Document` 参数后再从 Created 调用。

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

### A7【改 2026-04-21 v2】HyRoad 图层注册清单分裂：`HyRoadLayers.GetAll()` vs `PluginInitializer.GetRequiredLayers()`

**现象**

- 冷启动 AutoCAD，新 DWG 直接跑拾取命令
- 期望：`05_hy_道路_原线` 上出现一条锁定状态的 252 灰 Polyline（RawPick 档案）
- 实际：Polyline 出现在 `0` 图层、显示为**默认色（黄或白）**，并不锁定
- 或者：新开 DWG 点「预览」— LivePreview 也落到 `0` 图层的默认色

**触发条件**

- 新增了 `HyRoadLayers.XxxLayer` 常量（如 `RawPolylineLayer` / `LivePreviewLayer`）
- **但**忘记同步到 `PluginInitializer.GetRequiredLayers()` 的手写清单中
- Entity 创建时 `entity.Layer = "<不存在的层名>"` → AutoCAD 静默回落到当前图层 `"0"`，不抛异常

**根因**：HyCAD 项目存在<b>两份并行维护</b>的图层清单

- `HyCADTool.Refactored\Infrastructure\AutoCAD\Xdata\HyRoadLayers.cs` — 常量 + `GetAll()` 语义清单（服务代码查名用）
- `HyCADTool.Refactored\Presentation\PluginInitializer.cs` `GetRequiredLayers()` — 启动批量创建用的手写 `(name, color)[]`

两份清单由不同提交维护，很容易一边加常量一边忘更新创建清单。AutoCAD 对 `pl.Layer = "unknownName"` 不报错只回落，debug 时只能靠眼睛看颜色发现不对。

**正确做法**

1. 新增 `HyRoad*Layer` 常量时**必须同步更新** `PluginInitializer.GetRequiredLayers()`（或后续重构为直接 `foreach HyRoadLayers.GetAll()` 取代手写清单）
2. 图层有**锁定 / 冻结 / 线型**等特殊属性时走 `HyRoadLayerInitializer` 独立静态方法（`GetRequiredLayers()` 签名只有 `(name, color)`，承载不了 lock 位）。典型：
   - `HyRoadLayerInitializer.EnsureRawPolylineLayerLocked(doc)` — 保证 `05_hy_道路_原线` 存在且 `IsLocked = true`
   - 调用点：`PluginInitializer.InitializeStylesAndLayers()` 批量创建之后
3. 向**锁定层**写入 / 擦除实体的事务块必须包裹 `LayerLockScope.Unlock(tr, db, layerName)`：

```csharp
using (doc.LockDocument())
using (var tr = db.TransactionManager.StartTransaction())
{
    using (LayerLockScope.Unlock(tr, db, HyRoadLayers.RawPolylineLayer))
    {
        // 在同一事务里向锁定层 Append / Erase；Dispose 时自动恢复锁定
        var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        btr.AppendEntity(poly);
        tr.AddNewlyCreatedDBObject(poly, true);
    }
    tr.Commit();
}
```

`LayerLockScope` 内部：构造时记录 `LayerTableRecord.IsLocked` 原值 → `UpgradeOpen` 置 false；`Dispose` 时若原本锁定就 `UpgradeOpen` 改回 true，**所有异常全部吞**（避免 AutoCAD shutdown 路径上 finally 抛出升级为原生崩）。

**反例**（2026-04-21 实测 bug 链）

```csharp
// GetRequiredLayers() 忘加 RawPolylineLayer + LivePreviewLayer
private (string, short)[] GetRequiredLayers() => new[]
{
    (HyRoadLayers.AlignmentLayer, HyRoadLayers.AlignmentLayerColor),
    // ← 缺 RawPolylineLayer / LivePreviewLayer
};

// 拾取命令里向"不存在的层"写实体
var pl = RoadGeometryBridge.ToAutoCadPolyline(poly);
pl.Layer = HyRoadLayers.RawPolylineLayer;  // ← AutoCAD 静默回落到 "0"
pl.Color = Color.FromColorIndex(ColorMethod.ByLayer, 0);
btr.AppendEntity(pl);
// 结果：0 层黄线，不是预期的 252 灰锁定线
```

**已修复**：

- `HyCADTool.Refactored\Presentation\PluginInitializer.cs` `GetRequiredLayers()` 补齐 `RawPolylineLayer` + `LivePreviewLayer`
- 新增 `HyCADTool.Refactored\Infrastructure\AutoCAD\Xdata\HyRoadLayerInitializer.cs`，启动调用 `EnsureRawPolylineLayerLocked`
- 新增 `HyCADTool.Refactored\Infrastructure\AutoCAD\Services\Road\LayerLockScope.cs`（`IDisposable`）
- `RoadAlignmentRawPolylineService.DrawAll/DrawForAlignment/EraseAll` + `RoadAlignmentApplyService.Apply` 所有对原线层的读写包裹 `LayerLockScope.Unlock`
- 顺便下线了 DesignPreview 自动彩色机制：原线层现在**只**容纳 RawPick 一种 KIND，不再需要"同层多 KIND"共存（但 `KindAlignmentDesignPreview` 常量保留，用于 Apply 清理历史 DWG 残留）

**规则**

- 新增 `HyRoadLayers` 常量 → 必须审查 `PluginInitializer.GetRequiredLayers()` 是否同步
- 图层有锁定 / 冻结 / 线型特殊属性 → 独立 `HyRoadLayerInitializer.EnsureXxx(doc)`，不要硬塞进批量清单
- 锁定层写入**必须**用 `LayerLockScope.Unlock`；禁止在命令里直接 `layer.IsLocked = false` 不复原
- 同层多 KIND（历史场景）如需共存，擦除严格按 `(KIND, Id)` 过滤；禁止 `EraseByLayer` 作为公开 API

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

### B4a【新 2026-04-21】hy 面板尺寸 `Metric_*`（仅用 `DynamicResource`）

**权威表**：[`HyCAD.BlenderUI/Themes/Metrics.xaml`](../../../HyCAD.BlenderUI/Themes/Metrics.xaml) — 字体档、行高、输入宽、标签宽、工具栏按钮最小宽、`Thickness` 类间距等。

**规则**

- 业务 XAML（`HyCADTool.Refactored` 面板、Preferences、Road 工作区等）中，凡 `FontSize` / `Height` / `Width` / `MinWidth` / `MinHeight` / 典型 `Padding` / `Margin`（复用厚度），**一律** `{DynamicResource Metric_xxx}`；**禁止** `{StaticResource Metric_*}`（B4：`Thickness` / `Double` 跨字典 `StaticResource` 类型错配可致原生崩溃）。
- **禁止**在 `Metrics.xaml` 引入 `GridLength` 资源键；`ColumnDefinition` / `RowDefinition` 的 `Width` / `Height` 继续用字面量（或本字典内 `StaticResource` 且类型为 `GridLength` 的资源）。
- 顶层 `Window` 的 `Width`/`Height` 是否字面量由产品决定；控件与排版度量仍走 `Metric_*`。
- **运行时比例（2026-04-21）**：用户在「界面 → 尺寸」调整三类比例并写入 `hy-settings.json`；`BlenderMetricsScaleManager` 按命名规则从 `Metrics.xaml` **基值**派生。**禁止**为应用 Metric 而把整份 `BlenderTheme` merge 到 `Application.Current.Resources`（B1/B2 宿主污染）。正确做法：在合并了 `BlenderTheme` 的每个根 `Window` / `UserControl` 上设 `btmetrics:BlenderMetricsOverlay.Attach="True"`（`HyCAD.BlenderUI.Theming`），在本地 `MergedDictionaries` **首位**插入仅含缩放后 `Metric_*` 的 overlay，覆盖下层同名键。`Metrics.xaml` 仍是权威基值表；调密度优先走设置页比例，避免手改 XAML 与落盘值漂移。

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

### B11【真根因落地 · 2026-04-21 取证】AdWindows `Badge` / `badge.xaml`：ReCall AssemblyResolve 双载入 5.0.1.2 vs 5.1.1.1

> 详细分析与运行时取证：`doc/RoadDesign/00.md`。

**现象**

- 命令行或 `InstallWpfExceptionTraps` 打出：`XamlParseException`，对类型 `Autodesk.Internal.Windows.Badge`…
- **内层**：组件 `Badge` 不具有由 URI **`/AdWindows;component/themes/badge.xaml`** 识别的资源。
- 堆栈常见：`Autodesk.Private.Windows.PanelListView` / `PanelSetListView` → `Badge.InitializeComponent` → `Application.LoadComponent(this, uri)` → `FrameworkTemplate.LoadContent` → `MeasureOverride`。
- 触发时机：**Initialize 返回之后**，AutoCAD Ribbon 在 idle tick `new Badge()`（即使我们没挂 HyCAD Tab，AutoCAD 自带 Tab 一样会触发）。

**真根因（运行时证据 2026-04-21 取证 debug session 276061）**

Badge 异常发生时，AppDomain 里 `AdWindows` count = **3**：
- 1× **5.1.1.1**（`C:\Program Files\Autodesk\AutoCAD 2025\AdWindows.dll`，进程加载）
- 2× **5.0.1.2**（`location=""` → `Assembly.Load(byte[])` 加载特征）

**机理链路**：
1. `HyCADTool.Refactored.csproj` → `AutoCAD.NET 24.3.0` NuGet 包 → 传递引用 `AdWindows 5.0.1.2`（旧版）。
2. `<CopyLocalLockFileAssemblyies>true</CopyLocalLockFileAssemblies>` → `AdWindows.dll` 5.0.1.2 复制进 bin/Debug → ReCall C2 复制到临时目录。
3. AutoCAD 2025 进程实际加载 5.1.1.1。
4. UI 渲染 `PanelListView` 触发 `Badge.InitializeComponent()`，CLR 沿 type ref 链严格按 manifest 找 5.0.1.2 → AppDomain 没有 → `AssemblyResolve`。
5. ReCall `ResolveAssembly` 从临时目录 `Assembly.Load(byte[])` 加载 5.0.1.2，不同 requesting assembly 触发**两次** → 多出 2 份 5.0.1.2。
6. **类型身份割裂**：5.0.1.2 的 `Badge` ≠ 5.1.1.1 的 `Badge`（CLR 类型身份按 [Assembly+TypeName] 算），BAML 资源解析按程序集身份找 themes/badge.xaml → 找不到匹配。

**正确做法（仓库定局）**

1. ✅ ReCall `ResolveAssembly` 实现 **AutoCAD 宿主程序集黑名单**：`AdWindows` / `AcMr` / `AcCoreMgd` / `AcDbMgd` / `AcMgd` / `AcCui` / `AcWindows` / `Autodesk.AutoCAD.Interop` / `PresentationCore` / `PresentationFramework` / `WindowsBase` / `System.Xaml` 一律走 `AppDomain.GetAssemblies()` 短名匹配，**绝不**从 ReCall 临时 deps 目录 byte[] 加载。
2. ❌ **绝不**给 `Application.ResourceAssembly` 赋值（无效，但是个稳定噪音源）。
3. ❌ **绝不**主动 `Assembly.Load("AdWindows")` / `Assembly.LoadFrom("...AdWindows.dll")`。
4. ❌ **绝不**预热 AdWindows / Badge / SubView：`WarmupAdWindowsBadgeTheme` / `WarmupSubViews` 都已删除。原因：`XamlReader.Load(stream)` 不能读 BAML 二进制流（API 误用，抛 XmlException 0x0C 被 catch 吞掉），UserControl/Style 不是顶级 ResourceDictionary 也用不了 `new ResourceDictionary { Source = }`。
5. ✅ 仅 `WarmupBlenderTheme()` 保留——这是真正有效的（顶级 ResourceDictionary，正确 API）。
6. ✅ 仍禁止把 HyCAD 主题 merge 进 `Application.Current.Resources`（B10）。

**历史错误猜测（已被运行时证据证伪，禁止再走老路）**

| 错误猜测 | 证伪证据 |
|---|---|
| ❌ "根因 A：`Application.ResourceAssembly` 钉插件程序集让 Badge 解析被牵走" | 删了 ResourceAssembly 后 Badge 错误**仍然出现**（取证日志 line 7 `count=3`）。Badge 用的 `/AdWindows;component/...` 自带程序集名前缀，跟 ResourceAssembly 无关。|
| ❌ "WarmupAdWindowsBadgeTheme 双段预热可解决" | 双段预热**两段都失败**被 try/catch 吞掉，对运行时 0 效果（取证日志行 4/5/14/15 全部 catch 分支）。|
| ❌ "WarmupSubViews 用绝对 pack URI + XamlReader.Load(stream)" | API 误用，11 个 SubView 全部预热失败，命令行刷 11 条假阳性。|
| ❌ "主动 Assembly.Load("AdWindows") 防止运行时双载入" | 双载入是 ReCall AssemblyResolve handler 引发的，不是"是否主动 Load"问题。|

**反例（已撤销）**

```csharp
// 错误 1：ReCall ResolveAssembly 不分宿主程序集，把 5.0.1.2 byte[] 加载进 AppDomain
return Assembly.Load(File.ReadAllBytes(Path.Combine(dependenciesPath, "AdWindows.dll")));

// 错误 2：把全局 base 钉到自己 dll —— 完全无效噪音
System.Windows.Application.ResourceAssembly = typeof(PluginInitializer).Assembly;

// 错误 3：主动加载——按需解析时同样会被 ReCall handler 接走，无意义
try { Assembly.Load("AdWindows"); } catch { Assembly.LoadFrom(...); }

// 错误 4：SubView/Badge 预热，用 XamlReader.Load 读 BAML —— API 误用，全部失败
var info = Application.GetResourceStream(uri);
XamlReader.Load(info.Stream);  // 抛 XmlException 0x0C
```

**已修复文件**

- `ReCall/Recall.cs`（2026-04-21）：
  - `ResolveAssembly`：增加 `AutoCadHostAssemblyNames` 黑名单，宿主程序集请求一律走 `AppDomain` 已加载查表，绝不从 deps 目录 byte[] 加载。
- `HyCADTool.Refactored/Presentation/PluginInitializer.cs`（2026-04-21）：
  - `Initialize`：移除 `WarmupAdWindowsBadgeTheme()` 与 `WarmupSubViews()` 调用（无效死代码）。
  - 移除 `WarmupSubViews` / `WarmupAdWindowsBadgeTheme` / `EnsureAdWindowsAssemblyInAppDomain` 函数定义。
  - 移除 `using System.Windows.Markup;`。
  - 仅保留 `WarmupBlenderTheme()`（真正有效的预热）。

**回归提示**

- 改 `ReCall.ResolveAssembly` 黑名单的 PR，必须保证黑名单覆盖到所有 AutoCAD 进程已加载的程序集（用 `Process.GetCurrentProcess().Modules` 枚举对照）。
- 改 `HyCADTool.Refactored.csproj` 加新 PackageReference 时，检查传递引用是否包含 AutoCAD 宿主程序集；包含则加 `<ExcludeAssets>runtime</ExcludeAssets>`。
- 看到 Badge / badge.xaml 报错：第一句话先答 **「检查 ReCall AssemblyResolve 黑名单是否完整 + bin/Debug 里是否多出 AutoCAD 宿主 dll」**。**绝不**重新引入 `Application.ResourceAssembly` 赋值、`Assembly.Load("AdWindows")` 或 Badge 预热代码（这些都是已证伪的反向操作）。

---

## C 域：构建系统 / MSBuild 项目依赖

> 公共背景：本仓库 ReCall 设计为"AutoCAD 唯一直接 NETLOAD 的入口程序集"——`bin\Debug\ReCall.dll` 只要 AutoCAD 进程在跑就被锁，**这是常态、不是异常**。日常业务代码改动只在 Refactored，由 C2 命令 byte[] 热重载，永远不需要更新 ReCall.dll。任何把 ReCall 列入"每次构建都被检查/复制"链路的依赖关系，都会在 AutoCAD 开着时直接打断 Refactored 的构建。

---

### C1【新 2026-04-21】Refactored→ReCall 用 `<ProjectReference>` 致 AutoCAD 锁定 ReCall.dll 时整个解决方案构建中断

**现象**

- AutoCAD 开着调试 Refactored，VS 重新生成 HyCADToolGpt 解决方案（或单独 Build/Rebuild HyCADTool.Refactored）
- MSBuild 报错 `MSB3027: 无法将 obj\Debug\ReCall.dll 复制到 bin\Debug\ReCall.dll。超出了重试计数 10。失败。文件被"AutoCAD Application (PID)"锁定`
- 紧跟 `MSB3021: 无法将文件 obj\Debug\ReCall.dll 复制到 bin\Debug\ReCall.dll`
- 整个解决方案构建中断 → `HyCADTool.Refactored.dll` 没有重新产出 → 切回 AutoCAD 输 `C2` 加载的还是旧 Refactored → 看似"ReCall 热重载机制完全失效"
- 用户感受："那我 ReCall 代码没有任何意义了"

**触发条件**

1. `HyCADTool.Refactored.csproj` 含 `<ProjectReference Include="..\ReCall\ReCall.csproj">`（即使 `<Private>false</Private>`）
2. AutoCAD 进程已 NETLOAD `bin\Debug\ReCall.dll` → 文件被独占锁定
3. 任意触发 ReCall 重建的条件成立：用户 `Rebuild Solution`（强制全建）/ `commands.json` 改动 / `obj\Debug\ReCall.dll` 时间戳被外部刷新（git checkout 等）/ MSBuild 增量判定误判

**根因（双层叠加）**

第一层 — **ProjectReference 是构建依赖关系**：MSBuild 处理 `ProjectReference` 时**总是**调用上游项目的 `Build` Target，不仅仅是取一个引用路径。即便 ReCall 源码没改、CSC 跳过 CoreCompile，MSBuild 仍会执行 `CopyFilesToOutputDirectory` 的增量检查；任何让该 Target 判定"需要复制"的边界条件（obj 比 bin 新一秒、`@(IntermediateAssembly)` 元数据失效等）都会触发对锁定 dll 的覆写尝试。

第二层 — **SDK Target 末段重新导入**：`Microsoft.NET.Sdk` 在项目内容**之后**导入 `Microsoft.Common.CurrentVersion.targets`，所以即使在 ReCall.csproj 用同名 `<Target Name="CopyFilesToOutputDirectory" Condition="...">` 试图覆写跳过复制，**也会被 SDK 末段的同名 Target 覆盖**（"最后一个定义胜出"规则）。本次会话已实测验证：`dotnet build` 仍报 MSB3027/MSB3021。要让覆写生效必须放进 `Directory.Build.targets`，但那对单项目修复来说是过度工程。

**真正修复**：从根本上**断开 MSBuild 项目依赖链**——把 `<ProjectReference>` 换成 `<Reference HintPath>`。

```xml
<!-- HyCADTool.Refactored.csproj：旧（已删除） -->
<ProjectReference Include="..\ReCall\ReCall.csproj">
    <Private>false</Private>
</ProjectReference>

<!-- HyCADTool.Refactored.csproj：新 -->
<Reference Include="ReCall">
    <HintPath>..\ReCall\bin\$(Configuration)\ReCall.dll</HintPath>
    <Private>false</Private>
    <SpecificVersion>false</SpecificVersion>
</Reference>

<!-- 全新 checkout / 删过 bin 目录后 cold-start 兜底：
     仅在 ReCall.dll 真的不存在时一次性建一次（AutoCAD 必须未开），
     已存在则跳过、永不触发锁文件错误。 -->
<Target Name="EnsureReCallDllExists"
        BeforeTargets="ResolveAssemblyReferences"
        Condition="!Exists('..\ReCall\bin\$(Configuration)\ReCall.dll')">
    <Message Importance="high"
             Text="==&gt; [Refactored] 未发现 ..\ReCall\bin\$(Configuration)\ReCall.dll，先一次性构建 ReCall。" />
    <MSBuild Projects="..\ReCall\ReCall.csproj"
             Targets="Build"
             Properties="Configuration=$(Configuration);Platform=$(Platform)" />
</Target>
```

**为什么这个改动安全**

1. Refactored 对 ReCall 是**单向、纯类型可见性**依赖：用到的全部是 `CommandTable` / `CommandEntry` / `CommandListItem` / `CategoryGroup` / `RoadCommandShortAliases` 这些只读元数据类型。ReCall 反过来用反射访问 Refactored，**不构成循环依赖**。
2. `<Reference HintPath>` 只是给 csc 提供一个编译期类型来源，MSBuild 不再把 ReCall 当成"上游项目"去 Build → 不再触发 `CopyFilesToOutputDirectory` → 不再碰锁定的 dll。
3. 运行时类型由 AutoCAD NETLOAD 的那一份 ReCall.dll 提供（同一 AppDomain 内只一份），编译期与运行期类型身份天然对齐。
4. cold-start 兜底 Target 用 `Condition="!Exists(...)"` 严格守门——只在 dll 真的不存在时才触发一次性构建（此时 AutoCAD 不可能开着锁它），日常增量构建永远跳过。

**实测验证**（2026-04-21 本次会话）

| 场景 | 修复前 | 修复后 |
|---|---|---|
| AutoCAD 开着，dotnet build Refactored | MSB3027/MSB3021 错误，2 个 error，构建中断 | Exit 0，0 个 error，5.16s 完成，Refactored.dll 时间戳更新 |
| ReCall.dll 时间戳 | （未变化，因为 build 失败前就已被锁） | 仍是旧时间（AutoCAD 锁着的版本，**故意不动**） |

**反例（已撤销，绝不复活）**

```xml
<!-- 反例 1：保留 ProjectReference，妄图在 ReCall.csproj 用同名 Target 覆写跳过复制
     失败原因：SDK 末段重新导入会覆盖你的同名 Target，dotnet build 实测仍报 MSB3027 -->
<UsingTask TaskName="HyCADProbeFileLock" TaskFactory="RoslynCodeTaskFactory" .../>
<Target Name="CopyFilesToOutputDirectory" Condition="'$(_HyCADReCallDllLocked)' == 'true'">
    <!-- 永远不会执行——被 Microsoft.Common.CurrentVersion.targets 覆盖 -->
</Target>

<!-- 反例 2：把 ReCall 的 .cs 文件用 <Compile Include="..\ReCall\*.cs" Link="..."/>
     直接拉进 Refactored 项目编译
     失败原因：ReCall 类型会在 Refactored.dll 内重新定义一份，
     与 AutoCAD NETLOAD 那份 ReCall.dll 的同名类型构成 **类型身份割裂**
     → CommandFacade 反射调用时找不到正确的 CommandTable 实例 → 命令全部失效 -->
<Compile Include="..\ReCall\CommandTable.cs" Link="External\CommandTable.cs"/>

<!-- 反例 3：从 Solution Configuration Manager 把 ReCall 的 Debug.Build.0 取消勾选
     貌似能让 Build Solution 跳过 ReCall，但右键 Refactored→Build 时
     ProjectReference 仍会强制 Build 上游 ReCall（MSBuild 行为，与 .sln 配置无关）
     → 锁文件错误依旧 -->
```

**改 csproj 跨项目引用的强制清单**

1. ✅ Refactored 引用 ReCall 的方式：**仅** `<Reference HintPath>`，**禁止** `<ProjectReference>`。审 PR 时 `grep` 一下 `HyCADTool.Refactored.csproj`，出现 `Include="..\ReCall\ReCall.csproj"` 直接打回。
2. ✅ 任何新加入解决方案的项目，若想引用 ReCall 的类型 — 同样用 `<Reference HintPath="..\ReCall\bin\$(Configuration)\ReCall.dll">`，不开 ProjectReference。
3. ✅ 反向：ReCall **不**引用 Refactored（反射访问），保持单向依赖。任何 PR 想让 ReCall 加 `<Reference>` 或 `<ProjectReference>` 指向 Refactored — 直接打回（ReCall 是引导器，必须保持薄、稳定、无业务依赖）。
4. ✅ 删过 bin 目录或全新 checkout 后第一次 Build Refactored — 由 `EnsureReCallDllExists` Target 自动触发一次 ReCall 构建（前提：AutoCAD 没开），无需手工预热。
5. ❌ **绝不**尝试在 ReCall.csproj 里覆写 `CopyFilesToOutputDirectory`（被 SDK 末段覆盖，无效）。如果未来真的需要让 ReCall 自身在锁文件时跳过复制，改用 `Directory.Build.targets`（在 SDK targets 之后导入），或改用 `<Copy ContinueOnError="true">` 重定义私有 Target。

**新工作流变化（用户必须知道）**

| 改了什么 | 该怎么做 | AutoCAD 要不要关 |
|---|---|---|
| `HyCADTool.Refactored/**/*.cs` / XAML | VS 直接 Build → AutoCAD 输 `C2` 热重载 | **不用关** |
| `HyCAD.BlenderUI/**/*` | VS 直接 Build → `C2` | **不用关** |
| `commands.json` 配置 | VS 直接 Build → 下次命令调度自动重读（CommandTable mtime 失效缓存） | **不用关** |
| `ReCall/*.cs`（CommandTable / CommandFacade / Recall.cs） | **关闭 AutoCAD** → 解决方案资源管理器右键 ReCall → 生成 → 重启 AutoCAD | **必须关** |

**已修复文件**

- `HyCADTool.Refactored/HyCADTool.Refactored.csproj`（2026-04-21）：移除 `<ProjectReference Include="..\ReCall\ReCall.csproj">`，新增 `<Reference Include="ReCall"><HintPath>..\ReCall\bin\$(Configuration)\ReCall.dll</HintPath></Reference>` 与 `EnsureReCallDllExists` cold-start 兜底 Target。
- `ReCall/ReCall.csproj`（2026-04-21）：保持原状（曾尝试在此加 `RoslynCodeTaskFactory` + `CopyFilesToOutputDirectory` 覆写已被 SDK 末段覆盖证伪，本次会话已回滚）。

**同类宿主风险提示**

任何"长期持有 dll 的宿主进程"（Excel/Word VSTO / Revit / Office Add-in / VS Extension / IIS w3wp）+ 多项目解决方案，引导器/入口程序集都该用 `<Reference HintPath>` 而非 `<ProjectReference>`，避免业务项目重建时连带锁文件冲突。

---

### C2【新 2026-04-25】csproj `<PlatformTarget>x64</PlatformTarget>` 致 VS XAML 设计器（永远 32 位）全员 XDG-0001/XDG0023

**现象**

- 编译/运行/AutoCAD NETLOAD 全部完全正常，业务面板在 PaletteSet 里渲染零异常。
- 唯独 VS 错误列表里 `HyCADTool.Refactored` 项目下任意一个 panel xaml 都集中报：
  - `XDG0023` 或 `XDG0003`：「未能加载文件或程序集 HyCADTool.Refactored, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null 或它的某一个依赖项。系统找不到指定的文件。」（指向 xaml 里 `xmlns:vm` 引用的那一行）
  - `XDG-0001`：「查找资源字典 `pack://application:,,,/HyCAD.BlenderUI;component/Themes/Palettes/BlenderDark.xaml` 时出错」、`...BlenderTheme.xaml` 同样失败
  - `XDG0010`：BlenderButton / BlenderSplitter / BlenderTextBox / BlenderExpander / BlenderComboBox / BlenderCheckBox / BlenderButtonFlat / BlenderScrollViewer / EnumToIndexConverter 等**所有跨程序集资源**全员"未找到"
- 把同一 xaml 里 `pack://application:,,,/HyCADTool.Refactored;component/Presentation/Resources/RoadDesignerStyles.xaml`（指向**当前项目自己**的 pack URI）这一行**不报错**——这是关键不对称信号。
- 多个 panel xaml（`RoadAlignmentWorkbenchPanel.xaml` / `BaseReinPanel.xaml` / `PilePanel.xaml` 等）都同时报相同模式 → 是全局问题不是单文件问题。
- 用户感受："命令、面板、运行时全 OK，但设计器一直爆红，没法看预览，也没法在 XAML 里靠 IntelliSense 自动补全 `BlenderXxx` 资源。"

**触发条件**

1. `HyCADTool.Refactored.csproj` 与 `HyCAD.BlenderUI.csproj` 都显式声明 `<PlatformTarget>x64</PlatformTarget>`（项目祖辈版本里曾因配合 AutoCAD 64-bit 进程"显得严谨"加上去的）。
2. VS 2022 17.x 打开任一 panel `.xaml` 进入 XAML 设计器视图（拆分视图、设计视图、IntelliSense 弹出）。

**根因（双层叠加）**

第一层 — **dll 编译为 PE32+ x64-only**：`<PlatformTarget>x64</PlatformTarget>` 让 csc 在 PE 头里写 `0x20b` (PE32+) + machine `0x8664` (AMD64)，CLR header 标 ILONLY=1 但要求 64-bit。可用 PowerShell 校验：

```powershell
$bytes = [System.IO.File]::ReadAllBytes('HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll')
$peOff = [System.BitConverter]::ToInt32($bytes, 0x3C)
$magic = [System.BitConverter]::ToUInt16($bytes, $peOff + 0x18)
if ($magic -eq 0x20b) { 'PE32+ (64-bit only) — 32 位进程加载会 BadImageFormatException' }
elseif ($magic -eq 0x10b) { 'PE32 (AnyCPU/32-bit) — 32 位进程能加载' }
```

第二层 — **.NET Framework WPF 项目的 VS XAML 设计器宿主进程永远 32 位**：

- VS 2022 的 XAML 设计器宿主是独立进程：`XDesProc.exe` / `WpfSurface.exe` / `DesignToolsServer.exe`。
- "**Run the XAML Designer in a 64-bit process**" 这个开关**只对 .NET (Core) 5+ WPF 项目可见**。本仓库 `<TargetFramework>net48</TargetFramework>` → 选项页里**没有这一项**，且**永远没有**——这是 VS 的设计决定，不是 bug。
- 32 位宿主进程加载 PE32+ x64 dll 抛 `System.BadImageFormatException`，VS 错误列表把它包装成 `XDG0023` "未能加载文件或程序集... 系统找不到指定的文件"（误导性措辞，实质是位数错配）。
- 由此连锁：跨程序集 pack URI `pack://application:,,,/HyCAD.BlenderUI;component/...` 解析需要先 LoadFrom `HyCAD.BlenderUI.dll` → 同样 BadImageFormat → 资源字典加载失败 → `BlenderButton` 等键全员"未找到"。
- 同程序集 pack URI（`pack://application:,,,/HyCADTool.Refactored;component/...`）**不报错**是因为设计器对当前项目的 BAML 走 markup-compile 旁路，直接读 `obj\Debug` 下的 `g.resources`，根本不需要 LoadFrom 程序集 — 这就是上述"不对称信号"的原因。

**100% 验证方法**（避免猜测）

```powershell
# 用与 VS 设计器宿主同位数的 32-bit PowerShell 模拟它
& "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -Command {
    $bui = [System.Reflection.Assembly]::LoadFrom('HyCADTool.Refactored\bin\Debug\HyCAD.BlenderUI.dll')
    $bui.GetTypes().Length
    $ref = [System.Reflection.Assembly]::LoadFrom('HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll')
    $ref.GetTypes().Length
}
# 修复前：BlenderUI LoadFrom 抛 BadImageFormatException
# 修复后：BlenderUI 210 个类型、Refactored 1249 个类型，都能 LoadFrom + GetTypes
```

**真正修复**

```xml
<!-- HyCADTool.Refactored.csproj：旧 -->
<PlatformTarget>x64</PlatformTarget>

<!-- HyCADTool.Refactored.csproj：新 -->
<PlatformTarget>AnyCPU</PlatformTarget>
<Prefer32Bit>false</Prefer32Bit>

<!-- HyCAD.BlenderUI.csproj 同样改（这是 pack URI 所指向的程序集，必须能被 32 位设计器加载） -->
```

**为什么这个改动安全**

1. **运行时行为完全等同 x64**：AnyCPU + Prefer32Bit=false 在 64 位 AutoCAD 进程里 JIT 成 64 位，`IntPtr.Size==8`、native interop 走 64-bit 调用约定，与原 `PlatformTarget=x64` 字节级等价。AutoCAD .NET API 自带的 `AcMgd.dll` / `AcDbMgd.dll` 本身就是 AnyCPU（Autodesk 出厂如此），不存在"必须 x64"的硬约束。
2. **设计时 32 位能加载**：32 位 XDesProc / WpfSurface 现在能 LoadFrom 这两个 dll，跨程序集 pack URI 全部恢复，BlenderTheme/BlenderDark/BlenderButton 全部能解析。
3. **ReCall.csproj 故意保持 x64 不动**：设计器从不引用 ReCall（C1 的 `<Reference HintPath><Private>false</Private>` 让 ReCall.dll 既不在 Refactored 输出目录也不在设计器探测路径），所以 ReCall 维持 x64 对设计器零影响；运行时由 AutoCAD NETLOAD 加载，64 位 AutoCAD 进程加载 x64 ReCall.dll 完全正常。
4. **AcSeamless.dll 仍是 PE32+ x64 native 不影响**：它是 native 库，由 AcDbMgd 在 type init 阶段 P/Invoke 调用。设计器只对 Refactored.dll 做 LoadFrom + 反射 markup compile，**不**触发 AutoCAD type init 链路，所以 AcSeamless 的位数对设计器无关。

**修复完成后必须做的"硬重启"**（很关键，不做的话错误列表会残留）

VS 设计器宿主进程在 File→Exit 后会以 ServiceHub 的形式继续在后台跑一段时间复用，光重启 VS 主进程**不够**，必须把宿主进程一并杀掉、缓存目录一并删掉：

1. 关闭 VS。
2. Task Manager → Details，结束以下进程（如有）：`devenv.exe` / `XDesProc.exe` / `WpfSurface.exe` / `DesignToolsServer.exe` / `ServiceHub.Host.*.exe` / `MSBuild.exe`，等到一个都不剩。
3. 删除工作区下 `.vs\<解决方案名>` 整个文件夹（设计器本地缓存）。
4. 重新启动 VS，重开 XAML 设计器。

**反例（已撤销，绝不复活）**

```xml
<!-- 反例 1：保留 PlatformTarget=x64 + 期待 VS 出新版本支持 .NET Framework 64 位设计器
     真相：VS 2022 17.x 永远不会给 .NET Framework WPF 加 64 位设计器开关
          （已确认是 VS 设计决定，不是优先级问题），等不到 -->
<PlatformTarget>x64</PlatformTarget>

<!-- 反例 2：用 PostBuild Target 复制 ReCall.dll 进 Refactored\bin\Debug 期望解决 XDG0023
     真相：ReCall 是 x64 dll，复制进去也是 x64，设计器加载它仍然 BadImageFormat，
          且打破了 C1 "ReCall.dll 不出现在 Refactored\bin\Debug" 的设计承诺
          （会触发 AutoCAD 锁文件 + MSBuild 复制冲突）-->
<Target Name="CopyReCallDll" AfterTargets="Build">
    <Copy SourceFiles="..\ReCall\bin\$(Configuration)\ReCall.dll" DestinationFolder="bin\$(Configuration)" />
</Target>

<!-- 反例 3：Tools→Options→XAML 设计器→关闭 XAML 设计器
     真相：能消除错误，但同时失去整个 XAML 设计时支持（IntelliSense / 预览 / 资源补全），
          属于"砍头治痛"，绝不能这样做 -->
```

**改 csproj PlatformTarget 的强制清单**

1. ✅ `HyCADTool.Refactored.csproj` 与 `HyCAD.BlenderUI.csproj` 都用 `<PlatformTarget>AnyCPU</PlatformTarget>` + `<Prefer32Bit>false</Prefer32Bit>`。审 PR 时 `grep` 一下，出现 `<PlatformTarget>x64</PlatformTarget>` 直接打回。
2. ✅ 新加入解决方案的 WPF 类项目（任何会被 panel xaml 通过 `xmlns:` 或 pack URI 引用的项目）—— 必须 AnyCPU。
3. ✅ ReCall.csproj 保持 x64（设计器不会碰它，运行时由 AutoCAD 加载）。
4. ✅ 修改 PlatformTarget 后，必须按"硬重启"步骤清掉 VS 设计器宿主进程 + `.vs` 缓存，否则错误列表残留。
5. ❌ **绝不**用关闭 XAML 设计器、复制 dll 等"治标"手段——位数错配是结构性问题，必须从编译目标上修。

**已修复文件**

- `HyCADTool.Refactored/HyCADTool.Refactored.csproj`（2026-04-25）：`<PlatformTarget>x64</PlatformTarget>` → `<PlatformTarget>AnyCPU</PlatformTarget>` + 新增 `<Prefer32Bit>false</Prefer32Bit>`。
- `HyCAD.BlenderUI/HyCAD.BlenderUI.csproj`（2026-04-25）：同上修改。
- `ReCall/ReCall.csproj`：保持 x64 不动。

**同类宿主风险提示**

任何"宿主进程位数与目标程序集位数不匹配 + 反射加载"组合都有这个症状家族：VS XAML 设计器（永远 32 位 for .NET FX）、Office VSTO 加载 64-bit only 程序集、IIS 32-bit 应用池加载 64-bit 程序集。判定标准统一：把 dll 拖进 [CorFlags](https://learn.microsoft.com/en-us/dotnet/framework/tools/corflags-exe-corflags-conversion-tool) 看 `PE = PE32+` 与 `32BIT = 0` 组合即 64-bit only。

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

## § 验证状态与残留风险（2026-04-25）

以下为**对照仓库代码**的结论，用于回答「skill 里写的是否已落实」。

### 已在关键路径落实（实现可核对）

| 条目 | 核对要点 |
|------|----------|
| **A3** | `SettingsPanelViewModel.LoadSettings` 使用 `BuildStyleSignature()` 前后对比，**仅在不一致时** `_stylesDirty = true`（约 1190–1281 行），不是每次加载无脑置脏。 |
| **A5** | `SettlementTableService` / `DesignSpecService.TryCreateTable` / `EquipmentFoundationService` / `PileDrawingService` / `GroupCirclesByElevationCommand` 均含 `SetSize` 后 `GetMergeRange` + `UnmergeCells` 循环（以工程内 `grep UnmergeCells` 为准）。 |
| **A6** | `ReCall/Recall.cs` 存在 `PreloadCompanionAssemblies`，且在 `Assembly.Load(Refactored)` 前调用。 |
| **A2 与代码一致** | `DocumentActivated` → `EnsureCurrentDocumentResourcesInitialized`；`DocumentCreated` → 仅 `GetOrCreate` VM（因 Ensure 绑定 `MdiActiveDocument`，见上文 A2 正文）。 |
| **B10** | `BlenderThemeManager.PopulateAndRegister` 对 `SolidColorBrush` 使用 `OpacityProperty` 的 dummy `Binding`，防止 Seal（约 171–172 行）。 |
| **B1/B2** | `HyCAD.BlenderUI/Themes/Controls/ScrollBar.xaml` 为 **`x:Key="BlenderScrollBar"`** 命名 Style，非隐式无 Key。 |
| **D3 / D4** | `PluginInitializer.InstallWpfExceptionTraps`：`MdiActiveDocument` 空防御；`BindingErrorListener` 白名单 + `Debug.WriteLine`，避免命令行刷爆卡死。 |
| **Badge 真修复（B11）** | `ReCall/Recall.cs::ResolveAssembly` 增加 `AutoCadHostAssemblyNames` 黑名单，宿主程序集（`AdWindows` / `AcMr` / ...）一律走 `AppDomain` 已加载查表，绝不从 deps 目录 byte[] 加载，杜绝 5.0.1.2 与 5.1.1.1 双载入；见 **B11** 全文。 |
| **C1** | `HyCADTool.Refactored.csproj` 已移除 `<ProjectReference Include="..\ReCall\ReCall.csproj">`，改用 `<Reference Include="ReCall"><HintPath>..\ReCall\bin\$(Configuration)\ReCall.dll</HintPath></Reference>` + `EnsureReCallDllExists` cold-start Target（约 222–252 行）。`grep -n 'ReCall.csproj' HyCADTool.Refactored.csproj` 应 0 命中。AutoCAD 开着 dotnet build Refactored 实测 0 error。 |
| **C2** | `HyCADTool.Refactored.csproj` 与 `HyCAD.BlenderUI.csproj` 都已 `<PlatformTarget>AnyCPU</PlatformTarget>` + `<Prefer32Bit>false</Prefer32Bit>`。校验：用 32-bit PowerShell 跑 `[System.Reflection.Assembly]::LoadFrom('HyCADTool.Refactored\bin\Debug\HyCAD.BlenderUI.dll').GetTypes().Length` 应返回 210；同样测 Refactored.dll 应返回 1249。VS 重启后错误列表 0 个 XDG-0001/XDG0023/XDG0010。`ReCall.csproj` 故意保持 x64 不变（设计器不引用它）。 |

### 仍为环境型 / 缓解型风险（不是单靠改一行就能封死）

| 风险 | 说明 |
|------|------|
| **`/AdWindows;component/themes/badge.xaml`** | 真根因为 ReCall AssemblyResolve 双载入旧版 AdWindows 5.0.1.2，与 AutoCAD 进程 5.1.1.1 形成类型身份割裂。复现时先查：**ReCall 宿主程序集黑名单是否完整**、bin/Debug 里是否多出本不该有的旧版 AutoCAD 宿主 dll（见 **B11**、`.cursor/rules/05-AdWindows-WPF-PaletteSet宿主.mdc`）。 |
| **B10 同类** | 未来若有新的 `Freezable` 写入 Theme 字典且未做 freeze 阻断，仍可能再引入异常链。 |
| **A1** | 依赖持续 Code Review：`SingleInstance` 服务不得长期缓存 `Database`。 |
| **C1 同类** | 未来若新建解决方案项目（如 `HyCADTool.Plugins.X`）想引用 ReCall，必须用 `<Reference HintPath>` 而非 `<ProjectReference>`；同样必须保持 ReCall 不反向引用任何业务项目。审 PR 时 `grep` 一下 `ReCall\.csproj"` 看是否被任何 csproj 用 ProjectReference。 |
| **C2 同类** | 任何新加入解决方案的"会被 panel xaml 通过 `xmlns:` 或 pack URI 引用"的 WPF 类项目 — 必须 AnyCPU + Prefer32Bit=false，绝不能 `<PlatformTarget>x64</PlatformTarget>`。审 PR 时 `grep -n '<PlatformTarget>x64' *.csproj` 应只命中 `ReCall/ReCall.csproj` 一行。改完 PlatformTarget 后必须按 C2 "硬重启"步骤清掉 VS 设计器宿主进程 + `.vs` 缓存，否则错误列表残留。 |

### 与「Verification」自检表的关系

下文 **Verification** 是**手工回归清单**；本节是**静态代码与架构级**核对。二者互补：Verification 失败时回到对应字母条目 + 本节残留风险排查。

---

## Verification

**A 域验证**

- 切换 DWG 后执行命令不再报 `eNotFromThisDocument`（A1）
- 新 DWG 首次执行 `gj` / `gb` / `gb1` 不缺样式或图层（A2）
- 重复执行命令不再每次卡顿（A3）
- 样式/Scale/钢筋参数只从 `SettingsPanelViewModel.Current` 读（A4）
- 新 `Table` 表头各列文字都可见，不会被 Title 行合并吞（A5）

**B 域验证**

- `C2 → C1 → HyB`（或 `Hy`）打开统一面板，AutoCAD 不闪退（B1/B2）；命令行无 `Badge` / `badge.xaml` 的 `XamlParseException`（B11）
- 所有 UserControl 打开不抛 `StaticResourceExtension` 异常（B3）
- XAML 设计器打开 `Samples/DemoWindow.xaml` 无 `XDG0066`（B5/B6）
- 编译无 `MC3015` / `MC4111`（B7/B8）
- `ColumnDefinition.Width` / `Margin` 类属性加载不报异常（B4）
- 面板 Dock 到 AutoCAD 侧边不消失（B9）

**C 域验证**

- AutoCAD 开着的情况下 `dotnet build HyCADTool.Refactored/HyCADTool.Refactored.csproj -c Debug` 退出码 0，无 `MSB3027` / `MSB3021` / "文件被 AutoCAD Application 锁定" 错误（C1）
- 构建后 `HyCADTool.Refactored.dll` 时间戳更新，`ReCall.dll` 时间戳保持不变（C1）
- `grep -n 'ReCall\.csproj' HyCADTool.Refactored.csproj` 0 命中，`grep -n '<Reference Include="ReCall"' HyCADTool.Refactored.csproj` 命中 1 次（C1）
- 全新 checkout（删 `ReCall/bin/Debug` 后）首次 Build Refactored，`EnsureReCallDllExists` Target 触发一次 ReCall 自动构建（C1 cold-start）
- VS 重启后打开 `RoadAlignmentWorkbenchPanel.xaml` / `BaseReinPanel.xaml` 设计器，错误列表里 0 个 `XDG-0001` / `XDG0023` / `XDG0003` / `XDG0010`（C2）
- `grep -n '<PlatformTarget>x64' HyCADTool.Refactored.csproj HyCAD.BlenderUI.csproj` 0 命中（应都是 AnyCPU；C2）
- 在 32-bit PowerShell `& "$env:WINDIR\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"` 里 `LoadFrom HyCAD.BlenderUI.dll` + `GetTypes().Length` 返回 210；`LoadFrom HyCADTool.Refactored.dll` + `GetTypes().Length` 返回 1249（C2）

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
