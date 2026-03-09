---
name: hycad-multidoc-panel-resource-init
description: |
  解决 HyCADTool.Refactored 在 AutoCAD 多文档场景下，统一 HY 面板切换到新图纸后参数可见但样式/图层未初始化、命令首次执行异常或明显卡顿的问题。适用于 PaletteSet + SettingsPanelViewModel + hy-settings.json + CommandRegistry / PluginInitializer 联动场景。
author: Cursor Agent
version: 1.0.0
date: 2026-03-09
---
# HyCAD 多文档面板资源初始化
## Problem
`HyCADTool.Refactored` 的统一 `HY` 面板虽然已经是全局单例，但在 AutoCAD 多文档环境下，常见问题是：

- 面板切到新文档后，参数显示正常，但该文档缺少样式或图层
- `gj`、`gb`、`gb1` 等命令在新图纸第一次执行时报错或表现异常
- 每次命令执行前都重复触发样式重建，导致二次执行明显卡顿

## Context / Trigger Conditions
出现以下现象时优先应用本 Skill：

- 用户说“面板只对当前文档有效”
- 切换到新 DWG 后，第一次运行命令才发现资源没初始化
- `PluginInitializer.OnDocumentActivated()` / `OnDocumentCreated()` 为空实现
- `PanelManager` 已经按文档切换 `DataContext`，但底层资源未随文档切换补齐
- `LoadSettings()` 每次执行后都会把 `_stylesDirty` 强制设为 `true`

## Solution
### 1. 把“面板切换文档”和“文档资源初始化”分成两层
- `PanelManager` 负责 `PaletteSet` 与各 Tab 的 `DataContext` 切换
- `PluginInitializer` 负责文档级资源初始化：样式、图层、必要配置同步

不要把资源初始化逻辑塞进 WPF 面板事件里。

### 2. 在插件入口注册文档事件，并在首次切入文档时补资源
推荐模式：

1. `Initialize()` 时先为当前文档执行一次初始化
2. 订阅 `DocumentActivated` 与 `DocumentCreated`
3. 在 `DocumentActivated` 中调用类似 `EnsureCurrentDocumentResourcesInitialized(force: false)`
4. 用 `HashSet<string>` 记录已初始化文档名，避免反复重跑

结论：新图纸第一次切入当前文档时，要自动补齐资源，而不是等命令报错后再被动修。

### 3. 资源初始化要复用当前面板参数
初始化时优先这样取参数：

1. `SettingsPanelViewModel.GetOrCreate(documentName, styleService)`
2. `vm.LoadSettings()`
3. `vm.EnsureStylesApplied()`
4. `layerService.CreateMultipleLayers(...)`

这样可以保证：

- 每个新文档都沿用 `hy-settings.json` 中最近保存的主配置
- 样式名称、Scale、钢筋参数来源统一
- 命令与面板看到的是同一套配置

### 4. 样式 dirty 标记不能在每次加载配置后无脑置脏
错误模式：

- `CommandRegistry.Run()` 每次命令先 `LoadSettings()`
- `LoadSettings()` 末尾直接 `_stylesDirty = true`
- 所有命令都会无意义地再跑一次 `EnsureStylesApplied()`

推荐修复：

- 在 `LoadSettings()` 前后构造“样式签名”
- 只有样式相关字段真的变化时，才把 `_stylesDirty` 设为 `true`

这能明显改善 `gj`、`gb`、`gb1` 等重复执行前的等待感。

### 5. 新增钢筋出图参数时，优先挂到 `SettingsPanelViewModel`
例如“钢筋绘制宽度”这类参数：

- UI：加到 `HyToolPanel.xaml` 的钢筋参数区
- 状态：加到 `SettingsPanelViewModel`
- 持久化：同步到 `SettingsData`
- 出图：在 `ReinParameters` 或直接命令读取后乘以 `Scale`

不要继续把这种主面板参数散落到命令内部常量里。

## Verification
应用本 Skill 后，至少验证：

- `C2 -> C1` 打开面板后，切换到新文档仍能正常使用
- 新 DWG 第一次执行 `gj` / `gb` / `gb1` 不再缺样式或缺图层
- 来回切换已初始化文档，不会每次重复跑整套创建逻辑
- 重复执行命令时，不再因为样式重建出现明显额外停顿

## Notes
- `PanelManager` 解决的是“UI 跟随文档”，不是“文档资源已就绪”
- `PluginInitializer` 更适合放 AutoCAD 文档生命周期相关逻辑
- 文档初始化缓存通常按文档名即可；若后续发现重名图纸场景，再考虑改成更稳的键
