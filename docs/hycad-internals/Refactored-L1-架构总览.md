# HyCADTool.Refactored — L1 架构总览

> 生成日期：2026-04-07 | 343 个 .cs 文件 | 11 个 .xaml 面板/窗口

---

## 项目定位

AutoCAD 结构工程辅助插件（.NET 48 类库），以 `NETLOAD` 或 ReCall 热重载方式加载到 AutoCAD 进程，提供**钢筋绘制/标注、设备基础、桩基布置、基础沉降计算、设计说明排版、图面整理**等一站式命令，通过统一面板（`hy` 命令）和独立窗口（`HYJC` 等）驱动。

---

## 技术栈

| 项 | 值 |
|----|----|
| 框架 | .NET Framework 4.8 (`net48`) + WPF + WinForms |
| 宿主 | AutoCAD 2024（`AutoCAD.NET 24.3.0`） |
| DI | Autofac 7.1.0 |
| 几何 | NetTopologySuite 2.5.0, Clipper2 1.4.0 |
| 文本 | Markdig 0.37.0, Newtonsoft.Json 13.0.3 |
| 导出 | DocumentFormat.OpenXml 3.5.1 |
| 内部依赖 | HyCADTool.TextLayout（MText 排版辅助） |

---

## 目录结构

```
HyCADTool.Refactored/
├── Domain/            ← 平台无关业务核心（139 cs）
│   ├── DataStructures/    DCEL 半边数据结构
│   ├── Entities/          领域实体（Wall3D, Slab3D, Pile, AnchorBolt…）
│   ├── Enums/             枚举（钢筋方向、标高状态…）
│   ├── Interfaces/        领域接口（IStyleService, ILayerService…）
│   ├── Models/            数据模型
│   │   ├── Cluster/           聚类结果
│   │   ├── Configuration/     钢筋配置
│   │   ├── Settlement/        沉降计算输入/输出/土层
│   │   └── Text/              MText 渲染 & Markdown
│   ├── Services/          核心算法
│   │   ├── Geometry/          空间索引、邻接检测
│   │   ├── GeometryAlgorithms/ 点/线/面/DCEL/凸包算法
│   │   ├── MathAlgorithms/    距离/角度
│   │   └── OffsetAlgorithms/  多边形偏移
│   ├── Utilities/         通用工具（钢筋/实体映射）
│   └── ValueObjects/      值对象
│       ├── Configuration/     Global + Modules 配置
│       ├── Geometry/          Point/Line/Polygon/Arc/BBox…
│       ├── Grid/              行列值
│       └── Reinforcement/     钢筋结果
│
├── Infrastructure/    ← AutoCAD 平台封装（101 cs）
│   ├── AutoCAD/
│   │   ├── Converters/        几何转换
│   │   ├── Extensions/        Polyline/MLeader/Point3d 扩展
│   │   ├── Helpers/           辅助方法
│   │   ├── Interactive/       Jig（拖拽预览）
│   │   ├── Interfaces/        服务接口
│   │   ├── Metadata/          属性筛选元数据
│   │   ├── Repositories/      LineRepository
│   │   ├── Selection/         高级选择集 & 过滤器
│   │   ├── Services/          核心服务（绘图/图层/样式/标注/DCEL渲染）
│   │   │   └── Cluster/       聚类绘制/标注
│   │   ├── Utilities/         Transaction/Layer/Entity Helper
│   │   └── Workflows/         墙体/板/标高生成流程
│   ├── Configuration/     Autofac 注册、JSON/CSV 配置加载
│   └── Services/          WordExportService（OpenXml 导出）
│
├── Presentation/      ← 命令 + UI + ViewModel（97 cs, 11 xaml）
│   ├── Commands/          AutoCAD 命令实现（60+ 命令）
│   │   ├── Base/              ICommand / CommandResult
│   │   └── Paper/             视口排版命令
│   ├── Resources/         LibraryResources.xaml（全局样式）
│   ├── ViewModels/        MVVM ViewModel（Settings/Settlement/Pile/Cluster…）
│   └── Views/             WPF 面板 & 窗口
│       ├── Converters/        Bool/Enum/String 转换器
│       └── Helpers/           TextBoxHelper
│
├── Test/              ← 开发联调入口（5 cs）
│   ├── TestCommand.cs         C1 一键执行入口
│   ├── TestRunner.cs          批量测试驱动
│   └── SimpleLogger.cs        耗时计时
│
├── Docs/              ← 文档
└── Properties/        ← AssemblyInfo.cs
```

---

## 模块关系图

```
  PluginInitializer（AutoCAD 启动入口）
    → PanelManager → HyToolPanel (PaletteSet 统一面板)
    → CommandRegistry（60+ [CommandMethod] 注册）
        → SettlementCalculationCommand → SettlementWindow (独立 WPF 窗口)
        │     → SettlementPanelViewModel
        │         → SettlementCalculationService (Domain)
        │         → MarkdownTableParser (Domain)
        │         → SettlementReportGenerator (Domain)
        │         → SettlementTableService (Infrastructure) → AutoCAD Table
        │         → WordExportService (Infrastructure) → .docx
        │
        → DrawOffsetPolylineCommand, ReinAddAnchorCommand, MleaderReinCommand …
        │     → ReinService, BaseReinforcementService (Infrastructure)
        │         → ReinforcementUtils, ReinParameters (Domain)
        │
        → DrawPilesCommand → PilePanelViewModel
        │     → PileLayoutService, VoronoiOptimizationService (Domain)
        │     → PileDrawingService (Infrastructure)
        │
        → DesignSpecCommand / DesignSpecEditCommand → EditorLoader
        │     → DesignSpecService (Infrastructure)
        │     → MarkdownToMTextRenderer (Domain)
        │
        → OverKillCommand, BreakCurvesCommand, DCELCommand …
              → LineOverKillService, CurveBreakService (Domain)
              → DCELRenderer (Infrastructure)

  Infrastructure/Configuration/
    AutofacModule → 注册所有 Domain 接口 → Infrastructure 实现
    JsonConfigurationLoader → config.json → GlobalConfiguration
```

---

## 数据流

### 沉降计算（HYJC）

```
用户粘贴 Markdown 表格
  → MarkdownTableParser 解析为 SoilLayer[]
  → SettlementPanelViewModel 组装 SettlementInput
  → SettlementCalculationService.Calculate()
  → SettlementResult (LayerResults[], FinalSettlement…)
  → 三路输出：
      ① DataGrid + 摘要面板（WPF 窗口内）
      ② SettlementReportGenerator → Markdown 计算书 → WordExportService → .docx
      ③ SettlementTableService → AutoCAD Table 实体（落图）
```

### 面板命令通用流

```
用户输入命令 (如 gg/gb/g1)
  → CommandRegistry.[CommandMethod]
  → RouteThroughC1 (热重载) 或 Run (直接)
  → XxxCommand.Execute()
  → SelectionService 选择实体
  → Domain Service 计算
  → Infrastructure Service 写回图纸
```

---

## 模块一句话速查表

| 模块 | 文件数 | 一句话职责 |
|------|--------|-----------|
| `Domain/DataStructures` | 4 | DCEL（双连通边表）图数据结构 |
| `Domain/Entities` | 8 | 领域实体：墙、板、桩、锚栓、设备基础 |
| `Domain/Enums` | 4 | 业务枚举常量 |
| `Domain/Interfaces` | 11 | 领域服务接口（供 Infrastructure 实现） |
| `Domain/Models/Settlement` | 7 | 沉降计算全套模型（Input/Result/SoilLayer/承载力/桩结构） |
| `Domain/Models/Text` | 7 | Markdown→MText 渲染、HTML 预览、表格提取 |
| `Domain/Models/Configuration` | 1 | 钢筋配置模型 |
| `Domain/Models/Cluster` | 1 | 聚类结果模型 |
| `Domain/Services` | 30+ | 核心算法：沉降、几何、DCEL、OverKill、偏移、标高 |
| `Domain/ValueObjects` | 35+ | 不可变值对象：几何图元、配置、钢筋参数 |
| `Domain/Utilities` | 2 | 通用映射工具 |
| `Infrastructure/AutoCAD` | 90+ | AutoCAD API 封装：绘图/图层/样式/选择/标注/Jig |
| `Infrastructure/Configuration` | 5 | Autofac DI 注册 + JSON/CSV 配置加载 |
| `Infrastructure/Services` | 1 | Word 导出（OpenXml） |
| `Presentation/Commands` | 60+ | 全部 AutoCAD 命令实现 |
| `Presentation/ViewModels` | 7 | MVVM ViewModel（Settings/Settlement/Pile/Cluster/Filter/BaseRein/Markdown） |
| `Presentation/Views` | 11 xaml | WPF 面板/窗口/转换器/辅助 |
| `Test` | 5 | 开发联调：C1 入口 + 计时器 |

---

## 关键命令速查

| 命令 | 功能 | 入口 |
|------|------|------|
| `hy` | 打开 HY 统一工具面板 | `ShowPanelCommand` |
| `HYJC` / `hySC` | 基础沉降计算（独立窗口） | `SettlementCalculationCommand` |
| `gg` | 绘制偏移钢筋 | `DrawOffsetPolylineCommand` |
| `gb` / `gb1` / `gb2` | 钢筋引线标注 | `MleaderReinCommand` |
| `g1` / `g2` | 水平/竖直锚固 | `ReinAddAnchorCommand` |
| `ge` | 钢筋延伸 | `ReinExtendCommand` |
| `gd` | 钢筋裁剪 | `ReinCutCommand` |
| `HYpile` | 桩基布置 | `DrawPilesCommand` |
| `HYpileV` | 桩基 Voronoi 优化 | `PileVoronoiOptimizationCommand` |
| `HYpileG` / `HYpileGT` | 按标高分组圆 / 写标高文字 | `GroupCirclesByElevationCommand` |
| `hyDS` | 设计说明排版 | `DesignSpecCommand` |
| `hyDSe` | 设计说明编辑 | `DesignSpecEditCommand` |
| `HYOV` | OverKill 清理 | `OverKillCommand` |
| `C2` / `C1` | 热重载 / 执行测试命令 | ReCall + `TestCommand` |

---

## 架构分层原则

| 层 | 可依赖 | 禁止依赖 |
|----|--------|----------|
| **Domain** | 无外部依赖（纯算法） | AutoCAD API、WPF |
| **Infrastructure** | Domain | Presentation |
| **Presentation** | Domain + Infrastructure | — |
| **Test** | 全部 | — |

> Domain 层可独立迁移到非 AutoCAD 宿主（如 Blender、Web），是项目可移植性的核心保障。
