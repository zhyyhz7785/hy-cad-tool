# ReCall 验证清单（v2）

> 版本：2026-04（v2 命令表架构）
>
> 16 条**手工可执行**的验证场景 + `hyRecallSelfCheck` v2 输出逐行解读。
> 发版本前、排查疑难、新接手开发者首日体检用。

---

## 执行前提

一次性准备（改 `Recall.cs` / `CommandFacade.cs` / `CommandTable.cs` 之后必做）：

```
1. 关闭所有 AutoCAD 实例
2. dotnet build ReCall\ReCall.csproj  → 产出 ReCall\bin\Debug\ReCall.dll
3. 启动 AutoCAD，打开一个 DWG
4. NETLOAD → E:\...\ReCall\bin\Debug\ReCall.dll
5. hyRecallSelfCheck → 确认 "C2 已执行次数 = 0"、"_c1Action 已绑定 = 否"
```

**不需要**单独 `NETLOAD Refactored.dll` —— v2 架构下 Refactored 由 C2 复制到 `%TEMP%` 后匿名加载，永远不走 AutoCAD 的 NETLOAD 路径。

---

## A. 基础热重载（必做 5 条）

### A.1 首次 C2 成功

| 项目 | 内容 |
|---|---|
| 操作 | 命令 `C2` |
| 预期 | `C2 #1 完成 <ms> (复制X + 加载X + Terminate0 + InitializeXX + 表X)`<br>`命令表：81 条已配置 + 50 条占位符，解析失败 0 条`<br>`提示: 新命令只需改 commands.json 并用 N1~N50 占位符；...`<br>命令行**只有一套** "插件初始化中..." / "插件初始化完成！" |
| 诊断 | `hyRecallSelfCheck` → `C2 已执行次数 = 1`、`_c1Action 已绑定 = 是` |
| 反面信号 | 若出现两套 "插件初始化..." → `PluginInitializer` 又继承了 `IExtensionApplication`（陷阱 §4） |

### A.2 连续 C2 正常（Terminate 起作用）

| 项目 | 内容 |
|---|---|
| 操作 | 连续 `C2` 3 次 |
| 预期 | `C2 #2` / `#3` 输出，**Terminate 耗时 > 0ms**（比如 `Terminate57ms`），说明旧 PluginInitializer 被正确清理<br>每次只有一套 "插件初始化完成！"  |
| 诊断 | `hyRecallSelfCheck` → `C2 已执行次数 = 3`；副本数等于 `min(真实次数, 15)` |

### A.3 业务修改 C2 生效

| 项目 | 内容 |
|---|---|
| 前置 | 在 `DimensionAlignCommand.Execute` 顶部加 `ed.WriteMessage("new-flavor-v2")` |
| 操作 | VS 编译 → `C2` → 输 `ddaa` |
| 预期 | 命令行输出 `new-flavor-v2` |
| 诊断 | 若还是旧行为，`hyRecallSelfCheck` 看 `Refactored Location = <空 → 已热重载>`；非空 → C2 没成功 |

### A.4 副本清理生效

| 项目 | 内容 |
|---|---|
| 操作 | `C2` 20 次（或手工在 `%TEMP%\HyCADToolRefactored\` 下 `mkdir` 出 20 个假目录再 C2） |
| 预期 | 副本数稳定在 **≤ 15** |
| 诊断 | `hyRecallSelfCheck` → `副本数 = N (保留阈值 15)`，N 应 ≤ 15。长期超阈值说明有 Resolve handler 引用旧目录 |

### A.5 Refactored 确实是热重载版本

| 项目 | 内容 |
|---|---|
| 操作 | `C2` → `hyRecallSelfCheck` |
| 预期 | "Refactored 主程序集" 段：`Location = <空 → 已热重载 / Load(byte[])>`<br>"关联程序集" 段：`[Load(byte[])]  HyCADTool.Refactored  v1.0.0.0` |
| 诊断 | 若 `Location` 非空 → 某处残留了 `LoadFrom` 或 AutoCAD 直接 NETLOAD 了 Refactored（v2 架构下不应该发生） |

---

## B. 命令调用链（必做 5 条）

### B.1 普通命令

| 项目 | 内容 |
|---|---|
| 操作 | 输 `hy` |
| 预期 | HyTool 面板弹出 |
| 链路 | `Cmd_hy()` → `Invoke("hy")` → `CommandTable.Get("hy")` → 反射 `ShowPanelCommand.ShowHyToolPanel()` |

### B.2 带 bool 构造参数

| 项目 | 内容 |
|---|---|
| 操作 | 分别输 `g1` 和 `g2` |
| 预期 | 两条都进钢筋添加锚头 prompt；观察行为差异（锚头两端 vs 单端） |
| 链路 | `commands.json` 里 `g1` 的 `ctor: [false]`、`g2` 的 `ctor: [true]` → `Activator.CreateInstance(ReinAddAnchorCommand, new object[]{ true/false })` |

### B.3 带枚举构造参数

| 项目 | 内容 |
|---|---|
| 操作 | 分别输 `gb` / `gb1` / `gb2` |
| 预期 | 三条都走多重引线钢筋标注；行为对应 `Mode.Standard` / `Single` / `Six` |
| 链路 | `ctorEnumTypes: ["MleaderReinCommand+Mode"]` → `Enum.Parse(Mode, "Standard")` → 作为 ctor 第一个参数传入 |
| 反面 | 若字符串值写错（如 `"Stanadrd"`）→ `Enum.Parse` 抛 `ArgumentException`，命令行显示反射异常 |

### B.4 `_HyExec` 面板按钮链路

| 项目 | 内容 |
|---|---|
| 前置 | 打开 HyTool 面板，设好任何带"执行"按钮的命令参数 |
| 操作 | 面板按"执行"按钮 |
| 预期 | 面板 VM 写入 `_pendingAction` → 命令行发 `_HyExec` → `Invoke("_HyExec")` 走特殊分支 → `InvokePanelPendingCommand` 反射取 `ConsumePendingCommand` 并执行 |
| 诊断 | `hyRecallSelfCheck` → 命令校验结果里 `Special=1`（就是 `_HyExec`） |

### B.5 占位符 N1 临时映射

| 项目 | 内容 |
|---|---|
| 步骤 1 | 改 `commands.json` 的 `"N1"`：<br>`"N1": { "type": "HyCADTool.Refactored.Presentation.Commands.DimensionAlignCommand", "method": "Execute" }`<br>保存（**不用 C2，不用关 CAD**） |
| 步骤 2 | 输 `N1` |
| 预期 | 执行 `DimensionAlignCommand.Execute`（效果等同 `ddaa`） |
| 链路 | `Cmd_N1()` → `Invoke("N1")` → `CommandTable.EnsureLoaded()` 发现 mtime 变化 → 重读 JSON → 反射执行 |
| 收尾 | 把 `"N1"` 改回 `null` 保存 |

---

## C. 大小写容错（新增 1 条）

### C.1 大小写三重验证

| 项目 | 内容 |
|---|---|
| 操作 | 依次输 `hy` → ESC → `HY` → ESC → `Hy` |
| 预期 | 三次都打开 HyTool 面板，不出现 `✗ 命令表中未定义键 "..."` |
| 回归价值 | 防止将来误把 `CommandTable.cs` 的 `StringComparer.OrdinalIgnoreCase` 改回 `Ordinal`（陷阱 §1） |

---

## D. 错误处理（必做 3 条）

### D.1 commands.json 里 Type 拼错

| 项目 | 内容 |
|---|---|
| 前置 | 改 `commands.json` 把 `"hy"` 的 `type` 拼错：`"HyCADTool.Refactored.Presentation.Commands.ShowPanelComand"` (少一个 m) |
| 操作 | `C2` |
| 预期 | `C2 #N 完成` 后出现：<br>`命令表：81 条已配置 + ..., 解析失败 1 条`<br>`失败明细：`<br>`  - hy  →  TypeNotFound: HyCADTool.Refactored.Presentation.Commands.ShowPanelComand` |
| 收尾 | 改回正确拼写，再 `C2` → `Fail=0` |

### D.2 commands.json 里 Method 拼错

| 项目 | 内容 |
|---|---|
| 前置 | 把 `"hy"` 的 `method` 改成 `"ShowHyToolPanell"`（多一个 l） |
| 操作 | `C2` |
| 预期 | `失败明细：  - hy  →  MethodNotFound: ...ShowPanelCommand.ShowHyToolPanell` |
| 链路 | ValidateAll 的 `GetMethod(method, Public|Instance|Static)` 返回 null |

### D.3 Refactored.dll 找不到

| 项目 | 内容 |
|---|---|
| 前置 | 临时重命名 `HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll` |
| 操作 | `C2` |
| 预期 | `✗ 未找到: <路径>`<br>`  请先在 Visual Studio 中编译 HyCADTool.Refactored（配置: Debug）` |
| 收尾 | 恢复文件名，`C2` 成功 |

---

## E. 边界与安全（建议做 2 条）

### E.1 没 C2 直接输业务命令

| 项目 | 内容 |
|---|---|
| 前置 | 刚 NETLOAD 完 ReCall，**不做 C2** |
| 操作 | 输 `hy` |
| 预期 | `✗ Refactored 尚未加载，请先执行 C2。` |
| 价值 | 保证 Invoke 的第一道门槛生效 |

### E.2 hyRecallSelfCheck 不依赖 C2

| 项目 | 内容 |
|---|---|
| 前置 | 刚 NETLOAD 完 ReCall，**不做 C2** |
| 操作 | `hyRecallSelfCheck` |
| 预期 | 输出完整快照；其中 `C2 已执行次数 = 0`、`HyCADTool.Refactored: <未加载>`、`_c1Action 已绑定 = 否` |
| 价值 | 确保自检命令是"底座级"能力，任何异常时永远可用 |

---

## hyRecallSelfCheck 输出逐行解读（v2）

**典型健康输出**（摘自真实 C2 后）：

```
================ ReCall 自检 ================
ReCall.dll                = ReCall v1.0.0.0
ReCall.Location           = E:\...\ReCall\bin\Debug\ReCall.dll
C2 已执行次数             = 2
最近 C2 时间 (UTC)        = 2026-04-17 09:10:49
C1 _c1Action 已绑定       = 是
_refactoredAssembly       = 已缓存
_lastPluginInitInstance   = HyCADTool.Refactored.Presentation.PluginInitializer
AssemblyResolve 已注册    = True
UnhandledException 已注册 = True
当前依赖目录              = C:\Users\...\Temp\HyCADToolRefactored\639120138489497931
当前 NuGet 目录           = C:\Users\...\.nuget\packages

-------- Refactored 主程序集 --------
HyCADTool.Refactored v1.0.0.0
  Location = <空 → 已热重载 / Load(byte[])>
  IsDynamic=False, ReflectionOnly=False

-------- ServiceLocator --------
ServiceLocator.Container: Autofac.Core.Container (已初始化)

-------- 临时副本目录 --------
副本数 = 15  (保留阈值 15)
总占用 = 486.2 MB
基目录 = C:\Users\...\Temp\HyCADToolRefactored
  [0] 09:10:48 639120138489497931
  [1] 09:03:49 639120134298424440
  [2] 09:03:46 639120134259829844

-------- 关联程序集 (AppDomain 快照) --------
  [Load(byte[])]     Autofac                                  v7.1.0.0
  [Load(byte[])]     HyCADTool.Refactored                     v1.0.0.0
  [静态/LoadFrom]      Newtonsoft.Json                          v13.0.0.0
  [静态/LoadFrom]      ReCall                                   v1.0.0.0

-------- 命令表 (commands.json) --------
文件路径        = E:\...\ReCall\bin\Debug\commands.json
文件 mtime (UTC) = 2026-04-17 09:02:32
上次加载 (UTC)   = 2026-04-17 09:10:49
条目统计         = 81 已配置 + 50 占位符 = 共 131

-------- 命令校验结果 --------
OK=80  Special=1  Placeholder=50  Fail=0
  ✓ 全部已配置命令都能解析到 Refactored 里的 Type.Method

-------- 占位符状态 (N1~N50) --------
占位符总数 = 50，已占用 0，空闲 50
  空闲 (50 个)：N1 N2 N3 ... N50
==============================================
```

### 逐字段解读

| 字段 | 正常值 | 异常信号 |
|---|---|---|
| `C2 已执行次数` | ≥ 1 | 0 → 没 C2，业务命令会提示先 C2 |
| `_c1Action 已绑定 = 是` | 是 | 否 → C1 会报"请先执行 C2" |
| `_refactoredAssembly` | 已缓存 | 未缓存 → C2 没成功 |
| `_lastPluginInitInstance` | 类型全名 | `<空>` → C2 的 Initialize 反射失败 |
| `AssemblyResolve 已注册 = True` | True | False → 依赖解析全挂 |
| `Refactored Location = <空 → 已热重载>` | 空 | 非空 → 意外走了 NETLOAD 路径 |
| `ServiceLocator.Container: ... (已初始化)` | 已初始化 | 未初始化 → DI 命令全崩 |
| `副本数 ≤ 保留阈值` | 是 | 长期远超阈值 → 清理失败 |
| `关联程序集 Autofac/Newtonsoft` | `[Load(byte[])]` Autofac、`[静态/LoadFrom]` Newtonsoft | 都该显示对应的加载方式，异常值有版本冲突风险 |
| `命令表 上次加载` | 比 mtime 晚或相等 | 上次加载早于 mtime → 重载逻辑出 bug |
| `命令校验结果 Fail=0` | 0 | ≥ 1 → 有 type/method 拼错（陷阱 §1 附近） |
| `Special=1` | 1（就是 `_HyExec`） | ≠ 1 → `_HyExec` 被误删或多了别的 special |
| `Placeholder=50` | 50 | < 50 → N1~N50 有被改掉或删除 |
| `OK + Special + Placeholder` | = 条目统计总数 | 不等 → `ProbeCommandTableValidation` 逻辑又出 bug（历史 `Fail=-1` 案例） |
| `占位符状态 已占用` | 测试期 1~5，稳定期 0 | 过多 → 有实验性命令长期占槽没合并 |

---

## 快速排查：一条命令定位三类常见问题

| 问题描述 | hyRecallSelfCheck 关键字段 |
|---|---|
| "C2 后业务没变" | `Refactored Location` 是否为空；`C2 已执行次数` 是否递增；命令表 `Fail` 是否 0 |
| "输入 XXX 报'未知命令'" | 看 `commands.json` 里有没有这条；有就说明是 AutoCAD 命令表没扫到（陷阱 §2）；没有就是没配置 |
| "输入 XXX 报'命令表中未定义键'" | 大小写问题（陷阱 §1）或 `CommandFacade.cs` 里 Invoke 传的 key 写错 |
| "命令执行但行为不对" | `Refactored Location` 空？容器是否重建？回溯 AutofacModule 是否改了（陷阱 §5） |
| "临时目录吃磁盘" | "副本数 / 总占用 / 保留阈值"；关 CAD 手动清 |
| "面板 MissingMethodException" | 关联程序集 WebView2 版本 + context 标签（陷阱 §7） |
| "Autofac 注入失败" | `ServiceLocator.Container` 初始化状态 + AssemblyResolve 是否注册 |

---

## 版本对照表

| ReCall 版本 | 时间 | 关键改动 |
|---|---|---|
| 初版（HyCADTool 时代） | 2024 | 基础 C2/C1 |
| Refactored 适配版 | 2026-02 | 临时目录复制策略、WebView2 Preload |
| 2026-04 v1 | 2026-04 早 | **+** `hyRecallSelfCheck` 自检命令<br>**+** `CleanupOldTempCopies` 保留最新 15 副本<br>**+** `RouteThroughC1` + `CommandRelayStore` |
| **2026-04 v2（当前）** | 2026-04 | **+** `CommandTable` + `commands.json` 文件型映射表<br>**+** `CommandFacade` 集中 80+ `[CommandMethod]`<br>**+** `Invoke(key)` 反射调度 + 前置钩子<br>**+** 占位符 `N1~N50` 工作流<br>**+** 大小写不敏感字典（`OrdinalIgnoreCase`）<br>**-** 从 `PluginInitializer` 去除 `IExtensionApplication`<br>**-** 删除 `RouteThroughC1` / `CommandRelayStore` / `TryDispatchCommandKey` |
