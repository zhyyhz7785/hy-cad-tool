---
name: hycad-refactored-migration-patterns
description: |
  总结 HyCADTool.Refactored 的命令迁移与架构收口模式。用于分析或继续重构 AutoCAD 插件命令、统一面板、
  ViewModel 命令路由、C2/C1 热重载联调、SettingsPanelViewModel 配置持久化、Domain/Infrastructure/Presentation
  分层边界；以及新增「拾取 → PI 编辑 → 定稿」型单面板工作流（rLaw 工作台 v2 模式：三按钮 + 两层预览 +
  LayerLockScope 锁定图层写入 + 静态 SegmentRenderer + XData KIND 分家）时直接套用。
author: Cursor Agent
version: 1.2.0
date: 2026-04-21
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

---

## rLaw 工作台 v2：单面板三按钮工作流（2026-04-21 简化）

> 记录「路线工作台」(`RoadAlignmentWorkbenchPanel.xaml`) 的工作流 v2 决策。v1 的 DispatcherTimer 500ms debounce + 自动 DesignPreview 彩色机制已下线，分段彩色纯手动触发。后续接手本面板、或仿照模式实现"拾取 → 编辑 → 定稿"类面板时直接套用。

### 核心三阶段

**A. 拾取（一次性写入「档案」）** — `PickAlignmentCmd`
1. 用户在 CAD 中 pick 任意 Polyline
2. `RoadAlignmentUserPickRegisterCommand` 内部分叉：
   - **已标 `HY_ROAD`（`IsAlignmentKind = true`）** → 直接复用 `Alignment`，不动 DWG 几何，面板选中进入编辑
   - **未标 HY_ROAD** → 登记为 `AlignmentSourceKind.UserPicked` → 在同一 transaction 内 `Erase()` 用户原图 → commit 后：
     - `RoadAlignmentRawPolylineService.DrawForAlignment` 在 `05_hy_道路_原线`（**启动即锁定**）写 `KindAlignmentRawPick`（252 本色） — 内部 `LayerLockScope.Unlock` 临时解锁
3. `RoadJsonExportService.SaveForDocument` 落 `.roaddesign.json`

**B. PI 调整（仅瞬态）** — 面板事件驱动，**无** DispatcherTimer
1. `PiEditorViewModel` 属性变化事件触发 `OnPiEditorPreviewRequested`
2. 画瞬态黄线（`RoadAlignmentPreviewService`，`TransientManager`）
3. 就这样 — 不再自动往任何图层落彩色；用户需要分段彩色在 DWG 对照就点顶栏「预览」（见阶段 D）

**C. 应用（唯一"保存"动作）** — `ApplyAlignmentCmd` → `RoadAlignmentApplyService.Apply`
1. `AlignmentPiDesigner.Build` 用当前 PI 重建几何
2. 查 `05_hy_道路_平面线位` 有无同 `AlignmentId` 的 `KindAlignment` Polyline：
   - 有 → `RoadGeometryBridge.UpdateAutoCadPolyline` 就地更新（ObjectId 不变，保护下游标注）
   - 无 → 新建并写 `HY_ROAD` / `KindAlignment` XData
3. 更新 `Alignment.Centerline / Source = PiTable`
4. **`LayerLockScope.Unlock` 临时解锁 `05_hy_道路_原线`** → 擦本 Id 的 `KindAlignmentRawPick` + 历史 `KindAlignmentDesignPreview` 残留 → Dispose 恢复锁定
5. `LivePreview`（`05_hy_道路_预览`）保留不动 — 用户自管
6. 落 JSON + publish `AlignmentChangedEvent`（Created/Updated 区分首次 vs 后续）

**D. 手动快照通道（独立）** — `DrawLivePreviewCmd`
- 用户主动点「预览」→ 写 `05_hy_道路_预览` 层 `KindAlignmentLivePreview`
- `eraseExistingSameId: false` → 每点一次**追加一组**新段（用户留多方案并排比对）
- **Apply 不擦它**；用户自管清理（命令行 `hyRoadAlnRawHide` 或手动 Erase）

### 新加「锁定图层 + KIND 分家」面板必须遵守的 7 条
1. **KIND 穷举白名单**：新增 KIND 立即加入 `HyRoadXdata.IsAlignmentKind()`（或同类 `IsXxxKind()` 方法），下游擦除服务只扫白名单
2. **EraseByLayer 禁止作为公开 API**：所有擦除必须经 `EraseByKind` / `EraseByKindAndId`
3. **渲染下沉到静态 Renderer**：多 KIND 共享段渲染的必须抽 `XxxSegmentRenderer.Draw(targetLayer, xdataKind, eraseExistingSameId)` 三元组，避免各服务重复拼 ACI 映射
4. **事务边界**：`Erase 老实体`与`Create 新实体`放在同一 transaction；跨服务组合（Erase → Commit → Draw）要保证 Draw 服务自己 LockDocument + 自己 Transaction，不要跨服务共享 tr
5. **锁定图层写入**：所有向 `IsLocked = true` 图层写 / 擦实体的事务块必须包裹 `LayerLockScope.Unlock(tr, db, layerName)`；**不要**在命令里直接 `layer.IsLocked = false` 不复原
6. **图层注册清单同步**：新增 `HyRoadLayers.XxxLayer` 常量 → 必须同步更新 `PluginInitializer.GetRequiredLayers()`；有锁定 / 冻结 / 线型特殊属性走 `HyRoadLayerInitializer.EnsureXxx(doc)` 独立方法
7. **UI 语义显式标注 Tooltip**：每个按钮 Tooltip 必须写清"目标图层 / KIND / 是否持久化 / 是否被其它按钮自动擦除"四要素；本项目已在 `RoadAlignmentWorkbenchPanel.xaml` 顶栏三连给出标准写法

### 锁定图层写入约定 —— `LayerLockScope` 模板

`05_hy_道路_原线` 启动即锁定防用户误改，工作台内部写入 / 擦除须走如下模板（`HyCADTool.Refactored\Infrastructure\AutoCAD\Services\Road\LayerLockScope.cs`）：

```csharp
var db = doc.Database;
using (doc.LockDocument())
using (var tr = db.TransactionManager.StartTransaction())
{
    using (LayerLockScope.Unlock(tr, db, HyRoadLayers.RawPolylineLayer))
    {
        // 事务期内该层临时解锁；Append / Erase / 改 XData 都合法
        EraseForAlignmentInternal(tr, db, aln.Id);

        var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
        var pl = RoadGeometryBridge.ToAutoCadPolyline(poly);
        pl.Layer = HyRoadLayers.RawPolylineLayer;
        pl.Color = Color.FromColorIndex(ColorMethod.ByLayer, 0); // 252 ByLayer
        btr.AppendEntity(pl);
        tr.AddNewlyCreatedDBObject(pl, true);
        HyRoadXdata.Write(tr, db, pl, aln.Id, HyRoadXdata.KindAlignmentRawPick, SchemaVersion.Current);
    }
    tr.Commit();
}
```

关键语义：
- **构造**阶段记原锁态（`IsLocked`）→ 若锁着就 `UpgradeOpen` 改 false
- **Dispose** 阶段：若原本锁定就恢复为锁定；**所有异常全部吞**（finally 路径上抛 → AutoCAD shutdown 会原生崩）
- 图层不存在时返回 no-op scope，不抛异常

### 两类 Preview 服务选型矩阵（v2 简化）

| 服务 | 目标图层 | 图层锁定 | KIND | `eraseExistingSameId` | 触发 | 用户自管 | Apply 擦除 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `RoadAlignmentPreviewService` | — (Transient) | — | — | n/a | PI 改动即时 | 否 | n/a |
| `RoadAlignmentRawPolylineService` | `05_hy_道路_原线` | **是**（启动锁定） | `AlignmentRawPick` | `true`（拾取一次性写） | 拾取命令 | 否 | **是**（本 Id） |
| `RoadAlignmentLivePreviewService` | `05_hy_道路_预览` | 否 | `AlignmentLivePreview` | `false`（追加） | 「预览」按钮 | **是** | 否 |
| `RoadAlignmentApplyService` | `05_hy_道路_平面线位` | 否 | `Alignment` | 唯一持久真相 | 「应用」按钮 | 否 | — |

> **已下线**：`RoadAlignmentDesignPreviewService`（v1 的 DispatcherTimer 500ms 自动彩色预览）。类保留但**不要**再调用，用途仅为 Apply 时擦历史 DWG 残留。
