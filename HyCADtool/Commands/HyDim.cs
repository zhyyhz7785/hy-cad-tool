using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Annotation;
using HyCADTool.Models;
using System.Collections.Generic;
using HyCADTool.Models.Cluster;
namespace HyCADTool.Commands
{
    public static partial class HyCommand // ✅ 添加 partial
    {
        [CommandMethod("HY_AnnotateClusters")]
        public static void AnnotateClusters()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            // 获取比例参数
            PromptDoubleOptions opts = new PromptDoubleOptions("\n请输入标注比例系数（例如 1:50 输入 0.02）：");
            opts.DefaultValue = 0.02;
            opts.AllowZero = false;
            opts.AllowNegative = false;
            PromptDoubleResult result = ed.GetDouble(opts);
            if (result.Status != PromptStatus.OK) return;
            double scale = result.Value;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                // 显式指定类型（C# 7.3）
                List<ClusterResult> clusters = new List<ClusterResult>();
                List<Line> xAxes = new List<Line>();
                List<Line> yAxes = new List<Line>();
                // 示例自动扫描轴线（线段）
                foreach (ObjectId id in btr)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    Line line = ent as Line;
                    if (line != null)
                    {
                        if (line.StartPoint.Y == line.EndPoint.Y)
                            xAxes.Add(line);
                        else if (line.StartPoint.X == line.EndPoint.X)
                            yAxes.Add(line);
                    }
                }
                // 示例：添加一个虚拟的 ClusterResult（用于测试）
                Extents3d testExt = new Extents3d(new Point3d(1000, 1000, 0), new Point3d(2000, 1500, 0));
                ClusterResult dummy = new ClusterResult();
                dummy.EnvelopeExtents = testExt;
                clusters.Add(dummy);
                tr.Commit();
                AnnotationFramework framework = new AnnotationFramework();
                framework.Run(clusters, xAxes, yAxes, scale);
            }
        }
    }
}
