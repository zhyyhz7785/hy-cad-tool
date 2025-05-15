# Axis & AxisDatas 类说明文档

本模块定义了两个核心类型：`Axis` 和 `AxisDatas`，用于在 AutoCAD 二次开发中构建结构轴线系统，并按轴线划分平面区域，同时支持对任意点集合进行区域归属分组。

---

## ✨ 类型概览

### `Axis` 类

表示单根轴线对象，自动识别方向、生成编号与注释图元。

| 属性/方法         | 类型                             | 说明                                 |
|------------------|----------------------------------|--------------------------------------|
| `Line`           | `Line`                           | 原始轴线对象                         |
| `IsVertical`     | `bool`                           | 是否为垂直轴线                      |
| `Position`       | `double`                         | 垂直轴线返回 X 坐标，水平轴线返回 Y 坐标 |
| `SerialNumber`   | `int`                            | 编号，从 1 开始                      |
| `Name`           | `string`                         | X 轴为“1,2,3...”，Y 轴为“A,B,C...”     |
| `CircleCenter`   | `Point3d`                        | 标注圆心坐标                         |
| `Annotation`     | `(string Label, Circle Circle)?` | 包含圆圈与标签                       |
| `D` _(static)_   | `double`                         | 标注尺寸，默认为 `8 * BaseConfig.Scale` |
| `EnsureDirection`| `Line`                           | 保证轴线方向 X/Y 递增                |

---

### `AxisDatas` 类

表示一组结构轴线及其区域划分与注释数据。

| 属性/方法                  | 类型                                   | 说明                                         |
|---------------------------|----------------------------------------|----------------------------------------------|
| `XAxes` / `YAxes`         | `List<Axis>`                           | 已排序的轴线集合                             |
| `AxisCircles`             | `List<Circle>`                         | 轴线注释圆形                                 |
| `AxisTexts`               | `List<DBText>`                         | 轴线注释文字                                 |
| `RegionMap`               | `Dictionary<string, Extents3d>`        | 区域名称与边界映射                           |
| `RegionLabels`            | `Dictionary<string, Point3d>`          | 区域中心标签位置                             |
| `RegionFrames`            | `List<Polyline>`                       | 区域矩形边界框（多段线）                     |
| `RegionTexts`             | `List<DBText>`                         | 区域标签文字                                 |
| `PointsMap`               | `Dictionary<Extents3d, List<Point3d>>` | 每个区域对应的点集（需调用构造或手动填充）   |
| `OuterExtension` _(static)_ | `(double XExtend, double YExtend)`  | 单轴线边界扩展值，默认 (15000, 15000)       |

---

## 🏗️ 构造方法

```csharp
// 不填充点映射
var axisData = AxisDatas.FromLines(IEnumerable<Line> axisLines);

// 自动分组点数据
var axisData = AxisDatas.FromLines(IEnumerable<Line> axisLines, List<Point3d> allPoints);

// 延迟填充点区域映射
axisData.FillPointsMap(List<Point3d> allPoints);
