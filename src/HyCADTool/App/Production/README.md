# `Production/` — 生产构建专用入口（L3）

本目录**仅在** MSBuild 定义常量 **`HYCAD_PRODUCTION`** 时参与编译。用途：把 `HyCADTool.dll` 变成可 **直接 NETLOAD** 的成品插件，**不依赖** `ReCall.dll`；业务初始化仍走 **`PluginInitializer`**，与开发模式一致。

**深度**：L3（文件级：职责、关键成员、调用链、与 ReCall 的差异）。

---

## 参与编译的文件

| 文件 | 预处理器 | 说明 |
|------|----------|------|
| `ProductionEntry.cs` | `#if HYCAD_PRODUCTION` | 程序集级 `[ExtensionApplication]` + `[CommandClass]`；`ProductionExtension` 实现 `IExtensionApplication`。 |
| `ProductionCommandFacade.cs` | `#if HYCAD_PRODUCTION` | 与 `ReCall/CommandFacade.cs` 对称的 80+ 条 `[CommandMethod]`，经 `LicenseGate` 后调 `ProductionDispatcher.Invoke`。 |
| `ProductionDispatcher.cs` | `#if HYCAD_PRODUCTION` | 不引用 ReCall；用 `CommandCatalog` + 反射在**当前** `HyCADTool.dll` 内执行 `commands.json` 映射。 |

`Debug`/`Release` 下本目录**无** IL 输出（整文件被 `#if` 裁掉），避免 Refactored 被 `Assembly.Load(byte[])` 时 AutoCAD 再扫到第二套 `ExtensionApplication` / `CommandClass`。

---

## `ProductionEntry.cs` — `ProductionExtension`

### 程序集级 Attribute（本文件内）

- `[assembly: ExtensionApplication(typeof(ProductionExtension))]`
- `[assembly: CommandClass(typeof(ProductionCommandFacade))]`

### `ProductionExtension` 成员

| 成员 | 行为 |
|------|------|
| `void Initialize()` | `new PluginInitializer()` → `Initialize()`；异常尝试写命令行。 |
| `void Terminate()` | `_initializer?.Terminate()`，finally 置 `null`；不在此处抛。 |

**调用链**：用户 `NETLOAD HyCADTool.dll` → AutoCAD 调 `IExtensionApplication.Initialize` → 与 C2 反射路径共享同一 `PluginInitializer.Initialize` 子阶段（见 `Bootstrap/README.md`）。

---

## `ProductionCommandFacade.cs`

### 设计

- 与 `ReCall/CommandFacade.cs` **1:1 镜像**（命名空间/类名/调用目标三处替换），见文件头**维护约定**。
- 每条命令形如：`[CommandMethod("gj")] public void Cmd_gj() => LicenseGate.RunGated("gj", () => ProductionDispatcher.Invoke("gj"));`
- **许可**：`LicenseGate.RunGated` 包裹真正调度（与开发路径可能不同，以源码为准）。

### 维护约定

在 `ReCall/CommandFacade.cs` **增删** `[CommandMethod]` 后，**必须**同步本文件，否则生产包命令表与开发不一致。

---

## `ProductionDispatcher.cs` — 静态 `Invoke(string key)`

### 与 `ReCallClass.Invoke` 的等价点

- 查 **`CommandCatalog`**（与 ReCall 共用同一份 `commands.json` 解析与 mtime 缓存），**不**用 ReCall 的 `CommandTable` 类名（实现均指向命令目录数据）。
- `_HyExec`：面板待执行 → `SettingsPanelViewModel.ConsumePendingCommand` 同逻辑分支。
- **前置钩子**：`InvokePreHooks` — `CommitFocusedTextBoxValue`、`Current.LoadSettings`、`EnsureStylesApplied`（与 `Recall.cs` 内反射等价）。
- **反射目标**：`Assembly.GetExecutingAssembly()`，即本进程已加载的 `HyCADTool.dll`（**非** byte[] 影子程序集）。

### 性能与缓存

- `ConcurrentDictionary` 缓存 `Type` 与 `MethodInfo`（key = 类型全名 或 `Type.Method` 组合），避免每次命令全反射查找。
- 单次成功路径约：**EnsureLoaded** + 字典查表 + `Activator.CreateInstance` + `MethodInfo.Invoke`（与文件头描述一致）。

### 公开成员

| 成员 | 说明 |
|------|------|
| `void Invoke(string key)` | 主入口；`ed == null` 直接 return。 |
| `string GetCommandsJsonPath()` | 转调 `CommandCatalog.GetFilePath()`。 |

### 状态

- 静态 `_typeCache`、`_methodCache` 在 AppDomain 内长期存活；C2 不存在于生产路径，**关 CAD** 重 NETLOAD 才会清进程。

---

## 相关文档

- 总览与双路径：`../../../../doc/00-新的开始-2026-04-26-175400.md`（节「路径 B：生产模式」）  
- 父级说明：`../README.md`  
- 开发侧命令表：`src/ReCall/commands.json` + `hycad-new-command-registration` skill
