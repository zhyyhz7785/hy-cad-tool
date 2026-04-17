# ReCall — 2 分钟上手（v2）

> 版本：2026-04（v2 命令表架构）
> 代码入口：`ReCall/Recall.cs`、`ReCall/CommandFacade.cs`、`ReCall/commands.json`
> 旧架构文档（v1 / RouteThroughC1 时代）归档在 [old-v1/](./old-v1/)

---

## 一、它解决什么

.NET 加载进程的程序集**不能卸载**。AutoCAD 一旦 `NETLOAD` 一个 DLL，这个文件就被锁死，不关 AutoCAD 改不了。

**ReCall 是一个常驻适配器**：
- 自己永不变，一次性 `NETLOAD`。
- 把**真正要改的插件**（`HyCADTool.Refactored.dll`）复制到 `%TEMP%`，用 `Assembly.Load(byte[])` 匿名加载，反射调用业务类。
- 结果：**改业务代码 → VS 编译 → 输 C2 → 新代码生效**，全程不关 AutoCAD。

---

## 二、命令总览

| 分类 | 命令 | 数量 | 出处 |
|------|------|------|------|
| **ReCall 自带（底座）** | `C2` / `C1` / `hyRecallSelfCheck` | 3 | `Recall.cs` |
| **业务命令（转发）** | `hy` / `gj` / `hyRoadA` / `HYDCEL` ... | 80+ | `CommandFacade.cs` 的 `[CommandMethod]` → `ReCallClass.Invoke(key)` |
| **占位符（预留槽）** | `N1` ~ `N50` | 50 | `CommandFacade.cs` + `commands.json` |

### 三条底座命令

| 命令 | 作用 | 何时用 |
|------|------|--------|
| **C2** | 热加载 `HyCADTool.Refactored.dll`，反射执行 `PluginInitializer.Initialize`，刷新命令表 | 每次 VS 编译后 |
| **C1** | 临时调试：调用最新 `TestCommand.Run()` | 想临时跑一段自定义调试代码时（日常几乎不用） |
| **hyRecallSelfCheck** | 打印 ReCall / Refactored / 命令表 / 临时副本 / 关联程序集的诊断快照 | 任何异常时一键排查 |

---

## 三、三种改动 → 三种流程

| 改动场景 | 操作步骤 | 关 CAD？ |
|---------|---------|---------|
| **A. 改业务代码**（绝大多数）<br>改 `RoadAlignmentCommand.Execute` / `DimensionAlignCommand.Execute` 等 | VS 编译 → 输 `C2` | **否** |
| **B. 改映射**（已有 key 指向别的类，或给占位符 N1~N50 赋值）<br>只改 `commands.json` | 编辑 JSON 保存 → 下次该命令被输入时自动重载（按 mtime） | **否** |
| **C. 改命令名**（新 `[CommandMethod]`）<br>改 `CommandFacade.cs` | 关 CAD → `dotnet build ReCall` → 启动 CAD → `NETLOAD ReCall.dll` | **是** |

> **关键洞见**：场景 C 是最痛的，但 **v2 用 N1~N50 占位符绕过了它** —— 新命令测试阶段就用 N1；功能稳定、要发版本了才关一次 CAD，批量把 N1~N5 重命名为 `hyNewFeature1` 等。

---

## 四、日常循环（最频繁的场景 A）

```
改 HyCADTool.Refactored 的某个 *Command.Execute
  → Ctrl+Shift+B
    → AutoCAD 命令行：C2
      → 命令行：对应业务命令（hy / gj / hyRoadA ...）
        → 观察结果 → 回到第一步
```

**C2 典型输出**：

```
C2 #3 完成 451ms (复制63 + 加载31 + Terminate57 + Initialize278 + 表0)
  命令表：81 条已配置 + 50 条占位符，解析失败 0 条
```

---

## 五、占位符 N1~N50 套路（场景 C 的救急通道）

**痛点**：想测一个全新命令 `hyFoo`，但加 `[CommandMethod("hyFoo")]` 就得关一次 CAD，**心智负担重**。

**解法**：`CommandFacade.cs` 和 `commands.json` 里已经预留了 50 个占位符 `N1` ~ `N50`。测试期间：

```jsonc
// commands.json 改 N1 的映射，保存即可
"N1": { "type": "HyCADTool.Refactored.Presentation.Commands.FooCommand", "method": "Execute" }
```

AutoCAD 命令行输 `N1` → 下次自动重载 JSON → 反射调 `FooCommand.Execute`。

调通后**合并进真命名**：把 N1 改回 `null`，在 `CommandFacade.cs` 加 `[CommandMethod("hyFoo")] public void Cmd_hyFoo() => ReCallClass.Invoke("hyFoo");`，在 `commands.json` 加 `"hyFoo": {...}`，然后**一次性**关 CAD 重 `NETLOAD ReCall.dll`。这一次关 CAD 可以把多条新命令一起合入。

---

## 六、首次环境准备

**每次启动 AutoCAD 只做一件事**：

```
NETLOAD E:\...\ReCall\bin\Debug\ReCall.dll
```

然后就可以：

```
C2           ← 首次加载 Refactored
<业务命令>    ← 日常使用
hyRecallSelfCheck   ← 出问题时诊断
```

**Refactored.dll 从不单独 NETLOAD**——它由 ReCall 的 C2 流程复制到 `%TEMP%` 并匿名加载。

---

## 七、何时必须关 CAD

| 触发条件 | 为什么 |
|---------|--------|
| 改 `Recall.cs` / `CommandTable.cs` / `CommandFacade.cs` | ReCall 自己不热重载自己（锚点） |
| 新增一个 `[CommandMethod]` 想直接输命令名就跑 | AutoCAD 命令表只在 `NETLOAD` 时扫一次 |
| 改 `AutofacModule` 的 DI 注册规则 | `ServiceLocator.Container` 首次 C2 后不重建 |
| 升级托管依赖版本（WebView2 / Newtonsoft 等） | 老 Assembly 引用会指向老版本类型 |

其余情况（业务代码、映射、配置文件）都不需要关 CAD。

---

## 八、进一步阅读

- 机制细节（C2 6 步、Invoke 5 步、commands.json 格式、Load(byte[]) 原理）：[Recall-工作原理.md](./Recall-工作原理.md)
- 已知陷阱（含近期修的大小写 bug、双初始化 bug）：[Recall-陷阱与问题.md](./Recall-陷阱与问题.md)
- 手工验证场景（16 条）+ SelfCheck 字段解读：[Recall-验证清单.md](./Recall-验证清单.md)
- v1 旧架构文档（RouteThroughC1 / CommandRelayStore 时代）：[old-v1/](./old-v1/)
