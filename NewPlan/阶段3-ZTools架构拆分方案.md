# 阶段 3: ZTools 架构拆分方案（Clean Architecture + DDD）

> **创建时间**: 2025-10-13  
> **目标**: 按照 Clean Architecture 和 DDD 原则，将 ZTools 功能合理拆分到各层

---

## 📋 目录

1. [架构原则](#架构原则)
2. [ZTools 功能分析](#ztools-功能分析)
3. [层级拆分方案](#层级拆分方案)
4. [详细拆分清单](#详细拆分清单)
5. [实施计划](#实施计划)

---

## 架构原则

### Clean Architecture 分层

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│                  (UI, Commands, Controllers)                 │
└────────────────────────────┬────────────────────────────────┘
                             │ 依赖
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                   Application Layer                          │
│              (Use Cases, Application Services)               │
│              业务流程编排，不包含业务规则                      │
└────────────────────────────┬────────────────────────────────┘
                             │ 依赖
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                      Domain Layer                            │
│         (Entities, Value Objects, Domain Services)           │
│         纯业务逻辑，不依赖外部框架，可迁移到其他平台           │
└─────────────────────────────────────────────────────────────┘
                             ▲ 实现
                             │
┌─────────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                        │
│          (AutoCAD Services, Repositories, Extensions)        │
│          外部依赖实现，AutoCAD 特定代码                        │
└─────────────────────────────────────────────────────────────┘
```

### DDD 关键概念

| 概念 | 说明 | 位置 | 特征 |
|------|------|------|------|
| **Value Objects** | 值对象 | Domain | 不可变，无标识 |
| **Entities** | 实体 | Domain | 有标识，可变 |
| **Domain Services** | 领域服务 | Domain | 纯业务逻辑，无外部依赖 |
| **Application Services** | 应用服务 | Application | 用例编排 |
| **Infrastructure Services** | 基础设施服务 | Infrastructure | 外部依赖实现 |

---

## ZTools 功能分析

### 功能分类矩阵

根据对 ZTools 的分析，我将功能分为以下类别：

| 功能类别 | 依赖外部 | 业务复杂度 | 推荐层级 | 说明 |
|----------|----------|------------|----------|------|
| **纯几何算法** | ❌ 否 | 🔴 高 | **Domain** | 点、线、面的数学运算 |
| **几何判断** | ❌ 否 | 🟡 中 | **Domain** | 共线、相交、包含等判断 |
| **数学工具** | ❌ 否 | 🟢 低 | **Domain** | 角度、距离、插值等 |
| **AutoCAD 实体操作** | ✅ 是 | 🟡 中 | **Infrastructure** | 创建、修改、删除实体 |
| **AutoCAD 扩展方法** | ✅ 是 | 🟢 低 | **Infrastructure** | Entity, Polyline 扩展 |
| **图层/样式操作** | ✅ 是 | 🟢 低 | **Infrastructure** | 已有 LayerService, StyleService |
| **业务流程** | ✅ 是 | 🔴 高 | **Application** | 复杂的业务编排 |

---

## 层级拆分方案

### 方案概览

```
Domain Layer (纯业务逻辑，平台无关)
├── Services/GeometryAlgorithms/
│   ├── PointAlgorithms.cs           ✅ 点算法（距离、插值、投影）
│   ├── LineAlgorithms.cs            ✅ 线算法（交点、平行、垂直）
│   ├── PolygonAlgorithms.cs         ✅ 多边形算法（面积、包含、凸包）
│   ├── IntersectionAlgorithms.cs    ✅ 相交算法
│   ├── ConvexHullAlgorithm.cs       ✅ 凸包算法（已有）
│   └── OffsetAlgorithms.cs          ✅ 偏移算法
│
├── Services/MathAlgorithms/
│   ├── AngleCalculator.cs           ✅ 角度计算
│   ├── DistanceCalculator.cs        ✅ 距离计算
│   └── InterpolationAlgorithms.cs   ✅ 插值算法
│
└── ValueObjects/Geometry/
    ├── Point2D.cs                    ✅ 已有
    ├── Line2D.cs                     ✅ 已有
    ├── Polygon2D.cs                  ✅ 已有
    └── Circle2D.cs                   ✅ 已有

Application Layer (用例编排)
└── Services/
    └── (暂无，ZTools 主要是工具，不是用例)

Infrastructure Layer (AutoCAD 特定实现)
├── AutoCAD/Extensions/
│   ├── PolylineExtensions.cs        ✅ 已创建
│   ├── LineExtensions.cs            ⏳ 待创建
│   ├── Point3dExtensions.cs         ✅ 已创建
│   ├── EntityExtensions.cs          ✅ 已有（EntityHelper）
│   ├── DatabaseExtensions.cs        ⏳ 待创建
│   └── EditorExtensions.cs          ⏳ 待创建
│
├── AutoCAD/Services/
│   ├── DrawingService.cs            ✅ 已有
│   ├── EditorService.cs             ✅ 已有
│   ├── DatabaseService.cs           ✅ 已有
│   ├── LayerService.cs              ✅ 已有
│   ├── StyleService.cs              ✅ 已有
│   └── SelectionService.cs          ✅ 已有
│
└── AutoCAD/Utilities/
    ├── TransactionHelper.cs         ✅ 已有
    ├── EntityHelper.cs              ✅ 已有
    └── CadDrawingUtils.cs           ⏳ 可选
```

---

## 详细拆分清单

### 1. Domain Layer（领域层）- 平台无关

#### 1.1 几何算法服务

**Domain/Services/GeometryAlgorithms/PointAlgorithms.cs**

```csharp
namespace HyCADTool.Refactored.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 点算法服务 - 纯数学计算，平台无关
    /// </summary>
    public static class PointAlgorithms
    {
        /// <summary>
        /// 计算两点距离
        /// </summary>
        public static double Distance(Point2D p1, Point2D p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 计算中点
        /// </summary>
        public static Point2D Midpoint(Point2D p1, Point2D p2)
        {
            return new Point2D(
                (p1.X + p2.X) / 2,
                (p1.Y + p2.Y) / 2
            );
        }

        /// <summary>
        /// 线性插值
        /// </summary>
        public static Point2D Lerp(Point2D p1, Point2D p2, double t)
        {
            return new Point2D(
                p1.X + (p2.X - p1.X) * t,
                p1.Y + (p2.Y - p1.Y) * t
            );
        }

        /// <summary>
        /// 点到直线的距离
        /// </summary>
        public static double DistanceToLine(Point2D point, Line2D line)
        {
            // 使用公式: |ax + by + c| / sqrt(a^2 + b^2)
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            
            double numerator = Math.Abs(
                dy * point.X - dx * point.Y + 
                line.EndPoint.X * line.StartPoint.Y - 
                line.EndPoint.Y * line.StartPoint.X
            );
            
            double denominator = Math.Sqrt(dx * dx + dy * dy);
            
            return numerator / denominator;
        }

        /// <summary>
        /// 点在直线上的投影
        /// </summary>
        public static Point2D ProjectToLine(Point2D point, Line2D line)
        {
            var lineVec = new Vector2D(
                line.EndPoint.X - line.StartPoint.X,
                line.EndPoint.Y - line.StartPoint.Y
            );
            
            var pointVec = new Vector2D(
                point.X - line.StartPoint.X,
                point.Y - line.StartPoint.Y
            );
            
            double t = Vector2D.Dot(pointVec, lineVec) / Vector2D.Dot(lineVec, lineVec);
            
            return new Point2D(
                line.StartPoint.X + lineVec.X * t,
                line.StartPoint.Y + lineVec.Y * t
            );
        }

        /// <summary>
        /// 旋转点（绕原点）
        /// </summary>
        public static Point2D Rotate(Point2D point, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            
            return new Point2D(
                point.X * cos - point.Y * sin,
                point.X * sin + point.Y * cos
            );
        }

        /// <summary>
        /// 旋转点（绕指定中心）
        /// </summary>
        public static Point2D RotateAround(Point2D point, Point2D center, double angle)
        {
            // 平移到原点，旋转，再平移回去
            var translated = new Point2D(point.X - center.X, point.Y - center.Y);
            var rotated = Rotate(translated, angle);
            return new Point2D(rotated.X + center.X, rotated.Y + center.Y);
        }
    }
}
```

**Domain/Services/GeometryAlgorithms/LineAlgorithms.cs**

```csharp
namespace HyCADTool.Refactored.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 线算法服务 - 已有（需扩展）
    /// </summary>
    public static class LineAlgorithms
    {
        // 已有方法...

        /// <summary>
        /// 判断两线段是否平行
        /// </summary>
        public static bool AreParallel(Line2D line1, Line2D line2, double tolerance = 1e-10)
        {
            var dir1 = line1.Direction;
            var dir2 = line2.Direction;
            
            // 叉积为零表示平行
            double cross = dir1.X * dir2.Y - dir1.Y * dir2.X;
            return Math.Abs(cross) < tolerance;
        }

        /// <summary>
        /// 判断两线段是否垂直
        /// </summary>
        public static bool ArePerpendicular(Line2D line1, Line2D line2, double tolerance = 1e-10)
        {
            var dir1 = line1.Direction;
            var dir2 = line2.Direction;
            
            // 点积为零表示垂直
            double dot = dir1.X * dir2.X + dir1.Y * dir2.Y;
            return Math.Abs(dot) < tolerance;
        }

        /// <summary>
        /// 判断两线段是否共线
        /// </summary>
        public static bool AreCollinear(Line2D line1, Line2D line2, double tolerance = 1e-10)
        {
            if (!AreParallel(line1, line2, tolerance))
                return false;
            
            // 检查 line2 的起点是否在 line1 的延长线上
            var dist = PointAlgorithms.DistanceToLine(line2.StartPoint, line1);
            return dist < tolerance;
        }

        /// <summary>
        /// 计算两线段的交点
        /// </summary>
        public static Point2D? GetIntersection(Line2D line1, Line2D line2, double tolerance = 1e-10)
        {
            // 使用参数方程求解
            // Line1: P = P1 + t * (P2 - P1)
            // Line2: Q = Q1 + s * (Q2 - Q1)
            
            double x1 = line1.StartPoint.X, y1 = line1.StartPoint.Y;
            double x2 = line1.EndPoint.X, y2 = line1.EndPoint.Y;
            double x3 = line2.StartPoint.X, y3 = line2.StartPoint.Y;
            double x4 = line2.EndPoint.X, y4 = line2.EndPoint.Y;
            
            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            
            if (Math.Abs(denom) < tolerance)
                return null; // 平行或重合
            
            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double s = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;
            
            // 检查交点是否在两条线段内
            if (t >= 0 && t <= 1 && s >= 0 && s <= 1)
            {
                return new Point2D(
                    x1 + t * (x2 - x1),
                    y1 + t * (y2 - y1)
                );
            }
            
            return null;
        }
    }
}
```

**Domain/Services/GeometryAlgorithms/PolygonAlgorithms.cs**

```csharp
namespace HyCADTool.Refactored.Domain.Services.GeometryAlgorithms
{
    /// <summary>
    /// 多边形算法服务 - 已有（需扩展）
    /// </summary>
    public static class PolygonAlgorithms
    {
        // 已有方法...

        /// <summary>
        /// 计算多边形面积（有向面积）
        /// </summary>
        public static double CalculateArea(IEnumerable<Point2D> vertices)
        {
            var points = vertices.ToList();
            if (points.Count < 3)
                return 0;
            
            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }
            
            return Math.Abs(area / 2.0);
        }

        /// <summary>
        /// 计算多边形周长
        /// </summary>
        public static double CalculatePerimeter(IEnumerable<Point2D> vertices)
        {
            var points = vertices.ToList();
            if (points.Count < 2)
                return 0;
            
            double perimeter = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                perimeter += PointAlgorithms.Distance(points[i], points[j]);
            }
            
            return perimeter;
        }

        /// <summary>
        /// 判断点是否在多边形内（射线法）
        /// </summary>
        public static bool ContainsPoint(IEnumerable<Point2D> vertices, Point2D point)
        {
            var points = vertices.ToList();
            if (points.Count < 3)
                return false;
            
            int intersections = 0;
            
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                var p1 = points[i];
                var p2 = points[j];
                
                if (RayIntersectsSegment(point, p1, p2))
                {
                    intersections++;
                }
            }
            
            return (intersections % 2) == 1;
        }

        /// <summary>
        /// 射线与线段相交判断（内部使用）
        /// </summary>
        private static bool RayIntersectsSegment(Point2D point, Point2D p1, Point2D p2)
        {
            if (p1.Y == p2.Y)
                return false;
            
            if (point.Y < Math.Min(p1.Y, p2.Y) || point.Y >= Math.Max(p1.Y, p2.Y))
                return false;
            
            double xIntersect = p1.X + (point.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
            
            return xIntersect >= point.X;
        }

        /// <summary>
        /// 判断多边形是否为凸多边形
        /// </summary>
        public static bool IsConvex(IEnumerable<Point2D> vertices)
        {
            var points = vertices.ToList();
            if (points.Count < 4)
                return true;
            
            bool? isPositive = null;
            
            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                var p3 = points[(i + 2) % points.Count];
                
                double cross = (p2.X - p1.X) * (p3.Y - p2.Y) - (p2.Y - p1.Y) * (p3.X - p2.X);
                
                if (Math.Abs(cross) < 1e-10)
                    continue;
                
                if (isPositive == null)
                {
                    isPositive = cross > 0;
                }
                else if ((cross > 0) != isPositive)
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}
```

#### 1.2 数学算法服务

**Domain/Services/MathAlgorithms/AngleCalculator.cs**

```csharp
namespace HyCADTool.Refactored.Domain.Services.MathAlgorithms
{
    /// <summary>
    /// 角度计算器 - 纯数学计算
    /// </summary>
    public static class AngleCalculator
    {
        /// <summary>
        /// 弧度转角度
        /// </summary>
        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        /// <summary>
        /// 角度转弧度
        /// </summary>
        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// 归一化角度到 [0, 2π)
        /// </summary>
        public static double NormalizeAngle(double angle)
        {
            while (angle < 0)
                angle += 2 * Math.PI;
            while (angle >= 2 * Math.PI)
                angle -= 2 * Math.PI;
            return angle;
        }

        /// <summary>
        /// 计算两向量夹角
        /// </summary>
        public static double AngleBetweenVectors(Vector2D v1, Vector2D v2)
        {
            double dot = Vector2D.Dot(v1, v2);
            double mag1 = v1.Length;
            double mag2 = v2.Length;
            
            if (mag1 < 1e-10 || mag2 < 1e-10)
                return 0;
            
            double cos = dot / (mag1 * mag2);
            cos = Math.Max(-1, Math.Min(1, cos)); // Clamp
            
            return Math.Acos(cos);
        }

        /// <summary>
        /// 计算向量角度（相对于 X 轴）
        /// </summary>
        public static double VectorAngle(Vector2D vector)
        {
            return Math.Atan2(vector.Y, vector.X);
        }
    }
}
```

---

### 2. Infrastructure Layer（基础设施层）- AutoCAD 特定

#### 2.1 扩展方法（已创建 + 待创建）

**已创建**:
- ✅ `PolylineExtensions.cs` - Polyline 扩展
- ✅ `Point3dExtensions.cs` - Point3d 扩展
- ✅ `EntityHelper.cs` (EntityExtensions) - Entity 扩展

**待创建**:
- ⏳ `LineExtensions.cs` - Line 扩展
- ⏳ `DatabaseExtensions.cs` - Database 扩展
- ⏳ `EditorExtensions.cs` - Editor 扩展

**特点**:
- 依赖 AutoCAD API
- 提供便捷的操作方法
- 不包含复杂业务逻辑
- **桥接** Domain 算法和 AutoCAD 实体

**示例 - 桥接 Domain 算法**:

```csharp
// Infrastructure/AutoCAD/Extensions/PolylineExtensions.cs
public static class PolylineExtensions
{
    /// <summary>
    /// 判断点是否在多段线内（使用 Domain 算法）
    /// </summary>
    public static bool ContainsPoint(this Polyline poly, Point3d point)
    {
        if (!poly.Closed)
            return false;
        
        // 转换为 Domain 对象
        var vertices = poly.GetAllVertices2d()
            .Select(p => new Point2D(p.X, p.Y));
        
        var domainPoint = new Point2D(point.X, point.Y);
        
        // 调用 Domain 算法
        return PolygonAlgorithms.ContainsPoint(vertices, domainPoint);
    }

    /// <summary>
    /// 获取面积（使用 Domain 算法）
    /// </summary>
    public static double GetArea(this Polyline poly)
    {
        if (!poly.Closed)
            return 0;
        
        // 方案1: 使用 AutoCAD API（快速）
        return Math.Abs(poly.Area);
        
        // 方案2: 使用 Domain 算法（可移植）
        // var vertices = poly.GetAllVertices2d()
        //     .Select(p => new Point2D(p.X, p.Y));
        // return PolygonAlgorithms.CalculateArea(vertices);
    }
}
```

#### 2.2 服务（已有）

- ✅ `DrawingService` - 绘制服务
- ✅ `EditorService` - 编辑器服务
- ✅ `DatabaseService` - 数据库服务
- ✅ `LayerService` - 图层服务
- ✅ `StyleService` - 样式服务
- ✅ `SelectionService` - 选择服务

---

## 实施计划

### 阶段 3.1: Domain 层算法（2-3 天）

#### 任务清单

- [ ] 创建 `PointAlgorithms.cs`
- [ ] 扩展 `LineAlgorithms.cs`
- [ ] 扩展 `PolygonAlgorithms.cs`
- [ ] 创建 `AngleCalculator.cs`
- [ ] 创建 `DistanceCalculator.cs`
- [ ] 单元测试（可选，使用 xUnit）

### 阶段 3.2: Infrastructure 层扩展（1-2 天）

#### 任务清单

- [ ] 完善 `PolylineExtensions.cs`（桥接 Domain 算法）
- [ ] 完善 `Point3dExtensions.cs`（桥接 Domain 算法）
- [ ] 创建 `LineExtensions.cs`
- [ ] 扩展 `EntityHelper.cs`

### 阶段 3.3: 测试验证（1 天）

#### 任务清单

- [ ] 创建测试命令
- [ ] 验证 Domain 算法
- [ ] 验证扩展方法
- [ ] 性能测试（Domain vs AutoCAD API）

---

## 关键决策

### 决策 1: Domain 算法 vs AutoCAD API

| 场景 | 使用 Domain 算法 | 使用 AutoCAD API |
|------|-----------------|------------------|
| **优先级** | 可移植性 > 性能 | 性能 > 可移植性 |
| **适用场景** | 需要迁移到 Blender | AutoCAD 独有功能 |
| **示例** | 点在多边形内判断 | Polyline.Area 属性 |
| **策略** | 两者都实现，可切换 | 优先使用 AutoCAD API |

### 决策 2: 扩展方法的职责

扩展方法应该：
- ✅ 提供便捷的操作接口
- ✅ 桥接 Domain 算法和 AutoCAD 实体
- ✅ 处理类型转换（AutoCAD ↔ Domain）
- ❌ 不包含复杂业务逻辑（应在 Application 层）

### 决策 3: 代码复用策略

```
优先级：
1. 使用 AutoCAD API（性能最优）
2. 使用 Domain 算法（可移植）
3. 实现两个版本（提供选择）

示例：
- Area 计算：优先使用 Polyline.Area（AutoCAD API）
- 点在多边形内：使用 Domain 算法（可移植到 Blender）
```

---

## 收益分析

### Clean Architecture + DDD 的收益

| 收益 | 说明 |
|------|------|
| **可测试性** | Domain 算法纯函数，易于单元测试 |
| **可移植性** | Domain 算法无外部依赖，可迁移到 Blender/Python |
| **可维护性** | 清晰的层级划分，职责分明 |
| **可扩展性** | 易于添加新算法和新功能 |
| **代码复用** | Domain 算法可在多个地方使用 |

### 对比 ZTools 静态类

| 维度 | ZTools 静态类 | Clean Architecture 方案 |
|------|--------------|------------------------|
| 依赖关系 | 混乱 | 清晰（单向依赖） |
| 可测试性 | 困难 | 容易 |
| 可移植性 | 不可能 | Domain 层可移植 |
| 职责划分 | 混杂 | 清晰 |
| 代码组织 | 一个大类 | 多个小类，各司其职 |

---

## 总结

### 核心思想

```
ZTools 功能拆分原则：

1. 纯算法 → Domain Layer（可移植）
   - PointAlgorithms
   - LineAlgorithms
   - PolygonAlgorithms
   - MathAlgorithms

2. AutoCAD 操作 → Infrastructure Layer（平台特定）
   - Extensions (扩展方法)
   - Services (服务)
   - Utilities (工具类)

3. 业务编排 → Application Layer（用例）
   - 复杂流程
   - 多步骤操作

4. 扩展方法作为桥梁
   - 连接 Domain 和 AutoCAD
   - 提供便捷接口
```

### 下一步

1. ✅ 确认架构方案
2. ⏳ 实施 Domain 层算法
3. ⏳ 完善 Infrastructure 层扩展
4. ⏳ 测试验证

---

**文档创建时间**: 2025-10-13  
**状态**: 待确认

