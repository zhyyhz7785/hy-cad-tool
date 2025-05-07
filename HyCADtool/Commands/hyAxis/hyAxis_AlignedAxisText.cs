using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Utils;
using System.Collections.Generic;
using static HyCADTool.Utils.HyCADUtils;

namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("hyAxis_AlignedAxisText")]
        public static void AlignTextToLineByDistance()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // 获取距离因子
                double distanceFactor = HyCADUtils.GetDistanceFactorFromUser(ed);
                if (distanceFactor <= 0) return;

                // 获取选择
                ObjectId[] selectedIds = HyCADUtils.GetLineAndTextSelection(ed);
                if (selectedIds == null) return;

                // 处理实体
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    EntityCollection entities = HyCADUtils.SeparateEntities(selectedIds, db, tr);
                    Dictionary<Line, DBText> lineTextPairs = HyCADUtils.MatchLinesWithTexts(
                        entities.Lines, entities.Texts, distanceFactor, ed);
                    HyCADUtils.AlignTextsToLines(lineTextPairs);
                    tr.Commit();
                }

                ed.WriteMessage("\n处理完成！已将符合距离条件的文字对齐到对应直线左侧。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}