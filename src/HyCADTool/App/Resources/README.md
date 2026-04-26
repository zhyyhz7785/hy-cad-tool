# `Resources/` — 随程序集部署的静态资源（L3)

本目录存放 **编译输出随 `HyCADTool.dll` 同目录发布** 的非代码资源。当前以 **线型库** 为主，供 `PluginInitializer` 在**有活动文档**时按需加载到**当前图**的线型表。

**深度**：L3（文件说明、谁读、失败策略）。

---

## 文件

| 文件 | 内容类型 | 说明 |
|------|----------|------|
| `HyCAD-Linetypes.lin` | AutoCAD 线型定义（`.lin` 文本） | 包含 HyCAD 标准线型名（如点划线/虚线，具体名称以 `HyLinetypeNames` 常量为准）。 |

---

## 谁读取、怎么读

| 阶段 | 代码位置 | 行为 |
|------|----------|------|
| 插件初始化 | `PluginInitializer.TryEnsureHyCadStandardLinetypesLoaded()` | 解析 `typeof(PluginInitializer).Assembly.Location` 同目录下 `Resources/HyCAD-Linetypes.lin`；若**当前图**线型表尚不存在目标线型名，则经 `IStyleService.LoadLinetype` 加载。 |
| 文档/切换 | `PluginInitializer.EnsureCurrentDocumentResourcesInitialized` 内会再次调用同一线型逻辑（与活动文档相关）。 |

**路径规则**：`Path.Combine(assemblyDir, "Resources", "HyCAD-Linetypes.lin")`；若 `Assembly.Location` 为空（极端场景），会跳过并写**警告**到命令行。

---

## 失败与降级

- 文件不存在、无活动文档、StyleService 不可用、事务内读表失败等：**不阻断**整次 `Initialize`；只 `WriteMessage` 警告。  
- 业务命令若依赖某线型，应在命令内再保证或提示用户。

---

## 与 `App/_libraries` 的区别

| | `App/Resources/` | `App/_libraries/` |
|---|------------------|-------------------|
| 内容 | 线型等 **CAD/部署** 资源 | 道路等领域 **JSON 字典**（feature-catalog 等） |
| 说明文档 | 本文件 | [`_libraries/README.md`](../_libraries/README.md) |

---

## 相关代码引用

- `HyCADTool.App.Bootstrap.PluginInitializer` — `TryEnsureHyCadStandardLinetypesLoaded`、`EnsureCurrentDocumentResourcesInitialized`  
- 图层/线型名常量：建议与 `GetRequiredLayers`、道路图层规范同步（见 `hycad-project-pitfalls` 中“图层清单一致”条）。
