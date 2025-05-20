using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
// NetTopologySuite
using NetTopologySuite.Geometries;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
        [CommandMethod("GPT_ADAPTIVE_GRID")]
        public static void CreateAdaptiveGrid()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 1. 选择闭合多段线
            PromptEntityOptions peo = new PromptEntityOptions(
                "\n请选择一个闭合Polyline:"
            );
            peo.SetRejectMessage("\n对象必须是多段线。\n");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;
            ObjectId plId = per.ObjectId;
            // 2. 读取多段线并转换为 NTS Polygon
            Polygon ntsPolygon = null;
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                Polyline pl = tr.GetObject(plId, OpenMode.ForRead) as Polyline;
                if (pl == null)
                {
                    ed.WriteMessage("\n选取对象不是多段线，命令结束。");
                    return;
                }
                if (!pl.Closed)
                {
                    ed.WriteMessage("\n多段线未闭合，命令结束。");
                    return;
                }
                ntsPolygon = ConvertPolylineToNTSPolygon(pl);
                tr.Commit();
            }
            if (ntsPolygon == null)
            {
                ed.WriteMessage("\n转换多段线为 NTS Polygon 失败。");
                return;
            }
            // 3. 使用简化版 Quadtree 进行自适应划分
            var env = ntsPolygon.EnvelopeInternal;  // bounding box
            double xMin = env.MinX;
            double xMax = env.MaxX;
            double yMin = env.MinY;
            double yMax = env.MaxY;
            // 设定最大深度、最小网格尺寸 (视需求而定)
            int maxDepth = 6;
            double minCellSize = (xMax - xMin) / 200; // 例如将网格细分到1/200范围，可自调
            var quadtree = new SimpleQuadtree(
                 xMin, xMax, yMin, yMax,
                 maxDepth, minCellSize,
                 node => CheckIntersection(ntsPolygon, node)
             );
            // 4. 获取叶子节点并在 AutoCAD 中绘制
            List<QuadtreeNode> leaves = quadtree.GetLeafNodes();
            DrawAdaptiveGrid(leaves, ntsPolygon, doc);
            ed.WriteMessage($"\n自适应网格划分完成，共生成 {leaves.Count} 个子单元(含Outside)。");
        }
    }
}
