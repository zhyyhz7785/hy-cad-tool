# ReCall 陷阱与问题（v2）

> 版本：2026-04（v2 命令表架构）
>
> 本文档记录所有从 ReCall 机制**无法一眼看出**但实战中踩过的坑。每条陷阱附"现象 / 根因 / 解法 / 诊断"。
>
> v1 时代的坑（`RouteThroughC1` / `CommandRelayStore` / `SendStringToExecute` 节奏问题）已随架构升级消失，只在 [old-v1/](./old-v1/) 里留档。v2 的新坑集中在命令表、反射、PluginInitializer 生命周期。

---

## §1. 命令名大小写不匹配（2026-04 已修）

### 现象

```
命令: HY
✗ 命令表中未定义键 "hy"。请检查 commands.json（...）。
```

`hyRecallSelfCheck` 却显示 `81 条已配置 + 解析失败 0 条`，一切正常。

### 根因

- `commands.json` 里是 `"Hy"`（大写 H）
- `CommandFacade.cs` 里是 `[CommandMethod("hy")]` → `Invoke("hy")`（小写）
- `CommandTable` 的字典之前用 `StringComparer.Ordinal`（**区分大小写**）

AutoCAD 命令行不区分大小写，但我们在 `CommandTable` 里做了**严格**字典匹配 → 对不上。`ValidateAll` 走的是 `foreach` 遍历，和 `TryGetValue` 不同路径，所以 SelfCheck 看起来正常。

### 解法（已应用）

`CommandTable.cs` 三处 `Dictionary<string, CommandEntry>` 的 `Comparer` 统一改成 `StringComparer.OrdinalIgnoreCase`：

```csharp
private static Dictionary<string, CommandEntry> _entries
    = new Dictionary<string, CommandEntry>(StringComparer.OrdinalIgnoreCase);
```

效果：`commands.json` 里写 `"Hy"`、`CommandFacade` 里写 `Invoke("hy")`、用户输 `HY` — 三套大小写都命中同一条。

### 诊断

- 看 `hyRecallSelfCheck` 的"命令表 / 命令校验结果"段，`Fail=0` 只代表**加载 + 反射校验**过关，不代表 `Invoke` 能找到键
- 实际输一次命令（如 `hy`）是唯一确认 `Invoke` 路径的方法
- 如果将来再出现 `✗ 命令表中未定义键 "xxx"`，先检查 `CommandTable.cs` 的 `StringComparer.*` 有没有被改回去

---

## §2. 新增 `[CommandMethod]` 仍然要关 CAD 重新 NETLOAD ReCall

### 现象

在 `CommandFacade.cs` 里加了一行：

```csharp
[CommandMethod("hyNewFeature")]
public void Cmd_hyNewFeature() => ReCallClass.Invoke("hyNewFeature");
```

`dotnet build ReCall` → AutoCAD 命令行输 `hyNewFeature`：

```
未知命令"HYNEWFEATURE"。按 F1 查看帮助。
```

### 根因

**AutoCAD 只在 `NETLOAD` 一个新路径的 DLL 时扫描 `[CommandMethod]`。** ReCall 本身也受此限制——它的命令表是首次 NETLOAD 时一次性扫描的。

v2 架构已经把 Refactored.dll 的 NETLOAD 问题绕开了，但 ReCall.dll 自己还是"首次 NETLOAD"一锤定音。

### 解法

两种路线：

**A. 临时测试**：用占位符 `N1~N50` 暂时承载新命令（见 §3）。

**B. 正式发版**：积累几条新命令，**一次性**关 CAD 合入：

```
1. 关 AutoCAD
2. CommandFacade.cs 批量加 [CommandMethod]
3. commands.json 批量加 entry
4. dotnet build ReCall
5. 启动 AutoCAD → NETLOAD ReCall.dll → C2
```

### 诊断

`hyRecallSelfCheck` 看"条目统计"行：

```
条目统计  = 81 已配置 + 50 占位符 = 共 131
```

如果 81 没变大，说明你没改 `commands.json`，或者改了但 ReCall 没读取（mtime 可能没变化？保存一下）。
如果 `commands.json` 里有新 key 但 AutoCAD 不认，说明 `CommandFacade.cs` 没补对应 `[CommandMethod]`，且没重 NETLOAD。

---

## §3. 占位符 N1~N50 用法（推荐的新命令工作流）

### 背景

`CommandFacade.cs` 预埋了：

```csharp
[CommandMethod("N1")]  public void Cmd_N1()  => ReCallClass.Invoke("N1");
[CommandMethod("N2")]  public void Cmd_N2()  => ReCallClass.Invoke("N2");
...
[CommandMethod("N50")] public void Cmd_N50() => ReCallClass.Invoke("N50");
```

以及 `commands.json` 里：

```jsonc
"N1":  null, "N2":  null, ..., "N50": null
```

### 使用场景

你想测一个全新的命令类 `MyExperimentCommand.Execute()`：

**步骤 1** — 改 `commands.json`（保存即可）：
```jsonc
"N1": { "type": "HyCADTool.Refactored.Presentation.Commands.MyExperimentCommand",
        "method": "Execute" }
```

**步骤 2** — VS 编译 Refactored → `C2`。

**步骤 3** — AutoCAD 命令行输 `N1` → 反射执行 `MyExperimentCommand.Execute`。

**步骤 4** — 改代码 / 改 json，重复 2~3，**全程不关 CAD**。

### 调通后合并

攒够 5~10 条新命令后，一次性关 CAD：

1. `CommandFacade.cs`：把 `Cmd_N1` 改名为真名 `Cmd_hyMyExperiment`，`[CommandMethod("N1")]` 改为 `[CommandMethod("hyMyExperiment")]`
2. `commands.json`：把 `"N1"` 改名为 `"hyMyExperiment"`（内容不变），再给 `"N1": null` 补回来
3. `dotnet build ReCall` → 启动 CAD → `NETLOAD ReCall.dll`

### 诊断

`hyRecallSelfCheck` 的"占位符状态"段：

```
-------- 占位符状态 (N1~N50) --------
占位符总数 = 50，已占用 3，空闲 47
  已占用 (3 个): N1 → MyExperimentCommand.Execute
                N2 → FooBarCommand.Execute
                N3 → BazCommand.Execute
```

随时可以看哪几个 N 正在被实验性命令占用。

---

## §4. `PluginInitializer : IExtensionApplication` 会双初始化（2026-04 已修）

### 现象

首次 `C2` 后命令行出现**两套**完全一样的 "插件初始化中..." 日志：

```
========================================
HyCADTool.Refactored 插件初始化中...
========================================
✓ 依赖注入容器已初始化
...
✓ HyCADTool.Refactored 插件初始化完成！
========================================
========================================
HyCADTool.Refactored 插件初始化中...       ← 第二套
========================================
✓ 依赖注入容器已初始化
...
✓ HyCADTool.Refactored 插件初始化完成！
========================================
```

### 根因

AutoCAD 内部监听 `AppDomain.AssemblyLoad` 事件，**即使是 `Assembly.Load(byte[])` 加载的 Refactored.dll**，AutoCAD 也会扫描其中的 `IExtensionApplication` 实现并**自动调** `Initialize` / `Terminate`。结果：

```
ReCall 反射调一次   +  AutoCAD 自动调一次  =  两次 Initialize
```

双初始化会导致：
- Autofac 容器被注册两次（Reset + 重建）
- 文档事件订阅两遍（命令响应可能跑两次）
- 面板/图层冗余初始化

### 解法（已应用）

`PluginInitializer` 不再继承 `IExtensionApplication`：

```csharp
// 旧：public class PluginInitializer : IExtensionApplication
// 新：
public class PluginInitializer
{
    public void Initialize() { ... }
    public void Terminate()  { ... }
}
```

AutoCAD 扫不到接口 → 自动路径关闭 → 生命周期完全由 `ReCall.Reload()` 控制。

### 诊断

第一次 C2 后看命令行 "插件初始化完成！" 应该只有**一处**。如果再次出现两套，说明：
- 有某个 C# 工具重新生成了接口声明
- 或者 Refactored 里另有类型实现了 `IExtensionApplication` 被 AutoCAD 扫到

查找命令：`rg "IExtensionApplication" HyCADTool.Refactored` 应该返回 0 匹配。

---

## §5. DI 注册改动需关 CAD

### 现象

修改了 `HyCADTool.Refactored/Infrastructure/AutoCAD/AutofacModule.cs`，加了新接口实现：

```csharp
builder.RegisterType<NewRenderer>().As<IRenderer>().SingleInstance();
```

C2 后业务代码里 `ServiceLocator.Resolve<IRenderer>()` 仍然返回**老实现**。

### 根因

v2 里 `PluginInitializer.Terminate` 会 Reset 容器、`Initialize` 会重建。**所以大多数 DI 变更是跟着 C2 走的**。

但**两个例外**会导致看起来像"没刷新"：

1. **老 Assembly 里某个对象**（面板 ViewModel、TransactionManager 回调）仍持有旧 `IRenderer` 引用（它已经在 C2 前被 new 出来了），它看不到新注册。
2. 你在 Terminate 之后、新 Initialize 之前有异步任务恰好访问容器 —— 很罕见。

### 解法

**规则**：只要改 `AutofacModule` 的**注册规则**（加接口、换实现、改生命周期），保险起见**关 CAD 重启**。这是一次性成本，能避免一整类"奇怪的单例看不到更新"问题。

改命令类内部实现（不碰 DI 注册），C2 就够了。

---

## §6. `Assembly.Location` 在热重载后为空字符串

### 现象

```csharp
var loc = Assembly.GetExecutingAssembly().Location;  // "" ⚠
var dir = Path.GetDirectoryName(loc);                 // null
var res = Path.Combine(dir, "Resources");             // NPE
```

### 根因

`Assembly.Load(byte[])` 没有源文件路径，`Location` 返回 `""`。

### 规避

- **定位资源**：改用嵌入资源 + `Assembly.GetManifestResourceStream`，或 `AppDomain.CurrentDomain.BaseDirectory`（AutoCAD.exe 所在目录，业务代码通常不用这个）
- **定位自身副本目录**：反射读 `ReCall.ReCallClass._currentDependenciesPath` 或让 ReCall 提供 API
- **判断"是否热重载"**：`bool isHot = string.IsNullOrEmpty(typeof(X).Assembly.Location);`
- **打印版本**：用 `AssemblyName.Version`，不要读 `Location`

---

## §7. WebView2 托管依赖版本冲突（历史，架构未变）

### 现象（历史事故）

MarkdownEditor 面板打开时报：

```
Method not found:
  'System.Threading.Tasks.Task Microsoft.Web.WebView2.Wpf.WebView2.EnsureCoreWebView2Async(...)'
```

### 根因

`Load(byte[])` 无 hint path → CLR 解析 `Microsoft.Web.WebView2.Wpf` 时发现 AutoCAD 进程里已有**旧版** WebView2（AutoCAD 自带）→ 直接复用 → 方法签名不匹配。

### 解法（已应用）

`EditorLoader`（Refactored 里）在加载 MarkdownEditor.dll 之前**先 `LoadFrom` 我们期望版本的 WebView2**：

```csharp
PreloadManagedDependencies(_net8Dir);               // LoadFrom Microsoft.Web.WebView2.{Wpf,Core}.dll
var asm = Assembly.Load(File.ReadAllBytes(dllPath));  // 此时 CLR 命中 LoadFrom 版本
```

**通用套路**：进程里可能有旧版的托管依赖（WebView2、SQLite、System.Text.Json 等），`Load(byte[])` 主 DLL 之前用 `LoadFrom` 预加载期望版本。

### 诊断

`hyRecallSelfCheck` → "关联程序集"段：

```
[静态/LoadFrom]    Microsoft.Web.WebView2.Wpf    v1.0.2210.55
[静态/LoadFrom]    Microsoft.Web.WebView2.Core   v1.0.2210.55
[Load(byte[])]    HyCADTool.MarkdownEditor       v1.0.0.0
```

WebView2 显示 `[Load(byte[])]` 或版本异常 → Preload 失效。

---

## §8. 临时目录堆积（已有清理机制）

### 历史现象

每次 C2 在 `%TEMP%\HyCADToolRefactored\<ticks>\` 留一份 bin/Debug 副本（100~300 MB）。长期开发 100 次 C2 → 几十 GB，磁盘爆掉。

### 当前方案

`Recall.cs` 的 `CleanupOldTempCopies(TEMP_COPY_RETAIN = 15)`：

- C2 启动时按 `LastWriteTimeUtc` 降序，保留最新 15 个
- 其余 `Directory.Delete(recursive: true)`
- 删除失败（文件被进程持有）静默跳过，下次 C2 再清

### 诊断

```
hyRecallSelfCheck
-------- 临时副本目录 --------
副本数 = 12  (保留阈值 15)
总占用 = 486.2 MB
```

如果副本数长期 >> 15：
- 多半是有 Resolve handler 仍在通过 `_currentDependenciesPath` 访问某副本
- 关 CAD 后手动 `rd /s /q %TEMP%\HyCADToolRefactored` 清空

---

## §9. `_HyExec` 是特殊分支（不走命令表常规路径）

### 现象

`commands.json` 里有：

```jsonc
"_HyExec": { "type": "__PANEL_PENDING__", "method": "ConsumePendingCommand" }
```

`type` 是一个占位字符串 `__PANEL_PENDING__`，反射会找不到。`ValidateAll` 把它特殊标记为 `<特殊：面板待执行命令>` 并从 Fail 计数里排除。

### 根因

这是 **WPF 面板按"执行"按钮 → 命令行发 `_HyExec` → 从 `SettingsPanelViewModel` 取 pending `Action` 调用** 这条专门链路。常规的"查表 → 反射 Type.Method"解决不了"从 UI 取 Action"这个事，所以 `Invoke` 里对 `_HyExec` 做 `early return` 走 `InvokePanelPendingCommand`。

### 规避

- **不要**把 `_HyExec` 当普通命令替换为某个 `TypeName`
- **不要**去动它的 `type = "__PANEL_PENDING__"`（改成别的会让 ValidateAll 认为是 Type 拼错）
- SelfCheck 里 `Special=1` 就是它

---

## §10. ReCall 自身的修改必须关 CAD

### 规则

`Recall.cs` / `CommandTable.cs` / `CommandFacade.cs` 的任何修改都**不能**靠 C2 吃进去。它们属于 `ReCall.dll`，首次 NETLOAD 后永久锁在进程里。要改：

```
1. 关闭所有 AutoCAD 实例
2. dotnet build ReCall\ReCall.csproj
   (若看到 MSB3026 "文件被 AutoCAD Application 锁定" → 还有 CAD 没关)
3. 启动 AutoCAD
4. NETLOAD E:\...\ReCall\bin\Debug\ReCall.dll
5. C2
```

### 常见需要升级的场景

| 场景 | 改动位置 |
|------|---------|
| 加 / 改 `[CommandMethod]` 名 | `CommandFacade.cs` |
| 改 Invoke 流程、前置钩子、生命周期 | `Recall.cs` |
| 改命令表字典 Comparer / 解析逻辑 / Validator | `CommandTable.cs` |
| 改 `TARGET_PROJECT_NAME` / `TEMP_COPY_RETAIN` 等常量 | `Recall.cs` 顶部配置区 |

---

## §11. `AssemblyVersion` 不要在 Refactored 里升版本号

### 规则

```csharp
// HyCADTool.Refactored/Properties/AssemblyInfo.cs
[assembly: AssemblyVersion("1.0.0.0")]   // 别改
[assembly: AssemblyFileVersion("1.0.0.0")]
```

### 为什么

`Load(byte[])` 不按路径去重，但 CLR 解析依赖时**按 `AssemblyName` (name + version + culture + pkt) 匹配**。如果：

- 某次 C2 把 Refactored 升成 `1.0.1.0`
- 另一个组件（比如第三方 DLL）静态引用 `Refactored v1.0.0.0`
- CLR 解析时找不到 v1.0.0.0 → 抛 `FileNotFoundException`

因此**整个 ReCall 生态的共识是**：版本号固定在 `1.0.0.0`，靠 `Load(byte[])` 的匿名加载实现多份共存。要看"是哪一次 C2 的版本"→ 看 `hyRecallSelfCheck` 的"最近 C2 时间"或关联程序集列表里的 `Location`（空 = 已热重载）。
