---
name: wpf-webview2-pitfalls
description: WPF + WebView2 桌面应用开发避坑指南。从 HyCADTool.MarkdownEditor 项目的真实经验中提炼，覆盖双 WebView 同步、C#↔JS 互操作、配置持久化、contenteditable 编辑等场景的已知陷阱和规避方案。适用于任何 WPF+WebView2 混合应用的开发。
---

# WPF + WebView2 桌面应用避坑指南

> **来源**：HyCADTool.MarkdownEditor 项目实战经验（2026-02）
> **适用**：任何 WPF + WebView2 混合架构桌面应用
> **目标**：AI 生成代码时主动规避已知陷阱，减少调试循环

---

## 使用方式

当 AI 在以下场景生成代码时，**必须逐条检查**对应分类的坑：

| 场景 | 检查分类 |
|------|----------|
| WebView2 初始化 / HTML 加载 | P1 运行时依赖、P3 生命周期 |
| C# ↔ JS 消息传递 | P2 双向同步、P5 回环防护 |
| contenteditable 编辑 | P6 DOM 重建、P7 光标管理、P8 有损转换 |
| 配置参数读写 | P9 契约兼容、P10 默认值、P11 持久化 |
| 多定时器 / 防抖 | P4 竞态条件 |
| 大文件 HTML 渲染 | P12 性能 |
| 异常处理 | P13 静默吞异常 |
| ViewModel 设计 | P14 God Object |
| 反射调用 / 跨进程 | P15 契约断裂 |

---

## P1 — WebView2 运行时缺失导致宿主崩溃

**现象**：用户机器没装 Edge WebView2 Runtime，点击功能按钮时宿主应用（如 AutoCAD）直接崩溃。

**根因**：WebView2 控件初始化时抛未捕获异常，WPF 窗口崩溃连带宿主进程。

**规避**：
```csharp
// 在创建 WebView2 控件前检测运行时
string version = CoreWebView2Environment.GetAvailableBrowserVersionString();
if (string.IsNullOrEmpty(version))
{
    MessageBox.Show("需要安装 Edge WebView2 运行时", "提示");
    return;
}
```

**检查清单**：
- [ ] 入口处有 WebView2 Runtime 版本检测
- [ ] 检测失败时弹友好中文提示，不抛异常
- [ ] 不依赖 try-catch 兜底（catch 时可能已经破坏宿主状态）

---

## P2 — 双 WebView 双向同步回环（无限循环）

**现象**：编辑器 A 改内容 → 通知 B → B 更新 → 通知 A → A 再改 → 无限循环，UI 冻结或内容抖动。

**根因**：两个 WebView 共享同一份数据源，一方写入触发另一方写入，形成闭环。

**规避（四层防护，缺一不可）**：

| 层 | 机制 | 作用 |
|----|------|------|
| 1 | `_isUpdatingFromPreview` 标志位 | 阻止回声触发正向刷新 |
| 2 | 内容哈希比对（SHA256） | 过滤内容相同的伪变更 |
| 3 | 版本号递增 `_markdownVersion` | 拒绝旧版本覆盖新版本 |
| 4 | 防抖定时器（220ms+） | 合并高频变更，减少回路触发次数 |

**关键代码模式**：
```csharp
// 反向同步时设置标志
_isUpdatingFromPreview = true;
_pendingSyncHash = ComputeHash(markdown);
await editorWebView.SetContent(markdown);

// 正向接收时检查标志
void OnEditorMessage(string markdown)
{
    if (_isUpdatingFromPreview && ComputeHash(markdown) == _pendingSyncHash)
    {
        _isUpdatingFromPreview = false;
        return; // 丢弃回声
    }
    // 正常处理...
}
```

**检查清单**：
- [ ] 每个方向的同步路径都有独立的回环阻断机制
- [ ] 标志位有超时兜底（防止 JS 回声不到达时永久锁死）
- [ ] 不仅检查标志位，还比对内容哈希（标志位可能被竞态清除）

---

## P3 — WebView2 生命周期陷阱

**现象**：`ExecuteScriptAsync` 在 WebView2 未初始化完成时调用，抛 `InvalidOperationException`；或在窗口关闭后调用，抛 `ObjectDisposedException`。

**根因**：WebView2 初始化是异步的，`CoreWebView2` 对象直到 `EnsureCoreWebView2Async` 完成后才可用。

**规避**：
```csharp
// 维护就绪标志
private bool _webViewReady;

async void OnWebViewInitialized(object sender, EventArgs e)
{
    _webViewReady = true;
    // 此时才能安全调用 ExecuteScriptAsync
    if (!string.IsNullOrEmpty(_pendingHtml))
        webView.NavigateToString(_pendingHtml);
}

// 所有 JS 调用前检查
async Task SafeExecuteScript(string script)
{
    if (!_webViewReady || webView.CoreWebView2 == null) return;
    try { await webView.CoreWebView2.ExecuteScriptAsync(script); }
    catch (ObjectDisposedException) { } // 窗口已关闭
}
```

**检查清单**：
- [ ] 有 `_previewReady` / `_editorReady` 就绪标志
- [ ] WebView2 未就绪时缓存操作（`_pendingPreviewHtml`）
- [ ] 所有 `ExecuteScriptAsync` 调用包裹 try-catch
- [ ] 窗口 Closing 事件中标记不可用，阻止后续 JS 调用

---

## P4 — 多定时器交叉导致竞态条件

**现象**：快速输入时内容"回弹"——刚打的字消失，恢复到几百毫秒前的状态。

**根因**：多个防抖定时器（220ms 预览刷新 / 180ms 内容通知 / 400ms 分栏重排 / 900ms CAD 同步）交叉触发，旧版本内容覆盖新版本。

**时序示例**：
```
T=0ms    用户输入 "A"
T=50ms   用户输入 "B"（180ms 通知防抖还没到期）
T=180ms  通知到期 → 发送 "AB" 到 C#
T=180ms  C# 收到 → 触发 220ms 预览刷新防抖
T=200ms  用户输入 "C"
T=400ms  预览刷新用 "AB" 版本渲染 → 覆盖图纸中的 "ABC" → 回弹！
```

**规避**：
```javascript
// 每次 updateSource 携带版本号
var contentVersion = 0;
function notifyContentChanged() {
    contentVersion++;
    var ver = contentVersion;
    // ...
    postHost({ type:'contentChanged', markdown, version: ver, hash });
}

// C# 端拒绝低版本更新
if (change.Version <= _lastPreviewContentVersion)
    return; // 旧版本，丢弃
```

**检查清单**：
- [ ] 所有内容变更消息携带递增版本号
- [ ] 接收端严格校验版本号，拒绝旧版本
- [ ] 如果面板 A 有焦点（用户正在编辑），面板 B 的定时刷新不覆盖面板 A 的内容
- [ ] 防抖定时器使用"替换"语义（`clearTimeout` + `setTimeout`），而非累加

---

## P5 — C# ↔ JS 消息协议脆弱

**现象**：JS 端 `postMessage` 的字段名改了，C# 端解析静默失败，功能默默失效。

**根因**：C# 和 JS 之间用 JSON 字符串通信，没有编译期类型检查。字段名拼写错误、类型不匹配、缺少字段都不会报编译错误。

**规避**：
```csharp
// 统一用常量定义消息类型，不散落字符串
static class PreviewMessageTypes
{
    public const string ContentChanged = "contentChanged";
    public const string PageState = "pageState";
    public const string PaperGeometry = "paperGeometry";
}

// 解析时显式检查必要字段
var type = json["type"]?.ToString();
switch (type)
{
    case PreviewMessageTypes.ContentChanged:
        var markdown = json["markdown"]?.ToString();
        if (markdown == null) break; // 防御性检查
        // ...
}
```

**检查清单**：
- [ ] 消息类型用常量定义，不硬编码字符串
- [ ] JS 和 C# 两端的字段名保持严格一致（建议在注释中写明对端位置）
- [ ] 解析失败时输出调试日志，不静默吞掉
- [ ] 新增/修改消息字段时，同步更新两端代码

---

## P6 — DOM 全量重建导致状态丢失

**现象**：配置变更（纸张大小、字号等）后，图纸面板的光标位置、滚动位置、选区全部丢失。

**根因**：`NavigateToString(html)` 销毁整个 WebView2 页面 DOM 并重建，所有浏览器侧状态（光标、滚动、选区、事件监听器）全部清零。

**规避策略（按优先级）**：

| 优先级 | 策略 | 适用场景 |
|--------|------|----------|
| 1 | 原地 JS 更新（`ExecuteScriptAsync`） | 内容变更、不影响页面结构 |
| 2 | 差量 DOM 更新（只替换变化的节点） | 部分内容变更 |
| 3 | `NavigateToString` + 书签恢复 | 配置变更、必须重建页面 |

```javascript
// 差量更新：只替换变化的块
function updateSourceDiff(newBodyHtml) {
    var temp = document.createElement('div');
    temp.innerHTML = newBodyHtml;
    var oldBlocks = Array.from(sourceRoot.children);
    var newBlocks = Array.from(temp.children);
    for (var i = 0; i < Math.max(oldBlocks.length, newBlocks.length); i++) {
        if (i >= newBlocks.length) { sourceRoot.removeChild(oldBlocks[i]); continue; }
        if (i >= oldBlocks.length) { sourceRoot.appendChild(newBlocks[i].cloneNode(true)); continue; }
        if (oldBlocks[i].outerHTML !== newBlocks[i].outerHTML)
            sourceRoot.replaceChild(newBlocks[i].cloneNode(true), oldBlocks[i]);
    }
}
```

**检查清单**：
- [ ] `NavigateToString` 仅在必要时使用（配置变更/初始加载）
- [ ] 内容变更优先走原地 JS 更新路径
- [ ] 全量重建前捕获光标 + 滚动位置
- [ ] 全量重建后恢复光标 + 滚动位置（注意 DOM 就绪时序）

---

## P7 — contenteditable 光标管理地狱

**现象**：在 contenteditable 区域编辑后，光标跳到开头/末尾/错误位置。

**根因**：浏览器 contenteditable 的 Selection/Range API 基于 DOM 节点引用。一旦 DOM 重建，旧引用失效，光标丢失。

**书签机制（从简单到可靠）**：

```javascript
// 方案 A：DOM 路径书签（依赖 DOM 结构不变）
function captureBookmark() {
    var sel = window.getSelection();
    if (!sel.rangeCount) return null;
    var range = sel.getRangeAt(0);
    return {
        path: getNodePath(range.startContainer),
        offset: range.startOffset
    };
}

// 方案 B：文本偏移书签（更抗 DOM 变化，推荐）
function captureTextBookmark(block) {
    var sel = window.getSelection();
    var range = sel.getRangeAt(0);
    var textOffset = getTextOffsetInBlock(block, range.startContainer, range.startOffset);
    return { blockIndex: block.dataset.blockIndex, textOffset };
}
```

**恢复策略（优先级递减）**：
1. 按 DOM path 精确定位 → 最精确，DOM 变了就失败
2. 按 `data-block-index` + 文本偏移 → 抗重排，推荐
3. 按栏号 + 末尾节点 → 兜底，用户感知为"光标跳到段落末尾"

**检查清单**：
- [ ] 任何触发 DOM 重建的操作前，先捕获光标书签
- [ ] 恢复时有多级回退策略，不会抛异常
- [ ] 测试场景：输入→重排→光标保持、跨栏编辑、格式化操作后

---

## P8 — Markdown ↔ HTML 往返不幂等

**现象**：在图纸面板编辑后，Markdown 源码出现格式丢失、多余空行、嵌套列表被拍平。

**根因**：`Markdown → HTML → Markdown` 的往返转换是有损的。两端使用不同解析器（Vditor 用 Lute，预览用 Markdig），产出 HTML 有微妙差异。简化的 `blockToMarkdown()` 转换器不支持所有语法。

**已知有损场景**：

| 输入 | 转换后 | 丢失 |
|------|--------|------|
| 嵌套列表（多级） | 一级列表 | 嵌套层级 |
| ```` ``` ```` 代码块 | `<pre><code>` → 反引号 | 语言标记可能丢失 |
| 连续空行 | 单空行 | 空行数量 |
| HTML 实体 `&amp;` | 双重编码 `&amp;amp;` | 原始字符 |
| 图片 alt 文本 | 可能截断 | 完整 alt |

**规避**：
```javascript
// 在 DOM 块上存储原始 Markdown
function preserveOriginalMarkdown(blocks, markdown) {
    var mdBlocks = markdown.split(/\n\n+/);
    blocks.forEach((block, i) => {
        if (i < mdBlocks.length)
            block.setAttribute('data-original-md', mdBlocks[i]);
    });
}

// 提取时：未修改的块用原始 Markdown，修改的块才做逆转换
function extractMarkdown() {
    return blocks.map(block => {
        if (!block.dataset.dirty)
            return block.dataset.originalMd || blockToMarkdown(block);
        return blockToMarkdown(block);
    }).join('\n\n');
}
```

**检查清单**：
- [ ] 双端使用相同的 Markdown 解析器，或明确记录差异
- [ ] `HTML → Markdown` 逆转换覆盖嵌套列表、代码块、表格、图片等
- [ ] 回归测试：固定 Markdown 样本 → 渲染 → 逆转换 → 比对是否一致
- [ ] 未修改的内容块保留原始 Markdown，不做无谓的往返转换

---

## P9 — JSON 契约向后兼容断裂

**现象**：旧版配置 JSON 反序列化时，新增字段为 null/默认值，旧字段被忽略。

**根因**：`EditorConfig` 新增了字段但没有默认值；或者重命名了字段但没有兼容旧名。

**规避**：
```csharp
public class EditorConfig
{
    // 新增字段：必须给默认值
    public double NewFeatureParam { get; set; } = 1.0;

    // 重命名字段：保留旧名 getter/setter 做桥接
    [Obsolete("Use NewName instead.")]
    [JsonProperty("OldName")]
    public double OldName
    {
        get => NewName;
        set => NewName = value;
    }
    public bool ShouldSerializeOldName() => false; // 不再序列化旧名
}
```

**检查清单**：
- [ ] `EditorConfig` 的每个新增字段都有合理默认值
- [ ] 重命名字段通过 `[JsonProperty("旧名")]` + `[Obsolete]` 桥接
- [ ] 用 `ShouldSerialize旧名()` 阻止旧名参与输出序列化
- [ ] 反序列化后验证关键字段不为 null（防御性编程）

---

## P10 — 默认值散落导致不一致

**现象**：`EditorConfig` 类里写了默认值 `= 2`，`EditorConfigDefaults` 里写了 `= 3`，`ViewModel` 构造函数里又写了 `= 1`，三处不一致。

**根因**：默认值没有统一来源（Single Source of Truth），散落在多个位置。

**规避**：
```csharp
// 唯一默认值来源
public static class EditorConfigDefaults
{
    public const int ColumnCount = 2;
    // ... 所有默认值集中在此
}

// 其他类引用此处，不自己定义默认值
public class EditorConfig
{
    public int ColumnCount { get; set; } = EditorConfigDefaults.ColumnCount;
}
```

**检查清单**：
- [ ] 所有默认值集中在 `EditorConfigDefaults`（或等价的单一来源）
- [ ] `EditorConfig` 属性初始化器引用 `EditorConfigDefaults.Xxx`
- [ ] `Create()` 工厂方法引用同一来源（不重复写默认值）
- [ ] 搜索代码中硬编码的魔法数字，确认是否应提取到默认值中心

---

## P11 — 配置持久化静默失败

**现象**：用户调了半天参数，关闭重开后全部恢复默认。

**根因**：`Save()` 的 `catch {}` 吞掉了所有异常（权限不足、路径不存在、磁盘满），用户无感知。

**规避**：
```csharp
public void Save(EditorConfig config)
{
    try
    {
        Directory.CreateDirectory(dir);
        string json = JsonConvert.SerializeObject(config, Formatting.Indented);
        // 先写临时文件，再原子替换（防止写到一半断电）
        string tempPath = _configFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _configFilePath, overwrite: true);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[ConfigSave] 失败: {ex.Message}");
        // 至少在调试时能看到原因
    }
}
```

**检查清单**：
- [ ] `Load()` 失败时返回 null 并在调用处回退到默认配置
- [ ] `Save()` 使用临时文件 + 原子替换，防止写入中断导致文件损坏
- [ ] `catch` 块至少输出调试日志，不完全静默
- [ ] 保存路径使用 `%LocalAppData%`（非 `%TEMP%`），持久可靠

---

## P12 — 大文件 HTML 渲染性能

**现象**：Markdown 内容超过几百行后，每次编辑都卡顿明显，预览刷新延迟数秒。

**根因**：
1. `NavigateToString` 全量重建 DOM — O(n) 全文
2. `rebuildColumnsFromSource` 清空所有栏重新分配 — O(n) 全块
3. 全量渲染时 CSS 重排 + 重绘代价大

**规避**：

| 策略 | 实现 | 效果 |
|------|------|------|
| 防抖 | 220ms+ DispatcherTimer | 合并高频变更 |
| 增量更新 | 只替换变化的 DOM 块 | 减少 DOM 操作 |
| 版本追踪 | 版本号 + 哈希跳过重复 | 避免无效刷新 |
| 懒渲染 | 仅渲染可视页 | 减少不可见区域开销 |
| CSS containment | `contain: layout paint` | 限制重排范围 |

**检查清单**：
- [ ] 编辑触发的刷新走增量路径（不走 `NavigateToString`）
- [ ] 配置变更才走全量刷新
- [ ] 增量路径有失败兜底（fallback to full refresh）
- [ ] 超长文档（1000+ 行）做过性能测试

---

## P13 — 空 catch 吞异常

**现象**：功能不工作，但没有任何错误提示，调试困难。

**根因**：代码中大量 `catch { }` 或 `catch (Exception) { }` 完全吞掉异常。

**高危位置**（本项目实际出现的）：

```csharp
// EditorLauncher.cs — 反射调用入口
try { callback?.Invoke(resultJson); }
catch { }  // ← CAD 同步失败完全无感知

// EditorConfigPersistenceService.cs — 配置保存
catch { }  // ← 持久化失败完全无感知

// VditorCacheManager.cs — 资源下载
catch { continue; }  // ← 下载失败原因不明

// EditorLauncher.cs — 非模态窗口
catch { return false; }  // ← 窗口创建失败原因不明
```

**规避**：
```csharp
// 最低要求：输出调试日志
catch (Exception ex)
{
    Debug.WriteLine($"[模块名] 操作失败: {ex.Message}");
}

// 面向用户的操作：弹提示
catch (Exception ex)
{
    MessageBox.Show($"操作失败: {ex.Message}", "错误");
}
```

**检查清单**：
- [ ] 搜索所有 `catch { }` 和 `catch (Exception) { }`
- [ ] 每个 catch 至少有 `Debug.WriteLine`
- [ ] 面向用户的操作失败应有友好提示
- [ ] 关键路径（启动、保存、同步）的异常不可静默

---

## P14 — ViewModel 膨胀为 God Object

**现象**：`EditorViewModel` 超过 1000 行，包含内容管理 + 配置参数 + UI 状态 + 命令 + 文件操作，改任何一个小功能都要在这个巨型文件里翻找。

**根因**：所有状态和逻辑都堆到一个 ViewModel 里，违反单一职责原则。

**规避（拆分策略）**：

| 子 ViewModel | 职责 | 字段数 |
|-------------|------|--------|
| EditorContentViewModel | Markdown 文本 + 文件路径 | 5~8 |
| PreviewConfigViewModel | 排版参数（栏数/字号/边距等） | 15~20 |
| UIStateViewModel | 窗口状态 + 状态栏 | 5~8 |
| 主 EditorViewModel | 聚合 + 命令 + 事件路由 | 引用上面三个 |

**检查清单**：
- [ ] 单个 ViewModel 不超过 500 行
- [ ] 内容/配置/UI 状态分属不同子 ViewModel
- [ ] 主 ViewModel 只做聚合和事件路由，不存具体字段
- [ ] 子 ViewModel 之间不互相引用（通过主 ViewModel 协调）

---

## P15 — 反射调用的契约脆弱性

**现象**：编辑器项目改了 `ShowDialog` 的参数/返回值，编译通过，但 AutoCAD 运行时调用失败，排查极其困难。

**根因**：反射调用用字符串找方法，无编译期检查。方法签名变更 = 运行时静默失败。

**规避**：
```csharp
public static class EditorLauncher
{
    /// <summary>契约版本号，主项目应校验</summary>
    public const string ContractVersion = "2.0";

    /// <summary>
    /// ⚠️ 此方法签名为跨项目反射契约，修改前必须同步更新主项目。
    /// 参数: inputJson = EditorInput JSON, ownerHandle = 父窗口 HWND
    /// 返回: EditorResult JSON
    /// </summary>
    public static string ShowDialog(string inputJson, long ownerHandle = 0) { ... }
}
```

**检查清单**：
- [ ] 反射入口方法有版本常量
- [ ] 方法签名上有注释说明"这是反射契约，不可随意改"
- [ ] 主项目调用时校验版本号
- [ ] 变更签名时在文档中明确记录 breaking change

---

## P16 — 双解析器样式不一致

**现象**：编辑器中标题/引用/代码块的样式与预览面板完全不同，用户困惑。

**根因**：编辑器用 Vditor 内置解析器（Lute）+ `content-theme` CSS，预览用 Markdig + 自定义 CSS。两套解析器 + 两套样式 = 不一致。

**差异对照**：

| 元素 | 编辑器 (Vditor) | 预览 (Markdig) |
|------|----------------|----------------|
| 解析器 | Lute (JS) | Markdig (C#) |
| 标题样式 | content-theme dark | 自定义 h1/h2/h3 |
| 代码高亮 | highlight.js + 行号 | 无高亮 |
| 主题 | 暗色 #111418 | 浅色 #ffffff |

**规避**：
- 如果目标是"编辑即所见"，预览端引入 Vditor 的 `content-theme` CSS
- 代码高亮统一使用 highlight.js
- 或者接受差异，但在 UI 上明确标注"编辑视图"和"印刷视图"

**检查清单**：
- [ ] 明确定义：两个面板的样式是否需要一致
- [ ] 如需一致，共用同一套 CSS 主题
- [ ] 代码块高亮方案统一
- [ ] 有回归测试样本验证常见元素的渲染一致性

---

## P17 — CDN 资源离线不可用

**现象**：离线环境（工厂内网、无外网的工程机）打开编辑器，Vditor 白屏。

**根因**：Vditor 资源依赖网络下载，首次使用时若无网络则无法加载。

**规避（本项目已实现的方案）**：
1. 首次使用时从 npm 镜像下载 tarball → 解压到 `%LocalAppData%`
2. 后续启动用 WebView2 虚拟主机映射（`SetVirtualHostNameToFolderMapping`）
3. 下载失败时 fallback 到 CDN（降级而非崩溃）
4. 多镜像源回退（npmmirror → npm 官方）

**检查清单**：
- [ ] 本地缓存路径用 `%LocalAppData%`（非 `%TEMP%`，重启不丢）
- [ ] 缓存完整性校验（关键文件是否存在）
- [ ] 下载用 SemaphoreSlim 防并发
- [ ] 进度回调让用户知道在下载（不是卡死）
- [ ] 所有镜像都失败时有友好提示，不白屏

---

## P18 — 窗口关闭时资源泄漏

**现象**：关闭编辑器后内存不释放，多次打开关闭后宿主应用（AutoCAD）越来越卡。

**根因**：WebView2 进程未正确释放；事件订阅未取消；定时器未停止。

**规避**：
```csharp
protected override void OnClosed(EventArgs e)
{
    // 1. 停止所有定时器
    _previewRefreshDebounceTimer?.Stop();
    _autoCadSyncDebounceTimer?.Stop();
    _configSaveDebounceTimer?.Stop();

    // 2. 取消事件订阅
    ViewModel.PropertyChanged -= OnPropChanged;

    // 3. 释放 WebView2
    EditorWebView?.Dispose();
    PreviewWebView?.Dispose();

    // 4. 释放 HwndSource
    _hwndSource?.Dispose();

    base.OnClosed(e);
}
```

**检查清单**：
- [ ] `OnClosed` 中停止所有 DispatcherTimer
- [ ] `OnClosed` 中取消所有事件订阅
- [ ] `OnClosed` 中 Dispose 所有 WebView2 实例
- [ ] 非模态窗口关闭时清理回调（`ClearSyncCallbacks`）

---

## 快速自检清单（生成代码后过一遍）

```
□ WebView2 运行时检测？
□ 双向同步有回环防护？（标志位 + 哈希 + 版本号 + 防抖）
□ ExecuteScriptAsync 前检查 WebView2 就绪？
□ 多定时器有版本号防竞态？
□ C#↔JS 消息字段用常量定义？
□ DOM 更新优先走增量路径？
□ contenteditable 操作有光标保存/恢复？
□ Markdown 往返转换有回归测试？
□ 新增配置字段有默认值？重命名字段有兼容桥接？
□ 默认值来源唯一（EditorConfigDefaults）？
□ catch 块不完全空？至少有 Debug.WriteLine？
□ ViewModel 单文件不超 500 行？
□ 反射入口有版本号和契约注释？
□ 窗口关闭时释放所有资源？
□ 离线环境有本地缓存兜底？
```

---

**版本**：v1.0 | **更新**：2026-02-21 | **来源项目**：HyCADTool.MarkdownEditor
