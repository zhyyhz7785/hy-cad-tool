# 042 · 道路设计（rCs / 标准横断面）· AI 上下文索引

> **代号 `042`**：在对话里写「042」或 `@` 本文件，即表示聚焦 **M3 横断面绘制**（`hyRoadCs` 及其别名 **`rCs`**）、预设与出图链路；文档与代码锚点对齐后再回答或改代码。  
> **总览入口**：更宽的道路设计文档树与全量源码表见 **[041 · 道路设计 · AI 上下文索引](./041-道路设计-AI上下文索引.md)**。  
> 完整路径：`doc/RoadDesign/042-道路设计-rCs-AI上下文索引.md`

### 命令与别名（ReCall）

| 主命令 | 别名 | 职责 |
|--------|------|------|
| `hyRoadCs` | `rCs` | 新建：打开 `CrossSectionDrawWindow`（默认 CJJ 37 主干路预设）→ 定稿 → 插入点 → 落图 + JSON |
| `hyRoadCsLoad` | `rCsL` | 加载：从当前 DWG 已存 `Templates` 选一模板 → 同上窗口 |
| `hyRoadCsQuick` | `rCsQ` | 命令行：选预设直出图，不开 WPF（脚本 / 回归） |
| `hyRoadT` | — | **兼容壳**，转发至 `RoadCrossSectionDrawCommand.Execute()`（与 v2 `hyRoadCs` 同实现） |
| `hyRoadCsPresetSave` | — | 从当前 `RoadDesign` 选 Template 导出为用户预设（`%AppData%/HyCAD/presets/crosssection/`） |
| `hyRoadCsPresetLoad` | — | 列出内置 + 用户预设（命令行） |

注册与面板顺序：`ReCall/commands.json` 中 `hyRoad*` 道路组；`ReCall/CommandFacade.cs` / `RoadCommandShortAliases.cs` 中为 `rCs` / `rCsL` / `rCsQ` 绑定。

---

## 一、必读总纲

| 优先级 | 文档 | 用途 |
|:------:|------|------|
| P0 | [README.md](./README.md) | 目录说明、阅读顺序 |
| P0 | [01MASTER.md](./01MASTER.md) | 五元模型、M3 横断面在总路线图中的位置 |
| P0 | [041-道路设计-AI上下文索引.md](./041-道路设计-AI上下文索引.md) | 道路设计全主题单入口（本 042 为其 M3 子集） |
| P1 | [052-道路设计命令功能-2026-04-19.md](./052-道路设计命令功能-2026-04-19.md) **§4 M3 横断面** | `hyRoadCs` 三分支、窗口字段、图层表（与实现对照） |
| P1 | [053-道路设计领域类型关系-2026-04-20.md](./053-道路设计领域类型关系-2026-04-20.md) **§5** | `CrossSectionLayout` ↔ `Template` ↔ `CrossSectionFigure` |

---

## 二、领域与对标（横断面相关）

| 主题 | 文档 |
|------|------|
| 横断面装配 / Civil 对标 | [09Assembly.md](./09Assembly.md) |
| 横断面字段、参数表 | [06.md](./06.md) |
| Corridor 与 Assembly 概念（理解 Template 下游） | [10Corridor.md](./10Corridor.md) |
| 鸿业式标准横断面 UX 参考 | [HongYeRoad.md](./HongYeRoad.md) |
| LandXML 横断面字段边界 | [054-LandXML学习文档-2026-04-20.md](./054-LandXML学习文档-2026-04-20.md) §3.6 |

---

## 三、实施快照与工程笔记

| 编号 | 文档 | 用途 |
|------|------|------|
| 051 | [051-道路设计当前完成工作-2026-04-19.md](./051-道路设计当前完成工作-2026-04-19.md) | M3「标准断面绘制」完成度 |
| 052 | [052-道路设计命令功能-2026-04-19.md](./052-道路设计命令功能-2026-04-19.md) | 命令表与 `CrossSectionDrawWindow` 说明 |
| 053 | [053-道路设计领域类型关系-2026-04-20.md](./053-道路设计领域类型关系-2026-04-20.md) | 值对象与 JSON 中 Template 存储 |
| 055 | [055-M6-M10完工报告-2026-04-20.md](./055-M6-M10完工报告-2026-04-20.md) | M7 板块方案 / 预设 / `CrossSectionDrawMode` 等 |
| 057 | [057-数据格式与持久化策略-2026-04-20.md](./057-数据格式与持久化策略-2026-04-20.md) | `.roaddesign.json`、Registry |
| 046 | [046-道路设计-rCs-v3-横断面增强-2026-04-22.md](./046-道路设计-rCs-v3-横断面增强-2026-04-22.md) | rCs v3：大纲行内输入/拖拽、三级结构层、拾取赋值、高差引线 |
| 048 | [048-道路设计-rCs-v4-落图标注-2026-04-23.md](./048-道路设计-rCs-v4-落图标注-2026-04-23.md) | rCs v4：一键横断面出图（顶面+标注）、`AlignedDimension`、样式工厂 |

---

## 四、个人工作备忘（非正式规格）

| 文件 | 说明 |
|------|------|
| [00-.md](./00-.md) | 随记中含「rCs 窗口」、横断面与交叉口建模构想；**以 052/053/057 与代码为准** |
| [00-1.md](./00-1.md) | rCs 面板完善备忘 |

---

## 五、源码索引（HyCADTool.Refactored）

以下路径相对仓库根目录。**交付链路**（命令文件头注释）：`CrossSectionLayout` → `Template` → JSON（Registry + Export）→ `CrossSectionFigure` → `RoadStandardSectionDrawService` 落图。  
**DI**：在 `Infrastructure/Configuration/AutofacModule.cs` 中检索 `CrossSection`、`RoadStandardSection`；道路总注册可同时搜 `Road`。

### 5.1 WPF：`Presentation/Views/Road/`

| XAML | Code-behind | 说明 |
|------|-------------|------|
| `CrossSectionDrawWindow.xaml` | `CrossSectionDrawWindow.xaml.cs` | **v2 主界面**（Workbench：`hyRoadCs` / `rCs`） |
| `CrossSectionDesignerWindow.xaml` | `CrossSectionDesignerWindow.xaml.cs` | v1 参数窗（Obsolete，兼容 `hyRoadT` 旧入口与预览逻辑） |
| — | `CrossSectionPreviewRenderer.cs` | `CrossSectionFigure` → WPF `Canvas` 预览（新旧窗口共用） |

### 5.2 ViewModels：`Presentation/ViewModels/Road/`

| 文件 | 说明 |
|------|------|
| `CrossSectionDesignerViewModel.cs` | 横断面参数与预览事件（基类能力） |
| `CrossSectionDrawViewModel.cs` | 继承上者；v2 侧栏、预设、`CrossSectionDrawMode`（单线 / 结构厚度）等 |
| `RoadDesignViewModel.cs`（节选） | 统一道路面板「横断面」按钮绑定 `RoadCrossSectionDrawCommand`，避开 Obsolete 的 `hyRoadT` 壳 |

### 5.3 命令 `Presentation/Commands/Road/`

| 文件 | 说明 |
|------|------|
| `RoadCrossSectionDrawCommand.cs` | **`hyRoadCs` / `hyRoadCsLoad` / `hyRoadCsQuick` 实现** |
| `RoadCsPresetSaveCommand.cs` | `hyRoadCsPresetSave` |
| `RoadCsPresetLoadCommand.cs` | `hyRoadCsPresetLoad` |
| `RoadTemplateCommand.cs` | `hyRoadT` Obsolete 转发壳（→ `RoadCrossSectionDrawCommand`） |

### 5.4 领域模型 `Domain/Models/Road/`

| 文件 | 说明 |
|------|------|
| `Template.cs` | 持久化横断面模板（`RoadDesign.Templates`）；与 `CrossSectionLayout` 互转见 Builder |

### 5.5 值对象 `Domain/ValueObjects/Road/`

| 文件 | 说明 |
|------|------|
| `CrossSectionLayout.cs` | 运行时不可变横断面条带模型 |
| `CrossSectionBand.cs` | 单侧条带 |
| `CrossSectionFigure.cs` | 出图用二维图元指令集 |

### 5.6 领域服务 `Domain/Services/Road/`

| 文件 | 说明 |
|------|------|
| `CrossSectionLayoutBuilder.cs` | `Layout` ↔ `Template` ↔ `Figure` |
| `CrossSectionGeometryGenerator.cs` | 低层几何（偏移、路牙、路拱等） |
| `CrossSectionCodeChecker.cs` | CJJ 37 相关校核 |
| `CrossSectionPresets.cs` | 内置典型断面（支/次/主/快速等） |
| `CrossSectionPresetService.cs` | 内置 + 用户预设目录读写 |

### 5.7 Infrastructure

| 路径 / 文件 | 说明 |
|-------------|------|
| `Infrastructure/AutoCAD/Services/Road/RoadStandardSectionDrawService.cs` | 标准横断面图落模、图层与 `CrossSectionDrawMode` |
| `Presentation/Factories/RoadCsDrawStyleFactory.cs` | rCs 出图样式快照：CommitStyles 后置为当前，按名解析 Text/Dim/MLeader 的 `ObjectId`（不建 hy-rcs-*） |
| `Domain/ValueObjects/Road/CrossSectionAnnotationStyle.cs` | 出图标注样式快照（Text/Dim/MLeader StyleId + model 高度参数） |
| `Infrastructure/AutoCAD/Xdata/HyRoadXdata.cs` | 含 `KindCrossSection` 等与道路实体身份绑定 |

### 5.8 邻接（消费 Template / Layout，非 rCs 本体但常一起查）

| 文件 | 说明 |
|------|------|
| `Presentation/Commands/Road/RoadPlanCommand.cs` | 平面图扫掠：无 Template 时提示先 `hyRoadCs` |
| `Domain/Services/Road/CorridorSweepService.cs` | `Alignment` + `CrossSectionLayout` → 平面计划多段线 |
| `Presentation/Commands/Road/RoadExtractCommand.cs` | 与 `PolylineToBandExtractor` 等搭配时可反解 `CrossSectionBand` |

---

## 六、项目级避坑

- AutoCAD + PaletteSet + WPF：`hycad-project-pitfalls`、`wpf-paletteset-resource-pitfalls`
- 命令分层与统一面板：`hycad-refactored-migration-patterns`
- 最后一个 WPF 窗口关闭会导致 Application 退出：`PluginInitializer` 注释；**勿在无关路径随意改窗口生命周期**（影响 `hyRoadCs` 等）

---

## 七、维护说明

- **文档**：总纲仍以 041 + 052/053 为准；新增 rCs 专用备忘可放 `00-1.md` 或本节第三节。
- **源码**：新增或重命名 `CrossSection*` / `RoadCs*` / `RoadStandardSection*` 时，同步更新 **第五节**；若命令别名变化，同步 **文首命令表** 与 `ReCall/commands.json` 说明。

---

## 八、2026-04-21 Blender 风深度优化（三层结构模型）

### 8.1 数据结构三层

| 层级 | 模型 | 承载 |
|------|------|------|
| L1 · 条带 | `CrossSectionBand`（左/右 / 中央隔离带） | 机动车道 / 非机动车道 / 绿化带 / 分隔带 / 人行道 / 路牙 / 中分带 |
| L2 · 结构分段 | `StructureLayerKind`（Surface / Base / Subbase / Custom） | 面层 / 基层 / 垫层（绿化带无此层） |
| L3 · 单层 | `StructureLayer`（`StructureLayerScheme.Layers` 条目） | 厚度 / 填料 / 填充符号 / 左右加宽 / 左右坡 |

- L1 ↔ L2/L3：`CrossSectionBand.StructureScheme`（Phase 1，仅 VM/UI 使用，JSON 不持久化）；`DefaultStructureSchemes.For(TemplateComponentKind)` 按 Kind 返回默认方案。
- L1 仅当 Kind ∈ {Pavement, NonMotorized, Sidewalk} 时具备 L2/L3；`GreenStrip` / `MedianStrip` / `Kerb` / `Shoulder` 不展开结构层（UI 自动隐藏 Expander）。

### 8.2 UI 骨架（`CrossSectionDrawWindow.xaml`）

- **Col 0 · Outliner**：单一 `TreeView` 绑定 `OutlineRoot`，含三段
  1. `MedianOutlineNode`（启用 CheckBox + 宽度显示；双向绑 `CenterMedianWidth`）
  2. `SideOutlineNode "左"`（`Children` 直接引用 `LeftBands`）
  3. `SideOutlineNode "右"`（`Children` 直接引用 `RightBands`）
  条带项附启用 CheckBox + 类型色条（`KindToColorBrushConverter`）+ 名称 + 宽度 m。`IsSelected` / `IsExpanded` 两向绑定。
- **Col 4 · PropertyEditor**：三路选中互斥触发不同 Expander 组
  - 选 `MedianNode` → 「中央隔离带」Expander
  - 选 `BandRowViewModel` → 「选中条带 / 路面结构层 / 外侧路牙 / 内侧路牙」Expander
  - 选 `SideOutlineNode` → 仅显示全局 / 桩号 / 规范检查（未来可加批量操作）
- 「路面结构层」Expander：`AddSurface/Base/SubbaseLayerCommand` 三按钮 + MoveUp/Down/Remove + ListBox(`StructureLayers`) + 当前层详情（类型 / 厚度 / 填料 / 填充符号）。

### 8.3 VM 选中路由（三路互斥）

`CrossSectionDrawViewModel.SelectedOutlineNode` 根据 value 类型分发：
- `BandRowViewModel` → 清 Median/Side，置 `SelectedBand`
- `MedianOutlineNode` → 清 Band/Side，置 `SelectedMedianNode`
- `SideOutlineNode` → 清 Band/Median，置 `SelectedSideNode`

任一路变化后调用 `SyncTreeSelection()`，统一扫一遍所有节点的 `IsSelected`，防止 VM 主动改选时 TreeView 不跟随。

### 8.4 Phase 4（AutoCAD 取图） · **占位设计**

**目标**：增加 `hyRoadCsPick`（别名 `rCsP`）— 在 AutoCAD 中框选一组 LWPolyline / Line 自动识别条带宽度序列，填充到当前窗口。

**落地拆分**：

| 步骤 | 所在层 | 已有 or 新增 |
|------|--------|----------|
| 1. 选择与几何提取 | `Presentation/Commands/Road/RoadCsPickCommand.cs`（新） | 复用 `Editor.GetSelection` + `PolylineToBandExtractor` |
| 2. 几何 → `CrossSectionLayout` | `Domain/Services/Road/PolylineToBandExtractor.cs` | 已存在，输出 `IReadOnlyList<CrossSectionBand>` |
| 3. 注入 VM | `CrossSectionDrawViewModel.LoadLayout(layout)` | 已存在，直接复用 |
| 4. 结构层再注入 | `BandRowViewModel` ctor / Kind 变化时 `InjectDefaultStructure` | 本次已落地 |
| 5. 窗口激活 | 宿主面板（统一面板 or 独立窗口） | 需 `RoadCsPickCommand` 自己拉起一个 `CrossSectionDrawWindow` |

**边界与风险**：

- `PolylineToBandExtractor` 仅能识别几何宽度；Kind 需启发式（宽度阈值 + 图层名）或留空交由用户分类；
- 若取自已落图的 `RoadStandardSection`，XData `KindCrossSection` 可直接反解；
- 与 `hyRoadCsLoad`（已落图 Template 反查）形成互补：Pick 面向未建模的手绘图纸，Load 面向本插件已定稿数据；
- 命令注册：`ReCall/commands.json` + `ReCall/CommandFacade.cs` + `RoadCommandShortAliases.cs` 需同步扩展；别名规避 `rCsP` 已存在占用（待检）。

**落地顺序建议**：Phase 4 开工时先跑一轮 `PolylineToBandExtractor` 回归；再按上表五步一条路打穿，不拆 PR。

### 8.5 2026-04-22 v3 已落地要点

- 左大纲：条带行内编辑 `宽度/横坡/高差`，并支持同侧拖拽排序。
- 新增统一 `＋添加`（当前选中行下插），去掉中间竖向工具条。
- 右属性：`路面结构` 改为 `Level1/Level2/Level3` 三级分组，且数量上限固定（面≤3、基≤5、垫≤2）。
- 增加逐条带“从图形拾取”工作流（选 Polyline 反解宽度/坡度/总厚度）。
- 出图增加 `ElevationDiff` 标高引线（MLeader）。
