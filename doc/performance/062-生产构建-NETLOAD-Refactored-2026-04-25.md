# 062 — 生产构建：NETLOAD HyCADTool.Refactored.dll（无 ReCall 壳）

> **日期**：2026-04-25
> **目的**：发出去的客户安装包**只**包含 `HyCADTool.Refactored.dll` + 它的依赖，**不**带 `ReCall.dll`。
> AutoCAD 直接 NETLOAD `HyCADTool.Refactored.dll` 即可工作 → 用于性能基线对比 + 最终发布。

---

## 1. 三种加载路径对比

| 模式 | 入口 | DLL 来源 | CLR 加载方式 | 命令注册 | 典型用途 |
|---|---|---|---|---|---|
| **Dev (ReCall + C2)** | NETLOAD `ReCall.dll` → `C2` | `bin\Debug\HyCADTool.Refactored.dll` 复制到 `%TEMP%` | `Assembly.Load(byte[])` | ReCall.CommandFacade（ReCall.dll 内） | 开发热重载 |
| **Dev 自动加载** | NETLOAD `ReCall.dll`（idle 后自动 C2） | 同上 | 同上 | 同上 | 自动热加载 |
| **Production** | NETLOAD `HyCADTool.Refactored.dll` | `bin\Production\HyCADTool.Refactored.dll` | `Assembly.LoadFrom`（AutoCAD 自身） | Refactored.Production.ProductionCommandFacade（Refactored.dll 内） | **客户安装包 + 性能基线** |

**关键差异**：
- Production 模式下 ReCall.dll **完全不存在**，零热重载壳层
- 命令注册在 `Refactored.dll` 内部的 `ProductionCommandFacade`（144 条 `[CommandMethod]`）
- 启动入口是 `ProductionExtension : IExtensionApplication`，AutoCAD NETLOAD 时自动调 `Initialize`
- 调度器 `ProductionDispatcher` 自己读取同目录 `commands.json`，反射到目标 Type.Method

---

## 2. 出包步骤

### 2.1 编译 Production 配置

```powershell
cd e:\BaiduSyncdisk\Code\CSharp\CursorProjects\hy-cad-tool
dotnet build HyCADTool.Refactored\HyCADTool.Refactored.csproj -c Production
```

输出：`HyCADTool.Refactored\bin\Production\` 下生成：
- `HyCADTool.Refactored.dll` — 含 `[ExtensionApplication]` + 144 条 `[CommandMethod]`
- `HyCADTool.Refactored.pdb`
- `commands.json` — 自动从 `..\ReCall\commands.json` 复制
- `HyCAD.BlenderUI.dll` / `Newtonsoft.Json.dll` / `Autofac.dll` / `NetTopologySuite.dll` / `Clipper2.dll` / `Markdig.dll` / `DocumentFormat.OpenXml.dll` / `HyCADTool.TextLayout.dll` / `Resources\` / `_libraries\` …
- **没有** `ReCall.dll`

### 2.2 用户安装包内容

把 `bin\Production\` 整个目录打包给用户，目录树：

```
HyCADTool/
├─ HyCADTool.Refactored.dll      ← NETLOAD 这个
├─ HyCADTool.Refactored.pdb
├─ commands.json                 ← 必须与 dll 同目录
├─ HyCAD.BlenderUI.dll
├─ Newtonsoft.Json.dll
├─ Autofac.dll
├─ ... (其他 NuGet)
├─ Resources\HyCAD-Linetypes.lin
└─ _libraries\
```

### 2.3 客户使用

```text
AutoCAD 命令行:
NETLOAD
→ 选择 HyCADTool.Refactored.dll
→ [HyCAD] 初始化完成（来自 PluginInitializer.Initialize）
→ 输入 hy / gj / hyRoad ... 即可
```

无需任何 `C2`，无需 `ReCall.dll`。

---

## 3. 性能基线对比

同一台机器同一份 AutoCAD：

| 步骤 | Dev (C2) | Production (NETLOAD) | 说明 |
|---|---|---|---|
| AutoCAD 启动 | 不变 | 不变 | — |
| 加载 ReCall.dll | ~50ms | **0**（不存在） | Dev 多一次小 dll NETLOAD |
| C2 复制 bin → %TEMP% | 100~300ms | **0** | Production 无副本 |
| 读 Refactored.dll → byte[] | 30~80ms | **0** | NETLOAD 直接走 OS 文件映射 |
| `Assembly.Load(byte[])` vs `LoadFrom` | byte[] 略慢 | LoadFrom 略快 | 通常差 < 30ms |
| `PluginInitializer.Initialize` | 一致 | 一致 | 业务初始化路径完全相同 |
| 首条命令调度 | ReCall.Invoke 反射 | ProductionDispatcher.Invoke 反射 | 路径几乎一致，差 <1ms |

**结论**：Production 模式比 Dev (C2) **少 200~400ms 的 ReCall 壳层开销**。这就是用户真实感受的"NETLOAD 后到能用"的延迟。

---

## 4. 维护规则

### 4.1 同步 CommandFacade

`HyCADTool.Refactored\Production\ProductionCommandFacade.cs` 是 `ReCall\CommandFacade.cs` 的镜像。
**新增/删除 `[CommandMethod]` 时必须同步两份文件**。

机械同步脚本（PowerShell）：

```powershell
$src = Get-Content 'ReCall\CommandFacade.cs' -Raw
$out = $src `
  -replace 'namespace HyCADTool\.ReCall', 'namespace HyCADTool.Refactored.Production' `
  -replace 'class CommandFacade', 'class ProductionCommandFacade' `
  -replace 'ReCallClass\.Invoke', 'ProductionDispatcher.Invoke' `
  -replace '\[assembly: CommandClass.*\]\s*', ''
# 手动加 #if HYCAD_PRODUCTION / #endif 包裹
```

但日常加新命令应优先用 `commands.json` 的 N1~N50 占位符 → **不用动 CommandFacade.cs / ProductionCommandFacade.cs**。
只有把 N? 升格为正式命令名时才需同步两份。

### 4.2 改 commands.json

Production 配置下 `commands.json` 通过 `<None Link="commands.json">` 自动从 `ReCall\commands.json` 复制。
改了 `ReCall\commands.json` → `dotnet build -c Production` → 自动同步到 `bin\Production\`。

### 4.3 Production 构建依赖 ReCall.dll？

**不依赖**。Refactored.csproj 用 `<Reference HintPath>` 引用 ReCall.dll **仅为编译期类型可见性**（`CommandTable` 等），且 `<Private>false</Private>` 阻止复制到输出目录。生产 dll 加载时不需要 ReCall.dll 在场。

唯一前提：编译机上 `ReCall\bin\Debug\ReCall.dll` 必须存在（任何配置都行）。`EnsureReCallDllExists` Target 会在缺失时自动跑一次 ReCall 的 Debug 构建。

---

## 5. 何时切换两种模式

| 场景 | 用哪个 |
|---|---|
| 写业务代码 / 加新功能 | Dev (C2 热重载) |
| 测试单条命令的运行时 | Dev (C1 + SimpleLogger) |
| 测试整体启动延迟 | Production (NETLOAD) |
| 验证 commands.json 改动 | Dev（commands.json 即改即生效，零 C2） |
| 客户验收 / 上线发布 | Production |

---

## 6. 已删除的错误尝试

⚠ 历史曾尝试给 `ReCall.dll` 加 `HYCAD_PRODUCTION_MODE=1` 环境变量，让它内部切换到 `Assembly.LoadFrom`。
这是错的：用户实际客户机不会装 `ReCall.dll`，所以这种"模拟"不能真实反映生产性能；且会让 Refactored.dll 被锁。

正确做法：**ReCall 永远只做开发模式 (byte[] + temp 副本)**；
**生产性能用 Refactored 自带的 Production 配置编译出独立的 NETLOAD-able dll** —— 即本文档。

---

**文档版本**：v1.0 | **创建**：2026-04-25
