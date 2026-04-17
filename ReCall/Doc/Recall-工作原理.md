# Recall 工作原理

> 版本：2026-04
>
> 读完这篇你应该能回答：
> 1. C2 到底发生了什么？
> 2. 为什么是 `Load(byte[])` 而不是 `LoadFrom`？
> 3. C1 为什么能跑到最新代码？
> 4. 新命令 `hyRoadAlnStation` 是怎么在不重启 CAD 的情况下执行的？

---

## 一、角色定位

```
┌────────────────────┐         ┌──────────────────────────┐
│   ReCall.dll       │         │  HyCADTool.Refactored.dll│
│   "不动的底座"     │ 反射→→→ │  "要反复改的插件本体"    │
│   NETLOAD 一次     │         │  C2 动态重载             │
│                    │         │                          │
│  [CommandMethod]:  │         │  业务类型、命令类、      │
│   C2               │         │  TestCommand.Run()       │
│   C1               │         │                          │
│   hyRecallSelfCheck│         │                          │
└────────────────────┘         └──────────────────────────┘
```

ReCall 自己**不做业务**，也**不会热重载自己**。只有两种入口：
- AutoCAD 命令表（`C2` / `C1` / `hyRecallSelfCheck`），首次 NETLOAD 注册
- 静态字段 `_c1Action`，存着最新一次 C2 反射绑定的委托

---

## 二、C2 的 8 步流程

```
1. 解析解决方案根 (ReCall.dll → ../../../ 三级回跳)
   ↓
2. 检查 HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll 是否存在
   ↓
3. CleanupOldTempCopies(15)  — 清理 %TEMP%\HyCADToolRefactored\ 旧副本
   ↓
4. 复制整个 bin\Debug 到 %TEMP%\HyCADToolRefactored\<ticks>\
   ↓
5. 首次 C2：注册 AssemblyResolve + UnhandledException handler
   ↓
6. Assembly.Load(File.ReadAllBytes(副本主 DLL))  — 匿名加载
   ↓
7. 反射调用 ServiceLocator.Initialize(new ContainerBuilder().RegisterModule(new AutofacModule()).Build())
   ↓
8. 反射取 TestCommand.Run 静态方法 → 绑定为 _c1Action
```

**关键观察**：只有第 3~8 步会每次执行，第 5 步的 handler 只注册**一次**，但 handler 内部读的是静态字段 `_currentDependenciesPath`，它在第 4 步之后被覆盖为新副本路径——**实现了"handler 注册一次，依赖目录每次刷新"**。

---

## 三、为什么是 `Load(byte[])`

### 两种 API 的 CLR 行为对比

| API | 加载上下文 | identity 缓存 | 锁定源文件 |
|---|---|---|---|
| `Assembly.LoadFrom(path)` | Load-From context | **有**（按 `AssemblyName` 去重） | **是**（持有 file handle） |
| `Assembly.Load(byte[])` | No-context (anonymous) | **无** | **否**（已读入字节数组） |

### AssemblyInfo.cs 里固定了版本号

```csharp
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

如果用 `LoadFrom`：第一次 C2 加载 v1.0.0.0，第二次 C2 试图加载**也叫 v1.0.0.0** 的新字节流，CLR 直接复用旧实例 → **热重载失败**。

用 `Load(byte[])`：每次都是匿名的新 Assembly 对象，类型系统里存在多份同名同版本类型共存，`_c1Action` 指向最新那份。

### 副作用

`Load(byte[])` 加载的程序集：

```csharp
Assembly asm = Assembly.Load(File.ReadAllBytes(path));
Console.WriteLine(asm.Location);  // 空字符串 ""
```

→ `hyRecallSelfCheck` 用 `string.IsNullOrEmpty(Location)` 判定"已热重载"。业务代码里也千万别用 `typeof(T).Assembly.Location` 去定位资源（见陷阱文档）。

---

## 四、C1 的命令路由（三层 dispatch）

C1 入口是 `ReCall.RunTest()`，它只做一件事：`_c1Action.Invoke()`。而 `_c1Action` 是最新 Refactored 里的 `TestCommand.Run`。关键逻辑全在 `TestCommand.Run` 里：

```
┌─────────── C1 → TestCommand.Run ────────────┐
│                                             │
│  ① SettingsPanelViewModel.ConsumePending?   │
│      ↓ 找到待执行动作                       │
│      → 执行 → return                        │
│                                             │
│  ② CommandRelayStore.TryConsume(out key)?   │
│      ↓ 从 %TEMP%\...command-relay.json 读   │
│      → TryDispatchCommandKey(key)           │
│         ↓ switch 分派到命令类               │
│         → Execute → return                  │
│                                             │
│  ③ Editor.GetString("输入命令 key")         │
│      ↓ 用户手工输入                         │
│      → TryDispatchCommandKey(key)           │
│      → Execute → return                     │
│                                             │
│  ④ fallback: new DimensionAlignCommand...   │
│                                             │
└─────────────────────────────────────────────┘
```

每条路径都指向**同一个 `TryDispatchCommandKey`**，分派表是 C2 热重载的——所以新增命令只需要：

1. 写命令类 `XxxCommand.Execute()`
2. 在 `TryDispatchCommandKey` switch 里加一个 case
3. C2 → C1 → 输入 key

完全不用碰 `[CommandMethod]`，也不用重启 CAD。

---

## 五、RouteThroughC1 —— 老命令如何吃新代码

对于**首次 NETLOAD 时就存在**的 `[CommandMethod]`（如 `HYROADA`），在 AutoCAD 命令表里它的方法指针**永远指向第一份 Refactored.dll 的方法**。C2 换了新 DLL，命令表不重扫，旧方法仍然活着。

解决方法：让旧方法**不干正事**，而是**转发到 C1**。

`CommandRegistry.cs` 里的通用模板：

```csharp
[CommandMethod("hyRoadA")]
public void Cmd_hyRoadA() => RouteThroughC1("hyRoadA", () => new RoadAlignmentCommand().Execute());

// helper
private static void RouteThroughC1(string key, Action fallback)
{
    try
    {
        CommandRelayStore.Save(key);                                // 写 %TEMP%\...relay.json
        Application.DocumentManager.MdiActiveDocument
                   .SendStringToExecute("C1 ", true, false, true);  // 触发 C1
    }
    catch { fallback(); }                                           // C1 失败时本地兜底
}
```

流程：

```
用户输 HYROADA
  → 老 CommandRegistry.Cmd_hyRoadA (旧代码)
    → CommandRelayStore.Save("hyRoadA")
      → SendStringToExecute("C1 ")
        → ReCall.C1 → _c1Action (最新 TestCommand.Run)
          → 读 relay → TryDispatchCommandKey("hyRoadA")
            → new RoadAlignmentCommand().Execute()  ← 最新代码
```

**代价**：老 DLL 的 `Cmd_xxx` 方法体永远不跑业务，永远只 relay。所以"升级 RouteThroughC1 模板"需要一次关 CAD + 重新 NETLOAD（让命令表里的方法指针绑定新模板），之后就稳定了。

---

## 六、AssemblyResolve —— 依赖怎么找到的

`Load(byte[])` 没有 hint path，一旦 Refactored 里引用 `Autofac.dll` / `Newtonsoft.Json.dll` 之类，CLR 触发 `AppDomain.CurrentDomain.AssemblyResolve` 事件。ReCall 在首次 C2 注册唯一的 handler：

```csharp
if (!_assemblyResolveRegistered)
{
    _assemblyResolveHandler = CurrentDomain_AssemblyResolve;
    AppDomain.CurrentDomain.AssemblyResolve += _assemblyResolveHandler;
    _assemblyResolveRegistered = true;
}
```

handler 搜索顺序（`ResolveAssembly`）：

```
1. <副本目录>\<Name>.dll
    ↓ 未找到
2. <副本目录>\net8\<Name>.dll   ← MarkdownEditor 等 net8 依赖走这里
    ↓ 未找到
3. %USERPROFILE%\.nuget\packages\<Name>\...\<Name>.dll  (递归)
    ↓ 未找到
4. return null  → CLR 抛 FileNotFoundException
```

依赖也一律用 `Assembly.Load(File.ReadAllBytes(path))`：防止在 AppDomain 里留下 "LoadFrom context" 的旧版本占位。

---

## 七、ServiceLocator 初始化

Refactored 内部用 Autofac。ReCall 在 C2 反射完成一次：

```csharp
if (ServiceLocator.Container == null)
{
    var builder = new ContainerBuilder();
    builder.RegisterModule(new AutofacModule());
    ServiceLocator.Initialize(builder.Build());
}
```

**注意**：首次 C2 之后再 C2，ReCall 会**跳过**初始化（`Container != null`）。这意味着：

- 老 Autofac 容器里注册的单例**不会**被替换成新类型
- 多数情况无碍（接口签名不变），但如果修改了 `AutofacModule` 的注册规则，**必须关 CAD 重启**

---

## 八、WebView2 特例 —— 为什么 MarkdownEditor 需要 Preload

`Load(byte[])` 没有 hint path 的代价之一：CLR 看到 `Microsoft.Web.WebView2.Wpf` 的请求时，会先在 AppDomain 里扫"已加载程序集"，如果 AutoCAD 进程里已有旧版 WebView2（AutoCAD 自己内置），就直接复用 → 方法签名不匹配抛 `MissingMethodException`。

解决：`EditorLoader`（在 Refactored 里）在加载 MarkdownEditor.dll 之前，**先 `LoadFrom` 指定版本的 WebView2**：

```csharp
PreloadManagedDependencies(_net8Dir);           // LoadFrom WebView2.Wpf.dll, WebView2.Core.dll
var asm = Assembly.Load(File.ReadAllBytes(dllPath));   // 此时 CLR 已认 LoadFrom 进来的版本
```

之后 MarkdownEditor 的类型解析就会命中 LoadFrom 的那份，不再绑定 AutoCAD 进程里那个旧版。**这是托管依赖版本冲突的通用套路**：Preload with LoadFrom → Main with Load(byte[])。

---

## 九、一张图总结

```
┌──────────────────────────────────────────────────────────────┐
│ AutoCAD 进程                                                 │
│                                                              │
│  命令表 (静态，首次 NETLOAD 建立)                           │
│    C2 ──────► ReCall.Reload()                               │
│    C1 ──────► ReCall.RunTest() ──► _c1Action.Invoke()      │
│    hyRecallSelfCheck ──► SelfCheck()                         │
│    HYROADA ──► CommandRegistry.Cmd_hyRoadA (首版)           │
│                  │                                           │
│                  └─► RouteThroughC1 ─► SendStringToExecute C1│
│                                                              │
│  静态字段                                                   │
│    _c1Action                  ← 每次 C2 重新绑定            │
│    _currentDependenciesPath   ← 每次 C2 重新赋值            │
│    _assemblyResolveRegistered ← 只设 true 一次              │
│                                                              │
│  AppDomain 程序集列表                                       │
│    ReCall v1.0.0.0      [LoadFrom]  永不变                  │
│    Refactored v1.0.0.0  [byte[]]   ◄─ N 份共存，最新在顶  │
│    Refactored v1.0.0.0  [byte[]]                            │
│    Refactored v1.0.0.0  [byte[]]                            │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```
