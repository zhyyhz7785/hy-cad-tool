---
name: hycad-autocad-singleton-database-context
description: |
  解决 HyCADTool.Refactored 在 AutoCAD 多文档场景下，单例服务缓存旧 Database / Document 导致 Transaction.GetObject 抛出 eNotFromThisDocument 的问题。适用于 LayerManager、Renderer、Marker/Layer/Style 等持久服务与当前活动文档事务混用场景。
author: Cursor Agent
version: 1.0.0
date: 2026-03-15
---
# HyCAD AutoCAD 单例服务文档上下文
## Problem
在 `HyCADTool.Refactored` 中，一些 AutoCAD 适配服务被注册为单例后，如果在构造函数里缓存了 `Application.DocumentManager.MdiActiveDocument` 或其 `Database`，切换图纸后继续复用该服务，容易出现：

- `eNotFromThisDocument`
- `Transaction.GetObject(ObjectId, OpenMode)` 在图层、块表、字典等位置报错
- 命令看起来在“当前图纸”执行，但底层仍拿着旧文档数据库

## Context / Trigger Conditions
出现以下特征时优先应用本 Skill：

- 服务通过 Autofac 注册为 `SingleInstance()`
- 服务构造函数里保存了 `_database = Application.DocumentManager.MdiActiveDocument.Database`
- 命令在选择、计算阶段正常，直到渲染/写库阶段才报错
- 堆栈落在 `LayerManager`、Renderer、Style/Layer 初始化或 `GetObject` 上
- 错误只在切换 DWG、多文档联调、C2/C1 热重载后更容易复现

## Solution
### 1. 不要在单例 AutoCAD 服务里固化文档上下文
错误模式：

- 构造时缓存 `Document`
- 构造时缓存 `Database`
- 后续每次调用都复用这份旧上下文

推荐模式：

1. 服务尽量做成“无状态”
2. 在每次方法调用时重新解析当前 `Document/Database`
3. 若确实要支持显式数据库，保留构造注入作为兜底，但运行时优先取当前活动文档

### 2. 事务与数据库必须来自同一文档
像 `tr.GetObject(database.LayerTableId, ...)` 这类调用里：

- `tr` 来自哪个文档，就必须配套该文档的 `database`
- 不要把“旧数据库的 ObjectId”传给“当前事务”

经验上，最稳的做法是：

1. 进入方法时先解析当前文档数据库
2. 再基于该数据库取 `LayerTableId`、`BlockTableId` 等符号表对象
3. 统一在同一个事务内完成读写

### 3. 单例注册不一定要改，先修上下文获取
如果服务本身没有状态，通常不用急着把 `SingleInstance()` 改成 `InstancePerDependency()`。

优先修：

- 上下文获取时机
- `Database/ObjectId/Transaction` 的一致性

只有当服务内部确实持有文档级状态时，再考虑调整生命周期。

## Verification
应用本 Skill 后，至少验证：

- 切换到新 DWG 后执行命令不再报 `eNotFromThisDocument`
- `LayerManager` / Renderer 在当前文档正常创建图层与实体
- 同一命令在连续多次 `C2 -> C1` 联调下保持稳定
- 旧文档与新文档来回切换后，渲染结果仍落在当前图纸

## Notes
- 这类问题本质上不是“图层不存在”，而是“事务和数据库不属于同一文档”
- AutoCAD 多文档环境下，`MdiActiveDocument` 是运行时上下文，不适合作为单例字段的长期真相源
- 对 `LayerManager`、`DCELRenderer`、Marker/Style 类服务尤其要警惕这个坑
