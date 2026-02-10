# HyCADTool.Refactored 项目结构文档

> **生成时间**：2026-02-10  
> **项目版本**：v3.1  
> **文档用途**：理清项目结构、架构设计、模块关系

---

## 📋 目录

1. [项目概览](#1-项目概览)
2. [架构设计](#2-架构设计)
3. [目录结构详解](#3-目录结构详解)
4. [核心模块说明](#4-核心模块说明)
5. [依赖关系图](#5-依赖关系图)
6. [关键技术栈](#6-关键技术栈)
7. [开发工作流](#7-开发工作流)
8. [命令清单](#8-命令清单)
9. [配置系统](#9-配置系统)
10. [测试体系](#10-测试体系)

---

## 1. 项目概览

### 1.1 项目基本信息

| 项目属性 | 值 |
|---|---|
| **项目名称** | HyCADTool.Refactored |
| **目标框架** | .NET Framework 4.8 |
| **平台** | AutoCAD 2024 (x64) |
| **架构模式** | Clean Architecture + DDD |
| **语言版本** | C# 8.0 |
| **输出类型** | Class Library (DLL) |
| **UI 框架** | WPF + Windows Forms |

### 1.2 项目目标

1. **重构旧代码**：将 HyCADTool 旧项目按 Clean Architecture 重构
2. **平台无关设计**：Domain 层 100% 平台无关，为 Blender 迁移做准备
3. **提升代码质量**：可测试、可维护、可扩展
4. **建立测试体系**：热重启机制 + 自动化测试

### 1.3 当前进度

- **总体进度**：约 30%
- **已完成命令**：29 个
- **待迁移命令**：60+ 个
- **架构完成度**：95%
- **测试覆盖率**：待建立

---

## 2. 架构设计

### 2.1 Clean Architecture 分层

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│              (Commands, ViewModels, Views)                   │
│                                                              │
│  - Commands/         # AutoCAD 命令                          │
│  - ViewModels/       # WPF 视图模型                          │
│  - Views/            # WPF 界面                              │
└────────────────────────────┬────────────────────────────────┘
                             │ 依赖 ↓
┌─────────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                        │
│              (AutoCAD Services, Configuration)               │
│                                                              │
│  - AutoCAD/          # AutoCAD API 封装                      │
│    - Services/       # 绘图、编辑、选择服务                  │
│    - Extensions/     # 扩展方法                              │
│    - Converters/     # 几何转换器                            │
│  - Configuration/    # 依赖注入、配置管理                    │
└────────────────────────────┬────────────────────────────────┘
                             │ 实现 ↑ 依赖 ↓
┌─────────────────────────────────────────────────────────────┐
│                      Domain Layer                            │
│         (Entities, ValueObjects, Services)                   │
│         ✅ 100% 平台无关，可迁移到 Blender                    │
│                                                              │
│  - Entities/         # 领域实体                              │
│  - ValueObjects/     # 值对象（几何、配置）                  │
│  - Services/         # 领域服务（算法）                      │
│  - Interfaces/       # 接口定义                              │
│  - DataStructures/   # 数据结构（DCEL）                      │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 依赖规则

1. **Domain 层**：零依赖，100% 平台无关
2. **Infrastructure 层**：依赖 Domain，封装 AutoCAD API
3. **Presentation 层**：依赖 Domain + Infrastructure

### 2.3 设计原则

- **单一职责原则 (SRP)**：每个类只负责一件事
- **依赖倒置原则 (DIP)**：依赖接口而非实现
- **开闭原则 (OCP)**：对扩展开放，对修改关闭
- **接口隔离原则 (ISP)**：接口精简，按需定义

---

## 3. 目录结构详解

### 3.1 完整目录树

```
HyCADTool.Refactored/
│
├── Domain/                              # 领域层（平台无关）
│   ├── DataStructures/                  # 数据结构
│   │   └── DCEL/                        # 双连边数据结构
│   │       ├── DCELGraph.cs             # DCEL 图
│   │       ├── Vertex.cs                # 顶点
│   │       ├── HalfEdge.cs              # 半边
│   │       └── Face.cs                  # 面
│   │
│   ├── Entities/                        # 领域实体
│   │   ├── BuildingElement.cs           # 建筑元素基类
│   │   ├── Pile/                        # 桩实体
│   │   │   ├── Pile.cs
│   │   │   └── PileEnums.cs
│   │   ├── Slab3D.cs                    # 楼板实体
│   │   ├── Wall3D.cs                    # 墙体实体
│   │   └── SurfaceBasedModel.cs         # 曲面模型
│   │
│   ├── Enums/                           # 枚举类型
│   │   ├── DimensionFor.cs              # 标注类型
│   │   ├── IntersectionsDirection.cs    # 交点方向
│   │   ├── PileEnums.cs                 # 桩枚举
│   │   └── RebarDirection.cs            # 钢筋方向
│   │
│   ├── Interfaces/                      # 接口定义
│   │   ├── IGeometryService.cs          # 几何服务接口
│   │   ├── ILayerService.cs             # 图层服务接口
│   │   ├── IStyleService.cs             # 样式服务接口
│   │   ├── IInputService.cs             # 输入服务接口
│   │   ├── ISelectionFilterService.cs   # 选择过滤接口
│   │   └── ...（12 个接口）
│   │
│   ├── Models/                          # 领域模型
│   │   ├── Cluster/                     # 聚类模型
│   │   │   └── ClusterResult.cs
│   │   └── Configuration/               # 配置模型
│   │       ├── BaseReinforcementConfig.cs
│   │       ├── ClusterConfig.cs
│   │       └── ClusterDimOptions.cs
│   │
│   ├── Services/                        # 领域服务（算法）
│   │   ├── GeometryAlgorithms/          # 几何算法
│   │   │   ├── PointAlgorithmService.cs
│   │   │   ├── LineAlgorithmService.cs
│   │   │   ├── PolygonAlgorithmService.cs
│   │   │   ├── ConvexHullAlgorithm.cs
│   │   │   ├── ClusteringService.cs
│   │   │   ├── DCELBuilderService.cs
│   │   │   └── GeometryConverterService.cs
│   │   │
│   │   ├── MathAlgorithms/              # 数学算法
│   │   │   ├── AngleCalculator.cs
│   │   │   └── DistanceCalculator.cs
│   │   │
│   │   ├── OffsetAlgorithms/            # 偏移算法
│   │   │   ├── PolygonOffsetService.cs
│   │   │   ├── OffsetBuilder.cs
│   │   │   ├── OffsetMath.cs
│   │   │   └── OffsetNormals.cs
│   │   │
│   │   ├── Geometry/                    # 几何工具
│   │   │   ├── GeometryUtils.cs
│   │   │   ├── AdjacencyDetector.cs
│   │   │   └── SpatialIndexService.cs
│   │   │
│   │   ├── CurveBreakService.cs         # 曲线打断
│   │   ├── CurveIntersectionService.cs  # 曲线交点
│   │   ├── CurveSimplificationService.cs # 曲线简化
│   │   ├── LineOverKillService.cs       # 线段去重
│   │   ├── PolygonMerger.cs             # 多边形合并
│   │   ├── PileLayoutService.cs         # 桩布置服务
│   │   ├── VoronoiOptimizationService.cs # Voronoi 优化
│   │   ├── Elevation3DCalculator.cs     # 标高计算
│   │   ├── ElevationParser.cs           # 标高解析
│   │   └── ...（20+ 个服务）
│   │
│   ├── ValueObjects/                    # 值对象
│   │   ├── Geometry/                    # 几何值对象
│   │   │   ├── Point2D.cs
│   │   │   ├── Point3D.cs
│   │   │   ├── Vector2D.cs
│   │   │   ├── Vector3D.cs
│   │   │   ├── Line2D.cs
│   │   │   ├── Arc2D.cs
│   │   │   ├── Circle2D.cs
│   │   │   ├── Ellipse2D.cs
│   │   │   ├── Spline2D.cs
│   │   │   ├── Polygon2D.cs
│   │   │   ├── Polyline2D.cs
│   │   │   ├── BoundingBox.cs
│   │   │   ├── Tolerance.cs
│   │   │   └── ...（20+ 个几何类）
│   │   │
│   │   ├── Configuration/               # 配置值对象
│   │   │   ├── Global/                  # 全局配置
│   │   │   │   ├── GlobalConfiguration.cs
│   │   │   │   ├── ScaleConfig.cs
│   │   │   │   ├── LayerConfig.cs
│   │   │   │   ├── StylesConfig.cs
│   │   │   │   ├── TextStyleConfig.cs
│   │   │   │   ├── DimensionStyleConfig.cs
│   │   │   │   ├── MLeaderStyleConfig.cs
│   │   │   │   ├── PathConfig.cs
│   │   │   │   └── ToleranceConfig.cs
│   │   │   │
│   │   │   └── Modules/                 # 模块配置
│   │   │       ├── ElevationConfiguration.cs
│   │   │       ├── FoundationConfiguration.cs
│   │   │       ├── PileConfiguration.cs
│   │   │       └── ReinforcementConfiguration.cs
│   │   │
│   │   ├── Elevation.cs                 # 标高值对象
│   │   ├── ReinParameters.cs            # 钢筋参数
│   │   ├── SlabThickness.cs             # 板厚
│   │   ├── WallThickness.cs             # 墙厚
│   │   └── ToleranceSettings.cs         # 容差设置
│   │
│   └── Utilities/                       # 工具类
│       ├── EntityTypeMapping.cs         # 实体类型映射
│       └── ReinforcementUtils.cs        # 钢筋工具
│
├── Infrastructure/                      # 基础设施层
│   ├── AutoCAD/                         # AutoCAD 适配器
│   │   ├── Converters/                  # 转换器
│   │   │   ├── GeometryConverter.cs     # 几何转换
│   │   │   ├── IGeometryConverter.cs
│   │   │   └── ReinforcementConverter.cs
│   │   │
│   │   ├── Extensions/                  # 扩展方法
│   │   │   ├── EntityExtensions.cs      # 实体扩展
│   │   │   ├── PolylineExtensions.cs    # 多段线扩展
│   │   │   ├── LineExtensions.cs        # 线扩展
│   │   │   ├── Point3dExtensions.cs     # 点扩展
│   │   │   ├── GeometryExtensions.cs    # 几何扩展
│   │   │   ├── MLeaderExtensions.cs     # 引线扩展
│   │   │   ├── FilterExtensions.cs      # 过滤扩展
│   │   │   ├── SelectionExtensions.cs   # 选择扩展
│   │   │   ├── Extents3dExtensions.cs   # 范围扩展
│   │   │   └── TypeNameConverter.cs     # 类型名转换
│   │   │
│   │   ├── Helpers/                     # 辅助类
│   │   │   └── PolylineHelper.cs        # 多段线辅助
│   │   │
│   │   ├── Interactive/                 # 交互式绘图
│   │   │   ├── PolylineJig.cs           # 多段线 Jig
│   │   │   ├── HookJig.cs               # 弯钩 Jig
│   │   │   └── HookJigSeg.cs            # 弯钩段 Jig
│   │   │
│   │   ├── Interfaces/                  # 接口实现
│   │   │   ├── IDatabaseService.cs
│   │   │   ├── IEditorService.cs
│   │   │   ├── IDrawingService.cs
│   │   │   ├── ISelectionService.cs
│   │   │   ├── ILayerManager.cs
│   │   │   ├── ICurveSegmentExtractor.cs
│   │   │   ├── IDCELRenderer.cs
│   │   │   ├── IGeometry3DBuilder.cs
│   │   │   ├── ISolid3DBuilder.cs
│   │   │   └── IWallBuilder.cs
│   │   │
│   │   ├── Metadata/                    # 元数据
│   │   │   ├── PropertyMetadata.cs
│   │   │   └── FilterablePropertyMetadataProvider.cs
│   │   │
│   │   ├── Repositories/                # 仓储
│   │   │   ├── ILineRepository.cs
│   │   │   └── LineRepository.cs
│   │   │
│   │   ├── Selection/                   # 选择系统
│   │   │   ├── SelectionFilterService.cs
│   │   │   ├── SelectionFilterBuilder.cs
│   │   │   ├── SelectionHelper.cs
│   │   │   ├── AdvancedSelectionService.cs
│   │   │   ├── FilterManagerService.cs
│   │   │   ├── Filters/                 # 过滤器
│   │   │   │   ├── IEntityFilter.cs
│   │   │   │   ├── TypeFilter.cs
│   │   │   │   ├── LayerFilter.cs
│   │   │   │   ├── ColorFilter.cs
│   │   │   │   ├── LinetypeFilter.cs
│   │   │   │   ├── LineWeightFilter.cs
│   │   │   │   └── TransparencyFilter.cs
│   │   │   └── EntityFilters/
│   │   │       └── EntityPropertyFilters.cs
│   │   │
│   │   ├── Services/                    # AutoCAD 服务
│   │   │   ├── DatabaseService.cs       # 数据库服务
│   │   │   ├── EditorService.cs         # 编辑器服务
│   │   │   ├── DrawingService.cs        # 绘图服务
│   │   │   ├── SelectionService.cs      # 选择服务
│   │   │   ├── LayerService.cs          # 图层服务
│   │   │   ├── LayerManager.cs          # 图层管理
│   │   │   ├── StyleService.cs          # 样式服务
│   │   │   ├── InputService.cs          # 输入服务
│   │   │   ├── AutoCadGeometryService.cs # 几何服务
│   │   │   ├── AutoCadIntersectionService.cs # 交点服务
│   │   │   ├── AutoCadPolygonOffsetService.cs # 偏移服务
│   │   │   ├── CurveSegmentExtractor.cs # 曲线段提取
│   │   │   ├── CurveSegmentService.cs   # 曲线段服务
│   │   │   ├── DCELRenderer.cs          # DCEL 渲染
│   │   │   ├── Geometry3DBuilder.cs     # 3D 几何构建
│   │   │   ├── Solid3DBuilder.cs        # 3D 实体构建
│   │   │   ├── WallBuilder.cs           # 墙体构建
│   │   │   ├── PolygonAdapter.cs        # 多边形适配
│   │   │   ├── ReinService.cs           # 钢筋服务
│   │   │   ├── MarkerLayerService.cs    # 标记图层
│   │   │   ├── TransientMarkerService.cs # 临时标记
│   │   │   ├── PileDrawingService.cs    # 桩绘制服务
│   │   │   └── Cluster/                 # 聚类服务
│   │   │       ├── ClusterInputService.cs
│   │   │       ├── ClusterFactoryService.cs
│   │   │       ├── ClusterDrawService.cs
│   │   │       ├── AxisAnalysisService.cs
│   │   │       └── DimensionService.cs
│   │   │
│   │   ├── Utilities/                   # 工具类
│   │   │   ├── EntityHelper.cs
│   │   │   ├── LayerHelper.cs
│   │   │   ├── PointComparers.cs
│   │   │   └── TransactionHelper.cs
│   │   │
│   │   └── Workflows/                   # 工作流
│   │       ├── ElevationDataExtractor.cs
│   │       ├── SlabGenerationService.cs
│   │       └── WallGenerationService.cs
│   │
│   └── Configuration/                   # 配置管理
│       ├── AutofacModule.cs             # 依赖注入配置
│       ├── ServiceLocator.cs            # 服务定位器
│       ├── ConfigurationService.cs      # 配置服务
│       ├── GlobalConfigurationService.cs # 全局配置
│       ├── ModuleConfigurationService.cs # 模块配置
│       ├── JsonConfigurationLoader.cs   # JSON 加载器
│       └── CsvConfigurationLoader.cs    # CSV 加载器
│
├── Presentation/                        # 表示层
│   ├── Commands/                        # AutoCAD 命令
│   │   ├── Base/                        # 命令基类
│   │   │   ├── ICommand.cs
│   │   │   ├── CommandExecutor.cs
│   │   │   └── CommandResult.cs
│   │   │
│   │   ├── ShowPanelCommand.cs          # 面板命令
│   │   ├── DrawPilesCommand.cs          # 绘制桩命令
│   │   ├── GroupCirclesByElevationCommand.cs # 圆分组标注
│   │   ├── PileVoronoiOptimizationCommand.cs # Voronoi 优化
│   │   ├── BreakCurvesCommand.cs        # 曲线打断
│   │   ├── OverKillCommand.cs           # 去重命令
│   │   ├── JoinParallelLinesCommand.cs  # 平行线连接
│   │   ├── DrawOffsetPolylineCommand.cs # 偏移多段线
│   │   ├── DCELCommand.cs               # DCEL 命令
│   │   ├── DCELSettingsCommand.cs       # DCEL 设置
│   │   ├── Elevation3DCommand.cs        # 标高 3D
│   │   ├── SurfaceBasedElevation3DCommand.cs # 曲面标高
│   │   ├── LocateTifCommand.cs          # TIF 定位
│   │   ├── MBRCommand.cs                # 最小外接矩形
│   │   ├── HyovSettings.cs              # HYOV 设置
│   │   ├── DrawReinforcementCommand.cs  # 绘制钢筋
│   │   ├── ReinExtendCommand.cs         # 钢筋延伸
│   │   ├── ReinQuickExtendCommand.cs    # 钢筋快速延伸
│   │   ├── ReinCutCommand.cs            # 钢筋切断
│   │   ├── ReinAddAnchorCommand.cs      # 添加锚固
│   │   ├── MleaderReinCommand.cs        # 引线钢筋
│   │   ├── rode/                        # 地脚螺栓命令
│   │   │   └── ...（4 个命令）
│   │   └── Test*.cs                     # 测试命令（8 个）
│   │
│   ├── ViewModels/                      # 视图模型
│   │   ├── SettingsPanelViewModel.cs    # 设置面板 VM
│   │   ├── PilePanelViewModel.cs        # 桩面板 VM
│   │   ├── ClusterPanelViewModel.cs     # 聚类面板 VM
│   │   ├── FilterPanelViewModel.cs      # 过滤面板 VM
│   │   ├── ReinPanelViewModel.cs        # 钢筋面板 VM（已排除）
│   │   ├── BaseReinPanelViewModel.cs    # 基础钢筋 VM（已排除）
│   │   └── RelayCommand.cs              # 命令辅助类
│   │
│   ├── Views/                           # WPF 视图
│   │   ├── SettingsPanel.xaml           # 设置面板
│   │   ├── PilePanel.xaml               # 桩面板
│   │   ├── ClusterPanel.xaml            # 聚类面板
│   │   ├── FilterPanel.xaml             # 过滤面板
│   │   ├── HyovSettingsWindow.xaml      # HYOV 设置窗口
│   │   ├── ReinPanel.xaml               # 钢筋面板（已排除）
│   │   ├── BaseReinPanel.xaml           # 基础钢筋面板（已排除）
│   │   ├── Converters/                  # 转换器
│   │   │   ├── BoolToVisibilityConverter.cs
│   │   │   ├── EnumDescriptionConverter.cs
│   │   │   ├── EnumToIndexConverter.cs
│   │   │   └── InverseBoolConverter.cs
│   │   └── Helpers/
│   │       └── TextBoxHelper.cs         # 文本框辅助
│   │
│   ├── Resources/                       # 资源
│   │   └── LibraryResources.xaml        # 共享资源
│   │
│   ├── PanelManager.cs                  # 面板管理器
│   └── PluginInitializer.cs             # 插件初始化
│
├── Test/                                # 测试
│   ├── TestCommand.cs                   # 测试入口（C1 命令）
│   ├── TestRunner.cs                    # 测试运行器
│   └── SimpleLogger.cs                  # 简单日志
│
├── Properties/
│   └── AssemblyInfo.cs                  # 程序集信息
│
├── config.json                          # 配置文件
├── HyCADTool.Refactored.csproj          # 项目文件
└── Docs/                                # 文档
    └── ...（14 个文档）
```

### 3.2 文件统计

| 层级 | 文件数 | 说明 |
|---|---|---|
| **Domain** | 78 | 平台无关的领域逻辑 |
| **Infrastructure** | 87 | AutoCAD 平台适配 |
| **Presentation** | 40+ | 命令 + UI |
| **Test** | 3 | 测试框架 |
| **总计** | 208+ | C# 代码文件 |

---

## 4. 核心模块说明

### 4.1 Domain 层核心模块

#### 4.1.1 几何值对象 (ValueObjects/Geometry)

**用途**：平台无关的几何表示

| 类名 | 说明 | 关键方法 |
|---|---|---|
| `Point2D` | 二维点 | `DistanceTo()`, `Equals()` |
| `Point3D` | 三维点 | `DistanceTo()`, `Project()` |
| `Vector2D` | 二维向量 | `Normalize()`, `Dot()`, `Cross()` |
| `Line2D` | 二维线段 | `Length`, `Direction`, `Intersect()` |
| `Polygon2D` | 二维多边形 | `Area`, `Contains()`, `Offset()` |
| `Circle2D` | 二维圆 | `Radius`, `Center`, `Contains()` |

**设计原则**：
- 值对象（不可变）
- 零依赖（纯 C# 数学）
- 可序列化
- 可迁移到 Blender/Python

#### 4.1.2 几何算法服务 (Services/GeometryAlgorithms)

**用途**：平台无关的几何算法

| 服务名 | 说明 | 关键方法 |
|---|---|---|
| `PointAlgorithmService` | 点算法 | `FindClosest()`, `RemoveDuplicates()` |
| `LineAlgorithmService` | 线算法 | `Intersect()`, `Parallel()`, `Perpendicular()` |
| `PolygonAlgorithmService` | 多边形算法 | `Offset()`, `Union()`, `Intersection()` |
| `ConvexHullAlgorithm` | 凸包算法 | `GrahamScan()`, `JarvisMarch()` |
| `ClusteringService` | 聚类算法 | `DBSCAN()`, `KMeans()` |
| `DCELBuilderService` | DCEL 构建 | `BuildFromLines()`, `BuildFromPolygon()` |

**设计原则**：
- 单一职责
- 无副作用（纯函数）
- 可单元测试
- 可迁移到 Blender

#### 4.1.3 数据结构 (DataStructures/DCEL)

**用途**：双连边数据结构（Doubly Connected Edge List）

| 类名 | 说明 | 用途 |
|---|---|---|
| `DCELGraph` | DCEL 图 | 拓扑分析、面识别 |
| `Vertex` | 顶点 | 存储坐标、出边 |
| `HalfEdge` | 半边 | 存储边关系、方向 |
| `Face` | 面 | 存储边界、面积 |

**应用场景**：
- 多边形拓扑分析
- 面识别与分类
- 墙体连接检测
- 空间分割

#### 4.1.4 领域服务 (Services)

**用途**：业务逻辑算法

| 服务名 | 说明 | 用途 |
|---|---|---|
| `PileLayoutService` | 桩布置服务 | 桩位计算、布置优化 |
| `VoronoiOptimizationService` | Voronoi 优化 | Lloyd 算法、桩位优化 |
| `CurveBreakService` | 曲线打断 | 交点打断、自交处理 |
| `CurveSimplificationService` | 曲线简化 | Douglas-Peucker 算法 |
| `LineOverKillService` | 线段去重 | 重叠检测、合并 |
| `PolygonMerger` | 多边形合并 | 相邻多边形合并 |
| `Elevation3DCalculator` | 标高计算 | 3D 坐标计算 |
| `ElevationParser` | 标高解析 | 文字解析、格式化 |

### 4.2 Infrastructure 层核心模块

#### 4.2.1 AutoCAD 服务 (AutoCAD/Services)

**用途**：封装 AutoCAD API

| 服务名 | 说明 | 关键方法 |
|---|---|---|
| `DatabaseService` | 数据库操作 | `GetModelSpace()`, `AddEntity()` |
| `EditorService` | 编辑器操作 | `GetSelection()`, `Prompt()` |
| `DrawingService` | 绘图操作 | `DrawLine()`, `DrawCircle()` |
| `SelectionService` | 选择操作 | `SelectAll()`, `SelectByFilter()` |
| `LayerService` | 图层操作 | `CreateLayer()`, `SetCurrent()` |
| `StyleService` | 样式操作 | `CreateTextStyle()`, `CreateDimStyle()` |
| `InputService` | 用户输入 | `GetPoint()`, `GetString()`, `GetKeyword()` |

**设计原则**：
- 封装 AutoCAD API
- 异常处理
- Transaction 管理
- 资源释放

#### 4.2.2 扩展方法 (AutoCAD/Extensions)

**用途**：流畅接口设计

| 扩展类 | 说明 | 示例 |
|---|---|---|
| `EntityExtensions` | 实体扩展 | `entity.ToSpace()`, `entity.CreateSolidCircle()` |
| `PolylineExtensions` | 多段线扩展 | `pline.GetVertices()`, `pline.Simplify()` |
| `Point3dExtensions` | 点扩展 | `pt.DistanceTo()`, `pt.Transform()` |
| `MLeaderExtensions` | 引线扩展 | `AddMleader()`, `CreateMLeaderSinglePoint()` |
| `FilterExtensions` | 过滤扩展 | `filter.OfType()`, `filter.OnLayer()` |

**设计原则**：
- 流畅接口
- 链式调用
- 可读性优先

#### 4.2.3 转换器 (AutoCAD/Converters)

**用途**：Domain ↔ AutoCAD 转换

| 转换器 | 说明 | 方法 |
|---|---|---|
| `GeometryConverter` | 几何转换 | `ToPoint2D()`, `ToLine2D()`, `ToPolygon2D()` |
| `ReinforcementConverter` | 钢筋转换 | `ToReinforcement()`, `ToPolyline()` |

**转换方向**：
- **Domain → AutoCAD**：绘图时使用
- **AutoCAD → Domain**：算法计算时使用

#### 4.2.4 选择系统 (AutoCAD/Selection)

**用途**：高级选择过滤

| 组件 | 说明 | 用途 |
|---|---|---|
| `SelectionFilterService` | 过滤服务 | 构建选择过滤器 |
| `SelectionFilterBuilder` | 过滤构建器 | 流畅接口构建 |
| `AdvancedSelectionService` | 高级选择 | 复杂选择逻辑 |
| `FilterManagerService` | 过滤管理 | 过滤器管理 |
| `TypeFilter` | 类型过滤 | 按实体类型过滤 |
| `LayerFilter` | 图层过滤 | 按图层过滤 |
| `ColorFilter` | 颜色过滤 | 按颜色过滤 |

**使用示例**：
```csharp
var filter = new SelectionFilterBuilder()
    .OfType<Line>()
    .OnLayer("0")
    .WithColor(Color.Red)
    .Build();
```

### 4.3 Presentation 层核心模块

#### 4.3.1 命令系统 (Commands)

**用途**：AutoCAD 命令实现

**命令分类**：

| 类别 | 命令数 | 代表命令 |
|---|---|---|
| **面板命令** | 6 | `ShowPanelCommand` |
| **绘图命令** | 8 | `DrawPilesCommand`, `DrawReinforcementCommand` |
| **编辑命令** | 6 | `BreakCurvesCommand`, `OverKillCommand` |
| **分析命令** | 5 | `DCELCommand`, `MBRCommand` |
| **测试命令** | 8 | `TestCommand`, `TestServiceCommand` |

**命令基类**：
```csharp
public interface ICommand
{
    void Execute();
}
```

#### 4.3.2 面板系统 (Views + ViewModels)

**用途**：WPF 用户界面

| 面板 | 说明 | 功能 |
|---|---|---|
| `SettingsPanel` | 设置面板 | 全局配置、样式管理 |
| `PilePanel` | 桩面板 | 桩布置、Voronoi 优化 |
| `ClusterPanel` | 聚类面板 | 圆聚类、标注 |
| `FilterPanel` | 过滤面板 | 高级选择过滤 |
| `HyovSettingsWindow` | HYOV 设置 | HYOV 命令配置 |

**MVVM 模式**：
- **View**：XAML 界面
- **ViewModel**：业务逻辑 + 数据绑定
- **Model**：Domain 层实体

#### 4.3.3 面板管理器 (PanelManager)

**用途**：统一管理所有面板

**功能**：
- 面板创建
- 面板显示/隐藏
- 面板单例管理
- 面板生命周期

**使用示例**：
```csharp
var panelManager = ServiceLocator.Resolve<PanelManager>();
panelManager.ShowSettingsPanel();
```

---

## 5. 依赖关系图

### 5.1 层级依赖

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │  Commands    │  │  ViewModels  │  │    Views     │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
│         │                 │                 │               │
└─────────┼─────────────────┼─────────────────┼───────────────┘
          │                 │                 │
          ▼                 ▼                 ▼
┌─────────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                        │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Services   │  │  Extensions  │  │  Converters  │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
│         │                 │                 │               │
└─────────┼─────────────────┼─────────────────┼───────────────┘
          │                 │                 │
          ▼                 ▼                 ▼
┌─────────────────────────────────────────────────────────────┐
│                      Domain Layer                            │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │  Entities    │  │ ValueObjects │  │   Services   │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 模块依赖

```
ShowPanelCommand
    ├─→ PanelManager
    │   ├─→ SettingsPanelViewModel
    │   │   ├─→ StyleService (Infrastructure)
    │   │   └─→ ReinParameters (Domain)
    │   └─→ PilePanelViewModel
    │       ├─→ PileLayoutService (Domain)
    │       └─→ VoronoiOptimizationService (Domain)
    └─→ ServiceLocator

GroupCirclesByElevationCommand
    ├─→ SelectionService (Infrastructure)
    ├─→ DrawingService (Infrastructure)
    ├─→ LayerService (Infrastructure)
    ├─→ MLeaderExtensions (Infrastructure)
    ├─→ ElevationParser (Domain)
    └─→ ClusteringService (Domain)

PileVoronoiOptimizationCommand
    ├─→ PolylineHelper (Infrastructure)
    ├─→ DrawingService (Infrastructure)
    ├─→ VoronoiOptimizationService (Domain)
    └─→ PileLayoutService (Domain)

DCELCommand
    ├─→ SelectionService (Infrastructure)
    ├─→ DCELRenderer (Infrastructure)
    ├─→ DCELBuilderService (Domain)
    └─→ DCELGraph (Domain)
```

### 5.3 依赖注入容器 (Autofac)

**注册位置**：`Infrastructure/Configuration/AutofacModule.cs`

**注册阶段**：

| 阶段 | 说明 | 服务数 |
|---|---|---|
| **阶段 0** | 基础服务 | 5 |
| **阶段 1** | 配置服务 | 3 |
| **阶段 2** | 核心服务 | 3 |
| **阶段 3** | 几何服务 | 8 |
| **阶段 4** | 算法服务 | 10 |
| **阶段 5** | UI 服务 | 5 |
| **阶段 6-14** | 业务服务 | 20+ |

**服务生命周期**：
- **SingleInstance**：单例（大部分服务）
- **InstancePerDependency**：每次创建（少数）

---

## 6. 关键技术栈

### 6.1 核心依赖

| 依赖包 | 版本 | 用途 |
|---|---|---|
| **AutoCAD.NET** | 24.3.0 | AutoCAD API |
| **Autofac** | 7.1.0 | 依赖注入 |
| **NetTopologySuite** | 2.5.0 | 几何算法（Voronoi） |
| **Clipper2** | 1.4.0 | 多边形偏移 |
| **Newtonsoft.Json** | 13.0.3 | JSON 配置 |

### 6.2 技术选型理由

#### 6.2.1 Autofac（依赖注入）

**优势**：
- 强大的容器功能
- 支持模块化注册
- 生命周期管理
- 属性注入

**使用场景**：
- 服务注册
- 接口解耦
- 单例管理

#### 6.2.2 NetTopologySuite（几何算法）

**优势**：
- 成熟的几何库
- Voronoi 图生成
- 拓扑分析
- 空间索引

**使用场景**：
- Voronoi 优化
- 桩位布置
- 多边形运算

#### 6.2.3 Clipper2（多边形偏移）

**优势**：
- 高性能偏移算法
- 支持复杂多边形
- 布尔运算
- 精确计算

**使用场景**：
- 多边形偏移
- 墙体缓冲
- 保护层计算

---

## 7. 开发工作流

### 7.1 热重启机制

**核心命令**：

| 命令 | 说明 | 实现位置 |
|---|---|---|
| **C2** | 热重启 | `ReCall/Recall.cs` |
| **C1** | 执行测试 | `Test/TestCommand.cs` |

**工作流程**：

```
┌─────────────────────────────────────────────────────────────┐
│  Step 1: 修改代码                                            │
│  - 修改 Presentation/Commands/*.cs                          │
│  - 修改 Domain/Services/*.cs                                │
│  - 修改 Infrastructure/AutoCAD/Services/*.cs                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 2: Visual Studio 编译                                  │
│  - Ctrl+Shift+B                                             │
│  - 生成 bin/Debug/HyCADTool.Refactored.dll                  │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 3: AutoCAD 执行 C2                                     │
│  - 复制 DLL 到 temp 目录                                     │
│  - 卸载旧 DLL                                                │
│  - 加载新 DLL                                                │
│  - 重新初始化 Autofac 容器                                   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 4: AutoCAD 执行 C1                                     │
│  - 执行 TestCommand.Run()                                   │
│  - 调用 Test/TestCommand.cs 中配置的命令                    │
│  - 输出耗时（SimpleLogger）                                 │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Step 5: 检查结果                                            │
│  - 查看命令行输出                                            │
│  - 检查 AutoCAD 图形                                         │
│  - 验证功能正确性                                            │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
                    重复 1-5
```

### 7.2 测试入口配置

**文件**：`Test/TestCommand.cs`

**配置方式**：
```csharp
public void Run()
{
    // 只改这一行即可切换测试命令
    new Presentation.Commands.OverKillCommand().Execute();
    
    // 例如：
    // new Presentation.Commands.BreakCurvesCommand().Execute();
    // new Presentation.Commands.GroupCirclesByElevationCommand().Execute();
    // new Presentation.Commands.PileVoronoiOptimizationCommand().Execute();
}
```

**规则**：
- **只改一行**：切换测试命令
- **不改 TestRunner**：保持框架稳定
- **不改 ReCall**：保持热重启稳定

### 7.3 日志输出

**文件**：`Test/SimpleLogger.cs`

**使用方式**：
```csharp
SimpleLogger.LogElapsedTime("命令名称", () =>
{
    // 命令逻辑
});
```

**输出格式**：
```
[命令名称] 耗时: 123.45 ms
```

**输出规则**：
- **常用命令**：1-3 行
- **循环内**：禁止输出
- **调试信息**：仅开发时输出

---

## 8. 命令清单

### 8.1 已完成命令（29 个）

#### 8.1.1 图形处理命令

| 命令 | 说明 | 文件 |
|---|---|---|
| `HYOV` | 线段去重 | `OverKillCommand.cs` |
| `HYBC` | 曲线打断 | `BreakCurvesCommand.cs` |
| `HYDCEL` | DCEL 分析 | `DCELCommand.cs` |
| `HYMBR` | 最小外接矩形 | `MBRCommand.cs` |
| `JoinParallelLines` | 平行线连接 | `JoinParallelLinesCommand.cs` |
| `DrawOffsetPolyline` | 偏移多段线 | `DrawOffsetPolylineCommand.cs` |

#### 8.1.2 桩命令

| 命令 | 说明 | 文件 |
|---|---|---|
| `DrawPiles` | 绘制桩 | `DrawPilesCommand.cs` |
| `GroupCirclesByElevation` | 圆分组标注 | `GroupCirclesByElevationCommand.cs` |
| `PileVoronoiOptimization` | Voronoi 优化 | `PileVoronoiOptimizationCommand.cs` |

#### 8.1.3 三维建模命令

| 命令 | 说明 | 文件 |
|---|---|---|
| `C15` | 标高 3D | `Elevation3DCommand.cs` |
| `SurfaceBasedElevation3D` | 曲面标高 3D | `SurfaceBasedElevation3DCommand.cs` |

#### 8.1.4 钢筋命令

| 命令 | 说明 | 文件 |
|---|---|---|
| `DrawReinforcement` | 绘制钢筋 | `DrawReinforcementCommand.cs` |
| `ReinExtend` | 钢筋延伸 | `ReinExtendCommand.cs` |
| `ReinQuickExtend` | 快速延伸 | `ReinQuickExtendCommand.cs` |
| `ReinCut` | 钢筋切断 | `ReinCutCommand.cs` |
| `ReinAddAnchor` | 添加锚固 | `ReinAddAnchorCommand.cs` |
| `MleaderRein` | 引线钢筋 | `MleaderReinCommand.cs` |

#### 8.1.5 面板命令

| 命令 | 说明 | 文件 |
|---|---|---|
| `HYSET` | 设置面板 | `ShowPanelCommand.ShowSettingsPanel()` |
| `HYPILE` | 桩面板 | `ShowPanelCommand.ShowPilePanel()` |
| `HYCLUSTER` | 聚类面板 | `ShowPanelCommand.ShowClusterPanel()` |
| `HYFILTER` | 过滤面板 | `ShowPanelCommand.ShowFilterPanel()` |

#### 8.1.6 工具命令

| 命令 | 说明 | 文件 |
|---|---|---|
| `HYLOCATETIF` | TIF 定位 | `LocateTifCommand.cs` |
| `DCELSettings` | DCEL 设置 | `DCELSettingsCommand.cs` |
| `HyovSettings` | HYOV 设置 | `HyovSettings.cs` |

#### 8.1.7 测试命令

| 命令 | 说明 | 文件 |
|---|---|---|
| `TestConfig` | 配置测试 | `TestConfigCommand.cs` |
| `TestOffset` | 偏移测试 | `TestOffsetCommand.cs` |
| `TestDomainAlgorithms` | 算法测试 | `TestDomainAlgorithmsCommand.cs` |
| `TestService` | 服务测试 | `TestServiceCommand.cs` |
| `TestPanel` | 面板测试 | `TestPanelCommand.cs` |
| `TestArcSimplify` | 弧简化测试 | `TestArcSimplifyCommand.cs` |

### 8.2 待迁移高优先级命令

#### 8.2.1 钢筋绘制（⭐⭐⭐⭐⭐）

| 旧命令 | 说明 | 优先级 |
|---|---|---|
| `gj` | 钢筋绘制主命令 | P0 |
| `g1` | 单根钢筋 | P0 |
| `g2` | 双根钢筋 | P0 |
| `gb` | 钢筋编号 | P0 |
| `gp` | 钢筋排布 | P1 |
| `gd` | 点钢筋 | P1 |

#### 8.2.2 基础配筋（⭐⭐⭐⭐⭐）

| 旧命令 | 说明 | 优先级 |
|---|---|---|
| `hyb1` | 基础配筋 1 | P0 |
| `hyb2` | 基础配筋 2 | P0 |
| `hyb3` | 基础配筋 3 | P0 |
| `hyb4` | 基础配筋 4 | P1 |
| `hyb5` | 基础配筋 5 | P1 |
| `hyb6` | 基础配筋 6 | P1 |

#### 8.2.3 尺寸标注（⭐⭐⭐⭐⭐）

| 旧命令 | 说明 | 优先级 |
|---|---|---|
| `ddss` | 双向标注 | P0 |
| `dds` | 单向标注 | P0 |
| `dda` | 角度标注 | P1 |
| `ddr` | 半径标注 | P1 |

#### 8.2.4 地脚螺栓（⭐⭐⭐⭐）

| 旧命令 | 说明 | 优先级 |
|---|---|---|
| `hyab` | 地脚螺栓主命令 | P1 |
| `hyabC` | 地脚螺栓 C 型 | P1 |
| `hyabL` | 地脚螺栓 L 型 | P2 |
| `hyabJ` | 地脚螺栓 J 型 | P2 |

---

## 9. 配置系统

### 9.1 配置文件

**文件**：`config.json`

**结构**：
```json
{
  "Global": {
    "Scale": 40,
    "Tolerance": 0.001,
    "Layers": { ... },
    "Styles": { ... }
  },
  "Modules": {
    "Elevation": { ... },
    "Foundation": { ... },
    "Pile": { ... },
    "Reinforcement": { ... }
  }
}
```

### 9.2 配置加载流程

```
config.json
    ↓ JsonConfigurationLoader
GlobalConfiguration (Domain)
    ↓ GlobalConfigurationService
IConfigurationService
    ↓ Autofac 注入
Commands / Services
```

### 9.3 配置访问方式

**方式 1：依赖注入**
```csharp
public class MyCommand
{
    private readonly IConfigurationService _config;
    
    public MyCommand(IConfigurationService config)
    {
        _config = config;
    }
    
    public void Execute()
    {
        var scale = _config.GetGlobalConfig().Scale.Value;
    }
}
```

**方式 2：ServiceLocator**
```csharp
var config = ServiceLocator.Resolve<IConfigurationService>();
var scale = config.GetGlobalConfig().Scale.Value;
```

**方式 3：ViewModel（面板）**
```csharp
var scale = SettingsPanelViewModel.Current.Scale;
```

### 9.4 配置优先级

1. **面板配置**（最高优先级）：`SettingsPanelViewModel.Current`
2. **config.json**：全局配置
3. **代码默认值**（最低优先级）

---

## 10. 测试体系

### 10.1 测试框架

**核心文件**：
- `Test/TestCommand.cs`：测试入口（C1 命令）
- `Test/TestRunner.cs`：测试运行器
- `Test/SimpleLogger.cs`：简单日志

### 10.2 测试流程

```
┌─────────────────────────────────────────────────────────────┐
│  1. 配置测试命令                                             │
│     - 修改 TestCommand.cs 的 Run() 方法                     │
│     - 指定要测试的命令                                       │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  2. 编译项目                                                 │
│     - Visual Studio: Ctrl+Shift+B                           │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  3. 热重启                                                   │
│     - AutoCAD: C2                                           │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  4. 执行测试                                                 │
│     - AutoCAD: C1                                           │
│     - 查看输出和耗时                                         │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  5. 验证结果                                                 │
│     - 检查命令行输出                                         │
│     - 检查 AutoCAD 图形                                      │
│     - 验证功能正确性                                         │
└─────────────────────────────────────────────────────────────┘
```

### 10.3 测试示例

**测试圆分组标注命令**：

```csharp
// Test/TestCommand.cs
public void Run()
{
    new Presentation.Commands.GroupCirclesByElevationCommand().Execute();
}
```

**测试步骤**：
1. 在 AutoCAD 中绘制若干圆和文字
2. C2 重载插件
3. C1 执行测试
4. 检查圆是否按标高分组到不同图层
5. 检查引线标注是否正确

### 10.4 单元测试（待建立）

**计划**：
- 创建 `HyCADTool.Refactored.Tests` 项目
- 使用 xUnit 或 NUnit
- 重点测试 Domain 层（100% 平台无关）

**测试覆盖**：
- 几何算法
- 领域服务
- 值对象
- 数据结构

---

## 附录 A：关键约束与规则

### A.1 开发约束

1. **热重载限制**：重构项目中不能使用 `[CommandMethod]`（会导致 eDuplicateKey）
2. **面板调用**：面板命令通过 `ShowPanelCommand` 调用，不使用 CommandMethod
3. **测试入口**：只修改 `TestCommand.cs`，不修改 `TestRunner` 和 `ReCall`
4. **输出规范**：常用命令 1-3 行，循环内禁止输出

### A.2 代码规范

1. **异常处理**：引用 AutoCAD 的 C# 里用 `System.Exception`
2. **命名规范**：
   - 命令：`XxxCommand.cs`
   - 服务：`XxxService.cs`
   - 扩展：`XxxExtensions.cs`
   - 接口：`IXxx.cs`
3. **注释规范**：
   - 类：XML 文档注释
   - 方法：XML 文档注释
   - 复杂逻辑：行内注释

### A.3 架构约束

1. **Domain 层**：零依赖，100% 平台无关
2. **Infrastructure 层**：只依赖 Domain，封装 AutoCAD API
3. **Presentation 层**：依赖 Domain + Infrastructure
4. **依赖注入**：所有服务通过 Autofac 注册

### A.4 坐标与单位规则

**模型空间坐标 = 实际 mm（1:1）**

**参数分类**：

**直接用（实际 mm）**：不乘 Scale
- `RebarDiameter`：钢筋直径（14mm）
- `AnchorageLength`：锚固长度（500mm）
- `DotSeparation`：点钢筋间距（200mm）
- `BendingLineMinLength`：弯折最小长度（150mm）
- `AnchorageJoinLength`：锚固连接长度（1500mm）
- `RebarSpacing`：钢筋间距（200mm）

**需 × Scale（缩放值）**：面板存的是 `实际mm / Scale`，用于几何时需 `值 × Scale`
- `ProtectionThickness`：保护层厚度（1.0 → 40mm）
- `HookLength`：弯钩长度（1.0 → 40mm）
- `ReinforcementDiameter`：点钢筋绘制直径（0.35 → 14mm）
- `DotReinOffset`：点钢筋偏移（1.35 → 54mm）

**Scale 还用于**：
- 文字/标注/引线样式符号大小（`TextSize × Scale`、`Dimscale`）

---

## 附录 B：常见问题

### B.1 编译问题

**问题**：编译失败，提示找不到 AutoCAD API

**解决**：
1. 检查 NuGet 包是否正确安装
2. 检查 AutoCAD.NET 版本是否为 24.3.0
3. 清理并重新生成项目

### B.2 热重启问题

**问题**：C2 后仍看到旧版本输出

**解决**：
1. Visual Studio 重新生成（Ctrl+Shift+B）
2. 检查 bin/Debug 目录是否有新 DLL
3. 再次执行 C2

### B.3 依赖注入问题

**问题**：ServiceLocator.Resolve() 返回 null

**解决**：
1. 检查 `AutofacModule.cs` 是否注册了该服务
2. 检查服务生命周期是否正确
3. 检查是否在 C2 后执行

### B.4 面板问题

**问题**：面板无法显示

**解决**：
1. 检查 `.csproj` 文件中是否包含该 XAML
2. 检查 XAML 是否有编译错误
3. 检查 ViewModel 是否正确绑定

---

## 附录 C：参考文档

### C.1 项目文档

| 文档 | 位置 | 说明 |
|---|---|---|
| **AI 热启动模式** | `.cursor/rules/01-AI热启动模式.mdc` | 新对话必读 |
| **项目总览** | `.cursor/rules/02-项目总览与架构.md` | 项目全貌 |
| **命令清单** | `.cursor/rules/03-HyCADTool 命令详细清单.md` | 所有命令 |
| **迁移优先级** | `.cursor/rules/05-命令迁移优先级分析.md` | 优先级排序 |
| **桩命令报告** | `doc/01.md` | 桩命令重构报告 |

### C.2 技术文档

| 文档 | 位置 | 说明 |
|---|---|---|
| **DCEL 架构** | `Docs/HYLOCATETIF_ARCHITECTURE.md` | DCEL 架构设计 |
| **DCEL 快速参考** | `Docs/HYLOCATETIF_QUICK_REFERENCE.md` | DCEL 快速入门 |
| **DCEL 测试指南** | `Docs/HYLOCATETIF_TEST_GUIDE.md` | DCEL 测试 |
| **重构总结** | `Docs/DCEL-Refactoring-Summary.md` | DCEL 重构总结 |

### C.3 外部资源

| 资源 | 链接 | 说明 |
|---|---|---|
| **AutoCAD .NET API** | [Autodesk Developer Network](https://www.autodesk.com/developer-network/platform-technologies/autocad) | 官方文档 |
| **Autofac** | [autofac.org](https://autofac.org/) | 依赖注入 |
| **NetTopologySuite** | [GitHub](https://github.com/NetTopologySuite/NetTopologySuite) | 几何算法 |
| **Clipper2** | [GitHub](https://github.com/AngusJohnson/Clipper2) | 多边形偏移 |

---

## 附录 D：版本历史

| 版本 | 日期 | 说明 |
|---|---|---|
| **v1.0** | 2026-01-20 | 初始版本 |
| **v2.0** | 2026-01-26 | 更新架构、进度 |
| **v3.0** | 2026-02-09 | 完成桩命令重构 |
| **v3.1** | 2026-02-10 | 生成项目结构文档 |

---

## 结语

本文档详细梳理了 HyCADTool.Refactored 项目的完整结构，包括：

✅ **架构设计**：Clean Architecture + DDD  
✅ **目录结构**：三层分离，职责清晰  
✅ **核心模块**：Domain、Infrastructure、Presentation  
✅ **依赖关系**：层级依赖、模块依赖、依赖注入  
✅ **技术栈**：AutoCAD API、Autofac、NTS、Clipper2  
✅ **开发工作流**：热重启机制、测试流程  
✅ **命令清单**：已完成 29 个，待迁移 60+  
✅ **配置系统**：config.json、配置加载、优先级  
✅ **测试体系**：测试框架、测试流程、单元测试计划

**下一步**：
1. 继续迁移高优先级命令（钢筋、基础配筋、尺寸标注）
2. 建立单元测试项目
3. 完善文档和示例

---

**文档生成时间**：2026-02-10  
**文档版本**：v1.0  
**作者**：AI Assistant  
**审核状态**：待用户审核
