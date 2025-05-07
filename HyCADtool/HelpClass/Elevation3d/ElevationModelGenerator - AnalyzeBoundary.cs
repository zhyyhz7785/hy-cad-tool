using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using System.Collections.Generic;
namespace HyCADTool.HelpClass.CreatBase
{
    public partial class ElevationModelGenerator
    {
        public Dictionary<Polyline, List<BoundaryCondition>> AnalyzeBoundaryConditions()
        {
            var boundaryConditions = new Dictionary<Polyline, List<BoundaryCondition>>();
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            double tolerance = BaseConfig.ToleranceDouble;
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                // 图层处理代码保持不变
                foreach (var polygon in Polygons)
                {
                    var conditions = new List<BoundaryCondition>();
                    int numVertices = polygon.NumberOfVertices;
                    for (int i = 0; i < numVertices; i++)
                    {
                        Point3d startPoint = polygon.GetPoint3dAt(i);
                        Point3d endPoint = polygon.GetPoint3dAt((i + 1) % numVertices);
                        var edge = new Line(startPoint, endPoint);
                        bool isSoilBoundary = false;
                        Polyline adjacentPolygon = null;
                        Line coincidentEdge = null;
                        // 检查土壤边界
                        foreach (var outer in OuterContours)
                        {
                            for (int j = 0; j < outer.NumberOfVertices; j++)
                            {
                                Point3d outerStart = outer.GetPoint3dAt(j);
                                Point3d outerEnd = outer.GetPoint3dAt((j + 1) % outer.NumberOfVertices);
                                var outerEdge = new Line(outerStart, outerEnd);
                                if (IsLineEqual(edge, outerEdge, tolerance) || IsLineEqual(edge, ReverseLine(outerEdge), tolerance))
                                {
                                    isSoilBoundary = true;
                                    //coincidentEdge = outerEdge; // 如果是土壤边界，记录重合边
                                    break;
                                }
                            }
                            if (isSoilBoundary) break;
                        }
                        // 如果不是土壤边界，检查相邻多边形
                        if (!isSoilBoundary)
                        {
                            foreach (var otherPolygon in Polygons)
                            {
                                if (otherPolygon == polygon) continue;
                                for (int j = 0; j < otherPolygon.NumberOfVertices; j++)
                                {
                                    Point3d otherStart = otherPolygon.GetPoint3dAt(j);
                                    Point3d otherEnd = otherPolygon.GetPoint3dAt((j + 1) % otherPolygon.NumberOfVertices);
                                    var otherEdge = new Line(otherStart, otherEnd);
                                    if (IsLineEqual(edge, otherEdge, tolerance) || IsLineEqual(edge, ReverseLine(otherEdge), tolerance))
                                    {
                                        adjacentPolygon = otherPolygon;
                                        coincidentEdge = otherEdge; // 记录相邻多边形的重合边
                                        break;
                                    }
                                }
                                if (adjacentPolygon != null) break;
                            }
                            // 外包多边形逻辑保持不变
                        }
                        var isWall = BoundaryCondition.DetermineIsWall(polygon, ElevationsDic);
                        conditions.Add(new BoundaryCondition(edge, isSoilBoundary, adjacentPolygon, isWall, coincidentEdge));
                    }
                    boundaryConditions[polygon] = conditions;
                }
                tr.Commit();
            }
            return boundaryConditions;
        }
    }
}