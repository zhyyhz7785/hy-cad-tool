using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.BlockLeader
{
    /// <summary>
    /// 将块参照（含 2 根线 + MText）转换为 MLeader（HYc2_block2leader）
    /// </summary>
    public class BlockToMLeaderCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择一个或多个块参照：",
                RejectObjectsOnLockedLayers = true
            };
            var filter = new SelectionFilter(new[] {
                new TypedValue((int)DxfCode.Start, "INSERT")
            });
            var psr = ed.GetSelection(pso, filter);
            if (psr.Status != PromptStatus.OK) return;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ms = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                    foreach (SelectedObject selObj in psr.Value)
                    {
                        var br = (BlockReference)tr.GetObject(selObj.ObjectId, OpenMode.ForRead);
                        var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                        var transform = br.BlockTransform;

                        var lines = new List<Line>();
                        var mtexts = new List<MText>();

                        foreach (ObjectId id in btr)
                        {
                            var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent is Line line)
                                lines.Add((Line)line.GetTransformedCopy(transform));
                            else if (ent is MText mt)
                                mtexts.Add((MText)mt.GetTransformedCopy(transform));
                        }

                        if (lines.Count != 2)
                        {
                            ed.WriteMessage($"\n块 {br.Name} 需要 2 根线段，跳过");
                            continue;
                        }

                        // 找非水平线
                        var targetLine = lines.FirstOrDefault(l =>
                            Math.Abs(l.StartPoint.Y - l.EndPoint.Y) > Tolerance.Global.EqualPoint);
                        if (targetLine == null)
                        {
                            ed.WriteMessage($"\n块 {br.Name} 未找到非水平线，跳过");
                            continue;
                        }

                        var otherLine = lines.First(l => l != targetLine);

                        // 确定 p1(不重合端), p2(重合端)
                        var pts1 = new[] { targetLine.StartPoint, targetLine.EndPoint };
                        var pts2 = new[] { otherLine.StartPoint, otherLine.EndPoint };

                        Point3d p1, p2;
                        if (pts1[0].IsEqualTo(pts2[0], Tolerance.Global) ||
                            pts1[0].IsEqualTo(pts2[1], Tolerance.Global))
                        {
                            p1 = pts1[1];
                            p2 = pts1[0];
                        }
                        else
                        {
                            p1 = pts1[0];
                            p2 = pts1[1];
                        }

                        // 合并文字
                        var ordered = mtexts.OrderByDescending(m => m.Location.Y).ToList();
                        string content = string.Join("\\P", ordered.Select(m => m.Contents));

                        // 创建 MLeader
                        var mleader = CreateSinglePointMLeader(p1, p2, content, db);
                        ms.AppendEntity(mleader);
                        tr.AddNewlyCreatedDBObject(mleader, true);
                    }

                    tr.Commit();
                }

                ed.WriteMessage("\n块转 MLeader 完成");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n转换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建单点引线 MLeader（等效旧 AddMleaderSinglePoint）
        /// </summary>
        private MLeader CreateSinglePointMLeader(Point3d startPt, Point3d endPt, string content, Database db)
        {
            var ml = new MLeader();
            ml.MLeaderStyle = db.MLeaderstyle;
            ml.EnableDogleg = true;
            ml.DoglegLength = 2.5;

            var mt = new MText
            {
                TextStyleId = ml.TextStyleId,
                Color = ml.TextColor,
                TextHeight = ml.TextHeight,
                Contents = $"\\W0.7;{content}",
                Attachment = AttachmentPoint.TopLeft,
                Location = endPt
            };

            int leaderIdx = ml.AddLeader();
            int lineIdx = ml.AddLeaderLine(leaderIdx);
            ml.AddFirstVertex(lineIdx, startPt);
            ml.AddLastVertex(lineIdx, endPt);
            ml.MText = mt;

            return ml;
        }
    }
}
