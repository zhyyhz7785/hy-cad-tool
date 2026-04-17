# Recall 验证清单

> 版本：2026-04
>
> 16 条**手工可执行**的验证场景 + `hyRecallSelfCheck` 输出解读。每条都有"操作 / 预期 / 诊断"三段。发版本前、排查疑难问题时用。

---

## 执行前提

- 首次准备（**必须先关 CAD 重新 NETLOAD 当前 ReCall.dll**，因为新增了 `hyRecallSelfCheck` / `CleanupOldTempCopies` / 移除硬编码日志路径）：

```
1. 关闭所有 AutoCAD 实例
2. dotnet build ReCall\ReCall.csproj      → 生成 bin/Debug/ReCall.dll
3. 启动 AutoCAD，打开一个 DWG
4. 命令：NETLOAD → E:\...\ReCall\bin\Debug\ReCall.dll
5. 命令：NETLOAD → E:\...\HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll
6. 命令：hyRecallSelfCheck → 确认输出中 "C2 已执行次数 = 0" "_c1Action 已绑定 = 否"
```

---

## A. 基础热重载（必做 5 条）

### A.1 首次 C2 成功绑定

| 项目 | 内容 |
|---|---|
| 操作 | 命令 `C2` |
| 预期输出 | `C2 #1 完成 <ms> (复制<ms> + 加载<ms> + DI<ms>)` 和提示 `新增 [CommandMethod] 命令首次 NETLOAD 后才进命令表` |
| 诊断 | `hyRecallSelfCheck` 应显示 `C2 已执行次数 = 1`、`_c1Action 已绑定 = 是` |

### A.2 C1 调用绑定的 TestCommand

| 项目 | 内容 |
|---|---|
| 操作 | `C1`（在 TestCommand.Run 的 fallback 处直接输入回车跳过） |
| 预期输出 | `[C1] v1.0.0.0 @ <时间>` + `INFO: 命令执行 耗时 <ms>` |
| 诊断 | 若出现 `✗ 请先执行 C2 加载插件`，说明 `_c1Action == null`，上一次 C2 未成功或 TestCommand.Run 签名变了 |

### A.3 修改业务后 C2 生效

| 项目 | 内容 |
|---|---|
| 操作 | 改 `TestCommand.Run` fallback 那行（如改成 `new HelloCommand().Execute()`） → VS 编译 → `C2` → `C1` |
| 预期 | 新代码被执行（看到 HelloCommand 的输出） |
| 诊断 | 若还是旧行为，`hyRecallSelfCheck` 看 `Refactored v1.0.0.0 Location = <空 → 已热重载>`，如果 Location 非空说明没吃到新版 |

### A.4 连续多次 C2 无副作用

| 项目 | 内容 |
|---|---|
| 操作 | 连续 `C2` 3 次 |
| 预期 | 每次输出 `C2 #2/#3/#4 完成`，`_c2Count` 递增 |
| 诊断 | `hyRecallSelfCheck` 看"临时副本目录"段：副本数 = min(真实 C2 次数, 15) |

### A.5 临时副本自动清理

| 项目 | 内容 |
|---|---|
| 操作 | `C2` 20 次（或手工在 `%TEMP%\HyCADToolRefactored\` 下 `mkdir` 出 20 个假目录再 C2） |
| 预期 | 副本数稳定在 **≤ 15** |
| 诊断 | `hyRecallSelfCheck` "副本数 = N (保留阈值 15)" 中 N ≤ 15。如果 N 长期 > 15 → 删除失败（Resolve handler 引用了旧目录），关 CAD 重启可重置 |

---

## B. 命令路由（必做 4 条）

### B.1 老命令通过 RouteThroughC1 吃到新代码

| 项目 | 内容 |
|---|---|
| 前置 | 修改 `RoadAlignmentCommand.Execute` 的任意字符串（如在开头 `ed.WriteMessage("new-flavor-v2")`）→ VS 编译 → `C2` |
| 操作 | 命令 `hyRoadA` |
| 预期 | 看到 `new-flavor-v2` 和 `RouteThroughC1` 的流水线痕迹（C1 被触发） |
| 诊断 | 若仍是旧行为，说明当前 `CommandRegistry.Cmd_hyRoadA` 的方法指针还是第一份 NETLOAD 版本，但那份应该已经是 `RouteThroughC1` 模板了。检查：`hyRecallSelfCheck` → `Refactored Location` 是否为空。非空则说明第一次 NETLOAD 的 Refactored 还没升级到有 `RouteThroughC1` 的版本。 |

### B.2 新命令通过 C1 交互式输入运行

| 项目 | 内容 |
|---|---|
| 前置 | `TryDispatchCommandKey` 里已有 `case "hyRoadAlnStation"` |
| 操作 | `C2` → `C1` → 命令行显示 `[C1] 输入要执行的命令 key`，输入 `hyRoadAlnStation` 回车 |
| 预期 | 执行 RoadAlignmentStationCommand，输出桩号标注完成 |
| 诊断 | 若 `[C1] 未识别的命令 key`，说明 `TryDispatchCommandKey` switch 漏了这个 case |

### B.3 直接输入新命令名触发"未知命令"（**符合预期**）

| 项目 | 内容 |
|---|---|
| 操作 | 命令 `hyRoadAlnStation`（直接输命令名） |
| 预期 | `未知命令"HYROADALNSTATION"。按 F1 查看帮助。` |
| 解读 | 这是**正常**。新 `[CommandMethod]` 没进命令表，必须走 B.2 路径。要想直接输也能跑，只能**关 CAD → NETLOAD 新版** |

### B.4 CommandRelayStore 正常消费

| 项目 | 内容 |
|---|---|
| 操作 | `C2` → 用命令 `hyRoadA`（走 RouteThroughC1） |
| 预期 | relay 文件在 C1 后被删除 |
| 诊断 | 执行完后立即 `hyRecallSelfCheck`，应显示 `CommandRelayStore: 无 pending key` |

---

## C. 依赖与异常（必做 4 条）

### C.1 AssemblyResolve 解析依赖

| 项目 | 内容 |
|---|---|
| 操作 | `C2` → 看 `hyRecallSelfCheck` 的"关联程序集" |
| 预期 | 能看到 `Autofac`、`Newtonsoft.Json` 等依赖；如果 `net8` 依赖用到 MarkdownEditor，也应在列 |
| 诊断 | 若 C2 报 `Could not load file or assembly 'Autofac'`，说明 `AssemblyResolve` 未注册或 `_currentDependenciesPath` 为 null。用 `hyRecallSelfCheck` 验证 |

### C.2 WebView2 版本一致

| 项目 | 内容 |
|---|---|
| 操作 | 打开 Refactored 统一面板（或调用 MarkdownEditor），然后 `hyRecallSelfCheck` |
| 预期 | `Microsoft.Web.WebView2.Wpf` / `.Core` 在列表中显示 `[静态/LoadFrom]` 标签，版本非旧 |
| 诊断 | 若显示 `[Load(byte[])]` 或报 `MissingMethodException: EnsureCoreWebView2Async`，Preload 失效（见 [Recall-陷阱与问题.md §4](./Recall-陷阱与问题.md)） |

### C.3 缺失 DLL 时优雅失败

| 项目 | 内容 |
|---|---|
| 操作 | 临时把 `HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll` 改名 → `C2` |
| 预期 | `✗ 未找到: <路径>` + `期望路径: ...` + `请先在 Visual Studio 中编译` |
| 恢复 | 改回文件名，`C2` 应成功 |

### C.4 加载时抛异常被捕获

| 项目 | 内容 |
|---|---|
| 操作 | 在 `HyCADTool.Refactored` 的静态构造函数里故意抛 `throw new Exception("boom")` → C2 |
| 预期 | `✗ 加载失败: Exception: boom` + 可能有 InnerException 打印 |
| 诊断 | CAD 不崩，命令行给出错误。修复后 C2 恢复 |

---

## D. 边界与异常情景（建议做 3 条）

### D.1 从未 C2 直接 C1

| 项目 | 内容 |
|---|---|
| 前置 | 刚 NETLOAD 完 ReCall，**不做 C2** |
| 操作 | `C1` |
| 预期 | `✗ 请先执行 C2 加载插件` |
| 诊断 | 正常行为，不需修复 |

### D.2 hyRecallSelfCheck 不依赖 C2

| 项目 | 内容 |
|---|---|
| 前置 | 刚 NETLOAD 完 ReCall，**不做 C2** |
| 操作 | `hyRecallSelfCheck` |
| 预期 | 输出完整快照，其中 `C2 已执行次数 = 0`、`HyCADTool.Refactored: <未加载>` |
| 价值 | 验证自检命令是"底座级"能力，出问题时永远可用 |

### D.3 ReCall 本身需要重启

| 项目 | 内容 |
|---|---|
| 操作 | 改 `Recall.cs` 任何一行 → `dotnet build ReCall.csproj` |
| 预期 | 报 `MSB3026 文件被 AutoCAD Application 锁定` |
| 解读 | 这是**应该**的。ReCall 不能热重载自己，必须关 CAD 重启 |

---

## hyRecallSelfCheck 输出逐行解读

**典型健康输出**：

```
================ ReCall 自检 ================
ReCall.dll                = ReCall v1.0.0.0
ReCall.Location           = E:\...\ReCall\bin\Debug\ReCall.dll
C2 已执行次数             = 3
最近 C2 时间 (UTC)        = 2026-04-16 06:21:43
C1 _c1Action 已绑定       = 是
AssemblyResolve 已注册    = True
UnhandledException 已注册 = True
当前依赖目录              = C:\Users\...\Temp\HyCADToolRefactored\638792410914231105
当前 NuGet 目录           = C:\Users\...\.nuget\packages

-------- Refactored 主程序集 --------
HyCADTool.Refactored v1.0.0.0
  Location = <空 → 已热重载 / Load(byte[])>
  IsDynamic=False, ReflectionOnly=False

-------- ServiceLocator --------
ServiceLocator.Container: Autofac.Core.Container (已初始化)

-------- 临时副本目录 --------
副本数 = 3  (保留阈值 15)
总占用 = 486.2 MB
基目录 = C:\Users\...\Temp\HyCADToolRefactored
  [0] 14:21:43 638792410914231105
  [1] 14:18:22 638792408829384712
  [2] 14:15:01 638792406843952001

-------- 关联程序集 (AppDomain 快照) --------
  [Load(byte[])]    HyCADTool.Refactored                    v1.0.0.0
  [静态/LoadFrom]    ReCall                                   v1.0.0.0
  [静态/LoadFrom]    Autofac                                  v6.5.0.0
  [静态/LoadFrom]    Newtonsoft.Json                          v13.0.0.0

-------- CommandRelayStore --------
无 pending key（正常：relay 文件会被 C1 消费后立即删除）
==============================================
```

### 逐字段解读

| 字段 | 正常值 | 异常信号 |
|---|---|---|
| `C2 已执行次数` | ≥ 1 | 0 表示未 C2，C1 会失败 |
| `_c1Action 已绑定 = 是` | 是 | 否 → C1 会报 `请先执行 C2` |
| `AssemblyResolve 已注册 = True` | True | False → C2 后依赖加载失败 |
| `Refactored Location = <空 → 已热重载>` | 空字符串 | 非空 → 没走 `Load(byte[])`，可能是首次 NETLOAD 后没 C2 |
| `ServiceLocator.Container: ... (已初始化)` | 已初始化 | 未初始化 → DI 命令会崩 |
| `副本数 ≤ 保留阈值` | 是 | 远超阈值 → 清理失败，文件被锁 |
| 关联程序集 `[静态/LoadFrom]` Autofac/Newtonsoft | 有 | 若为 `[Load(byte[])]` → Preload 失效，可能版本冲突 |
| `CommandRelayStore: 无 pending key` | 常态 | 有残留 → 上一次 relay 没被消费，查 SendStringToExecute 时机问题 |

---

## 快速上手：一条命令排查三种常见问题

| 问题描述 | hyRecallSelfCheck 关键字段 |
|---|---|
| "C2 后业务没变" | `Refactored Location` = 空？`C2 已执行次数` = 是否递增？ |
| "新命令 未知命令" | `_c1Action 已绑定 = 是`？`Refactored Location = <空>`？（真正原因见陷阱文档 §1） |
| "临时目录吃磁盘" | `副本数 / 总占用 / 保留阈值`；关 CAD 手动清 |
| "面板 MissingMethodException" | 关联程序集 WebView2 版本 + context 标签 |
| "Autofac 注入失败" | `ServiceLocator.Container` 初始化状态 |

---

## 版本对照表

| ReCall 版本 | 时间 | 关键改动 |
|---|---|---|
| 初版（HyCADTool 时代） | 2024 | 基础 C2/C1 |
| Refactored 适配版 | 2026-02 | 临时目录复制策略、WebView2 Preload |
| 2026-04（当前） | 2026-04 | **+** `hyRecallSelfCheck` 自检命令<br>**+** `CleanupOldTempCopies` 保留最新 15 副本<br>**-** 硬编码 Debug 日志路径<br>**-** 5 个历史归档 cs 文件 |
