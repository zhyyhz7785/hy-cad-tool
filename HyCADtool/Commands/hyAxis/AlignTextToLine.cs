using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace HyCADTool.Utils
{
    public static class HyCADUtils
    {
        #region 用户交互方法
        public static double GetDistanceFactorFromUser(Editor ed, double defaultValue = 3.0)
        {
            PromptDoubleOptions distOpt = new PromptDoubleOptions("\n请输入距离倍数d [默认3.0]: ")
            {
                DefaultValue = defaultValue,
                AllowNegative = false
            };
            PromptDoubleResult distRes = ed.GetDouble(distOpt);
            return distRes.Status == PromptStatus.OK ? distRes.Value : 0;
        }
        public static ObjectId[] GetLineAndTextSelection(Editor ed)
        {
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择直线和文字: "
            };
            TypedValue[] filter = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, "LINE,TEXT,MTEXT")
            };
            SelectionFilter sf = new SelectionFilter(filter);
            PromptSelectionResult selResult = ed.GetSelection(selOpts, sf);
            return selResult.Status == PromptStatus.OK ? selResult.Value.GetObjectIds() : null;
        }
        #endregion
        #region 实体处理
        public static EntityCollection SeparateEntities(ObjectId[] selectedIds, Database db, Transaction tr)
        {
            List<Line> lines = new List<Line>();
            List<DBText> texts = new List<DBText>();
            foreach (ObjectId objId in selectedIds)
            {
                Entity ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                if (ent is Line line)
                {
                    lines.Add(line);
                }
                else if (ent is DBText dbText)
                {
                    texts.Add(dbText);
                }
                else if (ent is MText mText)
                {
                    DBText convertedText = ConvertMTextToDBText(mText, db, tr);
                    if (convertedText != null)
                    {
                        texts.Add(convertedText);
                        mText.Erase();
                    }
                }
            }
            return new EntityCollection { Lines = lines, Texts = texts };
        }
        public static Dictionary<Line, DBText> MatchLinesWithTexts(List<Line> lines, List<DBText> texts,
            double distanceFactor, Editor ed)
        {
            Dictionary<Line, DBText> lineTextPairs = new Dictionary<Line, DBText>();
            HashSet<ObjectId> processedTexts = new HashSet<ObjectId>();
            foreach (Line line in lines)
            {
                DBText nearestText = null;
                double minDistance = double.MaxValue;
                foreach (DBText text in texts)
                {
                    if (processedTexts.Contains(text.ObjectId)) continue;
                    double textHeight = text.Height;
                    double threshold = textHeight * distanceFactor;
                    double distance = CalculateDistance(line, text.Position);
                    if (distance < threshold && distance < minDistance)
                    {
                        minDistance = distance;
                        nearestText = text;
                    }
                }
                if (nearestText != null)
                {
                    lineTextPairs[line] = nearestText;
                    processedTexts.Add(nearestText.ObjectId);
                }
                else
                {
                    line.ColorIndex = 1;
                    ed.WriteMessage($"\n警告：直线 {line.ObjectId} 未找到配对文字，已标记为红色。");
                }
            }
            return lineTextPairs;
        }
        public static void AlignTextsToLines(Dictionary<Line, DBText> lineTextPairs)
        {
            foreach (var pair in lineTextPairs)
            {
                Line line = pair.Key;
                DBText text = pair.Value;
                if (text == null) continue;
                Point3d startPt = line.StartPoint;
                Point3d endPt = line.EndPoint;
                Point3d alignPt = startPt.Y <= endPt.Y ? startPt : endPt;
                Vector3d lineDir = endPt - startPt;
                Vector3d perpDir = GetPerpendicularVector(lineDir, true);
                double textHeight = text.Height;
                Point3d newPos = alignPt + perpDir * textHeight * 0.5;
                text.Position = newPos;
            }
        }
        #endregion
        #region 几何计算
        public static double CalculateDistance(Line line, Point3d point)
        {
            Point3d closestPt = line.GetClosestPointTo(point, false);
            return closestPt.DistanceTo(point);
        }
        public static Vector3d GetPerpendicularVector(Vector3d vector, bool toLeft = false)
        {
            Vector3d perpVector = new Vector3d(-vector.Y, vector.X, 0);
            perpVector = perpVector.GetNormal();
            return toLeft ? perpVector.Negate() : perpVector;
        }
        #endregion
        #region 类型转换
        public static DBText ConvertMTextToDBText(MText mText, Database db, Transaction tr)
        {
            try
            {
                string cleanText = CleanMTextFormatting(mText.Contents);
                ObjectId textStyleId = db.Textstyle;
                TextStyleTableRecord ts = tr.GetObject(textStyleId, OpenMode.ForRead) as TextStyleTableRecord;
                DBText dbText = new DBText
                {
                    TextString = cleanText,
                    Position = mText.Location,
                    Height = mText.TextHeight,
                    Rotation = mText.Rotation,
                    Layer = mText.Layer,
                    TextStyleId = textStyleId,
                    WidthFactor = ts?.XScale ?? 1.0
                };
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                btr.AppendEntity(dbText);
                tr.AddNewlyCreatedDBObject(dbText, true);
                return dbText;
            }
            catch (Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(
                    $"\n转换 MText 到 DBText 时出错: {ex.Message}");
                return null;
            }
        }
        public static string CleanMTextFormatting(string mTextContents)
        {
            if (string.IsNullOrEmpty(mTextContents)) return string.Empty;
            string cleanText = mTextContents;
            cleanText = Regex.Replace(cleanText, @"\\[^\\;]+;", "");
            cleanText = Regex.Replace(cleanText, @"\{|\}", "");
            return cleanText.Trim();
        }
        #endregion
        #region 辅助类
        public class EntityCollection
        {
            public List<Line> Lines { get; set; } = new List<Line>();
            public List<DBText> Texts { get; set; } = new List<DBText>();
        }
        #endregion
    }
}