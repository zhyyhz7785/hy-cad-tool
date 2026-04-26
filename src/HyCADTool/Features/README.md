# HyCADTool.Features

业务功能按领域分目录，命令经 `Shell` / `App` 的 Facade 与 `commands.json` 映射注册（热重载下勿直接用 `[CommandMethod]` 于 Refactored 主流程）。

**项目总览（心智模型）**：[`doc/00-新的开始-2026-04-26-175400.md`](../../../doc/00-新的开始-2026-04-26-175400.md)  
**Shell 机箱边界**：[`../Shell/README.md`](../Shell/README.md)

---

## 子目录 README 的 L3 约定

各 `Features/<切片>/README.md` 采用 **L3 深度**：在 L2（一句话 + 文件列表）之上，固定包含 **定位 / 职责边界 / 目录结构 / 命令与入口 / 依赖与协作 / 开发与审查要点**，并与总纲 `doc/00` 对齐。便于 PR 审查时代替口头复述分层。

---

## 切片内分层（每个 `Features/<切片>/`）

典型子目录（按需要取舍，非每个切片齐全）：

| 子目录 | 职责 |
|--------|------|
| `Domain/` | 纯模型与算法，**不**引用 AutoCAD API（可引用 `Shared/Geometry`） |
| `Services/` | 读图、写图、事务、与 AutoCAD 交互 |
| `ViewModels/` | 面板/对话框状态与命令绑定 |
| `Views/` | XAML + code-behind |
| 根或 `Commands/` | `Execute()` 入口类（由 ReCall/`commands.json` 调度） |

**一条切片 = 一条业务竖线**：尽量在切片内闭环；跨切片复用走 `Shared/`，全局机箱走 `Shell/`。

---

## 与 `Shell` / `Shared` / `App` 的关系

- **`Shell`**：机箱；`HyBlenderPanel`、Ribbon、全局配置 schema、`CommandCatalog`、`CommandDispatcher`、`IStyleService` 等。Feature 的**业务面板**可被 Shell 嵌入，但**实现代码**仍在 `Features`。
- **`Shared`**：跨业务几何与 AutoCAD 通用封装；Feature 的 Domain 应优先依赖 Shared，而非另一 Feature。
- **`App`**：DI 注册与插件启动；新增服务/命令类记得注册到 Autofac（见项目既有模式）。

---

## 放新文件：5 秒决策（Feature 侧）

1. 只属于本业务？→ `Features/<切片>/` 下按上表选子目录。
2. 多个业务都会用？→ `Shared/Geometry` 或 `Shared/AutoCAD`。
3. 全局 UI 框架/配置清单？→ 不放进 Feature；与维护者确认是否 `Shell`。
4. 需要发 AutoCAD 命令串（与 `commands.json` 一致）？→ 可用 `Shell.Commands.CommandDispatcher.Send`（视为机箱提供的公共服务）。

---

## PR Review Checklist（改 Features 时逐项勾）

- [ ] **分层**：新算法默认进 `Domain/` 或 `Services/`；未在命令类里堆数百行业务逻辑。
- [ ] **依赖方向**：未让 `Domain` 引用 AutoCAD、`ViewModels` 或 `Shell.Configuration`（读用户设置可通过注入或服务，避免 Domain 直接绑 `HyCadUserSettings`）。
- [ ] **跨切片**：未在 Feature A 里 `using Features.B` 深层类型；共用逻辑应下沉 `Shared` 或上提契约。
- [ ] **命令注册**：新命令已写入 `commands.json`、与 ReCall 占位符流程一致（见 `.cursor/skills/hycad-new-command-registration/SKILL.md`）。
- [ ] **设置与 Shell**：若改 `SettingsPanelViewModel` / 全局样式，确认是否影响所有命令；缩放/单位规则见 `.cursor/rules/01-AI热启动模式.mdc`。
- [ ] **WPF**：新面板本地合并主题字典；PaletteSet 宿主注意项见 `hycad-project-pitfalls` / `wpf-paletteset-resource-pitfalls`。

---

## 目录索引

| 目录 | 说明 |
|------|------|
| [AcadDimension](./AcadDimension/README.md) | 通用尺寸标注编辑（加点、分割、对齐） |
| [AnchorBolt](./AnchorBolt/README.md) | 地脚螺栓绘制、计算、剖面、表格等 |
| [BaseRein](./BaseRein/README.md) | 底板配筋多步工作流与面板 |
| [BlockLeader](./BlockLeader/README.md) | 块与多重引线互转、块颜色 |
| [Cluster](./Cluster/README.md) | 聚类与绘图服务（与图签、首选项聚类页联动） |
| [DCEL](./DCEL/README.md) | 双连通边表几何内核（命令入口在 `Misc`） |
| [DesignSpec](./DesignSpec/README.md) | 设计说明 / Markdown 相关领域模型与编辑视图 |
| [DimensionForReinforcement](./DimensionForReinforcement/README.md) | 配筋场景专用尺寸标注 |
| [DimTextAlign](./DimTextAlign/README.md) | 标注文字防重叠服务 |
| [Elevation](./Elevation/README.md) | 标高符号与相关 3D/文字 |
| [EquipmentFoundation](./EquipmentFoundation/README.md) | 设备基础：底座/螺栓/轴线数据与命令 |
| [Export](./Export/README.md) | 导出 CSV、线型捕集、与 DesignSpec 联动命令等 |
| [Misc](./Misc/README.md) | 杂项命令（含 DCEL 命令外壳） |
| [OverKill](./OverKill/README.md) | 线重叠清理（OVERKILL + 圆角等综合流程） |
| [Pad](./Pad/README.md) | 垫层/多边形生成与替换 |
| [Pile](./Pile/README.md) | 桩基绘制与圆分组、Voronoi 等 |
| [Reinforcement](./Reinforcement/README.md) | 钢筋通用绘制与引线、延长等 |
| [Road](./Road/README.md) | 道路全模块（线位、横断、纵断、平面、交叉口等） |
| [Settlement](./Settlement/README.md) | 基础沉降计算与报告 |
| [TestPaper](./TestPaper/README.md) | 试验/出图相关辅助 |
| [TitleBlock](./TitleBlock/README.md) | 图签、视口排布、布局视口生成 |

Shell 侧同类清单与已知耦合见 `../Shell/README.md`。
