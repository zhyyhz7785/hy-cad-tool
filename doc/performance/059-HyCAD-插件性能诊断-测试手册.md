# HyCAD 插件性能诊断 · 测试手册（Layer 1–6）

本文档配合仓库内已实现项使用：

- **Layer 4**：`PluginInitializer.Initialize` 结束前的 `⏱ PluginInitializer 阶段耗时(ms): ...` 行（每次 C2 / 自动加载后输出）。
- **Layer 5**：设置环境变量 `HYCAD_PERF_INVOKE=1` 后，`ReCallClass.Invoke` 在命令行输出 `[HyCAD perf] Invoke key=...`。
- **Layer 3**：环境变量 `HYCAD_RECALL_NO_AUTO_RELOAD=1` 或 `ReCallExtension.DisableAutoReloadForDiagnostics = true`（需自行在调试启动代码中设置）→ 仅 NETLOAD ReCall 后不自动 C2。

---

## 测试前固定条件

| 项目 | 要求 |
|------|------|
| AutoCAD 版本 | 与日常一致（如 2025） |
| 图纸 | **两份**：① 空白 `acad.dwt` ② 常用工程 DWG |
| DPI / 分辨率 | 全程同一组 |
| 杀毒 | 测试时段可关实时扫描（减少 `Assembly.Load` / 临时目录干扰） |

---

## 标准动作脚本 A–I（每层同一套、手机秒表记墙钟）

| 编号 | 动作 | 经验目标 |
|------|------|----------|
| A | 双击 AutoCAD → 首帧可点 | ≤ 8s（因机而异） |
| B | `NEW` 空白图 → 命令行可输 | ≤ 1s |
| C | 输 `hy` → 统一面板首帧 | ≤ 0.8s |
| D | 面板点一按钮 → 命令行有回响 | ≤ 0.3s |
| E | `LINE` → 第一点 → 拖鼠标看 jig | 目测顺滑（~60FPS） |
| F | 中键平移 | 同上 |
| G | 滚轮缩放 10 次 | 即时 |
| H | 开第二图后 `Ctrl+Tab` 切换 | ≤ 0.5s（第二轮起） |
| I | 空闲 10s，任务管理器中 acad CPU | ≈ 0% |

---

## Layer 1：环境噪声隔离

1. 结束进程：Cursor、Visual Studio、Chrome、Excel、微信、Typora、v2rayN、Notepad++ 等（任务管理器确认）。
2. 记录任务管理器：总 CPU、内存、磁盘。
3. 加载日常插件配置，跑 **A–I**，填 [060-结果汇总模板](060-HyCAD-插件性能-结果汇总模板.md)。
4. 与「全开后台」数据对比：若 **H、I、E/F** 明显改善 → 环境噪声为主因之一。

---

## Layer 2：AutoCAD 基线（无插件）

1. 临时重命名 **ApplicationPlugins** 下的 HyCAD bundle 目录（或整包 `bundle`），使 AutoCAD **不自动加载**任何 .NET 插件。
2. 启动 AutoCAD，**不要** `NETLOAD ReCall.dll`。
3. 跑 **A、B、E、F、G、H、I**（跳过 C、D）。
4. 若 **E/F** 仍卡 → 倾向图纸 / 显卡 / AutoCAD 自身，与 HyCAD 无关。

---

## Layer 3：插件分层（A1 → A4）

**关闭自动 C2**（二选一）：

- 启动 AutoCAD **前**在 PowerShell：`$env:HYCAD_RECALL_NO_AUTO_RELOAD = "1"`，再启动 acad.exe；或
- 用户级：`setx HYCAD_RECALL_NO_AUTO_RELOAD 1`（新开 CAD 进程生效）。

| 梯度 | 操作 | 解读 |
|------|------|------|
| A1 | `NETLOAD ReCall.dll`，**不**输 `C2` | ReCall 壳成本 |
| A2 | A1 后手动 `C2`（看命令行 C2 分段 ms + PluginInitializer 阶段行） | `Initialize` 总成本 |
| A3 | A2 后 `hy` 开面板 | 首开面板 + WPF 树 |
| A4 | A3 后点一业务按钮 | `Invoke` + 业务 |

**Invoke 细项**：设 `HYCAD_PERF_INVOKE=1` 后再测 A4，查看 `preHooks` vs `reflect+invoke`。

---

## Layer 4：初始化阶段（自动化）

### 4.1 ReCall 侧（`C2` 命令）

每次 `C2` 会输出（默认开启，可 `HYCAD_RECALL_C2_PERF=0` 仅保留摘要）：

- 首行：`── C2 开始  HH:mm:ss.fff local ──`
- 多行：每段带 **本机绝对时间戳** ` [HH:mm:ss.fff]`、**本段毫秒**、**累加毫秒**（清理 / 复制 / 预载伙伴 / 读盘 / `Assembly.Load` / `Terminate` / `Initialize` / 绑 C1 / `commands.json` / `ValidateAll`）
- `C2 摘要(毫秒)`：一行汇总各段；`占比(近似)`：`PluginInitializer` vs 其余
- 旧式一行：`C2 #N 完成 …` 仍保留便于复制

**慢点判读**：若 **json+校验** 很大 → 命令表条目过多或反射冷；若 **Init** 很大 → 看下面 4.2 的 Refactored 细分。

### 4.2 Refactored `PluginInitializer`

C2 完成后命令行查找：

```text
⏱ PluginInitializer 阶段耗时(ms): WpfStub=... WpfTraps=... Warmup=... Autofac+SL=... ...
```

将整行复制到汇总表。通常 **Warmup**、**RibbonMenus**、**Autofac+SL** 为 Top 候选。

---

## Layer 5：场景计时

| 场景 | 方法 |
|------|------|
| 面板按钮 | `HYCAD_PERF_INVOKE=1`，点按钮，看 `[HyCAD perf] Invoke key=_HyExec ...` 或业务 key |
| 多文档 | 开 5 图，`Ctrl+Tab` 循环 20 次，秒表记单次切换（第二轮起应 < 50ms 量级） |
| JIG | `LINE` 拖动（**不经过** HyCAD 命令表）— 若卡则非插件主因 |
| 平移/缩放 | 录屏或目测 FPS |

---

## Layer 6：常驻开销（PerfView + 任务管理器）

1. 下载 **PerfView**（微软），以管理员运行。
2. `Collect` → `Run Command` 留空，或手动 `Start Collection` 约 **30s**：此间 AutoCAD **空闲**（不点命令）。
3. 停止后打开 `CPU Stacks`，过滤进程 `acad` / `AcCore` / `HyCAD` 相关栈；关注 **GC Heap Net Mem**、**JIT**。
4. 并行观察任务管理器：**工作集**是否持续上涨、CPU 是否 > 5%。

---

## 相关代码路径

| 项 | 文件 |
|----|------|
| 自动 C2 开关 | [ReCall/Recall.cs](../../ReCall/Recall.cs) `ReCallExtension` |
| C2 分段计时 | [ReCall/Recall.cs](../../ReCall/Recall.cs) `ReCallClass.Reload` |
| Initialize 阶段计时 | [HyCADTool.Refactored/Presentation/PluginInitializer.cs](../../HyCADTool.Refactored/Presentation/PluginInitializer.cs) |
| Invoke 计时 | [ReCall/Recall.cs](../../ReCall/Recall.cs) `ReCallClass.Invoke` |

---

## 环境变量速查

| 变量 | 值 | 作用 |
|------|-----|------|
| `HYCAD_RECALL_NO_AUTO_RELOAD` | `1` | 禁止首个 Idle 自动 C2 |
| `HYCAD_RECALL_C2_PERF` | `0` / `off` | 关闭 C2 分段时间戳行（仍输出摘要+完成行） |
| `HYCAD_PERF_INVOKE` | `1` | 输出每次 Invoke 耗时 |

（需在启动 AutoCAD 的进程环境中设置。）
