以下是整理回顾的 **Annotation（标注）模块**所有核心代码结构与功能说明，使用 Markdown 格式记录，便于归档、开发协作与后续扩展。

---

# 📐 `Annotation` 模块设计说明（AutoCAD 二次开发）

本模块用于在 AutoCAD 中基于聚类区域（`ClusterResult`）和轴线布置，**自动生成多层级的 X/Y 向标注**，适用于结构布局、构件编号、工艺布置等工程图场景。

---

## 📁 命名空间与结构概览

```text
HyCADTool/
├── Annotation/
│   ├── AnnotationFramework.cs        # 主流程调度器
│   ├── AutoCADDimensionHelper.cs     # 标注绘制工具类（调用 AutoCAD API）
│   └── ClusterAnnotator.cs           # 标注算法模块（多层级处理）
```

---

## 📘 1. `AnnotationFramework.cs`

> **功能**：标注系统主流程调度器，外部调用统一入口。

* **方法**：

  ```csharp
  public void Run(List<ClusterResult> clusters, List<Line> xAxes, List<Line> yAxes, double scale)
  ```

* **工作流程**：

  1. 调用 `AxisIntersectionMapper`，建立 ClusterResult → 最近轴线交点 的映射；
  2. 为每个 ClusterResult 添加其 MBR 内部的附加交点；
  3. 传入 `ClusterAnnotator.Annotate()` 执行分层标注逻辑。

---

## 📘 2. `ClusterAnnotator.cs`

> **功能**：执行分层判定、多点集聚合与 X/Y 向维度分析，控制标注生成逻辑。

* **构造函数**：

  ```csharp
  public ClusterAnnotator(double scale)
  ```

* **主方法**：

  ```csharp
  public void Annotate(Dictionary<ClusterResult, Point3d> mapping)
  ```

* **主要逻辑**：

  * 整合每个 `ClusterResult` 中心点 + 附加交点 → 标注点集；
  * 判断是否需多层级标注（高度 / 宽度 + 点密度）：

    * 批次 = \$ \lceil \frac{h}{3 \cdot 5 \cdot \text{scale}} \rceil \$
    * 间距小于 `3 × scale` 出现 ≥2 次即考虑分层；
  * 按 X/Y 分别进行点分组排序，并调用 `AutoCADDimensionHelper` 执行标注；
  * 每个批次标注偏移不同位置。

---

## 📘 3. `AutoCADDimensionHelper.cs`

> **功能**：封装 AutoCAD API 对 `AlignedDimension` 的创建调用，绘制 X/Y 向标注。

* **方法**：

  ```csharp
  public static void AddXDimension(List<Point3d> points, Point3d dimLinePoint)
  public static void AddYDimension(List<Point3d> points, Point3d dimLinePoint)
  ```

* **特征**：

  * 点排序后逐对添加 `AlignedDimension`；
  * 默认图层：

    * X向：`00_hy_3公共_标注2_内x`
    * Y向：`00_hy_3公共_标注2_内y`
  * 使用 `Transaction` 写入标注对象；
  * 标注线位置采用统一偏移量（由 `ClusterAnnotator` 决定）。

---

## 📘 4. 调用入口命令（推荐在 `HyCommand.cs` 中注册）

```csharp
[CommandMethod("HY_AnnotateClusters")]
public static void AnnotateClusters()
```

* 提示输入 `scale`；
* 从模型空间获取点、轴线、聚类结果；
* 执行：

  ```csharp
  var framework = new AnnotationFramework();
  framework.Run(clusters, xAxes, yAxes, scale);
  ```

---

## ✅ 数据结构依赖说明

### `ClusterResult`（定义于 `HyCADTool.Models.Cluster`）

```csharp
public class ClusterResult
{
    public int ClusterId;
    public List<Point3d> Points;
    public Polyline EnvelopePolyline;
    public Extents3d EnvelopeExtents;      // MBR
    public List<Point3d> AdditionalIntersections;
    public Point3d Center { get; }          // (min+max)/2
    public List<Point3d> AllPoints { get; } // 中心点 + 附加点
}
```

---

## 🧩 依赖组件

* `HyCADTool.Models.ClusterResult`（聚类结构）
* `HyCADTool.GeometryUtils.AxisIntersectionMapper`（轴线交点计算）
* AutoCAD .NET API（`AlignedDimension`, `Polyline`, `Line`, `Transaction` 等）

---

## ✅ 扩展建议（可选）

| 功能方向   | 建议说明                     |
| ------ | ------------------------ |
| 多引线标注  | 可结合 MLeader 绘制编号说明       |
| 标注分组   | 按结构部件编号/区域编号分层管理         |
| UI面板集成 | 通过 WPF 设置标注参数，如偏移系数、图层名等 |
| 标注检查工具 | 提供重复标注、重叠检查逻辑，优化图纸整洁性    |

---

是否需要我将以上文档输出为 `.md` 文件，便于您存入项目文档中？
