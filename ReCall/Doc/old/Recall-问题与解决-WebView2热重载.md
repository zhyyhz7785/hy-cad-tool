# 问题与解决：WebView2 热重载冲突

> 2026-02

---

## 一、现象

C1 打开 MarkdownEditor 时弹出：

```text
Vditor 编辑器加载失败：Method not found:
'System.Threading.Tasks.Task Microsoft.Web.WebView2.Wpf.WebView2.EnsureCoreWebView2Async(Microsoft.Web.WebView2.Core.CoreWebView2Environment)'
请确认已安装 WebView2 Runtime。
```

---

## 二、根因

为支持热重载，EditorLoader 把 `Assembly.LoadFrom(dllPath)` 改成了 `Assembly.Load(File.ReadAllBytes(dllPath))`：

- **LoadFrom**：会把 DLL 所在目录设成依赖搜索 hint，CLR 会优先在 net8 目录加载 WebView2，版本正确。
- **Load(byte[])**：没有 hint，CLR 会先看 AppDomain 里是否已有同名程序集；AutoCAD 进程里已有旧版 WebView2，就直接复用，导致方法签名不匹配，出现 `MissingMethodException`。

---

## 三、解决方式

在 `Load(byte[])` 加载 MarkdownEditor 之前，先用 `LoadFrom` 预加载 WebView2 托管 DLL，让它们进入 LoadFrom context：

```csharp
// EditorLoader.TryLoadFromBaseDir
PreloadManagedDependencies(_net8Dir);   // 新增：LoadFrom WebView2.Wpf + WebView2.Core
var asm = Assembly.Load(File.ReadAllBytes(dllPath));
```

`PreloadManagedDependencies` 对 `Microsoft.Web.WebView2.Wpf.dll` 和 `Microsoft.Web.WebView2.Core.dll` 调用 `Assembly.LoadFrom(path)`。

---

## 四、结果

- MarkdownEditor 继续用 `Load(byte[])` → 热重载生效。
- WebView2 通过 `LoadFrom` 预加载 → CLR 使用 net8 目录中的版本，不再绑定 AutoCAD 里的旧版。

---

## 五、涉及文件

| 文件 | 修改 |
|---|---|
| `EditorLoader.cs` | 新增 `PreloadManagedDependencies`，并在加载 MarkdownEditor 前调用 |
| `ResolveEditorDeps` | 依赖解析从 `Load(byte[])` 改回 `LoadFrom`，保证类型一致性 |
