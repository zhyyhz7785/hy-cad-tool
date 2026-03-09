---
name: hycad-refactored-migration-patterns
description: |
  总结 HyCADTool.Refactored 的命令迁移与架构收口模式。用于分析或继续重构 AutoCAD 插件命令、统一面板、ViewModel 命令路由、C2/C1 热重载联调、SettingsPanelViewModel 配置持久化、Domain/Infrastructure/Presentation 分层边界时。
author: Cursor Agent
version: 1.0.0
date: 2026-03-08
---
# HyCADTool.Refactored 重构模式
## Problem
在 `HyCADTool.Refactored` 中继续迁移旧 AutoCAD 命令时，容易重复踩这些坑：

- 业务逻辑直接写进命令类，导致分层失效
- WPF 面板按钮直接碰 AutoCAD API，触发线程/命令上下文问题
- 样式、Scale、钢筋参数来源不统一
- 测试入口分散，C2/C1 热重载时不清楚该改哪里
- 新模块写法与已重构模块风格不一致

## Context / Trigger Conditions
出现以下场景时优先应用本 Skill：

- 用户要求继续重构 `HyCADTool.Refactored` 下的旧命令
- 需要判断新逻辑应该放 `Domain`、`Infrastructure` 还是 `Presentation`
- 需要给统一面板增加按钮、Tab、ViewModel 命令
- 需要处理 “面板按钮如何在 AutoCAD 正确线程执行”
- 需要设置或读取 `Scale`、样式参数、钢筋参数
- 需要说明 `C2 -> C1` 联调方式或修改测试入口
- 需要评估某个模块仍处于“旧代码迁移态”还是“已完成架构收口”

## Solution
### 1. 先按当前项目真实分层落位
- `Domain`：纯数据、算法、值对象、接口；不要引用 AutoCAD API
- `Infrastructure`：AutoCAD 适配、绘图、数据库、扩展字典、选择、配置实现
- `Presentation`：命令、WPF View/ViewModel、面板编排、插件入口
- `Test`：C2/C1 热重载联调入口，不是传统单元测试

迁移顺序优先遵守：
`Domain -> Infrastructure -> Presentation`

### 2. 命令迁移遵守当前项目的“薄命令”方向
优先模式：

1. `Presentation/Commands/*Command.cs` 只做编排、选择参数、调用服务
2. 业务规则、几何算法尽量进入 `Domain`
3. AutoCAD 实体创建、写库、选择器进入 `Infrastructure`

不要为了省事把旧静态工具类整块塞进 `Presentation`。

### 3. 面板按钮不要直接执行业务，必须走命令路由
统一模式：

1. 面板 ViewModel 中设置 `PendingCommand`
2. `SaveSettings()` 先持久化
3. 用 `SendStringToExecute("_HyExec\n")` 切回 AutoCAD 命令线程
4. 在 `CommandRegistry._HyExec` 中消费 `ConsumePendingCommand()`

结论：凡是涉及 AutoCAD 交互、选择、绘图的按钮，都优先走这条路由。

### 4. 配置读取优先级要一致
当前项目有两套配置：

- `hy-settings.json`：主配置；由 `SettingsPanelViewModel` 负责；管理样式、Scale、钢筋参数
- `config.json`：辅助配置；由 `ConfigurationService` 负责；管理容差、路径、桩基等少数模块参数

实际开发时遵守：

- 大多数命令优先从 `SettingsPanelViewModel.Current` 读参数
- `Scale` 的主真相源视为 `SettingsPanelViewModel.Current.Scale`
- 新增面板参数，优先加到 `SettingsPanelViewModel`
- 底板配筋专用参数继续放 `BaseReinforcementConfig`

### 5. 样式同步不要每次全量重建
沿用当前 dirty-flag 模式：

- 面板属性变更时只标记 `_stylesDirty = true`
- 命令执行前调用 `EnsureStylesApplied()`
- 插件启动时 `PluginInitializer.InitializeStylesAndLayers()` 做一次基础初始化

不要在每个命令里重复创建文字样式、标注样式和图层。

### 6. 测试入口只认一个位置
当前联调规则：

- 切换 C1 要执行的命令，只改 `HyCADTool.Refactored/Test/TestCommand.cs`
- 完成一个新的命令/面板功能后，把 `TestCommand.Run()` 指向它，方便用户直接 `C2 -> C1`
- `TestCommand - 复制.cs` 仅作参考模板，不作为正式入口

### 7. 判断“迁移完成度”时看四件事
一个模块如果同时满足以下多数条件，可视为基本完成：

- 命令已经进入 `CommandRegistry`
- 参数来源清晰，能从 ViewModel/配置统一读取
- 业务逻辑已下沉到服务或领域对象，不再堆在命令类里
- 能通过 `C2 -> C1` 或正式命令别名稳定联调

若仍出现这些特征，说明还在过渡态：

- 命令里大量 `new XxxService()` 或静态 `XxxService.Do()`
- 逻辑主要是旧代码原样搬运
- 有 `TODO`、`NotImplementedException`
- 参数来源混杂或还依赖旧面板字段

## Verification
应用本 Skill 后，检查以下项：

- `Domain` 不引用 AutoCAD API、`Infrastructure`、`Presentation`
- 新增按钮若涉及 AutoCAD 交互，已走 `_HyExec` 路由
- 新参数能在正确配置层找到唯一来源
- `Test/TestCommand.cs` 已切到本次完成的入口
- 命令能从 `CommandRegistry` 或 `C1` 正常执行

## Notes
- 当前项目是“单程序集内部分层”，不要按严格多项目方案强行重拆
- 允许过渡期保留 `ServiceLocator` 和少量静态服务，但新增代码应优先靠接口和 DI 靠拢
- 若一次迁移超过 3 个文件或 300 行，按项目规则应主动建议分段
- 遇到 AutoCAD 相关异常时使用 `System.Exception`
