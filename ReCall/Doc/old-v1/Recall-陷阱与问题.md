# Recall 陷阱与问题

> 版本：2026-04
>
> 本文档记录所有从 ReCall 机制**无法一眼看出**但实战中踩过的坑。每条陷阱附诊断方法和规避方案。

---

## §1. `[CommandMethod]` 只在**首次 NETLOAD** 扫描一次（最高频坑）

### 现象

刚给 `HyCADTool.Refactored` 添加了一个新命令：

```csharp
[CommandMethod("hyRoadAlnStation")]
public void Cmd_hyRoadAlnStation() => RouteThroughC1("hyRoadAlnStation", () => new RoadAlignmentStationCommand().Execute());
```

编译 → C2 成功 → 命令行输 `HYROADALNSTATION`：

```
未知命令"HYROADALNSTATION"。按 F1 查看帮助。
```

### 根因

AutoCAD 只有在 `NETLOAD` **新程序集路径**时才扫描 `[CommandMethod]` 属性并注册到命令表。`C2` 用的是 `Assembly.Load(byte[])`，CLR 层面是加载了新程序集，但 AutoCAD 的命令注册器**不会被触发**。

换言之：

| 命令生命周期 | 行为 |
|---|---|
| 首次 NETLOAD 时**已存在**的 `[CommandMethod]` | 每次 C2 后仍然可被输入 → 但调用的是 **旧方法体** |
| 首次 NETLOAD 时**未存在**的 `[CommandMethod]` | **永远不会**被注册到命令表，输入报"未知命令" |

### 两类命令、两种解法

**A. 老命令（修改业务代码）** → 让老方法 `RouteThroughC1`（见[工作原理 §5](./Recall-工作原理.md)）：

```csharp
[CommandMethod("hyRoadA")]
public void Cmd_hyRoadA() => RouteThroughC1("hyRoadA", () => new RoadAlignmentCommand().Execute());
```

一次关 CAD → NETLOAD Refactored 新版（吃进这个 RouteThroughC1 模板），之后 C2 热重载就能刷新业务。

**B. 新命令** → 用 `C1 → 交互式输入 key` 救急通道：

```
C2
C1
[C1] 输入要执行的命令 key（回车跳过走内置 fallback）: hyRoadAlnStation
```

在 `TestCommand.TryDispatchCommandKey` switch 里加一个 case 即可，分派表本身会随 C2 热重载。

### 诊断

用 `hyRecallSelfCheck` 看 `Refactored.Location` 是否为空：

```
Refactored v1.0.0.0
  Location = <空 → 已热重载 / Load(byte[])>  ← 如果是这个，说明 C2 生效了
```

若 Location 非空，说明用户仍在跑首次 NETLOAD 的版本，C2 没成功。

---

## §2. 静态字段覆盖的隐式耦合

### 现象

老 Refactored 对象里注入的某个路径（比如 `_currentDependenciesPath`），第二次 C2 后路径被刷成新副本目录。

### 根因

ReCall 的静态字段**每次 C2 都被覆盖**：

```csharp
_c1Action = CreateStaticMethodDelegate(asm, TEST_ENTRY_TYPE, TEST_ENTRY_METHOD, ed);
_currentDependenciesPath = loadDepsPath;       // ← 覆盖
_currentNugetPackagesPath = nugetPath;         // ← 覆盖
```

AssemblyResolve handler 注册一次，但**每次触发读的都是最新的 `_currentDependenciesPath`**。这是故意为之——老 assembly 里的对象若触发 Resolve，会得到最新副本里的依赖。

### 何时出问题

多数时候无害（依赖版本一致），除非：

- 你更改了 Refactored 的依赖版本（从 Newtonsoft 12 升到 13）
- 老对象持有对 Newtonsoft 12 类型的引用
- 老对象触发 Resolve → 读到新路径 → 返回 Newtonsoft 13 → **类型不匹配**

### 规避

修改依赖版本号后**必须关 CAD 重启**。仅仅 C2 会留下"新老 Assembly 引用同名类型不同 identity"的地雷。

---

## §3. 临时目录堆积（已修复）

### 历史现象

每次 C2 在 `%TEMP%\HyCADToolRefactored\<ticks>\` 留一份 bin/Debug 副本（100~300 MB）。长期开发 100 次 C2 → 几十 GB，磁盘爆掉。

### 当前方案

`Recall.cs` 的 `CleanupOldTempCopies(TEMP_COPY_RETAIN)`：C2 启动时按 `LastWriteTimeUtc` 降序保留最新 `TEMP_COPY_RETAIN`（默认 15）个，其余 `Directory.Delete(recursive: true)`。删除失败（文件被进程持有）静默跳过，下次 C2 再清。

### 诊断

```
hyRecallSelfCheck
-------- 临时副本目录 --------
副本数 = 12  (保留阈值 15)
总占用 = 1.2 GB
基目录 = C:\Users\xxx\AppData\Local\Temp\HyCADToolRefactored
  [0] 14:23:11 638792410914231105
  [1] 14:15:02 638792406028150281
```

如果副本数 >> 保留阈值，说明删除失败频发——多半是有 Resolve handler 仍在通过 `_currentDependenciesPath` 访问某副本的文件。可以关 CAD 后手动 `rd /s /q %TEMP%\HyCADToolRefactored` 清空。

---

## §4. WebView2 托管依赖版本冲突（历史事故）

### 现象（历史）

MarkdownEditor 面板打开时报：

```
Method not found:
  'System.Threading.Tasks.Task Microsoft.Web.WebView2.Wpf.WebView2.EnsureCoreWebView2Async(
      Microsoft.Web.WebView2.Core.CoreWebView2Environment)'
```

### 根因

`Load(byte[])` 无 hint path → CLR 解析 `Microsoft.Web.WebView2.Wpf` 时，发现 AutoCAD 进程里已经加载了**旧版** WebView2（AutoCAD 自带），**直接复用** → 方法签名不匹配。

### 解法（已应用）

`EditorLoader` 在加载 MarkdownEditor.dll 之前**先 `LoadFrom` 我们期望版本的 WebView2**，让它进 LoadFrom context：

```csharp
PreloadManagedDependencies(_net8Dir);
// → LoadFrom Microsoft.Web.WebView2.Wpf.dll
// → LoadFrom Microsoft.Web.WebView2.Core.dll

var asm = Assembly.Load(File.ReadAllBytes(dllPath));
// 此时 MarkdownEditor 引用 WebView2 类型，CLR 命中 LoadFrom 的版本
```

**通用套路**：凡是进程里可能已有旧版的托管依赖（WebView2、SQLite、System.Text.Json 等），在 `Load(byte[])` 主 DLL 之前用 `LoadFrom` 预加载期望版本。

### 诊断

`hyRecallSelfCheck` 的"关联程序集"列表：

```
[静态/LoadFrom]    Microsoft.Web.WebView2.Wpf                v1.0.2210.55
[静态/LoadFrom]    Microsoft.Web.WebView2.Core               v1.0.2210.55
[Load(byte[])]    HyCADTool.MarkdownEditor                   v1.0.0.0
```

如果 WebView2 显示 `[Load(byte[])]` 或版本异常，说明 Preload 失效了。

---

## §5. `Assembly.Location` 在热重载后为空字符串

### 现象

```csharp
var ver = Assembly.GetExecutingAssembly().Version;    // 1.0.0.0  ✓
var loc = Assembly.GetExecutingAssembly().Location;   // ""  ⚠
```

### 根因

`Assembly.Load(byte[])` 没有源文件路径，`Location` 属性返回空字符串。

### 踩坑场景

```csharp
// ❌ 热重载后 Directory 为 null，下面会 NPE
var resourcesDir = Path.Combine(
    Path.GetDirectoryName(typeof(MyClass).Assembly.Location),
    "Resources");
```

### 规避

- 需要定位资源文件：用 `ReCall.ResourceManager.ResourceAssembly` + 嵌入资源，或用 `AppDomain.CurrentDomain.BaseDirectory`
- 需要判断"当前是否热重载"：`bool isHot = string.IsNullOrEmpty(typeof(X).Assembly.Location);`
- 日志打印版本信息：读 `AssemblyName.Version`，不要读 `Location`

---

## §6. `[CommandMethod]` 不能写在热重载目标里（原则）

### 规则

`[CommandMethod]` **只能**注册在"首次 NETLOAD 即稳定"的程序集上。具体映射：

| 位置 | 允许 `[CommandMethod]` | 理由 |
|---|---|---|
| `ReCall.dll` | ✓ | 永不变，命令表永久绑定 |
| `HyCADTool.Refactored.dll` 的 `CommandRegistry` | ✓（但必须走 `RouteThroughC1`） | 老命令以转发器身份存在 |
| 其他热重载组件（如 `HyCADTool.MarkdownEditor.dll`） | **✗** | 会 `eDuplicateKey` 或永远不生效 |

### 违反后的表现

- 第一次 NETLOAD MarkdownEditor 并注册了 `[CommandMethod("HYMD")]`
- C2 重载 Refactored → 通过依赖关系拉 MarkdownEditor 的新版
- AutoCAD 命令注册器发现同名命令 → 抛 `eDuplicateKey`，或者静默保留老版
- 用户输 `HYMD` 永远跑老版

### 规避

在 MarkdownEditor 这类可热重载组件里**绝不**放 `[CommandMethod]`。所有入口通过 `TestCommand.TryDispatchCommandKey` 分派，或通过 Refactored 里的 `CommandRegistry` 用 `RouteThroughC1` 转发。

---

## §7. `SendStringToExecute` 的节奏陷阱

### 现象

`RouteThroughC1` 内部用：

```csharp
Application.DocumentManager.MdiActiveDocument
    .SendStringToExecute("C1 ", true, false, true);
```

参数 `activate=true, wrapUp=false, echo=true`。如果连续触发两个需要 relay 的命令，可能出现第二个命令的 relay 文件被**先**写入，但第一个命令的 C1 触发**先**到达，导致消费错 key。

### 诊断

看 `hyRecallSelfCheck` 的 `CommandRelayStore` 段：

```
-------- CommandRelayStore --------
存在 pending key: C:\...\HyCADTool.Refactored.command-relay.json  (写入时间 14:24:03)
  内容: {"CommandKey":"hyRoadA","Timestamp":1744779843000}
```

如果"写入时间"明显晚于你上一次 C1 的时间，说明 relay 文件泄漏了。

### 规避

- 不要在**同一个 C2 周期内连续触发两条** relay 命令（间隔至少等 C1 回显后）
- `TryConsume` 读完立即 `File.Delete`，所以单条命令的循环是安全的
- 异常场景：手动删 `%TEMP%\HyCADTool.Refactored.command-relay.json`

---

## §8. ServiceLocator.Container 首次 C2 后不再更新

### 现象

修改了 `AutofacModule` 的注册规则（新加一个接口实现），C2 后老的单例没有被替换。

### 根因

`ReCall.InitializeServiceLocator` 判空跳过：

```csharp
if (containerProp?.GetValue(null) != null) return;   // 已初始化，跳过
```

这是**故意**的：多次 C2 若重建 Container，会丢失运行时状态（ViewModel 实例、面板句柄等）。

### 规避

两选一：

- **改 DI 注册** → 关 CAD 重启（一次性开销）
- **改命令实现但不改 DI** → C2 热重载（常态）

---

## §9. ReCall 本身的修改必须关 CAD

### 规则

**`Recall.cs` 的任何修改都不能靠 C2 吃进去**——它自己是锚点，不参与热重载。要改 ReCall：

```
1. 关闭 AutoCAD
2. dotnet build ReCall\ReCall.csproj
3. 启动 AutoCAD
4. NETLOAD E:\...\ReCall\bin\Debug\ReCall.dll
5. NETLOAD E:\...\HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll
6. 恢复 C2/C1 循环
```

判断 AutoCAD 正锁着 ReCall.dll 的最快方式：`dotnet build` 看到 `MSB3026 文件被 AutoCAD Application 锁定`。

### 本文档对应的一次升级

2026-04 版本新增 `hyRecallSelfCheck` + `CleanupOldTempCopies` + 删除 `AgentDebugLog` 硬编码路径。**所有现有用户必须执行一次"关 CAD → 重 NETLOAD"** 才能用上自检命令。
