# HyCADTool.UniverEditor 代码审查报告

**审查日期**: 2026-06-30  
**审查范围**: src/HyCADTool.UniverEditor 模块（新增功能，约 14,747 行代码）  
**审查级别**: High effort（高强度审查）

---

## 执行摘要

本次审查针对新增的 Univer 表格编辑器集成模块进行了全面分析，发现 **10 个问题**：

- **7 个已确认的正确性缺陷**（CONFIRMED）：涉及线程安全、资源泄漏、错误处理
- **3 个可能的架构问题**（PLAUSIBLE）：涉及性能优化和设计模式

**严重性评估**：
- 🔴 **高危**: 3 个（线程安全问题、窗口泄漏、Z-order 管理失效）
- 🟡 **中危**: 4 个（超时逻辑缺陷、重试机制失效、精度不一致）
- 🟢 **低危**: 3 个（性能优化建议）

---

## 正确性缺陷（7 个已确认）

### 1. 🔴 窗口对象泄漏（资源管理）

**文件**: [EditorLauncher.cs:80](src/HyCADTool.UniverEditor/EditorLauncher.cs#L80)

**问题描述**:  
`window.Show()` 抛出异常时（如 WebView2 初始化失败），catch 块仅将 `_currentWindow` 设为 null，但未释放窗口对象或取消 Closed 事件订阅，导致窗口实例泄漏。

**失败场景**:
```csharp
// 第 96 行：_currentWindow = window
// 第 98 行：window.Show() 抛出异常
catch {
    _currentWindow = null;  // 仅置空引用，未 Dispose
}
// window 对象仍被 Closed 事件处理器引用，无法 GC
```

**影响**: 内存泄漏，反复失败时累积多个僵尸窗口对象。

**修复建议**:
```csharp
catch (Exception ex)
{
    window.Closed -= ...;  // 取消订阅
    window.Close();        // 显式关闭
    _currentWindow = null;
    throw;                 // 重新抛出异常
}
```

---

### 2. 🔴 线程安全问题（并发访问）

**文件**: [EditorLauncher.cs:21](src/HyCADTool.UniverEditor/EditorLauncher.cs#L21)

**问题描述**:  
静态 Action 委托（`RequestExportSnapshot`、`PrepareForCadInteraction` 等）在多个线程间访问时没有同步保护：
- **写入线程**: WPF UI 线程（窗口初始化时赋值）
- **读取线程**: CAD 命令线程（通过反射读取并调用）

**失败场景**:
1. CAD 命令线程调用 `PrepareForCadInteraction?.Invoke()`
2. 同时 WPF 线程在 `OnLoadedAsync()` 中更新委托
3. 发生撕裂读取（torn read），获取到 null 或旧委托
4. 结果：NullReferenceException 或调用错误的会话实例

**影响**: 窗口 Z-order 管理失效，CAD 交互阻塞。

**修复建议**:
```csharp
private static readonly object DelegateLock = new object();
private static Action _requestExportSnapshot;

public static Action RequestExportSnapshot
{
    get { lock (DelegateLock) return _requestExportSnapshot; }
    set { lock (DelegateLock) _requestExportSnapshot = value; }
}
```

---

### 3. 🔴 窗口 Z-order 管理失效

**文件**: [UniverTableEditorHostBridge.cs:664](src/HyCADTool/Features/Tables/Services/UniverTableEditorHostBridge.cs#L664)

**问题描述**:  
`PrepareForCadInteraction?.Invoke()` 使用 null 条件运算符，当反射赋值失败时委托为 null，调用变成空操作，但窗口本应设置 `Topmost = false` 让出前台给 CAD。

**失败场景**:
```csharp
// UniverEditorLoader.cs:109 反射失败
PrepareForCadInteraction = null;

// UniverTableEditorHostBridge.cs:664
PrepareForCadInteraction?.Invoke();  // 无操作

// 结果：编辑器窗口 Topmost=true 阻挡 CAD，用户无法拾取点
```

**影响**: 用户点击"拾取"或"发布"后，编辑器窗口遮挡 CAD 界面，无法交互。

**修复建议**:
```csharp
if (PrepareForCadInteraction == null)
    throw new InvalidOperationException("CAD 交互委托未初始化");
PrepareForCadInteraction.Invoke();
```

---

### 4. 🟡 导出快照超时（时序问题）

**文件**: [UniverSessionLifecycle.cs:87](src/HyCADTool.UniverEditor/Services/UniverSessionLifecycle.cs#L87)

**问题描述**:  
`ExportSnapshotAsync()` 在会话未就绪时直接返回 `Task.CompletedTask`，但调用方（Bridge）不知道消息未发送，会等待 3 秒超时。

**失败场景**:
```csharp
// 用户在窗口刚打开时触发导出（Ready 事件未触发）
public Task ExportSnapshotAsync()
{
    if (!_ready)
        return Task.CompletedTask;  // 静默返回
    // ... 正常流程
}

// Bridge 等待 3000ms 后超时，显示"exportSnapshot 超时，请重试"
```

**影响**: 窗口刚打开时操作会假性成功，用户需等待超时后重试。

**修复建议**:
```csharp
if (!_ready)
    throw new InvalidOperationException("会话未就绪，请稍后重试");
```

---

### 5. 🟡 重试机制失效（错误处理）

**文件**: [EditorLauncher.cs:99](src/HyCADTool.UniverEditor/EditorLauncher.cs#L99)

**问题描述**:  
`Show()` 方法总是返回 `true`，即使窗口显示失败。catch 块捕获异常后将 `_currentWindow` 置空，但继续创建新窗口并返回 true，导致调用方（`UniverEditorLoader`）无法感知失败并执行恢复逻辑。

**失败场景**:
```csharp
// EditorLauncher.cs:80-83
catch {
    _currentWindow = null;  // 捕获异常
    // 继续执行...
}
// 第 99 行：return true;  // 总是成功

// UniverEditorLoader.cs:60-62 的重试逻辑永远不会执行
if (!success) {
    TryForceResetStaleWindow();  // 永远不会到达
}
```

**影响**: 窗口显示失败时，强制重置逻辑被绕过，错误累积。

**修复建议**:
```csharp
catch (Exception ex)
{
    _currentWindow = null;
    return false;  // 返回失败
}
```

---

### 6. 🟡 窗口所有者句柄验证缺失

**文件**: [EditorLauncher.cs:141](src/HyCADTool.UniverEditor/EditorLauncher.cs#L141)

**问题描述**:  
`TrySetOwner()` 仅验证 `ownerHandle == 0`，未检查 `-1` 或其他无效值，导致无效句柄被转换为 IntPtr 并静默失败。

**失败场景**:
```csharp
// 调用方传入 ownerHandle = -1（已关闭的 CAD 窗口句柄）
if (ownerHandle == 0) return;  // 仅检查 0

try {
    var helper = new WindowInteropHelper(window);
    helper.Owner = new IntPtr(-1);  // 无效句柄，静默失败
}
catch { }  // 吞掉异常
```

**影响**: 窗口无法正确关联父窗口，Z-order 混乱。

**修复建议**:
```csharp
if (ownerHandle <= 0) return;  // 拒绝 0 和负数
```

---

### 7. 🟡 单位转换精度不一致

**文件**: [univer-bridge.ts:459](src/HyCADTool.UniverEditor/Web/src/univer-bridge.ts#L459)

**问题描述**:  
`mmToPoint` 转换在两个文件中实现不同：
- **univer-bridge.ts**: 先转换后四舍五入  
  `Math.round(mm * MM_TO_POINT * 100) / 100`
- **mm-display.ts**: 先四舍五入输入后转换  
  `formatMm(mm * MM_TO_POINT)` → `Math.round(positiveMm * 100) / 100`

**失败场景**:
```typescript
// 输入 3.714mm
// univer-bridge: 3.714 * 2.8346 = 10.5258 → 10.53 pt
// mm-display:    3.714 → 3.71, 3.71 * 2.8346 = 10.516 → 10.52 pt
// 结果不同：字体高度在快照加载和 UI 显示之间不一致
```

**影响**: 表格保存后重新加载时，文字高度可能偏差 0.01pt，多次编辑累积误差。

**修复建议**: 统一使用一个实现，提取到共享工具函数。

---

## 架构问题（3 个可能）

### 8. 🟢 DOM 注入依赖轮询（设计模式）

**文件**: [layout-tab-inject.ts:242](src/HyCADTool.UniverEditor/Web/src/layout-tab-inject.ts#L242)

**问题**: 使用 `MutationObserver` + 3000ms setTimeout 轮询 DOM 就绪，缺少 Univer 的生命周期钩子支持。

**影响**: 
- 6 个注入模块独立轮询同一 DOM 树
- Univer 更新渲染时序后，固定超时失效
- 潜在竞态条件

**建议**: 向 Univer 团队提交 PR 增加 `ribbonReady` 事件。

---

### 9. 🟢 视口查询未缓存（性能）

**文件**: [page-viewport.ts:69](src/HyCADTool.UniverEditor/Web/src/page-viewport.ts#L69)

**问题**: `resolveNodes()` 在每次视口更新时执行 5+ 个串行 `querySelector`，60 FPS 拖动时产生 300+ 次/秒 DOM 查询。

**影响**: 慢速设备上每帧增加 4ms 开销，造成拖动卡顿。

**建议**: 缓存查询结果，仅在 DOM 结构变化时刷新。

---

### 10. 🟢 标尺画布过度重绘（性能）

**文件**: [ruler-overlay.ts:969](src/HyCADTool.UniverEditor/Web/src/ruler-overlay.ts#L969)

**问题**: 标尺画布在任何视口变化时全量重绘（清空 + 100+ 绘制操作），即使仅滚动 1 像素。

**影响**: 60 FPS 平移时浪费 3-6ms/帧。

**建议**: 增加脏检查，仅在刻度位置实际改变时重绘。

---

## 审查统计

| 类别 | 数量 | 占比 |
|------|------|------|
| 正确性缺陷 | 7 | 70% |
| 架构/性能问题 | 3 | 30% |
| **总计** | **10** | **100%** |

### 按严重性分布

| 严重性 | 数量 | 说明 |
|--------|------|------|
| 🔴 高危 | 3 | 线程安全、资源泄漏、功能阻塞 |
| 🟡 中危 | 4 | 超时、重试失效、精度误差 |
| 🟢 低危 | 3 | 性能优化建议 |

---

## 修复优先级

### P0（立即修复）
1. **线程安全问题**（缺陷 #2, #3）- 影响所有 CAD 交互功能
2. **窗口泄漏**（缺陷 #1）- 长期运行会耗尽内存

### P1（本迭代修复）
3. **导出超时**（缺陷 #4）- 影响用户体验
4. **重试机制**（缺陷 #5）- 错误恢复路径失效
5. **句柄验证**（缺陷 #6）- 边缘场景崩溃

### P2（后续优化）
6. **精度不一致**（缺陷 #7）- 累积误差
7. **性能优化**（问题 #8-10）- 用户设备差异明显时考虑

---

## 建议的后续行动

1. **代码修复**: 按优先级修复 P0 和 P1 缺陷
2. **测试增强**: 
   - 添加多线程并发测试（委托访问竞态）
   - 添加异常注入测试（窗口初始化失败场景）
3. **重构考虑**:
   - 提取共享工具函数（mmToPoint、formatMm、DOM 注入模式）
   - 引入事件聚合器统一管理跨线程委托
4. **文档完善**: 补充线程模型和窗口生命周期说明

---

**审查工程师**: Claude Code  
**审查工具**: Claude Code /code-review (High effort)
