# `App` — 插件启动、配置与交付

本目录是 **HyCADTool.Refactored** 的**应用层根**：负责在 AutoCAD 进程内完成 DI 容器构建、WPF 宿主桩、全局/模块配置加载、样式与图层初始化、文档事件订阅，以及 **Production** 与 **ReCall 开发** 两条启动路径的衔接。

**边界**：

- **不放**业务命令实现（见 `Features/`）。
- **不放**统一面板壳与 Ribbon 编排主体（见 `Shell/`，但 `Bootstrap` 会引用 Shell 的配置与契约并完成注册）。

---

## 目录结构

| 路径 | 说明 |
|------|------|
| `Bootstrap/` | 核心启动：`PluginInitializer`、`AutofacModule`、配置加载（JSON/CSV）、`SimpleLogger`、`ServiceLocator` 等。 |
| `Production/` | **仅**在定义 `HYCAD_PRODUCTION` 时编译：`ProductionEntry`（`IExtensionApplication` + `[CommandClass]`）、`ProductionCommandFacade`、`ProductionDispatcher`。用于 NETLOAD 成品 DLL，与 ReCall 热重载路径共用同一套 `PluginInitializer`。 |
| `Test/` | 开发联调：`TestCommand`（C1 一键入口）、`TestRunner`、`DesignSpecLayoutTestData` 等。 |
| `Resources/` | 随插件部署的资源（如 `HyCAD-Linetypes.lin`）。 |
| `_libraries/` | 道路等领域 JSON 字典（`feature-catalog.json`、`blender-material-mapping.json`）；细节见同目录 [`_libraries/README.md`](_libraries/README.md)。 |
| `config.json` | 部分功能模块的默认/范围配置（如桩基面板），由配置服务加载。 |
| `Properties/AssemblyInfo.cs` | 程序集信息。 |

---

## 启动与生命周期（v2）

1. **开发模式（默认 Debug/Release）**  
   - Refactored 主程序集**不**实现 `IExtensionApplication`，避免 AutoCAD 对 `Assembly.Load(byte[])` 加载的程序集**重复**触发初始化。  
   - `ReCall` 的 C2 重载后通过反射调用 `PluginInitializer.Initialize()`；卸载前对**旧实例**调用 `Terminate()`，保证文档事件等幂等解绑。  
   - 详细设计见 `PluginInitializer.cs` 文件头注释。

2. **生产模式（`HYCAD_PRODUCTION`）**  
   - `ProductionEntry.cs` 注册 `[ExtensionApplication]` 与 `[CommandClass]`，NETLOAD 后 AutoCAD 直接走 `ProductionExtension.Initialize()` → 同样 `new PluginInitializer().Initialize()`。  
   - 命令由 `ProductionCommandFacade` 的 `[CommandMethod]` 暴露，内部经 `ProductionDispatcher` 分发；**与 `ReCall/CommandFacade.cs` 需保持同步**（见 `ProductionCommandFacade.cs` 顶部维护约定）。

---

## 关键类型

- **`PluginInitializer`**：构建 Autofac 容器、WPF `Application` 桩（`ShutdownMode.OnExplicitShutdown`）、主题预热、配置与图层/样式、道路子系统初始化、文档事件。  
- **`AutofacModule`**：注册 `Shared.*`、`Shell` 契约、`Features` 下各服务与 Presentation 等；新增可注入服务时主要改此处。  
- **`ConfigurationService` / `GlobalConfigurationService` / `ModuleConfigurationService`**：协调全局与模块配置；底层可经 `JsonConfigurationLoader`、`CsvConfigurationLoader` 等加载。  
- **`TestCommand.Run()`**：C1 临时测试入口；日常只改其内部一行调用目标，配合 C2 热重载（项目约定见 `.cursor/rules/01-AI热启动模式.mdc`）。

---

## 修改本目录时的注意点

- **WPF / PaletteSet**：初始化逻辑以 `PluginInitializer` 为准；不要随意添加 `Application.ResourceAssembly`、`Assembly.Load("AdWindows")` 或 AdWindows/Badge 预热（见 `.cursor/rules/05-AdWindows-WPF-PaletteSet宿主.mdc` 与 `ReCall/Recall.cs` 宿主程序集解析）。  
- **Production 命令表**：在 `ReCall/CommandFacade.cs` 增删命令后，按 `ProductionCommandFacade.cs` 内说明同步生产门面。  
- **图层清单**：若初始化里注册道路等图层，需与全局图层常量（如 `HyRoadLayers`）及 `GetRequiredLayers()` 类清单保持一致，避免实体静默落到 0 层（见 `.cursor/skills/hycad-project-pitfalls/SKILL.md`）。

---

## 相关文档

- 道路宿主与 Badge 根因：`doc/RoadDesign/00.md`  
- 新命令注册（N1~N50、`commands.json`）：`.cursor/skills/hycad-new-command-registration/SKILL.md`  
- `_libraries` 字典用途：[`_libraries/README.md`](_libraries/README.md)
