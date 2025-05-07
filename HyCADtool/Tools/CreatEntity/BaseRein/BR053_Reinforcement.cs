using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        public static void ReinforcementAll(
            Dictionary<Polyline, (int Count, double Min, double Max, double Average, double StdDev, double additionalRebarArea)> keyValuePairs)
        {
            var (doc, db, ed) = InitializeCadEnvironment();
            if (keyValuePairs == null || keyValuePairs.Count == 0)
            {
                ed.WriteMessage("\n请重新选择");
                return;
            }
            Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev, double additionalRebarArea)> updatedKeyValuePairs = new
            Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev, double additionalRebarArea)>();
            List<ObjectId> toBeErased = new List<ObjectId>();
            //生成配筋，更新配筋区域
            using (DocumentLock docLock = doc.LockDocument()) // 添加文档锁
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var kvp in keyValuePairs)
                    {
                        ProcessPolylineAll(tr, kvp.Key, kvp.Value, updatedKeyValuePairs, toBeErased);
                    }
                    // AddTableToDrawing(tr, updatedKeyValuePairs, Scale);
                    //标注完一个表格后
                    tr.Commit();
                }
            }
            //删除旧配筋区域
            toBeErased.DeleteByObjectIDs(doc, db);
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                UpdatePolylineDictionary(tr, updatedKeyValuePairs, keyValuePairs);
            }
        }
        private static void ProcessPolylineAll(
           Transaction tr,
           Polyline polyline, (int Count, double Min, double Max, double Average, double StdDev, double additionalRebarArea) stats,
           Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev, double additionalRebarArea)> updatedKeyValuePairs,
           List<ObjectId> toBeErased)
        {
            //钢筋面积（厘米转换为毫米）
            double maxArea = stats.Max * 100 * ReinforceSafety;
            //得到单根钢筋面积
            double singleRebarArea = Math.PI * Math.Pow(RebarDiameter / 2, 2);
            double customerArea;
            if (ExistingRebar == false)
            {
                customerArea = 0;
            }
            else
            {
                customerArea = 1000 / RebarSpacing * singleRebarArea;
            }
            double additionalArea = 0;
            double additionalDiameter = MinAdditionalDiameter;
            double additionalSpacing = AdditionalSpacing;
            //循环找到合理配筋
            foreach (var dia in AllowedDiameters)
            {
                additionalDiameter = dia;
                double additionalSingleRebarArea = Math.PI * Math.Pow(additionalDiameter / 2, 2);
                additionalArea = 1000 / additionalSpacing * additionalSingleRebarArea;
                if (additionalArea + customerArea > maxArea)
                {
                    break;
                }
            }
            Polyline newPolyline = (Polyline)polyline.Clone();
            //画出钢筋，添加
            var a = AnchorFactor;
            AnchorFactor = 0;
            Direction = RebarDirection.TopX;
            DrawRebarsInXDirection(tr, newPolyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
            Direction = RebarDirection.TopY;
            DrawRebarsInYDirection(tr, newPolyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
            Direction = RebarDirection.BottomX;
            DrawRebarsInXDirectionButton(tr, newPolyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
            Direction = RebarDirection.BottomY;
            DrawRebarsInYDirectionButton(tr, newPolyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
            AnchorFactor = a;
        }
    }
}
