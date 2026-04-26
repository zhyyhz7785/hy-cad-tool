# Rein 钢筋命令重构总结

> **用途**：新开文档/对话时传入，作为钢筋相关上下文，控制输入与决策范围。  
> **更新**：2026-02  
> **状态**：钢筋绘制/修改/标注命令已全部迁入 Refactored，并集中到设置面板 Tab B。

---

## 一、命令映射（旧 → 新）

| 旧命令 | 旧文件/方法 | 新命令类 | 说明 |
|--------|-------------|----------|------|
| **gj** | ReinCommand / Reinforcement.Rein | `DrawReinforcementCommand` | 绘制钢筋（边界→配筋） |
| **gg** | 偏移多段线+弯钩 | `DrawOffsetPolylineCommand` | 偏移多段线绘制+弯钩 |
| **g1** | ReinAddAnchor1 | `ReinAddAnchorCommand(isVertical: false)` | 单侧弯钩 |
| **g2** | ReinAddAnchor2 | `ReinAddAnchorCommand(isVertical: true)` | 竖向弯钩 |
| **ge** | ExtentRein | `ReinExtendCommand` | 延伸钢筋+15d 弯折 |
| **ge1** | 快速延伸至边界 | `ReinQuickExtendCommand` | 快速延伸至边界多段线 |
| **gd** | 截断钢筋 | `ReinCutCommand` | 截断钢筋 |
| **gb** | MleaderRein | `MleaderReinCommand(Mode.Standard)` | 多引线标注+点钢筋 |
| **gb1** | MleaderReinOne | `MleaderReinCommand(Mode.Single)` | 单引线标注 |
| **gb2** | MleaderReinTwo | `MleaderReinCommand(Mode.Six)` | 六点引线标注+点钢筋 |

所有上述命令**无** `[CommandMethod]`，通过 C1 路由或面板按钮触发执行。

---

## 二、文件位置（Refactored）

| 类型 | 路径 |
|------|------|
| 命令实现 | `HyCADTool.Refactored/Presentation/Commands/` |
| 面板 ViewModel | `HyCADTool.Refactored/Presentation/ViewModels/SettingsPanelViewModel.cs` |
| 面板 UI（Tab B 钢筋） | `HyCADTool.Refactored/Presentation/Views/SettingsPanel.xaml` |
| MLeader 扩展 | `HyCADTool.Refactored/Infrastructure/AutoCAD/Extensions/MLeaderExtensions.cs` |
| 实体扩展（ToSpace、点钢筋） | `HyCADTool.Refactored/Infrastructure/AutoCAD/Extensions/EntityExtensions.cs` |
| 配筋绘制服务 | `HyCADTool.Refactored/Infrastructure/AutoCAD/Services/ReinService.cs` |
| C1 入口 | `HyCADTool.Refactored/Test/TestCommand.cs` |

---

## 三、参数与单位规则（必遵）

- **模型空间 = 实际 mm（1:1）**  
- 面板参数分两类，命令内使用前需区分：

**红色参数（直接 mm）**：不乘 Scale  
`RebarDiameter`(14)、`RebarSpacing`(200)、`AnchorageLength`(500)、`DotSeparation`(200)、`BendingLineMinLength`(150)、`AnchorageJoinLength`(1500) 等。

**绿色参数（需 × Scale）**：面板存 `实际mm/Scale`，几何计算时 `值 × Scale`  
`ProtectionThickness`、`HookLength`、`ReinforcementDiameter`、`DotReinOffset`、`MleaderDistance` 等。

**特殊**：标注点钢筋沿多段线法线偏移使用 **DotReinOffsetOut**：
- 公式：`(DotReinOffset - 1) * Scale`（与旧 `Reinforcement.DotReinOffsetOut` 一致）
- 默认 1.35 → (1.35−1)×40 = 14mm，勿用 `DotReinOffset * Scale`（会变成 54mm 导致错位）。

Scale 还用于文字/标注/引线样式的符号大小（如 `TextSize×Scale`、Dimscale）。

---

## 四、面板与 C1 行为

- **C1**：  
  - 若有 **PendingCommand**（由面板按钮设置）→ 执行该命令（带 SimpleLogger 耗时）。  
  - 否则 → **打开/切换 HY 设置面板**。
- **Tab B「钢筋」**：  
  所有钢筋命令以按钮形式集中在此。  
  点击按钮 → `SendCommand(action)` → 设置 `PendingCommand`、`EnsureStylesApplied()` 包在 action 内 → `SendStringToExecute("C1\n")` → C1 在 AutoCAD 命令线程执行，避免 WPF 线程直接调 GetEntity 等。
- **参数同步**：  
  命令执行前应使用当前面板参数；面板触发的命令在 `SendCommand` 中已先调 `EnsureStylesApplied()`，直接调命令时若依赖样式/比例，应先 `SettingsPanelViewModel.Current?.EnsureStylesApplied()`。

---

## 五、读取面板参数

- 统一入口：`SettingsPanelViewModel.Current`（对应旧 `ReinPanel.ActivePanel`）。  
- 若面板未打开可能为 null，命令内应对 null 做默认值回退，例如：  
  `double scale = vm?.Scale ?? 40.0;`
- 导出为领域参数：`vm?.CreateReinParameters()` 得到 `ReinParameters`（供 DrawReinforcementCommand 等使用）。

---

## 六、图层名称（常量）

| 用途 | 图层名 |
|------|--------|
| 线钢筋 | `01_hy_1钢筋_线钢筋` |
| 点钢筋 | `01_hy_1钢筋_点钢筋` |
| 引线标注 | `00_hy_3公共_标注3_引线` |

命令内使用前应确保图层存在（如 `_layerService.LayerExists` + `CreateLayer`）。

---

## 七、基础设施替换（旧 → 新）

| 旧 | 新 |
|----|-----|
| `ZTools.SetCurrentLayer` | `ILayerService.SetCurrentLayer` |
| `ZTools.CreateSolidCircle` | `EntityExtensions.CreateSolidCircle` |
| `entity.ToSpace()` | `EntityExtensions.ToSpace()` |
| `points.AddMleader` / AddMleaderOne / AddMleaderSix | `MLeaderExtensions` 同名扩展方法 |
| `points.PointsToDotRein()` | `EntityExtensions.PointsToDotRein(points, diameter)`，diameter 为实际 mm（如 `ReinforcementDiameter * Scale`） |
| `"图层名".GetLayerId()` | `ILayerService.GetLayerId("图层名")` 或先 Ensure 图层再设 `entity.Layer = "图层名"` |

---

## 八、MLeader 与点钢筋注意点

- **AddMleader / AddMleaderOne**：  
  第二/三象限会翻转 `points` 顺序以统一引线方向；**不要**在翻转后重算 `vecH`，集中点 `centralPoint` 仍按原始 `endP`/`centerP` 与原始 `vecH` 的垂直方向计算，否则集中点会跑到错侧。
- **DotReinOffsetOut**：  
  仅标注取点用法线偏移，必须用 `(DotReinOffset - 1) * Scale`。
- **gb2 第二组点**：  
  第二组 3 点用固定偏移 17.5（与旧 `GetReinPointsByPoint` 一致），不读面板。

---

## 九、约束（与 01 规则一致）

- 重构代码**禁止**使用 `[CommandMethod]`（热重载 eDuplicateKey）。  
- 只改 Refactored；ReCall 仅改配置；旧项目 HyCADtool 不修改。  
- 常用命令输出极简 1～3 行；循环内禁止输出。  
- 异常处理用 `System.Exception`。

---

## 十、快速检查清单（改 Rein 相关时）

- [ ] 参数取自 `SettingsPanelViewModel.Current` 并做 null/默认值处理  
- [ ] 绿色参数已 × Scale，红色未乘  
- [ ] 标注点钢筋法线偏移用 DotReinOffsetOut = (DotReinOffset−1)*Scale  
- [ ] 需当前样式时已调 `EnsureStylesApplied()` 或由面板路由已包含  
- [ ] 图层先 Ensure 再写入实体  
- [ ] 未在 MLeader 翻转后重算 vecH

---

**版本**：v1.0  
**适用**：新对话/新文档时作为 Rein 重构上下文传入，控制输入与实现范围。
