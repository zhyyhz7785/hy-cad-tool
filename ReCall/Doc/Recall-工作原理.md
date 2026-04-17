# ReCall 工作原理（v2）

> 版本：2026-04
>
> 读完这篇你应该能回答：
> 1. ReCall 里的 `[CommandMethod]` 是怎么通过 `commands.json` 反射到 Refactored 的？
> 2. `C2` 到底发生了什么？
> 3. 为什么是 `Load(byte[])` 而不是 `LoadFrom`？
> 4. `commands.json` 改了为什么立即生效，但 `CommandFacade.cs` 改了却要关 CAD？
> 5. `PluginInitializer` 为什么不再继承 `IExtensionApplication`？

---

## 一、角色定位

```
┌──────────────────────────────────────────────────────────────┐
│ AutoCAD 进程                                                 │
│                                                              │
│  ReCall.dll                                                  │ ← NETLOAD 一次，永不变
│   ├─ [CommandMethod("C2")]                Reload()           │   ReCall 自带
│   ├─ [CommandMethod("C1")]                RunTest()          │   3 条底座命令
│   ├─ [CommandMethod("hyRecallSelfCheck")] SelfCheck()        │
│   │                                                          │
│   └─ CommandFacade                                           │ ← 80+ 业务 [CommandMethod]
│        ├─ [CommandMethod("hy")]     Cmd_hy()  ──┐            │   每个都只做一件事：
│        ├─ [CommandMethod("gj")]     Cmd_gj()  ──┤            │   ReCallClass.Invoke(key)
│        ├─ ... 80+ 条                            ├──► Invoke(key)
│        └─ [CommandMethod("N1")]..[CommandMethod("N50")]──┤   │
│                                                 │            │
│                                                 ▼            │
│                                  ┌──────────────────────┐    │
│                                  │ commands.json         │   │ ← 文件型映射表
│                                  │  key → type / method  │   │   按 mtime 自动重载
│                                  │  + 可选 ctor / enum    │   │
│                                  └──────────────────────┘    │
│                                                 │            │
│                                                 ▼            │
│  Refactored.dll                       Activator.CreateInstance│ ← Load(byte[]) 进 AppDomain
│   ├─ PluginInitializer.Initialize     + MethodInfo.Invoke    │   C2 每次刷新，N 份共存
│   ├─ RoadAlignmentCommand                                    │
│   ├─ DimensionAlignCommand                                   │
│   └─ ... 所有业务命令类（不再有 [CommandMethod]）            │
└──────────────────────────────────────────────────────────────┘
```

**关键分层**：

- **AutoCAD 命令表层**：只接触 `ReCall.dll` 的 `[CommandMethod]`（首次 `NETLOAD` 建立后永不刷新）
- **映射层**：`commands.json` 文件，按 `mtime` 按需重载（改 JSON 不用关 CAD）
- **执行层**：`Refactored.dll` 里的命令类，由 C2 热重载（`Load(byte[])`）

---

## 二、C2 的 6 步流程

`Recall.cs::Reload()` 的核心时序：

```
1. 定位 Refactored.dll 源路径
     ↓ ReCall.dll 所在目录 向上跳 DIRECTORY_LEVELS_UP=3 层 到解决方案根
     ↓ 拼 HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll
2. CleanupOldTempCopies(TEMP_COPY_RETAIN = 15)
     ↓ 按 LastWriteTime 保留最新 15 份副本，其余 rm -rf
3. 复制 bin\Debug 整个目录到 %TEMP%\HyCADToolRefactored\<ticks>\
     ↓ 一次复制所有依赖 DLL；副本目录路径存入 _currentDependenciesPath
4. Assembly.Load(File.ReadAllBytes(副本主 DLL))
     ↓ 匿名加载（Location = ""）
     ↓ 首次 C2 时注册 AssemblyResolve + UnhandledException handler
5. 反射：旧 PluginInitializer.Terminate → 新 PluginInitializer.Initialize
     ↓ Terminate 解绑文档事件、Reset Autofac 容器、保存面板设置
     ↓ Initialize 重建容器 + 订阅文档事件 + 初始化样式/图层/道路子系统
6. CommandTable.ForceReload + ValidateAll
     ↓ 读 commands.json
     ↓ 对每条 entry 反射校验 Type/Method 可解析
     ↓ 打印 "OK=N  Special=1  Placeholder=50  Fail=0"
```

**性能参考**（典型值）：

```
C2 #3 完成 451ms (复制63 + 加载31 + Terminate57 + Initialize278 + 表0)
```

`Initialize` 是大头（Autofac + 文档事件 + 道路子系统启动），但 **278ms 对开发循环来说已经接近"秒级"**。

---

## 三、Invoke(key) 的 5 步调用链

`Recall.cs::Invoke(string key)` 是所有 80+ 业务命令的公共入口：

```
1. 检查 _refactoredAssembly 是否已加载
     ↓ 未加载 → "✗ Refactored 尚未加载，请先执行 C2"
2. 特殊键分支 _HyExec
     ↓ == "_HyExec" → 从 SettingsPanelViewModel.ConsumePendingCommand 取 Action 直接调
     ↓ 这是面板按钮的专用路径（面板按下"执行"按钮 → AutoCAD 命令行发 _HyExec）
3. CommandTable.Contains(key) ?
     ↓ 不存在 → "✗ 命令表中未定义键 xxx"
     ↓ 是占位符 (entry == null) → "✗ 占位符尚未分配"
4. 前置钩子 InvokePreHooks
     ↓ 反射调 SettingsPanelViewModel 的 EnsureCurrentDocumentContext / SyncStyles / ... 三件套
     ↓ 确保命令执行前，图层/样式/设置 已同步到当前文档
5. 根据 CommandEntry 反射执行
     ↓ var t = refactored.GetType(entry.Type)
     ↓ var inst = Activator.CreateInstance(t, ResolveCtorArgs(entry.Ctor, entry.CtorEnumTypes))
     ↓ t.GetMethod(entry.Method).Invoke(inst, null)
```

**关键**：步骤 4 的前置钩子替代了 v1 时代每个命令类自己 `SettingsPanelViewModel.Current.Sync(...)`，集中在 ReCall 做一次，更不容易漏。

---

## 四、commands.json 格式

最小映射：

```jsonc
{
    "version": 1,
    "commands": {
        "hy": { "type": "HyCADTool.Refactored.Presentation.Commands.ShowPanelCommand",
                "method": "ShowHyToolPanel" },
        "gj": { "type": "HyCADTool.Refactored.Presentation.Commands.DrawReinforcementCommand",
                "method": "Execute" }
    }
}
```

带构造参数（bool、int、string 等原生类型直接写字面量）：

```jsonc
"g1": { "type": "...ReinAddAnchorCommand", "method": "Execute", "ctor": [false] },
"g2": { "type": "...ReinAddAnchorCommand", "method": "Execute", "ctor": [true]  }
```

带**枚举**构造参数（enum 值写**字符串**，对应位置声明枚举类型**全名**）：

```jsonc
"gb":  { "type": "...MleaderReinCommand", "method": "Execute",
         "ctor": ["Standard"],
         "ctorEnumTypes": ["...MleaderReinCommand+Mode"] },
"gb1": { "type": "...MleaderReinCommand", "method": "Execute",
         "ctor": ["Single"],
         "ctorEnumTypes": ["...MleaderReinCommand+Mode"] }
```

ReCall 的 `ResolveCtorArgs` 会按 `ctorEnumTypes[i]` 把 `ctor[i]` 字符串 `Enum.Parse` 成枚举；未声明枚举类型的位置按 `ctor[i]` 的 JSON 字面类型原样传。

**占位符**（未使用的槽）写 `null`：

```jsonc
"N1": null, "N2": null, ..., "N50": null
```

---

## 五、CommandTable 按 mtime 自动重载

`CommandTable.EnsureLoaded()` 每次被 `Invoke` 间接调用时：

```csharp
var mtime = File.GetLastWriteTimeUtc(path);
if (_lastLoadUtc != DateTime.MinValue && mtime == _lastFileMtimeUtc) return;  // 命中缓存
LoadFrom(path); _lastFileMtimeUtc = mtime; _lastLoadUtc = DateTime.UtcNow;
```

**效果**：编辑 `commands.json` 保存 → 文件 `mtime` 变 → 下一次任意业务命令被输入时自动重读。**完全不用关 CAD、不用 C2**。

> 字典 `Comparer` 用 `StringComparer.OrdinalIgnoreCase`（2026-04 修复），所以 `commands.json` 写 `"Hy"`、`CommandFacade` 里写 `Invoke("hy")`、用户输 `HY` —— 三套大小写都能匹配到同一条。

---

## 六、为什么是 `Load(byte[])` 而不是 `LoadFrom`

### 两种 API 的 CLR 行为对比

| API | 加载上下文 | identity 缓存 | 锁定源文件 |
|---|---|---|---|
| `Assembly.LoadFrom(path)` | Load-From context | **有**（按 `AssemblyName` 去重） | **是**（持有 file handle） |
| `Assembly.Load(byte[])` | No-context (anonymous) | **无** | **否**（已读入字节数组） |

### AssemblyInfo 里版本号固定

```csharp
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

用 `LoadFrom`：第一次 C2 加载 `v1.0.0.0`，第二次 C2 试图加载**也叫 `v1.0.0.0`** 的新字节流 → CLR 直接复用旧实例 → **热重载失败**。

用 `Load(byte[])`：每次匿名加载一个新 Assembly 对象，类型系统里存在多份同名同版本类型共存。ReCall 里 `_refactoredAssembly` 静态字段**每次 C2 都被覆盖为最新那份**，所以 `Invoke` 查的总是最新代码。

### 副作用：`Location` 空字符串

```csharp
Assembly asm = Assembly.Load(File.ReadAllBytes(path));
Console.WriteLine(asm.Location);  // ""
```

`hyRecallSelfCheck` 用 `string.IsNullOrEmpty(Location)` 判断"已热重载 / 未热重载"。业务代码**不能**用 `typeof(T).Assembly.Location` 定位资源（见陷阱 §6）。

---

## 七、AutoCAD 命令表只扫描一次 → 命令注册集中到 ReCall.CommandFacade

AutoCAD 有个硬规则：**只有 `NETLOAD` 一个全新路径的 DLL 时才扫描 `[CommandMethod]` 并注册到命令表**。`Load(byte[])` 不触发这个扫描。

v1 时代 Refactored 被 `NETLOAD` 进来，每次 C2 后 `HYROADA` 命令仍可输入，但其方法指针**永远指向第一份 Refactored 的方法**。为了让老命令吃新代码，v1 发明了 `RouteThroughC1` / `CommandRelayStore` —— 老方法体改成"写 relay 文件 + `SendStringToExecute C1`"，让 C1 再反射到最新版本。

**v2 彻底拆掉了这个中继链**：所有 `[CommandMethod]` 都搬到 `ReCall.CommandFacade`，每个都只做 `Invoke(key)` 转发。`CommandFacade` 方法指针永远指向 `ReCall.dll` 里的实现（ReCall 永不变），而 `Invoke` 内部查 `commands.json` → 反射 `_refactoredAssembly`（每次 C2 最新）→ 调用目标方法。

**收益**：

- 不再需要 `RouteThroughC1` / `CommandRelayStore`
- 新增"改业务代码"流程 = VS 编译 + C2，零关 CAD
- 新增"改映射"流程 = 只改 JSON，连 C2 都不用
- 只有"新增命令名"这唯一一种改动还要关 CAD

---

## 八、PluginInitializer 的生命周期 + 为什么不 IExtensionApplication

v2 里 `PluginInitializer` 是普通类，不继承 `IExtensionApplication`。生命周期由 ReCall 显式管理：

```
C2 #1:  InvokeLegacyTerminate() → (no-op, _lastPluginInitInstance == null)
        InvokePluginInitialize() → new PluginInitializer(); Initialize(); _lastPluginInitInstance = 保存

C2 #2:  InvokeLegacyTerminate() → 旧实例.Terminate()（解绑事件、Reset 容器）
        InvokePluginInitialize() → new PluginInitializer(); Initialize(); _lastPluginInitInstance = 保存
```

**为什么不继承 `IExtensionApplication`？**

AutoCAD 内部监听 `AppDomain.AssemblyLoad` 事件，即使是 `Assembly.Load(byte[])` 加载的程序集，AutoCAD 也会扫描它里面的 `IExtensionApplication` 实现并**自动调** `Initialize` / `Terminate`。结果就是：

```
ReCall 反射调一次   + AutoCAD 自动调一次  =  两套 "插件初始化中..." 日志
```

去掉接口后 AutoCAD 的自动路径就消失了，生命周期完全由 ReCall 控制，符合 v2 "所有时序都集中在一处" 的设计。

---

## 九、AssemblyResolve（简述）

`Load(byte[])` 没有 hint path，Refactored 引用 `Autofac.dll` / `Newtonsoft.Json.dll` 时 CLR 会触发 `AppDomain.AssemblyResolve`。ReCall 首次 C2 注册唯一 handler：

```csharp
if (!_assemblyResolveRegistered) {
    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    _assemblyResolveRegistered = true;
}
```

handler 查找顺序：

```
1. <当前副本目录>\<Name>.dll                           ← _currentDependenciesPath（每次 C2 覆盖）
2. <当前副本目录>\net8\<Name>.dll                      ← MarkdownEditor 等 net8 依赖
3. %USERPROFILE%\.nuget\packages\<Name>\...\<Name>.dll  （递归）
4. null → CLR 抛 FileNotFoundException
```

依赖也用 `Assembly.Load(File.ReadAllBytes(path))`：防止在 AppDomain 里留下 "LoadFrom context" 的旧版本占位。

---

## 十、ServiceLocator / Autofac（简述）

`PluginInitializer.Initialize` 里建 Autofac Container 并注入 `ServiceLocator.Container`。**每次 C2 通过 Terminate Reset + Initialize 重建**，所以业务类能拿到最新接口实现。

**例外**：如果修改了 `AutofacModule` 的**注册规则**（加接口、换实现），老 Assembly 里残留对象仍持有旧实例引用。稳妥起见 **改 DI 规则要关 CAD 重启**。多数时候只改命令逻辑、数据结构，Container 重建一次就够了。

---

## 十一、一张图总结

```
┌──────────────────────────────────────────────────────────────┐
│ AutoCAD 进程                                                 │
│                                                              │
│  命令表 (首次 NETLOAD 建立，永不刷新)                        │
│    C2                 ──► ReCall.Reload()                   │
│    C1                 ──► ReCall.RunTest()                  │
│    hyRecallSelfCheck  ──► ReCall.SelfCheck()                │
│    hy / gj / ... 80+  ──► CommandFacade.Cmd_xxx()           │
│                              │                               │
│                              └──► ReCallClass.Invoke(key)   │
│                                      │                       │
│     ┌───────────────────────────────┘                       │
│     ▼                                                        │
│  CommandTable (按 mtime 自动重载 commands.json)             │
│     │                                                        │
│     ▼                                                        │
│  Activator.CreateInstance(Type, ctorArgs)                   │
│  + MethodInfo.Invoke(instance, null)                        │
│                                                              │
│  AppDomain 程序集列表                                        │
│    ReCall v1.0.0.0     [LoadFrom]  永不变                   │
│    Refactored v1.0.0.0 [byte[]]   ◄─ C2 每次一份，最新在顶  │
│    Refactored v1.0.0.0 [byte[]]                             │
│    Refactored v1.0.0.0 [byte[]]                             │
│                                                              │
│  静态字段 (ReCall 内)                                        │
│    _refactoredAssembly        ← 每次 C2 重新赋值             │
│    _lastPluginInitInstance    ← 每次 C2 重新赋值             │
│    _currentDependenciesPath   ← 每次 C2 重新赋值             │
│    _assemblyResolveRegistered ← 只设 true 一次              │
└──────────────────────────────────────────────────────────────┘
```
