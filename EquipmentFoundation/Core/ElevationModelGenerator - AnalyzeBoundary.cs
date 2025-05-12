using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using EquipmentFoundation;
using HyCADTool.Config;
using EquipmentFoundation.Models;
namespace EquipmentFoundation
{
    public partial class ElevationModelGenerator
    {
        private Dictionary<Polyline, List<BoundaryCondition>> AnalyzeBoundaryConditions(GeometryInput input)
        {
            var boundaryConditions = new Dictionary<Polyline, List<BoundaryCondition>>();
            double tolerance = 0.001;
            foreach (var polygon in input.InnerPolygons)
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
                    foreach (var outer in input.OuterContours)
                    {
                        for (int j = 0; j < outer.NumberOfVertices; j++)
                        {
                            Point3d outerStart = outer.GetPoint3dAt(j);
                            Point3d outerEnd = outer.GetPoint3dAt((j + 1) % outer.NumberOfVertices);
                            var outerEdge = new Line(outerStart, outerEnd);
                            if (IsLineEqual(edge, outerEdge, tolerance) || IsLineEqual(edge, ReverseLine(outerEdge), tolerance))
                            {
                                isSoilBoundary = true;
                                coincidentEdge = outerEdge;
                                break;
                            }
                        }
                        if (isSoilBoundary) break;
                    }
                    if (!isSoilBoundary)
                    {
                        foreach (var otherPolygon in input.InnerPolygons)
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
                                    coincidentEdge = otherEdge;
                                    break;
                                }
                            }
                            if (adjacentPolygon != null) break;
                        }
                    }
                    var isWall = BoundaryCondition.DetermineIsWall(polygon, input.Elevations);
                    conditions.Add(new BoundaryCondition(edge, isSoilBoundary, adjacentPolygon, isWall, coincidentEdge));
                }
                boundaryConditions[polygon] = conditions;
            }
            return boundaryConditions;
        }
    }
}