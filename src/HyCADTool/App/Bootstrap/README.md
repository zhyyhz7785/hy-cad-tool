# `Bootstrap/` — 启动编排、DI、配置（L3）

本目录负责 **HyCADTool 在 AutoCAD 内的第一次“能跑”**：WPF 宿主桩、Autofac 容器、`ServiceLocator`、全局/模块配置加载，以及 **`PluginInitializer` 总入口**（由 ReCall C2 反射调用，或 Production 直接 `new` 后调用）。

**深度**：L3（文件级：职责、关键成员、调用链、状态）。

---

## 文件清单与职责

| 文件 | 职责（一句话） |
|------|----------------|
| `PluginInitializer.cs` | 插件初始化/终止总编排：WPF、`WarmupBlenderTheme`、Autofac、`LoadConfigurations`、线型/图层/样式、文档事件、道路子系统、Ribbon+CUIX、Operator 注册。 |
| `AutofacModule.cs` | `Autofac.Module`：注册 Shared/Shell/Features 下全部可注入服务（单例为主）。 |
| `ServiceLocator.cs` | 静态持有 `IContainer`；`Initialize` / `Reset` / `Resolve<T>` / `TryResolve<T>`。 |
| `ConfigurationService.cs` | 实现 `IConfigurationService`：`LoadAll`/`SaveAll`/`ResetAll`；`GetModuleConfig<T>`；`GetScale`/`SetScale` 与 `SettingsPanelViewModel` 协同。 |
| `GlobalConfigurationService.cs` | 全局配置：比例、容差、`config.json` 路径解析与 `JsonConfigurationLoader` 读写。 |
| `ModuleConfigurationService.cs` | 模块配置：Pile/Foundation/Reinforcement/Elevation 等块，同读 `config.json`。 |
| `JsonConfigurationLoader.cs` | JSON 文件读写封装（被上述两服务使用）。 |
| `CsvConfigurationLoader.cs` | CSV 配置加载（若被其它服务引用）。 |

---

## `PluginInitializer`

### 公开 API

| 成员 | 说明 |
|------|------|
| `void Initialize()` | 入口。成功后在命令行打印分阶段耗时与“入口指引”。失败则 `WriteMessage` 异常与堆栈。 |
| `void Terminate()` | 保存 `SettingsPanelViewModel`、卸载 Ribbon/CUIX（见环境变量分支）、`UnregisterDocumentEvents`、`ServiceLocator.Reset()`。 |

### 内部阶段顺序（与源码 `RunTimed` 顺序一致）

1. **WPF stub**：`Application.Current == null` 时 `new Application()`；`ShutdownMode = OnExplicitShutdown`（避免最后一个子窗体关闭拖死 WPF）。
2. **`InstallWpfExceptionTraps`**：`Dispatcher.UnhandledException` + `PresentationTraceSources.DataBindingSource`（项目相关错误进 `Debug`，不全量刷命令行）。
3. **`WarmupBlenderTheme`**：通过 `ResourceDictionary { Source = pack://...BlenderTheme.xaml }` 同步加载主题与 `BlenderWindow.xaml`；**不** merge 到 `Application.Current.Resources`。
4. **Autofac**：`new ContainerBuilder()` → `RegisterModule<AutofacModule>()` → `Build()` → `ServiceLocator.Initialize(container)`。
5. **`LoadConfigurations`**：`IConfigurationService.LoadAll()`。
6. **`TryEnsureHyCadStandardLinetypesLoaded`**：若当前图缺线型，从程序集旁 `Resources/HyCAD-Linetypes.lin` 加载（见 `Resources/README.md`）。
7. **`InitializeStylesAndLayers`**：`EnsureCurrentDocumentResourcesInitialized(force: true)`、道路原线层锁定、`BlenderThemeManager.Apply/Refresh`、`BlenderMetricsScaleManager`。
8. **`RegisterDocumentEvents`**：`DocumentActivated` / `DocumentCreated`。
9. **`InitializeRoadSubsystem`**：预解析 `RoadDesignRegistry`、`RoadJsonExportService`。
10. **`InitializeRibbonAndMenus`**：`HyCadRibbonBuilder.Build`、`CuiMenuBuilder.EnsureLoaded`；`HYCAD_RIBBON_DISABLED=1` 时跳过 Ribbon。
11. **`OperatorBootstrapper.RegisterAllCommands`**：将命令表注册为 BlenderUI Operator（失败仅警告）。

### 调用链（谁调用本类）

- **开发**：`ReCall.ReCallClass.InvokePluginInitialize` → 反射 `new PluginInitializer()` → `Initialize()`。
- **生产**：`ProductionExtension.Initialize` → `new PluginInitializer()` → `Initialize()`（`#if HYCAD_PRODUCTION`）。

### 状态

- 实例由 ReCall 保存在 `_lastPluginInitInstance`；每次 C2 前对旧实例调 `Terminate()`。
- `ServiceLocator` 全进程单例容器；`Reset()` 在 `Terminate` 中调用。

---

## `AutofacModule`

- **入口**：`protected override void Load(ContainerBuilder builder)`。
- **改什么**：新增可注入服务（接口 → 实现、生命周期）时**主要改此文件**；与 `Shell/Contracts`、各 `Features/*/Services` 对齐。
- **注意**：改注册规则后通常需**关 CAD 重 NETLOAD**（或至少让容器在预期时机重建）；见项目规则“AutofacModule 变更”说明。

---

## `ServiceLocator`

| 成员 | 行为 |
|------|------|
| `Initialize(IContainer)` | 替换内部 `_container`（不自动 Dispose 旧容器，由 `Reset` 处理）。 |
| `Reset()` | `Dispose` 旧容器并置 `null`；C2 前 `Terminate` 会调。 |
| `Container` | 未初始化则抛 `InvalidOperationException`。 |
| `Resolve<T>()` / `TryResolve<T>(out)` / `TryResolve<T>()` | 标准解析；`TryResolve` 失败返回 `default` 或 `false`。 |

**调用方**：命令类、服务、面板 VM 等凡无法构造注入处，通过 `ServiceLocator.Resolve<...>()` 取服务（项目承认的 Service Locator 妥协）。

---

## `ConfigurationService` / 全局与模块配置

- **`ConfigurationService`**：组合 `IGlobalConfigService` + `IModuleConfigService`；`LoadAll` 同时加载；`ResetAll` 显式重置若干模块键名（Pile/Foundation/Reinforcement/Elevation）。
- **`GetScale` / `SetScale`**：优先 `SettingsPanelViewModel.Current`，否则回落全局配置。

**数据流**：`PluginInitializer.LoadConfigurations` → `configService.LoadAll()` → 全局 + 模块 JSON 入内存模型；路径由 `GlobalConfigurationService`/`ModuleConfigurationService` 的 `FindConfigFile()` 解析（程序集目录、当前目录等）。

---

## `SimpleLogger`

| 成员 | 参数/行为 |
|------|-----------|
| `LogElapsedTime(string operation, Action action)` | `Stopwatch` 包一层，向 `MdiActiveDocument.Editor` 写 `INFO: {operation} 耗时 {ms} 毫秒`。 |
| `LogElapsedTime<T>(string operation, Func<T> func)` | 同上并返回 `func` 结果。 |
| `LogInfo(string message)` | 单行 INFO。 |

**典型调用**：`TestCommand.Run` 内包一层被测命令耗时（与 `TestRunner.LogElapsedTime` 风格类似，目标都是命令行）。

---

## 相关文档

- 启动总线：`../../../../doc/01-启动流程与调用链-2026-04-26-185800.md`
- WPF 宿主与 Badge：`.cursor/rules/05-AdWindows-WPF-PaletteSet宿主.mdc`
- 父级说明：`../README.md`
