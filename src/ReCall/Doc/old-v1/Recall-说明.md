# ReCall — 2 分钟上手

> 版本：2026-04（代码：`ReCall/Recall.cs`，目标：`HyCADTool.Refactored`）

---

## 一、它解决什么

.NET 加载进程的程序集**不能卸载**。AutoCAD 一旦 `NETLOAD` 了 `HyCADTool.Refactored.dll`，这个文件就被锁死，不关 AutoCAD 就改不了。

ReCall 是一个**常驻适配器**：自己永不变，把**真正要改的插件**复制到临时目录、匿名加载、反射调用，做到**不关 AutoCAD 也能吃新代码**。

---

## 二、三条命令

| 命令 | 作用 | 何时用 |
|---|---|---|
| **C2** | 重新加载 `HyCADTool.Refactored.dll`，绑定 C1 入口到最新 `TestCommand.Run` | 每次 Visual Studio 编译后 |
| **C1** | 调用最新一次 C2 绑定的 `TestCommand.Run()` | 每次要运行命令时 |
| **hyRecallSelfCheck** | 打印当前 ReCall / Refactored / Temp 副本 / 依赖程序集的诊断快照 | C2/C1 出问题时一键排查 |

> 三条命令都挂在 ReCall 身上，**只要 ReCall.dll NETLOAD 过一次就永久可用**。

---

## 三、日常循环

```
改 HyCADTool.Refactored 代码
  → Visual Studio Ctrl+Shift+B
    → AutoCAD 输入 C2
      → C1 触发 TestCommand.Run
        → 观察结果 → 回到第一步
```

**C2 正常输出**：

```
C2 #3 完成 412ms (复制280 + 加载110 + DI22)
```

**C1 正常输出**：

```
[C1] v1.0.0.0 @ 2026-04-16 14:23:11.507
INFO: 命令执行 耗时 2756 毫秒
```

---

## 四、切换要测的命令

只改这一行（在 `HyCADTool.Refactored/Test/TestCommand.cs` 最底部）：

```csharp
// fallback 分支
new DimensionAlignCommand().Execute();
// 例：new SettlementCalculationCommand().Execute();
```

或者保持 fallback 不动，**运行时临时指派**：`C1` → 命令行提示输入 key → 输入 `hyRoadA` / `hyjc` / `gg` 等（见 `TryDispatchCommandKey` 分派表）。

---

## 五、首次环境准备

AutoCAD 里只做一次：

```
NETLOAD E:\...\ReCall\bin\Debug\ReCall.dll
NETLOAD E:\...\HyCADTool.Refactored\bin\Debug\HyCADTool.Refactored.dll
```

之后只用 C2/C1 循环，**永远不再手动 NETLOAD Refactored**。

> 当你添加了**新的 `[CommandMethod]` 命令**（例如本仓库的 `hyRoadAlnStation`），AutoCAD 不会在 C2 时扫描到它，**直接输命令名会"未知命令"**。用 `C1` → 输入命令 key 这条路运行即可，详见 [Recall-陷阱与问题.md](./Recall-陷阱与问题.md) §1。

---

## 六、配置项（仅在更换目标项目时改）

`Recall.cs` 顶部的配置区：

```csharp
private const string TARGET_PROJECT_NAME = "HyCADTool.Refactored";
private const string TARGET_DLL_NAME     = "HyCADTool.Refactored.dll";
private const string BUILD_CONFIGURATION = "Debug";
private const int    DIRECTORY_LEVELS_UP = 3;   // Debug→bin→ReCall→根
private const string TEST_ENTRY_TYPE     = "HyCADTool.Refactored.Test.TestCommand";
private const string TEST_ENTRY_METHOD   = "Run";
private const int    TEMP_COPY_RETAIN    = 15;  // 临时副本保留数
private const string TEMP_BASE_NAME      = "HyCADToolRefactored";
```

---

## 七、进一步阅读

- 机制细节：[Recall-工作原理.md](./Recall-工作原理.md)
- 已知陷阱：[Recall-陷阱与问题.md](./Recall-陷阱与问题.md)
- 手工验证：[Recall-验证清单.md](./Recall-验证清单.md)
