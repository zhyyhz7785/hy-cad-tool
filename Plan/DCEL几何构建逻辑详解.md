# DCEL 几何构建逻辑详解

> **文档目的**: 详细说明DCEL（Doubly Connected Edge List，双连通边表）的构建几何逻辑，防止未来维护时遗忘关键细节  
> **创建日期**: 2024-10-15  
> **重要性**: ⭐⭐⭐⭐⭐ 核心技术文档  
> **适用版本**: HyCADTool.Refactored

---

## 📋 目录

1. [DCEL 基本概念](#dcel-基本概念)
2. [构建算法总览](#构建算法总览)
3. [详细步骤解析](#详细步骤解析)
4. [关键几何计算](#关键几何计算)
5. [左侧法则详解](#左侧法则详解)
6. [面方向判断](#面方向判断)
7. [关键代码片段](#关键代码片段)
8. [常见陷阱](#常见陷阱)

---

## DCEL 基本概念

### 什么是 DCEL？

DCEL（Doubly Connected Edge List）是计算几何中用于表示平面细分（planar subdivision）的经典数据结构。它能够高效地表示和查询平面图形的拓扑结构。

### 核心数据结构

```
┌─────────────────────────────────────────────────────────┐
│                    DCEL 组成                             │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Vertex (顶点)                                           │
│  ├── Position: Point2D         (位置)                    │
│  └── OutgoingHalfedges: List   (从该顶点出发的半边列表)  │
│                                                          │
│  HalfEdge (半边)                                         │
│  ├── StartVertex: Vertex       (起点)                    │
│  ├── Twin: HalfEdge            (孪生半边，方向相反)      │
│  ├── Next: HalfEdge            (面边界上的下一条边)      │
│  ├── Prev: HalfEdge            (面边界上的前一条边)      │
│  └── IncidentFace: Face        (所属的面)                │
│                                                          │
│  Face (面)                                               │
│  ├── Components: List<HalfEdge> (组成面的半边循环)      │
│  └── IsOuter: bool              (是否为外轮廓)           │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### 半边（HalfEdge）的核心概念

每条几何边在DCEL中被分解为**两条方向相反的半边**：

```
     Vertex1 ──────────────> Vertex2
              HalfEdge1 →

     Vertex1 <────────────── Vertex2
              ← HalfEdge2
```

- `HalfEdge1.Twin = HalfEdge2`
- `HalfEdge2.Twin = HalfEdge1`
- `HalfEdge1.StartVertex = Vertex1`
- `HalfEdge2.StartVertex = Vertex2`

---

## 构建算法总览

### 输入

- **曲线集合**：AutoCAD 中的 Line, Polyline, Arc, Circle 等
- **容差**：用于判断顶点重合的阈值（默认 0.01）

### 输出

- **DCELGraph**：包含所有 Vertex, HalfEdge, Face 的完整拓扑结构
- **面分类**：每个 Face 被标记为外轮廓（IsOuter=true）或内部（IsOuter=false）

### 算法流程图

```
┌─────────────────────────────────────┐
│  步骤 1: 提取线段                    │
│  从曲线提取 (StartPoint, EndPoint)  │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  步骤 2: 创建顶点和半边对            │
│  使用容差合并重复顶点                │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  步骤 3: 删除孤立顶点                │
│  移除度数 ≤ 1 的顶点和关联半边      │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  步骤 4: 构建面 (左侧法则)          │
│  使用逆时针遍历构建面边界            │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  步骤 5: 计算面方向                  │
│  通过有向面积判断 IsOuter            │
└─────────────────────────────────────┘
```

---

## 详细步骤解析

### 步骤 1: 提取线段

#### ⚠️ 关键逻辑：只提取起点和终点

```csharp
// 重要：与原代码保持一致！
// 即使是多段线 (Polyline)，也只提取首尾两点
var start = new Point2D(curve.StartPoint.X, curve.StartPoint.Y);
var end = new Point2D(curve.EndPoint.X, curve.EndPoint.Y);

return new[] { new Line2D(start, end) };
```

**为什么不提取中间顶点？**

假设有一条多段线：`A → B → C → D`

- ❌ **错误做法**：提取 3 条线段 `[A→B, B→C, C→D]`
  - 会创建额外的内部顶点 B 和 C
  - 生成的 DCEL 结构完全不同
  - 面的数量和形状错误

- ✅ **正确做法**：只提取 1 条线段 `[A→D]`
  - 保持原有的拓扑结构
  - 与用户的意图一致（多段线作为一个整体边界）

#### 代码位置

**文件**: `HyCADTool.Refactored/Infrastructure/AutoCAD/Services/CurveSegmentExtractor.cs`

```csharp
public Line2D[] ExtractFromCurve(Curve curve, double tolerance)
{
    if (curve == null)
        return Array.Empty<Line2D>();

    // 关键：所有曲线都只取首尾两点
    var start = new Point2D(curve.StartPoint.X, curve.StartPoint.Y);
    var end = new Point2D(curve.EndPoint.X, curve.EndPoint.Y);
    
    return new[] { new Line2D(start, end) };
}
```

---

### 步骤 2: 创建顶点和半边对

#### 顶点去重逻辑

使用**容差相等比较器**合并距离小于容差的顶点：

```csharp
private class Point2DEqualityComparer : IEqualityComparer<Point2D>
{
    private readonly Tolerance _tolerance;

    public bool Equals(Point2D p1, Point2D p2)
    {
        return Math.Abs(p1.X - p2.X) <= _tolerance.Value &&
               Math.Abs(p1.Y - p2.Y) <= _tolerance.Value;
    }

    public int GetHashCode(Point2D point)
    {
        // 将坐标映射到网格单元
        int hashX = Math.Round(point.X / _tolerance.Value).GetHashCode();
        int hashY = Math.Round(point.Y / _tolerance.Value).GetHashCode();
        return hashX ^ hashY;
    }
}
```

**为什么需要容差？**

- CAD 图形中的坐标可能有微小误差（如 `10.0` vs `10.0000001`）
- 容差 `0.01` 确保这些"几乎重合"的点被视为同一顶点
- 避免产生大量冗余的孤立顶点

#### 半边对创建

```csharp
public (HalfEdge, HalfEdge) AddEdgePair(Vertex origin, Vertex destination)
{
    var he1 = new HalfEdge { StartVertex = origin };
    var he2 = new HalfEdge { StartVertex = destination };

    // 设置孪生关系
    he1.Twin = he2;
    he2.Twin = he1;

    // 添加到顶点的出边列表
    origin.OutgoingHalfedges.Add(he1);
    destination.OutgoingHalfedges.Add(he2);

    HalfEdges.Add(he1);
    HalfEdges.Add(he2);

    return (he1, he2);
}
```

#### 代码位置

**文件**: `HyCADTool.Refactored/Domain/Services/GeometryAlgorithms/DCELBuilderService.cs`

---

### 步骤 3: 删除孤立顶点

#### 为什么需要删除孤立顶点？

假设图形中有悬垂边（dangling edges）：

```
    A ───── B
            │
            C ───── D
```

- 顶点 A 的度数为 1（只有一条出边 `A→B`）
- 这条边无法形成闭合面，应该删除
- 删除后 B 的度数也变为 1，继续删除
- 最终只保留能形成闭合面的顶点

#### 迭代删除算法

```csharp
private void RemoveIsolatedVertices(DCELGraph graph)
{
    int iteration = 0;
    bool hasRemoved = true;

    while (hasRemoved && iteration < MaxIterations)
    {
        hasRemoved = false;
        var halfEdgesToRemove = new HashSet<HalfEdge>();

        // 收集度数 ≤ 1 的顶点的关联半边
        foreach (var he in graph.HalfEdges.ToList())
        {
            if (he.StartVertex.OutgoingHalfedges.Count <= 1)
            {
                halfEdgesToRemove.Add(he);
                if (he.Twin != null)
                    halfEdgesToRemove.Add(he.Twin);
            }
        }

        // 删除半边
        if (halfEdgesToRemove.Count > 0)
        {
            hasRemoved = true;
            foreach (var he in halfEdgesToRemove)
            {
                he.StartVertex.OutgoingHalfedges.Remove(he);
                graph.HalfEdges.Remove(he);
            }

            // 删除度数为 0 的顶点
            var verticesToRemove = graph.Vertices
                .Where(v => v.OutgoingHalfedges.Count == 0)
                .ToList();
            foreach (var vertex in verticesToRemove)
            {
                graph.Vertices.Remove(vertex);
            }
        }
        iteration++;
    }
}
```

**最大迭代次数**：防止死循环（通常不超过 1000 次）

---

### 步骤 4: 构建面（左侧法则）

#### 什么是左侧法则？

在平面细分中，沿着半边逆时针遍历，左侧总是面的内部。

**图解**：

```
外轮廓（逆时针）：
        ↑
    ┌───┼───┐
    │   │   │
  ←─┤   ●   ├─→
    │       │
    └───────┘
        ↓

内部孔洞（顺时针）：
        ↓
    ┌───┼───┐
    │   │   │
  →─┤   ○   ├─←
    │       │
    └───────┘
        ↑
```

#### 算法流程

```csharp
private void BuildFacesFromHalfEdges(DCELGraph graph)
{
    // 按起点坐标排序半边（确保遍历顺序一致）
    var halfEdgeSet = graph.HalfEdges
        .OrderBy(he => he.StartVertex.Position.X)
        .ThenBy(he => he.StartVertex.Position.Y)
        .ThenBy(he => he.Twin.StartVertex.Position.Y)
        .ToList();

    while (halfEdgeSet.Any())
    {
        // 找到第一条未初始化的半边
        var startEdge = halfEdgeSet.FirstOrDefault(he => !he.IsInitialized);
        if (startEdge == null)
            break;

        var currentEdge = startEdge;
        var faceEdges = new List<HalfEdge>();
        int iterationCount = 0;

        // 沿着左侧法则遍历，直到回到起点
        do
        {
            faceEdges.Add(currentEdge);
            currentEdge.IsInitialized = true;

            // 找到逆时针方向的下一条半边
            var nextEdge = FindNextEdgeCounterClockwise(currentEdge);
            if (nextEdge == null)
                break;

            currentEdge = nextEdge;
            iterationCount++;

        } while (currentEdge != null && 
                 currentEdge != startEdge && 
                 iterationCount < MaxIterations);

        // 如果形成闭合循环，创建面
        if (currentEdge == startEdge && faceEdges.Count >= 3)
        {
            var face = graph.CreateFace(faceEdges);
            face.SetOrientation(); // 根据有向面积设置 IsOuter
        }
    }
}
```

---

## 关键几何计算

### 左侧法则：找到下一条半边

#### ⚠️ 核心逻辑：反向向量

这是**最容易出错**的地方！必须使用**反向向量**而不是正向向量。

```csharp
private HalfEdge FindNextEdgeCounterClockwise(HalfEdge currentEdge)
{
    var vertex1 = currentEdge.StartVertex;
    var vertex2 = currentEdge.Twin.StartVertex;
    
    // ⚠️ 关键：使用反向向量（从 vertex2 指向 vertex1）
    // 这与原代码完全一致！不要"优化"为正向向量！
    var vector = new Vector2D(
        vertex1.Position.X - vertex2.Position.X,
        vertex1.Position.Y - vertex2.Position.Y
    );

    HalfEdge bestEdge = null;
    double minAngle = double.MaxValue;

    // 遍历从 vertex2 出发的所有半边
    foreach (var candidate in vertex2.OutgoingHalfedges)
    {
        if (candidate == currentEdge.Twin)
            continue; // 跳过孪生边

        // 计算候选边的向量（从 vertex2 指向目标顶点）
        var currentVector = new Vector2D(
            candidate.Twin.StartVertex.Position.X - vertex2.Position.X,
            candidate.Twin.StartVertex.Position.Y - vertex2.Position.Y
        );
        
        // 计算逆时针角度
        double angle = CalculateCounterClockwiseAngle(vector, currentVector);

        if (angle < minAngle)
        {
            minAngle = angle;
            bestEdge = candidate;
        }
    }

    return bestEdge;
}
```

#### 为什么必须用反向向量？

**几何原理**：

```
当前边：A ──────→ B
反向：   A ←────── B

下一条候选边：B ──────→ C1, B ──────→ C2, B ──────→ C3
```

计算"从反向向量 BA 到候选向量 BC_i 的逆时针角度"，选择**最小角度**的候选边。

**图解**：

```
           C2
          /
         /  ← 最小角度 θ_min
        /
    A ─────→ B ─────→ C1
                \
                 \
                  C3
```

如果使用正向向量 `AB`，角度计算会完全相反，导致选择错误的下一条边！

#### 代码位置

**文件**: `HyCADTool.Refactored/Domain/Services/GeometryAlgorithms/DCELBuilderService.cs`

---

### 逆时针角度计算

#### 公式

```csharp
private double CalculateCounterClockwiseAngle(Vector2D from, Vector2D to)
{
    double dot = from.X * to.X + from.Y * to.Y;          // 点积
    double crossZ = from.X * to.Y - from.Y * to.X;       // 叉积的Z分量
    double angle = Math.Atan2(crossZ, dot);              // 反正切
    return angle >= 0 ? angle : (2 * Math.PI + angle);  // 映射到 [0, 2π]
}
```

#### 几何意义

- **点积** `dot`：衡量两向量的"相似度"（余弦关系）
- **叉积Z分量** `crossZ`：
  - `crossZ > 0`：`to` 在 `from` 的**左侧**（逆时针）
  - `crossZ < 0`：`to` 在 `from` 的**右侧**（顺时针）
  - `crossZ = 0`：`to` 与 `from` **共线**

- **Atan2(crossZ, dot)**：计算从 `from` 逆时针旋转到 `to` 的角度
  - 范围：`[-π, π]`
  - 如果 `angle < 0`，加 `2π` 映射到 `[0, 2π]`

#### 示例

```
from = (1, 0)   // 指向东
to1  = (0, 1)   // 指向北
→ angle1 = π/2  (90°)

to2  = (-1, 0)  // 指向西
→ angle2 = π    (180°)

to3  = (0, -1)  // 指向南
→ angle3 = 3π/2 (270°)
```

---

## 面方向判断

### 有向面积（Signed Area）

#### 公式

使用**Shoelace 公式**（鞋带公式）：

```csharp
public double CalculateSignedArea()
{
    if (Components == null || Components.Count < 3)
        return 0.0;

    double area = 0.0;
    foreach (var he in Components)
    {
        var current = he.StartVertex.Position;
        var next = he.Next.StartVertex.Position;
        area += (current.X * next.Y) - (next.X * current.Y);
    }

    return area * 0.5;
}
```

#### 几何意义

- **正值**（`area > 0`）：顶点按**逆时针**排列 → **外轮廓**
- **负值**（`area < 0`）：顶点按**顺时针**排列 → **内部孔洞**

#### 图解

```
逆时针（外轮廓）：
  1 ───→ 2
  ↑       ↓
  │       │
  4 ←─── 3

有向面积 = (1*2 - 1*2) + (2*3 - 1*3) + ... > 0

顺时针（内部）：
  1 ───→ 2
  ↓       ↑
  │       │
  4 ←─── 3

有向面积 = (1*4 - 1*2) + (2*3 - 2*4) + ... < 0
```

#### 代码位置

**文件**: `HyCADTool.Refactored/Domain/DataStructures/DCEL/Face.cs`

```csharp
internal void SetOrientation()
{
    double signedArea = CalculateSignedArea();
    IsOuter = signedArea > 0;
}
```

---

## 关键代码片段

### 完整流程入口

**文件**: `HyCADTool.Refactored/Domain/Services/GeometryAlgorithms/DCELBuilderService.cs`

```csharp
public DCELGraph BuildFromSegments(List<Line2D> segments, Tolerance tolerance)
{
    if (segments == null || segments.Count == 0)
        return new DCELGraph();

    var graph = new DCELGraph();
    var vertexMap = new Dictionary<Point2D, Vertex>(
        new Point2DEqualityComparer(tolerance)
    );

    // 步骤 1-2: 创建顶点和半边对
    foreach (var segment in segments)
    {
        var startVertex = GetOrCreateVertex(graph, vertexMap, segment.StartPoint);
        var endVertex = GetOrCreateVertex(graph, vertexMap, segment.EndPoint);
        graph.AddEdgePair(startVertex, endVertex);
    }

    // 步骤 3: 删除孤立顶点
    RemoveIsolatedVertices(graph);
    
    // 步骤 4-5: 构建面并设置方向
    BuildFacesFromHalfEdges(graph);

    return graph;
}
```

### DCELGraph 聚合根

**文件**: `HyCADTool.Refactored/Domain/DataStructures/DCEL/DCELGraph.cs`

```csharp
public class DCELGraph
{
    public List<Vertex> Vertices { get; private set; }
    public List<HalfEdge> HalfEdges { get; private set; }
    public List<Face> Faces { get; private set; }

    // 动态属性：根据 Face.IsOuter 过滤
    public IEnumerable<Face> OuterFaces => Faces.Where(f => f.IsOuter);
    public IEnumerable<Face> InnerFaces => Faces.Where(f => !f.IsOuter);

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

---

## 常见陷阱

### ❌ 陷阱 1：使用正向向量计算角度

**错误代码**：

```csharp
// ❌ 错误！这会导致面构建完全错误
var incomingVector = currentEdge.GetVector();  // 正向向量：A → B
```

**原因**：

- 左侧法则需要从**反向向量**到候选向量的角度
- 使用正向向量会选择错误的下一条边

**正确代码**：

```csharp
// ✅ 正确！使用反向向量
var vector = new Vector2D(
    vertex1.Position.X - vertex2.Position.X,  // B → A (反向)
    vertex1.Position.Y - vertex2.Position.Y
);
```

---

### ❌ 陷阱 2：提取多段线的中间顶点

**错误代码**：

```csharp
// ❌ 错误！会生成错误的拓扑结构
for (int i = 0; i < pline.NumberOfVertices - 1; i++)
{
    var p1 = pline.GetPoint2dAt(i);
    var p2 = pline.GetPoint2dAt(i + 1);
    segments.Add(new Line2D(p1, p2));  // 添加每个小段
}
```

**原因**：

- 用户选择的多段线应作为**单一边界**
- 提取中间顶点会创建额外的内部结构

**正确代码**：

```csharp
// ✅ 正确！只取首尾两点
var start = new Point2D(curve.StartPoint.X, curve.StartPoint.Y);
var end = new Point2D(curve.EndPoint.X, curve.EndPoint.Y);
return new[] { new Line2D(start, end) };
```

---

### ❌ 陷阱 3：忘记删除孤立顶点

**问题**：

- 悬垂边会干扰面构建算法
- 产生无效的半边循环

**解决**：

- 在面构建前**必须删除**所有度数 ≤ 1 的顶点
- 使用迭代算法，直到没有孤立顶点

---

### ❌ 陷阱 4：容差设置不当

**太小的容差**（如 `1e-10`）：

- 微小的坐标误差导致顶点不合并
- 产生大量冗余顶点
- 面构建失败

**太大的容差**（如 `1.0`）：

- 不同的顶点被错误合并
- 拓扑结构错误

**推荐值**：`0.01` ~ `0.1`（根据图形精度调整）

---

## 调试技巧

### 1. 可视化半边方向

在 AutoCAD 中绘制箭头表示半边方向：

```csharp
foreach (var he in graph.HalfEdges)
{
    var start = he.StartVertex.Position;
    var end = he.Twin.StartVertex.Position;
    // 绘制箭头从 start 指向 end
}
```

### 2. 检查面的闭合性

验证每个面是否形成闭合循环：

```csharp
var startEdge = face.Components.FirstOrDefault();
var currentEdge = startEdge;
int count = 0;

do
{
    currentEdge = currentEdge.Next;
    count++;
} while (currentEdge != startEdge && count < 1000);

if (currentEdge != startEdge)
{
    // 面未闭合，存在错误！
}
```

### 3. 输出顶点度数

```csharp
foreach (var vertex in graph.Vertices)
{
    Console.WriteLine($"Vertex at ({vertex.Position.X}, {vertex.Position.Y}): " +
                      $"Degree = {vertex.OutgoingHalfedges.Count}");
}
```

度数为 1 或 0 的顶点应该在步骤 3 被删除。

---

## 参考资料

### 学术文献

1. **Mark de Berg et al.** - *Computational Geometry: Algorithms and Applications*  
   第 2 章：Line Segment Intersection（线段相交）  
   第 9 章：Delaunay Triangulations（Delaunay 三角剖分）

2. **Preparata & Shamos** - *Computational Geometry: An Introduction*  
   第 2.3 节：Doubly-Connected Edge List

### 在线资源

- [Wikipedia: Doubly connected edge list](https://en.wikipedia.org/wiki/Doubly_connected_edge_list)
- [CGAL: Halfedge Data Structures](https://doc.cgal.org/latest/HalfedgeDS/index.html)

---

## 维护建议

### 代码修改前必读

1. **不要修改向量方向**
   - `FindNextEdgeCounterClockwise` 中的反向向量逻辑是**刻意设计**的
   - 任何看似"优化"的正向向量都会破坏功能

2. **不要修改曲线提取逻辑**
   - 只提取 `StartPoint` 和 `EndPoint` 是**刻意简化**的
   - 保持与原代码的行为一致

3. **修改后必须测试**
   - 对比原命令 `hyDcel` 和新命令的结果
   - 验证多种测试案例（简单矩形、带孔多边形、复杂图形）

### 扩展指南

如果要支持曲线离散化（Arc, Circle, Spline）：

1. 在 `ICurveSegmentExtractor` 中添加方法：
   ```csharp
   Line2D[] ExtractFromArc(Arc arc, double chordTolerance);
   ```

2. 将曲线离散化为多条小线段：
   ```csharp
   // 根据弦高容差计算分段数
   int segmentCount = CalculateSegmentCount(arc.Radius, chordTolerance);
   ```

3. **注意**：离散化后的线段**都会**成为 DCEL 的边，不同于多段线的处理！

---

## 总结

### 核心要点

1. **DCEL = Vertex + HalfEdge + Face**
   - 每条边拆分为两条方向相反的半边
   - 半边通过 Twin, Next, Prev 建立拓扑关系

2. **左侧法则 = 反向向量 + 最小逆时针角度**
   - 必须使用反向向量（从终点指向起点）
   - 选择角度最小的候选边作为下一条边

3. **面方向 = 有向面积的符号**
   - 正值 = 逆时针 = 外轮廓
   - 负值 = 顺时针 = 内部

4. **曲线提取 = 只取首尾两点**
   - 保持原有拓扑结构
   - 不提取中间顶点

### 文档维护

- **修改代码后**：更新本文档的对应部分
- **添加新功能**：补充到"扩展指南"章节
- **发现问题**：记录到"常见陷阱"章节

---

**文档版本**: v1.0  
**最后更新**: 2024-10-15  
**维护者**: HyCADTool 开发团队







