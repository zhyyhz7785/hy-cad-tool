# Shell / Commands / Base

> **文档深度**: L3 · **总纲**: [`doc/00-新的开始-2026-04-26-175400.md`](../../../../../doc/00-新的开始-2026-04-26-175400.md) · **父索引**: [`../README.md`](../README.md)

## 定位（对照总纲 §三）

**命令抽象与执行结果** 的小内核：`ICommand`、`CommandResult`、统一执行器，供壳层与部分调用方复用，避免与 WPF 的 `ICommand` 概念混淆时可在注释中写清。

## 职责边界

| 属于本目录 | 不属于本目录 |
|------------|----------------|
| 接口/结果 DTO/薄执行器 | 具体 `gj`、`hyab` 等业务命令实现 |

## 目录结构

- `ICommand.cs`
- `CommandResult.cs`
- `CommandExecutor.cs`

## 命令与入口

无独立命令键；被 `Shell` 内其他类型或未来扩展引用。

## 依赖与协作

- 与 `Features` 命令类命名并列时，注意命名空间区分（`HyCADTool.Shell.Commands.Base`）。

## 开发与审查要点

- [ ] 改接口视为破坏性变更，全局搜索实现者再动。
- [ ] 不在此目录增加对 `Features.*.Domain` 的引用。
