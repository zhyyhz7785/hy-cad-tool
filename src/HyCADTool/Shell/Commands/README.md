# Shell / Commands

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**命令基础设施与面板入口（无热重载 `[CommandMethod]` 重复注册的那一类）**：统一把 UI/Ribbon/菜单的意图送到 AutoCAD 命令行（`CommandDispatcher`），并维护可检索的命令目录（`CommandCatalog`）。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| `CommandDispatcher.Send`、`CommandCatalog`、 `ShowPanelCommand` | 业务 `Execute()` 体、Jig → `Features/<切片>/Commands` |
| 调试/诊断类命令（`TestOffsetCommand` 等） | 生产功能命令 |

## 目录结构

- `CommandDispatcher.cs`：`SendStringToExecute` 包装，键与 `commands.json` 一致。
- `CommandCatalog.cs`、 `CommandListDumpCommand.cs`：命令表与转储（排查用）。
- `ShowPanelCommand.cs`：`Hy`/`HyB` 等面板入口，调 `PanelManager`。
- `LicenseActivationCommand.cs` 等：壳层与许可相关命令类。
- `Base/`：通用 `ICommand`、`CommandResult`、`CommandExecutor`（见子目录 README）。

## 命令与入口

- **面板**：`ShowPanelCommand` 静态方法，由 `TestCommand`、Ribbon 等调用，**不**在 Refactored 主流程中为本类再加 `[CommandMethod]`。
- **业务键**：`CommandDispatcher.Send("gj")` 等，由 ReCall 已注册的 `CommandMethod` 接收执行。

## 依赖与协作

- **与 ReCall**：`CommandFacade` 与 `commands.json` 是真实注册源；本目录只负责「再次发送同一字符串」以复用该管道。
- **与 Features**：`Send` 的 key 与 Feature 内命令类名无编译期强绑定，改名键须同步 `commands.json`。

## 开发与审查要点

- [ ] 新按钮优先 `CommandDispatcher.Send(key)`，避免 Shell 里反射 `Type.GetType` 调 Feature。
- [ ] `ShowPanelCommand` 内勿引入业务事务或 Database 直写。
