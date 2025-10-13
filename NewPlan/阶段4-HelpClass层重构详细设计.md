# 阶段 4: HelpClass 层重构详细设计

> **规划日期**: 2025-10-13  
> **状态**: 进行中  
> **目标**: 迁移 HelpClass 层到 Clean Architecture 架构

---

## 📋 目录

1. [阶段目标](#阶段目标)
2. [HelpClass 目录分析](#helpclass-目录分析)
3. [重构策略](#重构策略)
4. [详细拆分方案](#详细拆分方案)
5. [实施计划](#实施计划)

---

## 阶段目标

### 核心目标

1. **迁移 HelpClass 辅助类到新架构**
   - 提取平台无关逻辑到 Domain 层
   - AutoCAD 特定代码放 Infrastructure 层
   - 保持功能完整性

2. **遵循 Clean Architecture 原则**
   - Domain 层：纯算法、数据结构（平台无关）
   - Infrastructure 层：AutoCAD 交互、Jig、绘制

3. **为后续阶段做准备**
   - 为阶段 5（核心工具层）提供基础
   - 建立可复用的辅助类库

---

## HelpClass 目录分析

### 目录结构树

```
HyCADtool/HelpClass/
├── CAD/                        # CAD 通用辅助类
│   ├── TypeConverter.cs        # 类型名称转换（DXF ↔ 中文）
│   └── Comparer.cs             # 点比较器（Point3d, Point2d, Coordinate）
│
├── DCEL/                       # 拓扑数据结构
│   ├── DCEL.cs                 # 双连接边表（Vertex, HalfEdge, Face）
│   ├── DcelDraw.cs             # DCEL 绘制
│   └── DCELFactory.cs          # DCEL 工厂
│
├── Jig/                        # 交互式绘图
│   ├── PolylineJig.cs          # 多段线 Jig（带偏移预览）
│   ├── HookJig.cs              # 弯钩 Jig（钢筋弯钩交互）
│   └── HookJigSeg.cs           # 分段弯钩 Jig
│
├── ElevationSymbol/            # 标高符号系统
│   ├── ElevationSymbol.cs      # 标高符号 Jig + 绘制
│   └── IElevationSymbol.cs     # 接口
│
├── Elevation3d/                # 三维标高模型生成器（39 文件）
│   ├── ElevationModelGenerator.cs
│   ├── GeometryData.cs
│   ├── WallData.cs
│   ├── CrossSection.cs
│   └── bf/                     # 备份文件（待清理）
│
├── Pile/                       # 桩基相关（22 文件）
│   ├── BasePile.cs             # 基础桩类（枚举 + 面积计算）
│   ├── PileConfig.cs           # 桩配置
│   ├── MinArea.cs              # 最小面积计算
│   └── StandardArea/           # 标准面积（包含多个备份）
│
├── TitleBlock/                 # 图框相关
│   ├── LayoutPacking.cs        # 布局打包
│   └── TitleBlock.cs           # 图框
│
└── 其他
    ├── Rec.cs                  # 矩形移动避让算法
    ├── RecT.cs                 # 矩形类型定义
    └── RowCols.cs              # 行列值泛型类
```

---

### 模块分类与优先级

| 模块 | 文件数 | 复杂度 | 优先级 | 依赖关系 | 说明 |
|------|--------|--------|--------|----------|------|
| **CAD 通用类** | 2 | ⭐ | P0 | 无 | 简单工具类 |
| **简单数据类** | 2 | ⭐ | P0 | 无 | Rec.cs, RowCols.cs |
| **DCEL** | 3 | ⭐⭐ | P1 | 无 | 纯数据结构 |
| **Jig** | 3 | ⭐⭐⭐ | P1 | 6 个命令 | AutoCAD Jig 机制 |
| **ElevationSymbol** | 2 | ⭐⭐⭐ | P2 | 4 个命令 | 标高符号系统 |
| **Pile** | 22 | ⭐⭐⭐⭐ | P2 | 桩基命令 | 复杂算法 |
| **Elevation3D** | 39 | ⭐⭐⭐⭐⭐ | P3 | 2 个命令 | 暂缓（复杂） |
| **TitleBlock** | 2 | ⭐⭐ | P3 | 图框命令 | 暂缓 |

---

## 重构策略

### 分层原则

#### Domain 层（平台无关）

**应该放到 Domain 层的内容**:
- ✅ 纯数据结构（DCEL 的 Vertex, HalfEdge, Face）
- ✅ 枚举类型（PileSectionType, PileType）
- ✅ 纯算法（面积计算、几何算法）
- ✅ 值对象（RowColValue）
- ✅ 比较器接口（可移植到 Python）

**示例**:
```csharp
// Domain/DataStructures/DCEL/
- Vertex.cs
- HalfEdge.cs  
- Face.cs
- DCELGraph.cs

// Domain/ValueObjects/
- RowColValue.cs

// Domain/Enums/
- PileSectionType.cs
- PileType.cs
```

---

#### Infrastructure 层（AutoCAD 特定）

**应该放到 Infrastructure 层的内容**:
- ✅ Jig 类（继承自 DrawJig, EntityJig）
- ✅ AutoCAD 类型转换（TypeConverter）
- ✅ AutoCAD 点比较器（Point3dEqualityComparer）
- ✅ 绘制器（DcelDraw, ElevationSymbolDrawer）
- ✅ 工厂类（与 AutoCAD 对象创建相关）

**示例**:
```csharp
// Infrastructure/AutoCAD/Interactive/
- PolylineJig.cs
- HookJig.cs
- ElevationSymbolJig.cs

// Infrastructure/AutoCAD/Utilities/
- TypeConverter.cs
- Point3dEqualityComparer.cs

// Infrastructure/AutoCAD/Drawing/
- DcelDrawer.cs
```

---

### 重构步骤模板

每个模块的重构流程：

```
1. 分析原代码
   ├── 识别依赖关系
   ├── 区分平台无关 vs AutoCAD 特定
   └── 确定拆分边界

2. 提取 Domain 层
   ├── 创建纯数据结构
   ├── 提取纯算法
   └── 定义接口

3. 创建 Infrastructure 层
   ├── 实现 AutoCAD 交互
   ├── 创建扩展方法
   └── 桥接 Domain 和 AutoCAD

4. 验证
   ├── 编译通过
   ├── 创建测试
   └── 功能验证
```

---

## 详细拆分方案

### 4.1 CAD 通用类（优先级 P0）

#### TypeConverter（类型名称转换）

**原位置**: `HelpClass/CAD/TypeConverter.cs`

**功能分析**:
- 提供 DXF 标签 ↔ 中文名称 的双向映射
- 静态字典 + 扩展方法
- 100% 平台无关（只是字符串映射）

**重构方案**:

```
Domain/ValueObjects/EntityTypeMapping/
└── EntityTypeConverter.cs          # 类型映射（纯字典）

Infrastructure/AutoCAD/Utilities/
└── AutoCadTypeExtensions.cs        # AutoCAD 类型扩展方法
```

**代码示例**:

```csharp
// Domain/ValueObjects/EntityTypeMapping/EntityTypeConverter.cs
namespace HyCADTool.Refactored.Domain.ValueObjects.EntityTypeMapping
{
    /// <summary>
    /// 实体类型转换器（Entity Type Converter）
    /// 提供 DXF 标签与中文名称的双向映射
    /// </summary>
    public static class EntityTypeConverter
    {
        // DXF 标签 → 中文名称
        private static readonly Dictionary<string, string> TypeToChinese = new()
        {
            { "Line", "直线" },
            { "Polyline", "多段线" },
            { "Circle", "圆" },
            // ... 其他映射
        };

        // 中文名称 → DXF 标签
        private static readonly Dictionary<string, string> ChineseToType = new()
        {
            { "直线", "LINE" },
            { "多段线", "LWPOLYLINE" },
            // ... 其他映射
        };

        public static string ToChinese(string dxfType)
        {
            return TypeToChinese.TryGetValue(dxfType, out var chinese) 
                ? chinese 
                : dxfType;
        }

        public static string ToDxfType(string chinese)
        {
            return ChineseToType.TryGetValue(chinese, out var type) 
                ? type 
                : chinese;
        }
    }
}

// Infrastructure/AutoCAD/Extensions/EntityExtensions.cs
public static class EntityExtensions
{
    /// <summary>
    /// 获取实体的中文名称
    /// </summary>
    public static string GetChineseName(this Entity entity)
    {
        return EntityTypeConverter.ToChinese(entity.GetType().Name);
    }
}
```

---

#### Comparer（点比较器）

**原位置**: `HelpClass/CAD/Comparer.cs`

**功能分析**:
- Point3d, Point2d 的容差比较器
- Coordinate（NetTopologySuite）比较器
- AutoCAD 特定类型

**重构方案**:

```
Infrastructure/AutoCAD/Utilities/
├── Point3dEqualityComparer.cs      # Point3d 比较器
├── Point2dEqualityComparer.cs      # Point2d 比较器
└── CoordinateEqualityComparer.cs   # Coordinate 比较器
```

**保持原样**（已经很好的设计）:
- 使用接口 `IEqualityComparer<T>`
- 支持自定义容差
- 位置：Infrastructure 层（AutoCAD 特定）

---

### 4.2 简单数据类（优先级 P0）

#### RowCols.cs - 行列值

**原位置**: `HelpClass/RowCols.cs`

**功能分析**:
- 泛型类 `RowColValue<T>`
- 表示行、列、值的三元组
- 100% 平台无关

**重构方案**:

```
Domain/ValueObjects/Grid/
└── RowColValue.cs                  # 行列值对象
```

**代码示例**:

```csharp
// Domain/ValueObjects/Grid/RowColValue.cs
namespace HyCADTool.Refactored.Domain.ValueObjects.Grid
{
    /// <summary>
    /// 行列值对象（Row Column Value Object）
    /// 表示网格中某个单元格的位置和值
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    public class RowColValue<T>
    {
        public int Row { get; }
        public int Col { get; }
        public T Value { get; }

        public RowColValue(int row, int col, T value)
        {
            Row = row;
            Col = col;
            Value = value;
        }

        // 添加值对象的 Equals 和 GetHashCode
        public override bool Equals(object obj)
        {
            if (obj is RowColValue<T> other)
            {
                return Row == other.Row 
                    && Col == other.Col 
                    && EqualityComparer<T>.Default.Equals(Value, other.Value);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Row, Col, Value);
        }
    }
}
```

---

#### Rec.cs - 矩形移动避让算法

**原位置**: `HelpClass/Rec.cs`

**功能分析**:
- 矩形碰撞检测与自动避让算法
- 依赖 `Polyline` 和 `ZTools` 扩展方法
- 混合了算法逻辑和 AutoCAD 操作

**重构方案**:

```
Domain/Services/CollisionDetection/
├── RectangleCollisionAlgorithms.cs    # 矩形碰撞算法（纯算法）
└── RectangleAvoidanceService.cs       # 避让服务

Infrastructure/AutoCAD/Utilities/
└── PolylineCollisionExtensions.cs     # Polyline 碰撞扩展方法
```

**关键算法**（可提取到 Domain）:
- `GetMoveStopVector()` - 计算移动停止向量
- `GetBastMoveStopVector()` - 获取最佳移动方向
- `GetDirectionRange()` - 生成方向范围

**暂缓处理**（复杂度高，依赖较多）:
- 建议在阶段 5 完成后再处理
- 当前阶段：仅清理代码，不拆分

---

### 4.3 DCEL 拓扑数据结构（优先级 P1）

**原位置**: `HelpClass/DCEL/`

**功能分析**:
- **DCEL.cs**: 
  - 纯数据结构（Vertex, HalfEdge, Face, DCEL）
  - 拓扑关系管理
  - 100% 平台无关（只依赖 Point3d，可替换为 Point2D）
  
- **DcelDraw.cs**: 
  - AutoCAD 绘制逻辑
  - 使用 Polyline, Line 绘制 DCEL 结构

- **DCELFactory.cs**: 
  - 从 AutoCAD 实体构建 DCEL
  - AutoCAD 特定

**重构方案**:

```
Domain/DataStructures/DCEL/
├── Vertex.cs                       # 顶点（纯数据）
├── HalfEdge.cs                     # 半边（纯数据）
├── Face.cs                         # 面（纯数据）
└── DCELGraph.cs                    # DCEL 图（数据结构 + 拓扑操作）

Infrastructure/AutoCAD/DCEL/
├── DCELFactory.cs                  # 从 AutoCAD 实体构建 DCEL
└── DCELDrawer.cs                   # 绘制 DCEL 到 AutoCAD
```

**代码示例**:

```csharp
// Domain/DataStructures/DCEL/Vertex.cs
namespace HyCADTool.Refactored.Domain.DataStructures.DCEL
{
    using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

    /// <summary>
    /// DCEL 顶点（Vertex）
    /// </summary>
    public class Vertex
    {
        public Point2D Position { get; set; }
        public List<HalfEdge> OutgoingHalfedges { get; set; }

        public Vertex(Point2D position)
        {
            Position = position;
            OutgoingHalfedges = new List<HalfEdge>();
        }
    }

    /// <summary>
    /// DCEL 半边（Half-Edge）
    /// </summary>
    public class HalfEdge
    {
        public Vertex StartVertex { get; set; }
        public HalfEdge Twin { get; set; }
        public HalfEdge Next { get; set; }
        public HalfEdge Prev { get; set; }
        public Face IncidentFace { get; set; }
        public bool IsInitialized { get; set; }

        public Vector2D GetVector()
        {
            if (Twin?.StartVertex == null || StartVertex == null)
                throw new InvalidOperationException("Half-edge is not properly initialized");

            return new Vector2D(
                Twin.StartVertex.Position.X - StartVertex.Position.X,
                Twin.StartVertex.Position.Y - StartVertex.Position.Y
            );
        }
    }

    /// <summary>
    /// DCEL 面（Face）
    /// </summary>
    public class Face
    {
        public List<HalfEdge> Components { get; set; }

        public Face()
        {
            Components = new List<HalfEdge>();
        }
    }
}

// Domain/DataStructures/DCEL/DCELGraph.cs
public class DCELGraph
{
    public List<Vertex> Vertices { get; private set; }
    public List<HalfEdge> HalfEdges { get; private set; }
    public List<Face> Faces { get; private set; }

    public DCELGraph()
    {
        Vertices = new List<Vertex>();
        HalfEdges = new List<HalfEdge>();
        Faces = new List<Face>();
    }

    public Vertex AddVertex(Point2D position)
    {
        var vertex = new Vertex(position);
        Vertices.Add(vertex);
        return vertex;
    }

    public (HalfEdge, HalfEdge) AddEdgePair(Vertex origin, Vertex destination)
    {
        var he1 = new HalfEdge { StartVertex = origin };
        var he2 = new HalfEdge { StartVertex = destination };
        
        he1.Twin = he2;
        he2.Twin = he1;
        
        origin.OutgoingHalfedges.Add(he1);
        destination.OutgoingHalfedges.Add(he2);
        
        HalfEdges.Add(he1);
        HalfEdges.Add(he2);
        
        return (he1, he2);
    }

    public Face CreateFace(List<HalfEdge> halfEdges)
    {
        var face = new Face();
        int edgeCount = halfEdges.Count;
        
        for (int i = 0; i < edgeCount; i++)
        {
            halfEdges[i].IncidentFace = face;
            halfEdges[i].IsInitialized = true;
            halfEdges[i].Next = halfEdges[(i + 1) % edgeCount];
            halfEdges[i].Prev = halfEdges[(i - 1 + edgeCount) % edgeCount];
            face.Components.Add(halfEdges[i]);
        }
        
        Faces.Add(face);
        return face;
    }
}
```

**迁移要点**:
- ✅ 将 `Point3d` 替换为 `Point2D`（Domain 值对象）
- ✅ 保持拓扑数据结构的纯净性
- ✅ 绘制逻辑独立到 Infrastructure 层

---

### 4.4 Jig 交互式绘图（优先级 P1）

**原位置**: `HelpClass/Jig/`

**功能分析**:
- **PolylineJig.cs**: 
  - 多段线交互式绘制
  - 实时偏移预览
  - 支持撤销（Z 关键字）

- **HookJig.cs**: 
  - 钢筋弯钩交互式绘制
  - 根据方向自动确定弯钩角度

- **HookJigSeg.cs**: 
  - 分段弯钩 Jig

**重构方案**:

```
Infrastructure/AutoCAD/Interactive/
├── PolylineJig.cs                  # 多段线 Jig（保持原样）
├── HookJig.cs                      # 弯钩 Jig（保持原样）
└── HookJigSeg.cs                   # 分段弯钩 Jig
```

**重构策略**:
- ✅ Jig 类 100% AutoCAD 特定，全部放 Infrastructure 层
- ✅ 添加注释和文档
- ✅ 改进错误处理
- ✅ 保持原有功能不变

**代码改进示例**:

```csharp
// Infrastructure/AutoCAD/Interactive/PolylineJig.cs
namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interactive
{
    using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.GraphicsInterface;

    /// <summary>
    /// 多段线交互式绘制 Jig（Polyline Interactive Drawing Jig）
    /// 功能：实时预览偏移后的多段线，支持撤销
    /// </summary>
    public class PolylineJig : DrawJig
    {
        private readonly Polyline _polyline;
        private readonly Point3dCollection _points;
        private Point3d _currentPoint;
        private readonly double _offsetDistance;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="offsetDistance">偏移距离（Offset Distance）</param>
        public PolylineJig(double offsetDistance)
        {
            _polyline = new Polyline();
            _points = new Point3dCollection();
            _offsetDistance = offsetDistance;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            // 实现绘制逻辑...
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            // 实现交互逻辑...
        }

        /// <summary>
        /// 启动 Jig 交互
        /// </summary>
        /// <returns>用户操作状态</returns>
        public PromptStatus StartJig()
        {
            Editor ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            // 实现交互循环...
        }

        /// <summary>
        /// 获取最终的点集合
        /// </summary>
        public Point3dCollection GetPoints() => _points;
    }
}
```

---

### 4.5 ElevationSymbol 标高符号（优先级 P2）

**原位置**: `HelpClass/ElevationSymbol/`

**功能分析**:
- 标高符号绘制（三角形 + 文字）
- Jig 交互式放置
- 支持翻转（左右、上下）
- 支持旋转
- 静态方法：批量旋转、更新标高

**重构方案**:

```
Domain/Services/ElevationSymbol/
└── ElevationSymbolGeometry.cs      # 标高符号几何计算（纯算法）

Infrastructure/AutoCAD/Symbols/
├── ElevationSymbolJig.cs           # 标高符号 Jig
├── ElevationSymbolDrawer.cs        # 标高符号绘制器
└── ElevationSymbolRepository.cs    # 标高符号数据访问
```

**拆分示例**:

```csharp
// Domain/Services/ElevationSymbol/ElevationSymbolGeometry.cs
namespace HyCADTool.Refactored.Domain.Services.ElevationSymbol
{
    using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

    /// <summary>
    /// 标高符号几何状态
    /// </summary>
    public enum ElevationSymbolState
    {
        Normal,
        FlipHorizontal,
        FlipVertical,
        FlipBoth
    }

    /// <summary>
    /// 标高符号几何计算服务
    /// </summary>
    public class ElevationSymbolGeometry
    {
        /// <summary>
        /// 计算标高符号的形状点
        /// </summary>
        /// <param name="basePoint">基准点</param>
        /// <param name="scale">比例</param>
        /// <param name="d">尺寸参数</param>
        /// <param name="angleRadians">旋转角度（弧度）</param>
        /// <param name="state">翻转状态</param>
        /// <returns>形状点数组</returns>
        public static Point2D[] CalculateShapePoints(
            Point2D basePoint, 
            double scale, 
            double d, 
            double angleRadians,
            ElevationSymbolState state)
        {
            double sqrt2 = Math.Sqrt(2) / 2 * d;
            
            Point2D[] points = new Point2D[4];
            points[0] = new Point2D(basePoint.X + 3.5 * d * scale, basePoint.Y + sqrt2 * scale);
            points[1] = new Point2D(basePoint.X - sqrt2 * scale, basePoint.Y + sqrt2 * scale);
            points[2] = basePoint;
            points[3] = new Point2D(basePoint.X + sqrt2 * scale, basePoint.Y + sqrt2 * scale);
            
            // 应用旋转
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = RotatePoint(points[i], basePoint, angleRadians);
            }
            
            // 应用翻转
            AdjustPointsByState(ref points, basePoint, state);
            
            return points;
        }

        /// <summary>
        /// 旋转点
        /// </summary>
        private static Point2D RotatePoint(Point2D point, Point2D center, double angle)
        {
            double cosTheta = Math.Cos(angle);
            double sinTheta = Math.Sin(angle);
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            double newX = center.X + (dx * cosTheta - dy * sinTheta);
            double newY = center.Y + (dx * sinTheta + dy * cosTheta);
            return new Point2D(newX, newY);
        }

        /// <summary>
        /// 根据状态调整点（翻转）
        /// </summary>
        private static void AdjustPointsByState(
            ref Point2D[] points, 
            Point2D center, 
            ElevationSymbolState state)
        {
            switch (state)
            {
                case ElevationSymbolState.FlipVertical:
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Point2D(points[i].X, 2 * center.Y - points[i].Y);
                    break;
                case ElevationSymbolState.FlipHorizontal:
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Point2D(2 * center.X - points[i].X, points[i].Y);
                    break;
                case ElevationSymbolState.FlipBoth:
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Point2D(2 * center.X - points[i].X, 2 * center.Y - points[i].Y);
                    break;
            }
        }
    }
}
```

---

### 4.6 Pile 桩基相关（优先级 P2）

**原位置**: `HelpClass/Pile/`

**功能分析**:
- **BasePile.cs**: 
  - 桩的枚举（PileSectionType, PileType, PileArrangementType）
  - Pile 类（面积计算）
  - 依赖 NetTopologySuite

- **PileConfig.cs**: 桩配置
- **MinArea.cs**: 最小面积计算
- **StandardArea/**: 标准面积（矩形、圆形）

**重构方案**:

```
Domain/Entities/Pile/
├── PileEnums.cs                    # 桩枚举
├── Pile.cs                         # 桩实体
└── PileLayoutAlgorithms.cs         # 布桩算法

Domain/Services/PileLayout/
├── StandardAreaFactory.cs          # 标准面积工厂
├── MinAreaCalculator.cs            # 最小面积计算
└── VoronoiPileLayoutService.cs     # Voronoi 布桩服务

Infrastructure/AutoCAD/Pile/
├── PileDrawer.cs                   # 桩绘制
└── PileRepository.cs               # 桩数据访问
```

**代码示例**:

```csharp
// Domain/Entities/Pile/PileEnums.cs
namespace HyCADTool.Refactored.Domain.Entities.Pile
{
    /// <summary>
    /// 桩截面类型（Pile Section Type）
    /// </summary>
    public enum PileSectionType
    {
        Circle,    // 圆形
        Square     // 方形
    }

    /// <summary>
    /// 桩类型（Pile Type）
    /// </summary>
    public enum PileType
    {
        Corner,    // 角桩
        Edge,      // 边桩
        Middle     // 中桩
    }

    /// <summary>
    /// 桩布置方式（Pile Arrangement Type）
    /// </summary>
    public enum PileArrangementType
    {
        Rectangle,  // 矩形布置
        Circular    // 梅花形布置
    }
}

// Domain/Entities/Pile/Pile.cs
public class Pile
{
    public int Id { get; set; }
    public Point2D Center { get; set; }
    public PileSectionType Section { get; set; }
    public double DiameterOrEdge { get; set; }
    public PileType PileType { get; set; }
    public bool IsInitialized { get; set; }
    public double PileArea { get; private set; }

    public Pile(PileSectionType section, double diameterOrEdge)
    {
        Section = section;
        DiameterOrEdge = diameterOrEdge;
        CalculatePileArea();
    }

    /// <summary>
    /// 计算桩面积（Calculate Pile Area）
    /// </summary>
    private void CalculatePileArea()
    {
        if (Section == PileSectionType.Circle)
        {
            PileArea = Math.PI * Math.Pow(DiameterOrEdge / 2, 2);
        }
        else if (Section == PileSectionType.Square)
        {
            if (DiameterOrEdge > 0)
            {
                PileArea = DiameterOrEdge * DiameterOrEdge;
            }
            else
            {
                throw new ArgumentException("方形桩的边长必须大于 0");
            }
        }
    }
}
```

---

### 4.7 暂缓处理的模块

#### Elevation3D（39 文件）

**原因**:
- 文件过多，包含大量备份文件
- 逻辑复杂，需要专门的阶段处理
- 当前优先级较低（仅 2 个命令依赖）

**建议**:
- 阶段 6 或独立阶段处理
- 先清理备份文件

#### TitleBlock（2 文件）

**原因**:
- 功能相对独立
- 优先级低

**建议**:
- 阶段 5 或 6 处理

---

## 实施计划

### 第 1 步：CAD 通用类（1 天）

**任务**:
- ✅ 迁移 TypeConverter 到 Domain/ValueObjects/EntityTypeMapping/
- ✅ 迁移 Comparer 到 Infrastructure/AutoCAD/Utilities/
- ✅ 迁移 RowCols 到 Domain/ValueObjects/Grid/
- ✅ 编译验证

**交付物**:
- `Domain/ValueObjects/EntityTypeMapping/EntityTypeConverter.cs`
- `Domain/ValueObjects/Grid/RowColValue.cs`
- `Infrastructure/AutoCAD/Utilities/Point3dEqualityComparer.cs`
- `Infrastructure/AutoCAD/Utilities/Point2dEqualityComparer.cs`
- `Infrastructure/AutoCAD/Utilities/CoordinateEqualityComparer.cs`

---

### 第 2 步：DCEL 数据结构（1-2 天）

**任务**:
- ✅ 提取 Vertex, HalfEdge, Face 到 Domain/DataStructures/DCEL/
- ✅ 替换 Point3d 为 Point2D
- ✅ 创建 DCELGraph 类
- ✅ 迁移 DCELFactory 到 Infrastructure
- ✅ 迁移 DcelDraw 到 Infrastructure
- ✅ 编译验证

**交付物**:
- `Domain/DataStructures/DCEL/Vertex.cs`
- `Domain/DataStructures/DCEL/HalfEdge.cs`
- `Domain/DataStructures/DCEL/Face.cs`
- `Domain/DataStructures/DCEL/DCELGraph.cs`
- `Infrastructure/AutoCAD/DCEL/DCELFactory.cs`
- `Infrastructure/AutoCAD/DCEL/DCELDrawer.cs`

---

### 第 3 步：Jig 交互式绘图（1 天）

**任务**:
- ✅ 迁移 PolylineJig, HookJig, HookJigSeg 到 Infrastructure/AutoCAD/Interactive/
- ✅ 添加注释和文档
- ✅ 改进错误处理
- ✅ 编译验证

**交付物**:
- `Infrastructure/AutoCAD/Interactive/PolylineJig.cs`
- `Infrastructure/AutoCAD/Interactive/HookJig.cs`
- `Infrastructure/AutoCAD/Interactive/HookJigSeg.cs`

---

### 第 4 步：ElevationSymbol（2 天）

**任务**:
- ✅ 提取几何计算到 Domain/Services/ElevationSymbol/
- ✅ 迁移 Jig 到 Infrastructure/AutoCAD/Symbols/
- ✅ 创建 Drawer 和 Repository
- ✅ 编译验证

**交付物**:
- `Domain/Services/ElevationSymbol/ElevationSymbolGeometry.cs`
- `Domain/Services/ElevationSymbol/ElevationSymbolState.cs`
- `Infrastructure/AutoCAD/Symbols/ElevationSymbolJig.cs`
- `Infrastructure/AutoCAD/Symbols/ElevationSymbolDrawer.cs`

---

### 第 5 步：Pile 桩基（2-3 天）

**任务**:
- ✅ 提取枚举和实体到 Domain/Entities/Pile/
- ✅ 提取算法到 Domain/Services/PileLayout/
- ✅ 迁移 AutoCAD 相关到 Infrastructure
- ✅ 编译验证

**交付物**:
- `Domain/Entities/Pile/PileEnums.cs`
- `Domain/Entities/Pile/Pile.cs`
- `Domain/Services/PileLayout/StandardAreaFactory.cs`
- `Infrastructure/AutoCAD/Pile/PileDrawer.cs`

---

### 第 6 步：测试与验证（1 天）

**任务**:
- ✅ 创建 Phase4TestCommand
- ✅ 测试 DCEL 数据结构
- ✅ 测试 Jig 交互
- ✅ 测试 ElevationSymbol
- ✅ 更新 ReCall 支持 C1P4 命令

**交付物**:
- `Test/Phase4TestCommand.cs`
- 测试报告

---

### 第 7 步：编写完成报告（0.5 天）

**任务**:
- ✅ 总结迁移内容
- ✅ 记录遇到的问题
- ✅ 提供使用指南

**交付物**:
- `NewPlan/阶段4-HelpClass层重构完成报告.md`

---

## 总工期估算

| 步骤 | 任务 | 工期 |
|------|------|------|
| 第 1 步 | CAD 通用类 | 1 天 |
| 第 2 步 | DCEL 数据结构 | 1-2 天 |
| 第 3 步 | Jig 交互式绘图 | 1 天 |
| 第 4 步 | ElevationSymbol | 2 天 |
| 第 5 步 | Pile 桩基 | 2-3 天 |
| 第 6 步 | 测试与验证 | 1 天 |
| 第 7 步 | 完成报告 | 0.5 天 |
| **总计** | | **8.5-10.5 天** |

---

## 验收标准

### 编译验收
- [ ] HyCADTool.Refactored.dll 编译成功
- [ ] 零编译错误
- [ ] 零严重警告

### 功能验收
- [ ] DCEL 数据结构可正常构建
- [ ] Jig 交互式绘图可用
- [ ] ElevationSymbol 可绘制
- [ ] Pile 面积计算正确

### 架构验收
- [ ] Domain 层无 AutoCAD 依赖
- [ ] Infrastructure 层正确封装 AutoCAD 操作
- [ ] 代码符合 SOLID 原则
- [ ] 无过度工程化

### 测试验收
- [ ] 所有阶段测试通过（11/11 → 15/15）
- [ ] 新增 4 个阶段 4 测试

---

## 附录

### 附录 A：文件映射表

| 原文件 | 新位置 | 说明 |
|--------|--------|------|
| `HelpClass/CAD/TypeConverter.cs` | `Domain/ValueObjects/EntityTypeMapping/EntityTypeConverter.cs` | 提取核心映射 |
| `HelpClass/CAD/Comparer.cs` | `Infrastructure/AutoCAD/Utilities/Point*Comparer.cs` | 拆分为 3 个文件 |
| `HelpClass/RowCols.cs` | `Domain/ValueObjects/Grid/RowColValue.cs` | 移动 |
| `HelpClass/DCEL/DCEL.cs` | `Domain/DataStructures/DCEL/*.cs` | 拆分为 4 个文件 |
| `HelpClass/Jig/*.cs` | `Infrastructure/AutoCAD/Interactive/*.cs` | 移动 |
| `HelpClass/ElevationSymbol/*.cs` | `Domain/Services/ElevationSymbol/*.cs` + `Infrastructure/AutoCAD/Symbols/*.cs` | 拆分 |
| `HelpClass/Pile/BasePile.cs` | `Domain/Entities/Pile/*.cs` | 拆分 |

---

### 附录 B：依赖分析

**Domain 层依赖**:
- ✅ 无外部依赖（除 System 库）
- ✅ 100% 可移植到 Python

**Infrastructure 层依赖**:
- Autodesk.AutoCAD.DatabaseServices
- Autodesk.AutoCAD.Geometry
- Autodesk.AutoCAD.EditorInput
- NetTopologySuite（仅 Coordinate 比较器）

---

<div align="center">

**阶段 4: HelpClass 层重构详细设计 v1.0**

创建日期: 2025-10-13  
预计工期: 8.5-10.5 天

</div>

