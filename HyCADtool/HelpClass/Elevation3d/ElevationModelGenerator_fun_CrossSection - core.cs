using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.HelpClass.CreatBase
{
    public partial class ElevationModelGenerator
    {
        /// <summary>
        /// 为每个 GeometryData 生成独立的二维底板剖面图，基于剖断线与 Polygon 的交点对
        /// </summary>
        /// <param name="points">交点数据列表，包含距离、标高和几何信息</param>
        /// <param name="sectionLine">剖断线，用于计算偏移</param>
        /// <param name="normalVector">偏移方向的法向量</param>
        /// <param name="db">当前数据库</param>
        /// <param name="ed">编辑器，用于输出调试信息</param>
        /// <param name="tr">事务对象</param>
        /// <param name="raftThickness">默认底板厚度</param>
        /// <summary>
        /// 为每个 GeometryData 生成独立的二维底板剖面图，基于两个交点构成顶面
        /// </summary>
        /// <param name="points">交点数据列表，包含距离、标高和几何信息</param>
        /// <param name="sectionLine">剖断线，用于计算偏移</param>
        /// <param name="normalVector">偏移方向的法向量</param>
        /// <param name="db">当前数据库</param>
        /// <param name="ed">编辑器，用于输出调试信息</param>
        /// <param name="tr">事务对象</param>
        /// <param name="raftThickness">默认底板厚度</param>
        private void GenerateBase(List<(double XDistance, double InnerElevation, double OuterElevation,
            Point3d Point, GeometryData GeomData, WallData WallData)> points,
            Line sectionLine, Vector3d normalVector, Database db, Editor ed, Transaction tr, double raftThickness)
        {
            if (points.Count < 2)
            {
                ed.WriteMessage("\n警告：总交点数不足（{points.Count}），无法生成底板剖面");
                return;
            }

            // 按 GeometryData 分组交点
            var groupedPoints = points.GroupBy(p => p.GeomData);
            int baseIndex = 0;

            foreach (var group in groupedPoints)
            {
                GeometryData geomData = group.Key;
                var groupPoints = group.OrderBy(p => p.XDistance).ToList();

                // 确保恰好有 2 个交点
                if (groupPoints.Count != 2)
                {
                    ed.WriteMessage($"\n警告：底板[{baseIndex}] 交点数异常（{groupPoints.Count}），应为 2 个，跳过生成");
                    continue;
                }

                // 创建底板轮廓（四边形）
                Polyline baseLine = new Polyline { Layer = BASE_LAYER };

                // 获取顶边两个端点
                var startPoint = groupPoints[0]; // 左侧交点
                var endPoint = groupPoints[1];   // 右侧交点

                double xStart = startPoint.XDistance;
                double topZStart = Math.Min(startPoint.InnerElevation, startPoint.OuterElevation); // 左侧顶部标高
                double xEnd = endPoint.XDistance;
                double topZEnd = Math.Min(endPoint.InnerElevation, endPoint.OuterElevation);       // 右侧顶部标高

                // 计算底板底部标高（向下延伸厚度）
                double baseThickness = geomData.BaseThickness > 0 ? geomData.BaseThickness : raftThickness;
                double baseZStart = topZStart - baseThickness; // 左侧底部标高
                double baseZEnd = topZEnd - baseThickness;     // 右侧底部标高

                // 添加四边形顶点（顺时针）
                baseLine.AddVertexAt(0, new Point2d(xStart, topZStart), 0, 0, 0); // 左上（顶边起点）
                baseLine.AddVertexAt(1, new Point2d(xEnd, topZEnd), 0, 0, 0);    // 右上（顶边终点）
                baseLine.AddVertexAt(2, new Point2d(xEnd, baseZEnd), 0, 0, 0);   // 右下
                baseLine.AddVertexAt(3, new Point2d(xStart, baseZStart), 0, 0, 0); // 左下
                baseLine.Closed = true;

                // 调试输出
                ed.WriteMessage($"\n底板[{baseIndex}] 剖面: XStart={xStart}, TopZStart={topZStart}, BaseZStart={baseZStart}");
                ed.WriteMessage($"\n底板[{baseIndex}] 剖面: XEnd={xEnd}, TopZEnd={topZEnd}, BaseZEnd={baseZEnd}");
                ed.WriteMessage($"\n底板[{baseIndex}] 生成完成，顶点数={baseLine.NumberOfVertices}, Closed={baseLine.Closed}, 厚度={baseThickness}");

                // 绘制到 AutoCAD
                DrawPolylineToAutoCAD(baseLine, sectionLine.StartPoint, normalVector, db, tr, BASE_LAYER);

                baseIndex++;
            }
        }

        /// <summary>
        /// 生成外墙轮廓并绘制到 AutoCAD，外墙位于外侧
        /// </summary>
        /// <param name="points">交点数据列表，包含距离、标高和几何信息</param>
        /// <param name="sectionLine">剖断线，用于计算偏移</param>
        /// <param name="normalVector">偏移方向的法向量</param>
        /// <param name="db">当前数据库</param>
        /// <param name="ed">编辑器，用于输出调试信息</param>
        /// <param name="tr">事务对象</param>
        private void GenerateWalls(List<(double XDistance, double InnerElevation, double OuterElevation,
            Point3d Point, GeometryData GeomData, WallData WallData)> points,
            Line sectionLine, Vector3d normalVector, Database db, Editor ed, Transaction tr)
        {
            // 遍历所有交点，生成墙体
            foreach (var point in points)
            {
                // 检查是否为墙体且厚度大于 0
                if (point.WallData.IsWall && point.WallData.Thickness > 0)
                {
                    double x = point.XDistance; // 沿剖断线的 X 坐标
                    double topZ = Math.Max(point.InnerElevation, point.OuterElevation); // 墙顶取较高标高
                    double baseZ = Math.Min(point.InnerElevation, point.OuterElevation); // 墙底取较低标高（与底板顶部对齐）
                    double thickness = point.WallData.Thickness; // 墙体厚度

                    // 判断墙体扩展方向：外侧标高较低时向右扩展，否则向左扩展
                    bool isOuterLower = point.OuterElevation < point.InnerElevation;
                    double xInner = x; // 内侧边界与交点对齐
                    double xOuter = isOuterLower ? x + thickness : x - thickness; // 外侧边界向外扩展厚度距离

                    // 创建墙体轮廓的多边形对象
                    Polyline wallLine = new Polyline { Layer = WALL_LAYER };

                    // 根据扩展方向添加顶点，形成闭合矩形
                    if (isOuterLower)
                    {
                        // 外侧标高较低，墙体向右扩展
                        wallLine.AddVertexAt(0, new Point2d(xInner, baseZ), 0, 0, 0); // 左下（内侧）
                        wallLine.AddVertexAt(1, new Point2d(xInner, topZ), 0, 0, 0);  // 左上（内侧）
                        wallLine.AddVertexAt(2, new Point2d(xOuter, topZ), 0, 0, 0);  // 右上（外侧）
                        wallLine.AddVertexAt(3, new Point2d(xOuter, baseZ), 0, 0, 0); // 右下（外侧）
                    }
                    else
                    {
                        // 内侧标高较低，墙体向左扩展
                        wallLine.AddVertexAt(0, new Point2d(xOuter, baseZ), 0, 0, 0); // 左下（外侧）
                        wallLine.AddVertexAt(1, new Point2d(xOuter, topZ), 0, 0, 0);  // 左上（外侧）
                        wallLine.AddVertexAt(2, new Point2d(xInner, topZ), 0, 0, 0);  // 右上（内侧）
                        wallLine.AddVertexAt(3, new Point2d(xInner, baseZ), 0, 0, 0); // 右下（内侧）
                    }
                    wallLine.Closed = true;

                    // 调试输出：检查墙体坐标和参数
                    ed.WriteMessage($"\n墙体生成: X={x}, BaseZ={baseZ}, TopZ={topZ}, Thickness={thickness}, Direction={(isOuterLower ? "右" : "左")}");

                    // 绘制墙体到 AutoCAD
                    DrawPolylineToAutoCAD(wallLine, sectionLine.StartPoint, normalVector, db, tr, WALL_LAYER);
                }
            }
        }

    }


}