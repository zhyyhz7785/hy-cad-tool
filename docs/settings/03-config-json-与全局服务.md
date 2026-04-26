# 03 — `config.json` 与全局配置服务

## 1. 文件角色

`config.json` 面向 **部署目录旁路**：与 **程序集所在目录**、**当前工作目录**、**AppDomain BaseDirectory** 等位置联动查找（见下文）。典型用途：

- **桩基模块默认**（`PileConfigData` 节点）  
- **旧版基础样式**（`BaseConfigData` 下的 `TextStyle` / `DimStyle` / `MLeaderStyle`）  
- **新结构全局配置**（`GlobalConfiguration` 节点，若存在则由 `JsonConfigurationLoader.LoadBaseConfiguration` 解析）

仓库内现成示例：`HyCADTool.Refactored/config.json`（当前主要为 `PileConfigData` + `BaseConfigData`）。

---

## 2. 查找顺序（与 DLL 位置关系）

`GlobalConfigurationService` 与 `ModuleConfigurationService` 的 `FindConfigFile()` 逻辑一致，按顺序尝试：

1. `Path.GetDirectoryName(当前程序集.Assembly.Location)`  
2. `Environment.CurrentDirectory`  
3. `AppDomain.CurrentDomain.BaseDirectory`  
4. 开发热重载友好路径：`BaseDirectory\..\..\..\HyCADTool.Refactored\bin\Debug`（存在则选用）

若均不存在文件，则返回 `Environment.CurrentDirectory\config.json` 作为**占位路径**，后续加载失败则回退默认对象。

---

## 3. `JsonConfigurationLoader` 解析入口

**类**：`HyCADTool.Refactored/Infrastructure/Configuration/JsonConfigurationLoader.cs`

| 方法 | 作用 |
|------|------|
| `LoadBaseConfiguration()` | 优先读根节点 `GlobalConfiguration`；若无则走 `LoadLegacyBaseConfiguration()`（从 `BaseConfigData` 拼出 `GlobalConfiguration`）。 |
| `LoadPileConfiguration()` | 读 `PileConfigData`，字段包括 `Section`、`DiameterOrEdge`、`ArrangementType`、`PileArrangeRate`、`Margin`、最小桩距、位移率、距轮廓距离等。 |
| `LoadTextStyleConfiguration()` 等 | 从 `BaseConfigData` 读旧式嵌套（供兼容）。 |

### 3.1 `GlobalConfiguration` 新格式（摘要）

当 JSON 存在 `GlobalConfiguration` 时，包含：

- `Scale`：默认比例与范围  
- `Tolerance`：双精度/向量/点容差  
- `Paths`：导入导出、临时目录、配置目录  
- `Styles`：`TextStyle` / `DimensionStyle` / `MLeaderStyle`  
- `ElevationLength`：数值（默认 2.0）

具体子字段见 `LoadBaseConfiguration` 内 `LoadScaleConfig`、`LoadToleranceConfig` 等私有方法。

### 3.2 仓库示例 `config.json` 结构（节选）

当前仓库文件**未包含** `GlobalConfiguration` 节点，而是：

- `PileConfigData`：桩型、直径、布置方式、边距、最小桩距等，带 `default` 与 `Range` / `Options`。  
- `BaseConfigData`：`TextStyle`、`DimStyle`、`MLeaderStyle` 旧式命名（小写 `dimtxt` 等）。

---

## 4. 服务类职责

| 类 | 职责 |
|----|------|
| `GlobalConfigurationService` | 持有 `GlobalConfiguration` 单例，支持 `LoadConfiguration` / `SaveConfiguration` / `ResetToDefault`（以源码为准）。 |
| `ModuleConfigurationService` | 按模块名缓存配置（如 `"Pile"`），`LoadAllConfigurations` 时从 JSON 填充。 |
| `ConfigurationService` | 对外统一入口：`Global`、`GetModuleConfig<T>`、`LoadAll`/`SaveAll`/`ResetAll`，以及 **Scale 优先读 `SettingsPanelViewModel`**（见 01）。 |

---

## 5. 与 `hy-settings.json` 的关系

| 维度 | `hy-settings.json` | `config.json` |
|------|-------------------|----------------|
| 位置 | 固定 `%APPDATA%\HyCADTool\` | DLL/工作目录旁 |
| 主要维护者 | `SettingsPanelViewModel`（用户面板） | 安装包/开发者 |
| 样式主路径 | **当前**以 VM 为主，驱动 `EnsureStylesApplied` | 旧/全局 loader 兼容 |
| 桩基 | 面板另有 `hy-pile-settings.json` | `PileConfigData` 提供模块默认 |

**结论**：日常调参以 **`hy-settings.json`** 与 **面板专用 JSON** 为主；`config.json` 更适合部署默认值与尚未迁移到 VM 的模块段。

---

## 6. 相关源码

- `HyCADTool.Refactored/Infrastructure/Configuration/GlobalConfigurationService.cs`  
- `HyCADTool.Refactored/Infrastructure/Configuration/ModuleConfigurationService.cs`  
- `HyCADTool.Refactored/Infrastructure/Configuration/JsonConfigurationLoader.cs`  
- `HyCADTool.Refactored/config.json`  

下一篇：[04-图层与样式初始化](./04-图层与样式初始化.md)。
