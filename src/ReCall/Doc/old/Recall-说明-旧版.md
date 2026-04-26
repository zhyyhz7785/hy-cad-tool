# ReCall — 热重启说明

> 更新：2026-02

---

## 一、是什么

ReCall 是一个**常驻 AutoCAD 的轻量适配器**，解决 .NET 不能卸载程序集的问题。  
编译 → C2 → C1，不重启 AutoCAD 就能测试新代码。

---

## 二、两个命令

| 命令 | 作用 |
|---|---|
| **C2** | 把 `HyCADTool.Refactored\bin\Debug` 整体**复制到临时目录**，再用 `Assembly.Load(byte[])` 加载。每次新目录、新实例，旧锁释放，可不关 AutoCAD 重编译。 |
| **C1** | 调用 `TestCommand.Run()`，执行你在 TestCommand.cs 里配置的那一条命令。 |

---

## 三、日常开发循环

```
改代码 → Visual Studio 编译 → C2 → C1 → 重复
```

- **C2 输出示例**：`C2 完成 312ms (复制280 + 加载22 + DI10)`
- **C1 输出示例**：`INFO: 命令执行 耗时 2756 毫秒`

---

## 四、切换要测的命令

只改这一行：

```csharp
// HyCADTool.Refactored/Test/TestCommand.cs
public static void Run()
{
    new Presentation.Commands.OverKillCommand().Execute();
    // 例如：new Presentation.Commands.DrawElevationCommand().Execute();
}
```

---

## 五、可配置项（Recall.cs 配置区）

```csharp
private const string TARGET_PROJECT_NAME   = "HyCADTool.Refactored";
private const string TARGET_DLL_NAME       = "HyCADTool.Refactored.dll";
private const string BUILD_CONFIGURATION   = "Debug";
private const int    DIRECTORY_LEVELS_UP   = 3;   // Debug→bin→ReCall→根 = 3 级
private const string TEST_ENTRY_TYPE       = "HyCADTool.Refactored.Test.TestCommand";
private const string TEST_ENTRY_METHOD     = "Run";
```

通常只需改 `BUILD_CONFIGURATION`（如切 Release）或 `DIRECTORY_LEVELS_UP`（移动目录层级时）。

---

## 六、机制要点

1. **复制再加载**：源码目录不被 AutoCAD 锁定，VS 可随时编译。
2. **`Assembly.Load(byte[])`**：匿名上下文，同名程序集可多次加载，每次 C2 都拿到真正的新版本。
3. **`AssemblyResolve`**：只注册一次，运行时缺少依赖时从临时目录按名称查找。
4. **DI 初始化**：C2 通过反射调用 `ServiceLocator.Initialize()`，部分命令不依赖 DI 时静默跳过。

---

## 七、注意事项

- ReCall.cs 只改**配置区**，业务代码全部在 `HyCADTool.Refactored`。
- `[CommandMethod]` 不能放在 Refactored 里（热重载会 `eDuplicateKey`），只走 C1 反射调用。
- 每次 C2 会在 `%TEMP%\HyCADToolRefactored\` 留一个 ticks 命名子目录，积累多了可手动清理。
