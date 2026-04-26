using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Misc
{
    /// <summary>
    /// 将文字对齐到对应直线左侧（按距离倍数匹配 Line↔DBText）
    /// </summary>
    public class AlignedAxisTextCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 获取距离倍数
                var distOpt = new PromptDoubleOptions("\n请输入距离倍数d [默认3.0]: ")
                {
                    DefaultValue = 3.0,
                    AllowNegative = false
                };
                var distRes = ed.GetDouble(distOpt);
                if (distRes.Status != PromptStatus.OK) return;
                double distanceFactor = distRes.Value;

                // 选择 Line + Text
                var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择直线和文字: " };
                var filter = new SelectionFilter(new[] {
                    new TypedValue((int)DxfCode.Start, "LINE,TEXT,MTEXT")
                });
                var selResult = ed.GetSelection(selOpts, filter);
                if (selResult.Status != PromptStatus.OK) return;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    // 分离线和文字
                    var lines = new List<Line>();
                    var texts = new List<DBText>();

                    foreach (ObjectId objId in selResult.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                        if (ent is Line line)
                            lines.Add(line);
                        else if (ent is DBText dbText)
                            texts.Add(dbText);
                        else if (ent is MText mText)
                        {
                            var converted = ConvertMTextToDBText(mText, db, tr, btr);
                            if (converted != null)
                            {
                                texts.Add(converted);
                                mText.Erase();
                            }
                        }
                    }

                    // 匹配 Line ↔ DBText
                    var pairs = MatchLinesWithTexts(lines, texts, distanceFactor, ed);

                    // 对齐
                    foreach (var pair in pairs)
                    {
                        var line = pair.Key;
                        var text = pair.Value;
                        if (text == null) continue;

                        Point3d startPt = line.StartPoint;
                        Point3d endPt = line.EndPoint;
                        Point3d alignPt = startPt.Y <= endPt.Y ? startPt : endPt;

                        Vector3d lineDir = endPt - startPt;
                        Vector3d perpDir = new Vector3d(-lineDir.Y, lineDir.X, 0).GetNormal().Negate();

                        text.Position = alignPt + perpDir * text.Height * 0.5;
                    }

                    tr.Commit();
                }

                ed.WriteMessage("\n处理完成");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n处理失败: {ex.Message}");
            }
        }

        private Dictionary<Line, DBText> MatchLinesWithTexts(
            List<Line> lines, List<DBText> texts, double distanceFactor, Editor ed)
        {
            var pairs = new Dictionary<Line, DBText>();
            var used = new HashSet<ObjectId>();

            foreach (var line in lines)
            {
                DBText nearest = null;
                double minDist = double.MaxValue;

                foreach (var text in texts)
                {
                    if (used.Contains(text.ObjectId)) continue;

                    double thresh = text.Height * distanceFactor;
                    Point3d closest = line.GetClosestPointTo(text.Position, false);
                    double dist = closest.DistanceTo(text.Position);

                    if (dist < thresh && dist < minDist)
                    {
                        minDist = dist;
                        nearest = text;
                    }
                }

                if (nearest != null)
                {
                    pairs[line] = nearest;
                    used.Add(nearest.ObjectId);
                }
                else
                {
                    line.ColorIndex = 1;
                    ed.WriteMessage($"\n警告：直线 {line.ObjectId} 未找到配对文字，已标红");
                }
            }

            return pairs;
        }

        private DBText ConvertMTextToDBText(MText mText, Database db, Transaction tr, BlockTableRecord btr)
        {
            try
            {
                string cleanText = Regex.Replace(mText.Contents ?? "", @"\\[^\\;]+;", "");
                cleanText = Regex.Replace(cleanText, @"\{|\}", "").Trim();

                var tsId = db.Textstyle;
                var ts = tr.GetObject(tsId, OpenMode.ForRead) as TextStyleTableRecord;

                var dbText = new DBText
                {
                    TextString = cleanText,
                    Position = mText.Location,
                    Height = mText.TextHeight,
                    Rotation = mText.Rotation,
                    Layer = mText.Layer,
                    TextStyleId = tsId,
                    WidthFactor = ts?.XScale ?? 1.0
                };

                btr.AppendEntity(dbText);
                tr.AddNewlyCreatedDBObject(dbText, true);
                return dbText;
            }
            catch (System.Exception ex)
            {
                AcApp.DocumentManager.MdiActiveDocument?.Editor
                    ?.WriteMessage($"\nMText 转换失败: {ex.Message}");
                return null;
            }
        }
    }
}
