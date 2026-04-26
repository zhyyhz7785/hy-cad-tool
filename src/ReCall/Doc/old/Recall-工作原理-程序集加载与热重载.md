# 工作原理：程序集加载与热重载

> 2026-02

---

## 一、整体流程

```
C2 执行
  → 复制 bin\Debug 到 %TEMP%\HyCADToolRefactored\{ticks}\
  → Assembly.Load(byte[]) 加载 Refactored.dll
  → AssemblyResolve 从临时目录解析依赖
  → 反射调用 TestCommand.Run

C1 执行（若调用 MarkdownEditor）
  → EditorLoader.EnsureLoaded()
  → PreloadManagedDependencies(WebView2)   // LoadFrom
  → Assembly.Load(byte[]) 加载 MarkdownEditor.dll
  → ResolveEditorDeps 解析其余依赖（LoadFrom）
```

---

## 二、为什么用 Load(byte[])

| 方式 | 行为 | 热重载 |
|---|---|---|
| `LoadFrom(path)` | 按路径加载，CLR 会按 AssemblyName 缓存；相同 identity 再 load 时返回已加载版本 | ❌ 第二次 C2 拿不到新代码 |
| `Load(byte[])` | 匿名上下文，无 hint 路径，但不参与 identity 缓存 | ✓ 每次 C2 都是新实例 |

热重载要求每次 C2 都加载**新程序集实例**，所以主 DLL 必须用 `Load(byte[])`。

---

## 三、Load(byte[]) 的副作用

`Load(byte[])` 不提供 hint path，依赖解析时：

- CLR 先在已加载程序集中按名称查找；
- 若 AutoCAD 进程内已有旧版 WebView2，会直接复用；
- 旧版 API 签名不同 → `MissingMethodException`。

---

## 四、预加载（Preload）策略

在 `Load(byte[])` 主 DLL 之前，用 `LoadFrom` 把**易冲突的托管依赖**提前加载进 LoadFrom context：

```
1. PreloadManagedDependencies(net8Dir)
   → LoadFrom(WebView2.Wpf.dll)
   → LoadFrom(WebView2.Core.dll)

2. Assembly.Load(byte[]) 加载 MarkdownEditor.dll

3. MarkdownEditor 引用 WebView2 类型
   → CLR 在已加载程序集中找到 LoadFrom 进来的版本
   → 版本一致，无 MissingMethodException
```

---

## 五、两套 AssemblyResolve

| 来源 | 作用域 | 搜索目录 |
|---|---|---|
| **ReCall** | Refactored 及其直接依赖 | 临时目录根 + net8 子目录 + NuGet |
| **EditorLoader** | MarkdownEditor 及其依赖 | net8 目录（来自 ReCall 副本） |

EditorLoader 的 handler 在加载 MarkdownEditor 时注册，负责解析 Markdig、Newtonsoft.Json 等；WebView2 通过 Preload 已加载，一般不会触发 Resolve。

---

## 六、结论

- **主 DLL**：用 `Load(byte[])`，保证热重载。
- **易冲突依赖**（如 WebView2）：先用 `LoadFrom` 预加载，保证版本正确。
- **其余依赖**：通过 AssemblyResolve + `LoadFrom` 按需加载，保持类型一致性。
