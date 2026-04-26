# `Test/` — 开发联调与样例数据（L3）

本目录**仅在开发构建**中用于 **C1 一键入口**、可复用的耗时日志封装，以及**不依赖 AutoCAD 图面**的纯数据/断言辅助。业务命令的正式注册仍在 `ReCall/CommandFacade` + `commands.json`。

**深度**：L3（文件级：职责、关键成员、调用链）。

---

## 文件清单

| 文件 | 职责 |
|------|------|
| `TestCommand.cs` | 静态 `Run()`：C1 实际执行体；内部用 `SimpleLogger.LogElapsedTime` 包一层，**默认只改一行**切换被测目标。 |
| `TestRunner.cs` | 可选封装：`RunAllTests()` 委托给 `TestCommand.Run()`；带 `Editor` 的 `LogElapsedTime` 重载。 |
| `DesignSpecLayoutTestData.cs` | 设计说明（DesignSpec）栏宽/字体预设与期望值计算，**无** AutoCAD API，供单测或临时对照。 |

---

## `TestCommand`

### 签名与行为

```csharp
public static class TestCommand
{
    public static void Run();
}
```

| 行为 | 说明 |
|------|------|
| 首行 | 向命令行写 `[C1] v{version} @ {timestamp}`。 |
| 主体 | `SimpleLogger.LogElapsedTime("命令执行", () => { /* 只改这一行 */ });` — 项目约定见 `.cursor/rules/01-AI热启动模式.mdc`。 |
| 典型切测 | 将 `ShowPanelCommand.xxx` 等改为你正在开发的命令/面板。 |

### 调用链

1. 用户在 AutoCAD 输 **`C1`**（注册在 `ReCall.dll` 的 `ReCallClass.RunTest`）。
2. C2 已成功时：`_c1Action` 指向 `TestCommand.Run` 的反射委托；`C1` 直接 `Invoke()`。
3. 若未 C2：`RunTest` 提示「请先执行 C2 加载插件」。

**不**经过 `ReCallClass.Invoke("…")` 与 `commands.json`（C1 是独立调试旁路）。

---

## `TestRunner`

### 构造函数

- `TestRunner()`：在构造时取 `MdiActiveDocument.Editor` 存到 `_editor`；无活动文档时 `_editor` 可为 `null`，后续 `WriteMessage` 被跳过。

### 成员

| 成员 | 说明 |
|------|------|
| `void RunAllTests()` | 直接 `TestCommand.Run()`。 |
| `void LogElapsedTime(string operation, Action action)` | 与 `SimpleLogger` 同风格，用实例构造时缓存的 `Editor` 写耗时。 |
| `T LogElapsedTime<T>(string operation, Func<T> func)` | 计时 + 返回 `T`。 |

**何时用 `TestRunner` vs `TestCommand`：** 需要持有 `Editor` 引用或想非静态调用时；日常热循环仍以 **`TestCommand.Run` 一行改测**为主。

---

## `DesignSpecLayoutTestData`

**命名空间**：`HyCADTool.App.Test`；**依赖**：`Features.DesignSpec.Domain.Models` 的 `DesignSpecConfig`。

| 成员 | 参数/行为 |
|------|-----------|
| `ApplyTrueTypePreset(DesignSpecConfig config)` | 设置雅黑等 TrueType 相关字段。 |
| `ApplyShxPreset(DesignSpecConfig config)` | 设置 `tssdeng.shx` / `hztxt.shx` 等 SHX 组合。 |
| `GetExpectedColumnWidthMm(pageWidthMm, marginLeftMm, marginRightMm, columnGutterMm, columnCount, scale)` | 按页边距与分栏数算单列宽度（mm 逻辑，与 `DesignSpecConfig` 一致）。 |
| `GetExpectedCharsPerColumn(DesignSpecConfig config)` | 用列宽与字宽估计每列字符数。 |
| `BuildAsciiLine` / `BuildCjkLine` / 其他 | 生成定长测试行（详见源码）。 |

**用途**：单测、Markdown→MText 排版算法对照；**不**在 `PluginInitializer` 中自动执行。

---

## 相关约定

- **换测目标**：只改 `TestCommand.Run` 内一行；**不要**为换测去改 `ReCall` / `TestRunner` 的注册方式（除非排障约定）。
- **父级说明**：`../README.md`  
- **C1/C2 语义**：`../../../../doc/01-启动流程与调用链-2026-04-26-185800.md`
