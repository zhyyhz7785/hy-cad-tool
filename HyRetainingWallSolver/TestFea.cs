using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using HyRetainingWallSolver.Models;
using HyRetainingWallSolver.Tools;
using MSolve.Edu.FEM.Elements;
using MSolve.Edu.FEM.Entities;
using MSolve.Edu.FEM.Material;
using System;
using System.Collections.Generic;
using System.Linq;
[assembly: CommandClass(typeof(HyRetainingWallSolver.Command.HyCommands))]
namespace HyRetainingWallSolver.Command
{
    public  class HyCommands
    {
        [CommandMethod("HYFeaD_DefineWallBoundaries")]
        public static void DefineWallBoundaries()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            var info = HyUtils.GetPolylineInfo("请选择闭合四边形多段线：");
            if (info == null)
                return;
            var (polyline, _, _, _) = info.Value;
            if (!polyline.Closed || polyline.NumberOfVertices != 4)
            {
                ed.WriteMessage("\n所选多段线必须为闭合四边形。\n");
                return;
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var boundaries = new List<WallBoundary>();
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    var p1 = polyline.GetPoint3dAt(i);
                    var p2 = polyline.GetPoint3dAt((i + 1) % polyline.NumberOfVertices);
                    boundaries.Add(new WallBoundary(p1, p2, BoundaryFixity.Fixed));
                }
                while (true)
                {
                    var edgeInfo = HyUtils.GetPolylineInfo("如需修改边界条件，请选择多段线任一边。ESC 退出修改。");
                    if (edgeInfo == null)
                        break;
                    var (_, _, _, segment) = edgeInfo.Value;
                    int index = HyUtils.FindSegmentIndex(boundaries, segment);
                    if (index < 0)
                    {
                        ed.WriteMessage("\n未能识别所选边，可能方向不一致。\n");
                        continue;
                    }
                    PromptKeywordOptions pko = new PromptKeywordOptions("\n请选择边界条件（输入 G=固端，J=简支，Z=自由）：")
                    {
                        AllowNone = false
                    };
                    pko.Keywords.Add("G", "G", "固端");
                    pko.Keywords.Add("J", "J", "简支");
                    pko.Keywords.Add("Z", "Z", "自由");
                    pko.Keywords.Default = "G";
                    PromptResult result = ed.GetKeywords(pko);
                    if (result.Status != PromptStatus.OK)
                        continue;
                    switch (result.StringResult.ToUpperInvariant())
                    {
                        case "G":
                            boundaries[index].Fixity = BoundaryFixity.Fixed;
                            break;
                        case "J":
                            boundaries[index].Fixity = BoundaryFixity.Hinged;
                            break;
                        case "Z":
                            boundaries[index].Fixity = BoundaryFixity.Free;
                            break;
                        default:
                            ed.WriteMessage("\n输入无效，请重新输入 G、J 或 Z。\n");
                            continue;
                    }
                }
                HyUtils.WriteToExtensionDictionary(tr, polyline, new WallGeometryInput
                {
                    PolylineId = polyline.ObjectId,
                    Boundaries = boundaries
                }, "WallGeometryInput", ed);
                tr.Commit();
            }
        }
        [CommandMethod("HYFeaSH_ShowWallBoundaries")]
        public static void ShowWallBoundaries()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            double scale = 50; // 统一比例参数
            var info = HyUtils.GetPolylineInfo("请选择一个已保存边界条件的多段线：");
            if (info == null) return;
            var (pline, _, _, _) = info.Value;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var wallData = HyUtils.ReadFromExtensionDictionary<WallGeometryInput>(tr, pline, "WallGeometryInput", ed);
                if (wallData == null || wallData.Boundaries == null || wallData.Boundaries.Count == 0)
                {
                    ed.WriteMessage("\n未找到边界条件数据。\n");
                    return;
                }
                HyCADTool.Tools.Tools.CreateLayer("HY_Fixed", 1);
                HyCADTool.Tools.Tools.CreateLayer("HY_Hinged", 3);
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (var boundary in wallData.Boundaries)
                {
                    Line symbolicLine = new Line(boundary.Start, boundary.End);
                    switch (boundary.Fixity)
                    {
                        case BoundaryFixity.Fixed:
                            HyUtils.DrawFixedSymbol(symbolicLine, btr, tr, scale);
                            break;
                        case BoundaryFixity.Hinged:
                            HyUtils.DrawHingedSymbol(symbolicLine, btr, tr, scale);
                            break;
                        case BoundaryFixity.Free:
                            break;
                    }
                }
                tr.Commit();
            }
        }
        [CommandMethod("HYFeaRe_RefreshWallBoundaryGraphics")]
        public static void RefreshWallBoundaryGraphics()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            double scale = 50;
            var info = HyUtils.GetPolylineInfo("请选择一个用于刷新的墙体边界多段线：");
            if (info == null) return;
            var (pline, _, _, _) = info.Value;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 读取已有支座信息
                var wallData = HyUtils.ReadFromExtensionDictionary<WallGeometryInput>(tr, pline, "WallGeometryInput", ed);
                if (wallData == null || wallData.Boundaries == null || wallData.Boundaries.Count == 0)
                {
                    ed.WriteMessage("\n未找到已保存的边界条件，无法刷新。\n");
                    return;
                }
                // 创建必要图层
                HyCADTool.Tools.Tools.CreateLayer("HY_Fixed", 1);
                HyCADTool.Tools.Tools.CreateLayer("HY_Hinged", 3);
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                // 删除旧图形（条件：图层为 HY_Fixed 或 HY_Hinged，位于边界附近）
                foreach (ObjectId entId in btr)
                {
                    Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent == null || (ent.Layer != "HY_Fixed" && ent.Layer != "HY_Hinged"))
                        continue;
                    Extents3d checkBounds = new Extents3d(
                        pline.GeometricExtents.MinPoint - new Vector3d(1000, 1000, 0),
                        pline.GeometricExtents.MaxPoint + new Vector3d(1000, 1000, 0));
                    if (ent.GeometricExtents.MinPoint.X >= checkBounds.MinPoint.X &&
                        ent.GeometricExtents.MinPoint.Y >= checkBounds.MinPoint.Y &&
                        ent.GeometricExtents.MinPoint.X <= checkBounds.MaxPoint.X &&
                        ent.GeometricExtents.MinPoint.Y <= checkBounds.MaxPoint.Y)
                    {
                        ent.UpgradeOpen();
                        ent.Erase();
                    }
                }
                // 重建图形：保留 Fixity，更新几何
                int count = Math.Min(pline.NumberOfVertices, wallData.Boundaries.Count);
                for (int i = 0; i < count; i++)
                {
                    Point3d p1 = pline.GetPoint3dAt(i);
                    Point3d p2 = pline.GetPoint3dAt((i + 1) % pline.NumberOfVertices);
                    var fixity = wallData.Boundaries[i].Fixity;
                    Line baseLine = new Line(p1, p2);
                    switch (fixity)
                    {
                        case BoundaryFixity.Fixed:
                            HyUtils.DrawFixedSymbol(baseLine, btr, tr, scale);
                            break;
                        case BoundaryFixity.Hinged:
                            HyUtils.DrawHingedSymbol(baseLine, btr, tr, scale);
                            break;
                        case BoundaryFixity.Free:
                            break;
                    }
                }
                tr.Commit();
                ed.WriteMessage("\n边界条件图形已刷新。\n");
            }
        }
        [CommandMethod("HYFeaDM_DrawMeshQuads")]
        public static void DrawMeshQuads()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            var info = HyUtils.GetPolylineInfo("请选择一个已定义边界的墙体多段线：");
            if (info == null) return;
            var (pline, _, _, _) = info.Value;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var geometry = HyUtils.ReadFromExtensionDictionary<WallGeometryInput>(tr, pline, "WallGeometryInput", ed);
                if (geometry == null)
                {
                    ed.WriteMessage("\n未找到边界信息。\n");
                    return;
                }
                var panel = WallInputFactory.CreateFromGeometryInput(geometry, pline);
                int nx = 10, ny = 5;
                var elements = WallMeshGenerator.GenerateMeshFromPolyline(panel, pline, nx, ny);
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                HyCADTool.Tools.Tools.CreateLayer("HY_Mesh", 8);
                HyCADTool.Tools.Tools.CreateLayer("HY_Mesh_Anno", 1);
                HashSet<string> writtenNodes = new HashSet<string>();
                int nodeId = 1;
                foreach (var quad in elements)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var pt1 = quad.Nodes[i];
                        var pt2 = quad.Nodes[(i + 1) % 4];
                        Line line = new Line(pt1, pt2)
                        {
                            Layer = "HY_Mesh",
                            Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
                        };
                        btr.AppendEntity(line);
                        tr.AddNewlyCreatedDBObject(line, true);
                    }
                    // 添加单元编号：在中心
                    Point3d center = GetQuadCenter(quad.Nodes);
                    AddText(btr, tr, $"E{quad.Id}", center, 50, "HY_Mesh_Anno");
                    // 添加节点编号
                    for (int i = 0; i < 4; i++)
                    {
                        var pt = quad.Nodes[i];
                        string key = $"{pt.X:F3}_{pt.Y:F3}";
                        if (!writtenNodes.Contains(key))
                        {
                            AddText(btr, tr, $"N{nodeId++}", pt, 30, "HY_Mesh_Anno");
                            writtenNodes.Add(key);
                        }
                    }
                }
                tr.Commit();
                ed.WriteMessage($"\n已绘制 {elements.Count} 个网格单元及编号标注。\n");
            }
        }
        public static Model BuildModelFromWallPanel(WallPanelInput panel, List<QuadElement> quads)
        {
            double youngModulus = panel.ElasticModulus;
            double poissonRatio = panel.PoissonRatio;
            double thickness = panel.Thickness;
            double gamma = 18000; // N/m³ 土重度
            double phiRad = 30 * Math.PI / 180;
            double Ka = (1 - Math.Sin(phiRad)) / (1 + Math.Sin(phiRad)); // Rankine
            var nodeDict = new Dictionary<string, Node>();
            var model = new Model();
            int nodeId = 1;
            // 1. 添加节点
            foreach (var quad in quads)
            {
                for (int i = 0; i < 4; i++)
                {
                    var pt = quad.Nodes[i];
                    string key = $"{pt.X:F4}_{pt.Y:F4}";
                    if (!nodeDict.ContainsKey(key))
                    {
                        var node = new Node
                        {
                            ID = nodeId++,
                            X = (float)pt.X,
                            Y = (float)pt.Y
                        };
                        nodeDict[key] = node;
                        model.NodesDictionary[node.ID] = node;
                    }
                }
            }
            // 2. 添加元素
            int elementId = 1;
            var material = new ElasticMaterial2D(StressState2D.PlaneStress)
            {
                YoungModulus = youngModulus,
                PoissonRatio = poissonRatio
            };
            foreach (var quad in quads)
            {
                var element = new Element { ID = elementId++ };
                foreach (var pt in quad.Nodes)
                {
                    string key = $"{pt.X:F4}_{pt.Y:F4}";
                    element.AddNode(nodeDict[key]);
                }
                element.ElementType = new Quad4(material) { Thickness = thickness };
                model.ElementsDictionary[element.ID] = element;
            }
            // 3. 施加边界约束
            foreach (var edge in panel.Edges)
            {
                if (edge.Fixity == BoundaryFixity.Free) continue;
                foreach (var node in model.NodesDictionary.Values)
                {
                    if (IsOnEdge(node, edge.Start, edge.End, 1e-3))
                    {
                        if (edge.Fixity == BoundaryFixity.Fixed)
                        {
                            node.Constraints.Add(DOFType.X);
                            node.Constraints.Add(DOFType.Y);
                        }
                        else if (edge.Fixity == BoundaryFixity.Hinged)
                        {
                            node.Constraints.Add(DOFType.Y);
                        }
                    }
                }
            }
            // 4. 施加主动土压力（施加在竖直边）
            foreach (var edge in panel.Edges)
            {
                if (Math.Abs(edge.Start.X - edge.End.X) < 1e-3) // 竖直边
                {
                    double x = edge.Start.X;
                    foreach (var node in model.NodesDictionary.Values)
                    {
                        if (IsOnEdge(node, edge.Start, edge.End, 1e-3))
                        {
                            double sigma = Ka * gamma * node.X;
                            double Fy = -sigma * thickness;
                            model.Loads.Add(new Load
                            {
                                Node = node,
                                DOF = DOFType.Y,
                                Amount = (float)Fy
                            });
                        }
                    }
                }
            }
            model.ConnectDataStructures();
            return model;
        }
        private static bool IsOnEdge(Node node, Point3d start, Point3d end, double tol)
        {
            float px = (float)node.X;
            float py = (float)node.Y;
            float x0 = (float)start.X;
            float y0 = (float)start.Y;
            float x1 = (float)end.X;
            float y1 = (float)end.Y;
            float dx = x1 - x0;
            float dy = y1 - y0;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            float cross = Math.Abs((px - x0) * (y1 - y0) - (py - y0) * (x1 - x0));
            float dot = (px - x0) * dx + (py - y0) * dy;
            return cross / length < tol && dot >= 0 && dot <= dx * dx + dy * dy;
        }
        /// <summary>
        /// 计算四边形中心点
        /// </summary>
        private static Point3d GetQuadCenter(Point3d[] pts)
        {
            return new Point3d(
                pts.Average(p => p.X),
                pts.Average(p => p.Y),
                0);
        }
        /// <summary>
        /// 在指定位置添加文字注释（单行 MText）
        /// </summary>
        private static void AddText(BlockTableRecord btr, Transaction tr, string text, Point3d pos, double height, string layer)
        {
            MText mt = new MText
            {
                Contents = text,
                TextHeight = height,
                Location = pos,
                Layer = layer,
                Attachment = AttachmentPoint.MiddleCenter,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 1)
            };
            btr.AppendEntity(mt);
            tr.AddNewlyCreatedDBObject(mt, true);
        }
    }
}
