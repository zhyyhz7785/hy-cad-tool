using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Tools;

namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("HYc2_block2leader")]

        public static void ConvertBlockToMLeader()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            // 设置选择选项，允许选择多个块参照
            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择一个或多个块参照：",
                RejectObjectsOnLockedLayers = true // 可选：拒绝锁定图层上的对象
            };

            // 设置选择过滤器，仅允许块参照
            TypedValue[] filter = new TypedValue[] { new TypedValue((int)DxfCode.Start, "INSERT") };
            SelectionFilter selFilter = new SelectionFilter(filter);

            // 获取用户选择的结果
            var psr = ed.GetSelection(pso, selFilter);
            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0) return;

            // 开始事务
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 获取模型空间
                var ms = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                // 遍历所有选中的块参照
                foreach (SelectedObject selObj in psr.Value)
                {
                    var br = (BlockReference)tr.GetObject(selObj.ObjectId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);

                    // 变换矩阵
                    Matrix3d transform = br.BlockTransform;

                    // 收集元素
                    List<Line> lines = new List<Line>();
                    List<MText> mtexts = new List<MText>();

                    foreach (ObjectId id in btr)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        if (ent is Line line)
                            lines.Add((Line)line.GetTransformedCopy(transform));
                        else if (ent is MText mtext)
                            mtexts.Add((MText)mtext.GetTransformedCopy(transform));
                    }

                    if (lines.Count != 2)
                    {
                        ed.WriteMessage($"\n块 {br.Name} 中必须包含两根线段，跳过此块。");
                        continue; // 跳过不符合条件的块
                    }

                    // 找到非水平直线（Y坐标差异较大）
                    Line targetLine = lines.FirstOrDefault(l => Math.Abs(l.StartPoint.Y - l.EndPoint.Y) > Tolerance.Global.EqualPoint);
                    if (targetLine == null)
                    {
                        ed.WriteMessage($"\n块 {br.Name} 中未找到非水平线，跳过此块。");
                        continue; // 跳过不符合条件的块
                    }

                    // 找到另一根直线
                    Line otherLine = lines.First(l => l != targetLine);

                    // 比较端点是否与另一条线段端点重合
                    Point3d p1 = Point3d.Origin, p2 = Point3d.Origin, p3 = Point3d.Origin;

                    var pts1 = new[] { targetLine.StartPoint, targetLine.EndPoint };
                    var pts2 = new[] { otherLine.StartPoint, otherLine.EndPoint };

                    if (pts1[0].IsEqualTo(pts2[0], Tolerance.Global) || pts1[0].IsEqualTo(pts2[1], Tolerance.Global))
                    {
                        p1 = pts1[1]; // 不重合点
                        p2 = pts1[0]; // 重合点
                    }
                    else
                    {
                        p1 = pts1[0];
                        p2 = pts1[1];
                    }

                    // 确定第三点 p3
                    p3 = pts2[0].IsEqualTo(p2, Tolerance.Global) ? pts2[1] : pts2[0];

                    // 获取内容并排序
                    var orderedTexts = mtexts.OrderByDescending(m => m.Location.Y).ToList();
                    string combinedContent = string.Join("\\P", orderedTexts.Select(m => m.Contents)); // MText多行内容需使用 \P

                    // 使用您提供的方法生成 MLeader
                    MLeader mleader =EtGpt.AddMleaderSinglePoint(p1, p2, combinedContent);

                    // 添加到模型空间
                    ms.AppendEntity(mleader);
                    tr.AddNewlyCreatedDBObject(mleader, true);
                }

                tr.Commit();
            }

            ed.WriteMessage("\n所有符合条件的块已转换为 MLeader。");
        }



    }
}